using UnityEngine;

// v3 经营操作在**世界场景**里的入口（B1a）。
//
// 原型场景那套 UI 是全屏的——玩家只看得到表格，看不到酒店。这里改成**底部抽屉**：
// 上半屏永远是 2.5D 酒店（NPC 在自己岗位上干活），下半屏是当前页；
// 抽屉可以收起，收起后只剩一条顶栏，整间酒店尽收眼底。
//
// 数据全部读 HotelSimSceneBridge.Sim——世界场景现在跑的是同一套经济内核。
// 房态权威仍在 v1 的 StaffAgent（它是实体化的清洁模拟），B1b 再接收客与收入。
public class WorldOperationsPanel : MonoBehaviour
{
    private enum Tab { Today, Pricing, Staff, Build, Rooms }

    private Tab _tab = Tab.Today;
    private bool _collapsed;
    private float _speed = 1f;          // 玩家选的倍速（Time.timeScale）
    private int _skipTargetMinute = -1; // >=0 表示正在快进到该分钟
    private int _skipDay = -1;
    private string _toast = "";
    private float _toastUntil;
    private RenovationPlanKind _plan = RenovationPlanKind.Economy;
    private ReclaimPlanKind _reclaimPlan = ReclaimPlanKind.PatchUp;
    private int _batch = 1;

    private HotelSim Sim => HotelSimSceneBridge.Instance != null ? HotelSimSceneBridge.Instance.Sim : null;

    private void Update()
    {
        // **不要**在这里调 GuiInput.PollSelfServed()：世界场景有 WorldInputController，
        // 它会在热区内把点击转发给 GUI（松手时）。若这里再按下沿自取一次，
        // 同一次点击就发布两遍——"买 10 份材料"会买两次（双通道双触发，v2 踩过的坑）。
        // 自取通道只给没有输入控制器的原型场景用。
        TickSkip();
    }

    /// <summary>"跳到下一时段" = 临时把 Time.timeScale 拉高，直到 Sim 走到目标分钟。
    ///
    /// 为什么不直接 FastForwardTo：世界场景里有**两套时钟**（v1 的日控制器 + Sim），
    /// 只快进 Sim 会让经济瞬移而 NPC 还在原地慢慢走，两边彻底脱节。
    /// 拉 timeScale 则所有用 Time.deltaTime 的东西一起加速——NPC 肉眼可见地跑起来，
    /// v1 的日结也按比例提前，这正是"快速模拟"该有的样子。</summary>
    private void TickSkip()
    {
        if (_skipTargetMinute < 0)
        {
            if (Time.timeScale != _speed) Time.timeScale = _speed;
            return;
        }

        var clock = Sim != null ? Sim.Clock : null;
        bool done = clock == null
                    || clock.CurrentDay != _skipDay              // 日结把天翻过去了
                    || clock.CurrentMinute >= _skipTargetMinute;
        if (done)
        {
            _skipTargetMinute = -1;
            Time.timeScale = _speed;
            return;
        }
        Time.timeScale = 10f;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;   // 别把加速状态留给别的场景
    }

    private void Say(string message)
    {
        _toast = GameText.T(message);
        _toastUntil = Time.time + 3.5f;
    }

