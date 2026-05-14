using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试：Room2DPrototypeDemandLoop 的 guest-keyed ready-room 查询。
//
// 覆盖：
//   - HasReadyRoomForGuest(bedPref)：bool；前台 CTA 用
//   - GetReadyRoomsForGuest(bedPref)：列表 + Suitability rank；Modal 1 列表用
//
// 测试隔离策略（沿用 DemandLoopMultiSlotTest）：
//   1. [SetUp] 新建 GameObject + AddComponent<Room2DPrototypeDemandLoop>()
//   2. autoFindReferences = false → FindRoomsIfNeeded 不扫场景，保留我们注入的 rooms[]
//   3. 用 helper 直接 new Room2DEntity GameObject 并塞进 _loop.rooms[]
//   4. [TearDown] DestroyImmediate 所有 GameObject
//
// 命名沿用项目内 snake_case（test-standards.md），与 sprint 内 *_UiAccessors_Test 一致。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class Room2DPrototypeDemandLoop_RoomSuitability_Test
    {
        private GameObject _loopGo;
        private Room2DPrototypeDemandLoop _loop;
        private readonly List<GameObject> _spawnedRoomGos = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _loopGo = new GameObject("TestDemandLoopForRoomSuitability");
            _loop = _loopGo.AddComponent<Room2DPrototypeDemandLoop>();

            // 关键：不让 FindRoomsIfNeeded 扫场景覆盖我们注入的 rooms[]。
            _loop.autoFindReferences = false;
            _loop.runDuringPlay = false;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawnedRoomGos.Count; i++)
            {
                if (_spawnedRoomGos[i] != null)
                {
                    Object.DestroyImmediate(_spawnedRoomGos[i]);
                }
            }
            _spawnedRoomGos.Clear();

            Object.DestroyImmediate(_loopGo);
        }

        // ── helper：创建一个 Room2DEntity 并设状态 / 床型 / 房号 ──────────────────

        private Room2DEntity CreateRoom(
            string name,
            int roomNumber,
            Room2DRoomCategory category,
            Room2DState state)
        {
            var go = new GameObject(name);
            _spawnedRoomGos.Add(go);
            var room = go.AddComponent<Room2DEntity>();
            room.roomName = name;
            room.roomNumber = roomNumber;
            room.roomCategory = category;
            room.currentState = state;
            return room;
        }

        private void SetRooms(params Room2DEntity[] rooms)
        {
            _loop.rooms = rooms;
        }

        // ── HasReadyRoomForGuest ──────────────────────────────────────────────

        [Test]
        public void test_HasReadyRoomForGuest_returns_false_when_no_rooms_ready()
        {
            // Arrange：两个房间都不是 Ready。
            var dirty = CreateRoom("R101", 101, Room2DRoomCategory.Single, Room2DState.Dirty);
            var occupied = CreateRoom("R102", 102, Room2DRoomCategory.Twin, Room2DState.Occupied);
            SetRooms(dirty, occupied);

            // Act
            bool result = _loop.HasReadyRoomForGuest(Room2DBedTypePreference.Single);

            // Assert
            Assert.That(result, Is.False,
                "没有 Ready 房时，HasReadyRoomForGuest 应返回 false。");
        }

        [Test]
        public void test_HasReadyRoomForGuest_returns_true_when_matching_room_ready()
        {
            // Arrange：床型匹配且 Ready。
            var matching = CreateRoom("R101", 101, Room2DRoomCategory.Single, Room2DState.Ready);
            SetRooms(matching);

            // Act
            bool result = _loop.HasReadyRoomForGuest(Room2DBedTypePreference.Single);

            // Assert
            Assert.That(result, Is.True,
                "床型匹配的 Ready 房存在时应返回 true。");
        }

        [Test]
        public void test_HasReadyRoomForGuest_returns_true_even_with_bed_mismatch_but_ready_room_present()
        {
            // Arrange：唯一的 Ready 房床型不匹配（Single 房 vs 要求 Family）。
            // CTA 仍应允许打开 Modal 1，因为玩家可以选 SoSo（fallback）房。
            var mismatchedReady = CreateRoom("R101", 101, Room2DRoomCategory.Single, Room2DState.Ready);
            SetRooms(mismatchedReady);

            // Act
            bool result = _loop.HasReadyRoomForGuest(Room2DBedTypePreference.Family);

            // Assert
            Assert.That(result, Is.True,
                "床型不匹配但仍是 Ready 时，SoSo 仍算可选 fallback，CTA 应返回 true。");
        }

        // ── GetReadyRoomsForGuest ─────────────────────────────────────────────

        [Test]
        public void test_GetReadyRoomsForGuest_returns_empty_when_no_ready_rooms()
        {
            // Arrange
            var dirty = CreateRoom("R101", 101, Room2DRoomCategory.Single, Room2DState.Dirty);
            var cleaning = CreateRoom("R102", 102, Room2DRoomCategory.Twin, Room2DState.Cleaning);
            SetRooms(dirty, cleaning);

            // Act
            IReadOnlyList<RoomSuitability> result = _loop.GetReadyRoomsForGuest(Room2DBedTypePreference.Single);

            // Assert
            Assert.That(result, Is.Not.Null, "结果不应为 null。");
            Assert.That(result.Count, Is.EqualTo(0),
                "没有 Ready 房时应返回空列表。");
        }

        [Test]
        public void test_GetReadyRoomsForGuest_returns_suitable_first_so_so_after()
        {
            // Arrange：三个 Ready 房：
            //   R201 Single（匹配 Single → Suitable）
            //   R102 Twin   （不匹配 Single → SoSo）
            //   R101 Single（匹配 Single → Suitable）
            // 期望顺序：[Suitable R101, Suitable R201, SoSo R102]
            //   - Suitable 在前（rank 升序）
            //   - 同档内 roomNumber 升序：R101 < R201
            var r201 = CreateRoom("R201", 201, Room2DRoomCategory.Single, Room2DState.Ready);
            var r102 = CreateRoom("R102", 102, Room2DRoomCategory.Twin, Room2DState.Ready);
            var r101 = CreateRoom("R101", 101, Room2DRoomCategory.Single, Room2DState.Ready);
            SetRooms(r201, r102, r101);

            // Act
            IReadOnlyList<RoomSuitability> result = _loop.GetReadyRoomsForGuest(Room2DBedTypePreference.Single);

            // Assert
            Assert.That(result.Count, Is.EqualTo(3),
                "三个 Ready 房应全部出现在结果中。");

            Assert.That(result[0].Rank, Is.EqualTo(RoomSuitabilityRank.Suitable));
            Assert.That(result[0].Room, Is.EqualTo(r101),
                "首位应是 roomNumber 最小的 Suitable 房（R101）。");

            Assert.That(result[1].Rank, Is.EqualTo(RoomSuitabilityRank.Suitable));
            Assert.That(result[1].Room, Is.EqualTo(r201),
                "次位应是第二小 roomNumber 的 Suitable 房（R201）。");

            Assert.That(result[2].Rank, Is.EqualTo(RoomSuitabilityRank.SoSo));
            Assert.That(result[2].Room, Is.EqualTo(r102),
                "末位应是 SoSo 档（R102 Twin）。");
        }

        [Test]
        public void test_GetReadyRoomsForGuest_filters_out_non_ready_rooms()
        {
            // Arrange：混合状态。只有 R102 (Ready) 应出现在结果里。
            var dirty = CreateRoom("R101", 101, Room2DRoomCategory.Single, Room2DState.Dirty);
            var ready = CreateRoom("R102", 102, Room2DRoomCategory.Single, Room2DState.Ready);
            var occupied = CreateRoom("R103", 103, Room2DRoomCategory.Single, Room2DState.Occupied);
            var cleaning = CreateRoom("R104", 104, Room2DRoomCategory.Single, Room2DState.Cleaning);
            var awaitingInspection = CreateRoom("R105", 105, Room2DRoomCategory.Single, Room2DState.AwaitingInspection);
            var blocked = CreateRoom("R106", 106, Room2DRoomCategory.Single, Room2DState.Blocked);
            SetRooms(dirty, ready, occupied, cleaning, awaitingInspection, blocked);

            // Act
            IReadOnlyList<RoomSuitability> result = _loop.GetReadyRoomsForGuest(Room2DBedTypePreference.Single);

            // Assert：只有 R102 是 Ready。
            Assert.That(result.Count, Is.EqualTo(1),
                "非 Ready 房（Dirty/Occupied/Cleaning/AwaitingInspection/Blocked）应被过滤掉。");
            Assert.That(result[0].Room, Is.EqualTo(ready),
                "结果应仅包含唯一的 Ready 房 R102。");
            Assert.That(result[0].Rank, Is.EqualTo(RoomSuitabilityRank.Suitable));
        }
    }
}
