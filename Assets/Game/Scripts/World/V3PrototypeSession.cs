using System.Collections.Generic;
using UnityEngine;

// M-C 好玩验证门的可玩原型（临时 UI，M-F 换正式竖剖面 + M-H 换 UGUI）。
//
// 目的只有一个：**让这套日循环能被真的玩一遍**，判断它到底好不好玩——
//   登录看晨报 → 调价/排班/装修决策 → 跳到入住高峰 → 日结 → 收保险箱 → 还贷 vs 装修取舍
// 开局按 spec §2：100 间房只有 10 间能用，背 $183k 债务，位置极好保底客源。
// 这个场景不碰 v2 巡查层，风险为零；模拟内核是同一套（Sim/），所以调参结论直接可用。
public class V3PrototypeSession : MonoBehaviour
{
    private enum Tab { Report, Pricing, Staff, Renovation, Rooms }

    [SerializeField] private int totalRooms = 100;
    [SerializeField] private int openRoomsAtStart = 12;
    [SerializeField] private int startingCash = 4000;
    [SerializeField] private int startingLoan = 183000;

    // M-C 调参（首轮试玩发现的结构性问题）：v1 沿用的 0.0015/日 在 v3 尺度下是
    // 每天 $274 利息，而 12 间老房满住的天花板约 $650 毛收入——扣掉工资后连利息
    // 都覆盖不了，债务每天净增，玩家从第一天就在打一场赢不了的仗。
    // 0.0004/日（约 1.2%/月）+ 每日固定还款 $150 → 本金真的会降，前期依然紧张。
    [SerializeField] private float dailyInterestRate = 0.0004f;
    [SerializeField] private int dailyRepayment = 150;
    [SerializeField] private int rngSeed = 8675309;

    private HotelSim _sim;
    private LoanAccount _loan;
    private Tab _tab = Tab.Report;
    private bool _awaitingMorningReport = true;
    private string _toast = "";
    private float _toastUntil;
    private int _renovationBatchSize = 1;
    private RenovationPlanKind _plan = RenovationPlanKind.Economy;

    private void Awake()
    {
        BuildHotel();
    }