    private void OnGUI()
    {
        if (Sim == null) return;

        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        // 日结 → **全屏晨报**：时间已被桥暂停，看完点"开门营业"才进新的一天。
        // 全屏 = 整个屏幕都是热区，世界这时收不到任何点击。
        var bridge = HotelSimSceneBridge.Instance;
        if (bridge != null && bridge.AwaitingMorningReport)
        {
            GuiInput.ReserveZone(new Rect(0, 0, w, h));
            DrawMorningReport(w, h);
            return;
        }

        DrawTopBar(w);

        float sheetTop = UiLayout.DrawerTop(h, _collapsed);

        // **登记热区，否则点击穿透到世界**：WorldInputController 只有在
        // GuiInput.IsInReservedZone 命中时才把点击转发给 GUI，不然照常打世界射线——
        // 点抽屉里的按钮会同时让经理跑过去（试玩截图抓到的点穿就是这个）。
        GuiInput.ReserveZone(new Rect(6, 6, w - 12, 62));                         // 顶栏
        GuiInput.ReserveZone(new Rect(10, sheetTop - 26f, w - 20, 26f));          // 折叠按钮那一条
        if (!_collapsed)
            GuiInput.ReserveZone(new Rect(6, sheetTop, w - 12, h - sheetTop));    // 整个抽屉

        // 折叠按钮永远在抽屉顶边上，位置一眼可预测
        if (GuiInput.Button(new Rect(10, sheetTop - 26f, 120, 24f),
                            GameText.T(_collapsed ? "OPEN THE DESK" : "HIDE THE DESK")))
            _collapsed = !_collapsed;

        if (Time.time < _toastUntil)
            GUI.Box(new Rect(140, sheetTop - 26f, w - 150, 24f), _toast);

        if (_collapsed) return;

        GUI.Box(new Rect(6, sheetTop, w - 12, h - sheetTop - 6f), "");
        float y = sheetTop + 8f;
        y = DrawSpeedRow(w, y);
        y = DrawTabs(w, y);

        switch (_tab)
        {
            case Tab.Pricing: DrawPricing(w, y); break;
            case Tab.Staff: DrawStaff(w, y); break;
            case Tab.Build: DrawBuild(w, y); break;
            case Tab.Rooms: DrawRooms(w, y); break;
            default: DrawToday(w, y); break;
        }
    }

