using System.Collections.Generic;
using NUnit.Framework;

// 入住情况看板（玩家要求："今天10间房可以卖，卖了6间，要显示成 6/10"，
// 以及"未来给我搞成一个日历UI，每天都是已售/全部"）。
//
// **这一批测试的来历**：同一个语义已经把玩家坑过两次
// （"UI 看是可售有几个，但是结算的时候说没房可卖"）。分子分母各算一遍
// 迟早对不上账，所以这里逐条钉死"那两个数字到底数的是什么"，
// 尤其是上门客、连住客、装修封房、破败房、分档超售这五种会让直觉出错的情形。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class OccupancyBoardTest
    {
        private static HotelSim BuildHotel(int rooms = 10, int seed = 31337,
                                           RoomTier tier = RoomTier.Old)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            tier, RoomSimState.Ready));

            var staff = new StaffRoster();
            for (int i = 0; i < 2; i++)
                staff.Register(new StaffMember(StaffRole.Reception, "R" + i, 65, new StaffAttributes(55, 55, 55), 1, null));
            for (int i = 0; i < 3; i++)
                staff.Register(new StaffMember(StaffRole.Housekeeper, "H" + i, 60, new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, seed);
            sim.FurnishInheritedRooms();
            return sim;
        }

        /// <summary>今天日历上三档的需求之和——"天真做法"，用来证明它漏了上门客。</summary>
        private static int NaiveCalendarSold(HotelSim sim, int day) =>
            sim.Calendar.DemandOn(day, RoomTier.Old)
          + sim.Calendar.DemandOn(day, RoomTier.Basic)
          + sim.Calendar.DemandOn(day, RoomTier.Better);

        // ── 分子：今晚数的是"今晚有人睡的房间" ────────────────────────────────

        [Test]
        public void TonightsNumeratorIsRoomsInUse_PlusGuestsStillOnTheirWayIn()
        {
            // 这是今晚那个分子的**定义**。在住 + 今天还没到店的预订，
            // 两边不重叠（check-in 把单从 Booked 翻成 CheckedIn，ArrivalsFor 只收 Booked）
            var sim = BuildHotel();
            sim.BeginDay();
            sim.RunToEndOfDay();

            var tonight = sim.OccupancyForNight(sim.Clock.CurrentDay);
            Assert.That(tonight.sold,
                        Is.EqualTo(sim.ActiveStayCount + sim.Bookings.ArrivalsFor(sim.Clock.CurrentDay).Count));
        }

        [Test]
        public void AGuestWhoAlreadyCheckedInIsNotCountedTwice()
        {
            // 要是 ArrivalsFor 也收 CheckedIn，入住一个人分子会跳 2，
            // 玩家会看到 12/10 这种鬼数字
            var sim = BuildHotel();
            sim.BeginDay();
            sim.RunToEndOfDay();

            Assert.That(sim.ActiveStayCount, Is.GreaterThan(0), "这一天得真有人住进来，否则这条什么也没验到");
            var tonight = sim.OccupancyForNight(sim.Clock.CurrentDay);
            Assert.That(tonight.sold, Is.LessThanOrEqualTo(sim.Rooms.Count),
                        "今晚已售不可能超过酒店总房数");
        }

        [Test]
        public void TonightCountsWalkIns_WhichTheBookingCalendarNeverSees()
        {
            // **这条是整个设计的理由**：上门客走 ticket 0，从不进预订簿，
            // 所以日历需求根本看不见他们。今晚的分子若图省事用日历需求，
            // 一个住满的酒店会显示成半空。
            //
            // 掷骰的事不赌单个种子：跑一串固定种子，只要有上门客住进来的那些天，
            // 每一天都必须比"天真做法"多算出人来。
            int daysWithWalkIns = 0;
            foreach (int seed in new[] { 11, 137, 4242, 90210, 31337 })
            {
                var sim = BuildHotel(seed: seed);
                sim.BeginDay();
                sim.RunToEndOfDay();

                int walkInsInHouse = sim.ActiveStayCount - sim.Bookings.InHouse().Count;
                if (walkInsInHouse <= 0) continue;

                daysWithWalkIns++;
                var tonight = sim.OccupancyForNight(sim.Clock.CurrentDay);
                Assert.That(tonight.sold, Is.GreaterThan(NaiveCalendarSold(sim, sim.Clock.CurrentDay)),
                            "种子 " + seed + "：有 " + walkInsInHouse
                            + " 个上门客住着，分子却没把他们算进去");
            }

            Assert.That(daysWithWalkIns, Is.GreaterThan(0),
                        "这批种子里一个上门客都没有，这条测试就什么也没验到——换种子");
        }

        // ── 分子：未来数的是间夜，连住客要占满他住的每一晚 ────────────────────

        [Test]
        public void AMultiNightStayStillOccupiesTheNightsAheadOfIt()
        {
            // 连住客已经 CheckedIn 了，但他明晚后晚照样占着房。
            // RebuildCalendarDemand 从 IsLive（Booked||CheckedIn）重建正是为了这个——
            // 只算 Booked 的话明天那一格会凭空空出来，玩家照着超售
            var sim = BuildHotel(rooms: 6);
            sim.Calendar.SetCapacity(5, RoomTier.Old, 6);
            sim.Calendar.Reserve(arrivalDay: 5, nights: 3, RoomTier.Old);

            Assert.That(sim.OccupancyForNight(5).sold, Is.GreaterThan(0));
            Assert.That(sim.OccupancyForNight(6).sold, Is.GreaterThan(0), "第二晚也该被占着");
            Assert.That(sim.OccupancyForNight(7).sold, Is.GreaterThan(0), "第三晚也该被占着");
        }

        [Test]
        public void FutureNightsReadTheirSoldCountStraightFromTheCalendar()
        {
            var sim = BuildHotel();
            sim.BeginDay();

            int day = sim.Clock.CurrentDay + 3;
            Assert.That(sim.OccupancyForNight(day).sold, Is.EqualTo(NaiveCalendarSold(sim, day)),
                        "未来那一格就是日历需求——上门客还没发生，不该凭空补人");
        }

        [Test]
        public void OnlyTonightIsFlaggedAsTonight()
        {
            // UI 靠这个字段换措辞。要是每格都是 true，两种口径混在一张表里没人看得懂
            var sim = BuildHotel();
            sim.BeginDay();

            var calendar = sim.OccupancyCalendar(8);
            Assert.That(calendar[0].isTonight, Is.True);
            Assert.That(calendar[0].day, Is.EqualTo(sim.Clock.CurrentDay));
            for (int i = 1; i < calendar.Count; i++)
                Assert.That(calendar[i].isTonight, Is.False, "第 " + i + " 格不是今晚");
        }

        // ── 分母：库存容量，且一天之内不许自己动 ──────────────────────────────

        [Test]
        public void TheDenominatorIsEveryRoomThatIsNotDerelict()
        {
            var sim = BuildHotel(rooms: 10);
            sim.BeginDay();
            Assert.That(sim.OccupancyForNight(sim.Clock.CurrentDay).capacity, Is.EqualTo(10));
        }

        [Test]
        public void DerelictRoomsAreNotPartOfTheDenominator()
        {
            // 破败房还没解锁，把它算进分母会让玩家以为自己有房卖
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < 10; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old,
                                            i < 4 ? RoomSimState.Ruined : RoomSimState.Ready));
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, 7);
            sim.BeginDay();

            Assert.That(sim.OccupancyForNight(sim.Clock.CurrentDay).capacity, Is.EqualTo(6),
                        "10 间房里 4 间破败 ⇒ 分母是 6");
        }

        [Test]
        public void ARoomUnderRenovationLeavesTheDenominatorAndComesBackWhenItIsDone()
        {
            var sim = BuildHotel(rooms: 10);
            sim.BeginDay();
            int today = sim.Clock.CurrentDay;
            int before = sim.OccupancyForNight(today).capacity;

            // 装修吃材料，开局仓库是空的。忘了这一步的话 Assume 会把这条测试
            // 判成 Inconclusive——那是**静默空转**，和假绿灯一样骗人
            Assert.That(sim.TryBuyMaterials(10), Is.True, "先得买到材料");
            Assert.That(sim.TryStartRenovation(RenovationPlanKind.Economy,
                                               new List<int> { 201 }, out string reason), Is.True, reason);
            int blockDays = sim.Renovations.DaysRemainingFor(201);
            Assert.That(blockDays, Is.GreaterThan(0), "装修得真的封房几天");

            Assert.That(sim.OccupancyForNight(today).capacity, Is.EqualTo(before - 1),
                        "在装修的房今天不能卖");
            Assert.That(sim.OccupancyForNight(today + blockDays).capacity, Is.EqualTo(before),
                        "完工那天要回到库存");
        }

        /// <summary>库存必须等于"照当前房态重数一遍"的结果。
        /// 容量是幂等纯函数，所以任何时候重算都该得到同一个数——
        /// 数不上就说明有人改了"哪些房能卖"却没重算。</summary>
        private static void AssertInventoryIsNotStale(HotelSim sim, string when)
        {
            var before = new Dictionary<int, int>();
            for (int offset = 0; offset < BookingGenerator.HorizonDays; offset++)
            {
                int day = sim.Clock.CurrentDay + offset;
                before[day] = sim.OccupancyForNight(day).capacity;
            }

            sim.RefreshCapacityForHorizon();

            foreach (var pair in before)
                Assert.That(sim.OccupancyForNight(pair.Key).capacity, Is.EqualTo(pair.Value),
                            when + "：第 " + pair.Key + " 晚的库存是旧的"
                            + "——有人改了『哪些房能卖』却没重算容量");
        }

        [Test]
        public void InventoryIsNeverStale_AfterAMidDayRenovation()
        {
            // **这条是给未来的自己设的闸门**。容量原本只在每天早晨算一次，
            // 于是玩家中午点了装修，前台整天还在按老容量接单、界面分母也是旧的。
            // 以后谁再加一条"改变可售房"的路径而忘了重算，这里会当场抓住。
            var sim = BuildHotel(rooms: 10);
            sim.BeginDay();
            Assert.That(sim.TryBuyMaterials(20), Is.True);
            AssertInventoryIsNotStale(sim, "开门时");

            Assert.That(sim.TryStartRenovation(RenovationPlanKind.Economy,
                                               new List<int> { 201 }, out string why), Is.True, why);
            AssertInventoryIsNotStale(sim, "中途开工装修之后");

            sim.RunToEndOfDay();
            AssertInventoryIsNotStale(sim, "跑完一整天之后");

            sim.SettleDay();
            sim.Clock.BeginNextDay();
            sim.BeginDay();
            AssertInventoryIsNotStale(sim, "第二天早晨");
        }

        [Test]
        public void ClearingADerelictRoomPutsItBackIntoTonightsInventoryAtOnce()
        {
            // **只处理"减少"不处理"增加"是同一个 bug 的另一半**：玩家花了几趟
            // 人工把破败房清出来，如果容量不重算，今晚那间房还是卖不出去，
            // 分母也不动——玩家会以为清理白干了
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < 6; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old,
                                            i == 0 ? RoomSimState.Ruined : RoomSimState.Ready));
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, 21);
            sim.BeginDay();

            int before = sim.OccupancyForNight(sim.Clock.CurrentDay).capacity;
            Assert.That(before, Is.EqualTo(5), "6 间里 1 间破败");

            Assert.That(sim.TryStartJunkClearing(201, out string reason), Is.True, reason);
            // 一趟一趟地清，直到这间房出来（一趟 WorkUnitsPerVisit，约四趟清完）
            int trips = JunkClearingModel.VisitsForOneRoom + 2;
            bool done = false;
            for (int trip = 0; trip < trips && !done; trip++)
                done = sim.ApplyJunkClearingVisit(201, JunkClearingModel.WorkUnitsPerVisit);
            Assert.That(done, Is.True, "清了 " + trips + " 趟还没清完？");

            Assert.That(sim.OccupancyForNight(sim.Clock.CurrentDay).capacity, Is.EqualTo(before + 1),
                        "清出来的房当晚就该进库存");
            AssertInventoryIsNotStale(sim, "清完破败房之后");
        }

        [Test]
        public void TheDenominatorDoesNotWobbleWhileTheDayRuns()
        {
            // 刻意不用"此刻 Ready 的间数"当分母：那个数会随打扫/入住上下跳，
            // 玩家会看到分母自己在动——比没有这个数字更糟
            var sim = BuildHotel(rooms: 10);
            sim.BeginDay();
            int today = sim.Clock.CurrentDay;
            int atOpening = sim.OccupancyForNight(today).capacity;

            sim.RunToEndOfDay();
            Assert.That(sim.OccupancyForNight(today).capacity, Is.EqualTo(atOpening),
                        "住满一天之后分母不该变");
        }

        // ── 满房：必须和拒单提示同一个源头 ────────────────────────────────────

        [Test]
        public void SoldOutAgreesWithTheNightsTheBookingDeskIsRefusing()
        {
            // 上一轮玩家的困惑就是两个数字各算一遍。这条把它们钉在一起
            var sim = BuildHotel(rooms: 4);
            sim.BeginDay();

            var refused = new HashSet<int>(sim.FullyBookedNights());
            foreach (var night in sim.OccupancyCalendar(8))
                Assert.That(night.soldOut, Is.EqualTo(refused.Contains(night.day)),
                            "第 " + night.day + " 晚：日历说 soldOut=" + night.soldOut
                            + "，拒单名单说 " + refused.Contains(night.day));
        }

        [Test]
        public void ABandStillHavingRoomMeansNotSoldOut_EvenWhenTheTotalsLookFull()
        {
            // **`sold >= capacity` 不能当满房判据**：三个档位各自一份库存。
            // Old 超售 2 间、Better 空着 2 间时，总数看着刚好满，其实还能卖 Better。
            // 这一格要是标成"订满"，玩家会以为自己卖不出去而白白空着房。
            var defs = new List<RoomDefinition>
            {
                new RoomDefinition(201, 1, 1, Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready),
                new RoomDefinition(202, 1, 1, Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready),
                new RoomDefinition(203, 1, 1, Room2DRoomCategory.Single, RoomTier.Better, RoomSimState.Ready),
                new RoomDefinition(204, 1, 1, Room2DRoomCategory.Single, RoomTier.Better, RoomSimState.Ready),
            };
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, 99);

            const int day = 9;
            sim.Calendar.SetCapacity(day, RoomTier.Old, 2);
            sim.Calendar.SetCapacity(day, RoomTier.Better, 2);
            for (int i = 0; i < 4; i++) sim.Calendar.Reserve(day, 1, RoomTier.Old);   // Old 卖了 4/2

            var night = sim.OccupancyForNight(day);
            Assert.That(night.sold, Is.EqualTo(4));
            Assert.That(night.capacity, Is.EqualTo(4), "总数刚好'满'——正是最容易判错的那一格");
            Assert.That(night.soldOut, Is.False, "Better 还空着两间，这一晚没订满");
        }

        [Test]
        public void OversellingIsReportedInsteadOfShowingANegativeVacancy()
        {
            var sim = BuildHotel(rooms: 4);
            const int day = 9;
            sim.Calendar.SetCapacity(day, RoomTier.Old, 4);
            for (int i = 0; i < 6; i++) sim.Calendar.Reserve(day, 1, RoomTier.Old);

            var night = sim.OccupancyForNight(day);
            Assert.That(night.Oversold, Is.EqualTo(2), "超售 2 间要说出来，那是要处置的事件");
            Assert.That(night.Left, Is.EqualTo(0), "剩余不返回负数——负数要玩家自己换算");
            Assert.That(night.soldOut, Is.True);
        }

        // ── 显示形状与边界 ────────────────────────────────────────────────────

        [Test]
        public void TheFractionReadsTheWayThePlayerAskedFor()
        {
            // 玩家原话："卖了6间，要显示成 6/10"
            Assert.That(new NightOccupancy(1, 6, 10, true, false).Fraction, Is.EqualTo("6/10"));
        }

        [Test]
        public void AnEmptyHotelNeverDividesByZero()
        {
            var night = new NightOccupancy(1, 0, 0, true, false);
            Assert.That(night.Rate, Is.EqualTo(0f));
            Assert.That(night.Left, Is.EqualTo(0));
            Assert.That(night.IsFull, Is.False, "0/0 不是订满，是一间房都没有");
            Assert.That(OccupancyBoard.PercentBookedAcross(new List<NightOccupancy> { night }),
                        Is.EqualTo(0));
        }

        [Test]
        public void TheCalendarNeverRunsPastTheBookingHorizon()
        {
            // 视野之外还没铺容量，画出来是假的 0/0——玩家会以为酒店没了
            var sim = BuildHotel();
            sim.BeginDay();

            var far = sim.OccupancyCalendar(BookingGenerator.HorizonDays + 20);
            Assert.That(far.Count, Is.EqualTo(BookingGenerator.HorizonDays));
            foreach (var night in far)
                Assert.That(night.capacity, Is.GreaterThan(0), "视野内每一晚都该有库存");
        }

        [Test]
        public void SummarisingNothingIsSafe()
        {
            // 顶栏第一帧可能在 BeginDay 之前跑到，日历会是空的
            Assert.That(OccupancyBoard.PercentBookedAcross(null), Is.EqualTo(0));
            Assert.That(OccupancyBoard.PercentBookedAcross(new List<NightOccupancy>()), Is.EqualTo(0));
            Assert.That(OccupancyBoard.FullNightsAcross(null), Is.EqualTo(0));
        }

        [Test]
        public void TheWeekSummaryCountsSoldAndCapacityAcrossEveryNight()
        {
            var nights = new List<NightOccupancy>
            {
                new NightOccupancy(1, 5, 10, true, false),
                new NightOccupancy(2, 10, 10, false, true),
                new NightOccupancy(3, 0, 10, false, false),
            };
            Assert.That(OccupancyBoard.PercentBookedAcross(nights), Is.EqualTo(50), "15/30");
            Assert.That(OccupancyBoard.FullNightsAcross(nights), Is.EqualTo(1));
        }

        [Test]
        public void TheDefaultCalendarIsAWeekPlusTonight()
        {
            var sim = BuildHotel();
            sim.BeginDay();
            Assert.That(sim.OccupancyCalendar().Count, Is.EqualTo(OccupancyBoard.DefaultDays));
        }
    }
}
