using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// 跨引擎一致性基准（Godot 移植 Phase 1）。
//
// 用途：同一份场景代码由三个宿主运行——dotnet test / Godot / Unity——输出必须**逐字节相同**。
// 因为 Sim 在三边是同一份源码，数字对不上就只可能是宿主接线错了，不可能是规则错了。
// 这把「两个数字看着矛盾」这类问题的排查范围一次性砍掉一半。
//
// 本文件位于 Assets/Game/Scripts/Sim/ 下，因此：
//   - Unity 通过 OldTownHotel.Game.asmdef 编译它
//   - OldTownHotel.Sim.csproj 通过既有 glob 自动收录它
//   - Godot 通过引用 Sim.csproj 拿到它
// 不需要为它单独建工程。
//
// 确定性铁律（写新场景时必须遵守）：
//   1. 固定种子，不碰 DateTime.Now / Environment / 文件系统
//   2. 一切格式化走 InvariantCulture——中文 locale 下 float.ToString() 会输出不同字符串，
//      这是最容易在跨宿主时炸出假 diff 的地方
//   3. 换行恒用 "\n"，不用 Environment.NewLine
//
// 移植完成后本文件可以删掉。
public static class ParityScenario
{
    /// <summary>报告格式版本。改了输出格式就 +1，让 golden 文件的失配是显式的而非神秘的。</summary>
    public const int FormatVersion = 1;

    private const int Seed = 90210;
    private const int Rooms = 12;
    private const int Housekeepers = 2;
    private const int Inspectors = 1;
    private const int Receptionists = 2;
    private const int StartingCash = 2000;
    private const int Days = 14;
    private const int MaterialLowWater = 4;      // 低于此库存就补货
    private const int MaterialRestockUnits = 8;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>跑固定场景，返回逐行确定性账本。</summary>
    public static string Run()
    {
        var sb = new StringBuilder();

        // 头部把配置也打出来：配置一改，diff 立刻显示是配置变了，
        // 而不是让人对着一堆变化的数字猜发生了什么。
        sb.Append("# OldTownHotel Sim parity ledger v").Append(FormatVersion.ToString(Inv)).Append('\n');
        sb.Append("# seed=").Append(Seed.ToString(Inv))
          .Append(" rooms=").Append(Rooms.ToString(Inv))
          .Append(" hsk=").Append(Housekeepers.ToString(Inv))
          .Append(" insp=").Append(Inspectors.ToString(Inv))
          .Append(" rcp=").Append(Receptionists.ToString(Inv))
          .Append(" cash0=").Append(StartingCash.ToString(Inv))
          .Append(" days=").Append(Days.ToString(Inv))
          .Append('\n');

        var sim = Build();

        for (int day = 1; day <= Days; day++)
        {
            sim.BeginDay();
            int planned = sim.ArrivalsPlannedToday;

            sim.RunToEndOfDay();
            var settlement = sim.SettleDay();

            // 玩家动作。故意脚本化成「称职玩家」：每天收箱 + 发薪。
            //
            // 第一版基准没有这两步，结果 14 天一分钱工资没发 —— 欠薪 → 士气崩 → 没人打扫
            // → 脏房堆积 → 无房可卖，第 9 天起营收恒为 0，后 5 行全是死数。
            // 那种账本对接线错误没有任何鉴别力：Godot 那边就算把整个员工系统接错了，
            // 输出照样是一片零，diff 依然全绿。基准必须让被测系统一直活着。
            //
            // 顺序不能反：PayWages 花的是现金，得先把保险箱收进来。
            int collected = sim.CollectSafebox();
            PayrollPayment pay = sim.PayWages();
            int repairsOrdered = DoMaintenance(sim);

            AppendDay(sb, day, planned, sim, settlement, collected, pay, repairsOrdered);

            sim.Clock.BeginNextDay();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 「称职玩家」的维护动作：补材料 + 把所有能修的家具下单送修。返回本日下单数。
    ///
    /// 没有这一步的话，家具坏一件锁一间房，12 间房会在第 9 天全部变成 Blocked，
    /// 后 5 天账本恒为零 —— 那样的基准对接线错误没有鉴别力。
    /// </summary>
    private static int DoMaintenance(HotelSim sim)
    {
        if (sim.Materials.Stock < MaterialLowWater)
            sim.TryBuyMaterials(MaterialRestockUnits);

        // 先快照 id 再下单：避免边遍历边改集合，也让顺序稳定（List 序 = 确定性）
        var repairable = new List<int>();
        var all = sim.Furniture.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i].IsRepairable) repairable.Add(all[i].instanceId);

        int ordered = 0;
        for (int i = 0; i < repairable.Count; i++)
            if (sim.TryRepairFurniture(repairable[i], out _)) ordered++;

        return ordered;
    }

