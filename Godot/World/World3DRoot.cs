using Godot;

// 3D 世界的最小宿主：几何 + 光 + 相机。没有玩法、没有寻路、没有 UI。
//
// Phase 4a 的唯一目标是「酒店在 Godot 里立起来，用和 Unity 一样的视角看到它」——
// 和 Rooms 屏当初那个"能看见"的里程碑同性质。寻路、25 个系统脚本、楼层显隐交互
// 都等这一步站稳了再逐个接。
//
// 跑法：
//   <godot> --path Godot res://World/World3D.tscn -- --shot=3
//   <godot> --path Godot res://World/World3D.tscn -- --floor=1 --shot=3
public partial class World3DRoot : Node3D
{
    private HotelGeometry _geometry;
    private Camera3D _camera;

    private double _shotAfter = -1;
    private bool _shotTaken;
    private int _onlyFloor = -1;      // -1 = 全部显示

    public override void _Ready()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--shot=")) _shotAfter = arg.Substring(7).ToFloat();
            else if (arg.StartsWith("--floor=")) _onlyFloor = arg.Substring(8).ToInt();
        }

        _geometry = new HotelGeometry { Name = "Hotel" };
        AddChild(_geometry);

        if (_onlyFloor > 0)
            for (int i = 0; i < _geometry.Floors.Count; i++)
                _geometry.Floors[i].Visible = (i + 1) == _onlyFloor;

        // Unity 那边是 URP 平行光，欧拉角 (50, 330, 0)。Godot 的 -Z 才是"前方"，
        // 而且 y 轴朝向相反，所以角度不能照抄——这里按同样的"从右后上方打下来"重新摆。
        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            LightEnergy = 1.1f,
            ShadowEnabled = true,
        };
        sun.RotationDegrees = new Vector3(-50, 30, 0);
        AddChild(sun);

        var env = new WorldEnvironment
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
        };
        AddChild(env);

        _camera = new Camera3D { Name = "Camera", Current = true };
        AddChild(_camera);
        FrameHotel();

        if (_shotAfter <= 0) return;
        GetViewport().GuiDisableInput = true;   // 截图必须可复现，理由见 Main.cs
    }

    /// <summary>
    /// 把相机摆到能看全酒店的位置。
    ///
    /// 不照搬 Unity 相机的 (-8,10,-8)/(35,45,0)：那组数值是在 Unity 的左手系里定的，
    /// 换系之后同样的数字指向别处。改成按几何实际包围盒来取景——这样即使楼层布局变了
    /// 也不会拍到画外，而且不依赖任何需要手工换算的常数。
    /// </summary>
    private void FrameHotel()
    {
        Aabb bounds = ComputeBounds(_geometry);
        if (bounds.Size == Vector3.Zero)
        {
            _camera.Position = new Vector3(20, 20, 20);
            _camera.LookAt(Vector3.Zero, Vector3.Up);
            return;
        }

        Vector3 centre = bounds.GetCenter();
        float radius = bounds.Size.Length() * 0.5f;

        // 等距感的方位角，和 Unity 那台 (35°俯角, 45°方位) 观感一致
        var dir = new Vector3(1, 0.82f, 1).Normalized();
        _camera.Position = centre + dir * (radius * 1.5f);
        _camera.LookAt(centre, Vector3.Up);
        _camera.Far = radius * 6f;

        GD.Print($"[world] bounds centre={centre} size={bounds.Size} -> camera {_camera.Position}");
    }

    private static Aabb ComputeBounds(Node node)
    {
        var acc = new Aabb();
        bool first = true;
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D mi && mi.Visible && mi.Mesh != null)
            {
                Aabb world = mi.GlobalTransform * mi.Mesh.GetAabb();
                if (first) { acc = world; first = false; }
                else acc = acc.Merge(world);
            }
            Aabb sub = ComputeBounds(child);
            if (sub.Size != Vector3.Zero)
            {
                if (first) { acc = sub; first = false; }
                else acc = acc.Merge(sub);
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
