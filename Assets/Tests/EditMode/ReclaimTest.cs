using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 破败房复原（用户设计）：**破败房不是脏，是废**，只能靠施工开出来。
// 断的是三档各自的取舍结构，不是具体造价——数值都是旋钮。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class ReclaimTest
    {
        private static HotelSim BuildHotel(int ruinedRooms = 6, int startingCash = 40000, int seed = 31337)
        {
            var defs = new List<RoomDefinition>();
            // 一间可用（不然开局没有任何收入来源），其余破败
            defs.Add(new RoomDefinition(201, 1, 1, Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready));
            for (int i = 0; i < ruinedRooms; i++)
                defs.Add(new RoomDefinition(202 + i, 1, 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ruined));

            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            for (int i = 0; i < 2; i++)
                staff.Register(new StaffMember(StaffRole.Housekeeper, "H" + i, 60, new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, startingCash, seed);
            sim.Materials.Add(80);
            return sim;
        }

        private static void RunDays(HotelSim sim, int days)
        {
            for (int d = 0; d < days; d++)
            {
                sim.BeginDay();
                sim.RunToEndOfDay();
                sim.SettleDay();
                sim.Clock.BeginNextDay();
            }
        }

        // ── 破败房只能走施工 ──────────────────────────────────────────────────

        [Test]
        public void NormalRenovation_StillRefusesDerelictRooms()
        {
            var sim = BuildHotel();

            Assert.That(sim.TryStartRenovation(RenovationPlanKind.Standard, new[] { 202 }, out string why),
                        Is.False, "破败房不能当普通装修下单——它连家具都没有");
            Assert.That(why, Is.Not.Empty);
        }

        [Test]
        public void ReclaimableRooms_OnlyListsDerelictOnesNotAlreadyStarted()
        {
            var sim = BuildHotel(ruinedRooms: 4);

            var first = sim.ReclaimableRooms(10);
            Assert.That(first.Count, Is.EqualTo(4));
            Assert.That(first, Has.No.Member(201), "已经在营业的房不是复原对象");

            Assert.That(sim.TryStartReclaim(ReclaimPlanKind.PatchUp, new[] { first[0] }, out string why),
                        Is.True, why);
            var second = sim.ReclaimableRooms(10);
            Assert.That(second, Has.No.Member(first[0]), "已开工的房不该再出现在候选里");
            Assert.That(second.Count, Is.EqualTo(3));
        }

        [Test]
        public void StartingWork_CostsCashAndMaterialsUpFront()
        {
            var sim = BuildHotel();
            var plan = ReclaimPlan.For(ReclaimPlanKind.Refit);
            var rooms = sim.ReclaimableRooms(2);
            int cashBefore = sim.Cash, materialsBefore = sim.Materials.Stock;

            Assert.That(sim.TryStartReclaim(ReclaimPlanKind.Refit, rooms, out string why), Is.True, why);

            Assert.That(sim.Cash, Is.EqualTo(cashBefore - ReclaimPricing.CashCostFor(plan, rooms.Count)));
            Assert.That(sim.Materials.Stock,
                        Is.EqualTo(materialsBefore - ReclaimPricing.MaterialCostFor(plan, rooms.Count)));
        }

        [Test]
        public void WithoutMaterialsOrCash_TheWholeOrderFails()
        {
            var broke = BuildHotel(startingCash: 100);
            Assert.That(broke.TryStartReclaim(ReclaimPlanKind.FullFit, broke.ReclaimableRooms(1),
                                              out string cashWhy), Is.False);
            Assert.That(cashWhy, Is.Not.Empty);
            Assert.That(broke.Renovations.RoomsUnderReclaim, Is.EqualTo(0), "不做部分成交");

            var noStock = BuildHotel();
            noStock.Materials.TryConsume(noStock.Materials.Stock);
            Assert.That(noStock.TryStartReclaim(ReclaimPlanKind.Refit, noStock.ReclaimableRooms(1),
                                                out string matWhy), Is.False);
            Assert.That(matWhy, Is.Not.Empty);
        }

        [Test]
        public void RoomStaysUnsellableWhileTheBuildersAreIn()
        {
            var sim = BuildHotel();
            var rooms = sim.ReclaimableRooms(1);
            sim.TryStartReclaim(ReclaimPlanKind.Refit, rooms, out _);
            int room = rooms[0];

            RunDays(sim, 1);

            Assert.That(sim.Rooms.At(room).state, Is.EqualTo(RoomSimState.Ruined),
                        "施工期间房还是破败的——不可售");
            Assert.That(sim.Renovations.IsReclaiming(room), Is.True);
        }

        // ── 三档的产出差别在家具上 ────────────────────────────────────────────

        /// <summary>开工并跑到完工，返回完工后的交付水平。</summary>
        private static float ReclaimAndMeasure(ReclaimPlanKind kind, out HotelSim sim, out int room)
        {
            sim = BuildHotel();
            var rooms = sim.ReclaimableRooms(1);
            room = rooms[0];
            Assert.That(sim.TryStartReclaim(kind, rooms, out string why), Is.True, why);

            var plan = ReclaimPlan.For(kind);
            RunDays(sim, ReclaimPricing.BlockDaysFor(plan, 1) + 1);
            return sim.DeliveredQualityOf(room);
        }

        [Test]
        public void PatchUp_MakesTheRoomUsableButOnlyGoodEnoughForOld()
        {
            float delivered = ReclaimAndMeasure(ReclaimPlanKind.PatchUp, out HotelSim sim, out int room);

            Assert.That(sim.Rooms.At(room).state, Is.Not.EqualTo(RoomSimState.Ruined), "房该开出来了");
            Assert.That(sim.Furniture.RequiredFurnitureWorking(room), Is.True,
                        "维修工把旧家具修到能用了（健康度回满）");

            // 崭新度**没有**回，所以交付垫底：诚实挂牌只能挂 Old
            Assert.That(delivered, Is.GreaterThanOrEqualTo(DemandModel.ExpectedQualityOf(RoomTier.Old)),
                        "按 Old 卖是诚实的");
            Assert.That(delivered, Is.LessThan(DemandModel.ExpectedQualityOf(RoomTier.Basic)),
                        "修旧家具不该白得 Basic 的资格——想提档得再花钱换家具");
        }

        [Test]
        public void PatchUp_DoesNotResetNewness()
        {
            ReclaimAndMeasure(ReclaimPlanKind.PatchUp, out HotelSim sim, out int room);

            foreach (var f in sim.Furniture.InRoom(room))
            {
                Assert.That(f.newness, Is.LessThan(0.5f),
                            "「维修只回健康度、不回崭新度」是铁律——只有翻新性装修能重置它");
                Assert.That(f.IsFaulted, Is.False, "但东西得是能用的");
            }
        }

        [Test]
        public void Refit_DeliversEnoughToChargeBasic()
        {
            float delivered = ReclaimAndMeasure(ReclaimPlanKind.Refit, out HotelSim sim, out int room);

            Assert.That(delivered,
                        Is.GreaterThanOrEqualTo(DemandModel.ExpectedQualityOf(RoomTier.Basic) - 0.02f),
                        "换了新床和新卫浴，就该能诚实挂 Basic");
            Assert.That(delivered, Is.LessThan(DemandModel.ExpectedQualityOf(RoomTier.Better)),
                        "只换必备不该直通 Better");
            foreach (var f in sim.Furniture.InRoom(room))
                Assert.That(f.newness, Is.GreaterThan(0.9f), "换新的就是新的");
        }

        [Test]
        public void FullFit_DeliversEnoughToChargeBetter()
        {
            float delivered = ReclaimAndMeasure(ReclaimPlanKind.FullFit, out HotelSim sim, out int room);

            Assert.That(delivered, Is.GreaterThanOrEqualTo(DemandModel.ExpectedQualityOf(RoomTier.Better)),
                        "全套换新够挂 Better");
            Assert.That(sim.Furniture.InRoom(room).Count,
                        Is.GreaterThan(2), "全套不只有床和卫浴");
        }

        [Test]
        public void TheThreeTiersTradeCashAgainstTimeAndQuality()
        {
            var patch = ReclaimPlan.For(ReclaimPlanKind.PatchUp);
            var refit = ReclaimPlan.For(ReclaimPlanKind.Refit);
            var full = ReclaimPlan.For(ReclaimPlanKind.FullFit);

            // 用户要求："相比于装修要更省"
            Assert.That(patch.cashPerRoom, Is.LessThan(refit.cashPerRoom));
            Assert.That(refit.cashPerRoom, Is.LessThan(full.cashPerRoom));
            Assert.That(patch.cashPerRoom,
                        Is.LessThan(RenovationPlan.For(RenovationPlanKind.Economy).cashPerRoom),
                        "修旧家具要比最便宜的装修方案还省");

            // 花钱买速度
            Assert.That(patch.blockDays, Is.GreaterThan(refit.blockDays));
            Assert.That(refit.blockDays, Is.GreaterThan(full.blockDays));

            Assert.That(patch.keepsOldFurniture, Is.True);
            Assert.That(refit.keepsOldFurniture, Is.False);
            Assert.That(full.fitsOptionalSlots, Is.True);
        }

        // ── 完工后交给客房部（这就是"和 HSK 冲突吗"的答案）─────────────────────

        [Test]
        public void FinishedWork_HandsTheRoomToHousekeepingNotStraightToSale()
        {
            var sim = BuildHotel();
            var rooms = sim.ReclaimableRooms(1);
            int room = rooms[0];
            sim.TryStartReclaim(ReclaimPlanKind.FullFit, rooms, out _);

            var plan = ReclaimPlan.For(ReclaimPlanKind.FullFit);
            // 正好跑到完工的那一天日结，此时还没经过客房部
            for (int d = 0; d < plan.blockDays; d++)
            {
                sim.BeginDay();
                sim.RunToEndOfDay();
                sim.SettleDay();
                sim.Clock.BeginNextDay();
            }

            Assert.That(sim.Rooms.At(room).state, Is.EqualTo(RoomSimState.Dirty),
                        "施工完是脏房——总得打扫一遍才能卖，所以复原也会占用客房部工时");
        }

        [Test]
        public void AfterCleaning_TheReclaimedRoomActuallySells()
        {
            var sim = BuildHotel(ruinedRooms: 2);
            var rooms = sim.ReclaimableRooms(1);
            int room = rooms[0];
            sim.TryStartReclaim(ReclaimPlanKind.Refit, rooms, out _);

            RunDays(sim, ReclaimPricing.BlockDaysFor(ReclaimPlan.For(ReclaimPlanKind.Refit), 1) + 2);

            Assert.That(sim.Rooms.At(room).state, Is.Not.EqualTo(RoomSimState.Ruined));
            Assert.That(sim.Rooms.At(room).state, Is.Not.EqualTo(RoomSimState.Dirty),
                        "客房部该把它打扫完了");
            Assert.That(sim.Rooms.OpenRoomCount, Is.GreaterThan(1), "房量真的涨了");
        }

        // ── 在建工单必须进存档（既有缺陷）─────────────────────────────────────

        [Test]
        public void WorkInProgressSurvivesASaveAndLoad()
        {
            var original = BuildHotel(ruinedRooms: 3);
            var rooms = original.ReclaimableRooms(2);
            Assert.That(original.TryStartReclaim(ReclaimPlanKind.PatchUp, rooms, out string why), Is.True, why);
            original.BeginDay(); original.RunToEndOfDay(); original.SettleDay();

            int daysLeftBefore = original.Renovations.DaysRemainingFor(rooms[0]);
            Assert.That(daysLeftBefore, Is.GreaterThan(0), "工单还在建");

            var state = new SimState();
            original.CaptureTo(state);
            Assert.That(state.buildJobs.Count, Is.EqualTo(1), "在建工单必须入档");

            // 过一遍 JSON，确认 DTO 真能序列化
            var back = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(new GameState { sim = state }));
            var restored = BuildHotel(ruinedRooms: 3);
            restored.RestoreFrom(back.sim);

            Assert.That(restored.Renovations.RoomsUnderReclaim, Is.EqualTo(2),
                        "花了钱的工单不能在读档时凭空消失");
            Assert.That(restored.Renovations.DaysRemainingFor(rooms[0]), Is.EqualTo(daysLeftBefore),
                        "剩余工期也要一致");
            Assert.That(restored.Renovations.IsReclaiming(rooms[0]), Is.True, "而且要记得它是复原单");
        }

        [Test]
        public void RoomStatesSurviveASaveAndLoad()
        {
            var original = BuildHotel(ruinedRooms: 3);
            original.BeginDay(); original.RunToEndOfDay(); original.SettleDay();
            int ruinedBefore = original.Rooms.CountOf(RoomSimState.Ruined);

            var state = new SimState();
            original.CaptureTo(state);
            Assert.That(state.roomStates.Count, Is.EqualTo(original.Rooms.Count));

            var back = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(new GameState { sim = state }));
            var restored = BuildHotel(ruinedRooms: 3);
            restored.RestoreFrom(back.sim);

            Assert.That(restored.Rooms.CountOf(RoomSimState.Ruined), Is.EqualTo(ruinedBefore),
                        "房态以前完全没存，读档后破败房会变成可售");
        }

        [Test]
        public void RestoredJobsDoNotCollideWithNewOnes()
        {
            var original = BuildHotel(ruinedRooms: 4);
            original.TryStartReclaim(ReclaimPlanKind.PatchUp, original.ReclaimableRooms(1), out _);
            var state = new SimState();
            original.CaptureTo(state);

            var restored = BuildHotel(ruinedRooms: 4);
            restored.RestoreFrom(state);
            int existingId = restored.Renovations.Active[0].jobId;

            restored.TryStartReclaim(ReclaimPlanKind.PatchUp, restored.ReclaimableRooms(1), out _);

            foreach (var job in restored.Renovations.Active)
                if (job != restored.Renovations.Active[0])
                    Assert.That(job.jobId, Is.Not.EqualTo(existingId), "新工单不能撞历史工单号");
        }

        [Test]
        public void V6Save_LoadsWithNoJobsAndDefaultRoomStates()
        {
            string v6 = "{\"version\":6," +
                "\"economy\":{\"cash\":700,\"loanBalance\":100,\"loanRate\":0,\"staff\":[],\"reputationSamples\":[]}," +
                "\"renovation\":{\"totalRooms\":12,\"startingRoomNumber\":201,\"rooms\":[],\"jobs\":[]}," +
                "\"progress\":{\"day\":5,\"satisfaction\":20}," +
                "\"rooms\":{\"occupied\":[]}," +
                "\"world\":{\"prestige\":3,\"tapedBreakdowns\":[],\"lockedRooms\":[]}," +
                "\"sim\":{\"day\":5,\"minute\":480,\"cash\":700,\"materialStock\":4,\"staff\":[]," +
                "\"furniture\":[],\"roomBands\":[],\"reservations\":[]}}";

            var gs = JsonUtility.FromJson<GameState>(v6);
            gs.MigrateToCurrentVersion();

            Assert.That(gs.version, Is.EqualTo(GameState.CurrentVersion));
            Assert.That(gs.sim.materialStock, Is.EqualTo(4), "v6 的数据不能丢");
            Assert.That(gs.sim.buildJobs, Is.Not.Null.And.Empty, "工单段补空表");
            Assert.That(gs.sim.roomStates, Is.Not.Null.And.Empty, "房态段补空表");

            // 空的房态表不该把现有房态清成 0（Ruined）——只在有数据时才覆盖
            var sim = BuildHotel(ruinedRooms: 3);
            int readyBefore = sim.Rooms.CountOf(RoomSimState.Ready);
            sim.RestoreFrom(gs.sim);
            Assert.That(sim.Rooms.CountOf(RoomSimState.Ready), Is.EqualTo(readyBefore),
                        "老档没有房态数据时，不能把房间全刷成破败");
        }
    }
}
