using System.Collections.Generic;
using NUnit.Framework;

// v3 房态总账：100 间房的纯数据权威 + 分区聚合池。
// 重点验收：ref 访问器真的改到原记录（struct 副本陷阱）、聚合计数正确、破败房不算可售。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class RoomLedgerTest
    {
        private static RoomLedger BuildSmallHotel()
        {
            // 2 层 × 4 间：2F 全可用，3F 全破败（模拟"开局只有极少数房能用"）
            var defs = new List<RoomDefinition>();
            for (int i = 1; i <= 4; i++)
                defs.Add(new RoomDefinition(200 + i, floor: 1, zone: 1, Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready));
            for (int i = 1; i <= 4; i++)
                defs.Add(new RoomDefinition(300 + i, floor: 2, zone: 2, Room2DRoomCategory.Twin, RoomTier.Old, RoomSimState.Ruined));
            return new RoomLedger(defs);
        }

        [Test]
        public void Build_IndexesRoomsAndCountsStates()
        {
            var ledger = BuildSmallHotel();

            Assert.That(ledger.Count, Is.EqualTo(8));
            Assert.That(ledger.CountOf(RoomSimState.Ready), Is.EqualTo(4));
            Assert.That(ledger.CountOf(RoomSimState.Ruined), Is.EqualTo(4));
            Assert.That(ledger.OpenRoomCount, Is.EqualTo(4), "破败房不算营业房量");
            Assert.That(ledger.SellableCount, Is.EqualTo(4));
        }

        [Test]
        public void RefAccessor_MutatesTheRealRecord_NotACopy()
        {
            var ledger = BuildSmallHotel();

            ref RoomRecord room = ref ledger.At(201);
            room.wear = 0.42f;
            room.occupantResvId = 987654321; // int 而非 short：预订 id 单调递增不能溢出

            Assert.That(ledger.At(201).wear, Is.EqualTo(0.42f).Within(1e-5f),
                        "ref 访问器必须改到原记录（struct 副本陷阱）");
            Assert.That(ledger.At(201).occupantResvId, Is.EqualTo(987654321));
        }

        [Test]
        public void SetState_UpdatesAggregatesImmediately()
        {
            var ledger = BuildSmallHotel();

            ledger.SetState(201, RoomSimState.Occupied);
            ledger.SetState(202, RoomSimState.Dirty);

            Assert.That(ledger.CountOf(RoomSimState.Ready), Is.EqualTo(2));
            Assert.That(ledger.CountOf(RoomSimState.Occupied), Is.EqualTo(1));
            Assert.That(ledger.CountOf(RoomSimState.Dirty), Is.EqualTo(1));
            Assert.That(ledger.SellableCount, Is.EqualTo(2), "只有 Ready 算可售");
            Assert.That(ledger.DirtyBacklog, Is.EqualTo(1));
        }

        [Test]
        public void CleaningPipeline_FlowsDirtyToReadyThroughInspection()
        {
            var ledger = BuildSmallHotel();
            ledger.SetState(201, RoomSimState.Dirty);

            ledger.SetState(201, RoomSimState.Cleaning);
            Assert.That(ledger.DirtyBacklog, Is.EqualTo(0), "在清洁中不再算积压");

            ledger.SetState(201, RoomSimState.AwaitingInspection);
            Assert.That(ledger.CountOf(RoomSimState.AwaitingInspection), Is.EqualTo(1));
            Assert.That(ledger.SellableCount, Is.EqualTo(3), "待检房还不可售");

            ledger.SetState(201, RoomSimState.Ready);
            Assert.That(ledger.SellableCount, Is.EqualTo(4));
        }

        [Test]
        public void ZoneAggregate_CountsPerZone()
        {
            var ledger = BuildSmallHotel();
            ledger.SetState(201, RoomSimState.Dirty);
            ledger.SetState(202, RoomSimState.Dirty);

            var zone1 = ledger.AggregateForZone(1);
            Assert.That(zone1.zone, Is.EqualTo(1));
            Assert.That(zone1.total, Is.EqualTo(4));
            Assert.That(zone1.CountOf(RoomSimState.Dirty), Is.EqualTo(2));
            Assert.That(zone1.CountOf(RoomSimState.Ready), Is.EqualTo(2));

            var zone2 = ledger.AggregateForZone(2);
            Assert.That(zone2.CountOf(RoomSimState.Ruined), Is.EqualTo(4));
            Assert.That(zone2.CountOf(RoomSimState.Dirty), Is.EqualTo(0));
        }

        [Test]
        public void Flags_ProblemAndRenovatingAutoSurface()
        {
            var ledger = BuildSmallHotel();

            Assert.That(ledger.IsSurfaced(201), Is.False, "普通房走聚合池，不单独浮出");

            ledger.AddFlags(201, RoomFlags.Problem);
            Assert.That(ledger.IsSurfaced(201), Is.True, "问题房必须浮出给玩家单独处理");

            ledger.RemoveFlags(201, RoomFlags.Problem);
            Assert.That(ledger.IsSurfaced(201), Is.False);

            ledger.AddFlags(203, RoomFlags.Vip);
            Assert.That(ledger.IsSurfaced(203), Is.True, "VIP 房逐房处理");

            ledger.AddFlags(204, RoomFlags.Renovating);
            Assert.That(ledger.SurfacedRoomNumbers, Is.EquivalentTo(new[] { 203, 204 }));
        }

        [Test]
        public void Renovation_BlocksRoomAndRemovesItFromSellable()
        {
            var ledger = BuildSmallHotel();
            int sellableBefore = ledger.SellableCount;

            ledger.SetState(201, RoomSimState.Blocked);
            ledger.AddFlags(201, RoomFlags.Renovating);

            Assert.That(ledger.SellableCount, Is.EqualTo(sellableBefore - 1));
            Assert.That(ledger.BlockedCount, Is.EqualTo(1));
            Assert.That(ledger.OpenRoomCount, Is.EqualTo(4), "装修中仍属营业房量，只是当期不可售");
        }

        [Test]
        public void UnlockRuinedRoom_TurnsItIntoDirtyOpenRoom()
        {
            var ledger = BuildSmallHotel();

            bool ok = ledger.TryUnlockRuinedRoom(301);

            Assert.That(ok, Is.True);
            Assert.That(ledger.At(301).state, Is.EqualTo(RoomSimState.Dirty), "解锁后先是脏房，得打扫");
            Assert.That(ledger.OpenRoomCount, Is.EqualTo(5));
            Assert.That(ledger.TryUnlockRuinedRoom(301), Is.False, "已解锁的房不能再解锁");
            Assert.That(ledger.TryUnlockRuinedRoom(999), Is.False, "不存在的房号安全失败");
        }

        [Test]
        public void UnknownRoomNumber_FailsSafely()
        {
            var ledger = BuildSmallHotel();

            Assert.That(ledger.Contains(999), Is.False);
            Assert.That(ledger.SetState(999, RoomSimState.Dirty), Is.False);
            Assert.That(ledger.IsSurfaced(999), Is.False);
            Assert.DoesNotThrow(() => ledger.AddFlags(999, RoomFlags.Problem));
        }

        [Test]
        public void StateMapping_RoundTripsWithLegacyRoom2DState()
        {
            // 镜像期：Sim 是权威，Room2DEntity 是它的视图，两个枚举必须无损互转
            foreach (Room2DState legacy in System.Enum.GetValues(typeof(Room2DState)))
            {
                RoomSimState sim = RoomStateMapping.FromLegacy(legacy);
                Assert.That(RoomStateMapping.ToLegacy(sim), Is.EqualTo(legacy), "往返丢失：" + legacy);
            }

            // Ruined 是 v3 新增的（v1 没有"破败未解锁"概念）：退化为 Blocked 给旧视图看
            Assert.That(RoomStateMapping.ToLegacy(RoomSimState.Ruined), Is.EqualTo(Room2DState.Blocked));
        }
    }
}
