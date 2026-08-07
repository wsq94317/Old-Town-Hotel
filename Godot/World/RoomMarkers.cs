using System.Collections.Generic;
using Godot;

// 每间房上方悬浮一块房态牌：颜色 + 房号 + 状态词。
//
// 颜色和文字走 RoomStateUi —— 和 Rooms 那一屏是**同一个映射函数**。所以 3D 世界里
// 一间房显示 DIRTY，2D 列表里同一间房必然也是 DIRTY，不可能出现两套说法。
// （这正是当初拒绝走 Room2DState 的理由：那条路上 7 个状态有 6 个会静默错位。）
//
// 标记挂在对应楼层节点下，所以楼层显隐会自动带上它们，不用另写一套显隐逻辑。
public partial class RoomMarkers : Node3D
{
    private sealed class Marker
    {
        public int Number;
        public MeshInstance3D Plate;
        public Label3D Text;
        public StandardMaterial3D Material;
        public RoomSimState Last = (RoomSimState)(-1);
    }

    private readonly List<Marker> _markers = new List<Marker>();

    private static readonly Vector3 PlateSize = new Vector3(1.7f, 0.18f, 1.7f);
    private const float HoverHeight = 2.3f;

    public void Build(IReadOnlyList<HotelGeometry.RoomAnchor> anchors, IReadOnlyList<Node3D> floors)
    {
        foreach (var a in anchors)
        {
            // 挂到所属楼层下面：楼层一隐藏，牌子跟着消失，不需要第二套显隐规则。
            Node3D parent = a.Floor >= 0 && a.Floor < floors.Count ? floors[a.Floor] : this;

            var holder = new Node3D { Name = $"Room{a.Number}" };
            parent.AddChild(holder);
            // 楼层节点已经被抬到 baseY，所以这里要减掉，否则会叠加一次楼层高度
            holder.Position = a.Position - parent.GlobalPosition;

            var mat = new StandardMaterial3D
            {
                AlbedoColor = Colors.Magenta,   // 刺眼兜底：没被 Refresh 覆盖就说明没接上
                Roughness = 0.6f,
                // 不受光：房态是 UI 信息，不该因为楼层阴影而变暗到读不出来
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };

            var plate = new MeshInstance3D
            {
                Name = "Plate",
                Mesh = new BoxMesh { Size = PlateSize },
                MaterialOverride = mat,
                Position = new Vector3(0, HoverHeight, 0),
            };
            holder.AddChild(plate);

            var label = new Label3D
            {
                Name = "Text",
                Text = a.Number.ToString(),
                // 字号 x PixelSize = 世界高度。房间间距是 5 个单位，所以整块牌子
                // 控制在 0.2 上下才不会糊成一片——照 UI 的直觉给 96 会盖住半层楼。
                FontSize = 64,
                PixelSize = 0.003f,
                Modulate = Colors.White,
                OutlineSize = 24,
                OutlineModulate = new Color(0, 0, 0, 0.75f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                Position = new Vector3(0, HoverHeight + 0.55f, 0),
            };
            holder.AddChild(label);

            _markers.Add(new Marker { Number = a.Number, Plate = plate, Text = label, Material = mat });
        }

        GD.Print($"[world] {_markers.Count} room markers");
    }

    /// <summary>每次刷新只在房态真变了才动材质和文字。</summary>
    public void Refresh(HotelSim sim)
    {
        if (sim == null) return;
        for (int i = 0; i < _markers.Count; i++)
        {
            var m = _markers[i];
            if (!sim.Rooms.Contains(m.Number)) continue;

            RoomSimState state = sim.Rooms.At(m.Number).state;
            if (state == m.Last) continue;
            m.Last = state;

            var (colour, label) = RoomStateUi.For(state);
            m.Material.AlbedoColor = colour;
            m.Text.Text = $"{m.Number}\n{label}";
        }
    }
}
