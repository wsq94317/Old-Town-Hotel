using System;
using System.Collections.Generic;

// 模拟内核聚合根（架构 A.1 的"唯一状态权威"）。纯 C#，不引用 UnityEngine。
//
// 一天的完整循环（M-B 打通，无预订簿——当日需求模型）：
//   BeginDay   排班落表 → 退房潮（结算收入/满意度/房变脏）→ 掷今日到店人数
//   StepMinute 管线（员工/清洁）+ 到店客按阶段权重滴入 → 占用 Ready 房
//   SettleDay  固定成本前置结算 → 净利入保险箱/溢出 → 磨损 → 员工日结
//
// 压力网在此闭环：排班精简 → 清洁慢 → 可售房少 → 到店的人住不进去 → 收入低
//                → 现金紧 → 被迫清仓价 → 客群恶化 → 事件/清洁负担升。
public sealed class HotelSim
{
    /// <summary>一次住宿（M-D 会被 BookingBook 的 Reservation 取代）。</summary>
    private struct Stay
    {
        public int nightlyRate;
        public GuestSegment segment;
        public float priceRatio;
        public int waitMinutes;   // 前台排队时长（商务客最恨这个）
        public bool flawedRoom;   // 没验房就上架的房：有概率带瑕疵
    }

    private readonly Random _rng;
    private readonly Dictionary<int, Stay> _stays = new Dictionary<int, Stay>();
    private double _arrivalCredit;
    private int _deskQueue;          // 排在前台等办入住的人数
    private double _deskCredit;      // 前台处理进度
    private int _grossIncomeToday;
    private int _commissionToday;
    private int _checkoutsToday;

    public SimClock Clock { get; }
    public RoomLedger Rooms { get; }
    public StaffRoster Staff { get; }
    public ShiftPlan Shifts { get; }
    public PricingPolicy Pricing { get; }
    public Safebox Safebox { get; }
    public OverflowLedger Overflow { get; }
    public ReputationLedger Reputation { get; }
    public SimPipeline Pipeline { get; }
    public DemandConfig DemandCfg { get; }
    public SimRenovationQueue Renovations { get; }
    public MaterialStore Materials { get; }

    /// <summary>今日前台排队最长时长（分钟）。注意：前台无人时会顶到哨兵值，
    /// 做强弱对比请用 TotalCheckInWaitToday（客人实际承受的等待总量）。</summary>
    public int PeakCheckInWaitToday { get; private set; }

    /// <summary>今日所有入住客承受的等待分钟总和——满意度实际吃的就是它。</summary>
    public int TotalCheckInWaitToday { get; private set; }

    public int FlawedStaysToday { get; private set; }

    /// <summary>可支配现金：只能靠收保险箱补充，主动支出（装修/还款/招聘）只花它。</summary>
    public int Cash { get; private set; }

    /// <summary>渠道佣金率（M-B 单渠道 A；M-G 换 ChannelPortfolio）。</summary>
    public float CommissionRate { get; set; } = 0.18f;

    /// <summary>今日排定的到店人数（晨报展示）。</summary>
    public int ArrivalsPlannedToday { get; private set; }
    public int ArrivalsCheckedInToday { get; private set; }
    public int ArrivalsTurnedAwayToday { get; private set; }
    public int CheckoutsToday => _checkoutsToday;
    public int GrossIncomeToday => _grossIncomeToday;

    /// <summary>最近一次日结结果（晨报读它）。</summary>
    public DaySettlementResult LastSettlement { get; private set; }

    public HotelSim(RoomLedger rooms, StaffRoster staff, RoomRateTable rates,
                    DemandConfig demandConfig, int startingCash, int rngSeed,
                    int reputationWindow = 20)
    {
        Clock = new SimClock();
        Rooms = rooms;
        Staff = staff;
        Shifts = new ShiftPlan();
        Pricing = new PricingPolicy(rates);
        Safebox = new Safebox();
        Overflow = new OverflowLedger();
        Reputation = new ReputationLedger(reputationWindow);
        DemandCfg = demandConfig;
        Cash = startingCash;
        _rng = new Random(rngSeed);
        Pipeline = new SimPipeline(Clock, rooms, staff, rngSeed);
        Renovations = new SimRenovationQueue();
        Materials = new MaterialStore();
    }