    private static HotelSim Build()
    {
        var defs = new List<RoomDefinition>();
        for (int i = 0; i < Rooms; i++)
        {
            defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                        RoomTier.Old, RoomSimState.Ready));
        }

        var staff = new StaffRoster();
        for (int i = 0; i < Housekeepers; i++)
            staff.Register(new StaffMember(StaffRole.Housekeeper, "HSK" + i.ToString(Inv), 60,
                                           new StaffAttributes(55, 55, 55), 1, null));
        for (int i = 0; i < Inspectors; i++)
            staff.Register(new StaffMember(StaffRole.Inspector, "INSP" + i.ToString(Inv), 70,
                                           new StaffAttributes(55, 55, 55), 1, null));
        for (int i = 0; i < Receptionists; i++)
            staff.Register(new StaffMember(StaffRole.Reception, "RCP" + i.ToString(Inv), 65,
                                           new StaffAttributes(55, 55, 55), 1, null));

        var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                               DemandConfig.Default, StartingCash, Seed);
        sim.FurnishInheritedRooms();
        return sim;
    }

    private static void AppendDay(StringBuilder sb, int day, int planned,
                                  HotelSim sim, DaySettlementResult s,
                                  int collected, PayrollPayment pay, int repairsOrdered)
    {
        int faulted = 0, underRepair = 0;
        var all = sim.Furniture.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].IsFaulted) faulted++;
            if (all[i].IsUnderRepair) underRepair++;
        }

        sb.Append("day=").Append(Pad(day, 2))
          .Append(" planned=").Append(Pad(planned, 3))
          .Append(" in=").Append(Pad(sim.ArrivalsCheckedInToday, 3))
          .Append(" away=").Append(Pad(sim.ArrivalsTurnedAwayToday, 3))
          .Append(" out=").Append(Pad(sim.CheckoutsToday, 3))
          // 房态分布，不只是 occ。房间卡在哪个状态是最能暴露接线错误的信号之一，
          // 而且只看 occ 的话「没客人」和「有客人但没房可卖」长得一模一样。
          .Append(" | rooms occ=").Append(Pad(sim.Rooms.CountOf(RoomSimState.Occupied), 3))
          .Append(" rdy=").Append(Pad(sim.Rooms.CountOf(RoomSimState.Ready), 3))
          .Append(" dty=").Append(Pad(sim.Rooms.CountOf(RoomSimState.Dirty), 3))
          .Append(" cln=").Append(Pad(sim.Rooms.CountOf(RoomSimState.Cleaning), 3))
          .Append(" insp=").Append(Pad(sim.Rooms.CountOf(RoomSimState.AwaitingInspection), 3))
          .Append(" blk=").Append(Pad(sim.Rooms.CountOf(RoomSimState.Blocked), 3))
          .Append(" ruin=").Append(Pad(sim.Rooms.CountOf(RoomSimState.Ruined), 3))
          .Append(" | gross=").Append(Pad(sim.GrossIncomeToday, 6))
          .Append(" room=").Append(Pad(sim.RoomIncomeToday, 6))
          .Append(" misc=").Append(Pad(sim.MiscIncomeToday, 5))
          .Append(" comm=").Append(Pad(sim.CommissionToday, 5))
          .Append(" | box=").Append(Pad(s.netToSafebox, 6))
          .Append(" over=").Append(Pad(s.overflowed, 5))
          .Append(" fromCash=").Append(Pad(s.cashPaidFromReserve, 6))
          .Append(" cash=").Append(Pad(s.cashAfter, 6))
          .Append(" net=").Append(Pad(s.NetProfit, 6))
          .Append(" wages=").Append(s.wagesPaid ? "ok " : "OWED")
          .Append(" unpaid=").Append(Pad(s.unpaidAmount, 5))
          .Append(" | got=").Append(Pad(collected, 6))
          .Append(" wage=").Append(Pad(sim.WagesAccruedToday, 5))
          .Append(" paid=").Append(Pad(pay.paid, 6))
          .Append(" owed=").Append(Pad(pay.stillOwed, 6))
          .Append(pay.cleared ? " clr" : " DUE")
          .Append(" mp=").Append(Pad(pay.moralePenalty, 3))
          .Append(" | mat=").Append(Pad(sim.Materials.Stock, 3))
          .Append(" fix=").Append(Pad(repairsOrdered, 3))
          .Append(" flt=").Append(Pad(faulted, 3))
          .Append(" wip=").Append(Pad(underRepair, 3))
          .Append(" | safebox=").Append(Pad(sim.Safebox.Balance, 6))
          .Append('/').Append(sim.Safebox.Capacity.ToString(Inv))
          .Append(" stars=").Append(sim.Reputation.Stars.ToString("F3", Inv))
          .Append(" sat=").Append(sim.Reputation.AverageSatisfaction.ToString("F4", Inv))
          .Append(" n=").Append(Pad(sim.Reputation.SampleCount, 3))
          .Append('\n');
    }

    // 右对齐定宽：让账本用肉眼也能扫，且 diff 只在真的数字变化时才出现
    private static string Pad(int value, int width) =>
        value.ToString(Inv).PadLeft(width);
}
