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
        if (WorldManagementHud.IsActive) return;

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
        if (WorldManagementHud.IsActive) return;
        if (Sim == null) return;

        var bridge = HotelSimSceneBridge.Instance;
        if (WorldManagementHud.IsActive
            && (bridge == null || !bridge.AwaitingMorningReport))
            return;

        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        // 日结 → **全屏晨报**：时间已被桥暂停，看完点"开门营业"才进新的一天。
        // 全屏 = 整个屏幕都是热区，世界这时收不到任何点击。
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

        // 存档入口放在这一行的**右端**。第一版塞在倍速行里，直接压住了
        // "跳到下一时段"（玩家截图抓到）——IMGUI 没有 z 序，重叠就是误触。
        // 这一行左边是折叠按钮、中间是提示条（占到 w-150），右端本来空着。
        if (GuiInput.Button(new Rect(w - 86f, sheetTop - 26f, 76f, 24f), GameText.T("SAVES")))
            SaveSlotPanel.Toggle();

        // 提示条右边界要给存档按钮留出位置，否则又是一处重叠
        if (Time.time < _toastUntil)
            GUI.Box(new Rect(140, sheetTop - 26f, w - 236f, 24f), _toast);

        if (_collapsed) return;

        GUI.Box(new Rect(6, sheetTop, w - 12, h - sheetTop - 6f), "");
        float y = sheetTop + 8f;
        y = DrawSelectedRoomCard(w, y);
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
        report.AddIf(Sim.BookingsDeclinedToday > 0, DeclinedBookingsLine());
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

    /// <summary>抽屉顶部：**你正在看哪一间房**。纯信息，一个按钮都没有。
    ///
    /// 第一版把操作按钮也塞在这里，结果和"装修"页的按钮重复——玩家截图里
    /// 「清理 301 房」出现两次、「换全套」出现两次、「开工」和「开工施工」
    /// 是两个不同的按钮，他的原话是"我看完了完全不知道怎么操作"。
    /// 现在定死一条规矩：**一个操作只在一个地方**。这张卡负责回答"这是哪一间、
    /// 它什么状况"，操作全部在施工页。</summary>
    private float DrawSelectedRoomCard(float w, float y)
    {
        int number = RoomSelection.Selected;
        if (number <= 0 || !Sim.Rooms.Contains(number))
        {
            GUI.Label(new Rect(14, y, w - 28, 20), GameText.T("Tap a room in the hotel to work on it."));
            return y + 22f;
        }

        RoomSimState state = Sim.Rooms.At(number).state;
        GUI.Box(new Rect(6, y, w - 12, 24),
                GameText.F("ROOM {0} - {1}   [{2}]", number,
                           GameText.T(RoomStatePalette.WordOf(state)),
                           GameText.T(FurnitureLedger.ConditionWord(
                               Sim.Furniture.WorstHealthIn(number), false))));
        y += 26f;

        // 正在进行中的事只报进度，不给按钮（按钮在施工页）
        if (Sim.Clearing.IsClearing(number))
        {
            DrawProgressBar(new Rect(14, y + 3, w - 130, 14), Sim.Clearing.ProgressOf(number));
            GUI.Label(new Rect(w - 112, y, 104, 20),
                      GameText.F("{0} more trips to go",
                                 JunkClearingModel.VisitsRemaining(Sim.Clearing.WorkDoneOn(number))));
            y += 22f;
        }
        else if (Sim.Renovations.IsRenovating(number))
        {
            GUI.Label(new Rect(14, y, w - 28, 20), GameText.T("Building work in progress."));
            y += 22f;
        }
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

    /// <summary>拒单那一行的人话：**点明是哪几晚满了**。
    ///
    /// 玩家实测困惑："UI 看是可售有几个，但是结算的时候说没房可卖。"
    /// 顶栏的"可售"是今天的空房，拒单说的是未来某一晚的间夜卖光了——
    /// 不点明晚号，这两个数字看起来就是自相矛盾。</summary>
    private string DeclinedBookingsLine()
    {
        var full = Sim.FullyBookedNights();
        if (full.Count == 0)
            return GameText.F("TURNED DOWN {0} BOOKINGS - they wanted bands you do not offer.",
                              Sim.BookingsDeclinedToday);

        if (full.Count == 1)
            return GameText.F("TURNED DOWN {0} BOOKINGS - night {1} is already full.",
                              Sim.BookingsDeclinedToday, full[0]);

        return GameText.F("TURNED DOWN {0} BOOKINGS - nights {1}-{2} are already full ({3} nights).",
                          Sim.BookingsDeclinedToday, full[0], full[full.Count - 1], full.Count);
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
            //
            // 打头的 "TONIGHT 6/10" 是玩家点名要的那个数（"卖了6间，要显示成6/10"）。
            // 它取代了原来的"在住 {n}"：在住只数此刻楼里的人，而今晚已售连
            // 路上那几位一起算，回答的是同一个问题但更准。
            //
            // MAT 后面的分数**只在"材料是唯一占用者"时诚实**。以后家具箱进仓库了，
            // 这个分子必须改成"总占格数"，否则就是审计点名的"混单位"歧义。
            // "taken" 而不是 "sold"、"clean now" 而不是 "ready to sell"：
            // 实测截图里「今晚 4/8」紧挨着「可卖 5」，玩家会去加——4+5=9 比总数还大。
            // 两个数其实是**重叠**的（一间干净房正等着今晚那位客人），
            // 所以第二个字段必须描述状态而不是动作，否则又是一处看着矛盾的数字。
            GameText.F("{0}*   TONIGHT {1} taken   {2} clean now   {3} to clean   {4} broken   {5} derelict   MAT {6}/{7}",
                       Sim.Reputation.Stars.ToString("0.0"),
                       Sim.OccupancyForNight(clock.CurrentDay).Fraction,
                       Sim.Rooms.SellableCount, Sim.Rooms.DirtyBacklog,
                       Sim.Rooms.CountOf(RoomSimState.Blocked),
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
        // 今晚一句人话，紧挨着日历——顶栏那个 4/8 太小，这里说清楚它是什么。
        // **不能写成"有人住"**：早上 8 点没人到店，可那几间已经订出去了。
        // 实测截图里这一行说"4 间有人住"而上面一行写着"已入住 0 人"——
        // 数字是对的，话是假的，玩家先看到的是后者
        var tonight = Sim.OccupancyForNight(Sim.Clock.CurrentDay);
        panel.Add(GameText.F("TONIGHT             {0}   ({1} taken for tonight, {2} still to sell)",
                             tonight.Fraction, tonight.sold, tonight.Left));

        // 满房的晚号直接列出来：顶栏的"今天可售"和"未来某晚订满"是两件事，
        // 玩家实测把它们当成了矛盾（"UI 看是可售有几个，结算说没房可卖"）
        var full = Sim.FullyBookedNights();
        panel.AddIf(full.Count > 0,
                    GameText.F("SOLD OUT on night(s) {0} - that is where the refusals come from.",
                               string.Join(", ", full)));

        y = panel.Draw(10, y, w - 20, GameText.T("TODAY"));
        DrawOccupancyCalendar(w, y + 4f);
    }

    /// <summary>入住日历：一格一晚，写着"已售/总数"（玩家要求："未来给我搞成一个
    /// 日历UI，每天都是已售/全部这样显示"）。
    ///
    /// 刻意做成格子而不是一行数字。上一版是 `SoldNightsPreview()` 吐出的
    /// "3/4/2/6/1/0/5"——玩家把斜杠读成了冒号，那一行根本没法读。
    /// 一格一晚、日号在上分数在下，斜杠才只有一个意思。
    ///
    /// 第一格是今晚，分子含上门客；后面几格是预订间夜。两者口径不同，
    /// 所以第一格的标题写 TONIGHT 而不是日号——两种口径混在一张表里
    /// 必须至少让人看出来它们不一样。</summary>
    private void DrawOccupancyCalendar(float w, float y)
    {
        const float gap = 3f;
        const float minCell = 44f;
        const float cellH = 46f;

        float available = w - 20f;
        // 窄屏就少画几晚，而不是把格子压成读不出数字的细条
        int fits = (int)((available + gap) / (minCell + gap));
        if (fits < 2) return;

        var nights = Sim.OccupancyCalendar(fits < OccupancyBoard.DefaultDays
                                               ? fits : OccupancyBoard.DefaultDays);
        if (nights.Count == 0) return;

        float cellW = (available - gap * (nights.Count - 1)) / nights.Count;

        GUI.Label(new Rect(10, y, available, 18),
                  GameText.F("THE WEEK AHEAD   sold / rooms   ({0}% booked, {1} night(s) full)",
                             OccupancyBoard.PercentBookedAcross(nights),
                             OccupancyBoard.FullNightsAcross(nights)));
        y += 20f;

        for (int i = 0; i < nights.Count; i++)
        {
            NightOccupancy night = nights[i];
            var cell = new Rect(10 + i * (cellW + gap), y, cellW, cellH);
            GUI.Box(cell, GUIContent.none);

            // 底色按入住率从下往上涨，满房转红——一眼扫出哪几晚该涨价
            float fill = (cellH - 6f) * Mathf.Clamp01(night.Rate);
            if (fill > 1f)
                GUI.DrawTexture(new Rect(cell.x + 3f, cell.yMax - 3f - fill, cell.width - 6f, fill),
                                night.soldOut ? FullNightFill : BusyNightFill);

            // 日号那一行：今晚特别标出来（它的分子口径和后面几格不同），
            // 周末标 W——需求本来就更高，玩家看得见才好提前涨价
            string head = night.isTonight
                ? GameText.T("TONIGHT")
                : (PricingPolicy.IsWeekend(night.day)
                       ? GameText.F("D{0} W", night.day)
                       : GameText.F("D{0}", night.day));
            GUI.Label(new Rect(cell.x, cell.y + 2f, cell.width, 16), head, CalendarHeadStyle());
            GUI.Label(new Rect(cell.x, cell.y + 18f, cell.width, 20), night.Fraction, CalendarCellStyle());

            // 超售不能只靠底色红：那是"已经卖多了"，和"刚好卖完"是两件事
            if (night.Oversold > 0)
                GUI.Label(new Rect(cell.x, cell.y + 30f, cell.width, 16),
                          GameText.F("+{0}!", night.Oversold), CalendarHeadStyle());
        }
    }

    private GUIStyle _calendarHead;
    private GUIStyle _calendarCell;

    private GUIStyle CalendarHeadStyle() =>
        _calendarHead ?? (_calendarHead = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9
        });

    private GUIStyle CalendarCellStyle() =>
        _calendarCell ?? (_calendarCell = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13
        });

    // DrawJunkClearing / 清理完的闪示都退役了：它们的内容已经并入施工页
    // （DrawDerelictActions）。留着就是第二个入口，而"一个操作只在一个地方"
    // 正是这次重排的头号规矩——玩家上一版看到「清理 301 房」出现两次。

    /// <summary>一根最朴素的进度条（白盒够用；3D 阶段会移到房间上方）。</summary>
    private void DrawProgressBar(Rect rect, float fill01)
    {
        GUI.Box(rect, GUIContent.none);
        float inner = (rect.width - 4f) * Mathf.Clamp01(fill01);
        if (inner > 1f)
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, inner, rect.height - 4f), ProgressFill);
    }

    private static Texture2D _progressFill;
    private static Texture2D ProgressFill =>
        _progressFill ?? (_progressFill = SolidTexture(new Color(0.45f, 0.8f, 0.45f, 1f)));

    private static Texture2D _busyNightFill;
    /// <summary>日历格的底色：还能卖（偏青，与进度条的绿区分开）。</summary>
    private static Texture2D BusyNightFill =>
        _busyNightFill ?? (_busyNightFill = SolidTexture(new Color(0.35f, 0.62f, 0.78f, 0.85f)));

    private static Texture2D _fullNightFill;
    /// <summary>日历格的底色：这一晚一间都不剩了。</summary>
    private static Texture2D FullNightFill =>
        _fullNightFill ?? (_fullNightFill = SolidTexture(new Color(0.82f, 0.42f, 0.34f, 0.9f)));

    private static Texture2D SolidTexture(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
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
    /// <summary>施工页：**只显示选中那间房此刻真正能做的事**。
    ///
    /// 上一版这一页把三件互不相关的事堆在一起（批量装修 / 破败房复原 / 买材料），
    /// 再叠上房卡的重复按钮，玩家的评价是"实在太混乱了，我看完了完全不知道怎么操作"。
    /// 重排的三条规矩：
    ///   ① **一个操作只在一个地方**——房卡只报状况，动手全在这里；
    ///   ② **只列这一刻合法的操作**——破败房不显示装修档位，好房不显示清理；
    ///   ③ **价格写在按钮上**（"START - $900, 2 materials"），而不是写在另一行
    ///      让玩家自己对应。能点的和只是说明的必须一眼分得开。
    /// 档位不再一次列三个全宽按钮（六个一样的灰条），改成一行"点它换下一档"。</summary>
    private void DrawBuild(float w, float y)
    {
        int number = RoomSelection.Selected;
        if (number <= 0 || !Sim.Rooms.Contains(number))
        {
            GUI.Label(new Rect(14, y, w - 28, 20),
                      GameText.T("Tap a room in the hotel, then come back here to work on it."));
            y += 24f;
            DrawMaterialsRow(w, y);
            return;
        }

        RoomSimState state = Sim.Rooms.At(number).state;

        // 已经在施工的房：不给任何新按钮，免得玩家重复下单
        if (Sim.Renovations.IsRenovating(number))
        {
            GUI.Label(new Rect(14, y, w - 28, 20),
                      GameText.F("Room {0} is a building site. Nothing to decide until it is done.", number));
            y += 24f;
            DrawMaterialsRow(w, y);
            return;
        }

        switch (state)
        {
            case RoomSimState.Ruined: y = DrawDerelictActions(w, y, number); break;
            case RoomSimState.Blocked: y = DrawBrokenActions(w, y, number); break;
            default: y = DrawRenovateActions(w, y, number); break;
        }

        DrawMaterialsRow(w, y);
    }

    /// <summary>破败房：两条路——只花人工的清理，或者花钱买速度的复原。</summary>
    private float DrawDerelictActions(float w, float y, int number)
    {
        GUI.Box(new Rect(6, y, w - 12, 22), GameText.T("DERELICT - two ways in")); y += 26f;

        if (Sim.Clearing.IsClearing(number))
        {
            GUI.Label(new Rect(14, y, w - 28, 20),
                      GameText.F("Housekeeping is digging it out - {0} more trips to go.",
                                 JunkClearingModel.VisitsRemaining(Sim.Clearing.WorkDoneOn(number))));
            y += 22f;
            if (GuiInput.Button(new Rect(10, y, w - 20, 24),
                                GameText.T("STOP CLEARING - the trips already walked are wasted")))
                Say(Sim.CancelJunkClearing(number)
                        ? GameText.F("R{0} left half-dug. That work is wasted.", number)
                        : "Nothing to cancel.");
            y += 28f;
        }
        else
        {
            if (GuiInput.Button(new Rect(10, y, w - 20, 26),
                                GameText.F("CLEAR IT OUT - free, {0} housekeeping trips",
                                           JunkClearingModel.VisitsForOneRoom)))
                Say(Sim.TryStartJunkClearing(number, out string reason)
                        ? GameText.F("R{0}: cobwebs and rubbish. Takes about {1} trips.",
                                     number, JunkClearingModel.VisitsForOneRoom)
                        : reason);
            y += 30f;
        }

        // 花钱那条路：一行换档，一行开工（价格写在开工按钮上）
        var rp = ReclaimPlan.For(_reclaimPlan);
        if (GuiInput.Button(new Rect(10, y, w - 20, 22),
                            GameText.T(ReclaimPlan.LabelOf(_reclaimPlan)) + "   >"))
            _reclaimPlan = _reclaimPlan == ReclaimPlanKind.FullFit
                ? ReclaimPlanKind.PatchUp : (ReclaimPlanKind)((int)_reclaimPlan + 1);
        y += 24f;

        var one = new System.Collections.Generic.List<int> { number };
        if (GuiInput.Button(new Rect(10, y, w - 20, 26),
                            GameText.F("START - ${0}, {1} materials, shut {2} days",
                                       ReclaimPricing.CashCostFor(rp, 1),
                                       ReclaimPricing.MaterialCostFor(rp, 1),
                                       ReclaimPricing.BlockDaysFor(rp, 1))))
            Say(Sim.TryStartReclaim(_reclaimPlan, one, out string why)
                    ? GameText.F("R{0} is a building site now.", number) : why);
        return y + 30f;
    }

    /// <summary>家具坏了：修（花钱花时间）或糊胶带（免费，明天照坏）。</summary>
    private float DrawBrokenActions(float w, float y, int number)
    {
        GUI.Box(new Rect(6, y, w - 12, 22), GameText.T("BROKEN FURNITURE")); y += 26f;

        FurnitureInstance broken = null;
        foreach (var item in Sim.Furniture.InRoom(number))
            if (item.IsFaulted) { broken = item; break; }

        if (broken == null)
        {
            GUI.Label(new Rect(14, y, w - 28, 20), GameText.T("Nothing broken in here after all."));
            return y + 22f;
        }

        FurnitureKind kind = FurnitureCatalog.Get(broken.kindId);
        GUI.Label(new Rect(14, y, w - 28, 20), GameText.T(FurnitureLedger.FaultLineOf(broken))); y += 22f;

        if (broken.IsUnderRepair)
        {
            GUI.Label(new Rect(14, y, w - 28, 20),
                      GameText.F("A handyman is on it - {0} day(s) left.", broken.repairDaysRemaining));
            return y + 22f;
        }

        if (!broken.wrecked &&
            GuiInput.Button(new Rect(10, y, w - 20, 26),
                            GameText.F("REPAIR - ${0}, {1} day(s)", kind.repairCost, kind.repairDays)))
            Say(Sim.TryRepairFurniture(broken.instanceId, out string reason)
                    ? GameText.F("{0} in room {1}: {2} day(s) of work. It will only come back as good as it is old.",
                                 GameText.T(kind.name), number, kind.repairDays)
                    : reason);
        y += 30f;

        if (!broken.taped &&
            GuiInput.Button(new Rect(10, y, w - 20, 24),
                            GameText.T("DUCT TAPE - free, sellable, broken again tomorrow")))
            Say(Sim.TryTapeFurniture(broken.instanceId, out string reason)
                    ? GameText.F("Taped up room {0}. Sellable, ugly, and broken again tomorrow.", number)
                    : reason);
        return y + 28f;
    }

    /// <summary>好房：升级装修。批量只在这里出现（批量折扣是它独有的东西）。</summary>
    private float DrawRenovateActions(float w, float y, int number)
    {
        GUI.Box(new Rect(6, y, w - 12, 22), GameText.T("RENOVATE")); y += 26f;

        if (GuiInput.Button(new Rect(10, y, w - 20, 22),
                            GameText.T(RenovationPlan.LabelOf(_plan)) + "   >"))
            _plan = _plan == RenovationPlanKind.Luxury
                ? RenovationPlanKind.Economy : (RenovationPlanKind)((int)_plan + 1);
        y += 24f;

        // 批量步进器紧挨着报价：多做几间更便宜但工期更长，差别要当场看得见
        if (GuiInput.Button(new Rect(10, y, 54, 22), "-") && _batch > 1) _batch--;
        if (GuiInput.Button(new Rect(68, y, 54, 22), "+")) _batch++;
        GUI.Label(new Rect(128, y, w - 140, 22),
                  GameText.F("{0} room(s) at once - bulk is cheaper and slower", _batch));
        y += 26f;

        var plan = RenovationPlan.For(_plan);
        if (GuiInput.Button(new Rect(10, y, w - 20, 26),
                            GameText.F("START - ${0}, {1} materials, shut {2} days",
                                       Sim.QuoteRenovation(_plan, _batch),
                                       RenovationPricing.MaterialCostFor(plan, _batch),
                                       RenovationPricing.BlockDaysFor(plan, _batch))))
        {
            // 选中的那间**一定在名单里**，其余按批量补齐——否则玩家点了半天
            // 结果动的是别的房间（"我明明选的是 204"）
            var rooms = new System.Collections.Generic.List<int> { number };
            foreach (int candidate in Sim.RenovatableRooms(_plan, _batch))
                if (candidate != number && rooms.Count < _batch) rooms.Add(candidate);

            Say(Sim.TryStartRenovation(_plan, rooms, out string reason)
                    ? GameText.F("{0} room(s) shut for {1} days. Better be worth it.",
                                 rooms.Count, RenovationPricing.BlockDaysFor(plan, rooms.Count))
                    : reason);
        }
        return y + 30f;
    }

    /// <summary>材料与仓库：施工吃它，所以放在施工页底部而不是自己占一页。</summary>
    private float DrawMaterialsRow(float w, float y)
    {
        GUI.Label(new Rect(14, y, w - 28, 20),
                  GameText.F("Materials {0}/{1} in the warehouse",
                             Sim.Materials.Stock,
                             Sim.Warehouse.HasLimit ? Sim.Warehouse.Capacity.ToString() : "-"));
        y += 22f;

        if (GuiInput.Button(new Rect(10, y, w - 20, 24),
                            GameText.F("BUY 10 MATERIALS - ${0}", Sim.Materials.PriceFor(10))))
        {
            // 三种失败三句话：装不下 / 买不起 / 成功。一句"买不起"糊过去的话，
            // 玩家会对着满仓库反复点按钮找钱（审计点名的"沉默失败"）
            int fits = Sim.MaterialsThatFit(10);
            if (fits < 10)
                Say(GameText.F("The warehouse only has room for {0} more. Space: {1}/{2}.",
                               fits, Sim.Materials.Stock, Sim.Warehouse.Capacity));
            else
                Say(Sim.TryBuyMaterials(10) ? "Materials delivered."
                                            : "Can't afford materials right now.");
        }
        return y + 28f;
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

    // SoldNightsPreview() 退役了：它吐的是 "3/4/2/6/1/0/5" 一行数字，
    // 玩家把斜杠读成了冒号。同一份信息现在由 DrawOccupancyCalendar 一格一晚地画。
}