    // ── 装修（核心长线玩法） ──────────────────────────────────────────────────

    /// <summary>某批房按某方案装修的报价（UI 展示批量折扣）。</summary>
    public int QuoteRenovation(RenovationPlanKind kind, int roomCount) =>
        RenovationPricing.CashCostFor(RenovationPlan.For(kind), roomCount);

    /// <summary>下装修单。房间立刻 Block（当期不可售——这才是装修的真代价）。
    /// 现金或材料不够则整单失败，不做部分成交。</summary>
    public bool TryStartRenovation(RenovationPlanKind kind, IList<int> roomNumbers, out string reason)
    {
        reason = "";
        if (roomNumbers == null || roomNumbers.Count == 0) { reason = "Pick some rooms first."; return false; }

        var plan = RenovationPlan.For(kind);
        var eligible = new List<int>();
        foreach (int number in roomNumbers)
        {
            if (!Rooms.Contains(number)) continue;
            RoomRecord room = Rooms.At(number);
            if (room.state == RoomSimState.Ruined) continue;      // 破败房要先解锁
            if (room.state == RoomSimState.Occupied) continue;    // 有人住着不能开工
            if (Renovations.IsRenovating(number)) continue;
            if ((int)room.tier >= (int)plan.targetTier) continue; // 已经达标
            eligible.Add(number);
        }
        if (eligible.Count == 0) { reason = "None of those rooms can take this plan right now."; return false; }

        int cash = RenovationPricing.CashCostFor(plan, eligible.Count);
        int materials = RenovationPricing.MaterialCostFor(plan, eligible.Count);

        if (Materials.Stock < materials)
        {
            reason = "Not enough materials (" + Materials.Stock + "/" + materials + "). Buy some first.";
            return false;
        }
        if (Cash < cash)
        {
            reason = "That costs $" + cash + " and you have $" + Cash + ".";
            return false;
        }

        Cash -= cash;
        Materials.TryConsume(materials);
        Renovations.Enqueue(plan, eligible);
        foreach (int number in eligible)
        {
            Rooms.SetState(number, RoomSimState.Blocked);
            Rooms.AddFlags(number, RoomFlags.Renovating);
        }
        return true;
    }

    /// <summary>解锁一间破败房的花费（清垃圾、通水电——还没算装修）。</summary>
    public const int RuinedRoomUnlockCost = 800;

    /// <summary>花钱把一间破败房拉回营业序列（变成脏房，还得打扫）。</summary>
    public bool TryUnlockRuinedRoom(int roomNumber, out string reason)
    {
        reason = "";
        if (!Rooms.Contains(roomNumber)) { reason = "No such room."; return false; }
        if (Rooms.At(roomNumber).state != RoomSimState.Ruined) { reason = "That one's already open."; return false; }
        if (Cash < RuinedRoomUnlockCost)
        {
            reason = "Clearing a derelict room costs $" + RuinedRoomUnlockCost + ".";
            return false;
        }
        Cash -= RuinedRoomUnlockCost;
        Rooms.TryUnlockRuinedRoom(roomNumber);
        return true;
    }

    /// <summary>找一间还没解锁的破败房（UI 的"解锁下一间"按钮用）。</summary>
    public bool TryFindRuinedRoom(out int roomNumber) =>
        Rooms.TryFindFirstInState(RoomSimState.Ruined, out roomNumber);

    /// <summary>可以装修的房号（未占用、未在施工、档位低于目标）。</summary>
    public List<int> RenovatableRooms(RenovationPlanKind kind, int max)
    {
        var result = new List<int>();
        var plan = RenovationPlan.For(kind);
        for (int i = 0; i < Rooms.Count && result.Count < max; i++)
        {
            RoomRecord room = Rooms.Peek(i);
            if (room.state == RoomSimState.Ruined || room.state == RoomSimState.Occupied) continue;
            if ((int)room.tier >= (int)plan.targetTier) continue;
            if (Renovations.IsRenovating(room.number)) continue;
            result.Add(room.number);
        }
        return result;
    }

