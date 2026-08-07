using System.Collections.Generic;
using Godot;

// 3D 世界里的 Sim 宿主。Unity 侧 HotelSimSceneBridge 的对位物。
//
// 房间定义来自**场景锚点**，不是另造一套假房表——和 Unity 那边同一个原则
// （bridge 的注释原话：「用场景里真实的房建完整内核，不是另造一套 100 间的假房」）。
//
// ══ 一处刻意不照搬 ══
//
// Unity 侧写的是 `Sim.Pipeline.ServiceEnabled = !MirrorMode`，而 MirrorMode 恒为 true，
// 也就是**把房态交给 v1 的 StaffAgent，Sim 只跑经济那一半**。那是镜像期的过渡妥协：
// 两套系统并存时必须有一套让权。
//
// Godot 这边没有 v1 StaffAgent，房态本来就该归 Sim。所以这里开着 ServiceEnabled——
// 照抄那行会得到一座房态永远不变的酒店，而且看起来像"数据没接上"。
public partial class WorldSimHost : Node
{
    private const int RngSeed = 20260720;        // 与 bridge 同种子，便于两边对数
    private const int StartingCash = 4000;
    private const int DerelictAtStart = 4;
    private const float DaySpeed = 2f;

    public HotelSim Sim { get; private set; }

    public void Build(IReadOnlyList<HotelGeometry.RoomAnchor> anchors)
    {
        var defs = new List<RoomDefinition>();
        foreach (var a in anchors)
        {
            defs.Add(new RoomDefinition(a.Number,
                                        floor: a.Floor,
                                        zone: a.Floor,      // 一层=一区，与 bridge 一致
                                        category: a.Category,
                                        tier: RoomTier.Old,
                                        state: RoomSimState.Ready));
        }
        if (defs.Count == 0)
        {
            GD.PushError("[sim] no room anchors — cannot build the hotel");
            return;
        }

        // 开局锁几间破败房：按房号从大到小锁，顶楼先烂（bridge 原话：老楼上面没人管）。
        // 没有这一步的话「清理破败房」这套操作没有对象，玩家也没有解锁的进展感。
        defs.Sort((x, y) => y.number.CompareTo(x.number));
        for (int i = 0; i < DerelictAtStart && i < defs.Count; i++)
        {
            var d = defs[i];
            defs[i] = new RoomDefinition(d.number, d.floor, d.zone, d.category, d.tier, RoomSimState.Ruined);
        }
        defs.Sort((x, y) => x.number.CompareTo(y.number));

        var staff = new StaffRoster();
        for (int i = 0; i < 2; i++)
            staff.Register(new StaffMember(StaffRole.Housekeeper, "HSK" + i, 60,
                                           new StaffAttributes(55, 55, 55), 1, null));
        staff.Register(new StaffMember(StaffRole.Inspector, "INSP0", 70,
                                       new StaffAttributes(55, 55, 55), 1, null));
        staff.Register(new StaffMember(StaffRole.Reception, "RCP0", 65,
                                       new StaffAttributes(55, 55, 55), 1, null));

        Sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                           DemandConfig.Default, StartingCash, RngSeed);

        // 见文件头：Unity 那边这里是 false，Godot 这边房态归 Sim。
        Sim.Pipeline.ServiceEnabled = true;
        Sim.Clock.SpeedMultiplier = DaySpeed;

        Sim.FurnishInheritedRooms();
        Sim.Warehouse.SetCapacity(Warehouse.DefaultCapacity);
        Sim.Materials.Add(6);
        Sim.BeginDay();

        GD.Print($"[sim] built {defs.Count} rooms ({DerelictAtStart} derelict), " +
                 $"seed {RngSeed}, cash ${StartingCash}");
    }

    public override void _Process(double delta)
    {
        if (Sim == null) return;

        Sim.Clock.Advance((float)delta);
        while (Sim.Clock.TryConsumeTick()) Sim.StepMinute();

        if (!Sim.Clock.DayEndReached) return;

        var result = Sim.SettleDay();
        GD.Print($"[sim] day {Sim.Clock.CurrentDay} settled: net ${result.NetProfit}");

        // 与 ParityScenario / Main.cs 同一套「称职玩家」动作：不发薪不修家具的话
        // 酒店会在第 9 天前后死透，房态全变 Blocked，屏幕上什么变化都看不到。
        Sim.PayWages();
        if (Sim.Materials.Stock < 4) Sim.TryBuyMaterials(8);
        var all = Sim.Furniture.All;
        var repairable = new List<int>();
        for (int i = 0; i < all.Count; i++)
            if (all[i].IsRepairable) repairable.Add(all[i].instanceId);
        for (int i = 0; i < repairable.Count; i++) Sim.TryRepairFurniture(repairable[i], out _);

        Sim.Clock.BeginNextDay();
        Sim.BeginDay();
    }
}
