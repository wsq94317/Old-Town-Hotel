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

    private double _shotAfter = -1;
    private bool _shotTaken;
    private bool _overview;

    // 单层视角用 Unity 那台相机的实测正交半高 6（Godot 全高 = 12）。
    // 总览要看下 27 米高的整栋楼，按包围盒算，不写死。
    private const float FloorZoom = 12f;
    private float _overviewZoom = 40f;
    private Vector3 _hotelCentre;

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

        BuildLighting();

        Aabb bounds = ComputeBounds(_geometry);
        _hotelCentre = bounds.GetCenter();
        _overviewZoom = FitOrthoSize(bounds);

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
        _camera.Size = FloorZoom;
        _camera.FocusOn(new Vector3(_hotelCentre.X, FloorMath.BaseYFor(floorIndex), _hotelCentre.Z),
                        instant: true);
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
        if (_shotAfter <= 0 || _shotTaken) return;
        _shotAfter -= delta;
        if (_shotAfter > 0) return;
        _shotTaken = true;
        Capture();
    }

    private async void Capture()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = ProjectSettings.GlobalizePath("res://Dev/world3d.png");
        Error err = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print(err == Error.Ok ? $"[shot] {path}" : $"[shot] FAILED {err}");
        GetTree().Quit(err == Error.Ok ? 0 : 1);
    }
}