    /// <summary>买材料。</summary>
    public bool TryBuyMaterials(int units)
    {
        if (units <= 0) return true;
        int price = Materials.PriceFor(units);
        if (!TrySpendCash(price)) return false;
        Materials.Add(units);
        return true;
    }

    // ── 一天开始 ─────────────────────────────────────────────────────────────

    /// <summary>开门：排班落表 → 退房潮 → 掷今日客量。</summary>
    public void BeginDay()
    {
        Shifts.ApplyTo(Staff);

        // 先清零再跑退房潮——顺序反了会把刚结算的房费当场抹掉（踩过）。
        // 语义：晨间退房收的是昨夜的房费，计入**今天**的日结。
        _grossIncomeToday = 0;
        _commissionToday = 0;
        ArrivalsCheckedInToday = 0;
        ArrivalsTurnedAwayToday = 0;
        _arrivalCredit = 0d;
        _deskQueue = 0;
        _deskCredit = 0d;
        PeakCheckInWaitToday = 0;
        TotalCheckInWaitToday = 0;
        FlawedStaysToday = 0;

        RunCheckoutWave();

        bool weekend = PricingPolicy.IsWeekend(Clock.CurrentDay);
        float ratio = Pricing.PriceRatioFor(Clock.CurrentDay);
        ArrivalsPlannedToday = DemandModel.ArrivalsFor(
            DemandCfg, Rooms.OpenRoomCount, Reputation.Stars, ratio, weekend, _rng.NextDouble(),
            Rooms.AverageTierNormalised);
    }

    /// <summary>晨间退房潮：结算房费与满意度，房间变脏（客群决定额外清洁负担）。</summary>
    private void RunCheckoutWave()
    {
        _checkoutsToday = 0;
        if (_stays.Count == 0) return;

        var checkingOut = new List<int>(_stays.Keys);
        foreach (int roomNumber in checkingOut)
        {
            if (!Rooms.Contains(roomNumber)) continue;
            Stay stay = _stays[roomNumber];
            RoomTier tier = Rooms.At(roomNumber).tier;

            float satisfaction = SatisfactionFor(stay, tier);
            int paid = SimMath.RoundToInt(stay.nightlyRate * satisfaction);
            BookRoomRevenue(paid);

            // VIP 的评价权重更高：多记一次样本（"差评更致命"的最简实现）
            Reputation.RecordGuest(satisfaction);
            if (GuestSegmentProfile.For(stay.segment).satisfactionWeight >= 2f)
                Reputation.RecordGuest(satisfaction);

            Rooms.SetState(roomNumber, RoomSimState.Dirty);
            ref RoomRecord room = ref Rooms.At(roomNumber);
            room.occupantResvId = 0;
            // 高周转客群把房间用得更狠 → 磨损更快（客流咬资产）
            room.wear = SimMath.Clamp01(room.wear + 0.01f * GuestSegmentProfile.For(stay.segment).extraCleaningLoad);

            _stays.Remove(roomNumber);
            _checkoutsToday++;
        }
    }

    private float SatisfactionFor(Stay stay, RoomTier tier)
    {
        float sat = 1f
                  - DemandModel.ExpectationPenalty(stay.priceRatio)
                  - DemandModel.TierDisappointment(stay.segment, tier)
                  - DemandModel.WaitSatisfactionPenalty(stay.segment, stay.waitMinutes);
        if (stay.flawedRoom) sat -= FlawedRoomPenalty;   // 没验房就上架的代价
        return SimMath.Clamp(sat, ReputationLedger.MinSatisfaction, ReputationLedger.MaxSatisfaction);
    }

    /// <summary>住进带瑕疵的房间（没经验房就上架）扣的满意度。</summary>
    public const float FlawedRoomPenalty = 0.22f;

