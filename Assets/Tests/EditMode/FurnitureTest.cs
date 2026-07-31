using System.Collections.Generic;
using NUnit.Framework;

// 家具系统：崭新度与健康度**严格解耦**（维修只回健康度，崭新度只有翻新能重置）、
// 崭新度折扣装饰度、故障概率、残值、三种装修方案的家具处理。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FurnitureTest
    {
        // ── 磨损模型 ─────────────────────────────────────────────────────────

        [Test]
        public void WearMultiplier_RampsFromOneToSix()
        {
            Assert.That(FurnitureWearModel.WearMultiplier(1f), Is.EqualTo(1f).Within(1e-4f), "全新=基准速度");
            Assert.That(FurnitureWearModel.WearMultiplier(0f), Is.EqualTo(6f).Within(1e-4f), "报废=6倍速");
            Assert.That(FurnitureWearModel.WearMultiplier(0.5f), Is.LessThan(3f),
                        "平方曲线：中段恶化还不明显，快报废时才急剧变差");
            Assert.That(FurnitureWearModel.WearMultiplier(2f), Is.EqualTo(1f).Within(1e-4f), "越界钳住");
        }

        [Test]
        public void OldFurniture_LosesHealthMuchFasterThanNew()
        {
            float fresh = FurnitureWearModel.HealthLossPerNight(newness: 1f, segmentWear: 1f);
            float worn = FurnitureWearModel.HealthLossPerNight(newness: 0.1f, segmentWear: 1f);

            Assert.That(worn, Is.GreaterThan(fresh * 4f), "旧家具健康度掉得快得多——这就是'修理只是买时间'的根据");
        }

        [Test]
        public void FaultChance_JumpsOnceHealthDropsBelowThreshold()
        {
            double healthy = FurnitureWearModel.FaultChancePerDay(0.8f);
            double atThreshold = FurnitureWearModel.FaultChancePerDay(FurnitureWearModel.TroubleThreshold);
            double dying = FurnitureWearModel.FaultChancePerDay(0.05f);
            double dead = FurnitureWearModel.FaultChancePerDay(0f);

            Assert.That(healthy, Is.EqualTo(FurnitureWearModel.HealthyFaultChance).Within(1e-6));
            Assert.That(atThreshold, Is.EqualTo(healthy).Within(1e-6), "阈值处连续，不跳变");
            Assert.That(dying, Is.GreaterThan(healthy * 10d));
            Assert.That(dead, Is.EqualTo(0.16d).Within(1e-6), "健康度归零时 16%/天");
        }

        [Test]
        public void EffectiveDecor_DiscountsByNewness_AndZeroesWhenFaulted()
        {
            Assert.That(FurnitureWearModel.EffectiveDecor(10, newness: 1f, faulted: false),
                        Is.EqualTo(10f).Within(1e-3f), "全新给满分");
            Assert.That(FurnitureWearModel.EffectiveDecor(10, newness: 0f, faulted: false),
                        Is.EqualTo(4f).Within(1e-3f), "报废但能用：还剩四成体面");
            Assert.That(FurnitureWearModel.EffectiveDecor(10, newness: 1f, faulted: true),
                        Is.EqualTo(0f), "坏了的沙发不加分");
        }

        [Test]
        public void SalvageValue_IsDeliberatelyLow()
        {
            int half = FurnitureWearModel.SalvageValue(700, 0.5f);
            Assert.That(half, Is.EqualTo(70), "$700 的床用到半寿命只值 $70");
            Assert.That(FurnitureWearModel.SalvageValue(700, 0f), Is.EqualTo(0));
        }

        // ── 目录 ─────────────────────────────────────────────────────────────

        [Test]
        public void Catalog_HasRequiredSlotsAndAsciiCopy()
        {
            bool anyBed = false, anyBathroom = false;
            foreach (var kind in FurnitureCatalog.All)
            {
                if (kind.slot == FurnitureSlot.Bed) anyBed = true;
                if (kind.slot == FurnitureSlot.Bathroom) anyBathroom = true;
                Assert.That(kind.cashCost, Is.GreaterThan(0), kind.name);
                Assert.That(kind.lifespanGuestNights, Is.GreaterThan(0), kind.name);
                Assert.That(kind.faultLines, Is.Not.Null.And.Not.Empty, kind.name + " 需要故障文案");
                foreach (char c in kind.name)
                    Assert.That(c, Is.LessThan(128), "游戏内文案必须英文：" + kind.name);
                foreach (string line in kind.faultLines)
                    foreach (char c in line)
                        Assert.That(c, Is.LessThan(128), "故障文案必须英文（占位字体没有中文/破折号）：" + line);
            }
            Assert.That(anyBed && anyBathroom, Is.True);
        }

        [Test]
        public void TopTierLoadout_ScoresFullDeliveredQuality()
        {
            int sum = 0;
            foreach (int kindId in FurnitureCatalog.TopTierLoadout())
                sum += FurnitureCatalog.Get(kindId).decorPoints;

            Assert.That(sum, Is.GreaterThanOrEqualTo(FurnitureCatalog.FullyFurnishedDecor),
                        "顶配配置应能打满交付水平（满配基准是固定常数，不随新家具浮动）");
        }

        [Test]
        public void UpgradedRequiredKind_WalksUpAndStopsAtTop()
        {
            Assert.That(FurnitureCatalog.UpgradedRequiredKind(FurnitureCatalog.SaggingBed),
                        Is.EqualTo(FurnitureCatalog.ProperBed));
            Assert.That(FurnitureCatalog.UpgradedRequiredKind(FurnitureCatalog.ProperBed),
                        Is.EqualTo(FurnitureCatalog.MemoryFoamBed));
            Assert.That(FurnitureCatalog.UpgradedRequiredKind(FurnitureCatalog.MemoryFoamBed),
                        Is.EqualTo(FurnitureCatalog.MemoryFoamBed), "顶配再升还是顶配");
        }

        // ── 总账 ─────────────────────────────────────────────────────────────

        private static FurnitureLedger DerelictRoom(int room = 201, float newness = 0.1f, float health = 0.35f)
        {
            var ledger = new FurnitureLedger();
            ledger.FurnishDerelictRoom(room, newness, health);
            return ledger;
        }

        [Test]
        public void Place_AssignsStableNonReusedIds()
        {
            var ledger = new FurnitureLedger();
            var a = ledger.Place(201, FurnitureCatalog.Sofa);
            var b = ledger.Place(201, FurnitureCatalog.Rug);
            Assert.That(b.instanceId, Is.GreaterThan(a.instanceId));

            ledger.Remove(a.instanceId);
            var c = ledger.Place(201, FurnitureCatalog.WallArt);
            Assert.That(c.instanceId, Is.GreaterThan(b.instanceId), "删除后不复用 id");
            Assert.That(ledger.InRoom(201).Count, Is.EqualTo(2));
        }

        [Test]
        public void VisualVariant_IsStoredWithoutChangingFurnitureKind()
        {
            var ledger = new FurnitureLedger();
            FurnitureInstance sofa = ledger.Place(
                201, FurnitureCatalog.Sofa, visualVariantId: 2);

            Assert.That(sofa.kindId, Is.EqualTo(FurnitureCatalog.Sofa));
            Assert.That(sofa.visualVariantId, Is.EqualTo(2));
            Assert.That(ledger.TrySetVisualVariant(sofa.instanceId, 3), Is.True);
            Assert.That(sofa.visualVariantId, Is.EqualTo(3));
            Assert.That(ledger.TrySetVisualVariant(sofa.instanceId, -1), Is.False);
        }

        [Test]
        public void DerelictRoom_HasWorkingRequiredButTerribleDelivery()
        {
            var ledger = DerelictRoom();

            Assert.That(ledger.RequiredFurnitureWorking(201), Is.True, "床和卫浴都在，能卖");
            Assert.That(ledger.DeliveredQuality(201), Is.LessThan(0.1f),
                        "但交付水平极低——按 Basic 挂牌卖就等着退款");
        }

        [Test]
        public void FaultedRequiredFurniture_MakesRoomUnsellable()
        {
            var ledger = DerelictRoom();
            var bed = ledger.InRoom(201)[0];
            bed.faultLineIndex = 0;

            Assert.That(ledger.RequiredFurnitureWorking(201), Is.False, "床塌了不能卖房");

            ledger.Repair(bed.instanceId);
            Assert.That(ledger.RequiredFurnitureWorking(201), Is.True);
        }

        [Test]
        public void MissingRequiredFurniture_AlsoMakesRoomUnsellable()
        {
            var ledger = new FurnitureLedger();
            ledger.Place(201, FurnitureCatalog.ProperBed);   // 只有床，没卫浴
            Assert.That(ledger.RequiredFurnitureWorking(201), Is.False);
        }

        [Test]
        public void Repair_RestoresHealthOnly_NeverNewness()
        {
            // 这是用户第二轮要求的核心：崭新度只按衰减走，维修一动不动
            var ledger = DerelictRoom(newness: 0.12f, health: 0.05f);
            var bed = ledger.InRoom(201)[0];
            bed.faultLineIndex = 1;

            ledger.Repair(bed.instanceId);

            // **规则改了**（用户设计"修好的床还是有塌陷的可能"）：健康度不再回满，
            // 只回到"这件家具剩下的寿命"为止。修一张破床只能修成一张能用的破床。
            Assert.That(bed.health, Is.EqualTo(0.12f).Within(1e-4f), "健康度回到寿命上限，不是回满");
            Assert.That(bed.health, Is.LessThan(FurnitureWearModel.TroubleThreshold),
                        "崭新度 12% 的床修完仍在故障线以下——它几天后照样会坏");
            Assert.That(bed.IsFaulted, Is.False, "故障清除");
            Assert.That(bed.newness, Is.EqualTo(0.12f).Within(1e-4f), "崭新度一动不动");
            Assert.That(bed.patchedUp, Is.True, "将就过了：以后有塌的可能");
        }

        [Test]
        public void OnlyRefurbishment_ResetsNewness()
        {
            var ledger = DerelictRoom(newness: 0.08f, health: 0.2f);

            ledger.RefurbishRoom(201);

            foreach (var f in ledger.InRoom(201))
            {
                Assert.That(f.newness, Is.EqualTo(1f).Within(1e-4f), "翻新是唯一能重置崭新度的途径");
                Assert.That(f.health, Is.EqualTo(1f).Within(1e-4f));
            }
            Assert.That(ledger.DeliveredQuality(201), Is.GreaterThan(0.1f), "翻新后交付水平上升");
        }

        [Test]
        public void StandardRenovation_RefurbishesAndUpgradesRequiredOnly()
        {
            var ledger = DerelictRoom();
            ledger.Place(201, FurnitureCatalog.BoxyTv, newness: 0.1f, health: 0.3f);
            ledger.InRoom(201)[0].visualVariantId = 3;
            ledger.InRoom(201)[2].visualVariantId = 2;

            ledger.RefurbishAndUpgradeRequired(201);

            int beds = 0, tvs = 0;
            foreach (var f in ledger.InRoom(201))
            {
                var kind = FurnitureCatalog.Get(f.kindId);
                if (kind.slot == FurnitureSlot.Bed)
                {
                    beds++;
                    Assert.That(f.kindId, Is.EqualTo(FurnitureCatalog.ProperBed));
                    Assert.That(f.visualVariantId, Is.Zero, "品类升级后回到新家具的默认模型");
                }
                if (kind.slot == FurnitureSlot.Entertainment)
                {
                    tvs++;
                    Assert.That(f.kindId, Is.EqualTo(FurnitureCatalog.BoxyTv), "可选家具不升档");
                    Assert.That(f.visualVariantId, Is.EqualTo(2), "未换品类的外观配置应保留");
                }
                Assert.That(f.newness, Is.EqualTo(1f).Within(1e-4f), "但全部翻新");
            }
            Assert.That(beds, Is.EqualTo(1));
            Assert.That(tvs, Is.EqualTo(1));
        }

        [Test]
        public void RenovationCompletion_PreservesFurnitureChosenDuringThatJob()
        {
            var ledger = DerelictRoom();
            FurnitureInstance chosenBed = ledger.InRoom(201)[0];
            chosenBed.visualVariantId = 2;
            ledger.MarkSelectedForRenovation(chosenBed.instanceId);

            ledger.RefurbishAndUpgradeRequired(201);

            Assert.That(chosenBed.kindId, Is.EqualTo(FurnitureCatalog.SaggingBed));
            Assert.That(chosenBed.visualVariantId, Is.EqualTo(2));
            Assert.That(chosenBed.newness, Is.EqualTo(1f));

            ledger.ClearRenovationSelections(201);
            Assert.That(chosenBed.selectedForRenovation, Is.False);
        }

        [Test]
        public void LuxuryRenovation_FillsAroundFurnitureChosenDuringThatJob()
        {
            var ledger = DerelictRoom();
            FurnitureInstance chosenBed = ledger.InRoom(201)[0];
            ledger.MarkSelectedForRenovation(chosenBed.instanceId);

            ledger.ReplaceRoomWithTopTier(201);

            Assert.That(ledger.TryGet(chosenBed.instanceId, out FurnitureInstance preserved), Is.True);
            Assert.That(preserved.kindId, Is.EqualTo(FurnitureCatalog.SaggingBed));
            Assert.That(ledger.RequiredFurnitureWorking(201), Is.True);
            Assert.That(ledger.InRoom(201).Count, Is.EqualTo(6));
        }

        [Test]
        public void LuxuryRenovation_ReplacesEverythingWithTopTier()
        {
            var ledger = DerelictRoom();

            ledger.ReplaceRoomWithTopTier(201);

            Assert.That(ledger.DeliveredQuality(201), Is.EqualTo(1f).Within(1e-3f), "顶配打满交付");
            Assert.That(ledger.RequiredFurnitureWorking(201), Is.True);
            Assert.That(ledger.SegmentAppeal(201, GuestSegment.Vip), Is.GreaterThan(1f), "顶配讨 VIP 喜欢");
        }

        // ── 衰减 ─────────────────────────────────────────────────────────────

        [Test]
        public void GuestNight_DecaysBothNumbers()
        {
            var ledger = new FurnitureLedger();
            var sofa = ledger.Place(201, FurnitureCatalog.Sofa);

            ledger.ApplyGuestNight(201, segmentWear: 1f);

            Assert.That(sofa.newness, Is.LessThan(1f));
            Assert.That(sofa.health, Is.LessThan(1f));
        }

        [Test]
        public void PartyGuests_ChewThroughFurnitureFasterThanBusiness()
        {
            var partyRoom = new FurnitureLedger();
            var partySofa = partyRoom.Place(201, FurnitureCatalog.Sofa);
            var quietRoom = new FurnitureLedger();
            var quietSofa = quietRoom.Place(201, FurnitureCatalog.Sofa);

            float partyWear = GuestSegmentProfile.For(GuestSegment.Party).extraCleaningLoad;
            float businessWear = GuestSegmentProfile.For(GuestSegment.Business).extraCleaningLoad;
            for (int i = 0; i < 50; i++)
            {
                partyRoom.ApplyGuestNight(201, partyWear);
                quietRoom.ApplyGuestNight(201, businessWear);
            }

            Assert.That(partySofa.newness, Is.LessThan(quietSofa.newness), "派对客把家具用得更狠");
        }

        [Test]
        public void IdleRooms_StillAgeSlowly_SoHoardingEmptyRoomsDoesNotPreserveFurniture()
        {
            var ledger = new FurnitureLedger();
            var art = ledger.Place(201, FurnitureCatalog.WallArt);
            float before = art.newness;

            for (int day = 0; day < 100; day++) ledger.ApplyIdleDay();

            Assert.That(art.newness, Is.LessThan(before), "空置也老化——堵掉故意空置保鲜家具的漏洞");
            Assert.That(art.newness, Is.GreaterThan(0.9f), "但慢得多，正常游玩几乎无感");
        }

        [Test]
        public void FreshFurniture_StaysTroubleFreeForMonths()
        {
            // 验算表第一行：刚翻新的房应该清爽约 90 天（60% 入住率）
            var ledger = new FurnitureLedger();
            var bed = ledger.Place(201, FurnitureCatalog.ProperBed);
            int daysUntilTrouble = 0;
            for (int day = 1; day <= 200; day++)
            {
                if (day % 5 < 3) ledger.ApplyGuestNight(201, 1f);   // ≈60% 入住
                ledger.ApplyIdleDay();
                if (bed.health < FurnitureWearModel.TroubleThreshold) { daysUntilTrouble = day; break; }
            }
            Assert.That(daysUntilTrouble, Is.InRange(60, 140),
                        "翻新完应该有几个月太平（实测 " + daysUntilTrouble + " 天）——这是翻新的爽感");
        }

        [Test]
        public void DerelictFurniture_IsAlreadyInTroubleAndDiesFast()
        {
            // 验算表第二行：继承的破家具开局就在故障阈值内
            var ledger = DerelictRoom(newness: 0.1f, health: 0.35f);
            var bed = ledger.InRoom(201)[0];
            Assert.That(FurnitureWearModel.FaultChancePerDay(bed.health),
                        Is.GreaterThan(FurnitureWearModel.HealthyFaultChance * 0.9d));

            for (int day = 0; day < 14; day++)
            {
                ledger.ApplyGuestNight(201, 1f);
                ledger.ApplyIdleDay();
            }
            Assert.That(bed.health, Is.LessThan(FurnitureWearModel.TroubleThreshold),
                        "两周内破家具就在频繁出事的区间");
        }

        [Test]
        public void RollDailyFaults_OnlyFiresOnUnfaultedItems_AndIsRollDriven()
        {
            var ledger = DerelictRoom(newness: 0.05f, health: 0.0f);

            var faults = ledger.RollDailyFaults(() => 0.0d);   // 必中
            Assert.That(faults.Count, Is.EqualTo(2), "两件必备家具都坏了");
            foreach (var f in faults)
                Assert.That(FurnitureLedger.FaultLineOf(f), Is.Not.Empty, "有无厘头文案");

            var again = ledger.RollDailyFaults(() => 0.0d);
            Assert.That(again, Is.Empty, "已故障的不再重复触发");

            var healthy = new FurnitureLedger();
            healthy.Place(201, FurnitureCatalog.ProperBed);
            Assert.That(healthy.RollDailyFaults(() => 0.99d), Is.Empty, "高骰不出事");
        }

        [Test]
        public void AverageHealth_DrivesRoomWear()
        {
            var ledger = new FurnitureLedger();
            var a = ledger.Place(201, FurnitureCatalog.ProperBed);
            var b = ledger.Place(201, FurnitureCatalog.BasicBathroom);
            a.health = 0.2f;
            b.health = 0.8f;

            Assert.That(ledger.AverageHealth(201), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(ledger.AverageHealth(999), Is.EqualTo(1f), "没家具的房不算磨损");
        }

        [Test]
        public void SegmentAppeal_RewardsMatchingFurniture()
        {
            var ledger = new FurnitureLedger();
            ledger.Place(201, FurnitureCatalog.WorkDesk);
            ledger.Place(202, FurnitureCatalog.BigFlatTv);

            Assert.That(ledger.SegmentAppeal(201, GuestSegment.Business),
                        Is.GreaterThan(ledger.SegmentAppeal(201, GuestSegment.Party)), "书桌讨商务客喜欢");
            Assert.That(ledger.SegmentAppeal(202, GuestSegment.Party),
                        Is.GreaterThan(ledger.SegmentAppeal(202, GuestSegment.Business)), "大电视讨派对客喜欢");
        }

        [Test]
        public void RestoreInstance_RoundTripsIdsAndState()
        {
            var ledger = new FurnitureLedger();
            ledger.RestoreInstance(instanceId: 77, kindId: FurnitureCatalog.Sofa, roomNumber: 305,
                                   posX: 0.3f, posY: 0.7f, newness: 0.44f, health: 0.66f,
                                   faultLineIndex: 1, visualVariantId: 3);

            Assert.That(ledger.TryGet(77, out FurnitureInstance f), Is.True);
            Assert.That(f.newness, Is.EqualTo(0.44f).Within(1e-4f));
            Assert.That(f.health, Is.EqualTo(0.66f).Within(1e-4f));
            Assert.That(f.IsFaulted, Is.True);
            Assert.That(f.visualVariantId, Is.EqualTo(3));
            Assert.That(ledger.InRoom(305).Count, Is.EqualTo(1));

            var next = ledger.Place(305, FurnitureCatalog.Rug);
            Assert.That(next.instanceId, Is.GreaterThan(77), "读档后新买的家具不能撞历史 id");
        }
    }
}
