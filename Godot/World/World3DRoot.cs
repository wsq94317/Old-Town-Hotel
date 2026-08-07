using Godot;

// 3D 世界宿主：几何 + 光 + 相机 + 楼层显隐。还没有玩法和寻路。
//
// 操作：
//   左键拖拽     平移（进入自由视角）
//   滚轮         缩放
//   1..7         跳到指定楼层
//   Q / E        下一层 / 上一层
//   Tab          切换「单层」和「剖面总览」
//
// 跑法：
//   <godot> --path Godot res://World/World3D.tscn
//   <godot> --path Godot res://World/World3D.tscn -- --floor=7 --shot=3
//   <godot> --path Godot res://World/World3D.tscn -- --overview --shot=3
public partial class World3DRoot : Node3D
{
    private HotelGeometry _geometry;
    private FloorVisibility _floors;
    private ManagerCamera _camera;
    private WorldSimHost _sim;
    private RoomMarkers _markers;
    private FloorNavBaker _nav;
    private readonly System.Collections.Generic.List<StaffWalker> _staff = new System.Collections.Generic.List<StaffWalker>();
    private double _markerAccumulator;

    private double _shotAfter = -1;
    private bool _shotTaken;
    private bool _overview;

    // Unity 那台相机的正交半高是 6（Godot 全高 12），但那是「跟着经理走」的贴身视角。
    // Godot 这边经理还没移植，用同样的 12 只能看见楼层的一个角。改成按楼层实际范围取景，
    // 等经理接上之后再切回贴身跟随。
    private float _floorZoom = 12f;
    private float _overviewZoom = 40f;
    private Vector3 _hotelCentre;
    private readonly System.Collections.Generic.List<Vector3> _floorCentres = new System.Collections.Generic.List<Vector3>();

    public override void _Ready()
    {
        int startFloor = 0;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--shot=")) _shotAfter = arg.Substring(7).ToFloat();
            else if (arg.StartsWith("--floor=")) startFloor = arg.Substring(8).ToInt() - 1;
            else if (arg == "--overview") _overview = true;
        }

        _geometry = new HotelGeometry { Name = "Hotel" };
        AddChild(_geometry);

        _floors = new FloorVisibility { Name = "FloorVisibility" };
        AddChild(_floors);
        _floors.Bind(_geometry.Floors);

        // Sim 用**场景锚点**建房，和 Unity 侧 HotelSimSceneBridge 同一个原则：
        // 用真实的房建内核，不另造一套假房表。
        _sim = new WorldSimHost { Name = "Sim" };
        AddChild(_sim);
        _sim.Build(_geometry.RoomAnchors);

        _nav = new FloorNavBaker { Name = "NavBaker" };
        AddChild(_nav);
        _nav.BakeAll(_geometry.NavRegions);
        for (int i = 0; i < _geometry.NavRegions.Count; i++)
        {
            int polys = FloorNavBaker.PolygonCount(_geometry.NavRegions[i]);
            // 0 个多边形 = 这层完全走不了。宁可在启动时喊出来，也不要等到
            // 「agent 站着不动」再去猜是寻路挂了还是目标点给错了。
            if (polys == 0) GD.PushWarning($"[nav] Floor{i + 1} baked 0 polygons — nothing walkable");
            else GD.Print($"[nav] Floor{i + 1}: {polys} polygons");
        }

        _markers = new RoomMarkers { Name = "RoomMarkers" };
        AddChild(_markers);
        _markers.Build(_geometry.RoomAnchors, _geometry.Floors);
        _markers.Refresh(_sim.Sim);

        BuildLighting();

        Aabb bounds = ComputeBounds(_geometry);
        _hotelCentre = bounds.GetCenter();
        _overviewZoom = FitOrthoSize(bounds);

        // 每层单独算中心和取景。各层 footprint 并不一样（Floor5 只有 119 个零件、
        // Floor3 有 242），统一对准整栋楼的中心会让小楼层偏到画面角落。
        float widest = 0f;
        foreach (var floor in _geometry.Floors)
        {
            Aabb fb = ComputeBounds(floor);
            _floorCentres.Add(fb.Size == Vector3.Zero ? _hotelCentre : fb.GetCenter());
            if (fb.Size == Vector3.Zero) continue;
            // 压扁 Y：单层取景该由平面尺寸决定，不该被层内高度左右
            var flat = new Aabb(new Vector3(fb.Position.X, 0, fb.Position.Z),
                                new Vector3(fb.Size.X, 4f, fb.Size.Z));
            widest = Mathf.Max(widest, FitOrthoSize(flat));
        }
        // 所有楼层共用同一个缩放：切层时画面大小跳变比楼层偏移更让人晕。
        _floorZoom = widest > 0f ? widest : 12f;

