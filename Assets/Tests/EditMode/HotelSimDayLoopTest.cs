using System.Collections.Generic;
using NUnit.Framework;

// 完整日循环 + 压力网耦合验收（架构 B.1 的验收方式）。
// 断言用**同种子多日累计量对比**，不断言逐日单调——随机需求下个别天会反弹，
// 单调断言必然闪断（修订版 3 的修正）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class HotelSimDayLoopTest
    {
        private const int Seed = 90210;

        private static HotelSim BuildHotel(int rooms = 12, int housekeepers = 2, int inspectors = 1,
                                           int startingCash = 2000, int seed = Seed)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            for (int i = 0; i < housekeepers; i++)
                staff.Register(new StaffMember(StaffRole.Housekeeper, "HSK" + i, 60,
                                               new StaffAttributes(55, 55, 55), 1, null));
            for (int i = 0; i < inspectors; i++)
                staff.Register(new StaffMember(StaffRole.Inspector, "INSP" + i, 70,
                                               new StaffAttributes(55, 55, 55), 1, null));

            return new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                DemandConfig.Default, startingCash, seed);
        }

        private static DaySettlementResult RunOneDay(HotelSim sim, int interest = 0)
        {
            sim.BeginDay();
            sim.RunToEndOfDay();
            var result = sim.SettleDay(interest: interest);
            sim.Clock.BeginNextDay();
            return result;
        }

        // ── 日循环闭环 ───────────────────────────────────────────────────────

        [Test]
        public void FirstDay_GuestsCheckInAndRoomsFill()
        {
            var sim = BuildHotel();
            sim.BeginDay();

            Assert.That(sim.ArrivalsPlannedToday, Is.GreaterThan(0), "开门就该排出今日客量");
            Assert.That(sim.Rooms.CountOf(RoomSimState.Occupied), Is.EqualTo(0), "还没到入住时间");

            sim.RunToEndOfDay();

            Assert.That(sim.ArrivalsCheckedInToday, Is.GreaterThan(0), "入住高峰过后该有人住进来");
            Assert.That(sim.Rooms.CountOf(RoomSimState.Occupied), Is.EqualTo(sim.ArrivalsCheckedInToday));
            Assert.That(sim.GrossIncomeToday, Is.EqualTo(0), "房费在次日退房时才结算（过夜模型）");
        }

        [Test]
        public void SecondDay_CheckoutWaveBooksRevenueAndDirtiesRooms()
        {
            var sim = BuildHotel();
            RunOneDay(sim);
            int stayed = sim.Rooms.CountOf(RoomSimState.Occupied);
            Assert.That(stayed, Is.GreaterThan(0), "第一晚要有人住着");

            sim.BeginDay(); // 第二天晨间退房潮

            Assert.That(sim.CheckoutsToday, Is.EqualTo(stayed));
            Assert.That(sim.GrossIncomeToday, Is.GreaterThan(0), "退房结算房费");
            Assert.That(sim.Rooms.CountOf(RoomSimState.Dirty), Is.GreaterThanOrEqualTo(stayed),
                        "退房后房间变脏，要打扫");
            Assert.That(sim.Rooms.CountOf(RoomSimState.Occupied), Is.EqualTo(0));
        }

        [Test]
        public void Income_ReachesSafeboxNotCashDirectly()
        {
            var sim = BuildHotel(startingCash: 1000);
            RunOneDay(sim);          // 第一晚入住
            int cashBefore = sim.Cash;
            RunOneDay(sim);          // 第二天退房 + 日结

            Assert.That(sim.Safebox.Balance, Is.GreaterThan(0), "净利进保险箱");
            Assert.That(sim.Cash, Is.EqualTo(cashBefore), "不收就不进现金——这就是回流钩子");

            int collected = sim.CollectSafebox();
            Assert.That(collected, Is.GreaterThan(0));
            Assert.That(sim.Cash, Is.EqualTo(cashBefore + collected));
            Assert.That(sim.Safebox.Balance, Is.EqualTo(0));
        }

        [Test]
        public void TrySpendCash_OnlySpendsWhatWasCollected()
        {
            var sim = BuildHotel(startingCash: 100);
            RunOneDay(sim);
            RunOneDay(sim);

            Assert.That(sim.TrySpendCash(5000), Is.False, "保险箱里的钱不能直接花");
            sim.CollectSafebox();
            Assert.That(sim.TrySpendCash(100), Is.True, "收了才能花");
        }

        [Test]
        public void SevenDays_RunWithoutDrift()
        {
            var sim = BuildHotel();
            for (int day = 1; day <= 7; day++)
            {
                var result = RunOneDay(sim, interest: 20);
                Assert.That(result.NetProfit, Is.Not.EqualTo(int.MinValue));
                Assert.That(sim.Rooms.Count, Is.EqualTo(12), "第 " + day + " 天房间数不变");
                Assert.That(sim.Cash, Is.GreaterThanOrEqualTo(0), "现金不为负");
            }
            Assert.That(sim.Clock.CurrentDay, Is.EqualTo(8));
            Assert.That(sim.Reputation.ExportSamples().Count, Is.GreaterThan(0), "评分样本在累积");
        }

        // ── 压力网：排班精简的代价（B.1 验收） ────────────────────────────────

        [Test]
        public void LeanStaffing_CostsSellableNightsBacklogAndProfit_OverFiveDays()
        {
            // 40 间房：一个管家一班约清 19 间，而日均到店约 26 间——人手真正稀缺，
            // 排班档位才有意义。12 间的小店一个人就够用，那是设计使然不是 bug。
            var baseline = BuildHotel(rooms: 40, housekeepers: 3, inspectors: 1);
            var lean = BuildHotel(rooms: 40, housekeepers: 3, inspectors: 1);
            baseline.Shifts.SetTier(StaffRole.Housekeeper, ShiftTier.Full);
            lean.Shifts.SetTier(StaffRole.Housekeeper, ShiftTier.Skeleton);

            int baseNights = 0, leanNights = 0;
            int baseBacklog = 0, leanBacklog = 0;
            int baseRevenue = 0, leanRevenue = 0;

            for (int day = 1; day <= 5; day++)
            {
                RunOneDay(baseline);
                RunOneDay(lean);
                baseNights += baseline.ArrivalsCheckedInToday;
                leanNights += lean.ArrivalsCheckedInToday;
                baseBacklog += baseline.Rooms.DirtyBacklog;
                leanBacklog += lean.Rooms.DirtyBacklog;
                baseRevenue += baseline.GrossIncomeToday;
                leanRevenue += lean.GrossIncomeToday;
            }

            // 结构上必然成立的三条（模型保证，与调参无关）
            Assert.That(leanNights, Is.LessThan(baseNights), "精简排班 → 五日累计入住间夜更少");
            Assert.That(leanBacklog, Is.GreaterThan(baseBacklog), "精简排班 → 脏房积压更多");
            Assert.That(leanRevenue, Is.LessThan(baseRevenue), "少住几间 → 毛收入更低");

            // 注意：此处**不断言净利更低**。当前调参下精简排班省的工资多于损失的房费，
            // 因为 M-B 里排班不足的唯一代价就是"少住几间"——脏房积压还没有满意度惩罚
            // （分到脏房/前台排队的扣分要 M-C 才接上），声誉螺旋也来不及在 5 天内咬人。
            // 这是记录在案的平衡缺口，M-C 好玩验证门调参时处理，不是模型缺陷。
        }

        [Test]
        public void LeanStaffing_TurnsGuestsAway_BecauseRoomsAreNotReady()
        {
            var lean = BuildHotel(rooms: 40, housekeepers: 3, inspectors: 0);
            lean.Shifts.SetTier(StaffRole.Housekeeper, ShiftTier.Skeleton);

            int turnedAway = 0;
            for (int day = 1; day <= 6; day++) // 脏房每天净增，要几天才积压到卡住入住
            {
                RunOneDay(lean);
                turnedAway += lean.ArrivalsTurnedAwayToday;
            }

            Assert.That(turnedAway, Is.GreaterThan(0),
                        "房间来不及打扫 → 到店的人住不进去（客流压力与服务压力咬合的可观测证据）");
        }

        // ── 压力网：定价策略的代价 ────────────────────────────────────────────

        [Test]
        public void ClearancePricing_FillsMoreRoomsThanSqueeze_OverFiveDays()
        {
            var cheap = BuildHotel();
            var pricey = BuildHotel();
            cheap.Pricing.DefaultTemplate = PriceTemplate.Clearance;
            pricey.Pricing.DefaultTemplate = PriceTemplate.Squeeze;

            int cheapCheckIns = 0, priceyCheckIns = 0;
            for (int day = 1; day <= 5; day++)
            {
                RunOneDay(cheap);
                RunOneDay(pricey);
                cheapCheckIns += cheap.ArrivalsCheckedInToday;
                priceyCheckIns += pricey.ArrivalsCheckedInToday;
            }

            Assert.That(cheapCheckIns, Is.GreaterThan(priceyCheckIns),
                        "改模板 → 入住率按弹性变化（M-B 验收点）");
        }

        [Test]
        public void SqueezePricing_HurtsSatisfaction_ViaExpectationPenalty()
        {
            var market = BuildHotel();
            var squeeze = BuildHotel();
            squeeze.Pricing.DefaultTemplate = PriceTemplate.Squeeze;

            for (int day = 1; day <= 5; day++)
            {
                RunOneDay(market);
                RunOneDay(squeeze);
            }

            Assert.That(squeeze.Reputation.AverageSatisfaction,
                        Is.LessThan(market.Reputation.AverageSatisfaction),
                        "榨利润抬高客人期待 → 满意度更低（价高的代价）");
        }

        // ── 亏损日与欠薪链 ───────────────────────────────────────────────────

        [Test]
        public void CrushingInterest_DrainsCashThenMissesPayroll()
        {
            var sim = BuildHotel(startingCash: 50);
            RunOneDay(sim); // 先住进人，第二天才有收入

            var result = RunOneDay(sim, interest: 5000); // 荒唐的利息制造真实亏损

            Assert.That(result.wagesPaid, Is.False, "真实经营亏损 + 现金见底 → 欠薪");
            Assert.That(result.unpaidAmount, Is.GreaterThan(0));
            Assert.That(sim.Cash, Is.EqualTo(0), "现金榨干但不为负");

            float moraleAfter = sim.Staff.AverageMorale;
            Assert.That(moraleAfter, Is.LessThan(StaffMember.DefaultMorale), "欠薪打击士气");
        }

        [Test]
        public void FullSafebox_StillPaysWages()
        {
            // 回归：钱堆在保险箱里绝不能造成欠薪（修订版 3 推翻的旧设计）。
            // 给一点开局现金：第一天没有隔夜客可退房，零收入零现金本就发不出工资，
            // 那是真实亏损而不是保险箱造成的。
            var sim = BuildHotel(startingCash: 500);
            for (int day = 1; day <= 6; day++)
            {
                var result = RunOneDay(sim);
                Assert.That(result.wagesPaid, Is.True,
                            "第 " + day + " 天：只要当日收入够付固定成本就不欠薪，与箱子满不满无关");
            }
            Assert.That(sim.Safebox.Balance + sim.Overflow.Balance, Is.GreaterThan(0));
        }

        // ── 确定性 ───────────────────────────────────────────────────────────

        [Test]
        public void SameSeed_ReproducesWholeWeekExactly()
        {
            var a = BuildHotel(seed: 777);
            var b = BuildHotel(seed: 777);

            for (int day = 1; day <= 5; day++)
            {
                var ra = RunOneDay(a);
                var rb = RunOneDay(b);
                Assert.That(ra.netToSafebox, Is.EqualTo(rb.netToSafebox), "第 " + day + " 天入箱额一致");
                Assert.That(a.ArrivalsCheckedInToday, Is.EqualTo(b.ArrivalsCheckedInToday));
                Assert.That(a.Rooms.DirtyBacklog, Is.EqualTo(b.Rooms.DirtyBacklog));
            }
            Assert.That(a.Cash, Is.EqualTo(b.Cash));
            Assert.That(a.Safebox.Balance, Is.EqualTo(b.Safebox.Balance));
        }
    }
}
