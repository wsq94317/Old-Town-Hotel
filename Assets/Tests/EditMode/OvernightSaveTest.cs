using NUnit.Framework;
using UnityEngine;

// day-cycle v2 / save v2：过夜占用持久化回归——日结时 Occupied 的房间进存档，
// 读档后次日晨间退房潮如实退房；v1 旧档无 rooms 字段也能正常加载。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class OvernightSaveTest
    {
        private GameObject _root;
        private Room2DPrototypeDemandLoop _loop;
        private Room2DEntity _room101;
        private Room2DEntity _room102;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("overnight-save-root");
            _loop = _root.AddComponent<Room2DPrototypeDemandLoop>();
            _loop.autoFindReferences = false;
            _room101 = MakeRoom(101);
            _room102 = MakeRoom(102);
            _loop.rooms = new[] { _room101, _room102 };
        }

        private Room2DEntity MakeRoom(int number)
        {
            var go = new GameObject("room-" + number);
            go.transform.SetParent(_root.transform);
            var room = go.AddComponent<Room2DEntity>();
            room.roomNumber = number;
            room.currentState = Room2DState.Ready;
            return room;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Capture_RecordsOnlyOccupiedRooms()
        {
            _room101.currentState = Room2DState.Occupied;

            RoomsState state = _loop.CaptureOccupancy();

            Assert.AreEqual(1, state.occupied.Count);
            Assert.AreEqual(101, state.occupied[0].room);
        }

        [Test]
        public void RoundTrip_RestoresOccupancy()
        {
            _room101.currentState = Room2DState.Occupied;
            _room102.currentState = Room2DState.Dirty;

            RoomsState state = _loop.CaptureOccupancy();

            // 模拟读档前的干净场景。
            _room101.currentState = Room2DState.Ready;
            _room102.currentState = Room2DState.Ready;

            _loop.RestoreOccupancy(state);

            Assert.AreEqual(Room2DState.Occupied, _room101.currentState);
            Assert.AreEqual(Room2DState.Ready, _room102.currentState); // Dirty 不入档（spec §4 只存过夜占用）
        }

        [Test]
        public void Restore_NullOrEmpty_IsNoOp()
        {
            _loop.RestoreOccupancy(null);
            _loop.RestoreOccupancy(new RoomsState());
            Assert.AreEqual(Room2DState.Ready, _room101.currentState);
        }

        [Test]
        public void GameStateV1Json_LoadsWithEmptyRooms()
        {
            // v1 存档没有 rooms 字段：JsonUtility 应保留字段初始化器的空列表。
            string v1Json = "{\"version\":1,\"economy\":{\"cash\":500,\"loanBalance\":0,\"loanRate\":0.0,\"staff\":[],\"reputationSamples\":[]},\"renovation\":{\"totalRooms\":0,\"startingRoomNumber\":0,\"rooms\":[],\"jobs\":[]},\"progress\":{\"day\":3,\"satisfaction\":10}}";
            GameState gs = JsonUtility.FromJson<GameState>(v1Json);
            Assert.IsNotNull(gs.rooms);
            Assert.IsNotNull(gs.rooms.occupied);
            Assert.AreEqual(0, gs.rooms.occupied.Count);
            Assert.AreEqual(3, gs.progress.day);
        }

        [Test]
        public void RestoredOccupancy_ChecksOutInMorningWave()
        {
            // 端到端：恢复过夜客 → 晨间退房潮 → 逼近阈值 → 处理退房 → 房间变脏。
            var state = new RoomsState();
            state.occupied.Add(new OccupiedRoomEntry { room = 101, stayQuality = 1 });
            _loop.RestoreOccupancy(state);
            Assert.AreEqual(Room2DState.Occupied, _room101.currentState);

            _loop.BeginMorningCheckoutWave();
            // 该房的 elapsed 已被 wave 排到 occupiedDurationSeconds 附近；直接推过阈值。
            _room101.stateElapsedSeconds = _loop.occupiedDurationSeconds + 1f;
            _loop.ProcessOccupiedCheckouts();

            Assert.AreEqual(Room2DState.Dirty, _room101.currentState);
            // fallbackMorningDirtyRooms 不应生效（有真实过夜客）——102 保持 Ready。
            Assert.AreEqual(Room2DState.Ready, _room102.currentState);
        }
    }
}
