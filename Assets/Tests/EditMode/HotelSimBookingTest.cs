using System.Collections.Generic;
using NUnit.Framework;

// M-D 接线验收：预订流取代"直接掷客量"，walk-in 降级为剩余需求流。
// 这里断的是**流程接对了**，不是任何具体数字——客量本身由 DemandModel 的测试守着。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class HotelSimBookingTest
    {
        private static HotelSim BuildHotel(int rooms = 20, int startingCash = 20000, int seed = 31337,
                                           bool furnish = false, int housekeepers = 3, int receptionists = 2)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            for (int i = 0; i < receptionists; i++)
                staff.Register(new StaffMember(StaffRole.Reception, "R" + i, 65, new StaffAttributes(55, 55, 55), 1, null));
            for (int i = 0; i < housekeepers; i++)
                staff.Register(new StaffMember(StaffRole.Housekeeper, "H" + i, 60, new StaffAttributes(55, 55, 55), 1, null));
            staff.Register(new StaffMember(StaffRole.Inspector, "I", 70, new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, startingCash, seed);
            if (furnish) sim.FurnishInheritedRooms();
            return sim;
        }

        private static void RunOneDay(HotelSim sim)
        {
            sim.BeginDay();
            sim.RunToEndOfDay();
            sim.SettleDay();
            sim.Clock.BeginNextDay();
        }

        // ── 开门第一天就得有生意 ──────────────────────────────────────────────

        [Test]
        public void FirstMorning_SeedsTheWholeHorizon_SoDayOneIsNotAnEmptyHotel()
        {
            var sim = BuildHotel();
            sim.BeginDay();

            Assert.That(sim.ArrivalsPlannedToday, Is.GreaterThan(0),
                        "开局第一天不能是空店——第一个早晨要把整个预订视野一次铺开");
            Assert.That(sim.ReservationArrivalsToday, Is.GreaterThan(0), "今天该有提前订好的客人");

            // 视野末尾那天也已经有人下单
            int lastDay = sim.Clock.CurrentDay + BookingGenerator.HorizonDays - 1;
            Assert.That(sim.Bookings.ArrivalsFor(lastDay).Count, Is.GreaterThan(0),
                        "两周后的那天也该有预订，玩家才有东西可规划");
        }

        [Test]
        public void HorizonIsSeededOnce_NotRefilledEveryMorningForTheSameDay()
        {
            var sim = BuildHotel();
            sim.BeginDay();
            int day3Before = sim.Bookings.ArrivalsFor(3).Count;
            Assume.That(day3Before, Is.GreaterThan(0));

            sim.RunToEndOfDay(); sim.SettleDay(); sim.Clock.BeginNextDay();
            sim.BeginDay();     // 第二个早晨

            Assert.That(sim.Bookings.ArrivalsFor(3).Count, Is.LessThanOrEqualTo(day3Before),
                        "同一天不能被反复下单——第二个早晨只该填刚进入视野的那一天（取消只会让它变少）");
        }

        [Test]
        public void ArrivalsAreReservationsPlusWalkIns()
        {
            var sim = BuildHotel();
            sim.BeginDay();

            Assert.That(sim.ArrivalsPlannedToday,
                        Is.EqualTo(sim.ReservationArrivalsToday + sim.WalkInArrivalsToday),
                        "今日到店 = 提前预订 + walk-in，一个人都不能凭空多出来或漏掉");
            Assert.That(sim.WalkInArrivalsToday, Is.GreaterThan(0), "walk-in 仍是活的剩余需求流");
            Assert.That(sim.ReservationArrivalsToday, Is.GreaterThan(sim.WalkInArrivalsToday),
                        "提前预订才是主流量");
        }

        // ── 入住把订单与房间真正绑起来 ────────────────────────────────────────

        [Test]
        public void CheckingIn_BindsTheReservationToARealRoom()
        {
            var sim = BuildHotel();
            sim.BeginDay();
            sim.RunToEndOfDay();
            Assume.That(sim.ArrivalsCheckedInToday, Is.GreaterThan(0));

            var inHouse = sim.Bookings.InHouse();
            Assert.That(inHouse.Count, Is.GreaterThan(0), "在住的单要能查出来");

            foreach (var r in inHouse)
            {
                Assert.That(sim.Rooms.Contains(r.assignedRoomNumber), Is.True, "分到的房号必须真实存在");
                Assert.That(sim.Rooms.At(r.assignedRoomNumber).state, Is.EqualTo(RoomSimState.Occupied));
                Assert.That(sim.Rooms.At(r.assignedRoomNumber).occupantResvId, Is.EqualTo(r.id),
                            "房记录里的 occupantResvId 要指回那张单（身份不能对不上）");
            }
        }

        [Test]
        public void ReservationsPayTheLockedPrice_NotTodaysPrice()
        {
            var sim = BuildHotel();
            sim.BeginDay();
            var arrivals = sim.Bookings.ArrivalsFor(sim.Clock.CurrentDay);
            Assume.That(arrivals.Count, Is.GreaterThan(0));
            int locked = arrivals[0].lockedPrice;

            // 客人到店前把定价模板拉到榨利润档
            sim.Pricing.DefaultTemplate = PriceTemplate.Squeeze;
            sim.RunToEndOfDay();

            var inHouse = sim.Bookings.InHouse();
            Assume.That(inHouse.Count, Is.GreaterThan(0));
            Assert.That(inHouse[0].lockedPrice, Is.EqualTo(locked),
                        "下单时锁的价不该被之后的调价改掉——这才是提前定价的意义");
        }

        // ── 连住：日历占 n 个间夜，房间也必须真被占 n 天 ───────────────────────

        [Test]
        public void MultiNightStay_HoldsTheRoomForEveryNightItPaidFor()
        {
            var sim = BuildHotel(rooms: 6, housekeepers: 2, receptionists: 2);

            // 三晚单必须在 BeginDay **之前**放进去：今日到店队列是 BeginDay 里建的，
            // 之后再加单只会挂在簿子上等到打烊变成 no-show（第一版就这么写，结果测试
            // 被 Assume 判成 Inconclusive 静默跳过——跳过的测试不是测试）。
            var planted = sim.Bookings.Add(BookingChannels.DirectId, arrivalDay: 1, nights: 3,
                                           RoomTier.Old, 80, GuestSegment.Business, bookedOnDay: 1);
            sim.BeginDay();
            sim.RunToEndOfDay();

            var stay = sim.Bookings.Find(planted.id);
            Assume.That(stay.state, Is.EqualTo(ReservationState.CheckedIn), "三晚单得先住进来");
            int room = stay.assignedRoomNumber;

            // 第 2 天早上：还在住，房间不该进清洁池
            sim.SettleDay(); sim.Clock.BeginNextDay(); sim.BeginDay();
            Assert.That(sim.Rooms.At(room).state, Is.EqualTo(RoomSimState.Occupied),
                        "第二晚：连住客不退房，房间也不该变脏可卖");
            Assert.That(sim.Bookings.Find(planted.id).state, Is.EqualTo(ReservationState.CheckedIn));

            // 第 3 天早上：仍在住
            sim.RunToEndOfDay(); sim.SettleDay(); sim.Clock.BeginNextDay(); sim.BeginDay();
            Assert.That(sim.Rooms.At(room).state, Is.EqualTo(RoomSimState.Occupied), "第三晚还在住");

            // 第 4 天早上：三晚住完，退房
            sim.RunToEndOfDay(); sim.SettleDay(); sim.Clock.BeginNextDay(); sim.BeginDay();
            Assert.That(sim.Bookings.Find(planted.id).state, Is.EqualTo(ReservationState.Completed),
                        "三晚住完才算结束");
            Assert.That(sim.Rooms.At(room).state, Is.Not.EqualTo(RoomSimState.Occupied),
                        "住完了房间要放回清洁流程");
        }

        // ── 装修 Block 会把已卖出的间夜变成超售 ───────────────────────────────

        [Test]
        public void BlockingSoldRoomsForRenovation_OversellsThoseDays()
        {
            var sim = BuildHotel(rooms: 8, startingCash: 60000);
            sim.TryBuyMaterials(60);
            sim.BeginDay();     // 先把预订铺开，间夜卖出去

            int targetDay = sim.Clock.CurrentDay + 3;
            Assume.That(sim.Calendar.DemandOn(targetDay, RoomTier.Old), Is.GreaterThan(0),
                        "得先有卖出去的间夜才谈得上超售");
            Assume.That(sim.Calendar.IsOversold(targetDay, RoomTier.Old), Is.False);

            // 把大半间房送去装修（Economy 关房 4 天，覆盖 targetDay）
            var rooms = new List<int> { 201, 202, 203, 204, 205, 206 };
            Assert.That(sim.TryStartRenovation(RenovationPlanKind.Economy, rooms, out string why), Is.True, why);

            sim.RunToEndOfDay(); sim.SettleDay(); sim.Clock.BeginNextDay();
            sim.BeginDay();     // 次晨按新房态重算容量

            Assert.That(sim.Calendar.IsOversold(targetDay, RoomTier.Old), Is.True,
                        "把已经卖掉的间夜送去装修 ⇒ 那天超售。这就是装修的真代价");
            Assert.That(sim.Calendar.OversoldCountOn(targetDay, RoomTier.Old), Is.GreaterThan(0));
        }

        // ── 满房拒单：库存不够时不接单，而不是接了再赶人 ───────────────────────

        [Test]
        public void WhenInventoryRunsOut_BookingsAreDeclinedRatherThanQuietlyAccepted()
        {
            var tiny = BuildHotel(rooms: 2, housekeepers: 1, receptionists: 1);
            tiny.BeginDay();

            Assert.That(tiny.BookingsDeclinedToday, Is.GreaterThan(0),
                        "两间房撑不住十四天的需求，多出来的单必须当场谈崩（晨报据此提示该开房了）");

            // 任何一天的需求都不能超过容量（没开故意超售档）
            for (int d = tiny.Clock.CurrentDay; d < tiny.Clock.CurrentDay + BookingGenerator.HorizonDays; d++)
                Assert.That(tiny.Calendar.RemainingOn(d, RoomTier.Old), Is.GreaterThanOrEqualTo(0),
                            $"第 {d} 天不该在没开故意超售的情况下超卖");
        }

        [Test]
        public void DeliberateOverbooking_LetsTheBookDrawnPastCapacity()
        {
            // 房量要小到需求真的顶到容量上限：需求本身与房量成正比（DemandModel），
            // 房间一多就永远不满，这条断言也就无从成立（4 间房实测拒单为 0）。
            var careful = BuildHotel(rooms: 2, seed: 777);
            var gambler = BuildHotel(rooms: 2, seed: 777);
            gambler.OverbookingAllowance = 3;

            careful.BeginDay();
            gambler.BeginDay();

            Assert.That(careful.BookingsDeclinedToday, Is.GreaterThan(0),
                        "两间房必须先真的接不下单，否则这条对照没有意义");
            Assert.That(gambler.BookingsDeclinedToday, Is.LessThan(careful.BookingsDeclinedToday),
                        "开了故意超售档就能多接单——赌的是取消率");
            Assert.That(gambler.Bookings.Count, Is.GreaterThan(careful.Bookings.Count),
                        "多接下来的单要真的进簿子");
        }

        // ── 抽成按渠道走 ──────────────────────────────────────────────────────

        [Test]
        public void CommissionFollowsTheChannel_DirectBookingsCostNothing()
        {
            // 两家同种子的酒店各塞一张同价同档的单，只差渠道：
            // 自动生成的单在两边完全一致，所以佣金之差就是这一张单的抽成。
            var viaPlatform = BuildHotel(rooms: 10, seed: 24680);
            var viaDirect = BuildHotel(rooms: 10, seed: 24680);

            viaPlatform.Bookings.Add(BookingChannels.PlatformAId, arrivalDay: 1, nights: 1,
                                     RoomTier.Old, 80, GuestSegment.Budget, bookedOnDay: 1);
            viaDirect.Bookings.Add(BookingChannels.DirectId, arrivalDay: 1, nights: 1,
                                   RoomTier.Old, 80, GuestSegment.Budget, bookedOnDay: 1);

            // 第一天住进来，第二天早上退房把房费与佣金入账
            foreach (var sim in new[] { viaPlatform, viaDirect })
            {
                sim.BeginDay(); sim.RunToEndOfDay(); sim.SettleDay(); sim.Clock.BeginNextDay();
                sim.BeginDay();
            }

            Assume.That(viaDirect.GrossIncomeToday, Is.GreaterThan(0), "住了一晚就该有房费");

            Assert.That(viaPlatform.CommissionToday, Is.GreaterThan(viaDirect.CommissionToday),
                        "同一张单走平台要被抽成，走直营不用——把客人养成回头客的回报");
        }

        // ── no-show：人没来，库存白占 ─────────────────────────────────────────

        [Test]
        public void GuestsWhoNeverShowUp_AreMarkedNoShow_NotLeftDangling()
        {
            var sim = BuildHotel(rooms: 4, receptionists: 0);   // 前台没人 = 队伍一动不动
            sim.BeginDay();
            int booked = sim.ReservationArrivalsToday;
            Assume.That(booked, Is.GreaterThan(0));

            sim.RunToEndOfDay();
            Assume.That(sim.ArrivalsCheckedInToday, Is.EqualTo(0), "前台空岗，没人办得进去");

            sim.SettleDay();

            Assert.That(sim.NoShowsToday, Is.EqualTo(booked),
                        "打烊时还没进来的今日预订必须落到 no-show，不能挂在 Booked 上永远占库存");
            Assert.That(sim.Bookings.ArrivalsFor(sim.Clock.CurrentDay), Is.Empty,
                        "结算后今天不该再剩下待到店的单");
        }

        // 视野边缘的回归测试：容量表必须比预订视野多铺 MaxNights 天。
        // 少铺的话，视野最后一天的连住单会伸到"容量从未设置"（=0）的日子上，
        // CanAccept 于是把**所有连住单**都拒掉 —— 簿子一天天变薄、客人只剩单晚，
        // 而且没有任何报错。M-D 实测：活单从 131 一路掉到 71。
        [Test]
        public void CapacityIsSetPastTheHorizon_SoEdgeMultiNightBookingsCanBeAccepted()
        {
            var sim = BuildHotel(rooms: 30);
            sim.BeginDay();

            int lastHorizonDay = sim.Clock.CurrentDay + BookingGenerator.HorizonDays - 1;
            for (int d = lastHorizonDay; d < lastHorizonDay + BookingGenerator.MaxNights; d++)
                Assert.That(sim.Calendar.CapacityOn(d, RoomTier.Old), Is.GreaterThan(0),
                            $"第 {d} 天要有容量，否则视野末尾的连住单永远接不下来");

            Assert.That(sim.Calendar.CanAccept(lastHorizonDay, BookingGenerator.MaxNights, RoomTier.Old),
                        Is.True, "视野最后一天的最长连住单必须能接");
        }

        [Test]
        public void TheBookDoesNotSlowlyStarve_OverAMonth()
        {
            var sim = BuildHotel(rooms: 30);
            sim.BeginDay();
            int liveEarly = CountLiveFuture(sim);
            sim.RunToEndOfDay(); sim.SettleDay(); sim.Clock.BeginNextDay();

            for (int d = 0; d < 30; d++) RunOneDay(sim);
            sim.BeginDay();
            int liveLate = CountLiveFuture(sim);

            // 每晨有单进来、有单到店，稳态下未来活单量该在同一量级上下浮动。
            // 掉到一半以下说明某处在静默吞单（视野边缘的连住单就是这么消失的）。
            Assert.That(liveLate, Is.GreaterThan(liveEarly / 2),
                        $"一个月后未来活单从 {liveEarly} 掉到 {liveLate}——有地方在静默吞订单");
        }

        private static int CountLiveFuture(HotelSim sim)
        {
            int n = 0;
            foreach (var r in sim.Bookings.All)
                if (r.state == ReservationState.Booked && r.arrivalDay > sim.Clock.CurrentDay) n++;
            return n;
        }

        // 每一位排定的到店客都必须有下场：入住 / 被拒 / 落成超售待处置。
        // 少了容差的话，阶段权重之和在浮点下凑不满 1.0，**当天最后一位客人被静默吞掉**
        // ——既没入住也没被拒，只在日结时莫名变成 no-show。
        [Test]
        public void EveryPlannedArrival_HasAnOutcome_NobodyIsSilentlyDropped()
        {
            foreach (int rooms in new[] { 1, 2, 3, 5, 8, 13 })
            {
                var sim = BuildHotel(rooms: rooms, housekeepers: 2, receptionists: 2, seed: 606 + rooms);
                sim.BeginDay();
                int planned = sim.ArrivalsPlannedToday;
                sim.RunToEndOfDay();

                int accounted = sim.ArrivalsCheckedInToday
                              + sim.ArrivalsTurnedAwayToday
                              + sim.PendingOverbookings.Count;

                Assert.That(accounted, Is.EqualTo(planned),
                            $"{rooms} 间房：排定 {planned} 位，只交代了 {accounted} 位——有人被静默吞掉了");
            }
        }

        // ── 长跑不发散 ────────────────────────────────────────────────────────

        [Test]
        public void ThirtyDays_DoNotLeakReservationsOrRoomNights()
        {
            var sim = BuildHotel(rooms: 12, furnish: true);
            for (int d = 0; d < 30; d++) RunOneDay(sim);

            // 簿子不会无限长：了结的单会被归档
            Assert.That(sim.Bookings.Count, Is.LessThan(30 * 30),
                        "已了结的单要归档，不能一路堆到天荒地老");

            // 在住的单必须与 Occupied 的房一一对应
            int occupied = sim.Rooms.CountOf(RoomSimState.Occupied);
            Assert.That(sim.Bookings.InHouse().Count, Is.LessThanOrEqualTo(occupied),
                        "在住单数不能超过在住房数（walk-in 没有单，所以是 ≤）");

            // 过去的日子要从日历里清掉
            Assert.That(sim.Calendar.DemandOn(1, RoomTier.Old), Is.EqualTo(0),
                        "第 1 天早就过去了，不该还占着内存与账面");
        }
    }
}
