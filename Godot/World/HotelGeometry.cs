using System.Collections.Generic;
using Godot;

// 从 Unity 导出的 JSON 重建酒店几何。
//
// 数据来自 Assets/Game/Scripts/Editor/GodotGeometryExporter.cs：遍历 7 个楼层预制体，
// 把每个 MeshFilter 的世界变换 + 图元类型 + 材质颜色导成 JSON。
// 实测 1,390 个零件、跳过 0 个——整座酒店的几何全是 Unity 内置图元（绝大多数是 Cube），
// 所以这套映射是完备的，不是抽样。
//
// ══ 坐标系 ══
//
// Unity 左手系 +Z 向前，Godot 右手系 −Z 向前。只翻位置不翻旋转会得到一座
// **镜像**的酒店——看起来"差不多对"，但门在错误的一侧，最难发现的那种错。
//
//   位置    (x, y, z)     -> (x, y, -z)
//   四元数  (x, y, z, w)  -> (-x, -y, z, w)
//   缩放    不变
//
// 缩放用的是导出时的 lossyScale（世界缩放）：预制体内部有嵌套缩放，逐级还原容易在
// 某一层漏一个乘法，世界空间是唯一无歧义的口径。
public partial class HotelGeometry : Node3D
{
    private const string DataPath = "res://data/hotel-geometry.json";

    // 1,390 个零件如果各建一个材质会白白吃掉显存和 draw call 分组。
    // 颜色是从 Unity 材质读出来的、重复度很高，按颜色缓存。
    private readonly Dictionary<Color, StandardMaterial3D> _materials = new Dictionary<Color, StandardMaterial3D>();
    private readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();

    public readonly List<Node3D> Floors = new List<Node3D>();
    public int PartCount { get; private set; }

    public override void _Ready() => Build();

    public void Build()
    {
        using var f = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
        if (f == null)
        {
            GD.PushError($"[world] cannot open {DataPath} — run Tools/Godot Port/Export Hotel Geometry in Unity");
            return;
        }

        var json = Json.ParseString(f.GetAsText());
        if (json.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError("[world] geometry json did not parse to a dictionary");
            return;
        }

        var root = json.AsGodotDictionary();
        var floors = root["floors"].AsGodotArray();

        for (int i = 0; i < floors.Count; i++)
        {
            var floor = floors[i].AsGodotDictionary();
            var holder = new Node3D { Name = floor["name"].AsString() };
            AddChild(holder);
            Floors.Add(holder);

            // 楼层高度：预制体里的几何都在局部原点，垂直堆叠是场景实例给的，
            // 导出器从活场景读出来放在 baseY 里。y 不用翻符号——上下方向两个引擎一致。
            float baseY = floor.ContainsKey("baseY") ? (float)floor["baseY"] : 0f;
            holder.Position = new Vector3(0, baseY, 0);

            var parts = floor["parts"].AsGodotArray();
            for (int p = 0; p < parts.Count; p++)
                AddPart(holder, parts[p].AsGodotDictionary());
        }

        GD.Print($"[world] rebuilt {PartCount} parts across {Floors.Count} floors, " +
                 $"{_materials.Count} distinct materials");
    }

    private void AddPart(Node3D parent, Godot.Collections.Dictionary part)
    {
        var p = part["p"].AsGodotArray();
        var r = part["r"].AsGodotArray();
        var s = part["s"].AsGodotArray();
        var c = part["c"].AsGodotArray();

        // Unity -> Godot：位置翻 z，四元数翻 x/y
        var position = new Vector3((float)p[0], (float)p[1], -(float)p[2]);
        var rotation = new Quaternion(-(float)r[0], -(float)r[1], (float)r[2], (float)r[3]).Normalized();
        var scale = new Vector3((float)s[0], (float)s[1], (float)s[2]);

        var mi = new MeshInstance3D
        {
            Name = part["n"].AsString(),
            Mesh = MeshFor(part["m"].AsString()),
            MaterialOverride = MaterialFor(new Color((float)c[0], (float)c[1], (float)c[2], (float)c[3])),
            Visible = part["vis"].AsBool(),
        };
        parent.AddChild(mi);

        // 先设 Basis 再设 origin：Transform3D 是整体赋值的，分开设会互相覆盖。
        mi.Transform = new Transform3D(new Basis(rotation).Scaled(scale), position);
        PartCount++;
    }

    private Mesh MeshFor(string kind)
    {
        if (_meshes.TryGetValue(kind, out var cached)) return cached;

        // Unity 内置图元的默认尺寸：Cube 边长 1、Sphere 直径 1、Cylinder 高 2、Capsule 高 2、
        // Plane 是 10x10。Godot 的默认值不同，这里逐个对齐，否则整体比例会错。
        Mesh m = kind switch
        {
            "cube"     => new BoxMesh { Size = Vector3.One },
            "sphere"   => new SphereMesh { Radius = 0.5f, Height = 1f },
            "cylinder" => new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 2f },
            "capsule"  => new CapsuleMesh { Radius = 0.5f, Height = 2f },
            "plane"    => new PlaneMesh { Size = new Vector2(10, 10) },
            "quad"     => new QuadMesh { Size = Vector2.One },
            _          => new BoxMesh { Size = Vector3.One },
        };
        _meshes[kind] = m;
        return m;
    }

    private StandardMaterial3D MaterialFor(Color c)
    {
        if (_materials.TryGetValue(c, out var cached)) return cached;
        var m = new StandardMaterial3D
        {
            AlbedoColor = c,
            Roughness = 0.85f,
            Metallic = 0f,
        };
        if (c.A < 1f) m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _materials[c] = m;
        return m;
    }
}