    /// <summary>没有验房员时，清洁完直接上架的房有多大概率带瑕疵。</summary>
    public const double UninspectedFlawChance = 0.35d;

    // ── 每分钟 ───────────────────────────────────────────────────────────────

    /// <summary>推进一个游戏分钟（调用方负责先 Clock.TryConsumeTick）。</summary>
    public void StepMinute()
    {
        Pipeline.StepMinute();
        StepArrivals();
        StepFrontDesk();
    }

    /// <summary>到店客按阶段权重滴入：入住高峰吃掉大头（PhaseScheduler.ArrivalWeightOf）。</summary>
    private void StepArrivals()
    {
        int remaining = ArrivalsPlannedToday - ArrivalsCheckedInToday - ArrivalsTurnedAwayToday;
        if (remaining <= 0) return;

        SimDayPhase phase = PhaseScheduler.PhaseFor(Clock.CurrentMinute);
        float weight = PhaseScheduler.ArrivalWeightOf(phase);
        if (weight <= 0f) return;

        int phaseMinutes = PhaseMinutesOf(phase);
        if (phaseMinutes <= 0) return;

        _arrivalCredit += ArrivalsPlannedToday * weight / phaseMinutes;
        while (_arrivalCredit >= 1d && remaining > 0)
        {
            _arrivalCredit -= 1d;
            _deskQueue++;              // 到店先排队，不是瞬移进房
            remaining--;
        }
    }

    /// <summary>前台每分钟消化队列。人手不足 → 队伍变长 → 等待时间变长 →
    /// 商务客满意度掉得最狠（服务压力咬客流压力的另一条边）。</summary>
    private void StepFrontDesk()
    {
        if (_deskQueue <= 0)
        {
            _deskCredit = 0d;
            return;
        }

        float perHour = ServiceCapacityModel.CheckInsPerHour(Staff);
        if (perHour <= 0f)
        {
            // 前台无人：队伍只会变长（前台空岗惩罚已在 v2 的 StaffFacilitySystem 里有先例）
            PeakCheckInWaitToday = Math.Max(PeakCheckInWaitToday, EstimatedWaitMinutes(perHour));
            return;
        }

        _deskCredit += perHour / 60d;
        while (_deskCredit >= 1d && _deskQueue > 0)
        {
            _deskCredit -= 1d;
            int wait = EstimatedWaitMinutes(perHour);
            _deskQueue--;
            AdmitOneGuest(wait);
        }
        PeakCheckInWaitToday = Math.Max(PeakCheckInWaitToday, EstimatedWaitMinutes(perHour));
    }

    /// <summary>队列长度 ÷ 处理速率 = 队尾那位大概要等多久。</summary>
    private int EstimatedWaitMinutes(float checkInsPerHour)
    {
        if (_deskQueue <= 0) return 0;
        if (checkInsPerHour <= 0f) return 60;   // 无人值守：按一小时算（够狠）
        return SimMath.RoundToInt(_deskQueue / checkInsPerHour * 60f);
    }

    private static int PhaseMinutesOf(SimDayPhase phase)
    {
        switch (phase)
        {
            case SimDayPhase.CheckoutPeak: return PhaseScheduler.MiddayStart - PhaseScheduler.CheckoutPeakStart;
            case SimDayPhase.Midday: return PhaseScheduler.CheckInPeakStart - PhaseScheduler.MiddayStart;
            case SimDayPhase.CheckInPeak: return PhaseScheduler.EveningStart - PhaseScheduler.CheckInPeakStart;
            case SimDayPhase.Evening: return PhaseScheduler.SettleStart - PhaseScheduler.EveningStart;
            default: return 0;
        }
    }

