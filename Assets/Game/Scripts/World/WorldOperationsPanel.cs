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

    /// <summary>全屏晨报的不透明底。GUI.Box 默认皮肤半透明，场景会透出来搅乱阅读；
    /// 报告是一天的收束时刻，底下的世界该完全让位。</summary>
    private static Texture2D _reportBackdrop;

    private static Texture2D ReportBackdrop
    {
        get
        {
            if (_reportBackdrop == null)   // 域重载后静态清空，惰性重建
            {
                _reportBackdrop = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _reportBackdrop.SetPixel(0, 0, new Color(0.09f, 0.10f, 0.13f, 1f));
                _reportBackdrop.Apply();
            }
            return _reportBackdrop;
        }
    }

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
            GuiInput.ReserveZone(GuiScale.FullScreenVirtualRect());
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
        y = DrawSelectedRoomCard(w, y);
        y = DrawSpeedRow(w, y);

        // 存档入口：三个槽位，可存可读（玩家要求）
        if (GuiInput.Button(new Rect(w - 92, y - 30f, 82, 22), GameText.T("SAVES")))
            SaveSlotPanel.Toggle();
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
        // 不透明底要盖住**整块物理屏幕**：安全区坐标系里 (0,0,w,h) 盖不住刘海带
        // 和底部条，世界会从上下两条缝里露出来（试玩截图抓到的正是这个）
        GUI.DrawTexture(GuiScale.FullScreenVirtualRect(), ReportBackdrop);
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
        // 事故：家具塌了压伤客人。一天最多一起，所以一行说完
        report.AddIf(Sim.InjuriesToday > 0, GameText.T(Sim.LastInjuryLine));

        // 夜班的成绩单：接住了几个、几个吃了闭门羹（玩家靠这两行决定要不要排夜班）
        report.AddIf(Sim.NightCheckInsToday > 0,
                     GameText.F("NIGHT DESK took in {0} late arrival(s) - rooms you'd have lost.",
                                Sim.NightCheckInsToday));
        report.AddIf(Sim.NightLockedOutToday > 0,
                     GameText.F("{0} guest(s) found a locked door at night. They wrote about it.",
                                Sim.NightLockedOutToday));
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

        // ── 两笔要玩家亲手付的钱（用户设计：要肉疼）──────────────────────────
        // 顺序刻意：先给他收钱的快感，紧接着让他把一部分交出去。
        y = DrawBillsToPay(w, y);

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
                {
                    // 退款从现金掏：没钱时会失败，不能谎报"已退款"
                    Say(Sim.ApproveRefund(r.requestId) ? "Refunded. Reputation intact."
                        : GameText.F("Refund is ${0} and you have ${1}. Collect the safebox or refuse.",
                                     r.amount, Sim.Cash));
                    break;
                }
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

    /// <summary>世界里点中的那间房：状态、房况、以及**这一刻真正能做的事**。
    ///
    /// 玩家反馈"场景里还没有玩家可以解锁和装修的房间"——症结不是缺功能
    /// （清理/复原/装修/维修的逻辑全都有），是那些功能和那栋楼没有关系：
    /// 房间在场景里连碰撞体都没有，只能在抽屉页里对着房号列表盲操作。
    /// 现在：世界里点一下房间 → 这张卡出现 → 按钮就在手边。</summary>
    private float DrawSelectedRoomCard(float w, float y)
    {
        int number = RoomSelection.Selected;
        if (number <= 0 || !Sim.Rooms.Contains(number))
        {
            GUI.Label(new Rect(14, y, w - 28, 20), GameText.T("Tap a room in the hotel to work on it."));
            return y + 22f;
        }

        RoomSimState state = Sim.Rooms.At(number).state;
        GUI.Box(new Rect(6, y, w - 12, 24), GameText.F("ROOM {0} - {1}   [{2}]",
                                                      number,
                                                      GameText.T(RoomStatePalette.WordOf(state)),
                                                      GameText.T(FurnitureLedger.ConditionWord(
                                                          Sim.Furniture.WorstHealthIn(number), false))));
        y += 28f;

        // 破败房：清理（免费，占人工）
        if (state == RoomSimState.Ruined)
        {
            if (Sim.Clearing.IsClearing(number))
            {
                float progress = Sim.Clearing.ProgressOf(number);
                DrawProgressBar(new Rect(14, y + 3, w - 120, 14), progress);
                GUI.Label(new Rect(w - 100, y, 90, 20),
                          GameText.F("{0} more trips to go",
                                     JunkClearingModel.VisitsRemaining(Sim.Clearing.WorkDoneOn(number))));
                y += 24f;
            }
            else if (GuiInput.Button(new Rect(10, y, w - 20, 26),
                                     GameText.F("CLEAR OUT R{0} (free - costs housekeeping time)", number)))
            {
                Say(Sim.TryStartJunkClearing(number, out string reason)
                        ? GameText.F("R{0}: cobwebs and rubbish. Takes about {1} trips.",
                                     number, JunkClearingModel.VisitsForOneRoom)
                        : reason);
                y += 30f;
            }
            else y += 30f;

            // 花钱买速度：复原方案（现有三档）
            var one = new System.Collections.Generic.List<int> { number };
            var rp = ReclaimPlan.For(_reclaimPlan);
            if (GuiInput.Button(new Rect(10, y, w - 20, 24),
                                GameText.F("or {0} - ${1}, {2} materials, {3} days",
                                           GameText.T(ReclaimPlan.LabelOf(_reclaimPlan)),
                                           ReclaimPricing.CashCostFor(rp, 1),
                                           ReclaimPricing.MaterialCostFor(rp, 1),
                                           ReclaimPricing.BlockDaysFor(rp, 1))))
                Say(Sim.TryStartReclaim(_reclaimPlan, one, out string why)
                        ? GameText.F("R{0} is a building site now.", number) : why);
            y += 28f;
            return y;
        }

        // 家具坏了：修 / 糊胶带就在这张卡上
        var brokenHere = new System.Collections.Generic.List<FurnitureInstance>();
        foreach (var item in Sim.Furniture.InRoom(number))
            if (item.IsFaulted) brokenHere.Add(item);

        if (brokenHere.Count > 0)
        {
            var item = brokenHere[0];
            FurnitureKind kind = FurnitureCatalog.Get(item.kindId);
            GUI.Label(new Rect(14, y, w - 28, 20), GameText.T(FurnitureLedger.FaultLineOf(item)));
            y += 22f;
            float half = (w - 30) / 2f;
            if (!item.wrecked && !item.IsUnderRepair &&
                GuiInput.Button(new Rect(10, y, half, 24), GameText.F("FIX ${0}", kind.repairCost)))
                Say(Sim.TryRepairFurniture(item.instanceId, out string reason)
                        ? GameText.F("{0} in room {1}: {2} day(s) of work. It will only come back as good as it is old.",
                                     GameText.T(kind.name), number, kind.repairDays) : reason);
            if (!item.taped &&
                GuiInput.Button(new Rect(20 + half, y, half, 24), GameText.T("TAPE")))
                Say(Sim.TryTapeFurniture(item.instanceId, out string reason)
                        ? GameText.F("Taped up room {0}. Sellable, ugly, and broken again tomorrow.", number)
                        : reason);
            y += 28f;
            return y;
        }

        // 好房：升级装修
        if (GuiInput.Button(new Rect(10, y, w - 20, 24),
                            GameText.F("RENOVATE R{0} - {1}", number,
                                       GameText.T(RenovationPlan.LabelOf(_plan)))))
        {
            var one = new System.Collections.Generic.List<int> { number };
            Say(Sim.TryStartRenovation(_plan, one, out string reason)
                    ? GameText.F("R{0} is shut for the works.", number) : reason);
        }
        y += 28f;
        return y;
    }

    /// <summary>晨报上的账单区：发工资 + 还贷。
    /// **两笔都从现金扣、不够动保险箱**——玩家亲手付（肉疼），
    /// 但"忘了按收取"绝不会害他欠薪（修订版 3 的铁律）。</summary>
    private float DrawBillsToPay(float w, float y)
    {
        int owed = Sim.Payroll.Owed;
        bool weekly = Sim.Payroll.Cycle == PayrollCycle.Weekly;
        int daysLeft = Sim.Payroll.DaysUntilPayday(Sim.Clock.CurrentDay);

        if (owed > 0)
        {
            // 周付的常驻提醒：本周待付多少、还剩几天（用户明确要求）
            GUI.Label(new Rect(14, y, w - 28, 20),
                      weekly
                          ? GameText.F("PAYROLL  ${0} owed, payday in {1} day(s)", owed, daysLeft)
                          : GameText.F("PAYROLL  ${0} owed today", owed));
            y += 22f;

            float half = (w - 30) / 2f;
            if (GuiInput.Button(new Rect(10, y, half, 28), GameText.F("PAY WAGES ${0}", owed)))
            {
                var payment = Sim.PayWages();
                Say(payment.cleared
                        ? GameText.F("Paid ${0}. They can eat this week.", payment.paid)
                        : GameText.F("Only managed ${0}. Still ${1} short - they noticed.",
                                     payment.paid, payment.stillOwed));
            }
            if (GuiInput.Button(new Rect(20 + half, y, half, 28),
                               GameText.T(weekly ? "SWITCH TO DAILY" : "SWITCH TO WEEKLY")))
            {
                Say(Sim.Payroll.TrySetCycle(weekly ? PayrollCycle.Daily : PayrollCycle.Weekly,
                                            out string why)
                        ? GameText.T(weekly ? "Paying daily from now on."
                                            : "Weekly it is. Keep the cash - and the risk.")
                        : why);
            }
            y += 32f;
        }

        // 还贷：金额按 DebtPolicy 建议（赚得多就多还），但**要玩家自己按**
        var economy = Economy;
        if (economy != null && economy.Loan != null && economy.Loan.Balance > 0)
        {
            int suggested = DebtPolicy.ScheduledRepaymentFor(
                economy.Loan.Balance, Sim.LastSettlement.netToSafebox,
                Sim.Cash + Sim.Safebox.Balance, cap: 150);

            GUI.Label(new Rect(14, y, w - 28, 20),
                      GameText.F("DEBT ${0}   credit {1}/100 - {2}", economy.Loan.Balance,
                                 Sim.Credit.Score,
                                 GameText.T(CreditPolicy.LabelOf(Sim.Credit.Rating))));
            y += 22f;

            // 规则写在脸上：一行说完"怎么加怎么减、它只管借贷额度"，玩家不用猜
            GUI.Label(new Rect(14, y, w - 28, 20), GameText.T(CreditPolicy.RuleLine));
            y += 22f;

            // **付过了就把按钮收起来**：留着它玩家会再点一次，然后看到
            // "一分钱都挤不出来"——以为出了 bug（试玩截图抓到的正是这个）。
            if (Sim.Credit.PaidToday)
            {
                GUI.Label(new Rect(14, y, w - 28, 20),
                          GameText.T("Bank paid for today. Credit will tick up tomorrow."));
                y += 26f;
            }
            else if (suggested > 0)
            {
                if (GuiInput.Button(new Rect(10, y, w - 20, 28),
                                    GameText.F("PAY THE BANK ${0}", suggested)))
                {
                    int paid = Sim.PayLoanInstallment(suggested);
                    if (paid > 0) economy.RepayLoan(paid);
                    Say(paid > 0
                            ? GameText.F("Paid the bank ${0}. Interest keeps running anyway.", paid)
                            : GameText.T("Not a cent to spare. The bank will remember."));
                }
                y += 32f;
            }
            else
            {
                GUI.Label(new Rect(14, y, w - 28, 20),
                          GameText.T("Nothing due today - but the interest never sleeps."));
                y += 26f;
            }
        }

        return y;
    }

    private EconomySystem _economyCache;

    /// <summary>贷款账本在 v1 的 EconomySystem 上。惰性查找 + 每次校验，
    /// 因为域重载会把引用清成"假 null"（本项目踩过两次的坑）。</summary>
    private EconomySystem Economy
    {
        get
        {
            if (_economyCache == null) _economyCache = FindFirstObjectByType<EconomySystem>();
            return _economyCache;
        }
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
        // 收款常驻顶栏：保险箱有钱随时能收。这是"今天赚到了"的成就感入口，
        // 不该藏在晨报里等第二天（收款的爽点要即时）。
        if (Sim.Safebox.Balance > 0 &&
            GuiInput.Button(new Rect(w - 118, 27, 104, 22),
                            GameText.F("COLLECT ${0}", Sim.Safebox.Balance)))
            Say(GameText.F("Collected ${0}.", Sim.CollectSafebox()));
        GUI.Label(new Rect(14, 46, w - 28, 20),
            // 斜杠分隔被玩家读成"："（"11 可售 / 0 待清" 看着像 11:0）——
            // 改成"数字+标签"成对，中间用间距分组
            // MAT 后面的分数**只在"材料是唯一占用者"时诚实**。以后家具箱进仓库了，
            // 这个分子必须改成"总占格数"，否则就是审计点名的"混单位"歧义。
            GameText.F("{0}*   {1} sellable   {2} to clean   {3} in use   {4} broken   {5} derelict   MAT {6}/{7}",
                       Sim.Reputation.Stars.ToString("0.0"), Sim.Rooms.SellableCount, Sim.Rooms.DirtyBacklog,
                       Sim.Rooms.CountOf(RoomSimState.Occupied), Sim.Rooms.CountOf(RoomSimState.Blocked),
                       Sim.Rooms.CountOf(RoomSimState.Ruined), Sim.Materials.Stock,
                       Sim.Warehouse.HasLimit ? Sim.Warehouse.Capacity.ToString() : "-"));
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

        // 前台那一行：没有它的话"解雇前台"表现为客人凭空不来，玩家查不出原因
        float perHour = ServiceCapacityModel.CheckInsPerHour(Sim.Staff);
        int onDesk = Sim.Staff.ProductiveCountOfRole(StaffRole.Reception);
        if (perHour <= 0f && Sim.DeskQueueLength > 0)
            panel.Add(GameText.F("FRONT DESK UNMANNED - {0} guests waiting, nobody checking them in.",
                                 Sim.DeskQueueLength));
        else if (perHour <= 0f)
            panel.Add(GameText.T("FRONT DESK UNMANNED - hire a receptionist or nobody gets a room."));
        else
            panel.Add(GameText.F("FRONT DESK        {0} on duty   {1}/h   queue {2}",
                                 onDesk, perHour.ToString("0.#"), Sim.DeskQueueLength));
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

    /// <summary>破败房清垃圾区：免费但占客房部工时，要好几趟才清完。
    /// 每间在清的房画一根进度条——玩家要看见"它在动"，否则会以为按钮没生效。</summary>
    private float DrawJunkClearing(float w, float y, int derelictCount)
    {
        var underway = Sim.RoomsBeingCleared();

        if (derelictCount <= 0 && underway.Count == 0)
        {
            GUI.Label(new Rect(14, y, w - 28, 20),
                      GameText.T("Nothing left to dig out. Every room is in the rotation."));
            return y + 22f;
        }

        // 刚清完的房要吱一声：进度条直接消失的话，玩家会以为"按了没反应"
        // （探针踩到过同一个歧义：ProgressOf 对"没开始"和"已完工"都返回 0）
        if (Sim.Rooms.CountOf(RoomSimState.Ruined) < _lastDerelictSeen)
        {
            _clearedFlashUntil = Time.time + 4f;
            _lastClearedCount += _lastDerelictSeen - Sim.Rooms.CountOf(RoomSimState.Ruined);
        }
        _lastDerelictSeen = Sim.Rooms.CountOf(RoomSimState.Ruined);

        if (Time.time < _clearedFlashUntil)
        {
            GUI.Label(new Rect(14, y, w - 28, 20),
                      GameText.F("DUG OUT {0} room(s) - now filthy, housekeeping takes over.",
                                 _lastClearedCount));
            y += 22f;
        }

        // 进行中：房号 + 进度条 + 还差几趟 + 放弃
        for (int i = 0; i < underway.Count && i < 4; i++)
        {
            int roomNumber = underway[i];
            float progress = Sim.Clearing.ProgressOf(roomNumber);
            int visitsLeft = JunkClearingModel.VisitsRemaining(Sim.Clearing.WorkDoneOn(roomNumber));

            GUI.Label(new Rect(14, y, 96, 20), GameText.F("R{0}", roomNumber));
            DrawProgressBar(new Rect(66, y + 3, w - 158, 14), progress);
            GUI.Label(new Rect(w - 88, y, 46, 20), progress.ToString("P0"));
            if (GuiInput.Button(new Rect(w - 44, y, 34, 20), GameText.T("X")))
                Say(Sim.CancelJunkClearing(roomNumber)
                    ? GameText.F("R{0} left half-dug. That work is wasted.", roomNumber)
                    : "Nothing to cancel.");
            y += 22f;

            GUI.Label(new Rect(24, y, w - 38, 18),
                      GameText.F("{0} more trips to go", visitsLeft)); y += 20f;
        }

        // 待指派：一键派下一间
        var waiting = Sim.RoomsNeedingClearing(1);
        if (waiting.Count > 0)
        {
            if (GuiInput.Button(new Rect(10, y, w - 20, 24),
                                GameText.F("CLEAR OUT R{0} (free - costs housekeeping time)", waiting[0])))
                Say(Sim.TryStartJunkClearing(waiting[0], out string reason)
                    ? GameText.F("R{0}: cobwebs and rubbish. Takes about {1} trips.",
                                 waiting[0], JunkClearingModel.VisitsForOneRoom)
                    : reason);
            y += 28f;
        }
        else if (underway.Count > 0)
        {
            GUI.Label(new Rect(14, y, w - 28, 20),
                      GameText.T("Housekeeping digs these out when no room needs turning over."));
            y += 22f;
        }

        return y;
    }

    private int _lastDerelictSeen = -1;
    private float _clearedFlashUntil;
    private int _lastClearedCount;

    /// <summary>一根最朴素的进度条（白盒够用；3D 阶段会移到房间上方）。</summary>
    private void DrawProgressBar(Rect rect, float fill01)
    {
        GUI.Box(rect, GUIContent.none);
        float inner = (rect.width - 4f) * Mathf.Clamp01(fill01);
        if (inner > 1f)
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, inner, rect.height - 4f), ProgressFill);
    }

    private static Texture2D _progressFill;
    private static Texture2D ProgressFill
    {
        get
        {
            if (_progressFill == null)
            {
                _progressFill = new Texture2D(1, 1);
                _progressFill.SetPixel(0, 0, new Color(0.45f, 0.8f, 0.45f, 1f));
                _progressFill.Apply();
            }
            return _progressFill;
        }
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

        // ── 夜班：22:00-08:00 的幕后班（用户要求）──────────────────────────────
        // 夜班接住"打烊时还在排队的客人"，代价是 +50% 工资。它必须和加成、
        // 和夜间产能并排显示，否则玩家没法算这笔账。
        int nightDesk = Sim.Shifts.NightCountOnDuty(Sim.Staff, StaffRole.Reception);
        int nightWanted = Sim.Shifts.NightCountOf(StaffRole.Reception);
        if (GuiInput.Button(new Rect(10, y, w - 20, 24),
                            GameText.F("NIGHT DESK  {0} on nights (+50% pay)  -  tap to change", nightDesk)))
        {
            // 在 0..前台总人数 之间循环（留一个人给白班，所以上限是总数-1）
            int receptionTotal = 0;
            foreach (var e in Sim.Staff.Entries)
                if (e.member != null && e.member.Role == StaffRole.Reception) receptionTotal++;
            int max = receptionTotal - 1 < 0 ? 0 : receptionTotal - 1;
            Sim.Shifts.SetNightCount(StaffRole.Reception, nightWanted >= max ? 0 : nightWanted + 1);
            Sim.Shifts.ApplyTo(Sim.Staff);
            Say(GameText.F("Night desk set to {0}. Takes effect tomorrow morning.",
                           Sim.Shifts.NightCountOf(StaffRole.Reception)));
        }
        y += 26f;
        GUI.Label(new Rect(14, y, w - 28, 20),
            nightDesk > 0
                ? GameText.F("Night desk can take {0} late arrivals; the rest find a locked door.",
                             NightDeskModel.CapacityFor(nightDesk))
                : GameText.T("Nobody on nights: guests still queuing at 22:00 are lost and angry."));
        y += 24f;

        bool weekend = PricingPolicy.IsWeekend(Sim.Clock.CurrentDay);
        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("On duty {0}/{1} by day, {2} on nights   wages today ${3}{4}",
                       Sim.Staff.OnDutyCount, Sim.Staff.Count, Sim.Shifts.NightStaffOnDuty,
                       Sim.Shifts.DailyWageCost(Sim.Staff, Sim.Clock.CurrentDay),
                       weekend ? GameText.T("  (WEEKEND +30%)") : "")); y += 22f;
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
        {
            // 三种失败要说三句不同的话：装不下 / 买不起 / 成功。
            // 一句"买不起"糊过去的话，玩家会对着满仓库反复点按钮找钱（审计点名的"沉默失败"）
            int fits = Sim.MaterialsThatFit(10);
            if (fits < 10)
                Say(GameText.F("The warehouse only has room for {0} more. Space: {1}/{2}.",
                               fits, Sim.Materials.Stock, Sim.Warehouse.Capacity));
            else
                Say(Sim.TryBuyMaterials(10) ? "Materials delivered."
                                            : "Can't afford materials right now.");
        }
        if (GuiInput.Button(new Rect(20 + half, y, half, 24), GameText.T("START THE WORK")))
        {
            var rooms = Sim.RenovatableRooms(_plan, _batch);
            Say(Sim.TryStartRenovation(_plan, rooms, out string reason)
                ? GameText.F("{0} room(s) shut for {1} days. Better be worth it.",
                             rooms.Count, RenovationPricing.BlockDaysFor(plan, rooms.Count))
                : reason);
        }
        y += 30f;

        // ── 破败房清垃圾：不要钱，要人工（玩家设计的那条"一次次进屋"的路）──────
        int derelict = Sim.Rooms.CountOf(RoomSimState.Ruined);
        GUI.Label(new Rect(14, y, w - 28, 20),
            GameText.F("DERELICT ROOMS - {0} left to open", derelict)); y += 22f;

        y = DrawJunkClearing(w, y, derelict);

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

        // **每一件坏家具都要看得见**：原来硬截前 2 条且没有"还有 N 件"的提示，
        // 而塌掉的家具是修不动的——它要是被截掉，玩家会有一间永远封着、
        // 既看不见也修不了的房（设计审计点名的问题）。塌掉的排最前。
        broken.Sort((a, bb) => (bb.wrecked ? 1 : 0).CompareTo(a.wrecked ? 1 : 0));
        int shownBroken = broken.Count < 4 ? broken.Count : 4;
        for (int b = 0; b < shownBroken; b++)
        {
            var item = broken[b];
            FurnitureKind kind = FurnitureCatalog.Get(item.kindId);

            // 房况说人话（"快塌了"而不是 0.28）——整套塌陷机制都建立在健康度上，
            // 不显示它的话所有后果都是"莫名其妙发生的事"
            GUI.Label(new Rect(14, y, w - 150, 20),
                      "R" + item.roomNumber + " [" +
                      GameText.T(FurnitureLedger.ConditionWord(item.health, item.wrecked)) + "] " +
                      GameText.T(FurnitureLedger.FaultLineOf(item)));

            if (!item.IsUnderRepair)
            {
                // 塌掉的修不动：不画 FIX，免得玩家反复点一个永远失败的按钮
                if (!item.wrecked &&
                    GuiInput.Button(new Rect(w - 132, y - 2, 62, 22), GameText.F("FIX ${0}", kind.repairCost)))
                    Say(Sim.TryRepairFurniture(item.instanceId, out string reason)
                        ? GameText.F("{0} in room {1}: {2} day(s) of work. It will only come back as good as it is old.",
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
        if (broken.Count > shownBroken)
        {
            GUI.Label(new Rect(14, y, w - 28, 20),
                      GameText.F("...and {0} more broken pieces.", broken.Count - shownBroken));
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