    private void BuildHotel()
    {
        // 100 间房：前 openRoomsAtStart 间能用，其余破败待解锁（spec §2）
        var defs = new List<RoomDefinition>(totalRooms);
        for (int i = 0; i < totalRooms; i++)
        {
            int floor = 1 + i / 16;                 // 每层 16 间
            int number = 100 * (floor + 1) + (i % 16) + 1;
            defs.Add(new RoomDefinition(
                number, floor, floor, Room2DRoomCategory.Single, RoomTier.Old,
                i < openRoomsAtStart ? RoomSimState.Ready : RoomSimState.Ruined));
        }

        var staff = new StaffRoster();
        staff.Register(new StaffMember(StaffRole.Reception, "Reception", 65,
                                       new StaffAttributes(55, 55, 55), 1, null));
        staff.Register(new StaffMember(StaffRole.Housekeeper, "Housekeeper", 60,
                                       new StaffAttributes(55, 55, 55), 1, null));

        _sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                            DemandConfig.Default, startingCash, rngSeed);
        _loan = new LoanAccount(startingLoan, dailyInterestRate);
        _sim.Materials.Add(6); // 开局送几份材料，省得第一天什么都干不了
        _sim.FurnishInheritedRooms();  // 继承的破家具：崭新度 5-15%，天天出故障
    }

    private void Update()
    {
        if (_sim == null || _awaitingMorningReport) return;

        _sim.Clock.Advance(Time.deltaTime);
        int budget = 120;
        while (budget-- > 0 && _sim.Clock.TryConsumeTick()) _sim.StepMinute();

        if (_sim.Clock.DayEndReached) CloseTheDay();
    }

    private void CloseTheDay()
    {
        int interest = _loan.AccrueDailyInterest();
        int repayment = ScheduledRepayment();
        var result = _sim.SettleDay(interest: interest, scheduledRepayment: repayment, supplies: 0);
        if (result.wagesPaid && repayment > 0) _loan.Repay(repayment);

        _sim.Clock.BeginNextDay();
        _awaitingMorningReport = true;
        _tab = Tab.Report;
    }

    /// <summary>每日计划还款：自动扣（"忘按还款键"不是有趣的失败）。</summary>
    private int ScheduledRepayment() => _loan.Balance > 0 ? dailyRepayment : 0;

    private void Say(string message)
    {
        _toast = message;
        _toastUntil = Time.time + 3.5f;
    }

    // ── UI ───────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (_sim == null) return;
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        DrawTopBar(w);
        if (_awaitingMorningReport) { DrawMorningReport(w, h); return; }

        DrawTabs(w);
        switch (_tab)
        {
            case Tab.Pricing: DrawPricing(w, h); break;
            case Tab.Staff: DrawStaff(w, h); break;
            case Tab.Renovation: DrawRenovation(w, h); break;
            case Tab.Rooms: DrawRooms(w, h); break;
            default: DrawLiveStatus(w, h); break;
        }

        if (Time.time < _toastUntil)
            GUI.Box(new Rect(10, h - 46, w - 20, 26), _toast);
    }

    private void DrawTopBar(float w)
    {
        var clock = _sim.Clock;
        GUI.Box(new Rect(6, 6, w - 12, 62), "");
        GUI.Label(new Rect(14, 10, w - 28, 20),
            $"DAY {clock.CurrentDay}   {clock.TimeFormatted}   {PhaseScheduler.Label(PhaseScheduler.PhaseFor(clock.CurrentMinute))}");
        GUI.Label(new Rect(14, 28, w - 28, 20),
            $"CASH ${_sim.Cash}   SAFEBOX ${_sim.Safebox.Balance}/{_sim.Safebox.Capacity}" +
            (_sim.Overflow.Balance > 0 ? $"   SPILLED ${_sim.Overflow.Balance}" : "") +
            $"   DEBT ${_loan.Balance}");
        GUI.Label(new Rect(14, 46, w - 28, 20),
            $"{_sim.Reputation.Stars:0.0}*   ROOMS {_sim.Rooms.SellableCount} ready / {_sim.Rooms.DirtyBacklog} dirty / " +
            $"{_sim.Rooms.CountOf(RoomSimState.Occupied)} in use / {_sim.Rooms.CountOf(RoomSimState.Ruined)} derelict" +
            $"   MATERIALS {_sim.Materials.Stock}");
    }

    private void DrawTabs(float w)
    {
        float bw = (w - 20) / 5f;
        if (GUI.Button(new Rect(10, 74, bw, 26), "STATUS")) _tab = Tab.Report;
        if (GUI.Button(new Rect(10 + bw, 74, bw, 26), "PRICING")) _tab = Tab.Pricing;
        if (GUI.Button(new Rect(10 + bw * 2, 74, bw, 26), "STAFF")) _tab = Tab.Staff;
        if (GUI.Button(new Rect(10 + bw * 3, 74, bw, 26), "RENOVATE")) _tab = Tab.Renovation;
        if (GUI.Button(new Rect(10 + bw * 4, 74, bw, 26), "ROOMS")) _tab = Tab.Rooms;

        // 时间控制
        if (GUI.Button(new Rect(10, 104, 52, 24), "0.25x")) _sim.Clock.SpeedMultiplier = 0.25f;
        if (GUI.Button(new Rect(66, 104, 52, 24), "1x")) _sim.Clock.SpeedMultiplier = 1f;
        if (GUI.Button(new Rect(122, 104, 52, 24), "2x")) _sim.Clock.SpeedMultiplier = 2f;
        if (GUI.Button(new Rect(178, 104, w - 188, 24), "SKIP TO NEXT PHASE"))
        {
            if (PhaseScheduler.CanSkip(_sim.Clock.CurrentMinute, 0, out string reason))
                _sim.Clock.FastForwardTo(PhaseScheduler.NextKeyMinuteAfter(_sim.Clock.CurrentMinute));
            else Say(reason);
        }
    }

    private void DrawMorningReport(float w, float h)
    {
        var last = _sim.LastSettlement;
        GUI.Box(new Rect(10, 74, w - 20, 210), $"YESTERDAY  (day {_sim.Clock.CurrentDay - 1})");
        float y = 100;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Gross taken          ${last.netToSafebox + last.overflowed + last.cashPaidFromReserve}"); y += 20;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Into the safebox     ${last.netToSafebox}"); y += 20;
        if (last.overflowed > 0)
        { GUI.Label(new Rect(20, y, w - 40, 20), $"Spilled (65% back)   ${last.overflowed}"); y += 20; }
        if (last.cashPaidFromReserve > 0)
        { GUI.Label(new Rect(20, y, w - 40, 20), $"Covered from cash    ${last.cashPaidFromReserve}"); y += 20; }
        GUI.Label(new Rect(20, y, w - 40, 20),
            last.wagesPaid ? "Payroll: everybody got paid." : $"PAYROLL SHORT by ${last.unpaidAmount}. They noticed."); y += 24;
        GUI.Label(new Rect(20, y, w - 40, 20),
            $"Checked in {_sim.ArrivalsCheckedInToday}, turned away {_sim.ArrivalsTurnedAwayToday}, " +
            $"queue cost {_sim.TotalCheckInWaitToday} min"); y += 20;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Rating {_sim.Reputation.Stars:0.00}*   Debt ${_loan.Balance}"); y += 28;

        if (_sim.Safebox.Balance > 0 && GUI.Button(new Rect(20, y, (w - 50) / 2f, 26), $"COLLECT ${_sim.Safebox.Balance}"))
            Say($"Collected ${_sim.CollectSafebox()}.");
        if (_sim.Overflow.Balance > 0 &&
            GUI.Button(new Rect(30 + (w - 50) / 2f, y, (w - 50) / 2f, 26), $"SALVAGE ${_sim.Overflow.Balance} (65%)"))
            Say($"Salvaged ${_sim.RecoverOverflow()} of the cash that wouldn't fit.");

        // 退款申请（虚报价的代价）——批准/拒绝就在晨报上做
        var refunds = _sim.PendingRefunds;
        if (refunds.Count > 0)
        {
            GUI.Box(new Rect(10, 288, w - 20, 26 + refunds.Count * 40), "REFUND DEMANDS (" + refunds.Count + ")");
            float ry = 310;
            for (int i = 0; i < refunds.Count && i < 3; i++)
            {
                var r = refunds[i];
                GUI.Label(new Rect(20, ry, w - 40, 20), "R" + r.roomNumber + " $" + r.amount + ": " + r.line);
                if (GUI.Button(new Rect(20, ry + 18, (w - 50) / 2f, 20), "REFUND $" + r.amount))
                { _sim.ApproveRefund(r.requestId); Say("Refunded. Reputation intact."); break; }
                if (GUI.Button(new Rect(30 + (w - 50) / 2f, ry + 18, (w - 50) / 2f, 20), "REFUSE"))
                { _sim.RejectRefund(r.requestId); Say("Refused. They are writing a review as we speak."); break; }
                ry += 40;
            }
            return;   // 先处理完退款再开门
        }

        if (GUI.Button(new Rect(10, 296, w - 20, 34), "OPEN THE DOORS"))
        {
            _sim.BeginDay();
            _awaitingMorningReport = false;
            _tab = Tab.Report;
        }
    }

    private void DrawLiveStatus(float w, float h)
    {
        GUI.Box(new Rect(10, 134, w - 20, 150), "TODAY");
        float y = 158;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Expected arrivals   {_sim.ArrivalsPlannedToday}"); y += 20;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Checked in          {_sim.ArrivalsCheckedInToday}"); y += 20;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Turned away         {_sim.ArrivalsTurnedAwayToday}   (no clean room)"); y += 20;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Checkouts booked    {_sim.CheckoutsToday}   (${_sim.GrossIncomeToday})"); y += 20;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Flawed rooms sold   {_sim.FlawedStaysToday}   (no inspector on duty?)"); y += 20;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Staff working       {_sim.Staff.ProductiveCountOfRole(StaffRole.Housekeeper)} hsk, " +
            $"morale {_sim.Staff.AverageMorale:0}"); y += 20;

        if (_sim.TryFindRuinedRoom(out int ruined) &&
            GUI.Button(new Rect(10, 296, w - 20, 30), $"CLEAR A DERELICT ROOM  (${HotelSim.RuinedRoomUnlockCost})"))
        {
            if (_sim.TryUnlockRuinedRoom(ruined, out string reason)) Say($"Room {ruined} is back in service. It needs cleaning.");
            else Say(reason);
        }
    }

    private void DrawPricing(float w, float h)
    {
        GUI.Box(new Rect(10, 134, w - 20, 190), "PRICING - pick a strategy, not a spreadsheet");
        float y = 158;
        foreach (PriceTemplate t in System.Enum.GetValues(typeof(PriceTemplate)))
        {
            bool active = _sim.Pricing.DefaultTemplate == t;
            if (GUI.Button(new Rect(20, y, w - 40, 26), (active ? "> " : "  ") + PricingPolicy.LabelOf(t)))
                _sim.Pricing.DefaultTemplate = t;
            y += 30;
        }
        int day = _sim.Clock.CurrentDay;
        GUI.Label(new Rect(20, y, w - 40, 20),
            $"Tonight: ${_sim.Pricing.PriceFor(day, RoomTier.Old)} old / " +
            $"${_sim.Pricing.PriceFor(day, RoomTier.Basic)} basic / " +
            $"${_sim.Pricing.PriceFor(day, RoomTier.Better)} better" +
            (PricingPolicy.IsWeekend(day) ? "   (weekend)" : ""));
    }

    private void DrawStaff(float w, float h)
    {
        GUI.Box(new Rect(10, 134, w - 20, 200), "STAFF - cheap shifts show up in the reviews");
        float y = 158;
        foreach (StaffRole role in new[] { StaffRole.Reception, StaffRole.Housekeeper, StaffRole.Inspector })
        {
            ShiftTier tier = _sim.Shifts.TierOf(role);
            GUI.Label(new Rect(20, y, 110, 22), role.ToString());
            if (GUI.Button(new Rect(130, y, w - 150, 22), ShiftPlan.LabelOf(tier)))
                _sim.Shifts.SetTier(role, NextTier(tier));
            y += 26;
        }
        y += 6;
        GUI.Label(new Rect(20, y, w - 40, 20),
            $"On duty {_sim.Staff.OnDutyCount}/{_sim.Staff.Count}   wages today ${_sim.Shifts.DailyWageCost(_sim.Staff)}"); y += 22;
        GUI.Label(new Rect(20, y, w - 40, 20),
            $"Clean capacity {ServiceCapacityModel.CleanRoomsPerHour(_sim.Staff, 1f):0.0}/h   " +
            $"check-ins {ServiceCapacityModel.CheckInsPerHour(_sim.Staff):0.0}/h"); y += 26;

        if (GUI.Button(new Rect(20, y, w - 40, 26), "HIRE A HOUSEKEEPER  ($200 signing)"))
        {
            if (_sim.TrySpendCash(200))
            {
                int id = _sim.Staff.Register(new StaffMember(StaffRole.Housekeeper, "HSK" + _sim.Staff.Count, 60,
                                            new StaffAttributes(55, 55, 55), 1, null));
                _sim.Staff.StartShift(id);
                Say("Hired. They start immediately.");
            }
            else Say("Not enough cash. Collect the safebox first.");
        }
        y += 30;
        if (GUI.Button(new Rect(20, y, w - 40, 26), "HIRE AN INSPECTOR  ($250 signing)"))
        {
            if (_sim.TrySpendCash(250))
            {
                int id = _sim.Staff.Register(new StaffMember(StaffRole.Inspector, "INSP" + _sim.Staff.Count, 70,
                                            new StaffAttributes(55, 55, 55), 1, null));
                _sim.Staff.StartShift(id);
                Say("An inspector. Rooms will go on sale slower, but clean.");
            }
            else Say("Not enough cash.");
        }
    }

    private static ShiftTier NextTier(ShiftTier tier) =>
        tier == ShiftTier.Full ? ShiftTier.Skeleton : (ShiftTier)((int)tier + 1);

    /// <summary>房间/家具面板：挂牌档（你声称多好）vs 交付（家具实际多好）+ 维修。</summary>
    private void DrawRooms(float w, float h)
    {
        GUI.Box(new Rect(10, 134, w - 20, 240), "ROOMS - what you charge vs what you deliver");
        float y = 158;

        GUI.Label(new Rect(20, y, w - 40, 20), "Price band for the whole hotel (what you claim it is):");
        y += 22;
        float bw = (w - 50) / 3f;
        int i = 0;
        foreach (RoomTier band in System.Enum.GetValues(typeof(RoomTier)))
        {
            if (GUI.Button(new Rect(20 + i * (bw + 5), y, bw, 24),
                           band + " $" + _sim.Pricing.PriceFor(_sim.Clock.CurrentDay, band)))
            {
                for (int f = 0; f < FloorMath.FloorCount; f++) _sim.SetPriceBandForFloor(f, band);
                Say("Every room is now listed as " + band + ". Guests will judge.");
            }
            i++;
        }
        y += 30;

        float delivered = _sim.AverageDeliveredQuality();
        RoomTier currentBand = _sim.Rooms.Count > 0 ? _sim.Rooms.Peek(0).tier : RoomTier.Old;
        float expected = DemandModel.ExpectedQualityOf(currentBand);
        float gap = delivered - expected;
        GUI.Label(new Rect(20, y, w - 40, 20),
            $"Delivered {delivered:0.00}  vs  promised {expected:0.00}   gap {gap:+0.00;-0.00}"); y += 20;
        GUI.Label(new Rect(20, y, w - 40, 20),
            gap >= 0f ? "Guests are getting more than they paid for. Good reviews, less cash."
                      : gap > -0.2f ? "Slightly oversold. Tolerable."
                      : "OVERSOLD. Expect refund demands."); y += 24;

        int faulted = _sim.Furniture.FaultedItems().Count;
        GUI.Label(new Rect(20, y, w - 40, 20),
            $"Furniture: {_sim.Furniture.Count} pieces, avg newness " +
            $"{AverageNewnessAllRooms():0.00}, {faulted} broken"); y += 24;

        var broken = _sim.Furniture.FaultedItems();
        for (int b = 0; b < broken.Count && b < 3; b++)
        {
            var item = broken[b];
            FurnitureKind kind = FurnitureCatalog.Get(item.kindId);
            string status = item.IsUnderRepair ? " [being fixed, " + item.repairDaysRemaining + "d]" : "";
            GUI.Label(new Rect(20, y, w - 150, 20), "R" + item.roomNumber + ": " + FurnitureLedger.FaultLineOf(item) + status);
            if (!item.IsUnderRepair &&
                GUI.Button(new Rect(w - 128, y - 2, 108, 22), "FIX $" + kind.repairCost))
            {
                if (_sim.TryRepairFurniture(item.instanceId, out string reason))
                    Say(kind.name + " in room " + item.roomNumber + ": " + kind.repairDays + " day(s) of work.");
                else Say(reason);
            }
            y += 22;
        }

        if (broken.Count > 3) GUI.Label(new Rect(20, y, w - 40, 20), "...and " + (broken.Count - 3) + " more");
    }

    private float AverageNewnessAllRooms()
    {
        var all = _sim.Furniture.All;
        if (all.Count == 0) return 1f;
        float sum = 0f;
        for (int i = 0; i < all.Count; i++) sum += all[i].newness;
        return sum / all.Count;
    }

    private void DrawRenovation(float w, float h)
    {
        GUI.Box(new Rect(10, 134, w - 20, 210), "RENOVATION - the cost is the nights you can't sell");
        float y = 158;
        foreach (RenovationPlanKind kind in System.Enum.GetValues(typeof(RenovationPlanKind)))
        {
            bool active = _plan == kind;
            if (GUI.Button(new Rect(20, y, w - 40, 26), (active ? "> " : "  ") + RenovationPlan.LabelOf(kind)))
                _plan = kind;
            y += 30;
        }

        var plan = RenovationPlan.For(_plan);
        if (GUI.Button(new Rect(20, y, 80, 24), "- room") && _renovationBatchSize > 1) _renovationBatchSize--;
        if (GUI.Button(new Rect(104, y, 80, 24), "+ room")) _renovationBatchSize++;
        int quote = _sim.QuoteRenovation(_plan, _renovationBatchSize);
        int materials = RenovationPricing.MaterialCostFor(plan, _renovationBatchSize);
        int days = RenovationPricing.BlockDaysFor(plan, _renovationBatchSize);
        GUI.Label(new Rect(192, y, w - 210, 24), $"{_renovationBatchSize} room(s)");
        y += 28;
        GUI.Label(new Rect(20, y, w - 40, 20),
            $"${quote} total (${RenovationPricing.CashPerRoomFor(plan, _renovationBatchSize)}/room), " +
            $"{materials} materials, shut {days} days"); y += 24;

        if (GUI.Button(new Rect(20, y, (w - 50) / 2f, 26), "BUY 10 MATERIALS"))
        {
            if (_sim.TryBuyMaterials(10)) Say("Materials delivered.");
            else Say("Can't afford materials right now.");
        }
        if (GUI.Button(new Rect(30 + (w - 50) / 2f, y, (w - 50) / 2f, 26), "START THE WORK"))
        {
            var rooms = _sim.RenovatableRooms(_plan, _renovationBatchSize);
            if (_sim.TryStartRenovation(_plan, rooms, out string reason))
                Say($"{rooms.Count} room(s) shut for {days} days. Better be worth it.");
            else Say(reason);
        }
        y += 30;
        GUI.Label(new Rect(20, y, w - 40, 20), $"Under renovation now: {_sim.Renovations.RoomsUnderRenovation} room(s)");
    }
}
