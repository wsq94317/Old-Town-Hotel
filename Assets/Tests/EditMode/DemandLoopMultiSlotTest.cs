using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// EditMode 测试:Story 3 Phase 2 demand loop multi-slot schema 回归。
//
// 测试隔离策略:
//   1. [SetUp] 固定 seed + new GameObject + AddComponent<Room2DPrototypeDemandLoop>()
//      —— Awake 立即跑,EnsureQueuesInitialised 自动调用,upcoming queue 已就绪
//   2. 部分测试需要 Room2DEntity fixture,在测试内 new + DestroyImmediate
//   3. [TearDown] DestroyImmediate(GameObject)
//
// 命名风格:Test_PascalCase(与 Story 1/2 一致;项目级 TD-005 已记录)。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DemandLoopMultiSlotTest
    {
        private const int DETERMINISTIC_SEED = 67890;
        private const int DISTRIBUTION_SAMPLE_SIZE = 200;

        private GameObject _go;
        private Room2DPrototypeDemandLoop _loop;

        [SetUp]
        public void SetUp()
        {
            Random.InitState(DETERMINISTIC_SEED);
            _go = new GameObject("TestDemandLoopForMultiSlot");
            _loop = _go.AddComponent<Room2DPrototypeDemandLoop>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── helper:创建一个测试 Room2DEntity 并设 RoomCategory ─────────────────

        private static Room2DEntity CreateRoom(string name, Room2DRoomCategory category)
        {
            var go = new GameObject(name);
            var room = go.AddComponent<Room2DEntity>();
            room.roomName = name;
            // roomCategory 是 public 字段(对齐项目惯例),直接赋值。
            room.roomCategory = category;
            return room;
        }

        // ── AC8.1:Schedule 后队列填到 capacity ──────────────────────────────

        [Test]
        public void Test_ScheduleUpcomingDemandPreview_FillsTwoSlots()
        {
            // Arrange / Act:Awake 已经触发了 ScheduleUpcomingDemandPreview;
            // 但保险起见再显式调一次(幂等)。
            _loop.ScheduleUpcomingDemandPreview();

            // Assert:UpcomingQueueCount 应等于配置的 capacity(默认 2)。
            Assert.That(_loop.UpcomingQueueCount, Is.EqualTo(2),
                "Schedule 后 upcoming queue 应填满到 upcomingQueueCapacity(=2);"
                + "当前 count=" + _loop.UpcomingQueueCount);
        }

        // ── AC8.2:Activate 后 slot 0 被 pop,其余 slot 左移 ────────────────

        [Test]
        public void Test_ActivateUpcomingDemand_PopsSlotZero_RestShiftsLeft()
        {
            // Arrange:记录 schedule 后 slot 1 的客人 type(Activate 后它应该变成新 slot 0)。
            _loop.ScheduleUpcomingDemandPreview();
            Room2DGuestType originalSlot1Type = _loop.GetUpcomingGuestType(1);

            // Act:激活当前 demand(等价于 FIFO pop slot[0])。
            // 通过 public ContextMenu 入口 ActivateUpcomingDemandNow() 触发。
            _loop.ActivateUpcomingDemandNow();

            // Assert:
            //   - 新 slot 0 = 原 slot 1(FIFO 核心断言)
            //   - 队列长度至少 1(slot 0 已 pop,但 manual 模式下不立刻 refill;
            //     refill 由 FinishActivatedUpcomingDemand 在 demand 完成时驱动,
            //     而 useManualActiveDemand=true 默认会在 Activate 后等玩家手动完成。
            //     这是 sprint scope 之外的 demand 生命周期细节 —— 本测试只保证 FIFO 正确。)
            Assert.That(_loop.GetUpcomingGuestType(0), Is.EqualTo(originalSlot1Type),
                "Pop slot 0 后,原 slot 1 应左移成为新 slot 0。");
            Assert.That(_loop.UpcomingQueueCount, Is.GreaterThanOrEqualTo(1),
                "Activate 后队列长度应 ≥ 1(原 slot 1 已左移到 slot 0;refill 时机由 Finish 决定,非本测试范围)。");
        }

        // ── AC8.3:slot-indexed Reserve 写到正确 index ──────────────────────

        [Test]
        public void Test_ReserveRoomForUpcomingDemand_SlotIndex_WritesCorrectIndex()
        {
            // Arrange
            _loop.ScheduleUpcomingDemandPreview();
            var roomA = CreateRoom("RoomA", Room2DRoomCategory.Single);

            try
            {
                // Act:把 roomA 预分配给 slot 1。
                bool ok = _loop.ReserveRoomForUpcomingDemand(1, roomA);

                // Assert
                Assert.That(ok, Is.True, "Reserve 返回 ok=true。");
                Assert.That(_loop.GetReservedRoomAt(1), Is.EqualTo(roomA),
                    "Slot 1 应被写入 roomA。");
                Assert.That(_loop.GetReservedRoomAt(0), Is.Null,
                    "Slot 0 不应被影响(仍为 null)。");
            }
            finally
            {
                Object.DestroyImmediate(roomA.gameObject);
            }
        }

        // ── AC8.4:同房不可占两 slot,后写覆盖前写并清空 ─────────────────────
        //
        // 业务意图(Story 3 AC5 第二段):玩家给 slot 0 配了房 A,然后切换到 slot 1,
        // 又想把同一间房 A 配给 slot 1 → 旧 slot 0 应自动释放,房 A 转到 slot 1。

        [Test]
        public void Test_ReserveRoomForUpcomingDemand_SameRoomDifferentSlot_TransfersReservation()
        {
            // Arrange
            _loop.ScheduleUpcomingDemandPreview();
            var roomA = CreateRoom("RoomA", Room2DRoomCategory.Single);

            try
            {
                // Act:先给 slot 0,然后给 slot 1。
                _loop.ReserveRoomForUpcomingDemand(0, roomA);
                _loop.ReserveRoomForUpcomingDemand(1, roomA);

                // Assert:同房转移规则。
                Assert.That(_loop.GetReservedRoomAt(0), Is.Null,
                    "同房转移到 slot 1 后,slot 0 应自动释放。");
                Assert.That(_loop.GetReservedRoomAt(1), Is.EqualTo(roomA),
                    "Slot 1 应持有 roomA。");
            }
            finally
            {
                Object.DestroyImmediate(roomA.gameObject);
            }
        }

        // ── AC8.5:ClearReservedRoom(slot) 只清那个 slot,不影响其他 slot ─────

        [Test]
        public void Test_ClearReservedRoom_SlotIndex_ReleasesOnly_ThatSlot()
        {
            // Arrange:两 slot 都配上房。
            _loop.ScheduleUpcomingDemandPreview();
            var roomA = CreateRoom("RoomA", Room2DRoomCategory.Single);
            var roomB = CreateRoom("RoomB", Room2DRoomCategory.Twin);

            try
            {
                _loop.ReserveRoomForUpcomingDemand(0, roomA);
                _loop.ReserveRoomForUpcomingDemand(1, roomB);

                // Act:清 slot 0。
                _loop.ClearReservedRoom(0);

                // Assert
                Assert.That(_loop.GetReservedRoomAt(0), Is.Null,
                    "Clear(0) 应释放 slot 0。");
                Assert.That(_loop.GetReservedRoomAt(1), Is.EqualTo(roomB),
                    "Slot 1 不应被影响,仍持有 roomB。");
            }
            finally
            {
                Object.DestroyImmediate(roomA.gameObject);
                Object.DestroyImmediate(roomB.gameObject);
            }
        }

        // ── AC8.6:Business 客人 BedType 分布:Any 50% / Single 50% ──────────
        //
        // 容差区间宽设(35-65 / 200 样本 = 17.5%-32.5% per bucket vs 期望 25% 中点)
        // —— 主要捕"常量返回"或"权重严重失衡"。

        [Test]
        public void Test_PickRandomBedTypePreference_BusinessGuest_DistributionMatchesSpec()
        {
            // Arrange
            var counts = new Dictionary<Room2DBedTypePreference, int>
            {
                { Room2DBedTypePreference.Any, 0 },
                { Room2DBedTypePreference.Single, 0 },
                { Room2DBedTypePreference.Twin, 0 },
                { Room2DBedTypePreference.Family, 0 }
            };

            // Act
            for (int i = 0; i < DISTRIBUTION_SAMPLE_SIZE; i++)
            {
                counts[_loop.PickRandomBedTypePreference(Room2DGuestType.Business)]++;
            }

            // Assert:Business 只应产出 Any 或 Single。
            Assert.That(counts[Room2DBedTypePreference.Twin], Is.EqualTo(0),
                "Business 不应产出 Twin;实际 " + counts[Room2DBedTypePreference.Twin]);
            Assert.That(counts[Room2DBedTypePreference.Family], Is.EqualTo(0),
                "Business 不应产出 Family;实际 " + counts[Room2DBedTypePreference.Family]);

            // 期望 ~50/50,容忍 35-65%(避免 flaky)。
            int anyCount = counts[Room2DBedTypePreference.Any];
            int singleCount = counts[Room2DBedTypePreference.Single];
            float anyRatio = anyCount / (float)DISTRIBUTION_SAMPLE_SIZE;
            float singleRatio = singleCount / (float)DISTRIBUTION_SAMPLE_SIZE;

            Assert.That(anyRatio, Is.InRange(0.35f, 0.65f),
                "Business → Any 期望 ~50%,实际 " + anyRatio.ToString("P1")
                + "(" + anyCount + "/" + DISTRIBUTION_SAMPLE_SIZE + ")");
            Assert.That(singleRatio, Is.InRange(0.35f, 0.65f),
                "Business → Single 期望 ~50%,实际 " + singleRatio.ToString("P1")
                + "(" + singleCount + "/" + DISTRIBUTION_SAMPLE_SIZE + ")");
        }

        // ── AC8.7:Family 客人 BedType 分布:Family 70% / Twin 30% ───────────

        [Test]
        public void Test_PickRandomBedTypePreference_FamilyGuest_DistributionMatchesSpec()
        {
            // Arrange
            var counts = new Dictionary<Room2DBedTypePreference, int>
            {
                { Room2DBedTypePreference.Any, 0 },
                { Room2DBedTypePreference.Single, 0 },
                { Room2DBedTypePreference.Twin, 0 },
                { Room2DBedTypePreference.Family, 0 }
            };

            // Act
            for (int i = 0; i < DISTRIBUTION_SAMPLE_SIZE; i++)
            {
                counts[_loop.PickRandomBedTypePreference(Room2DGuestType.Family)]++;
            }

            // Assert
            Assert.That(counts[Room2DBedTypePreference.Any], Is.EqualTo(0),
                "Family 不应产出 Any。");
            Assert.That(counts[Room2DBedTypePreference.Single], Is.EqualTo(0),
                "Family 不应产出 Single。");

            // Family 期望 ~70%,容忍 55-85%;Twin 期望 ~30%,容忍 15-45%。
            float familyRatio = counts[Room2DBedTypePreference.Family] / (float)DISTRIBUTION_SAMPLE_SIZE;
            float twinRatio = counts[Room2DBedTypePreference.Twin] / (float)DISTRIBUTION_SAMPLE_SIZE;

            Assert.That(familyRatio, Is.InRange(0.55f, 0.85f),
                "Family → Family 期望 ~70%,实际 " + familyRatio.ToString("P1"));
            Assert.That(twinRatio, Is.InRange(0.15f, 0.45f),
                "Family → Twin 期望 ~30%,实际 " + twinRatio.ToString("P1"));
        }

        // ── AC8.8:VIP 客人 BedType 分布:Single 60% / Family 40% ────────────

        [Test]
        public void Test_PickRandomBedTypePreference_VipGuest_DistributionMatchesSpec()
        {
            // Arrange
            var counts = new Dictionary<Room2DBedTypePreference, int>
            {
                { Room2DBedTypePreference.Any, 0 },
                { Room2DBedTypePreference.Single, 0 },
                { Room2DBedTypePreference.Twin, 0 },
                { Room2DBedTypePreference.Family, 0 }
            };

            // Act
            for (int i = 0; i < DISTRIBUTION_SAMPLE_SIZE; i++)
            {
                counts[_loop.PickRandomBedTypePreference(Room2DGuestType.VIP)]++;
            }

            // Assert
            Assert.That(counts[Room2DBedTypePreference.Any], Is.EqualTo(0),
                "VIP 不应产出 Any。");
            Assert.That(counts[Room2DBedTypePreference.Twin], Is.EqualTo(0),
                "VIP 不应产出 Twin。");

            // VIP Single 期望 ~60%,容忍 45-75%;Family 期望 ~40%,容忍 25-55%。
            float singleRatio = counts[Room2DBedTypePreference.Single] / (float)DISTRIBUTION_SAMPLE_SIZE;
            float familyRatio = counts[Room2DBedTypePreference.Family] / (float)DISTRIBUTION_SAMPLE_SIZE;

            Assert.That(singleRatio, Is.InRange(0.45f, 0.75f),
                "VIP → Single 期望 ~60%,实际 " + singleRatio.ToString("P1"));
            Assert.That(familyRatio, Is.InRange(0.25f, 0.55f),
                "VIP → Family 期望 ~40%,实际 " + familyRatio.ToString("P1"));
        }
    }
}
