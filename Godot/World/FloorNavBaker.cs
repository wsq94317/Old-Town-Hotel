using System.Collections.Generic;
using Godot;

// 给每层烘一张导航网格。
//
// 每层独立、互不相连是刻意的：Unity 那边跨楼层不走导航网格，StairZone 和
// ElevatorController 都是直接 NavMeshAgent.Warp 过去（场景里 OffMeshLink = 0）。
// 所以 Godot 这边也不做跨层连接，换层同样是设位置。
//
// 烘焙参数按酒店尺度调，不是 Godot 的默认值：
//   · 走廊只有 2 个单位宽，agent 半径给默认的 0.5 会把走廊整条判成不可走
//   · 层高 4，爬升高度要小于门槛否则会认为能"爬"上家具
public partial class FloorNavBaker : Node3D
{
    // 人的尺度：半径 0.3（比走廊半宽小得多，留余量），高 1.7
    private const float AgentRadius = 0.3f;
    private const float AgentHeight = 1.7f;
    // 台阶/门槛能跨，家具不能。必须是 CellHeight 的整数倍——否则 Godot 会把它向下
    // 取整到体素单位并每层警告一次，实际生效值和你写的不是一个数。
    private const float CellHeight = 0.1f;
    private const float MaxClimb = 0.3f;
    private const float MaxSlope = 45f;
    private const float CellSize = 0.15f;      // 比默认细：家具之间的缝要能走出来

    public int BakedRegions { get; private set; }

    public void BakeAll(IReadOnlyList<NavigationRegion3D> regions)
    {
        // 导航**地图**有自己的体素尺寸（默认 0.25），和网格不一致时 Godot 会警告，
        // 而且寻路会按地图的粗粒度走——我们特意把网格调细到 0.15 是为了让家具之间的
        // 缝能走出来，地图不跟着调的话这份精度白费。必须在烘焙前设好。
        Rid map = GetViewport().World3D.NavigationMap;
        NavigationServer3D.MapSetCellSize(map, CellSize);
        NavigationServer3D.MapSetCellHeight(map, CellHeight);

        foreach (var region in regions)
        {
            var mesh = new NavigationMesh
            {
                AgentRadius = AgentRadius,
                AgentHeight = AgentHeight,
                AgentMaxClimb = MaxClimb,
                AgentMaxSlope = MaxSlope,
                CellSize = CellSize,
                CellHeight = CellHeight,
                // 只吃这个导航区自己的子节点——楼层之间互不污染
                GeometrySourceGeometryMode = NavigationMesh.SourceGeometryMode.RootNodeChildren,
                GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.MeshInstances,
            };
            region.NavigationMesh = mesh;
            region.BakeNavigationMesh(onThread: false);
            // 烘焙把顶点写进 NavigationMesh 资源，但不保证节点层把它推给服务器。
            // 不显式推的话资源里有上百个多边形，服务器上却是空的——
            // MapGetClosestPoint 会把每个点都返回 (0,0,0)。
            NavigationServer3D.RegionSetNavigationMesh(region.GetRid(), mesh);
            BakedRegions++;
        }

        // NavigationServer3D 默认在物理帧末尾才同步地图。烘完立刻查询的话，
        // MapGetClosestPoint 会把**每一个**点都返回成 (0,0,0)——地图在查询者看来是空的。
        // 那个症状极具误导性：看起来像"网格没烘出来"，但多边形数明明是几百。
        // 在 _Ready 里就要用导航的话，必须强制同步一次。
        NavigationServer3D.MapForceUpdate(map);

        GD.Print($"[nav] map rid={map} active={NavigationServer3D.MapIsActive(map)} " +
                 $"regions={NavigationServer3D.MapGetRegions(map).Count} " +
                 $"r0_map_matches={(regions.Count > 0 ? (regions[0].GetNavigationMap() == map).ToString() : "n/a")} " +
                 $"r0_enabled={(regions.Count > 0 ? regions[0].Enabled.ToString() : "n/a")}");

        GD.Print($"[nav] baked {BakedRegions} floor regions " +
                 $"(radius {AgentRadius}, climb {MaxClimb}, cell {CellSize})");
    }

    /// <summary>
    /// 某层导航网格的多边形数。0 = 这层完全走不了，通常意味着 agent 半径比走廊还宽，
    /// 或者几何全被当成障碍。用它做冒烟检查，比"agent 不动"这种症状好查得多。
    /// </summary>
    public static int PolygonCount(NavigationRegion3D region)
    {
        var nm = region?.NavigationMesh;
        return nm == null ? 0 : nm.GetPolygonCount();
    }
}