        // 员工生成必须**等一个物理帧**再做。
        //
        // NavigationServer3D 是异步的：region 注册进地图后要到物理帧末尾才真正可查询。
        // 在 _Ready 里直接调 MapGetClosestPoint，会把每一个点都返回成 (0,0,0)——
        // 地图在查询者看来是空的，尽管 MapGetRegions 已经报了 7 个区域、每层都有上百个
        // 多边形。MapForceUpdate 也救不回来。那个症状极具误导性：员工全被吸到世界原点，
        // 看起来像坐标换算错了，实际是问早了一帧。
        //
        // 也顺带解释了为什么岗位必须按楼层中心算完之后再摆——两个前置条件叠在这里。
        CallDeferred(nameof(SpawnStaffDeferred));

        _camera = new ManagerCamera { Name = "Camera" };
        AddChild(_camera);
        // 世界边界用几何实际范围，不照抄 Unity 那组 X[-10,10]/Z[-6,6]——
        // 那是按当时的楼层尺寸定的，而导出的酒店 X 跨度有 30。
        _camera.SetWorldBounds(
            new Vector2(bounds.Position.X, bounds.Position.Z),
            new Vector2(bounds.End.X, bounds.End.Z));

        _floors.FloorChanged += idx =>
        {
            _camera.OnFloorChanged(idx);
            GD.Print($"[world] floor {idx + 1}");
        };

        if (_overview) EnterOverview();
        else { _floors.ShowFloor(startFloor); ExitOverview(startFloor); }

