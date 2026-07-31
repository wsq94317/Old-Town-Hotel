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
    // 0.0004/日（约 1.2%/月）→ 本金真的会降，前期依然紧张。
    // M-C2 二轮：dailyRepayment 由"每天硬扣"改为**单日上限**，实扣走 DebtPolicy
    // 按昨日净利分成（固定硬扣会把玩家泵到 $0 永远攒不出首付，见 DebtPolicy 注释）。
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
    private int _reclaimBatchSize = 1;
    private ReclaimPlanKind _reclaimPlan = ReclaimPlanKind.PatchUp;
    private int _yesterdayNetProfit;

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
        _sim.Warehouse.SetCapacity(Warehouse.DefaultCapacity);   // 仓库会满
        _sim.Materials.Add(6); // 开局送几份材料，省得第一天什么都干不了
        _sim.FurnishInheritedRooms();  // 继承的破家具：崭新度 5-15%，天天出故障
    }

    private void Update()
    {
        // 自取触点：本场景没有 WorldInputController 转发点击，OnGUI 按钮全靠这条通道。
        // **必须在早退之前**——晨报页正处于 _awaitingMorningReport，早退会让它永远收不到点击。
        GuiInput.PollSelfServed();

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
        _yesterdayNetProfit = result.netToSafebox;

        _sim.Clock.BeginNextDay();
        _awaitingMorningReport = true;
        _tab = Tab.Report;
    }

    /// <summary>每日计划还款：仍自动扣（"忘按还款键"不是有趣的失败），
    /// 但金额按昨日净利分成、以 dailyRepayment 为单日上限——见 DebtPolicy。</summary>
    private int ScheduledRepayment() =>
        DebtPolicy.ScheduledRepaymentFor(_loan.Balance, _yesterdayNetProfit, _sim.Cash, dailyRepayment);

    /// <summary>提示条的唯一出口。在这里过一次翻译，Sim 返回的 reason 就自动跟着走，
    /// 不必在每个调用点包一层（查不到的原样显示英文）。</summary>
    private void Say(string message)
    {
        _toast = GameText.T(message);
        _toastUntil = Time.time + 3.5f;
    }

    // ── UI ───────────────────────────────────────────────────────────────────

    /// <summary>拒单那一行：**点明是哪几晚满了**（世界场景同款措辞）。
    /// 顶栏的"可售"是今天的空房，拒单说的是未来某晚的间夜卖光了——
    /// 不点明晚号，玩家会把这两个数字当成自相矛盾（实测反馈）。</summary>
    private string DeclinedLine()
    {
        var full = _sim.FullyBookedNights();
        if (full.Count == 0)
            return GameText.F("TURNED DOWN {0} BOOKINGS - they wanted bands you do not offer.",
                              _sim.BookingsDeclinedToday);
        if (full.Count == 1)
            return GameText.F("TURNED DOWN {0} BOOKINGS - night {1} is already full.",
                              _sim.BookingsDeclinedToday, full[0]);
        return GameText.F("TURNED DOWN {0} BOOKINGS - nights {1}-{2} are already full ({3} nights).",
                          _sim.BookingsDeclinedToday, full[0], full[full.Count - 1], full.Count);
    }

    private void OnGUI()
    {
        if (WorldManagementHud.IsActive) return;
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
            GameText.F("DAY {0}   {1}   {2}", clock.CurrentDay, clock.TimeFormatted,
                       GameText.T(PhaseScheduler.Label(PhaseScheduler.PhaseFor(clock.CurrentMinute)))));
        GUI.Label(new Rect(14, 28, w - 28, 20),
            GameText.F("CASH ${0}   SAFEBOX ${1}/{2}", _sim.Cash, _sim.Safebox.Balance, _sim.Safebox.Capacity) +
            (_sim.Overflow.Balance > 0 ? GameText.F("   SPILLED ${0}", _sim.Overflow.Balance) : "") +
            GameText.F("   DEBT ${0}", _loan.Balance));
        // **Blocked 必须显示**：坏家具会把房拽出可售循环，不显示的话玩家看到的是
        // 房间凭空消失（试玩实测：20 间房卡在 Blocked 里，玩家完全不知道发生了什么）
        GUI.Label(new Rect(14, 46, w - 28, 20),
            GameText.F("{0}*   ROOMS {1} ready / {2} dirty / {3} in use / {4} BROKEN / {5} derelict   MAT {6}",
                       _sim.Reputation.Stars.ToString("0.0"), _sim.Rooms.SellableCount, _sim.Rooms.DirtyBacklog,
                       _sim.Rooms.CountOf(RoomSimState.Occupied), _sim.Rooms.CountOf(RoomSimState.Blocked),
                       _sim.Rooms.CountOf(RoomSimState.Ruined), _sim.Materials.Stock));
    }

    private void DrawTabs(float w)
    {
        float bw = (w - 20) / 5f;
        if (GuiInput.Button(new Rect(10, 74, bw, 26), GameText.T("STATUS"))) _tab = Tab.Report;
        if (GuiInput.Button(new Rect(10 + bw, 74, bw, 26), GameText.T("PRICING"))) _tab = Tab.Pricing;
        if (GuiInput.Button(new Rect(10 + bw * 2, 74, bw, 26), GameText.T("STAFF"))) _tab = Tab.Staff;
        if (GuiInput.Button(new Rect(10 + bw * 3, 74, bw, 26), GameText.T("RENOVATE"))) _tab = Tab.Renovation;
        if (GuiInput.Button(new Rect(10 + bw * 4, 74, bw, 26), GameText.T("ROOMS"))) _tab = Tab.Rooms;

        // 时间控制（倍速标签是数字，不翻）
        if (GuiInput.Button(new Rect(10, 104, 52, 24), "0.25x")) _sim.Clock.SpeedMultiplier = 0.25f;
        if (GuiInput.Button(new Rect(66, 104, 52, 24), "1x")) _sim.Clock.SpeedMultiplier = 1f;
        if (GuiInput.Button(new Rect(122, 104, 52, 24), "2x")) _sim.Clock.SpeedMultiplier = 2f;
        if (GuiInput.Button(new Rect(178, 104, w - 188, 24), GameText.T("SKIP TO NEXT PHASE")))
        {
            if (PhaseScheduler.CanSkip(_sim.Clock.CurrentMinute, 0, out string reason))
                _sim.Clock.FastForwardTo(PhaseScheduler.NextKeyMinuteAfter(_sim.Clock.CurrentMinute));
            else Say(reason);
        }
    }

    private void DrawMorningReport(float w, float h)
    {
        var last = _sim.LastSettlement;

        var report = new GuiPanel();
        report.Add(GameText.F("Gross taken          ${0}",
            last.netToSafebox + last.overflowed + last.cashPaidFromReserve));
        report.Add(GameText.F("Into the safebox     ${0}", last.netToSafebox));
        report.AddIf(last.overflowed > 0, GameText.F("Spilled (65% back)   ${0}", last.overflowed));
        report.AddIf(last.cashPaidFromReserve > 0,
                     GameText.F("Covered from cash    ${0}", last.cashPaidFromReserve));
        report.Add(last.wagesPaid ? GameText.T("Payroll: everybody got paid.")
                                  : GameText.F("PAYROLL SHORT by ${0}. They noticed.", last.unpaidAmount));
        report.Add(GameText.F("Checked in {0}, turned away {1}, queue cost {2} min",
                              _sim.ArrivalsCheckedInToday, _sim.ArrivalsTurnedAwayToday,
                              _sim.TotalCheckInWaitToday));
        report.Add(GameText.F("Commission ${0}   Cancelled {1}   No-shows {2}",
                              _sim.CommissionToday, _sim.CancellationsToday, _sim.NoShowsToday));
        report.AddIf(_sim.BookingsDeclinedToday > 0,
                     DeclinedLine());
        report.Add(GameText.F("Rating {0}*   Debt ${1}",
                              _sim.Reputation.Stars.ToString("0.00"), _loan.Balance));
        AddReputationBreakdown(report);

        float y = report.Draw(10, 74, w - 20, GameText.F("YESTERDAY  (day {0})", _sim.Clock.CurrentDay - 1));
        y += 8;

        // 收钱按钮：有钱可收才占位置
        bool hasSafebox = _sim.Safebox.Balance > 0, hasSpill = _sim.Overflow.Balance > 0;
        if (hasSafebox || hasSpill)
        {
            float half = (w - 50) / 2f;
            if (hasSafebox &&
                GuiInput.Button(new Rect(20, y, hasSpill ? half : w - 40, 26),
                                GameText.F("COLLECT ${0}", _sim.Safebox.Balance)))
                Say(GameText.F("Collected ${0}.", _sim.CollectSafebox()));
            if (hasSpill &&
                GuiInput.Button(new Rect(hasSafebox ? 30 + half : 20, y, hasSafebox ? half : w - 40, 26),
                                GameText.F("SALVAGE ${0} (65%)", _sim.Overflow.Balance)))
                Say(GameText.F("Salvaged ${0} of the cash that wouldn't fit.", _sim.RecoverOverflow()));
            y += 32;
        }

        // 退款申请（虚报价的代价）——批准/拒绝就在晨报上做
        var refunds = _sim.PendingRefunds;
        if (refunds.Count > 0)
        {
            int shown = refunds.Count < 3 ? refunds.Count : 3;
            GUI.Box(new Rect(10, y, w - 20, 26 + shown * 42), GameText.F("REFUND DEMANDS ({0})", refunds.Count));
            float ry = y + 24;
            for (int i = 0; i < shown; i++)
            {
                var r = refunds[i];
                // 客人原话（r.line）是无厘头文案，翻译表里没有就照原样显示
                GUI.Label(new Rect(20, ry, w - 40, 19),
                          "R" + r.roomNumber + " $" + r.amount + ": " + GameText.T(r.line));
                float half = (w - 50) / 2f;
                if (GuiInput.Button(new Rect(20, ry + 19, half, 20), GameText.F("REFUND ${0}", r.amount)))
                {
                    Say(_sim.ApproveRefund(r.requestId) ? "Refunded. Reputation intact."
                        : GameText.F("Refund is ${0} and you have ${1}. Collect the safebox or refuse.",
                                     r.amount, _sim.Cash));
                    break;
                }
                if (GuiInput.Button(new Rect(30 + half, ry + 19, half, 20), GameText.T("REFUSE")))
                { _sim.RejectRefund(r.requestId); Say("Refused. They are writing a review as we speak."); break; }
                ry += 42;
            }
            return;   // 先处理完退款再开门
        }

        if (GuiInput.Button(new Rect(10, y, w - 20, 34), GameText.T("OPEN THE DOORS")))
        {
            _sim.BeginDay();
            _awaitingMorningReport = false;
            _tab = Tab.Report;
        }
    }

    /// <summary>客房部进度 + "不可售的房都卡在哪"，加进当日面板。
    ///
    /// 试玩时房间会成批地从可售列表里消失而玩家看不出原因（家具坏了？在装修？
    /// 还是根本没解锁？），所以这里把不可售的房**按原因拆开**，
    /// 并显示此刻正在打扫哪几间——`Cleaning` 状态就是为此才真正启用的。</summary>
    private void AddHousekeepingStatus(GuiPanel panel)
    {
        var cleaningNow = _sim.Rooms.RoomNumbersInState(RoomSimState.Cleaning, 6);
        int dirty = _sim.Rooms.CountOf(RoomSimState.Dirty);
        int awaiting = _sim.Rooms.CountOf(RoomSimState.AwaitingInspection);

        panel.Add(GameText.F("HOUSEKEEPING  done {0}   cleaning {1}   waiting {2}   to inspect {3}",
                             _sim.Pipeline.RoomsCleanedToday, cleaningNow.Count, dirty, awaiting));

        if (cleaningNow.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cleaningNow.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(cleaningNow[i]);
            }
            panel.Add(GameText.F("  in progress: {0}", sb.ToString()));
        }
        else if (dirty > 0)
        {
            panel.Add(GameText.T("  nobody is cleaning - off shift, slacking, or nobody hired"));
        }

        // 不可售的房按原因拆开：装修中 / 家具坏了 / 没解锁
        int renovating = 0, broken = 0;
        for (int i = 0; i < _sim.Rooms.Count; i++)
        {
            RoomRecord room = _sim.Rooms.Peek(i);
            if (room.state != RoomSimState.Blocked) continue;
            if (_sim.Renovations.IsRenovating(room.number)) renovating++;
            else broken++;
        }
        // 复原中的房还是 Ruined 状态（施工期间依然不可售），要从破败数里单列出来，
        // 否则玩家看不出"我已经花钱在开这几间了"
        int reclaiming = _sim.Renovations.RoomsUnderReclaim;
        int derelict = _sim.Rooms.CountOf(RoomSimState.Ruined) - reclaiming;
        panel.Add(GameText.F("UNSELLABLE    renovating {0}   broken {1}   derelict {2}   (sellable {3})",
                             renovating, broken, derelict < 0 ? 0 : derelict, _sim.Rooms.SellableCount));
        panel.AddIf(reclaiming > 0,
                    GameText.F("  reclaiming {0} derelict room(s) - building work in progress", reclaiming));
    }

    /// <summary>把"今天为什么涨/为什么掉"加进晨报面板。只列影响最大的两条正、两条负——
    /// 玩家要的是"该改什么"，不是一张完整的会计报表。</summary>
    private void AddReputationBreakdown(GuiPanel panel)
    {
        var down = _sim.Breakdown.Ranked(positive: false);
        var up = _sim.Breakdown.Ranked(positive: true);
        if (down.Count == 0 && up.Count == 0)
        {
            panel.Add(GameText.T("No guests rated you yesterday."));
            return;
        }

        for (int i = 0; i < down.Count && i < 2; i++)
        {
            var e = down[i];
            panel.Add(GameText.F("  DOWN  {0}   {1}  (x{2})",
                                 GameText.T(ReputationBreakdown.LabelOf(e.cause)),
                                 e.total.ToString("0.00"), e.count));
        }
        for (int i = 0; i < up.Count && i < 2; i++)
        {
            var e = up[i];
            panel.Add(GameText.F("  UP    {0}   +{1}  (x{2})",
                                 GameText.T(ReputationBreakdown.LabelOf(e.cause)),
                                 e.total.ToString("0.00"), e.count));
        }
    }

    private void DrawLiveStatus(float w, float h)
    {
        var today = new GuiPanel();
        today.Add(GameText.F("Expected arrivals   {0}   ({1} booked + {2} walk-in)",
                             _sim.ArrivalsPlannedToday, _sim.ReservationArrivalsToday,
                             _sim.WalkInArrivalsToday));
        today.Add(GameText.F("Checked in          {0}", _sim.ArrivalsCheckedInToday));
        today.Add(GameText.F("Turned away         {0}   (no clean room)", _sim.ArrivalsTurnedAwayToday));
        today.Add(GameText.F("Checkouts booked    {0}   (${1})", _sim.CheckoutsToday, _sim.GrossIncomeToday));
        today.Add(GameText.F("Flawed rooms sold   {0}   (no inspector on duty?)", _sim.FlawedStaysToday));
        today.Add(GameText.F("Staff working       {0} hsk, morale {1}",
                             _sim.Staff.ProductiveCountOfRole(StaffRole.Housekeeper),
                             _sim.Staff.AverageMorale.ToString("0")));
        today.Add(GameText.F("Next 7 nights sold  {0}   (cap {1})",
                             SoldNightsPreview(), _sim.Rooms.OpenRoomCount));
        AddHousekeepingStatus(today);

        float y = today.Draw(10, 134, w - 20, GameText.T("TODAY")) + 8;

        // 超售客站在前台等你拍板。拖到打烊 = 按硬赶处理还要额外掉声誉，所以要显眼
        var waiting = _sim.PendingOverbookings;
        if (waiting.Count > 0)
        {
            var incident = waiting[0];
            GUI.Box(new Rect(10, y, w - 20, 100),
                GameText.F("OVERBOOKED - {0} guest(s) with a room you don't have", waiting.Count));
            GUI.Label(new Rect(20, y + 24, w - 40, 19),
                GameText.F("Booked {0} at ${1}. Waited {2} min.",
                           GameText.T(incident.bookedBand.ToString()), incident.lockedPrice,
                           incident.waitMinutes));

            float half = (w - 50) / 2f;
            bool canUpgrade = _sim.CanUpgradeOverbooking(incident.incidentId);
            GUI.enabled = canUpgrade;
            if (GuiInput.Button(new Rect(20, y + 46, half, 22),
                                GameText.T(canUpgrade ? "UPGRADE THEM" : "NOTHING FREE YET")))
                Resolve(incident.incidentId, OverbookingResolution.Upgrade,
                        "Upgraded at the old price. They are thrilled.");
            GUI.enabled = true;

            if (GuiInput.Button(new Rect(30 + half, y + 46, half, 22),
                                GameText.F("PAY THEM OFF (${0})", incident.CompensationCost)))
                Resolve(incident.incidentId, OverbookingResolution.Compensate,
                        "Paid for a room down the road. Expensive apology.");

            if (GuiInput.Button(new Rect(20, y + 72, w - 40, 22),
                                GameText.T("SEND THEM AWAY (free, they will write about it)")))
                Resolve(incident.incidentId, OverbookingResolution.WalkAway,
                        "They left. Loudly.");
            return;   // 有人等着就先处置，别让玩家分心去开破房
        }

        if (_sim.TryFindRuinedRoom(out int _))
            GUI.Label(new Rect(20, y, w - 40, 19),
                GameText.T("Derelict rooms need building work - see the RENOVATE tab."));
    }

    private void Resolve(int incidentId, OverbookingResolution resolution, string success)
    {
        Say(_sim.TryResolveOverbooking(incidentId, resolution, out string reason) ? success : reason);
    }

    /// <summary>未来七晚各卖出多少间夜——玩家据此判断"该不该现在动装修"。</summary>
    private string SoldNightsPreview()
    {
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < 7; k++)
        {
            int day = _sim.Clock.CurrentDay + k;
            int sold = _sim.Calendar.DemandOn(day, RoomTier.Old)
                     + _sim.Calendar.DemandOn(day, RoomTier.Basic)
                     + _sim.Calendar.DemandOn(day, RoomTier.Better);
            sb.Append(sold).Append(k == 6 ? "" : "/");
        }
        return sb.ToString();
    }

    private void DrawPricing(float w, float h)
    {
        GUI.Box(new Rect(10, 134, w - 20, 190), GameText.T("PRICING - pick a strategy, not a spreadsheet"));
        float y = 158;
        foreach (PriceTemplate t in System.Enum.GetValues(typeof(PriceTemplate)))
        {
            bool active = _sim.Pricing.DefaultTemplate == t;
            if (GuiInput.Button(new Rect(20, y, w - 40, 26),
                                (active ? "> " : "  ") + GameText.T(PricingPolicy.LabelOf(t))))
                _sim.Pricing.DefaultTemplate = t;
            y += 30;
        }
        int day = _sim.Clock.CurrentDay;
        GUI.Label(new Rect(20, y, w - 40, 20),
            GameText.F("Tonight: ${0} old / ${1} basic / ${2} better",
                       _sim.Pricing.PriceFor(day, RoomTier.Old),
                       _sim.Pricing.PriceFor(day, RoomTier.Basic),
                       _sim.Pricing.PriceFor(day, RoomTier.Better)) +
            (PricingPolicy.IsWeekend(day) ? GameText.T("   (weekend)") : ""));
    }

    private void DrawStaff(float w, float h)
    {
        GUI.Box(new Rect(10, 134, w - 20, 200), GameText.T("STAFF - cheap shifts show up in the reviews"));
        float y = 158;
        foreach (StaffRole role in new[] { StaffRole.Reception, StaffRole.Housekeeper, StaffRole.Inspector })
        {
            ShiftTier tier = _sim.Shifts.TierOf(role);
            GUI.Label(new Rect(20, y, 110, 22), GameText.T(role.ToString()));
            if (GuiInput.Button(new Rect(130, y, w - 150, 22), GameText.T(ShiftPlan.LabelOf(tier))))
                _sim.Shifts.SetTier(role, NextTier(tier));
            y += 26;
        }
        y += 6;
        GUI.Label(new Rect(20, y, w - 40, 20),
            GameText.F("On duty {0}/{1}   wages today ${2}",
                       _sim.Staff.OnDutyCount, _sim.Staff.Count, _sim.Shifts.DailyWageCost(_sim.Staff))); y += 22;
        GUI.Label(new Rect(20, y, w - 40, 20),
            GameText.F("Clean capacity {0}/h   check-ins {1}/h",
                       ServiceCapacityModel.CleanRoomsPerHour(_sim.Staff, 1f).ToString("0.0"),
                       _sim.CurrentCheckInsPerHour.ToString("0.0"))); y += 26;

        if (GuiInput.Button(new Rect(20, y, w - 40, 26), GameText.T("HIRE A HOUSEKEEPER  ($200 signing)")))
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
        if (GuiInput.Button(new Rect(20, y, w - 40, 26), GameText.T("HIRE AN INSPECTOR  ($250 signing)")))
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
        GUI.Box(new Rect(10, 134, w - 20, 240), GameText.T("ROOMS - what you charge vs what you deliver"));
        float y = 158;

        GUI.Label(new Rect(20, y, w - 40, 20),
                  GameText.T("Price band for the whole hotel (what you claim it is):"));
        y += 22;
        float bw = (w - 50) / 3f;
        int i = 0;
        foreach (RoomTier band in System.Enum.GetValues(typeof(RoomTier)))
        {
            if (GuiInput.Button(new Rect(20 + i * (bw + 5), y, bw, 24),
                           GameText.T(band.ToString()) + " $" + _sim.Pricing.PriceFor(_sim.Clock.CurrentDay, band)))
            {
                for (int f = 0; f < FloorMath.FloorCount; f++) _sim.SetPriceBandForFloor(f, band);
                Say(GameText.F("Every room is now listed as {0}. Guests will judge.", GameText.T(band.ToString())));
            }
            i++;
        }
        y += 30;

        float delivered = _sim.AverageDeliveredQuality();
        RoomTier currentBand = _sim.Rooms.Count > 0 ? _sim.Rooms.Peek(0).tier : RoomTier.Old;
        float expected = DemandModel.ExpectedQualityOf(currentBand);
        float gap = delivered - expected;
        GUI.Label(new Rect(20, y, w - 40, 20),
            GameText.F("Delivered {0}  vs  promised {1}   gap {2}",
                       delivered.ToString("0.00"), expected.ToString("0.00"),
                       gap.ToString("+0.00;-0.00"))); y += 20;
        GUI.Label(new Rect(20, y, w - 40, 20),
            GameText.T(gap >= 0f ? "Guests are getting more than they paid for. Good reviews, less cash."
                                 : gap > -0.2f ? "Slightly oversold. Tolerable."
                                               : "OVERSOLD. Expect refund demands.")); y += 24;

        int faulted = _sim.Furniture.FaultedItems().Count;
        GUI.Label(new Rect(20, y, w - 40, 20),
            GameText.F("Furniture: {0} pieces, avg newness {1}, {2} broken",
                       _sim.Furniture.Count, AverageNewnessAllRooms().ToString("0.00"), faulted)); y += 24;

        var broken = _sim.Furniture.FaultedItems();
        for (int b = 0; b < broken.Count && b < 3; b++)
        {
            var item = broken[b];
            FurnitureKind kind = FurnitureCatalog.Get(item.kindId);
            // 故障文案是无厘头味道的核心，翻译表里没有的照原样显示英文
            string status = item.IsUnderRepair
                ? GameText.F(" [being fixed, {0}d]", item.repairDaysRemaining) : "";
            GUI.Label(new Rect(20, y, w - 150, 20),
                      "R" + item.roomNumber + ": " + GameText.T(FurnitureLedger.FaultLineOf(item)) + status);
            if (!item.IsUnderRepair)
            {
                if (GuiInput.Button(new Rect(w - 128, y - 2, 62, 22), GameText.F("FIX ${0}", kind.repairCost)))
                {
                    if (_sim.TryRepairFurniture(item.instanceId, out string reason))
                        Say(GameText.F("{0} in room {1}: {2} day(s) of work.",
                                       GameText.T(kind.name), item.roomNumber, kind.repairDays));
                    else Say(reason);
                }
                // 免费的应急路：没钱时唯一能让房间重新开卖的办法
                if (!item.taped &&
                    GuiInput.Button(new Rect(w - 62, y - 2, 42, 22), GameText.T("TAPE")))
                {
                    if (_sim.TryTapeFurniture(item.instanceId, out string reason))
                        Say(GameText.F("Taped up room {0}. Sellable, ugly, and broken again tomorrow.",
                                       item.roomNumber));
                    else Say(reason);
                }
            }
            y += 22;
        }

        if (broken.Count > 3)
            GUI.Label(new Rect(20, y, w - 40, 20), GameText.F("...and {0} more", broken.Count - 3));
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
        GUI.Box(new Rect(10, 134, w - 20, 200),
                GameText.T("RENOVATION - the cost is the nights you can't sell"));
        float y = 158;
        foreach (RenovationPlanKind kind in System.Enum.GetValues(typeof(RenovationPlanKind)))
        {
            bool active = _plan == kind;
            if (GuiInput.Button(new Rect(20, y, w - 40, 26),
                                (active ? "> " : "  ") + GameText.T(RenovationPlan.LabelOf(kind))))
                _plan = kind;
            y += 30;
        }

        var plan = RenovationPlan.For(_plan);
        if (GuiInput.Button(new Rect(20, y, 80, 24), GameText.T("- room")) && _renovationBatchSize > 1)
            _renovationBatchSize--;
        if (GuiInput.Button(new Rect(104, y, 80, 24), GameText.T("+ room"))) _renovationBatchSize++;
        int quote = _sim.QuoteRenovation(_plan, _renovationBatchSize);
        int materials = RenovationPricing.MaterialCostFor(plan, _renovationBatchSize);
        int days = RenovationPricing.BlockDaysFor(plan, _renovationBatchSize);
        GUI.Label(new Rect(192, y, w - 210, 24), GameText.F("{0} room(s)", _renovationBatchSize));
        y += 28;
        GUI.Label(new Rect(20, y, w - 40, 20),
            GameText.F("${0} total (${1}/room), {2} materials, shut {3} days",
                       quote, RenovationPricing.CashPerRoomFor(plan, _renovationBatchSize),
                       materials, days)); y += 24;

        if (GuiInput.Button(new Rect(20, y, (w - 50) / 2f, 26), GameText.T("BUY 10 MATERIALS")))
        {
            if (_sim.TryBuyMaterials(10)) Say("Materials delivered.");
            else if (_sim.MaterialsThatFit(10) < 10)
                Say(GameText.F("The warehouse only has room for {0} more. Space: {1}/{2}.",
                               _sim.MaterialsThatFit(10), _sim.Materials.Stock,
                               _sim.Warehouse.Capacity));
            else Say("Can't afford materials right now.");
        }
        if (GuiInput.Button(new Rect(30 + (w - 50) / 2f, y, (w - 50) / 2f, 26), GameText.T("START THE WORK")))
        {
            var rooms = _sim.RenovatableRooms(_plan, _renovationBatchSize);
            if (_sim.TryStartRenovation(_plan, rooms, out string reason))
                Say(GameText.F("{0} room(s) shut for {1} days. Better be worth it.", rooms.Count, days));
            else Say(reason);
        }
        y += 30;
        GUI.Label(new Rect(20, y, w - 40, 20),
            GameText.F("Under renovation now: {0} room(s)", _sim.Renovations.RoomsUnderRenovation)); y += 26;

        DrawReclaimSection(w, y);
    }

    /// <summary>破败房复原：**破败房不是脏，是废**，只能靠施工开出来。
    /// 三档的差别不在档位而在家具——修旧的最省但只配挂 Old，换全套最贵但直接够 Better。</summary>
    private void DrawReclaimSection(float w, float y)
    {
        var candidates = _sim.ReclaimableRooms(_reclaimBatchSize);
        GUI.Box(new Rect(10, y, w - 20, 148),
                GameText.F("DERELICT ROOMS - {0} left to open", _sim.Rooms.CountOf(RoomSimState.Ruined)));
        y += 24;

        foreach (ReclaimPlanKind kind in System.Enum.GetValues(typeof(ReclaimPlanKind)))
        {
            bool active = _reclaimPlan == kind;
            if (GuiInput.Button(new Rect(20, y, w - 40, 24),
                                (active ? "> " : "  ") + GameText.T(ReclaimPlan.LabelOf(kind))))
                _reclaimPlan = kind;
            y += 26;
        }

        var plan = ReclaimPlan.For(_reclaimPlan);
        if (GuiInput.Button(new Rect(20, y, 70, 22), GameText.T("- room")) && _reclaimBatchSize > 1)
            _reclaimBatchSize--;
        if (GuiInput.Button(new Rect(94, y, 70, 22), GameText.T("+ room"))) _reclaimBatchSize++;
        int batch = candidates.Count;
        GUI.Label(new Rect(172, y, w - 190, 22),
                  GameText.F("{0} room(s)", batch)); y += 24;

        GUI.Label(new Rect(20, y, w - 40, 20),
            GameText.F("${0} total, {1} materials, {2} days shut",
                       ReclaimPricing.CashCostFor(plan, batch),
                       ReclaimPricing.MaterialCostFor(plan, batch),
                       ReclaimPricing.BlockDaysFor(plan, batch))); y += 22;

        if (GuiInput.Button(new Rect(20, y, w - 40, 24), GameText.T("START BUILDING WORK")))
        {
            if (_sim.TryStartReclaim(_reclaimPlan, candidates, out string reason))
                Say(GameText.F("{0} derelict room(s) under construction.", candidates.Count));
            else Say(reason);
        }
    }
}