    private void AdmitOneGuest(int waitMinutes)
    {
        if (!Rooms.TryFindFirstInState(RoomSimState.Ready, out int roomNumber))
        {
            // 没有可售房：这位客人走了（M-D 有预订簿后这里变成超售处置）
            ArrivalsTurnedAwayToday++;
            return;
        }

        int day = Clock.CurrentDay;
        float ratio = Pricing.PriceRatioFor(day);
        var mix = DemandModel.SegmentMixFor(ratio, PricingPolicy.IsWeekend(day));
        GuestSegment segment = mix.Pick(_rng.NextDouble());
        RoomTier tier = Rooms.At(roomNumber).tier;

        // 没有验房员在班时清洁完直接上架 → 这间房有概率带瑕疵（"快"的代价）
        bool flawed = !ServiceCapacityModel.HasInspectorOnDuty(Staff)
                      && _rng.NextDouble() < UninspectedFlawChance;
        if (flawed) FlawedStaysToday++;
        TotalCheckInWaitToday += waitMinutes;

        Rooms.SetState(roomNumber, RoomSimState.Occupied);
        _stays[roomNumber] = new Stay
        {
            nightlyRate = Pricing.PriceFor(day, tier),
            segment = segment,
            priceRatio = ratio,
            waitMinutes = waitMinutes,
            flawedRoom = flawed,
        };
        ArrivalsCheckedInToday++;
    }

    private void BookRoomRevenue(int amount)
    {
        if (amount <= 0) return;
        _grossIncomeToday += amount;
        _commissionToday += SimMath.RoundToInt(amount * SimMath.Clamp01(CommissionRate));
    }

    /// <summary>设施/小费等杂项收入（不进星级样本）。</summary>
    public void RecordMiscIncome(int amount)
    {
        if (amount <= 0) return;
        _grossIncomeToday += amount;   // 杂项不抽佣金
    }

    // ── 日结 ─────────────────────────────────────────────────────────────────

    /// <summary>打烊结算。返回结果供晨报展示。</summary>
    public DaySettlementResult SettleDay(int interest = 0, int scheduledRepayment = 0, int supplies = 0)
    {
        var input = new DaySettlementInput(
            grossIncome: _grossIncomeToday,
            commission: _commissionToday,
            wages: Shifts.DailyWageCost(Staff),
            interest: interest,
            scheduledRepayment: scheduledRepayment,
            supplies: supplies,
            cashOnHand: Cash);

        DaySettlementResult result = DaySettlement.Resolve(input, Safebox.RoomLeft);

        Safebox.Deposit(result.netToSafebox);
        Overflow.Add(result.overflowed);
        Overflow.RollTheft(_rng.NextDouble());
        Cash = result.cashAfter;

        // 顺序要紧：先算当晚磨损，再让装修完工——反过来会让"翻新重置磨损"当场被覆盖
        ApplyNightlyWear();
        AdvanceRenovations();
        Staff.SettleDay(result.wagesPaid);
        Pipeline.SettleDay(result.wagesPaid);

        LastSettlement = result;
        return result;
    }

    /// <summary>装修推进一天。完工的房档位提升，但装修完也是要打扫的（灰尘）。</summary>
    private void AdvanceRenovations()
    {
        var finished = Renovations.TickDay();
        for (int i = 0; i < finished.Count; i++)
        {
            var job = finished[i];
            for (int r = 0; r < job.roomNumbers.Count; r++)
            {
                int number = job.roomNumbers[r];
                if (!Rooms.Contains(number)) continue;
                ref RoomRecord room = ref Rooms.At(number);
                room.tier = job.targetTier;
                room.wear = 0f;                 // 翻新过的房重置磨损
                Rooms.RemoveFlags(number, RoomFlags.Renovating);
                Rooms.SetState(number, RoomSimState.Dirty); // 收工要清一遍才能卖
            }
        }
    }

    /// <summary>过夜磨损：住过的房磨损更快（满房加速磨损 → 损坏概率升）。</summary>
    private void ApplyNightlyWear()
    {
        for (int i = 0; i < Rooms.Count; i++)
        {
            ref RoomRecord room = ref Rooms.AtIndex(i);
            if (room.state == RoomSimState.Ruined) continue;
            bool occupied = room.state == RoomSimState.Occupied || room.state == RoomSimState.Dirty;
            room.wear = SimMath.Clamp01(room.wear + 0.02f * (occupied ? 1f : 0.2f));
        }
    }