    /// <summary>全屏晨报：昨日结算 + 评价明细 + 收保险箱 + 退款闸门。
    /// 退款没处理完不给开门（和原型同一条规矩：拖着不处理绝不能划算）。</summary>
    private void DrawMorningReport(float w, float h)
    {
        GUI.Box(new Rect(0, 0, w, h), "");
        var last = Sim.LastSettlement;

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
                              Sim.ArrivalsCheckedInToday, Sim.ArrivalsTurnedAwayToday,
                              Sim.TotalCheckInWaitToday));
        report.Add(GameText.F("Commission ${0}   Cancelled {1}   No-shows {2}",
                              Sim.CommissionToday, Sim.CancellationsToday, Sim.NoShowsToday));
        report.AddIf(Sim.BookingsDeclinedToday > 0,
                     GameText.F("TURNED DOWN {0} BOOKINGS - no rooms left to sell.",
                                Sim.BookingsDeclinedToday));
        report.Add(GameText.F("Rating {0}*", Sim.Reputation.Stars.ToString("0.00")));
        AddReputationBreakdown(report);

        float y = report.Draw(10, 40, w - 20,
                              GameText.F("YESTERDAY  (day {0})", Sim.Clock.CurrentDay - 1)) + 10f;

        // 收钱按钮：有钱可收才占位置
        bool hasSafebox = Sim.Safebox.Balance > 0, hasSpill = Sim.Overflow.Balance > 0;
        if (hasSafebox || hasSpill)
        {
            float half = (w - 50) / 2f;
            if (hasSafebox &&
                GuiInput.Button(new Rect(20, y, hasSpill ? half : w - 40, 28),
                                GameText.F("COLLECT ${0}", Sim.Safebox.Balance)))
                Say(GameText.F("Collected ${0}.", Sim.CollectSafebox()));
            if (hasSpill &&
                GuiInput.Button(new Rect(hasSafebox ? 30 + half : 20, y, hasSafebox ? half : w - 40, 28),
                                GameText.F("SALVAGE ${0} (65%)", Sim.Overflow.Balance)))
                Say(GameText.F("Salvaged ${0} of the cash that wouldn't fit.", Sim.RecoverOverflow()));
            y += 34f;
        }

        // 退款闸门：处理完才给开门
        var refunds = Sim.PendingRefunds;
        if (refunds.Count > 0)
        {
            int shown = refunds.Count < 3 ? refunds.Count : 3;
            GUI.Box(new Rect(10, y, w - 20, 26 + shown * 44), GameText.F("REFUND DEMANDS ({0})", refunds.Count));
            float ry = y + 24;
            for (int i = 0; i < shown; i++)
            {
                var r = refunds[i];
                GUI.Label(new Rect(20, ry, w - 40, 19),
                          "R" + r.roomNumber + " $" + r.amount + ": " + GameText.T(r.line));
                float half = (w - 50) / 2f;
                if (GuiInput.Button(new Rect(20, ry + 19, half, 22), GameText.F("REFUND ${0}", r.amount)))
                { Sim.ApproveRefund(r.requestId); Say("Refunded. Reputation intact."); break; }
                if (GuiInput.Button(new Rect(30 + half, ry + 19, half, 22), GameText.T("REFUSE")))
                { Sim.RejectRefund(r.requestId); Say("Refused. They are writing a review as we speak."); break; }
                ry += 44;
            }
            if (Time.time < _toastUntil)
                GUI.Box(new Rect(10, h - 40, w - 20, 26), _toast);
            return;   // 先处理完退款再开门
        }

        if (GuiInput.Button(new Rect(10, y, w - 20, 40), GameText.T("OPEN THE DOORS")))
            HotelSimSceneBridge.Instance.ContinueToNextDay();

        if (Time.time < _toastUntil)
            GUI.Box(new Rect(10, h - 40, w - 20, 26), _toast);
    }

    /// <summary>把"今天为什么涨/为什么掉"加进晨报（与原型同一套明细）。</summary>
    private void AddReputationBreakdown(GuiPanel panel)
    {
        var down = Sim.Breakdown.Ranked(positive: false);
        var up = Sim.Breakdown.Ranked(positive: true);
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

    /// <summary>顶栏压在酒店画面上方，只占三行——玩家随时看得见钱、评分、房况。</summary>
    private void DrawTopBar(float w)
    {
        var clock = Sim.Clock;
        GUI.Box(new Rect(6, 6, w - 12, 62), "");
        // 倍速指示常驻顶栏：跳段的 10 倍速若没有可见提示，玩家看到的就是
        // "速度莫名忽快忽慢"（试玩反馈原话）。变速必须永远有解释。
        string speedTag = _skipTargetMinute >= 0 ? ">>> x10"
                        : _speed != 1f ? "x" + _speed.ToString("0.##") : "";
        GUI.Label(new Rect(14, 10, w - 28, 20),
            GameText.F("DAY {0}   {1}   {2}", clock.CurrentDay, clock.TimeFormatted,
                       GameText.T(PhaseScheduler.Label(PhaseScheduler.PhaseFor(clock.CurrentMinute))))
            + (speedTag.Length > 0 ? "   " + speedTag : ""));
        GUI.Label(new Rect(14, 28, w - 28, 20),
            GameText.F("CASH ${0}   SAFEBOX ${1}/{2}", Sim.Cash, Sim.Safebox.Balance, Sim.Safebox.Capacity));
        GUI.Label(new Rect(14, 46, w - 28, 20),
            GameText.F("{0}*   ROOMS {1} ready / {2} dirty / {3} in use / {4} BROKEN / {5} derelict   MAT {6}",
                       Sim.Reputation.Stars.ToString("0.0"), Sim.Rooms.SellableCount, Sim.Rooms.DirtyBacklog,
                       Sim.Rooms.CountOf(RoomSimState.Occupied), Sim.Rooms.CountOf(RoomSimState.Blocked),
                       Sim.Rooms.CountOf(RoomSimState.Ruined), Sim.Materials.Stock));
    }

    /// <summary>倍速与"跳到下一时段"（原型就有，玩家点名要回来）。</summary>
    private float DrawSpeedRow(float w, float y)
    {
        bool skipping = _skipTargetMinute >= 0;

        if (GuiInput.Button(new Rect(10, y, 52, 22), (_speed == 0.25f ? "> " : "") + "0.25x")) SetSpeed(0.25f);
        if (GuiInput.Button(new Rect(66, y, 52, 22), (_speed == 1f ? "> " : "") + "1x")) SetSpeed(1f);
        if (GuiInput.Button(new Rect(122, y, 52, 22), (_speed == 2f ? "> " : "") + "2x")) SetSpeed(2f);

        if (GuiInput.Button(new Rect(182, y, w - 192, 22),
                            GameText.T(skipping ? "FAST-FORWARDING..." : "SKIP TO NEXT PHASE"))
            && !skipping)
        {
            if (PhaseScheduler.CanSkip(Sim.Clock.CurrentMinute, 0, out string reason))
            {
                _skipTargetMinute = PhaseScheduler.NextKeyMinuteAfter(Sim.Clock.CurrentMinute);
                _skipDay = Sim.Clock.CurrentDay;
            }
            else Say(reason);
        }
        return y + 26f;
    }

    private void SetSpeed(float speed)
    {
        _speed = speed;
        if (_skipTargetMinute < 0) Time.timeScale = speed;
    }

    private float DrawTabs(float w, float y)
    {
        float bw = (w - 20) / 5f;
        if (GuiInput.Button(new Rect(10, y, bw, 24), GameText.T("STATUS"))) _tab = Tab.Today;
        if (GuiInput.Button(new Rect(10 + bw, y, bw, 24), GameText.T("PRICING"))) _tab = Tab.Pricing;
        if (GuiInput.Button(new Rect(10 + bw * 2, y, bw, 24), GameText.T("STAFF"))) _tab = Tab.Staff;
        if (GuiInput.Button(new Rect(10 + bw * 3, y, bw, 24), GameText.T("RENOVATE"))) _tab = Tab.Build;
        if (GuiInput.Button(new Rect(10 + bw * 4, y, bw, 24), GameText.T("ROOMS"))) _tab = Tab.Rooms;
        return y + 28f;
    }

    // ── 各页 ─────────────────────────────────────────────────────────────────

    private void DrawToday(float w, float y)
    {
        var panel = new GuiPanel();
        panel.Add(GameText.F("Expected arrivals   {0}   ({1} booked + {2} walk-in)",
                             Sim.ArrivalsPlannedToday, Sim.ReservationArrivalsToday, Sim.WalkInArrivalsToday));
        panel.Add(GameText.F("Checked in          {0}", Sim.ArrivalsCheckedInToday));
        panel.Add(GameText.F("Turned away         {0}   (no clean room)", Sim.ArrivalsTurnedAwayToday));
        panel.Add(GameText.F("Checkouts booked    {0}   (${1})", Sim.CheckoutsToday, Sim.GrossIncomeToday));

        var cleaning = Sim.Rooms.RoomNumbersInState(RoomSimState.Cleaning, 6);
        panel.Add(GameText.F("HOUSEKEEPING  done {0}   cleaning {1}   waiting {2}   to inspect {3}",
                             Sim.Pipeline.RoomsCleanedToday, cleaning.Count,
                             Sim.Rooms.CountOf(RoomSimState.Dirty),
                             Sim.Rooms.CountOf(RoomSimState.AwaitingInspection)));
        if (cleaning.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cleaning.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(cleaning[i]);
            }
            panel.Add(GameText.F("  in progress: {0}", sb.ToString()));
        }
        panel.Add(GameText.F("Next 7 nights sold  {0}   (cap {1})",
                             SoldNightsPreview(), Sim.Rooms.OpenRoomCount));
        panel.Draw(10, y, w - 20, GameText.T("TODAY"));
    }

    private void DrawPricing(float w, float y)
    {
        foreach (PriceTemplate t in System.Enum.GetValues(typeof(PriceTemplate)))
        {
            bool active = Sim.Pricing.DefaultTemplate == t;
            if (GuiInput.Button(new Rect(10, y, w - 20, 24),
                                (active ? "> " : "  ") + GameText.T(PricingPolicy.LabelOf(t))))
                Sim.Pricing.DefaultTemplate = t;
            y += 27f;
        }
        int day = Sim.Clock.CurrentDay;
        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("Tonight: ${0} old / ${1} basic / ${2} better",
                       Sim.Pricing.PriceFor(day, RoomTier.Old),
                       Sim.Pricing.PriceFor(day, RoomTier.Basic),
                       Sim.Pricing.PriceFor(day, RoomTier.Better)));
    }

    private void DrawStaff(float w, float y)
    {
        foreach (StaffRole role in new[] { StaffRole.Reception, StaffRole.Housekeeper, StaffRole.Inspector })
        {
            ShiftTier tier = Sim.Shifts.TierOf(role);
            GUI.Label(new Rect(14, y, 96, 22), GameText.T(role.ToString()));
            if (GuiInput.Button(new Rect(112, y, w - 128, 22), GameText.T(ShiftPlan.LabelOf(tier))))
                Sim.Shifts.SetTier(role, tier == ShiftTier.Full ? ShiftTier.Skeleton : (ShiftTier)((int)tier + 1));
            y += 26f;
        }
        y += 4f;
        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("On duty {0}/{1}   wages today ${2}",
                       Sim.Staff.OnDutyCount, Sim.Staff.Count, Sim.Shifts.DailyWageCost(Sim.Staff))); y += 22f;
        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("Clean capacity {0}/h   check-ins {1}/h",
                       ServiceCapacityModel.CleanRoomsPerHour(Sim.Staff, 1f).ToString("0.0"),
                       ServiceCapacityModel.CheckInsPerHour(Sim.Staff).ToString("0.0")));
    }

    /// <summary>装修 + 破败房复原共用一页（施工队是同一支）。</summary>
    private void DrawBuild(float w, float y)
    {
        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("Under renovation now: {0} room(s)", Sim.Renovations.RoomsUnderRenovation)); y += 24f;

        foreach (RenovationPlanKind kind in System.Enum.GetValues(typeof(RenovationPlanKind)))
        {
            bool active = _plan == kind;
            if (GuiInput.Button(new Rect(10, y, w - 20, 22),
                                (active ? "> " : "  ") + GameText.T(RenovationPlan.LabelOf(kind))))
                _plan = kind;
            y += 25f;
        }

        var plan = RenovationPlan.For(_plan);
        if (GuiInput.Button(new Rect(10, y, 62, 22), GameText.T("- room")) && _batch > 1) _batch--;
        if (GuiInput.Button(new Rect(76, y, 62, 22), GameText.T("+ room"))) _batch++;
        GUI.Label(new Rect(146, y, w - 160, 22), GameText.F("{0} room(s)", _batch)); y += 26f;

        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("${0} total (${1}/room), {2} materials, shut {3} days",
                       Sim.QuoteRenovation(_plan, _batch),
                       RenovationPricing.CashPerRoomFor(plan, _batch),
                       RenovationPricing.MaterialCostFor(plan, _batch),
                       RenovationPricing.BlockDaysFor(plan, _batch))); y += 24f;

        float half = (w - 30) / 2f;
        if (GuiInput.Button(new Rect(10, y, half, 24), GameText.T("BUY 10 MATERIALS")))
            Say(Sim.TryBuyMaterials(10) ? "Materials delivered." : "Can't afford materials right now.");
        if (GuiInput.Button(new Rect(20 + half, y, half, 24), GameText.T("START THE WORK")))
        {
            var rooms = Sim.RenovatableRooms(_plan, _batch);
            Say(Sim.TryStartRenovation(_plan, rooms, out string reason)
                ? GameText.F("{0} room(s) shut for {1} days. Better be worth it.",
                             rooms.Count, RenovationPricing.BlockDaysFor(plan, rooms.Count))
                : reason);
        }
        y += 30f;

        // 破败房复原
        int derelict = Sim.Rooms.CountOf(RoomSimState.Ruined);
        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("DERELICT ROOMS - {0} left to open", derelict)); y += 22f;
        if (derelict > 0)
        {
            if (GuiInput.Button(new Rect(10, y, w - 20, 22),
                                "> " + GameText.T(ReclaimPlan.LabelOf(_reclaimPlan))))
                _reclaimPlan = _reclaimPlan == ReclaimPlanKind.FullFit
                    ? ReclaimPlanKind.PatchUp : (ReclaimPlanKind)((int)_reclaimPlan + 1);
            y += 26f;

            var candidates = Sim.ReclaimableRooms(_batch);
            var rp = ReclaimPlan.For(_reclaimPlan);
            GUI.Label(new Rect(14, y, w - 28, 20),
                GameText.F("${0} total, {1} materials, {2} days shut",
                           ReclaimPricing.CashCostFor(rp, candidates.Count),
                           ReclaimPricing.MaterialCostFor(rp, candidates.Count),
                           ReclaimPricing.BlockDaysFor(rp, candidates.Count))); y += 24f;

            if (GuiInput.Button(new Rect(10, y, w - 20, 24), GameText.T("START BUILDING WORK")))
                Say(Sim.TryStartReclaim(_reclaimPlan, candidates, out string reason)
                    ? GameText.F("{0} derelict room(s) under construction.", candidates.Count)
                    : reason);
        }
    }

    private void DrawRooms(float w, float y)
    {
        GUI.Label(new Rect(14, y, w - 28, 20),
                  GameText.T("Price band for the whole hotel (what you claim it is):")); y += 22f;

        float bw = (w - 40) / 3f;
        int i = 0;
        foreach (RoomTier band in System.Enum.GetValues(typeof(RoomTier)))
        {
            if (GuiInput.Button(new Rect(10 + i * (bw + 5), y, bw, 24),
                                GameText.T(band.ToString()) + " $" + Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, band)))
            {
                for (int f = 0; f < FloorMath.FloorCount; f++) Sim.SetPriceBandForFloor(f, band);
                Say(GameText.F("Every room is now listed as {0}. Guests will judge.",
                               GameText.T(band.ToString())));
            }
            i++;
        }
        y += 30f;

        float delivered = Sim.AverageDeliveredQuality();
        RoomTier current = Sim.Rooms.Count > 0 ? Sim.Rooms.Peek(0).tier : RoomTier.Old;
        float expected = DemandModel.ExpectedQualityOf(current);
        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("Delivered {0}  vs  promised {1}   gap {2}",
                       delivered.ToString("0.00"), expected.ToString("0.00"),
                       (delivered - expected).ToString("+0.00;-0.00"))); y += 22f;

        var broken = Sim.Furniture.FaultedItems();
        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("Furniture: {0} pieces, avg newness {1}, {2} broken",
                       Sim.Furniture.Count, AverageNewness().ToString("0.00"), broken.Count)); y += 22f;

        for (int b = 0; b < broken.Count && b < 2; b++)
        {
            var item = broken[b];
            FurnitureKind kind = FurnitureCatalog.Get(item.kindId);
            GUI.Label(new Rect(14, y, w - 150, 20),
                      "R" + item.roomNumber + ": " + GameText.T(FurnitureLedger.FaultLineOf(item)));
            if (!item.IsUnderRepair)
            {
                if (GuiInput.Button(new Rect(w - 132, y - 2, 62, 22), GameText.F("FIX ${0}", kind.repairCost)))
                    Say(Sim.TryRepairFurniture(item.instanceId, out string reason)
                        ? GameText.F("{0} in room {1}: {2} day(s) of work.",
                                     GameText.T(kind.name), item.roomNumber, kind.repairDays)
                        : reason);
                if (!item.taped &&
                    GuiInput.Button(new Rect(w - 66, y - 2, 46, 22), GameText.T("TAPE")))
                    Say(Sim.TryTapeFurniture(item.instanceId, out string reason)
                        ? GameText.F("Taped up room {0}. Sellable, ugly, and broken again tomorrow.",
                                     item.roomNumber)
                        : reason);
            }
            y += 22f;
        }
    }

    private float AverageNewness()
    {
        var all = Sim.Furniture.All;
        if (all.Count == 0) return 1f;
        float sum = 0f;
        for (int i = 0; i < all.Count; i++) sum += all[i].newness;
        return sum / all.Count;
    }

    private string SoldNightsPreview()
    {
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < 7; k++)
        {
            int day = Sim.Clock.CurrentDay + k;
            int sold = Sim.Calendar.DemandOn(day, RoomTier.Old)
                     + Sim.Calendar.DemandOn(day, RoomTier.Basic)
                     + Sim.Calendar.DemandOn(day, RoomTier.Better);
            sb.Append(sold).Append(k == 6 ? "" : "/");
        }
        return sb.ToString();
    }
}
