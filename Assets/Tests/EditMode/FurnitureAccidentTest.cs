using System.Collections.Generic;
using NUnit.Framework;

// 家具修理的天花板 + 塌陷伤人（用户设计）。
//
// 一句话规则：**修好之后的健康度 = 这件家具剩下的寿命**。
// 于是"修一张破床只能修成一张能用的破床，它几天后照样会坏"是现有磨损曲线的
// 自然结果，不需要脆弱标记或第二套计时器。一直修不换，早晚塌，塌了会伤人。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FurnitureAccidentTest
    {
        private static HotelSim BuildHotel(int rooms = 6, int startingCash = 20000, int seed = 4242)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            staff.Register(new StaffMember(StaffRole.Housekeeper, "H", 60, new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, startingCash, seed);
            sim.FurnishInheritedRooms();
            return sim;
        }

        // ── 修理的天花板：一条能一句话说清的规则 ──────────────────────────────

        [Test]
        public void RepairOnlyBringsItBackAsGoodAsItIsOld()
        {
            // 崭新度就是天花板。这条规则玩家能一句话听懂，而且它自己产出了
            // "修好的床几天内还会塌"——不需要额外的脆弱标记。
            Assert.That(FurnitureWearModel.RepairedHealthCeiling(1f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(FurnitureWearModel.RepairedHealthCeiling(0.8f), Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(FurnitureWearModel.RepairedHealthCeiling(0.1f), Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(FurnitureWearModel.RepairedHealthCeiling(0f),
                        Is.EqualTo(FurnitureWearModel.RepairedHealthFloor).Within(1e-4f),
                        "崭新度归零也留一点下限——修一下顶两天是破产玩家的自救路之一");
        }

        [Test]
        public void ARepairedDerelictPieceIsStillBelowTheTroubleLine()
        {
            // 这是整套设计的支点：继承来的破家具（崭新度 5-15%）修完仍在故障线
            // （0.30）以下，于是既有的 FaultChancePerDay 已经落在陡峭段。
            foreach (float newness in new[] { 0.05f, 0.10f, 0.15f })
                Assert.That(FurnitureWearModel.RepairedHealthCeiling(newness),
                            Is.LessThan(FurnitureWearModel.TroubleThreshold),
                            "崭新度 " + newness + " 修完必须仍在故障线以下");
        }

        [Test]
        public void RepairingHealthyFurnitureIsStillACleanFix()
        {
            // **爱惜家具的人不该被惩罚**：偶发故障的新家具修完照旧健康
            Assert.That(FurnitureWearModel.RepairedHealthCeiling(0.95f),
                        Is.GreaterThan(FurnitureWearModel.TroubleThreshold));
        }

        [Test]
        public void RepairNeverPushesHealthDownwards()
        {
            // 健康家具偶发故障时，修理不该把它按到天花板那么低
            var sim = BuildHotel();
            var bed = sim.Furniture.InRoom(201)[0];
            bed.newness = 0.10f;      // 老骨架
            bed.health = 0.90f;       // 但此刻状态很好（偶发故障）
            bed.faultLineIndex = 0;

            sim.Furniture.Repair(bed.instanceId);

            Assert.That(bed.health, Is.EqualTo(0.90f).Within(1e-4f), "修理只往上抬，不往下压");
            Assert.That(bed.IsFaulted, Is.False);
        }

        [Test]
        public void RepairStillNeverTouchesNewness()
        {
            // 铁律没变：维修只回健康度，崭新度只有翻新性装修能重置
            var sim = BuildHotel();
            var bed = sim.Furniture.InRoom(201)[0];
            float newnessBefore = bed.newness;
            bed.health = 0.01f;
            bed.faultLineIndex = 0;

            sim.Furniture.Repair(bed.instanceId);

            Assert.That(bed.newness, Is.EqualTo(newnessBefore).Within(1e-6f), "崭新度一动不动");
        }

        // ── 塌陷：只惩罚"一直糊着不换" ────────────────────────────────────────

        [Test]
        public void HealthyFurnitureNeverCollapses()
        {
            // 经营良好的酒店**永远见不到这套机制**
            Assert.That(FurnitureWearModel.CollapseChancePerNight(1f, taped: false), Is.EqualTo(0d));
            Assert.That(FurnitureWearModel.CollapseChancePerNight(0.5f, taped: false), Is.EqualTo(0d));
            Assert.That(FurnitureWearModel.CollapseChancePerNight(
                            FurnitureWearModel.CollapseHealthThreshold, taped: true), Is.EqualTo(0d),
                        "刚好在阈值上也不塌");
        }

        [Test]
        public void TheWorseItGetsTheMoreLikelyItGivesWay_AndTapeMakesItWorse()
        {
            double bad = FurnitureWearModel.CollapseChancePerNight(0.10f, taped: false);
            double worse = FurnitureWearModel.CollapseChancePerNight(0.02f, taped: false);
            double taped = FurnitureWearModel.CollapseChancePerNight(0.10f, taped: true);

            Assert.That(bad, Is.GreaterThan(0d));
            Assert.That(worse, Is.GreaterThan(bad), "越烂越容易塌");
            Assert.That(taped, Is.GreaterThan(bad), "胶带撑不住一个人的体重");
            Assert.That(worse, Is.LessThanOrEqualTo(FurnitureWearModel.MaxCollapseChancePerNight),
                        "有上限——这该是记得住的事故，不是每晚抽奖");
        }

        [Test]
        public void OnlyWeightBearingThingsCanHurtSomebody()
        {
            // 床/卫浴/沙发塌下来会伤人；电视挂画坏了只是难看
            Assert.That(FurnitureCatalog.BearsWeight(FurnitureSlot.Bed), Is.True);
            Assert.That(FurnitureCatalog.BearsWeight(FurnitureSlot.Bathroom), Is.True);
            Assert.That(FurnitureCatalog.BearsWeight(FurnitureSlot.Seating), Is.True);
            Assert.That(FurnitureCatalog.BearsWeight(FurnitureSlot.Entertainment), Is.False);
            Assert.That(FurnitureCatalog.BearsWeight(FurnitureSlot.Wall), Is.False);
        }

        [Test]
        public void AtMostOneAccidentPerDay()
        {
            // **这是"记得住的事故"和"死亡螺旋"之间的那条线**
            Assert.That(HotelSim.MaxInjuriesPerDay, Is.EqualTo(1));
        }

        [Test]
        public void CompensationHurtsForADayOrTwo_NotForTheRun()
        {
            // 用户明确要求"赔偿不要太狠"。12 间房日毛收入约 $500。
            Assert.That(HotelSim.MinInjuryCompensation, Is.GreaterThan(0));
            Assert.That(HotelSim.MaxInjuryCompensation, Is.LessThanOrEqualTo(900),
                        "上限不能高到一次事故就终结一局");
            Assert.That(HotelSim.MaxInjuryCompensation,
                        Is.GreaterThan(HotelSim.MinInjuryCompensation));
        }

        // ── 塌掉之后：修不动，但永远还有出路 ──────────────────────────────────

        [Test]
        public void AWreckedPieceCannotBeRepaired()
        {
            var sim = BuildHotel();
            var bed = sim.Furniture.InRoom(201)[0];
            bed.faultLineIndex = 0;
            bed.wrecked = true;

            Assert.That(sim.TryRepairFurniture(bed.instanceId, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty, "而且要告诉玩家为什么");
            Assert.That(bed.IsRepairable, Is.False);
        }

        [Test]
        public void AWreckedPieceCanStillBeTaped_SoTheRoomIsNeverLostForever()
        {
            // **永不锁死玩家**：塌了修不动，但胶带永远可用（同"清垃圾免费"）。
            // 没有这一条，现金归零 + 一次塌陷就等于永久少一间房。
            var sim = BuildHotel();
            var bed = sim.Furniture.InRoom(201)[0];
            bed.faultLineIndex = 0;
            bed.wrecked = true;

            Assert.That(sim.TryTapeFurniture(bed.instanceId, out string reason), Is.True, reason);
            Assert.That(bed.IsUsable, Is.True, "糊上就还能卖");
        }

        [Test]
        public void TheConditionIsSayableInWords_NotJustAFloat()
        {
            // 整套机制都建立在健康度上，而加这套机制之前三个面板 grep "health"
            // 是零命中——数字不显示，后果就全是"莫名其妙发生的事"
            Assert.That(FurnitureLedger.ConditionWord(1f, false), Is.Not.Empty);
            Assert.That(FurnitureLedger.ConditionWord(0.05f, false),
                        Is.Not.EqualTo(FurnitureLedger.ConditionWord(1f, false)),
                        "快塌了和结实必须是不同的词");
            Assert.That(FurnitureLedger.ConditionWord(0.05f, true), Is.EqualTo("WRECKED"));
        }

        [Test]
        public void WorstHealthInARoom_IsWhatTheUiShouldShow()
        {
            var sim = BuildHotel();
            var items = sim.Furniture.InRoom(201);
            Assert.That(items.Count, Is.GreaterThan(1), "这条测试需要房里有多件家具");
            items[0].health = 0.9f;
            items[1].health = 0.1f;

            Assert.That(sim.Furniture.WorstHealthIn(201), Is.EqualTo(0.1f).Within(1e-4f),
                        "一间房的状况由最差那件决定");
            Assert.That(sim.Furniture.WorstHealthIn(999), Is.EqualTo(1f), "没有的房安全返回");
        }

        // ── 隔离：事故的掷骰不许打乱既有种子实验 ──────────────────────────────

        [Test]
        public void TheAccidentRollDoesNotDisturbTheMainRandomStream()
        {
            // 主 _rng 的抽取次序被十几个种子测试钉着。同一个种子跑同一天，
            // 结果必须逐字一致——否则加这个功能等于把所有对照实验重排。
            var a = BuildHotel(seed: 777);
            var b = BuildHotel(seed: 777);

            foreach (var sim in new[] { a, b })
            {
                sim.BeginDay();
                sim.RunToEndOfDay();
                sim.SettleDay();
            }

            Assert.That(a.ArrivalsCheckedInToday, Is.EqualTo(b.ArrivalsCheckedInToday));
            Assert.That(a.GrossIncomeToday, Is.EqualTo(b.GrossIncomeToday));
            Assert.That(a.CheckoutsToday, Is.EqualTo(b.CheckoutsToday));
            Assert.That(a.Reputation.AverageSatisfaction,
                        Is.EqualTo(b.Reputation.AverageSatisfaction).Within(1e-6f));
        }
    }
}