    // ── 玩家动作 ─────────────────────────────────────────────────────────────

    /// <summary>收保险箱 → 现金。</summary>
    public int CollectSafebox()
    {
        int collected = Safebox.Collect();
        Cash += collected;
        return collected;
    }

    /// <summary>找回溢出账本（只有 65%）。</summary>
    public int RecoverOverflow()
    {
        int recovered = Overflow.Recover();
        Cash += recovered;
        return recovered;
    }

    /// <summary>主动支出（装修/材料/提前还款/升级/招聘）。付不起返回 false。</summary>
    public bool TrySpendCash(int amount)
    {
        if (amount <= 0) return true;
        if (Cash < amount) return false;
        Cash -= amount;
        return true;
    }

    // ── 存档（v4 增量第一批） ─────────────────────────────────────────────────

    /// <summary>捕获 Sim 跨日状态。房态在 RoomsState/装修表里，这里只存内核自己的东西。</summary>
    public void CaptureTo(SimState state)
    {
        if (state == null) return;

        state.day = Clock.CurrentDay;
        state.minute = Clock.CurrentMinute;
        state.totalMinutesElapsed = Clock.TotalMinutesElapsed;

        state.safeboxLevel = Safebox.Level;
        state.safeboxBalance = Safebox.Balance;
        state.overflowBalance = Overflow.Balance;
        state.cash = Cash;

        state.defaultPriceTemplate = (int)Pricing.DefaultTemplate;
        state.priceOverrides.Clear();
        foreach (var kv in Pricing.Overrides)
            state.priceOverrides.Add(new PriceOverrideEntry { day = kv.Key, template = (int)kv.Value });

        state.shiftTiers.Clear();
        foreach (StaffRole role in System.Enum.GetValues(typeof(StaffRole)))
            state.shiftTiers.Add(new ShiftTierEntry { role = (int)role, tier = (int)Shifts.TierOf(role) });

        state.nextStaffId = Staff.NextIdSeed;
        state.staff.Clear();
        var entries = Staff.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            state.staff.Add(new StaffSimState
            {
                staffId = e.staffId,
                rosterIndex = i,
                operationalState = (int)e.state,
                fatigue = e.fatigue,
                slackMinutesRemaining = e.slackMinutesRemaining,
            });
        }
    }

    /// <summary>恢复 Sim 跨日状态。名册须已按同样顺序重建（从 Payroll 建）。</summary>
    public void RestoreFrom(SimState state)
    {
        if (state == null) return;

        Clock.RestoreFromSave(state.day, state.minute, state.totalMinutesElapsed);

        Safebox.RestoreFromSave(state.safeboxLevel, state.safeboxBalance);
        Overflow.RestoreFromSave(state.overflowBalance);
        Cash = state.cash < 0 ? 0 : state.cash;

        Pricing.DefaultTemplate = (PriceTemplate)state.defaultPriceTemplate;
        Pricing.ClearAllOverrides();
        if (state.priceOverrides != null)
            foreach (var entry in state.priceOverrides)
                Pricing.SetOverride(entry.day, (PriceTemplate)entry.template);

        if (state.shiftTiers != null)
            foreach (var entry in state.shiftTiers)
                Shifts.SetTier((StaffRole)entry.role, (ShiftTier)entry.tier);

        Staff.RestoreIdSeed(state.nextStaffId);
        if (state.staff != null)
            foreach (var s in state.staff)
                Staff.RestoreEntryState(s.rosterIndex, s.staffId,
                                        (StaffOperationalState)s.operationalState,
                                        s.fatigue, s.slackMinutesRemaining);
    }

    /// <summary>跑完当天剩余的所有 tick（测试与跳段用）。</summary>
    public void RunToEndOfDay()
    {
        Clock.FastForwardTo(SimClock.DayEndMinute);
        while (Clock.TryConsumeTick()) StepMinute();
    }
}
