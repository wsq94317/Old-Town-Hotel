using System.Collections.Generic;
using Godot;

// 一名在楼层里走动的员工。用 Godot 的 NavigationAgent3D 在本层导航网格上寻路。
//
// 目标由 **Sim 决定**，不是随机游荡：去本层第一间需要处理的房
// （客房组看 Dirty，巡检组看 AwaitingInspection）。没活干就回岗位待命。
// 这样屏幕上看到的走位是真实运营状态的表现，而不是装饰性动画。
//
// 只在本层活动。跨楼层在 Unity 那边是 NavMeshAgent.Warp 传送（StairZone /
// ElevatorController），不走导航网格；等电梯移植过来再接，现在不假装能跨层。
public partial class StaffWalker : CharacterBody3D
{
    private const float Speed = 2.2f;
    private const float ArriveRadius = 0.6f;

    private NavigationAgent3D _agent;
    private MeshInstance3D _body;
    private Label3D _tag;
    private StandardMaterial3D _material;

    private HotelSim _sim;
    private int _floor;
    // 可能有活的房态。客房组给两个（Dirty=等着打扫，Cleaning=正在打扫）：
    // SimPipeline 是按人力池算的，不记录谁在哪间房，所以这个 walker 代表的是**池子**
    // 而不是某一个具体员工——和 FrontDesk 员工卡同一个口径，不假装有派工系统。
    // 只盯 Dirty 的话，脏房在两次采样之间就被清完了，屏幕上永远看不到人动。
    private RoomSimState[] _wants;
    private Vector3 _home;
    // 用 -2 而不是 -1 做初值：-1 是「没活干」的合法取值，两者相同的话
    // 第一次 RetargetFromSim 会当成「目标没变」直接早退，TargetPosition 就永远停在
    // 默认的 (0,0,0)——员工会一动不动，而症状看起来像寻路没生效。
    private int _targetRoom = -2;

    private readonly Dictionary<int, Vector3> _roomPositions = new Dictionary<int, Vector3>();

    /// <summary>
    /// 把一个世界点吸到最近的导航网格点上。
    ///
    /// ⚠️ 已知未解问题：MapGetClosestPoint 在本工程里对**任何**输入都返回 (0,0,0)，
    /// 尽管地图报告 7 个已激活区域、每层 110-150 个多边形。试过的都没用：
    /// 对齐 map/mesh 的 cell_size 与 cell_height、MapForceUpdate、延后一个物理帧、
    /// 用 RegionSetNavigationMesh 直接向服务器推网格。
    ///
    /// 所以这里对明显无效的结果（吸到原点、或移动超过 4 米）**退回原始点**。
    /// 员工因此停在岗位不动，但至少站在楼里而不是被拽到世界原点——
    /// 后者会让整个场景看起来像坐标换算错了，掩盖真正的问题。
    /// </summary>
    private Vector3 SnapToNav(Vector3 p)
    {
        Rid map = GetWorld3D().NavigationMap;
        Vector3 snapped = NavigationServer3D.MapGetClosestPoint(map, p);

        bool bogus = snapped == Vector3.Zero || snapped.DistanceTo(p) > 4f;
        if (bogus)
        {
            GD.PushWarning($"[nav] {Name}: nav query unusable for {p} (got {snapped}); " +
                           $"keeping the raw point. Pathfinding is not working yet.");
            return p;
        }
        return snapped;
    }

    /// <summary>诊断用：一张静态截图看不出「他在往哪走、走到了没有」。</summary>
    public string Describe() =>
        $"{Name}: target={(_targetRoom >= 0 ? _targetRoom.ToString() : "idle")} " +
        $"pos=({GlobalPosition.X:0.0},{GlobalPosition.Z:0.0}) " +
        $"remaining={(_agent != null ? _agent.DistanceToTarget() : 0f):0.00} " +
        $"reachable={(_agent != null && _agent.IsTargetReachable())}";

    public void Configure(HotelSim sim, int floor, RoomSimState[] wants, Color colour, string label,
                          Vector3 home, IEnumerable<HotelGeometry.RoomAnchor> anchors)
    {
        _sim = sim;
        _floor = floor;
        _wants = wants;

        // 目标点必须吸附到导航网格上。岗位点是按楼层几何中心算的、房间锚点是房间中心，
        // 两者都不保证落在可走面上——落在墙里的话 IsTargetReachable() 恒 false，
        // 员工一步都不会走，而症状看起来像「寻路没接上」。
        _home = SnapToNav(home);
        foreach (var a in anchors)
            if (a.Floor == floor) _roomPositions[a.Number] = SnapToNav(a.Position);

        // 自己也站到网格上，否则起点离网格太远时 agent 同样算不出路径
        GlobalPosition = _home;

        _material.AlbedoColor = colour;
        _tag.Text = label;

        RetargetFromSim();   // 立刻定一次目标，别等第一次节流刷新
    }

    public override void _Ready()
    {
        _agent = new NavigationAgent3D
        {
            PathDesiredDistance = 0.4f,
            TargetDesiredDistance = ArriveRadius,
            // 高度偏移：导航网格贴地，胶囊的原点在中心，不抬起来脚会陷进地板
            PathHeightOffset = 0.0f,
            AvoidanceEnabled = false,   // 单人先不开避让，等多人同层再说
        };
        AddChild(_agent);

        _material = new StandardMaterial3D { AlbedoColor = Colors.Magenta, Roughness = 0.7f };
        _body = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.28f, Height = 1.7f },
            MaterialOverride = _material,
            Position = new Vector3(0, 0.85f, 0),   // 胶囊中心抬到半高，脚落在 y=0
        };
        AddChild(_body);

        // CharacterBody3D 没有碰撞体时 MoveAndSlide 不会正常推进——Godot 只会给一条
        // 配置警告，人却站着不动，看起来像寻路失败。形状和胶囊外观保持一致。
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.28f, Height = 1.7f },
            Position = new Vector3(0, 0.85f, 0),
        });

        _tag = new Label3D
        {
            Text = "",
            FontSize = 48,
            PixelSize = 0.004f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            OutlineSize = 16,
            OutlineModulate = new Color(0, 0, 0, 0.8f),
            Position = new Vector3(0, 2.1f, 0),
        };
        AddChild(_tag);
    }

    /// <summary>重新挑目标。由外部按低频调用——每帧重算路径既浪费又会让走位抖。</summary>
    public void RetargetFromSim()
    {
        if (_sim == null) return;

        // 本层第一间需要处理的房。RoomNumbersInState 返回的是全楼的，按本层过滤。
        int pick = -1;
        foreach (RoomSimState want in _wants)
        {
            foreach (int n in _sim.Rooms.RoomNumbersInState(want, 8))
                if (_roomPositions.ContainsKey(n)) { pick = n; break; }
            if (pick >= 0) break;
        }

        if (pick == _targetRoom) return;
        _targetRoom = pick;

        Vector3 dest = pick >= 0 ? _roomPositions[pick] : _home;
        _agent.TargetPosition = dest;
        _tag.Modulate = pick >= 0 ? Colors.White : new Color(1, 1, 1, 0.45f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_agent == null || _agent.IsNavigationFinished())
        {
            Velocity = Vector3.Zero;
            return;
        }

        Vector3 next = _agent.GetNextPathPosition();
        Vector3 dir = (next - GlobalPosition);
        dir.Y = 0f;
        if (dir.LengthSquared() < 0.0001f) { Velocity = Vector3.Zero; return; }

        Velocity = dir.Normalized() * Speed;
        MoveAndSlide();
    }
}
