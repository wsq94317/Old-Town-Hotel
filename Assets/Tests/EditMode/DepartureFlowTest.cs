using NUnit.Framework;
using UnityEngine;

// day-cycle v2 退房卡片流：晨间待退房队列（点卡办退房 + 错峰自动兜底）、
// 开局垫过夜客（不再瞬间垫脏房）、客人类型随存档往返。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DepartureFlowTest
    {
        private GameObject _root;
        private Room2DPrototypeDemandLoop _loop;
        private Room2DEntity _r101;
        private Room2DEntity _r102;
        private Room2DEntity _r103;
        private Room2DEntity _r104;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("departure-test-root");
            _loop = _root.AddComponent<Room2DPrototypeDemandLoop>();
            _loop.autoFindReferences = false;
            _r101 = MakeRoom(101);
            _r102 = MakeRoom(102);
            _r103 = MakeRoom(103);
            _r104 = MakeRoom(104);
            _loop.rooms = new[] { _r101, _r102, _r103, _r104 };
        }

        private Room2DEntity MakeRoom(int number)
        {
            var go = new GameObject("room-" + number);
            go.transform.SetParent(_root.transform);
            var room = go.AddComponent<Room2DEntity>();
            room.roomNumber = number;
            room.roomName = "Room " + number;
            room.currentState = Room2DState.Ready;
            return room;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Wave_PopulatesDepartures_ForOvernightRooms()
        {
            _r101.currentState = Room2DState.Occupied;

            _loop.BeginMorningCheckoutWave();

            Assert.AreEqual(1, _loop.DepartureCount);
            Assert.AreSame(_r101, _loop.GetDepartureRoom(0));
        }

        [Test]
        public void Wave_FreshBoot_SeedsOvernightDepartures_NotInstantDirty()
        {
            // 全 Ready（新开局）：垫过夜客走退房流程，而不是瞬间垫脏房。
            _loop.BeginMorningCheckoutWave();

            Assert.AreEqual(3, _loop.DepartureCount);
            int occupied = 0, dirty = 0;
            foreach (var r in _loop.rooms)
            {
                if (r.currentState == Room2DState.Occupied) occupied++;
                if (r.currentState == Room2DState.Dirty) dirty++;
            }
            Assert.AreEqual(3, occupied);
            Assert.AreEqual(0, dirty);
        }

        [Test]
        public void TapCheckout_ChecksOutImmediately_AndFiresPlayerEvent()
        {
            _r101.currentState = Room2DState.Occupied;
            _loop.BeginMorningCheckoutWave();
            int events = 0;
            bool byPlayerFlag = false;
            _loop.OnDepartureCheckedOut += (room, amount, byPlayer) => { events++; byPlayerFlag = byPlayer; };

            bool ok = _loop.TryCheckOutDeparture(_r101);

            Assert.IsTrue(ok);
            Assert.AreEqual(Room2DState.Dirty, _r101.currentState);
            Assert.AreEqual(0, _loop.DepartureCount);
            Assert.AreEqual(1, events);
            Assert.IsTrue(byPlayerFlag);
        }

        [Test]
        public void TapCheckout_RoomNotDeparting_Fails()
        {
            _r101.currentState = Room2DState.Occupied;
            _loop.BeginMorningCheckoutWave();

            // 102 是 Ready 房，不在待退房队列。
            Assert.IsFalse(_loop.TryCheckOutDeparture(_r102));
            Assert.IsFalse(_loop.TryCheckOutDeparture(null));
            Assert.AreEqual(1, _loop.DepartureCount);
        }

        [Test]
        public void AutoCheckout_RemovesFromDepartures_AndFiresNonPlayerEvent()
        {
            _r101.currentState = Room2DState.Occupied;
            _loop.BeginMorningCheckoutWave();
            int events = 0;
            bool byPlayerFlag = true;
            _loop.OnDepartureCheckedOut += (room, amount, byPlayer) => { events++; byPlayerFlag = byPlayer; };

            _r101.stateElapsedSeconds = _loop.occupiedDurationSeconds + 1f;
            _loop.ProcessOccupiedCheckouts();

            Assert.AreEqual(Room2DState.Dirty, _r101.currentState);
            Assert.AreEqual(0, _loop.DepartureCount);
            Assert.AreEqual(1, events);
            Assert.IsFalse(byPlayerFlag);
        }

        [Test]
        public void GuestType_RoundTrips_ThroughSaveAndDepartureQueue()
        {
            var state = new RoomsState();
            state.occupied.Add(new OccupiedRoomEntry
            {
                room = 101,
                stayQuality = (int)Room2DPrototypeDemandLoop.Room2DMatchQuality.GoodMatch,
                guestType = (int)Room2DGuestType.VIP
            });
            _loop.RestoreOccupancy(state);

            RoomsState captured = _loop.CaptureOccupancy();
            Assert.AreEqual((int)Room2DGuestType.VIP, captured.occupied[0].guestType);

            _loop.BeginMorningCheckoutWave();
            Assert.AreEqual(Room2DGuestType.VIP, _loop.GetDepartureGuestType(0));
        }
    }
}
