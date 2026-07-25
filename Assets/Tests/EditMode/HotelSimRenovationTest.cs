using System.Collections.Generic;
using NUnit.Framework;

// M-C 验收：装修接入日循环（投入 → Block → 完工档位提升 → 房价/需求上涨），
// 以及补齐的两条压力惩罚（前台排队、无验房的瑕疵房）——它们要让"精简排班"不再净赚。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class HotelSimRenovationTest
    {
        private static HotelSim BuildHotel(int rooms = 12, int housekeepers = 2, int inspectors = 1,
                                           int receptionists = 2, int startingCash = 20000, int seed = 31337)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, 1, 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            for (int i = 0; i < housekeepers; i++)
                staff.Register(new StaffMember(StaffRole.Housekeeper, "HSK" + i, 60,
                                               new StaffAttributes(55, 55, 55), 1, null));
            for (int i = 0; i < inspectors; i++)
                staff.Register(new StaffMember(StaffRole.Inspector, "INSP" + i, 70,
                                               new StaffAttributes(55, 55, 55), 1, null));
            for (int i = 0; i < receptionists; i++)
                staff.Register(new StaffMember(StaffRole.Reception, "RCP" + i, 65,
                                               new StaffAttributes(55, 55, 55), 1, null));

            return new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                DemandConfig.Default, startingCash, seed);
        }

        private static void RunOneDay(HotelSim sim)
        {
            sim.BeginDay();
            sim.RunToEndOfDay();
            sim.SettleDay();
            sim.Clock.BeginNextDay();
        }

        // ── 装修接入日循环 ────────────────────────────────────────────────────

        [Test]
        public void Renovation_NeedsMaterialsAndCash_AndFailsWholeOrder()
        {
            var sim = BuildHotel(startingCash: 200);

            Assert.That(sim.TryStartRenovation(RenovationPlanKind.Economy, new[] { 201 }, out string reason),
                        Is.False);
            Assert.That(reason, Does.Contain("materials"), "先卡材料");

            sim.TryBuyMaterials(2);
            Assert.That(sim.TryStartRenovation(RenovationPlanKind.Economy, new[] { 201 }, out reason),
                        Is.False);
            Assert.That(reason, Does.Contain("$"), "材料够了，钱不够");
            Assert.That(sim.Rooms.At(201).state, Is.EqualTo(RoomSimState.Ready), "失败不该动房态");
        }

        [Test]
        public void Renovation_BlocksRoomsImmediately_ThatIsTheRealCost()
        {
            var sim = BuildHotel();
            sim.TryBuyMaterials(20);
            int sellableBefore = sim.Rooms.SellableCount;

            bool ok = sim.TryStartRenovation(RenovationPlanKind.Standard, new[] { 201, 202 }, out _);

            Assert.That(ok, Is.True);
            Assert.That(sim.Rooms.SellableCount, Is.EqualTo(sellableBefore - 2),
                        "开工即关房——装修真正的代价是这几天卖不出去");
            Assert.That(sim.Rooms.At(201).state, Is.EqualTo(RoomSimState.Blocked));
            Assert.That(sim.Rooms.IsSurfaced(201), Is.True, "装修中的房要浮出给玩家看见");
            Assert.That(sim.Renovations.RoomsUnderRenovation, Is.EqualTo(2));
        }

        [Test]
        public void Renovation_CompletesAfterBlockDays_AndRaisesTier()
        {
            var sim = BuildHotel();
            sim.TryBuyMaterials(20);
            sim.TryStartRenovation(RenovationPlanKind.Standard, new[] { 201 }, out _); // 2 天

            RunOneDay(sim);
            Assert.That(sim.Rooms.At(201).tier, Is.EqualTo(RoomTier.Old), "第一天还没完工");

            RunOneDay(sim);

            Assert.That(sim.Rooms.At(201).tier, Is.EqualTo(RoomTier.Basic), "完工升档");
            Assert.That(sim.Rooms.At(201).state, Is.EqualTo(RoomSimState.Dirty),
                        "装修完也是要打扫的（一屋灰）");
            Assert.That(sim.Renovations.IsRenovating(201), Is.False);
            Assert.That(sim.Rooms.At(201).wear, Is.EqualTo(0f).Within(1e-4f), "翻新重置磨损");
        }

        [Test]
        public void RenovatedRoom_EarnsMorePerNight()
        {
            var sim = BuildHotel();
            int oldRate = sim.Pricing.PriceFor(1, RoomTier.Old);
            int basicRate = sim.Pricing.PriceFor(1, RoomTier.Basic);
            int betterRate = sim.Pricing.PriceFor(1, RoomTier.Better);

            Assert.That(basicRate, Is.GreaterThan(oldRate), "翻新过的房卖得贵——投资回报的来源");
            Assert.That(betterRate, Is.GreaterThan(basicRate));
        }

        [Test]
        public void LuxuryPlan_CostsMoreAndTakesLonger_ButJumpsTwoTiers()
        {
            var sim = BuildHotel();
            sim.TryBuyMaterials(40);
            int cashBefore = sim.Cash;

            sim.TryStartRenovation(RenovationPlanKind.Luxury, new[] { 201 }, out _); // 5 天
            int spent = cashBefore - sim.Cash;

            for (int i = 0; i < 5; i++) RunOneDay(sim);

            Assert.That(sim.Rooms.At(201).tier, Is.EqualTo(RoomTier.Better));
            Assert.That(spent, Is.GreaterThan(RenovationPricing.CashCostFor(
                            RenovationPlan.For(RenovationPlanKind.Standard), 1)));
        }

        [Test]
        public void OccupiedRoom_CannotBeRenovated()
        {
            var sim = BuildHotel();
            sim.TryBuyMaterials(20);
            RunOneDay(sim);   // 让人住进来

            var occupied = new List<int>();
            for (int i = 0; i < sim.Rooms.Count; i++)
                if (sim.Rooms.Peek(i).state == RoomSimState.Occupied) occupied.Add(sim.Rooms.Peek(i).number);
            Assert.That(occupied, Is.Not.Empty, "得先有人住着才能测这条");

            bool ok = sim.TryStartRenovation(RenovationPlanKind.Economy, occupied, out string reason);

            Assert.That(ok, Is.False, "有人住着不能开工");
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void AlreadyGoodRoom_IsSkipped()
        {
            var sim = BuildHotel();
            sim.TryBuyMaterials(40);
            sim.TryStartRenovation(RenovationPlanKind.Luxury, new[] { 201 }, out _);
            for (int i = 0; i < 5; i++) RunOneDay(sim);
            Assert.That(sim.Rooms.At(201).tier, Is.EqualTo(RoomTier.Better));

            bool ok = sim.TryStartRenovation(RenovationPlanKind.Economy, new[] { 201 }, out _);
            Assert.That(ok, Is.False, "已经比目标档位高的房不该再花钱装");
        }

        [Test]
        public void BatchRenovation_IsCheaperPerRoom_ButShutsMoreRoomsForLonger()
        {
            var single = BuildHotel();
            var batch = BuildHotel();
            single.TryBuyMaterials(40);
            batch.TryBuyMaterials(40);

            int singleQuote = single.QuoteRenovation(RenovationPlanKind.Economy, 1);
            int batchQuote = batch.QuoteRenovation(RenovationPlanKind.Economy, 6);

            Assert.That(batchQuote / 6, Is.LessThan(singleQuote), "批量单价更低");
            Assert.That(batchQuote, Is.GreaterThan(singleQuote), "但一次性投入大得多");

            batch.TryStartRenovation(RenovationPlanKind.Economy, new[] { 201, 202, 203, 204, 205, 206 }, out _);
            Assert.That(batch.Renovations.DaysRemainingFor(201),
                        Is.GreaterThan(RenovationPlan.For(RenovationPlanKind.Economy).blockDays),
                        "批量把工期拉长了");
            Assert.That(batch.Rooms.SellableCount, Is.EqualTo(6), "12 间里 6 间关掉了");
        }

        [Test]
        public void RenovationInvestment_PaysBackOverTime()
        {
            // "装修一间房的投资回收天数可在晨报观测"（M-C 验收点）
            var renovated = BuildHotel(seed: 4242);
            var untouched = BuildHotel(seed: 4242);
            renovated.TryBuyMaterials(40);
            renovated.TryStartRenovation(RenovationPlanKind.Luxury, new[] { 201, 202, 203, 204 }, out _);

            int renovatedRevenue = 0, untouchedRevenue = 0;
            for (int day = 1; day <= 14; day++)
            {
                renovated.BeginDay(); renovated.RunToEndOfDay();
                renovatedRevenue += renovated.GrossIncomeToday;
                renovated.SettleDay(); renovated.Clock.BeginNextDay();

                untouched.BeginDay(); untouched.RunToEndOfDay();
                untouchedRevenue += untouched.GrossIncomeToday;
                untouched.SettleDay(); untouched.Clock.BeginNextDay();
            }

            Assert.That(renovated.Rooms.At(201).tier, Is.EqualTo(RoomTier.Better));
            Assert.That(renovatedRevenue, Is.GreaterThan(untouchedRevenue),
                        "两周之后，翻新过的房把关房损失赚回来了（长线投资成立）");
        }

        // ── 补齐的压力惩罚 ───────────────────────────────────────────────────

        [Test]
        public void NoReception_MeansTheQueueNeverMoves()
        {
            var sim = BuildHotel(receptionists: 0);
            sim.BeginDay();
            sim.RunToEndOfDay();

            Assert.That(sim.ArrivalsCheckedInToday, Is.EqualTo(0),
                        "前台没人：客人只能干等（前台空岗的代价）");
            Assert.That(sim.PeakCheckInWaitToday, Is.GreaterThan(0));
        }

        [Test]
        public void ThinReception_MakesGuestsWaitLonger()
        {
            var thin = BuildHotel(rooms: 40, receptionists: 1);
            var staffed = BuildHotel(rooms: 40, receptionists: 3);

            thin.BeginDay(); thin.RunToEndOfDay();
            staffed.BeginDay(); staffed.RunToEndOfDay();

            // 用"客人实际承受的等待总量"对比。PeakCheckInWait 在前台临时无人（摸鱼）时
            // 会顶到哨兵值 60，两边都容易撞顶，做不了强弱比较。
            Assert.That(thin.TotalCheckInWaitToday, Is.GreaterThan(staffed.TotalCheckInWaitToday),
                        "前台人少 → 客人累计等得更久");
        }

        [Test]
        public void QueueWait_DragsSatisfactionDown()
        {
            var thin = BuildHotel(rooms: 40, receptionists: 1, seed: 8080);
            var staffed = BuildHotel(rooms: 40, receptionists: 3, seed: 8080);

            for (int day = 1; day <= 4; day++)
            {
                RunOneDay(thin);
                RunOneDay(staffed);
            }

            Assert.That(thin.Reputation.AverageSatisfaction,
                        Is.LessThan(staffed.Reputation.AverageSatisfaction),
                        "排队扣满意度 → 星级 → 明天的客量（服务压力咬客流压力）");
        }

        [Test]
        public void NoInspector_ProducesFlawedRoomsAndUnhappyGuests()
        {
            var noInspector = BuildHotel(rooms: 24, inspectors: 0, seed: 606);
            var withInspector = BuildHotel(rooms: 24, inspectors: 1, seed: 606);

            int flawedWithout = 0, flawedWith = 0;
            for (int day = 1; day <= 5; day++)
            {
                RunOneDay(noInspector);
                RunOneDay(withInspector);
                flawedWithout += noInspector.FlawedStaysToday;   // 逐日累计——per-day 计数器每天清零
                flawedWith += withInspector.FlawedStaysToday;
            }

            Assert.That(flawedWithout, Is.GreaterThan(0),
                        "没验房员：清完直接上架，有房带瑕疵卖出去了");
            Assert.That(flawedWith, Is.EqualTo(0),
                        "有验房员就不会把带瑕疵的房卖出去（质量闸门的意义）");
            // 注意：不比较两家酒店的平均满意度——员工配置不同会让 RNG 流分叉，
            // 客群混合随之改变，0.02 量级的差异被噪声吞掉。瑕疵惩罚的经济后果
            // 在 LeanStaffing_IsNoLongerFreeMoney_OverTwoWeeks 里才有足够效应量。
        }

        // ── 平衡：精简排班不再是免费的钱（§C2 缺口收口） ────────────────────────

        [Test]
        public void LeanStaffing_IsNoLongerFreeMoney_OverTwoWeeks()
        {
            var baseline = BuildHotel(rooms: 40, housekeepers: 4, inspectors: 1, receptionists: 3, seed: 5150);
            var lean = BuildHotel(rooms: 40, housekeepers: 4, inspectors: 1, receptionists: 3, seed: 5150);
            lean.Shifts.SetTier(StaffRole.Housekeeper, ShiftTier.Skeleton);
            lean.Shifts.SetTier(StaffRole.Reception, ShiftTier.Skeleton);
            lean.Shifts.SetTier(StaffRole.Inspector, ShiftTier.Skeleton);

            int baseProfit = 0, leanProfit = 0;
            for (int day = 1; day <= 14; day++)
            {
                baseline.BeginDay(); baseline.RunToEndOfDay();
                baseProfit += baseline.SettleDay().NetProfit;
                baseline.Clock.BeginNextDay();

                lean.BeginDay(); lean.RunToEndOfDay();
                leanProfit += lean.SettleDay().NetProfit;
                lean.Clock.BeginNextDay();
            }

            Assert.That(lean.Reputation.Stars, Is.LessThan(baseline.Reputation.Stars),
                        "抠人手 → 排队+瑕疵房 → 星级下滑");
            Assert.That(leanProfit, Is.LessThan(baseProfit),
                        "两周下来，省的工资赔不上流失的客人与差评（§C2 平衡缺口已收口）");
        }
    }
}
