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
        public float delivered;   // 入住时的实际交付水平（家具装饰度）
        public RoomTier band;     // 当时的挂牌档
    }

    /// <summary>退款申请（虚报价的代价）。玩家在手机上批准/拒绝。</summary>
    public sealed class RefundRequest
    {
        public int requestId;
        public int roomNumber;
        public int amount;
        public GuestSegment segment;
        public float gap;          // 负值：交付比挂牌差多少
        public string line;        // 客人的原话（英文）
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
    public FurnitureLedger Furniture { get; }

    private readonly List<RefundRequest> _refunds = new List<RefundRequest>();
    private int _nextRefundId;

    /// <summary>待处理的退款申请（进手机通知）。</summary>
    public IReadOnlyList<RefundRequest> PendingRefunds => _refunds;

    public int RefundsApprovedToday { get; private set; }
    public int RefundsRejectedToday { get; private set; }

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
        Furniture = new FurnitureLedger();
    }

    // ── 挂牌档：玩家声称房间有多好（家具系统设计 §2） ──────────────────────────
    // RoomTier 不再表示品质，而是**挂出去的价格档**。品质由家具装饰度决定。
    // 按楼层批量设 + 个别房覆盖（与区域聚合、批量装修同一交互思路，不做逐间填表）。

    public bool SetPriceBand(int roomNumber, RoomTier band)
    {
        if (!Rooms.Contains(roomNumber)) return false;
        ref RoomRecord room = ref Rooms.At(roomNumber);
        room.tier = band;
        return true;
    }

    /// <summary>整层批量设挂牌档。返回改了多少间。</summary>
    public int SetPriceBandForFloor(int floor, RoomTier band)
    {
        int n = 0;
        for (int i = 0; i < Rooms.Count; i++)
        {
            if (Rooms.Peek(i).floor != floor) continue;
            ref RoomRecord room = ref Rooms.AtIndex(i);
            room.tier = band;
            n++;
        }
        return n;
    }

    /// <summary>某房的实际交付水平（家具装饰度，含崭新度折扣）。</summary>
    public float DeliveredQualityOf(int roomNumber) => Furniture.DeliveredQuality(roomNumber);

    /// <summary>全店平均交付水平——喂需求乘数（"更好的酒店更有人来"）。</summary>
    public float AverageDeliveredQuality()
    {
        int open = 0;
        float sum = 0f;
        for (int i = 0; i < Rooms.Count; i++)
        {
            RoomRecord room = Rooms.Peek(i);
            if (room.state == RoomSimState.Ruined) continue;
            open++;
            sum += Furniture.DeliveredQuality(room.number);
        }
        return open == 0 ? 0f : sum / open;
    }

    /// <summary>开局：给已开放的房配上继承来的破家具（崭新度与健康度都很低）。</summary>
    public void FurnishInheritedRooms(float minNewness = 0.05f, float maxNewness = 0.15f)
    {
        for (int i = 0; i < Rooms.Count; i++)
        {
            RoomRecord room = Rooms.Peek(i);
            if (room.state == RoomSimState.Ruined) continue;
            if (Furniture.InRoom(room.number).Count > 0) continue;
            float newness = minNewness + (float)_rng.NextDouble() * (maxNewness - minNewness);
            float health = 0.2f + (float)_rng.NextDouble() * 0.3f;
            Furniture.FurnishDerelictRoom(room.number, newness, health);
        }
    }

    /// <summary>解锁破败房时也要配上破家具。</summary>
    private void FurnishNewlyOpenedRoom(int roomNumber)
    {
        if (Furniture.InRoom(roomNumber).Count > 0) return;
        float newness = 0.05f + (float)_rng.NextDouble() * 0.1f;
        Furniture.FurnishDerelictRoom(roomNumber, newness, 0.2f + (float)_rng.NextDouble() * 0.2f);
    }

    // ── 家具买卖与维修 ────────────────────────────────────────────────────────

    /// <summary>买一件家具摆进房（新买即全新）。可选位满了就失败。</summary>
    public bool TryBuyFurniture(int roomNumber, int kindId, out string reason)
    {
        reason = "";
        if (!Rooms.Contains(roomNumber)) { reason = "No such room."; return false; }
        if (!FurnitureCatalog.TryGet(kindId, out FurnitureKind kind)) { reason = "No such furniture."; return false; }

        var existing = Furniture.InRoom(roomNumber);
        if (kind.IsRequired)
        {
            // 必备位是替换语义：先拆旧的（有残值）
            for (int i = 0; i < existing.Count; i++)
            {
                if (FurnitureCatalog.Get(existing[i].kindId).slot != kind.slot) continue;
                if (!TrySpendCash(kind.cashCost)) { reason = "That costs $" + kind.cashCost + "."; return false; }
                Cash += FurnitureWearModel.SalvageValue(FurnitureCatalog.Get(existing[i].kindId).cashCost,
                                                        existing[i].newness);
                Furniture.Remove(existing[i].instanceId);
                Furniture.Place(roomNumber, kindId);
                return true;
            }
        }
        else
        {
            int optional = 0;
            for (int i = 0; i < existing.Count; i++)
                if (!FurnitureCatalog.Get(existing[i].kindId).IsRequired) optional++;
            if (optional >= FurnitureCatalog.OptionalSlotCount)
            {
                reason = "No free decor slot in that room (" + FurnitureCatalog.OptionalSlotCount + " max).";
                return false;
            }
        }

        if (!TrySpendCash(kind.cashCost)) { reason = "That costs $" + kind.cashCost + " and you have $" + Cash + "."; return false; }
        Furniture.Place(roomNumber, kindId);
        return true;
    }

    /// <summary>卖掉一件家具（残值很低）。必备家具卖掉会让房间不可售。</summary>
    public int SellFurniture(int instanceId)
    {
        if (!Furniture.TryGet(instanceId, out FurnitureInstance f)) return 0;
        int value = FurnitureWearModel.SalvageValue(FurnitureCatalog.Get(f.kindId).cashCost, f.newness);
        Furniture.Remove(instanceId);
        Cash += value;
        return value;
    }

    /// <summary>安排维修：花钱 + 占用工期。修好只回健康度，崭新度不动。</summary>
    public bool TryRepairFurniture(int instanceId, out string reason)
    {
        reason = "";
        if (!Furniture.TryGet(instanceId, out FurnitureInstance f)) { reason = "Nothing to fix there."; return false; }
        if (!f.IsFaulted) { reason = "That one works fine."; return false; }
        if (f.IsUnderRepair) { reason = "Someone's already on it."; return false; }

        FurnitureKind kind = FurnitureCatalog.Get(f.kindId);
        if (!TrySpendCash(kind.repairCost))
        {
            reason = "Repair costs $" + kind.repairCost + " and you have $" + Cash + ".";
            return false;
        }
        Furniture.BeginRepair(instanceId, kind.repairDays);
        return true;
    }

    // ── 退款申请 ─────────────────────────────────────────────────────────────

    /// <summary>批准退款：退还房费，声誉不再额外受损。</summary>
    public bool ApproveRefund(int requestId)
    {
        RefundRequest request = FindRefund(requestId);
        if (request == null) return false;
        _grossIncomeToday -= request.amount;
        if (_grossIncomeToday < 0) _grossIncomeToday = 0;
        _refunds.Remove(request);
        RefundsApprovedToday++;
        return true;
    }

    /// <summary>拒绝退款：钱保住，但再补一记差评（且可能升级成客诉事件）。</summary>
    public bool RejectRefund(int requestId)
    {
        RefundRequest request = FindRefund(requestId);
        if (request == null) return false;
        Reputation.RecordGuest(ReputationLedger.MinSatisfaction);
        _refunds.Remove(request);
        RefundsRejectedToday++;
        return true;
    }

    private RefundRequest FindRefund(int requestId)
    {
        for (int i = 0; i < _refunds.Count; i++)
            if (_refunds[i].requestId == requestId) return _refunds[i];
        return null;
    }

    /// <summary>无视到日结：按拒绝处理，且额外掉一记声誉（比主动拒绝更亏）。</summary>
    private void AutoResolveIgnoredRefunds()
    {
        for (int i = 0; i < _refunds.Count; i++)
        {
            Reputation.RecordGuest(ReputationLedger.MinSatisfaction);
            Reputation.RecordGuest(ReputationLedger.MinSatisfaction);
        }
        _refunds.Clear();
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
            if (!RenovationWouldHelp(kind, number)) continue;     // 已经没什么可翻的
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
        FurnishNewlyOpenedRoom(roomNumber);   // 清出来的房也只有一套破家具
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

    /// <summary>这个方案对这间房还有没有意义（避免白花钱）。</summary>
    private bool RenovationWouldHelp(RenovationPlanKind kind, int roomNumber)
    {
        var items = Furniture.InRoom(roomNumber);
        if (items.Count == 0) return true;             // 空房：装了才有家具

        switch (kind)
        {
            case RenovationPlanKind.Economy:
                return Furniture.AverageNewness(roomNumber) < 0.95f;   // 还有崭新度可翻
            case RenovationPlanKind.Standard:
                for (int i = 0; i < items.Count; i++)
                {
                    FurnitureKind k = FurnitureCatalog.Get(items[i].kindId);
                    if (k.IsRequired && FurnitureCatalog.UpgradedRequiredKind(items[i].kindId) != items[i].kindId)
                        return true;                                    // 必备家具还能升
                }
                return Furniture.AverageNewness(roomNumber) < 0.95f;
            default: // Luxury
                return Furniture.DeliveredQuality(roomNumber) < 0.99f;  // 还没到顶配
        }
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
        RefundsApprovedToday = 0;
        RefundsRejectedToday = 0;

        // 需求乘数用**实际交付水平**（家具装饰度），不是挂牌档——
        // 否则玩家把全店改标 Better 就能凭空拉来客人。
        ArrivalsPlannedToday = DemandModel.ArrivalsFor(
            DemandCfg, Rooms.OpenRoomCount, Reputation.Stars, ratio, weekend, _rng.NextDouble(),
            AverageDeliveredQuality());
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

            float satisfaction = SatisfactionFor(stay, roomNumber);
            int paid = SimMath.RoundToInt(stay.nightlyRate * satisfaction);
            BookRoomRevenue(paid);
            MaybeRequestRefund(roomNumber, stay, paid);

            // VIP 的评价权重更高：多记一次样本（"差评更致命"的最简实现）
            Reputation.RecordGuest(satisfaction);
            if (GuestSegmentProfile.For(stay.segment).satisfactionWeight >= 2f)
                Reputation.RecordGuest(satisfaction);

            Rooms.SetState(roomNumber, RoomSimState.Dirty);
            ref RoomRecord room = ref Rooms.At(roomNumber);
            room.occupantResvId = 0;

            // 家具按这一晚磨一次（客群倍率：派对客把家具用得最狠）
            Furniture.ApplyGuestNight(roomNumber, GuestSegmentProfile.For(stay.segment).extraCleaningLoad);

            _stays.Remove(roomNumber);
            _checkoutsToday++;
        }
    }

    /// <summary>满意度：每一项都是一根设计好的杠杆。</summary>
    private float SatisfactionFor(Stay stay, int roomNumber)
    {
        float sat = 1f
                  - DemandModel.ExpectationPenalty(stay.priceRatio)                          // 定价模板高于市场
                  + DemandModel.PriceBandSatisfactionDelta(stay.delivered, stay.band)         // 挂牌 vs 交付（双向）
                  - DemandModel.SegmentDisappointment(stay.segment, stay.delivered)           // 客人个人标准
                  - DemandModel.WaitSatisfactionPenalty(stay.segment, stay.waitMinutes)       // 前台排队
                  + DemandModel.AppealBonus(Furniture.SegmentAppeal(roomNumber, stay.segment)); // 家具对口味
        if (stay.flawedRoom) sat -= FlawedRoomPenalty;   // 没验房就上架的代价
        return SimMath.Clamp(sat, ReputationLedger.MinSatisfaction, ReputationLedger.MaxSatisfaction);
    }

    /// <summary>虚报价的代价：交付远低于挂牌 → 按概率生成退款申请。</summary>
    private void MaybeRequestRefund(int roomNumber, Stay stay, int paid)
    {
        double chance = DemandModel.RefundChanceFor(stay.delivered, stay.band);
        if (chance <= 0d || _rng.NextDouble() >= chance) return;

        float gap = stay.delivered - DemandModel.ExpectedQualityOf(stay.band);
        _refunds.Add(new RefundRequest
        {
            requestId = ++_nextRefundId,
            roomNumber = roomNumber,
            amount = paid,
            segment = stay.segment,
            gap = gap,
            line = RefundLineFor(stay.segment),
        });
    }

    private static string RefundLineFor(GuestSegment segment)
    {
        switch (segment)
        {
            case GuestSegment.Business: return "I book fifty nights a year. Not here, apparently. Refund.";
            case GuestSegment.Party: return "We've seen better. And we were not sober. Refund.";
            case GuestSegment.Vip: return "I have told people about this place. I will tell more. Refund.";
            default: return "I paid for a room, not an experience. Refund.";
        }
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
        if (!TryFindSellableRoom(out int roomNumber))
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
            nightlyRate = Pricing.PriceFor(day, tier),   // tier = 挂牌档，决定收多少钱
            segment = segment,
            priceRatio = ratio,
            waitMinutes = waitMinutes,
            flawedRoom = flawed,
            delivered = Furniture.DeliveredQuality(roomNumber), // 家具决定实际交付
            band = tier,
        };
        ArrivalsCheckedInToday++;
    }

    /// <summary>找一间真能卖的房：Ready 且必备家具可用（床塌了不能卖）。</summary>
    private bool TryFindSellableRoom(out int roomNumber)
    {
        for (int i = 0; i < Rooms.Count; i++)
        {
            RoomRecord room = Rooms.Peek(i);
            if (room.state != RoomSimState.Ready) continue;
            if (Furniture.InRoom(room.number).Count > 0
                && !Furniture.RequiredFurnitureWorking(room.number)) continue;
            roomNumber = room.number;
            return true;
        }
        roomNumber = 0;
        return false;
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

        // 无视到日结的退款申请按拒绝处理，且额外掉声誉（比主动拒绝更亏）
        AutoResolveIgnoredRefunds();

        // 顺序要紧：先算当晚老化，再让装修/维修完工——反过来会让"翻新重置"当场被覆盖
        Furniture.ApplyIdleDay();
        FurnitureFaultsToday = Furniture.RollDailyFaults(() => _rng.NextDouble());
        Furniture.TickRepairs();
        ApplyNightlyWear();
        AdvanceRenovations();
        SyncRoomWearFromFurniture();
        SyncRoomBlocksFromFurniture();

        Staff.SettleDay(result.wagesPaid);
        Pipeline.SettleDay(result.wagesPaid);

        LastSettlement = result;
        return result;
    }

    /// <summary>本日新出故障的家具（交给 BreakdownSystem / 手机通知呈现）。</summary>
    public List<FurnitureInstance> FurnitureFaultsToday { get; private set; } = new List<FurnitureInstance>();

    /// <summary>装修推进一天。完工时按方案处理家具（家具系统设计 §5）——
    /// **挂牌档 room.tier 不再被装修改动**，那是玩家自己定的价格档。</summary>
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

                switch (job.planKind)
                {
                    case RenovationPlanKind.Economy:
                        Furniture.RefurbishRoom(number);                  // 纯翻新：崭新度回满
                        break;
                    case RenovationPlanKind.Standard:
                        Furniture.RefurbishAndUpgradeRequired(number);    // 翻新 + 必备升一档
                        break;
                    default:
                        Furniture.ReplaceRoomWithTopTier(number);         // 全套换顶配
                        break;
                }

                ref RoomRecord room = ref Rooms.At(number);
                room.wear = 0f;
                Rooms.RemoveFlags(number, RoomFlags.Renovating);
                Rooms.SetState(number, RoomSimState.Dirty); // 收工要清一遍才能卖
            }
        }
    }

    /// <summary>房间磨损由家具平均健康度派生（避免两套衰减系统）。</summary>
    private void SyncRoomWearFromFurniture()
    {
        for (int i = 0; i < Rooms.Count; i++)
        {
            int number = Rooms.Peek(i).number;
            if (Furniture.InRoom(number).Count == 0) continue;
            ref RoomRecord room = ref Rooms.AtIndex(i);
            room.wear = SimMath.Clamp01(1f - Furniture.AverageHealth(number));
        }
    }

    /// <summary>必备家具坏了/在修 → 房间封锁；修好且房态是封锁 → 放回脏房待清。</summary>
    private void SyncRoomBlocksFromFurniture()
    {
        for (int i = 0; i < Rooms.Count; i++)
        {
            RoomRecord snapshot = Rooms.Peek(i);
            if (snapshot.state == RoomSimState.Ruined || snapshot.state == RoomSimState.Occupied) continue;
            if (Rooms.IsSurfaced(snapshot.number) && Renovations.IsRenovating(snapshot.number)) continue;
            if (Furniture.InRoom(snapshot.number).Count == 0) continue;

            bool usable = Furniture.RequiredFurnitureWorking(snapshot.number);
            if (!usable && snapshot.state != RoomSimState.Blocked)
            {
                Rooms.SetState(snapshot.number, RoomSimState.Blocked);
                Rooms.AddFlags(snapshot.number, RoomFlags.Problem);
            }
            else if (usable && snapshot.state == RoomSimState.Blocked
                     && !Renovations.IsRenovating(snapshot.number))
            {
                Rooms.RemoveFlags(snapshot.number, RoomFlags.Problem);
                Rooms.SetState(snapshot.number, RoomSimState.Dirty);
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
