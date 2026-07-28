using System.Collections.Generic;
using NUnit.Framework;

// M-D-T5：超售落地的三选一处置（升级换房 / 赔钱送走 / 硬赶）。
// 断的是三条路各自的代价结构，不是具体数值——数值是调参旋钮。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class OverbookingTest
    {
        // ── 决策表本身 ────────────────────────────────────────────────────────

        [Test]
        public void UpgradeIsTheKindestOutcome_WalkingThemIsTheWorst()
        {
            float upgrade = OverbookingPolicy.SatisfactionFor(OverbookingResolution.Upgrade);
            float compensate = OverbookingPolicy.SatisfactionFor(OverbookingResolution.Compensate);
            float walk = OverbookingPolicy.SatisfactionFor(OverbookingResolution.WalkAway);

            Assert.That(upgrade, Is.GreaterThan(compensate));
            Assert.That(compensate, Is.GreaterThan(walk));
            Assert.That(walk, Is.EqualTo(ReputationLedger.MinSatisfaction), "硬赶就是最低分");
            Assert.That(upgrade, Is.GreaterThan(0.5f),
                        "订了便宜档却住进好房是**正面**体验——这是酒店业真实的救场手法");
        }

        [Test]
        public void CompensationCostsRealMoney_ScaledToWhatTheyPaid()
        {
            Assert.That(OverbookingPolicy.CompensationFor(100),
                        Is.GreaterThan(100), "要替他付别家的房费，还要赔个不是");
            Assert.That(OverbookingPolicy.CompensationFor(200),
                        Is.GreaterThan(OverbookingPolicy.CompensationFor(100)), "贵的单赔得多");
            Assert.That(OverbookingPolicy.CompensationFor(0), Is.EqualTo(0));
        }

        [Test]
        public void OnlyUpgradeNeedsARoom_OnlyCompensationNeedsCash()
        {
            Assert.That(OverbookingPolicy.NeedsARoom(OverbookingResolution.Upgrade), Is.True);
            Assert.That(OverbookingPolicy.NeedsARoom(OverbookingResolution.WalkAway), Is.False);
            Assert.That(OverbookingPolicy.CostsCash(OverbookingResolution.Compensate), Is.True);
            Assert.That(OverbookingPolicy.CostsCash(OverbookingResolution.WalkAway), Is.False,
                        "硬赶不花钱——这就是现金流断了之后的诱惑");
        }

        // ── 接线：什么时候真的会超售 ──────────────────────────────────────────

        /// <summary>造一个必然超售的局面：一间房、两张今天到店的单。</summary>
        private static HotelSim OversoldHotel(int seed = 4242)
        {
            var defs = new List<RoomDefinition>
            {
                new RoomDefinition(201, 1, 1, Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready),
            };
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            staff.Register(new StaffMember(StaffRole.Housekeeper, "H", 60, new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, seed);
            // 两张单硬塞进同一天：库存只有一间，第二位必然落到超售处置
            sim.Bookings.Add(BookingChannels.PlatformAId, 1, 1, RoomTier.Old, 80, GuestSegment.Budget, 1);
            sim.Bookings.Add(BookingChannels.PlatformAId, 1, 1, RoomTier.Old, 80, GuestSegment.Business, 1);
            return sim;
        }

        [Test]
        public void AReservationWithNoRoom_BecomesAPendingDecision_NotASilentTurnAway()
        {
            var sim = OversoldHotel();
            sim.BeginDay();
            sim.RunToEndOfDay();

            Assert.That(sim.OverbookingsToday, Is.GreaterThan(0), "没房的预订客要落成超售事件");
            Assert.That(sim.PendingOverbookings.Count, Is.GreaterThan(0), "并且挂着等玩家处置");
            Assert.That(sim.ArrivalsTurnedAwayToday, Is.EqualTo(0),
                        "**还没被赶走**——玩家还没做决定，不能先把账记成拒客");

            var incident = sim.PendingOverbookings[0];
            Assert.That(incident.lockedPrice, Is.GreaterThan(0), "赔钱要按他付的价算");
            Assert.That(incident.CompensationCost, Is.GreaterThan(incident.lockedPrice));
        }

        [Test]
        public void WalkInsWithNoRoom_AreJustTurnedAway_NoDecisionNeeded()
        {
            var sim = OversoldHotel();
            sim.BeginDay();
            sim.RunToEndOfDay();
            int overbookings = sim.PendingOverbookings.Count;

            // 上门客没有预订，你也没承诺过什么——不该占用玩家的决策注意力
            Assert.That(overbookings, Is.LessThanOrEqualTo(sim.ReservationArrivalsToday),
                        "超售事件只能来自预订客，walk-in 不该进这个队列");
        }

        // ── 三条路各自的后果 ──────────────────────────────────────────────────

        [Test]
        public void WalkingThemAway_CostsNothingButWreckstheRating()
        {
            var sim = OversoldHotel();
            sim.BeginDay(); sim.RunToEndOfDay();
            Assume.That(sim.PendingOverbookings.Count, Is.GreaterThan(0));
            int id = sim.PendingOverbookings[0].incidentId;
            int cashBefore = sim.Cash;
            float starsBefore = sim.Reputation.Stars;

            Assert.That(sim.TryResolveOverbooking(id, OverbookingResolution.WalkAway, out string why),
                        Is.True, why);

            Assert.That(sim.Cash, Is.EqualTo(cashBefore), "硬赶不花钱");
            Assert.That(sim.Reputation.Stars, Is.LessThan(starsBefore), "但星级要掉");
            Assert.That(sim.OverbookingsWalkedToday, Is.EqualTo(1));
            Assert.That(sim.ArrivalsTurnedAwayToday, Is.EqualTo(1), "这时候才算拒客");
            Assert.That(sim.PendingOverbookings, Is.Empty, "处置完就出队列");
        }

        [Test]
        public void Compensating_SpendsCash_AndIsRefusedWhenBroke()
        {
            var sim = OversoldHotel();
            sim.BeginDay(); sim.RunToEndOfDay();
            Assume.That(sim.PendingOverbookings.Count, Is.GreaterThan(0));
            var incident = sim.PendingOverbookings[0];
            int cashBefore = sim.Cash;

            Assert.That(sim.TryResolveOverbooking(incident.incidentId,
                        OverbookingResolution.Compensate, out string why), Is.True, why);

            Assert.That(sim.Cash, Is.EqualTo(cashBefore - incident.CompensationCost), "赔钱是真掏现金");
            Assert.That(sim.OverbookingsCompensatedToday, Is.EqualTo(1));
        }

        [Test]
        public void Compensating_FailsCleanlyWithNoCash_LeavingTheDecisionOpen()
        {
            var sim = OversoldHotel();
            sim.BeginDay(); sim.RunToEndOfDay();
            Assume.That(sim.PendingOverbookings.Count, Is.GreaterThan(0));
            int id = sim.PendingOverbookings[0].incidentId;
            sim.TrySpendCash(sim.Cash);          // 掏空现金

            Assert.That(sim.TryResolveOverbooking(id, OverbookingResolution.Compensate, out string why),
                        Is.False, "没钱赔不了");
            Assert.That(why, Is.Not.Empty, "要告诉玩家为什么不行");
            Assert.That(sim.PendingOverbookings.Count, Is.EqualTo(1),
                        "失败的处置不能把客人吞掉——决定还挂在那里");
        }

        [Test]
        public void Upgrading_NeedsARoom_AndSeatsThemAtTheOriginalPrice()
        {
            var sim = OversoldHotel();
            sim.BeginDay(); sim.RunToEndOfDay();
            Assume.That(sim.PendingOverbookings.Count, Is.GreaterThan(0));
            int id = sim.PendingOverbookings[0].incidentId;

            // 满房状态下升级不可行，而且失败要说清楚
            Assert.That(sim.CanUpgradeOverbooking(id), Is.False, "一间房全占着，没得升级");
            Assert.That(sim.TryResolveOverbooking(id, OverbookingResolution.Upgrade, out string why),
                        Is.False);
            Assert.That(why, Is.Not.Empty);
            Assert.That(sim.PendingOverbookings.Count, Is.EqualTo(1), "客人还等着");
        }

        [Test]
        public void Upgrading_WorksOnceARoomFrees_AndCountsAsAProperCheckIn()
        {
            // 两间可卖房、三张单：第三位超售。那间 Better 房开局是脏的（卖不出去），
            // "打扫好了"之后升级才能安顿下来。注意不能拿住着人的房来模拟腾房——
            // 台账权威下把 Occupied 写回 Ready 属于非法状态，分房点会正确地无视它。
            var defs = new List<RoomDefinition>
            {
                new RoomDefinition(201, 1, 1, Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready),
                new RoomDefinition(202, 1, 1, Room2DRoomCategory.Single, RoomTier.Better, RoomSimState.Dirty),
                new RoomDefinition(203, 1, 1, Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready),
            };
            // **不雇管家**：雇了的话那间 Dirty 房白天就被打扫好，第三位客人自己
            // 住进去，超售根本不会发生——这间脏房必须等测试亲手"打扫"
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, 8888);
            for (int i = 0; i < 3; i++)
                sim.Bookings.Add(BookingChannels.DirectId, 1, 1, RoomTier.Old, 80, GuestSegment.Budget, 1);

            sim.BeginDay(); sim.RunToEndOfDay();
            Assert.That(sim.PendingOverbookings.Count, Is.GreaterThan(0), "第三位该落到超售");
            var incident = sim.PendingOverbookings[0];
            int checkedInBefore = sim.ArrivalsCheckedInToday;

            // 那间 Better 脏房打扫好了（真正合法的"腾房"：它没有在住客人）
            sim.Rooms.SetState(202, RoomSimState.Ready);

            Assert.That(sim.CanUpgradeOverbooking(incident.incidentId), Is.True, "现在有房了");
            Assert.That(sim.TryResolveOverbooking(incident.incidentId,
                        OverbookingResolution.Upgrade, out string why), Is.True, why);

            Assert.That(sim.ArrivalsCheckedInToday, Is.EqualTo(checkedInBefore + 1),
                        "升级换房是一次真正的入住，要计入入住数");
            Assert.That(sim.Rooms.At(202).state, Is.EqualTo(RoomSimState.Occupied));
            Assert.That(sim.Rooms.At(202).occupantResvId, Is.EqualTo(incident.reservationId),
                        "房记录要指回那张单");
            Assert.That(sim.Bookings.Find(incident.reservationId).state,
                        Is.EqualTo(ReservationState.CheckedIn));
            Assert.That(sim.ArrivalsTurnedAwayToday, Is.EqualTo(0), "升级不是拒客");
        }

        // ── 拖着不处理最亏（与退款链同一个规矩）───────────────────────────────

        [Test]
        public void IgnoringThemUntilSettlement_IsWorseThanChoosingToWalkThem()
        {
            var chose = OversoldHotel(seed: 1234);
            var ignored = OversoldHotel(seed: 1234);

            // 先垫一段正常口碑：不垫的话两边的样本**全都是**最低分，
            // 平均值一模一样——多记几条同值样本不会改变平均，差异无从体现。
            foreach (var sim in new[] { chose, ignored })
                for (int i = 0; i < 10; i++) sim.Reputation.RecordGuest(1.3f);

            foreach (var sim in new[] { chose, ignored }) { sim.BeginDay(); sim.RunToEndOfDay(); }
            Assume.That(chose.PendingOverbookings.Count, Is.GreaterThan(0));
            Assume.That(ignored.PendingOverbookings.Count, Is.EqualTo(chose.PendingOverbookings.Count));

            // 一边主动硬赶，一边拖到日结
            chose.TryResolveOverbooking(chose.PendingOverbookings[0].incidentId,
                                        OverbookingResolution.WalkAway, out _);
            chose.SettleDay();
            ignored.SettleDay();

            Assert.That(ignored.Reputation.Stars, Is.LessThan(chose.Reputation.Stars),
                        "拖着不处理比主动赶人更亏——不然玩家永远选'不看手机'");
            Assert.That(ignored.PendingOverbookings, Is.Empty, "日结后队列要清空，不能跨日堆积");
            Assert.That(ignored.OverbookingsWalkedToday, Is.GreaterThan(0), "如实记成硬赶");
        }

        [Test]
        public void OverbookedGuestsAreNotCountedAsNoShows()
        {
            var sim = OversoldHotel();
            sim.BeginDay(); sim.RunToEndOfDay();
            Assume.That(sim.PendingOverbookings.Count, Is.GreaterThan(0));

            sim.SettleDay();

            Assert.That(sim.NoShowsToday, Is.EqualTo(0),
                        "人来了、是你没房——不能记成'客人没出现'（顺序错了就会）");
            Assert.That(sim.OverbookingsWalkedToday, Is.GreaterThan(0));
        }

        // ── 故意超售：赌取消率 ────────────────────────────────────────────────

        [Test]
        public void DeliberateOverbooking_TradesDeclinedBookingsForOverbookingRisk()
        {
            var careful = BuildSmallHotel(allowance: 0, seed: 555);
            var gambler = BuildSmallHotel(allowance: 4, seed: 555);

            int carefulOversold = 0, gamblerOversold = 0;
            int carefulDeclined = 0, gamblerDeclined = 0;
            for (int d = 0; d < 14; d++)
            {
                foreach (var sim in new[] { careful, gambler })
                {
                    sim.BeginDay(); sim.RunToEndOfDay();
                    // 一律硬赶，把处置策略这个变量固定住
                    while (sim.PendingOverbookings.Count > 0)
                        sim.TryResolveOverbooking(sim.PendingOverbookings[0].incidentId,
                                                  OverbookingResolution.WalkAway, out _);
                    sim.SettleDay(); sim.Clock.BeginNextDay();
                }
                carefulOversold += careful.OverbookingsToday;
                gamblerOversold += gambler.OverbookingsToday;
                carefulDeclined += careful.BookingsDeclinedToday;
                gamblerDeclined += gambler.BookingsDeclinedToday;
            }

            Assert.That(gamblerDeclined, Is.LessThan(carefulDeclined), "赌徒少拒了单");
            Assert.That(gamblerOversold, Is.GreaterThanOrEqualTo(carefulOversold),
                        "代价是更可能在到店日没房——这就是那笔赌注");
        }

        private static HotelSim BuildSmallHotel(int allowance, int seed)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < 3; i++)
                defs.Add(new RoomDefinition(201 + i, 1, 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            staff.Register(new StaffMember(StaffRole.Housekeeper, "H", 60, new StaffAttributes(55, 55, 55), 1, null));
            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, seed);
            sim.OverbookingAllowance = allowance;
            return sim;
        }
    }
}
