using System.Collections.Generic;
using NUnit.Framework;

// M-C2 接线验收：挂牌档 vs 交付、退款链、家具故障封房、装修三档对家具的处理。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class HotelSimFurnitureTest
    {
        private static HotelSim BuildHotel(int rooms = 12, int startingCash = 20000, int seed = 24601,
                                           bool furnish = true)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            staff.Register(new StaffMember(StaffRole.Housekeeper, "H", 60, new StaffAttributes(55, 55, 55), 1, null));
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

        // ── 挂牌档是玩家定的，装修不再动它 ────────────────────────────────────

        [Test]
        public void PriceBand_IsPlayerSet_PerRoomAndPerFloor()
        {
            var sim = BuildHotel();

            Assert.That(sim.SetPriceBand(201, RoomTier.Better), Is.True);
            Assert.That(sim.Rooms.At(201).tier, Is.EqualTo(RoomTier.Better));
            Assert.That(sim.Rooms.At(202).tier, Is.EqualTo(RoomTier.Old), "只改那一间");

            int changed = sim.SetPriceBandForFloor(1, RoomTier.Basic);
            Assert.That(changed, Is.EqualTo(12), "整层批量设（不做逐间填表）");
            Assert.That(sim.Rooms.At(201).tier, Is.EqualTo(RoomTier.Basic), "批量覆盖了个别设置");
            Assert.That(sim.SetPriceBand(999, RoomTier.Basic), Is.False, "不存在的房安全失败");
        }

        [Test]
        public void Renovation_DoesNotTouchThePriceBand()
        {
            var sim = BuildHotel();
            sim.SetPriceBand(201, RoomTier.Better);
            sim.TryBuyMaterials(40);
            sim.TryStartRenovation(RenovationPlanKind.Luxury, new[] { 201 }, out _);
            for (int i = 0; i < 5; i++) RunOneDay(sim);

            Assert.That(sim.Rooms.At(201).tier, Is.EqualTo(RoomTier.Better),
                        "挂牌档是玩家的决定，装修只换家具");
            Assert.That(sim.Furniture.DeliveredQuality(201), Is.EqualTo(1f).Within(1e-3f));
        }

        // ── 交付 vs 挂牌：双向 ────────────────────────────────────────────────

        [Test]
        public void UnderChargingAGoodRoom_DelightsGuests()
        {
            var honest = BuildHotel(seed: 111);
            var generous = BuildHotel(seed: 111);
            // 两家都把房装到顶配，但一家按 Better 卖、一家按 Old 卖（挂低卖高）
            foreach (var sim in new[] { honest, generous })
            {
                sim.TryBuyMaterials(200);
                var rooms = new List<int>();
                for (int i = 0; i < 6; i++) rooms.Add(201 + i);
                sim.TryStartRenovation(RenovationPlanKind.Luxury, rooms, out _);
                for (int d = 0; d < 5; d++) RunOneDay(sim);
            }
            honest.SetPriceBandForFloor(1, RoomTier.Better);
            generous.SetPriceBandForFloor(1, RoomTier.Old);

            for (int d = 0; d < 6; d++) { RunOneDay(honest); RunOneDay(generous); }

            Assert.That(generous.Reputation.AverageSatisfaction,
                        Is.GreaterThan(honest.Reputation.AverageSatisfaction),
                        "顶配房按老房价卖 → 客人惊喜（收入少但口碑好）");
        }

        [Test]
        public void OverChargingADerelictRoom_GeneratesRefundRequests()
        {
            var sim = BuildHotel(seed: 4242);
            sim.SetPriceBandForFloor(1, RoomTier.Better);   // 破家具房硬挂顶价

            RunOneDay(sim);      // 有人住进来
            sim.BeginDay();      // 退房结算 → 应该有人要求退款

            Assert.That(sim.PendingRefunds.Count, Is.GreaterThan(0),
                        "交付远低于挂牌 → 退款申请（虚报价的代价）");
            var request = sim.PendingRefunds[0];
            Assert.That(request.amount, Is.GreaterThan(0));
            Assert.That(request.gap, Is.LessThan(0f));
            Assert.That(request.line, Is.Not.Empty);
            foreach (char c in request.line)
                Assert.That(c, Is.LessThan(128), "客人原话必须英文：" + request.line);
        }

        [Test]
        public void HonestPricing_KeepsRefundsAway()
        {
            var sim = BuildHotel(seed: 4242);
            sim.SetPriceBandForFloor(1, RoomTier.Old);   // 破房就按破房价卖

            RunOneDay(sim);
            sim.BeginDay();

            Assert.That(sim.PendingRefunds, Is.Empty, "如实定价没人要求退款");
        }

        [Test]
        public void ApproveRefund_GivesBackTheMoney()
        {
            var sim = BuildHotel(seed: 4242);
            sim.SetPriceBandForFloor(1, RoomTier.Better);
            // 打烊结账（M-F）：退款申请在 SettleDay 的退房潮里产生，晨报上处理
            sim.BeginDay(); sim.RunToEndOfDay(); sim.SettleDay();
            Assert.That(sim.PendingRefunds.Count, Is.GreaterThan(0), "虚报价该在打烊时收到退款申请");

            sim.CollectSafebox();   // 得先有现金才退得起
            int cashBefore = sim.Cash;
            var request = sim.PendingRefunds[0];
            bool ok = sim.ApproveRefund(request.requestId);

            Assert.That(ok, Is.True);
            Assert.That(sim.Cash, Is.EqualTo(cashBefore - request.amount),
                        "退款从现金掏——钱已入箱，扣毛收入是扣寂寞");
            Assert.That(sim.RefundsApprovedToday, Is.EqualTo(1));
            Assert.That(sim.ApproveRefund(request.requestId), Is.False, "同一笔不能退两次");

            // 现金掏空后退不了，只能拒绝吃声誉（和赔偿超售客同一逻辑）
            if (sim.PendingRefunds.Count > 0)
            {
                sim.TrySpendCash(sim.Cash);
                Assert.That(sim.ApproveRefund(sim.PendingRefunds[0].requestId), Is.False,
                            "没现金就退不了款");
            }
        }

        [Test]
        public void RejectRefund_KeepsMoneyButCostsReputation()
        {
            var sim = BuildHotel(seed: 4242);
            sim.SetPriceBandForFloor(1, RoomTier.Better);
            RunOneDay(sim);
            sim.BeginDay();
            Assume.That(sim.PendingRefunds.Count, Is.GreaterThan(0));

            int incomeBefore = sim.GrossIncomeToday;
            float satBefore = sim.Reputation.AverageSatisfaction;
            sim.RejectRefund(sim.PendingRefunds[0].requestId);

            Assert.That(sim.GrossIncomeToday, Is.EqualTo(incomeBefore), "钱保住了");
            Assert.That(sim.Reputation.AverageSatisfaction, Is.LessThan(satBefore), "但补了一记差评");
            Assert.That(sim.RefundsRejectedToday, Is.EqualTo(1));
        }

        [Test]
        public void IgnoredRefunds_AutoResolveWorseThanRejecting()
        {
            var ignoring = BuildHotel(seed: 4242);
            var rejecting = BuildHotel(seed: 4242);
            ignoring.SetPriceBandForFloor(1, RoomTier.Better);
            rejecting.SetPriceBandForFloor(1, RoomTier.Better);

            // 打烊结账：第一晚 SettleDay 产生退款申请（挂给晨报）
            RunOneDay(ignoring);
            RunOneDay(rejecting);
            Assert.That(rejecting.PendingRefunds.Count, Is.GreaterThan(0), "打烊时该有退款申请");
            while (rejecting.PendingRefunds.Count > 0)
                rejecting.RejectRefund(rejecting.PendingRefunds[0].requestId);

            // 记下拖着的这批单号：下一次日结要把**它们**收口（今晚新产生的照样挂着）
            var ignoredIds = new List<int>();
            foreach (var r in ignoring.PendingRefunds) ignoredIds.Add(r.requestId);

            ignoring.BeginDay(); ignoring.RunToEndOfDay(); ignoring.SettleDay();
            rejecting.BeginDay(); rejecting.RunToEndOfDay(); rejecting.SettleDay();

            foreach (var r in ignoring.PendingRefunds)
                Assert.That(ignoredIds, Has.No.Member(r.requestId),
                            "昨晚拖着的退款必须在下一次日结被自动收口");
            Assert.That(ignoring.Reputation.AverageSatisfaction,
                        Is.LessThanOrEqualTo(rejecting.Reputation.AverageSatisfaction),
                        "装作没看见比明确拒绝更亏");
        }

        // ── 家具故障 → 房间不可售 ─────────────────────────────────────────────

        [Test]
        public void FaultedBed_BlocksTheRoomUntilRepaired()
        {
            var sim = BuildHotel();
            var bed = sim.Furniture.InRoom(201)[0];
            bed.faultLineIndex = 0;

            RunOneDay(sim);   // 日结会同步房态

            Assert.That(sim.Rooms.At(201).state, Is.EqualTo(RoomSimState.Blocked), "床坏了房间封锁");
            Assert.That(sim.Rooms.IsSurfaced(201), Is.True, "问题房要浮出给玩家看见");

            Assert.That(sim.TryRepairFurniture(bed.instanceId, out _), Is.True);
            Assert.That(bed.IsUnderRepair, Is.True, "维修要花时间，不是瞬间好");
            Assert.That(sim.Rooms.At(201).state, Is.EqualTo(RoomSimState.Blocked), "修的过程中还是封着");

            RunOneDay(sim);   // 工期走完

            Assert.That(bed.IsFaulted, Is.False, "修好了");
            // 修好 ≠ 修新：健康度只回到这件家具剩下的寿命为止（新规则）。
            // 不锁具体数值——工期跑完那一晚崭新度还会再掉一点，钉死数字会闪断。
            Assert.That(bed.health, Is.GreaterThan(0f), "修完总比坏着好");
            Assert.That(bed.health, Is.LessThan(FurnitureWearModel.TroubleThreshold),
                        "破家具修完仍在故障线以下——它几天后照样会坏，这是设计");
            Assert.That(bed.patchedUp, Is.True, "将就过了：以后有塌的可能");
            Assert.That(sim.Rooms.At(201).state, Is.Not.EqualTo(RoomSimState.Blocked), "房间放回来了");
        }

        [Test]
        public void RepairCostsCashAndLeavesNewnessAlone()
        {
            var sim = BuildHotel();
            var bed = sim.Furniture.InRoom(201)[0];
            bed.faultLineIndex = 0;
            float newnessBefore = bed.newness;
            int cashBefore = sim.Cash;

            sim.TryRepairFurniture(bed.instanceId, out _);

            Assert.That(sim.Cash, Is.LessThan(cashBefore), "维修花钱");
            Assert.That(bed.newness, Is.EqualTo(newnessBefore).Within(1e-4f),
                        "崭新度一动不动——只有翻新装修能重置它");
        }

        [Test]
        public void BrokeHotel_CannotAffordRepairs()
        {
            var sim = BuildHotel(startingCash: 0);
            var bed = sim.Furniture.InRoom(201)[0];
            bed.faultLineIndex = 0;

            Assert.That(sim.TryRepairFurniture(bed.instanceId, out string reason), Is.False);
            Assert.That(reason, Does.Contain("$"));
            Assert.That(bed.IsUnderRepair, Is.False);
        }

        [Test]
        public void DerelictFurniture_StartsBreakingWithinAFortnight()
        {
            var sim = BuildHotel(rooms: 20);
            int faultDays = 0;
            for (int day = 1; day <= 20; day++)
            {
                RunOneDay(sim);
                if (sim.FurnitureFaultsToday.Count > 0) faultDays++;
            }
            Assert.That(faultDays, Is.GreaterThan(0),
                        "继承来的破家具会不断出故障——这就是'破败'的具象化");
        }

        // ── 家具买卖 ─────────────────────────────────────────────────────────

        [Test]
        public void BuyingOptionalFurniture_RaisesDeliveredQuality()
        {
            var sim = BuildHotel();
            float before = sim.Furniture.DeliveredQuality(201);

            Assert.That(sim.TryBuyFurniture(201, FurnitureCatalog.BigFlatTv, out _), Is.True);

            Assert.That(sim.Furniture.DeliveredQuality(201), Is.GreaterThan(before), "摆家具提升交付");
            Assert.That(sim.Furniture.SegmentAppeal(201, GuestSegment.Party), Is.GreaterThan(0f),
                        "大电视讨派对客喜欢");
        }

        [Test]
        public void OptionalSlots_AreLimited()
        {
            var sim = BuildHotel();
            int[] optional = { FurnitureCatalog.BigFlatTv, FurnitureCatalog.Sofa,
                               FurnitureCatalog.WorkDesk, FurnitureCatalog.Rug };
            foreach (int kind in optional)
                Assert.That(sim.TryBuyFurniture(201, kind, out _), Is.True);

            Assert.That(sim.TryBuyFurniture(201, FurnitureCatalog.WallArt, out string reason), Is.False);
            Assert.That(reason, Does.Contain("slot"), "可选位满了");
        }

        [Test]
        public void BuyingARequiredItem_ReplacesTheOldOneAndPaysSalvage()
        {
            var sim = BuildHotel();
            int bedsBefore = 0;
            foreach (var f in sim.Furniture.InRoom(201))
                if (FurnitureCatalog.Get(f.kindId).slot == FurnitureSlot.Bed) bedsBefore++;

            Assert.That(sim.TryBuyFurniture(201, FurnitureCatalog.MemoryFoamBed, out _), Is.True);

            int bedsAfter = 0;
            foreach (var f in sim.Furniture.InRoom(201))
                if (FurnitureCatalog.Get(f.kindId).slot == FurnitureSlot.Bed) bedsAfter++;
            Assert.That(bedsAfter, Is.EqualTo(bedsBefore), "床是替换语义，不会变两张");
            Assert.That(sim.Furniture.InRoom(201)[sim.Furniture.InRoom(201).Count - 1].kindId,
                        Is.EqualTo(FurnitureCatalog.MemoryFoamBed));
        }

        [Test]
        public void SellingFurniture_ReturnsVeryLittle()
        {
            var sim = BuildHotel();
            sim.TryBuyFurniture(201, FurnitureCatalog.Sofa, out _);
            var sofa = sim.Furniture.InRoom(201)[sim.Furniture.InRoom(201).Count - 1];
            int cashBefore = sim.Cash;

            int back = sim.SellFurniture(sofa.instanceId);

            Assert.That(back, Is.LessThan(FurnitureCatalog.Get(FurnitureCatalog.Sofa).cashCost / 4),
                        "残值压得很低");
            Assert.That(sim.Cash, Is.EqualTo(cashBefore + back));
            Assert.That(sim.Furniture.TryGet(sofa.instanceId, out _), Is.False);
        }

        // ── 需求跟随实际交付而非挂牌档 ────────────────────────────────────────

        [Test]
        public void DemandFollowsRealQuality_NotTheLabelYouSlapOnIt()
        {
            var relabelled = BuildHotel(seed: 909, startingCash: 60000);
            var actuallyNice = BuildHotel(seed: 909, startingCash: 60000);

            relabelled.SetPriceBandForFloor(1, RoomTier.Better);  // 只改标签
            actuallyNice.TryBuyMaterials(200);
            var rooms = new List<int>();
            for (int i = 0; i < 12; i++) rooms.Add(201 + i);
            // 断言开工成功：钱不够时整单失败，不写这句的话测试会以一个含糊的数字挂掉
            Assert.That(actuallyNice.TryStartRenovation(RenovationPlanKind.Luxury, rooms, out string why),
                        Is.True, why);
            for (int d = 0; d < 10; d++) RunOneDay(actuallyNice);
            Assume.That(actuallyNice.Furniture.DeliveredQuality(201), Is.GreaterThan(0.9f), "装修得完工");

            // M-D 之后不能只比"某一天"的到店数：到店客来自**十四天前下的单**，
            // 而装修期间全店 Block ⇒ 那些天容量为 0 ⇒ 一张单都接不下来。
            // 于是刚完工那天的到店数必然很低——这是正确行为，不是需求模型出错。
            // 改为让预订簿在新品质下重新铺满一个完整视野，再比**累计**入住量
            // （同种子累计多日对比是本项目定下的口径，逐日单调断言太脆）。
            int niceCheckIns = 0, relabelledCheckIns = 0;
            for (int d = 0; d < BookingGenerator.HorizonDays + 7; d++)
            {
                RunOneDay(actuallyNice);
                niceCheckIns += actuallyNice.ArrivalsCheckedInToday;
                RunOneDay(relabelled);
                relabelledCheckIns += relabelled.ArrivalsCheckedInToday;
            }

            Assert.That(niceCheckIns, Is.GreaterThan(relabelledCheckIns),
                        "真装修才拉来客人；把全店改标 Better 不会凭空变多");
        }

        // ── 三档挂牌必须各自对得上一个投资阶段 ────────────────────────────────
        //
        // M-C2 试玩发现的真 bug：期待值原本 0.10/0.55/0.90，而标准装修完的房只交付
        // 0.35，于是**连翻新过的房都不能诚实地按 Basic 卖**，装完也只配老房价——
        // 装修在玩家眼里就纯亏。期待值必须落在家具真能提供的范围内，否则档位是骗局。
        // 这个测试锁的是"档位 ↔ 投资阶段"的对应关系本身，不是具体数字：
        // 以后调家具装饰度，只要还满足这三条对应，随便调。
        [Test]
        public void EachPriceBand_HasAnInvestmentStageThatHonestlyDeliversIt()
        {
            var sim = BuildHotel(rooms: 1, startingCash: 200000, seed: 4242);
            sim.TryBuyMaterials(200);
            var one = new List<int> { 201 };

            // 阶段 1：继承的破家具 ⇒ 只配 Old
            float derelict = sim.DeliveredQualityOf(201);
            Assert.That(derelict, Is.GreaterThanOrEqualTo(DemandModel.ExpectedQualityOf(RoomTier.Old)),
                        "开局的破房按 Old 卖必须是诚实的，不然玩家第一天就吃退款");
            Assert.That(derelict, Is.LessThan(DemandModel.ExpectedQualityOf(RoomTier.Basic)),
                        "破房不该白得 Basic 的资格");

            // 阶段 2：标准装修（必备升一档 + 崭新度回满）⇒ 刚好够 Basic
            Assert.That(sim.TryStartRenovation(RenovationPlanKind.Standard, one, out string why), Is.True, why);
            for (int d = 0; d < 6; d++) RunOneDay(sim);
            float renovated = sim.DeliveredQualityOf(201);
            Assume.That(sim.Rooms.At(201).state, Is.Not.EqualTo(RoomSimState.Blocked), "装修得完工");
            Assert.That(renovated, Is.GreaterThanOrEqualTo(DemandModel.ExpectedQualityOf(RoomTier.Basic) - 0.02f),
                        "装修完就该能诚实挂 Basic——这是玩家掏 $1300 买到的东西");
            Assert.That(renovated, Is.LessThan(DemandModel.ExpectedQualityOf(RoomTier.Better)),
                        "光换必备家具不该直通 Better，Better 得靠可选装饰位");

            // 阶段 3：补齐可选装饰位 ⇒ 够 Better
            foreach (int kind in new[] { FurnitureCatalog.BigFlatTv, FurnitureCatalog.Sofa,
                                         FurnitureCatalog.WorkDesk, FurnitureCatalog.Rug })
                Assert.That(sim.TryBuyFurniture(201, kind, out string reason), Is.True, reason);

            Assert.That(sim.DeliveredQualityOf(201),
                        Is.GreaterThanOrEqualTo(DemandModel.ExpectedQualityOf(RoomTier.Better)),
                        "装修 + 摆满可选家具才够 Better；这是装饰度投资的出口");
        }
    }
}
