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
    /// <summary>一次住宿。身份由 BookingBook 的 Reservation 承载，这里只留结算要用的快照。</summary>
    private struct Stay
    {
        public int nightlyRate;
        public GuestSegment segment;
        public float priceRatio;
        public int waitMinutes;   // 前台排队时长（商务客最恨这个）
        public bool flawedRoom;   // 没验房就上架的房：有概率带瑕疵
        public float delivered;   // 入住时的实际交付水平（家具装饰度）
        public RoomTier band;     // 当时的挂牌档
        public int reservationId; // 0 = walk-in
        public int channelId;     // 抽成按渠道走：直营 0%，平台 A 18%
        public int nightsLeft;    // 还要住几晚。>0 的人早上不退房（连住占多个间夜）
    }

    /// <summary>用胶带糊上一件坏家具：**不花钱**，房间立刻能重新开卖，
    /// 代价是交付水平掉下去（客人看得见胶带）而且明天照坏。
    ///
    /// 这条出路是试玩逼出来的：现金归零时故障会把房一间间永久封死，
    /// 玩家第 23 天有 20 间房卡在 Blocked 里、既看不见也修不动，局面实际已死。
    /// 修理要钱，钱要靠卖房，卖房要有能用的房——这个环必须有一个零成本的缺口。</summary>
    public bool TryTapeFurniture(int instanceId, out string reason)
    {
        reason = "";
        if (!Furniture.TryGet(instanceId, out FurnitureInstance f)) { reason = "Nothing to fix there."; return false; }
        if (!f.IsFaulted) { reason = "That one works fine."; return false; }
        if (f.IsUnderRepair) { reason = "Someone's already on it."; return false; }
        if (f.taped) { reason = "It's already held together with tape."; return false; }

        f.taped = true;
        TapedTodayCount++;
        SyncRoomBlocksFromFurniture();      // 房间立刻放回可售流程
        return true;
    }

    /// <summary>今日糊了几件（晨报提醒玩家这些明天会复发）。</summary>
    public int TapedTodayCount { get; private set; }

    /// <summary>昨夜有几件胶带失效了（晨报的"复发"提示）。</summary>
    public int TapeExpiredToday { get; private set; }

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
    private int _arrivalsReleasedToday;   // 今日已放进前台队列的人数（累计目标制的游标）
    private double _deskCredit;      // 前台处理进度

    // 到店"票据"：正数 = 预订单号，0 = walk-in。今日该来的人先排在 _pendingArrivals，
    // 按阶段权重滴到 _deskQueue，前台按速率逐个消化。
    // 用票据而不是纯计数，是因为 check-in 时要知道**这位客人订的是哪一档**——
    // RoomMatcher 要靠它做库存保护（别把 Better 房贱卖给订 Old 的人）。
    private readonly Queue<int> _pendingArrivals = new Queue<int>();
    private readonly Queue<int> _deskQueue = new Queue<int>();
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
    public BookingBook Bookings { get; }
    public AvailabilityCalendar Calendar { get; }

    /// <summary>当日声誉明细（晨报读它回答"今天为什么涨/为什么掉"）。</summary>
    public ReputationBreakdown Breakdown { get; } = new ReputationBreakdown();

    /// <summary>一位有预订却没房的客人。玩家在手机上三选一（升级/赔钱/硬赶）。</summary>
    public sealed class OverbookingIncident
    {
        public int incidentId;
        public int reservationId;
        public RoomTier bookedBand;
        public int lockedPrice;
        public GuestSegment segment;
        public int waitMinutes;

        /// <summary>赔钱送走要花多少现金。</summary>
        public int CompensationCost => OverbookingPolicy.CompensationFor(lockedPrice);
    }

    private readonly List<RefundRequest> _refunds = new List<RefundRequest>();
    private int _nextRefundId;
    private readonly List<OverbookingIncident> _overbookings = new List<OverbookingIncident>();
    private int _nextOverbookingId;

    /// <summary>待处理的退款申请（进手机通知）。</summary>
    public IReadOnlyList<RefundRequest> PendingRefunds => _refunds;

    public int RefundsApprovedToday { get; private set; }
    public int RefundsRejectedToday { get; private set; }

    /// <summary>待处置的超售客（进手机通知，三选一）。</summary>
    public IReadOnlyList<OverbookingIncident> PendingOverbookings => _overbookings;

    /// <summary>今日超售了几位客人（赌输了的账单）。</summary>
    public int OverbookingsToday { get; private set; }
    public int OverbookingsUpgradedToday { get; private set; }
    public int OverbookingsCompensatedToday { get; private set; }
    public int OverbookingsWalkedToday { get; private set; }

    /// <summary>今日前台排队最长时长（分钟）。注意：前台无人时会顶到哨兵值，
    /// 做强弱对比请用 TotalCheckInWaitToday（客人实际承受的等待总量）。</summary>
    public int PeakCheckInWaitToday { get; private set; }

    /// <summary>今日所有入住客承受的等待分钟总和——满意度实际吃的就是它。</summary>
    public int TotalCheckInWaitToday { get; private set; }

    public int FlawedStaysToday { get; private set; }

    /// <summary>可支配现金：只能靠收保险箱补充，主动支出（装修/还款/招聘）只花它。</summary>
    public int Cash { get; private set; }

    /// <summary>历史遗留的单一佣金率。M-D 起抽成**按渠道**走（BookingChannels），
    /// 这个属性只留给设施/杂项收入之类没有渠道概念的入账口径。</summary>
    public float CommissionRate { get; set; } = 0.18f;

    /// <summary>今日排定的到店人数（晨报展示）= 今日预订 + walk-in。</summary>
    public int ArrivalsPlannedToday { get; private set; }
    public int ArrivalsCheckedInToday { get; private set; }
    public int ArrivalsTurnedAwayToday { get; private set; }
    public int CheckoutsToday => _checkoutsToday;
    public int GrossIncomeToday => _grossIncomeToday;

    /// <summary>今日渠道佣金（晨报要列明细：保险箱只收净额）。</summary>
    public int CommissionToday => _commissionToday;

    /// <summary>今日到店里有多少是提前预订的（其余为 walk-in）。</summary>
    public int ReservationArrivalsToday { get; private set; }
    public int WalkInArrivalsToday { get; private set; }

    /// <summary>今晨被取消的预订数（晨报"坏消息"栏）。</summary>
    public int CancellationsToday { get; private set; }

    /// <summary>没来的预订：占了一晚库存却分文未收，这是超售赌注的另一面。</summary>
    public int NoShowsToday { get; private set; }

    /// <summary>因为库存不够而没接下来的订单数——晨报据此提示"该开房了"。</summary>
    public int BookingsDeclinedToday { get; private set; }

    /// <summary>玩家设的"故意超售"档：赌渠道取消率，赌输了当天要处置超售客。</summary>
    public int OverbookingAllowance { get; set; }

    /// <summary>已了结的单在簿子里多留几天供晨报回看。</summary>
    public const int CompletedBookingGraceDays = 3;

    /// <summary>预订视野是否已铺开（第一个早晨要把 today..+13 一次填满，
    /// 之后每晨只填刚进入视野的那一天，否则同一天会被反复下单）。</summary>
    private bool _horizonSeeded;

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
        Bookings = new BookingBook();
        Calendar = new AvailabilityCalendar();
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
    /// <summary>批准退款：**从现金掏**。打烊结账（M-F）之后，退款申请出现时房费
    /// 已经入箱结算完了——再扣当日毛收入等于扣了个寂寞。现金不够就退不了，
    /// 只能拒绝并吃声誉（和赔偿超售客同一逻辑：没钱时最贵的是口碑）。</summary>
    public bool ApproveRefund(int requestId)
    {
        RefundRequest request = FindRefund(requestId);
        if (request == null) return false;
        if (!TrySpendCash(request.amount)) return false;
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
        Breakdown.Add(ReputationCause.RefundRefused, ReputationLedger.MinSatisfaction - 1f);
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

    // ── 超售处置（三选一，架构 §B.4） ────────────────────────────────────────

    private OverbookingIncident FindOverbooking(int incidentId)
    {
        for (int i = 0; i < _overbookings.Count; i++)
            if (_overbookings[i].incidentId == incidentId) return _overbookings[i];
        return null;
    }

    /// <summary>这位超售客现在能不能升级安顿下来（要有房才行；房可能是刚打扫好的）。</summary>
    public bool CanUpgradeOverbooking(int incidentId)
    {
        OverbookingIncident incident = FindOverbooking(incidentId);
        if (incident == null) return false;
        return TryPickRoomFor(incident.bookedBand, incident.segment, out _);
    }

    /// <summary>处置一位超售客。升级要有房、赔钱要有现金，硬赶永远可行（代价是声誉）。</summary>
    public bool TryResolveOverbooking(int incidentId, OverbookingResolution resolution, out string reason)
    {
        reason = "";
        OverbookingIncident incident = FindOverbooking(incidentId);
        if (incident == null) { reason = "Nobody's waiting on that."; return false; }

        Reservation reservation = Bookings.Find(incident.reservationId);

        switch (resolution)
        {
            case OverbookingResolution.Upgrade:
            {
                if (!TryPickRoomFor(incident.bookedBand, incident.segment, out int roomNumber))
                {
                    reason = "Still nothing free to put them in.";
                    return false;
                }
                // 按**原价**收——升级是你的赔礼，不是加价的机会
                CheckInResolvedGuest(incident, reservation, roomNumber);
                OverbookingsUpgradedToday++;
                break;
            }

            case OverbookingResolution.Compensate:
            {
                int cost = incident.CompensationCost;
                if (!TrySpendCash(cost))
                {
                    reason = "Compensation is $" + cost + " and you have $" + Cash + ".";
                    return false;
                }
                if (reservation != null) Bookings.MarkTurnedAway(reservation.id);
                ArrivalsTurnedAwayToday++;
                OverbookingsCompensatedToday++;
                break;
            }

            default:   // WalkAway
            {
                if (reservation != null) Bookings.MarkTurnedAway(reservation.id);
                ArrivalsTurnedAwayToday++;
                OverbookingsWalkedToday++;
                break;
            }
        }

        float satisfaction = OverbookingPolicy.SatisfactionFor(resolution);
        Reputation.RecordGuest(satisfaction);
        Breakdown.Add(CauseOf(resolution), satisfaction - 1f);
        _overbookings.Remove(incident);
        return true;
    }

    private static ReputationCause CauseOf(OverbookingResolution resolution)
    {
        switch (resolution)
        {
            case OverbookingResolution.Upgrade: return ReputationCause.OverbookingUpgraded;
            case OverbookingResolution.Compensate: return ReputationCause.OverbookingPaid;
            default: return ReputationCause.OverbookingWalked;
        }
    }

    /// <summary>升级换房安顿下来：与正常入住走同一套记账，只是房价用原来锁定的。</summary>
    private void CheckInResolvedGuest(OverbookingIncident incident, Reservation reservation, int roomNumber)
    {
        RoomTier band = Rooms.At(roomNumber).tier;
        TotalCheckInWaitToday += incident.waitMinutes;

        Rooms.SetState(roomNumber, RoomSimState.Occupied);
        ref RoomRecord record = ref Rooms.At(roomNumber);
        record.occupantResvId = incident.reservationId;

        _stays[roomNumber] = new Stay
        {
            nightlyRate = incident.lockedPrice,
            segment = incident.segment,
            priceRatio = Pricing.PriceRatioFor(Clock.CurrentDay),
            waitMinutes = incident.waitMinutes,
            flawedRoom = false,
            delivered = Furniture.DeliveredQuality(roomNumber),
            band = band,
            reservationId = incident.reservationId,
            channelId = reservation != null ? reservation.channelId : BookingChannels.DirectId,
            nightsLeft = reservation != null ? reservation.nights : 1,
        };

        if (reservation != null) Bookings.CheckIn(reservation.id, roomNumber);
        ArrivalsCheckedInToday++;
    }

    /// <summary>无视到日结的超售客：按硬赶处理，并额外扣声誉——拖着不处理绝不能划算。</summary>
    private void AutoResolveIgnoredOverbookings()
    {
        for (int i = 0; i < _overbookings.Count; i++)
        {
            OverbookingIncident incident = _overbookings[i];
            if (Bookings.Find(incident.reservationId) != null)
                Bookings.MarkTurnedAway(incident.reservationId);
            ArrivalsTurnedAwayToday++;
            OverbookingsWalkedToday++;

            float walked = OverbookingPolicy.SatisfactionFor(OverbookingResolution.WalkAway);
            Reputation.RecordGuest(walked);
            float logged = walked - 1f;
            for (int k = 0; k < OverbookingPolicy.IgnoredExtraReputationSamples; k++)
            {
                Reputation.RecordGuest(ReputationLedger.MinSatisfaction);
                logged += ReputationLedger.MinSatisfaction - 1f;
            }
            Breakdown.Add(ReputationCause.OverbookingWalked, logged);
        }
        _overbookings.Clear();
    }

    /// <summary>无视到日结：按拒绝处理，且额外掉一记声誉（比主动拒绝更亏）。</summary>
    private void AutoResolveIgnoredRefunds()
    {
        for (int i = 0; i < _refunds.Count; i++)
        {
            Reputation.RecordGuest(ReputationLedger.MinSatisfaction);
            Reputation.RecordGuest(ReputationLedger.MinSatisfaction);
            Breakdown.Add(ReputationCause.RefundIgnored, (ReputationLedger.MinSatisfaction - 1f) * 2f);
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



    /// <summary>找一间还没复原的破败房。</summary>
    public bool TryFindRuinedRoom(out int roomNumber) =>
        Rooms.TryFindFirstInState(RoomSimState.Ruined, out roomNumber);

    // ── 破败房复原（用户设计：破败房不是脏，是废，只能装修） ───────────────────

    /// <summary>能开工复原的破败房号。</summary>
    public List<int> ReclaimableRooms(int max)
    {
        var result = new List<int>();
        for (int i = 0; i < Rooms.Count && result.Count < max; i++)
        {
            RoomRecord room = Rooms.Peek(i);
            if (room.state != RoomSimState.Ruined) continue;
            if (Renovations.IsRenovating(room.number)) continue;
            result.Add(room.number);
        }
        return result;
    }

    /// <summary>某批破败房按某方案复原的报价（UI 展示批量折扣）。</summary>
    public int QuoteReclaim(ReclaimPlanKind kind, int roomCount) =>
        ReclaimPricing.CashCostFor(ReclaimPlan.For(kind), roomCount);

    /// <summary>开工复原破败房。房间保持 Ruined（施工中依然不可售），
    /// 完工时才装家具、转 Dirty 交给客房部打扫。
    /// 现金或材料不够则整单失败，不做部分成交（与装修同一规矩）。</summary>
    public bool TryStartReclaim(ReclaimPlanKind kind, IList<int> roomNumbers, out string reason)
    {
        reason = "";
        if (roomNumbers == null || roomNumbers.Count == 0) { reason = "Pick some rooms first."; return false; }

        var plan = ReclaimPlan.For(kind);
        var eligible = new List<int>();
        foreach (int number in roomNumbers)
        {
            if (!Rooms.Contains(number)) continue;
            if (Rooms.At(number).state != RoomSimState.Ruined) continue;   // 只有破败房能复原
            if (Renovations.IsRenovating(number)) continue;
            eligible.Add(number);
        }
        if (eligible.Count == 0) { reason = "No derelict rooms available to work on."; return false; }

        int cash = ReclaimPricing.CashCostFor(plan, eligible.Count);
        int materials = ReclaimPricing.MaterialCostFor(plan, eligible.Count);

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
        Renovations.EnqueueReclaim(plan, eligible);
        return true;
    }

    /// <summary>复原完工：按方案装家具，然后房间变脏交给客房部——施工完总得打扫。
    /// 于是复原会占用客房部工时，和退房脏房抢同一批人手。</summary>
    private void CompleteReclaim(SimRenovationJob job)
    {
        var plan = ReclaimPlan.For(job.reclaimKind);
        for (int r = 0; r < job.roomNumbers.Count; r++)
        {
            int number = job.roomNumbers[r];
            if (!Rooms.Contains(number)) continue;

            if (plan.keepsOldFurniture)
            {
                // 请维修工把原来的破家具修到能用：健康度回满，**崭新度一点不回**
                // （"维修不能动崭新度"是铁律）。所以这条路交付垫底，只配挂 Old。
                if (Furniture.InRoom(number).Count == 0)
                    Furniture.FurnishDerelictRoom(number, DerelictNewnessFloor, health: 1f);
                else
                    Furniture.ReviveRoomHealth(number);
            }
            else if (plan.fitsOptionalSlots)
            {
                Furniture.ReplaceRoomWithTopTier(number);      // 全套换新，够挂 Better
            }
            else
            {
                Furniture.FurnishWithNewRequired(number);      // 新床 + 新卫浴，够挂 Basic
            }

            Rooms.TryUnlockRuinedRoom(number);   // Ruined → Dirty：施工完是脏房，不是可售
        }
    }

    /// <summary>修旧家具那条路给出的崭新度（就是继承破家具的水平——它本来就是这个房里的旧东西）。</summary>
    private const float DerelictNewnessFloor = 0.10f;

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
        Pipeline.BeginDay();        // 清空"今日已清洁间数"

        // 先清零再跑退房潮——顺序反了会把刚结算的房费当场抹掉（踩过）。
        // 语义：晨间退房收的是昨夜的房费，计入**今天**的日结。
        _grossIncomeToday = 0;
        _commissionToday = 0;
        ArrivalsCheckedInToday = 0;
        ArrivalsTurnedAwayToday = 0;
        _arrivalsReleasedToday = 0;
        _deskQueue.Clear();
        _pendingArrivals.Clear();
        _deskCredit = 0d;
        PeakCheckInWaitToday = 0;
        TotalCheckInWaitToday = 0;
        FlawedStaysToday = 0;
        ReservationArrivalsToday = 0;
        WalkInArrivalsToday = 0;
        NoShowsToday = 0;
        OverbookingsToday = 0;
        OverbookingsUpgradedToday = 0;
        OverbookingsCompensatedToday = 0;
        OverbookingsWalkedToday = 0;
        TapedTodayCount = 0;

        // 声誉明细清零：晨报显示的是昨日累计的账（玩家点"开门营业"才走到这里，
        // 报告已经看完了）。退房潮已挪到 SettleDay（打烊结账），这里不再跑。
        Breakdown.Reset();

        RefundsApprovedToday = 0;
        RefundsRejectedToday = 0;

        RunBookingMorning();
        BuildTodaysArrivalQueue();
        ArrivalsPlannedToday = _pendingArrivals.Count;
    }

    // ── 预订流（架构 §B.4） ───────────────────────────────────────────────────
    //
    // 晨间顺序是有讲究的，换一步就出错：
    //   ① 按房态重算未来每天的容量（装修 Block 会让已卖出的间夜变成超售）
    //   ② 对未来的单掷取消骰（先取消再接新单，否则刚取消腾出的位当天用不上）
    //   ③ 生成新订单（第一个早晨铺满整个视野，之后只填刚进入视野的那天）
    //   ④ 按簿子**全量重算**日历需求（增量维护漏一处就永久对不上账）

    private void RunBookingMorning()
    {
        int today = Clock.CurrentDay;

        Calendar.PruneBefore(today);
        // 归档留几天缓冲：晨报要列"昨天住完的客人"，当天住完当天就删的话，
        // 退房潮刚结算完的单在同一次 BeginDay 里就被抹掉，界面和测试都看不见它。
        Bookings.PruneCompletedBefore(today - CompletedBookingGraceDays);
        RefreshCapacityForHorizon();

        CancellationsToday = Bookings.RollCancellations(today, _rng.NextDouble).Count;
        Bookings.RebuildCalendarDemand(Calendar);

        BookingsDeclinedToday = 0;
        if (!_horizonSeeded)
        {
            // 开局：整个视野一次铺开。不铺的话第一天没人订过房 = 空店开门
            for (int d = today; d < today + BookingGenerator.HorizonDays; d++) GenerateBookingsFor(d);
            _horizonSeeded = true;
        }
        else
        {
            GenerateBookingsFor(today + BookingGenerator.HorizonDays - 1);
        }
    }

    /// <summary>重算未来每天每档的可售房量。**是设置不是扣减**——
    /// 装修中的房在完工日之前不计入容量，于是已卖出的间夜自然把 remaining 压成负数。
    ///
    /// 铺的天数要比预订视野多出 MaxNights：视野最后一天的连住单会伸到视野之外，
    /// 那些天若没有容量（=0），CanAccept 会把所有连住单拒掉。</summary>
    private void RefreshCapacityForHorizon()
    {
        int today = Clock.CurrentDay;
        var perBand = new int[3];
        int daysToCover = BookingGenerator.HorizonDays + BookingGenerator.MaxNights;

        for (int offset = 0; offset < daysToCover; offset++)
        {
            int day = today + offset;
            perBand[0] = perBand[1] = perBand[2] = 0;

            for (int i = 0; i < Rooms.Count; i++)
            {
                RoomRecord room = Rooms.Peek(i);
                if (room.state == RoomSimState.Ruined) continue;      // 没解锁的房不是库存

                // 装修中的房：完工那天才回到库存（施工队还剩几天是已知的）
                int blockedDays = Renovations.DaysRemainingFor(room.number);
                if (blockedDays > offset) continue;

                perBand[(int)room.tier]++;
            }

            Calendar.SetCapacity(day, RoomTier.Old, perBand[0]);
            Calendar.SetCapacity(day, RoomTier.Basic, perBand[1]);
            Calendar.SetCapacity(day, RoomTier.Better, perBand[2]);
        }
    }

    /// <summary>酒店实际挂出来的档位（客人只能订这些）。</summary>
    private List<RoomTier> OfferedBands()
    {
        var bands = new List<RoomTier>(3);
        bool old = false, basic = false, better = false;
        for (int i = 0; i < Rooms.Count; i++)
        {
            RoomRecord room = Rooms.Peek(i);
            if (room.state == RoomSimState.Ruined) continue;
            if (room.tier == RoomTier.Old) old = true;
            else if (room.tier == RoomTier.Basic) basic = true;
            else better = true;
        }
        if (old) bands.Add(RoomTier.Old);
        if (basic) bands.Add(RoomTier.Basic);
        if (better) bands.Add(RoomTier.Better);
        return bands;
    }

    /// <summary>给某个未来日生成订单。库存不够的单直接谈崩（记进 BookingsDeclinedToday）。</summary>
    private void GenerateBookingsFor(int targetDay)
    {
        bool weekend = PricingPolicy.IsWeekend(targetDay);
        float ratio = Pricing.PriceRatioFor(targetDay);

        // 需求乘数用**实际交付水平**（家具装饰度），不是挂牌档——
        // 否则玩家把全店改标 Better 就能凭空拉来客人。
        int demand = DemandModel.ArrivalsFor(DemandCfg, Rooms.OpenRoomCount, Reputation.Stars,
                                             ratio, weekend, _rng.NextDouble(),
                                             AverageDeliveredQuality());

        var mix = DemandModel.SegmentMixFor(ratio, weekend);
        var intents = BookingGenerator.IntentsFor(targetDay, BookingGenerator.AdvanceDemandFor(demand),
                                                  mix, OfferedBands(), _rng.NextDouble);

        for (int i = 0; i < intents.Count; i++)
        {
            BookingIntent intent = intents[i];
            if (!Calendar.CanAccept(intent.arrivalDay, intent.nights, intent.tier, OverbookingAllowance))
            {
                BookingsDeclinedToday++;   // 满房 = 拒单，钱从指缝漏走（该开新房了）
                continue;
            }

            // 房价在下单当时锁定：之后调价不影响已售出的单（这才是"提前定价"的意义）
            int locked = Pricing.PriceFor(intent.arrivalDay, intent.tier);
            Bookings.Add(intent.channelId, intent.arrivalDay, intent.nights, intent.tier, locked,
                         intent.segment, bookedOnDay: Clock.CurrentDay);
            Calendar.Reserve(intent.arrivalDay, intent.nights, intent.tier);
        }
    }

    /// <summary>今日到店队列 = 今日预订（打乱前先按单号，稳定可复现）+ walk-in 补足。</summary>
    private void BuildTodaysArrivalQueue()
    {
        int today = Clock.CurrentDay;
        var arrivals = Bookings.ArrivalsFor(today);
        for (int i = 0; i < arrivals.Count; i++) _pendingArrivals.Enqueue(arrivals[i].id);
        ReservationArrivalsToday = arrivals.Count;

        // walk-in 是剩余需求流：按今日总需求的 20% 折算，与预订无关地另算一次
        bool weekend = PricingPolicy.IsWeekend(today);
        float ratio = Pricing.PriceRatioFor(today);
        int demandToday = DemandModel.ArrivalsFor(DemandCfg, Rooms.OpenRoomCount, Reputation.Stars,
                                                  ratio, weekend, _rng.NextDouble(),
                                                  AverageDeliveredQuality());
        WalkInArrivalsToday = BookingGenerator.WalkInDemandFor(demandToday);
        for (int i = 0; i < WalkInArrivalsToday; i++) _pendingArrivals.Enqueue(0);
    }

    /// <summary>晨间退房潮：结算房费与满意度，房间变脏（客群决定额外清洁负担）。
    ///
    /// **连住客不退房**：一张 n 晚的单在日历上占了 n 个间夜，房间也必须真的被占住 n 天，
    /// 否则库存说"占着"而房态说"空着"，超售检测立刻失去意义。
    /// 房费按晚计（住一晚收一晚），满意度与家具磨损同样按晚发生——
    /// 派对客住三晚就把家具磨三次，这是连住的真代价。</summary>
    private void RunCheckoutWave()
    {
        _checkoutsToday = 0;
        if (_stays.Count == 0) return;

        var occupied = new List<int>(_stays.Keys);
        foreach (int roomNumber in occupied)
        {
            if (!Rooms.Contains(roomNumber)) continue;
            Stay stay = _stays[roomNumber];

            // 昨夜这一晚：照收房费、照记评价、照磨家具
            float satisfaction = SatisfactionFor(stay, roomNumber);
            int paid = SimMath.RoundToInt(stay.nightlyRate * satisfaction);
            BookRoomRevenue(paid, stay.channelId);
            MaybeRequestRefund(roomNumber, stay, paid);

            // VIP 的评价权重更高：多记一次样本（"差评更致命"的最简实现）
            Reputation.RecordGuest(satisfaction);
            if (GuestSegmentProfile.For(stay.segment).satisfactionWeight >= 2f)
                Reputation.RecordGuest(satisfaction);

            // 家具按这一晚磨一次（客群倍率：派对客把家具用得最狠）
            Furniture.ApplyGuestNight(roomNumber, GuestSegmentProfile.For(stay.segment).extraCleaningLoad);

            stay.nightsLeft--;
            if (stay.nightsLeft > 0)
            {
                // 还要续住：房间继续 Occupied，不进清洁池（今天也没有这间房可卖）
                _stays[roomNumber] = stay;
                continue;
            }

            if (stay.reservationId > 0) Bookings.Complete(stay.reservationId);
            Rooms.SetState(roomNumber, RoomSimState.Dirty);
            ref RoomRecord room = ref Rooms.At(roomNumber);
            room.occupantResvId = 0;

            _stays.Remove(roomNumber);
            _checkoutsToday++;
        }
    }

    /// <summary>满意度：每一项都是一根设计好的杠杆。</summary>
    /// <summary>满意度：每一项都是一根设计好的杠杆，**并且每一项都记进当日明细**。
    /// 以前这些项算完就扔，玩家只看到星级上下动却不知道原因（试玩原话："没玩明白"）。</summary>
    private float SatisfactionFor(Stay stay, int roomNumber)
    {
        float priceExpectation = -DemandModel.ExpectationPenalty(stay.priceRatio);
        float bandVsDelivered = DemandModel.PriceBandSatisfactionDelta(stay.delivered, stay.band);
        float segmentStandards = -DemandModel.SegmentDisappointment(stay.segment, stay.delivered);
        float queueWait = -DemandModel.WaitSatisfactionPenalty(stay.segment, stay.waitMinutes);
        float appeal = DemandModel.AppealBonus(Furniture.SegmentAppeal(roomNumber, stay.segment));
        float flawed = stay.flawedRoom ? -FlawedRoomPenalty : 0f;

        Breakdown.Add(ReputationCause.PriceExpectation, priceExpectation);
        Breakdown.Add(ReputationCause.BandVsDelivered, bandVsDelivered);
        Breakdown.Add(ReputationCause.SegmentStandards, segmentStandards);
        Breakdown.Add(ReputationCause.QueueWait, queueWait);
        Breakdown.Add(ReputationCause.FurnitureAppeal, appeal);
        if (stay.flawedRoom) Breakdown.Add(ReputationCause.FlawedRoom, flawed);

        float sat = 1f + priceExpectation + bandVsDelivered + segmentStandards
                       + queueWait + appeal + flawed;
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
        if (_pendingArrivals.Count == 0) return;

        // 按**累计目标**放人，不用逐分钟累加信用额：
        // 到这一分钟为止该来的人数 = 排定人数 × 累计比例，还差几个就放几个。
        // 累计式在日终构造上等于 100%，不依赖任何浮点容差——
        // 逐分钟累加的写法会停在 99.97%，把当天最后一位客人静默吞掉（实测踩到）。
        float fraction = PhaseScheduler.CumulativeArrivalFractionAt(Clock.CurrentMinute);
        int shouldHaveArrived = SimMath.RoundToInt(ArrivalsPlannedToday * fraction);

        while (_arrivalsReleasedToday < shouldHaveArrived && _pendingArrivals.Count > 0)
        {
            _deskQueue.Enqueue(_pendingArrivals.Dequeue());   // 到店先排队，不是瞬移进房
            _arrivalsReleasedToday++;
        }
    }

    /// <summary>前台每分钟消化队列。人手不足 → 队伍变长 → 等待时间变长 →
    /// 商务客满意度掉得最狠（服务压力咬客流压力的另一条边）。</summary>
    private void StepFrontDesk()
    {
        if (_deskQueue.Count <= 0)
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
        while (_deskCredit >= 1d && _deskQueue.Count > 0)
        {
            _deskCredit -= 1d;
            int wait = EstimatedWaitMinutes(perHour);
            AdmitOneGuest(_deskQueue.Dequeue(), wait);
        }
        PeakCheckInWaitToday = Math.Max(PeakCheckInWaitToday, EstimatedWaitMinutes(perHour));
    }

    /// <summary>队列长度 ÷ 处理速率 = 队尾那位大概要等多久。</summary>
    private int EstimatedWaitMinutes(float checkInsPerHour)
    {
        if (_deskQueue.Count <= 0) return 0;
        if (checkInsPerHour <= 0f) return 60;   // 无人值守：按一小时算（够狠）
        return SimMath.RoundToInt(_deskQueue.Count / checkInsPerHour * 60f);
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

    /// <summary>办一位客人入住。ticket >0 = 预订单号，0 = walk-in。
    /// **两条流在这里合并**：都经 RoomMatcher 挑房，所以永远不会漂移成两套规则。</summary>
    private void AdmitOneGuest(int ticket, int waitMinutes)
    {
        int day = Clock.CurrentDay;
        float ratio = Pricing.PriceRatioFor(day);

        Reservation reservation = ticket > 0 ? Bookings.Find(ticket) : null;
        if (ticket > 0 && (reservation == null || reservation.state != ReservationState.Booked))
            return;   // 单子在到店前被取消/已处理，队列里的票据作废

        GuestSegment segment;
        RoomTier wantedBand;
        if (reservation != null)
        {
            segment = reservation.segment;
            wantedBand = reservation.tier;      // 订单承诺的档位
        }
        else
        {
            var mix = DemandModel.SegmentMixFor(ratio, PricingPolicy.IsWeekend(day));
            segment = mix.Pick(_rng.NextDouble());
            // walk-in 没订单，按客群期待去要一档（VIP 上门也会先问最好的房）
            wantedBand = BookingGenerator.PreferredBandFor(segment, OfferedBands());
        }

        if (!TryPickRoomFor(wantedBand, segment, out int roomNumber))
        {
            if (reservation == null)
            {
                // walk-in 没房就只能走：他没有预订，你也没承诺过什么
                ArrivalsTurnedAwayToday++;
                return;
            }

            // 有预订却没房 = 超售落地。**不当场赶人**，挂成待处置事件让玩家三选一。
            // 拖着不管到日结按"硬赶"处理并额外扣声誉（与退款链同一个规矩）。
            _overbookings.Add(new OverbookingIncident
            {
                incidentId = ++_nextOverbookingId,
                reservationId = reservation.id,
                bookedBand = reservation.tier,
                lockedPrice = reservation.lockedPrice,
                segment = segment,
                waitMinutes = waitMinutes,
            });
            OverbookingsToday++;
            return;
        }

        RoomTier band = Rooms.At(roomNumber).tier;

        // 没有验房员在班时清洁完直接上架 → 这间房有概率带瑕疵（"快"的代价）
        bool flawed = !ServiceCapacityModel.HasInspectorOnDuty(Staff)
                      && _rng.NextDouble() < UninspectedFlawChance;
        if (flawed) FlawedStaysToday++;
        TotalCheckInWaitToday += waitMinutes;

        Rooms.SetState(roomNumber, RoomSimState.Occupied);
        ref RoomRecord record = ref Rooms.At(roomNumber);
        record.occupantResvId = reservation != null ? reservation.id : 0;

        _stays[roomNumber] = new Stay
        {
            // 预订单用下单时锁定的价；walk-in 按今天的挂牌价
            nightlyRate = reservation != null ? reservation.lockedPrice : Pricing.PriceFor(day, band),
            segment = segment,
            priceRatio = ratio,
            waitMinutes = waitMinutes,
            flawedRoom = flawed,
            delivered = Furniture.DeliveredQuality(roomNumber), // 家具决定实际交付
            band = band,
            reservationId = reservation != null ? reservation.id : 0,
            channelId = reservation != null ? reservation.channelId : BookingChannels.DirectId,
            nightsLeft = reservation != null ? reservation.nights : 1,   // walk-in 一律一晚
        };

        if (reservation != null) Bookings.CheckIn(reservation.id, roomNumber);
        ArrivalsCheckedInToday++;
    }

    /// <summary>打烊时还没出现的今日预订 = no-show。
    /// 不收钱、不扣声誉（人没来，怪不到服务上），但那一晚的库存已经白占了——
    /// 这正是"故意超售"赌注的另一面：赌取消/no-show，赌赢了多赚，赌输了要处置人。</summary>
    private void ResolveNoShows()
    {
        var stillWaiting = Bookings.ArrivalsFor(Clock.CurrentDay);
        for (int i = 0; i < stillWaiting.Count; i++)
        {
            Bookings.MarkNoShow(stillWaiting[i].id);
            NoShowsToday++;
        }
    }

    /// <summary>把所有能卖的房交给 RoomMatcher 打分，挑最该给这位客人的那间。</summary>
    private bool TryPickRoomFor(RoomTier wantedBand, GuestSegment segment, out int roomNumber)
    {
        var candidates = new List<RoomCandidate>();
        for (int i = 0; i < Rooms.Count; i++)
        {
            RoomRecord room = Rooms.Peek(i);
            if (room.state != RoomSimState.Ready) continue;

            bool sellable = Furniture.InRoom(room.number).Count == 0
                            || Furniture.RequiredFurnitureWorking(room.number);
            if (!sellable) continue;   // 床塌了不能卖

            candidates.Add(new RoomCandidate(room.number, room.tier,
                                             Furniture.DeliveredQuality(room.number),
                                             Furniture.SegmentAppeal(room.number, segment),
                                             flawed: false, sellable: true));
        }

        return RoomMatcher.TryPick(new RoomRequest(wantedBand, segment), candidates, out roomNumber);
    }

    /// <summary>房费入账。抽成**按渠道**走：直营 0%，平台 A 18%——
    /// 这就是"把客人从平台养成回头客"的回报，也是 CommissionRate 只作兜底的原因。</summary>
    private void BookRoomRevenue(int amount, int channelId)
    {
        if (amount <= 0) return;
        _grossIncomeToday += amount;
        float rate = BookingChannels.Get(channelId).commission;
        _commissionToday += SimMath.RoundToInt(amount * SimMath.Clamp01(rate));
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
        // 顺序要紧：超售客的单在待处置期间仍是 Booked，
        // 先跑 no-show 会把"人来了、是你没房"错记成"人没来"。
        AutoResolveIgnoredOverbookings();
        ResolveNoShows();

        // 昨晚晨报上没处理的退款先按拒绝清掉（拖着不处理绝不能划算）——
        // 必须排在退房潮**之前**，否则今晚新产生的退款申请当场就被没收，
        // 玩家在晨报上永远见不到它们。
        AutoResolveIgnoredRefunds();

        // **打烊结账**（M-F 调整）：退房结算从次日早晨挪到 22:00。
        // 原时序下"今天卖了 7 间房"的钱要第二天早上才入账、日结永远晚一拍，
        // 晨报上没有任何可收的钱——收款是成就感的来源，不该被记账口径偷走。
        // 也与世界场景 v1 的"打烊清场"对齐：客人本来就是日终离场的。
        RunCheckoutWave();

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

        // 顺序要紧：先算当晚老化，再让装修/维修完工——反过来会让"翻新重置"当场被覆盖
        Furniture.ApplyIdleDay();
        FurnitureFaultsToday = Furniture.RollDailyFaults(() => _rng.NextDouble());
        // 胶带过夜失效（要排在 TickRepairs 之前：真修完的那件会自己撕掉胶带，
        // 只糊没修的次晨重新封房——这才是"明日复发"）
        TapeExpiredToday = Furniture.ExpireTape();
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

            // 破败房复原走另一套完工处理（装家具而不是翻新家具）
            if (job.isReclaim)
            {
                CompleteReclaim(job);
                for (int r = 0; r < job.roomNumbers.Count; r++)
                    Rooms.RemoveFlags(job.roomNumbers[r], RoomFlags.Renovating);
                continue;
            }

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

        // v5：家具 + 材料 + 挂牌档
        state.materialStock = Materials.Stock;
        state.nextFurnitureId = Furniture.NextIdSeed;
        state.furniture.Clear();
        var items = Furniture.All;
        for (int i = 0; i < items.Count; i++)
        {
            var f = items[i];
            state.furniture.Add(new FurnitureSaveEntry
            {
                instanceId = f.instanceId,
                kindId = f.kindId,
                roomNumber = f.roomNumber,
                posX = f.posX,
                posY = f.posY,
                newness = f.newness,
                health = f.health,
                faultLineIndex = f.faultLineIndex,
                repairDaysRemaining = f.repairDaysRemaining,
            });
        }
        state.roomBands.Clear();
        for (int i = 0; i < Rooms.Count; i++)
        {
            RoomRecord room = Rooms.Peek(i);
            state.roomBands.Add(new RoomBandEntry { room = room.number, band = (int)room.tier });
        }

        // v6：预订簿逐单入档。**日历不存**——容量每晨按房态重算、需求由簿子全量重算，
        // 存一份派生数据只会多出一个会对不上账的地方。
        state.nextReservationId = Bookings.NextIdSeed;
        state.overbookingAllowance = OverbookingAllowance;
        state.bookingHorizonSeeded = _horizonSeeded;
        state.reservations.Clear();
        var book = Bookings.All;
        for (int i = 0; i < book.Count; i++)
        {
            Reservation r = book[i];
            state.reservations.Add(new ReservationSaveEntry
            {
                id = r.id,
                channelId = r.channelId,
                bookedOnDay = r.bookedOnDay,
                arrivalDay = r.arrivalDay,
                nights = r.nights,
                tier = (int)r.tier,
                lockedPrice = r.lockedPrice,
                segment = (int)r.segment,
                state = (int)r.state,
                assignedRoomNumber = r.assignedRoomNumber,
            });
        }

        // v7：在建施工单 + 房态。以前两者都不存——花了钱的工单读档后凭空消失，
        // 而房态回落成默认值（装修中/破败/脏房全变可售）。
        state.nextBuildJobId = Renovations.NextJobIdSeed;
        state.buildJobs.Clear();
        var jobs = Renovations.Active;
        for (int i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];
            var entry = new BuildJobEntry
            {
                jobId = job.jobId,
                planKind = (int)job.planKind,
                targetTier = (int)job.targetTier,
                daysRemaining = job.daysRemaining,
                isReclaim = job.isReclaim,
                reclaimKind = (int)job.reclaimKind,
            };
            entry.rooms.AddRange(job.roomNumbers);
            state.buildJobs.Add(entry);
        }

        state.roomStates.Clear();
        for (int i = 0; i < Rooms.Count; i++)
        {
            RoomRecord room = Rooms.Peek(i);
            state.roomStates.Add(new RoomStateEntry { room = room.number, state = (int)room.state });
        }

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

        // v5：家具 + 材料 + 挂牌档（旧档这些段为空 → 沿用当前场景状态，不覆盖）
        Materials.RestoreFromSave(state.materialStock);
        if (state.furniture != null && state.furniture.Count > 0)
        {
            Furniture.Clear();
            foreach (var f in state.furniture)
                Furniture.RestoreInstance(f.instanceId, f.kindId, f.roomNumber, f.posX, f.posY,
                                          f.newness, f.health, f.faultLineIndex)
                         .repairDaysRemaining = f.repairDaysRemaining;
            Furniture.RestoreIdSeed(state.nextFurnitureId);
        }
        if (state.roomBands != null)
            foreach (var entry in state.roomBands)
                SetPriceBand(entry.room, (RoomTier)entry.band);

        // v6：预订簿。日历不读档——次晨 BeginDay 会按房态重算容量、按簿子重算需求。
        OverbookingAllowance = state.overbookingAllowance;
        _horizonSeeded = state.bookingHorizonSeeded;
        Bookings.Clear();
        if (state.reservations != null)
        {
            foreach (var r in state.reservations)
                Bookings.RestoreReservation(r.id, r.channelId, r.arrivalDay, r.nights,
                                            (RoomTier)r.tier, r.lockedPrice, (GuestSegment)r.segment,
                                            (ReservationState)r.state, r.assignedRoomNumber,
                                            r.bookedOnDay);
            Bookings.RestoreIdSeed(state.nextReservationId);
            Bookings.RebuildCalendarDemand(Calendar);
        }

        // v7：房态先恢复，再恢复工单（工单只是记着"这几间在施工"，不改房态）
        if (state.roomStates != null)
            foreach (var entry in state.roomStates)
                Rooms.SetState(entry.room, (RoomSimState)entry.state);

        Renovations.Clear();
        if (state.buildJobs != null)
            foreach (var job in state.buildJobs)
                Renovations.RestoreJob(job.jobId, (RenovationPlanKind)job.planKind,
                                       (RoomTier)job.targetTier, job.daysRemaining, job.rooms,
                                       job.isReclaim, (ReclaimPlanKind)job.reclaimKind);

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
