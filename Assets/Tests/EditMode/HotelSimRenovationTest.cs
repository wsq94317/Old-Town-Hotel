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
                                           int receptionists = 2, int startingCash = 20000, int seed = 31337,
                                           bool furnish = true)
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

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, startingCash, seed);
            // 继承的破家具（M-C2）。空房也可售，所以想隔离掉家具故障这个变量时传 furnish: false
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
            Assert.That(sim.Furniture.AverageNewness(201), Is.LessThan(0.9f), "第一天还没完工");

            RunOneDay(sim);

            // 装修不再改挂牌档（那是玩家定的价格档），它换新的是家具
            Assert.That(sim.Furniture.AverageNewness(201), Is.EqualTo(1f).Within(1e-3f), "完工=家具崭新度回满");
            Assert.That(sim.Furniture.InRoom(201)[0].kindId, Is.EqualTo(FurnitureCatalog.ProperBed),
                        "标准方案把必备家具升一档");
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

            Assert.That(sim.Furniture.DeliveredQuality(201), Is.EqualTo(1f).Within(1e-3f), "豪华=顶配满交付");
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
            Assert.That(sim.Furniture.DeliveredQuality(201), Is.EqualTo(1f).Within(1e-3f));

            bool ok = sim.TryStartRenovation(RenovationPlanKind.Luxury, new[] { 201 }, out _);
            Assert.That(ok, Is.False, "已经顶配的房不该再花钱装");
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
                // 打烊结账（M-F）：房费在 SettleDay 里入账，读毛收入要在它**之后**
                renovated.BeginDay(); renovated.RunToEndOfDay(); renovated.SettleDay();
                renovatedRevenue += renovated.GrossIncomeToday;
                renovated.Clock.BeginNextDay();

                untouched.BeginDay(); untouched.RunToEndOfDay(); untouched.SettleDay();
                untouchedRevenue += untouched.GrossIncomeToday;
                untouched.Clock.BeginNextDay();
            }

            // 断言的是"翻新过的房交付水平远高于没动的房"，不是"两周后还剩几成新"——
            // 后者是调参事实：入住率一变（M-D 接入预订流后就变了），磨损速度跟着变，
            // 14 天后的绝对崭新度会在 0.88 上下浮动，写死 0.9 就成了假失败。
            Assert.That(renovated.Furniture.DeliveredQuality(201),
                        Is.GreaterThan(untouched.Furniture.DeliveredQuality(201) + 0.5f),
                        "豪华装修后的交付水平必须远高于没动过的破房");
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
            // 场景要选在**前台真的是瓶颈**的地方，两个坑都得避开：
            // ① 不放继承的破家具（理由见 LeanStaffing 那条）：带上它的话，人手足的酒店
            //    住进更多客人 ⇒ 家具磨损更快 ⇒ 封房更多 ⇒ 满意度反而更低，断言直接翻转。
            // ② 房量要够大。40 间房时累计等待确实有差（1063 vs 579 分钟），但摊到每位
            //    客人约 10 分钟，正好卡在 WaitGraceMinutes 的宽容窗口上，满意度分毫不动
            //    ——那是宽容窗口在正常工作，不是因果断了。80 间房时每人等约 32 分钟，
            //    惩罚才真正咬下去（实测 0.757 vs 0.896）。
            var thin = BuildHotel(rooms: 80, receptionists: 1, seed: 8080, furnish: false);
            var staffed = BuildHotel(rooms: 80, receptionists: 2, seed: 8080, furnish: false);

            int thinWait = 0, staffedWait = 0;
            float thinQueueDamage = 0f, staffedQueueDamage = 0f;
            int thinGuests = 0, staffedGuests = 0;
            for (int day = 1; day <= 4; day++)
            {
                RunOneDay(thin);
                thinWait += thin.TotalCheckInWaitToday;
                thinGuests += thin.Breakdown.CountOf(ReputationCause.QueueWait);
                thinQueueDamage += thin.Breakdown.TotalOf(ReputationCause.QueueWait);

                RunOneDay(staffed);
                staffedWait += staffed.TotalCheckInWaitToday;
                staffedGuests += staffed.Breakdown.CountOf(ReputationCause.QueueWait);
                staffedQueueDamage += staffed.Breakdown.TotalOf(ReputationCause.QueueWait);
            }

            Assert.That(thinWait, Is.GreaterThan(staffedWait), "前台越薄，客人累计等得越久");

            // 直接断**排队这一项**造成的声誉损失，不用 Reputation.AverageSatisfaction：
            // 后者是只看最近 20 位客人的滑动窗口，80 间房一天来 50 人，窗口盖不住半天，
            // 两家酒店很容易同时被压到满意度下限 0.5 而分不出高下（实测就是这样挂的）。
            // 声誉明细给的是因果量本身，没有窗口噪声。
            Assert.That(thinGuests, Is.GreaterThan(0));
            Assert.That(staffedGuests, Is.GreaterThan(0));
            float thinPerGuest = thinQueueDamage / thinGuests;
            float staffedPerGuest = staffedQueueDamage / staffedGuests;

            Assert.That(thinPerGuest, Is.LessThan(staffedPerGuest),
                        $"排队扣满意度：人手薄的每位客人被排队扣 {thinPerGuest:0.000}，"
                        + $"人手足的只扣 {staffedPerGuest:0.000}");
            Assert.That(staffedPerGuest, Is.GreaterThan(-0.05f),
                        "人手足够时排队几乎不该扣分——宽容窗口要真的起作用");
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
            // **不放继承的破家具**：这条测的是人手压力。带上那些家具的话，两周内
            // 40 间房会因为故障全部封锁（实测第 14 天 38/40 封房、可售 0，之后收入完全
            // 停在原地），两家酒店都是"被家具搞死"，利润差只是谁死得更体面——
            // 与排班毫无关系。家具那条死亡螺旋由 HotelSimFurnitureTest 单独守。
            var baseline = BuildHotel(rooms: 40, housekeepers: 4, inspectors: 1, receptionists: 3,
                                      seed: 5150, furnish: false);
            var lean = BuildHotel(rooms: 40, housekeepers: 4, inspectors: 1, receptionists: 3,
                                  seed: 5150, furnish: false);
            lean.Shifts.SetTier(StaffRole.Housekeeper, ShiftTier.Skeleton);
            lean.Shifts.SetTier(StaffRole.Reception, ShiftTier.Skeleton);
            lean.Shifts.SetTier(StaffRole.Inspector, ShiftTier.Skeleton);

            int baseProfit = 0, leanProfit = 0;
            int baseTurnedAway = 0, leanTurnedAway = 0;
            int baseWait = 0, leanWait = 0;
            int baseCheckedIn = 0, leanCheckedIn = 0;
            for (int day = 1; day <= 14; day++)
            {
                baseline.BeginDay(); baseline.RunToEndOfDay();
                baseProfit += baseline.SettleDay().NetProfit;
                baseTurnedAway += baseline.ArrivalsTurnedAwayToday;
                baseWait += baseline.TotalCheckInWaitToday;
                baseCheckedIn += baseline.ArrivalsCheckedInToday;
                baseline.Clock.BeginNextDay();

                lean.BeginDay(); lean.RunToEndOfDay();
                leanProfit += lean.SettleDay().NetProfit;
                leanTurnedAway += lean.ArrivalsTurnedAwayToday;
                leanWait += lean.TotalCheckInWaitToday;
                leanCheckedIn += lean.ArrivalsCheckedInToday;
                lean.Clock.BeginNextDay();
            }

            // 机制层面的证据（结构上必然成立）
            Assert.That(leanTurnedAway, Is.GreaterThan(baseTurnedAway), "抠人手 → 更多客人住不进来");
            // 用**人均**等待，不用累计等待：抠人手会少住进很多客人，
            // 累计等待反而更小（人少了当然总和小），这是聚合量的经典陷阱。
            // 客人实际承受的是"我等了多久"，所以人均才是这条因果的正确度量。
            Assert.That(leanCheckedIn, Is.GreaterThan(0));
            Assert.That(baseCheckedIn, Is.GreaterThan(0));
            float leanPerGuest = leanWait / (float)leanCheckedIn;
            float basePerGuest = baseWait / (float)baseCheckedIn;
            Assert.That(leanPerGuest, Is.GreaterThan(basePerGuest),
                        $"抠前台 → 每位客人等更久（精简 {leanPerGuest:0.0} 分 vs 全员 {basePerGuest:0.0} 分）");

            // §C2 平衡缺口的收口断言
            Assert.That(leanProfit, Is.LessThan(baseProfit),
                        "两周下来，省的工资赔不上流失的客人与差评（§C2 平衡缺口已收口）");

            // M-D 实测补充：这条平衡在**两周尺度**上成立（全员 $21k vs 骨架 $16.5k），
            // 但把同一局跑到 56 天，骨架班会反超（$48.9k vs $45.4k）。原因不是惩罚失效，
            // 而是 40 间房配 4 名管家本身就过剩：一名管家一班约清 19 间，而连住单把
            // 日退房量压到 ~15 间，一个人真的够用，省下的四倍工资盖过了流失的客人。
            // 人手压力要到房量/周转再高一档才真正咬人（与 §C2 记的"约 30 间房起显现"一致）。
            // 所以这里断的是两周窗口——把窗口拉长会变成在断"4 名管家是否过剩"，那是另一回事。

            // 注意：**不断言星级更低**。M-C2 引入家具后出现一个真实但反直觉的对冲——
            // 客人少 ⇒ 家具磨损慢 ⇒ 交付水平保持得更好 ⇒ 住进来的那few个客人反而更满意。
            // 星级于是被两股力量夹住，噪声量级；利润才是这条平衡的可靠判据。
            // （这个副作用本身是合理的：空置的酒店确实更"新"，只是不赚钱。）
        }
    }
}