        if (_shotAfter > 0) GetViewport().GuiDisableInput = true;   // 截图必须可复现
    }

    /// <summary>
    /// 每个有客房的楼层放一名客房清洁和一名巡检。
    ///
    /// 只在有房间锚点的层放人——把人扔到泳池层没有意义，而且那层没有它们要处理的房。
    /// 员工挂在所属楼层节点下，楼层显隐自动带上，不需要另写显隐规则。
    /// 跨楼层不做：Unity 那边是 Warp 传送（StairZone / ElevatorController），
    /// 等电梯移植过来再接，现在不假装能跨层。
    /// </summary>
    private async void SpawnStaffDeferred()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        SpawnStaff();
    }

    private void SpawnStaff()
    {
        var floorsWithRooms = new System.Collections.Generic.HashSet<int>();
        foreach (var a in _geometry.RoomAnchors) floorsWithRooms.Add(a.Floor);

        foreach (int floor in floorsWithRooms)
        {
            if (floor < 0 || floor >= _geometry.Floors.Count) continue;
            Node3D parent = _geometry.Floors[floor];
            Vector3 baseY = new Vector3(0, FloorMath.BaseYFor(floor), 0);

            Add(parent, baseY, floor,
                new[] { RoomSimState.Dirty, RoomSimState.Cleaning },
                RoomStateUi.Dirty, "HSK", new Vector3(-1.5f, 0, 0));
            Add(parent, baseY, floor,
                new[] { RoomSimState.AwaitingInspection },
                RoomStateUi.Insp, "INSP", new Vector3(1.5f, 0, 0));
        }

        void Add(Node3D parent, Vector3 baseY, int floor, RoomSimState[] wants, Color colour,
                 string label, Vector3 postOffset)
        {
            // 岗位设在该层的几何中心附近——走廊通常在中间，落在导航网格上的概率最高。
            Vector3 home = new Vector3(_floorCentres[floor].X, FloorMath.BaseYFor(floor), _floorCentres[floor].Z) + postOffset;

            var w = new StaffWalker { Name = $"{label}_F{floor + 1}" };
            parent.AddChild(w);
            w.GlobalPosition = home;
            w.Configure(_sim.Sim, floor, wants, colour, label, home, _geometry.RoomAnchors);
            _staff.Add(w);
        }

        GD.Print($"[world] {_staff.Count} staff walkers on {floorsWithRooms.Count} floors");
    }

    private void BuildLighting()
    {
        // Unity 是 URP 平行光，欧拉角 (50, 330, 0)。Godot 的 -Z 是前方、旋转约定也不同，
        // 角度不能照抄；这里按同样的「右后上方打下来」重新摆，观感对齐即可。
        var sun = new DirectionalLight3D { Name = "Sun", LightEnergy = 1.1f, ShadowEnabled = true };
        sun.RotationDegrees = new Vector3(-50, 30, 0);
        AddChild(sun);

        AddChild(new WorldEnvironment
        {
            Name = "Env",
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("#2B2118"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("#6B5840"),
                AmbientLightEnergy = 0.45f,
            },
        });
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode >= Key.Key1 && k.Keycode <= Key.Key7)
            {
                int idx = (int)(k.Keycode - Key.Key1);
                _floors.ShowFloor(idx);
                ExitOverview(idx);
            }
            else if (k.Keycode == Key.Q) { _floors.Step(-1); ExitOverview(_floors.CurrentFloor); }
            else if (k.Keycode == Key.E) { _floors.Step(+1); ExitOverview(_floors.CurrentFloor); }
            else if (k.Keycode == Key.Tab)
            {
                if (_overview) { _floors.ShowFloor(_floors.CurrentFloor); ExitOverview(_floors.CurrentFloor); }
                else EnterOverview();
            }
        }
        else if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            // 缩放改的是正交 Size，不是相机距离——正交投影里移动相机不改变画面大小。
            if (mb.ButtonIndex == MouseButton.WheelUp) _camera.Size = Mathf.Max(4f, _camera.Size * 0.9f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) _camera.Size = Mathf.Min(200f, _camera.Size * 1.1f);
        }
    }

    private void EnterOverview()
    {
        _overview = true;
        _floors.ShowAll();
        _camera.Size = _overviewZoom;
        _camera.FocusOn(_hotelCentre, instant: true);
    }

    private void ExitOverview(int floorIndex)
    {
        _overview = false;
        _camera.Size = _floorZoom;
        Vector3 c = floorIndex >= 0 && floorIndex < _floorCentres.Count ? _floorCentres[floorIndex] : _hotelCentre;
        _camera.FocusOn(new Vector3(c.X, FloorMath.BaseYFor(floorIndex), c.Z), instant: true);
    }

    /// <summary>
    /// 算出「刚好装下整栋楼」的正交 Size。
    ///
    /// 正交相机的取景只由 Size 决定，移动相机不改变画面大小。两个容易踩的点：
    ///  · Godot 默认 KeepHeight，Size 是**垂直**范围；竖屏 1080x1920 下水平范围只有它的
    ///    0.5625 倍，只按楼高算会把 30 米宽的酒店裁掉两边。
    ///  · 用包围盒对角线估又会过度补偿（等距投影不会把 3D 对角线整根投到水平轴上），
    ///    结果是楼缩在画面中间一小块。
    /// 所以直接把 8 个角投到相机空间量真实范围——精确，且楼型变了自动跟着变。
    /// </summary>
    private float FitOrthoSize(Aabb bounds)
    {
        // 相机朝向是固定的：从 Offset 方向看向焦点。用同样的基构造观察矩阵。
        Basis view = Transform3D.Identity.LookingAt(-ManagerCamera.ViewOffset, Vector3.Up).Basis.Inverse();

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = view * bounds.GetEndpoint(i);
            minX = Mathf.Min(minX, corner.X); maxX = Mathf.Max(maxX, corner.X);
            minY = Mathf.Min(minY, corner.Y); maxY = Mathf.Max(maxY, corner.Y);
        }

        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float aspect = vp.X / vp.Y;
        // Size 表示垂直范围，所以水平需求要换算回垂直口径再取大者
        return Mathf.Max(maxY - minY, (maxX - minX) / aspect) * 1.08f;
    }

    private static Aabb ComputeBounds(Node node)
    {
        var acc = new Aabb();
        bool first = true;
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D mi && mi.Mesh != null)
            {
                Aabb world = mi.GlobalTransform * mi.Mesh.GetAabb();
                if (first) { acc = world; first = false; } else acc = acc.Merge(world);
            }
            Aabb sub = ComputeBounds(child);
            if (sub.Size != Vector3.Zero)
            {
                if (first) { acc = sub; first = false; } else acc = acc.Merge(sub);
            }
        }
        return first ? new Aabb() : acc;
    }

    public override void _Process(double delta)
    {
        // 房态刷新节流到 4Hz。和 Rooms 屏一样：Sim 一分钟能吐上百个 tick，
        // 每个 tick 都去比对 12 间房的材质纯属浪费。
        _markerAccumulator += delta;
        if (_markerAccumulator >= 0.25)
        {
            _markerAccumulator = 0;
            _markers?.Refresh(_sim?.Sim);
            // 目标重挑和房态刷新同频。每帧重算路径既浪费，也会让走位一直抖。
            foreach (var s in _staff) s.RetargetFromSim();
        }

        if (_shotAfter <= 0 || _shotTaken) return;
        _shotAfter -= delta;
        if (_shotAfter > 0) return;
        _shotTaken = true;
        Capture();
    }

    private async void Capture()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        // 把截图时刻的模拟状态一并打出来：一张静态图看不出「现在是第几天、几点、
        // 有没有脏房」，而这些恰恰决定了画面里该有什么。
        var sim = _sim?.Sim;
        if (sim != null)
        {
            GD.Print($"[shot] day {sim.Clock.CurrentDay} {sim.Clock.TimeFormatted} " +
                     $"| ready={sim.Rooms.CountOf(RoomSimState.Ready)} occ={sim.Rooms.CountOf(RoomSimState.Occupied)} " +
                     $"dirty={sim.Rooms.CountOf(RoomSimState.Dirty)} clean={sim.Rooms.CountOf(RoomSimState.Cleaning)} " +
                     $"insp={sim.Rooms.CountOf(RoomSimState.AwaitingInspection)} ruined={sim.Rooms.CountOf(RoomSimState.Ruined)}");
            foreach (var s in _staff) GD.Print("[shot] " + s.Describe());
        }

        string path = ProjectSettings.GlobalizePath("res://Dev/world3d.png");
        Error err = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print(err == Error.Ok ? $"[shot] {path}" : $"[shot] FAILED {err}");
        GetTree().Quit(err == Error.Ok ? 0 : 1);
    }
}
