using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class ManagerInteractionTest
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("manager-interaction-root");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void FindCommandRoom_IgnoresRoomsOnOtherFloors()
        {
            var lower = CreateRoom(101, new Vector3(0f, FloorMath.BaseYFor(0), 0f));
            var upper = CreateRoom(201, new Vector3(0f, FloorMath.BaseYFor(1), 0f));

            Room2DEntity picked = ManagerInteraction.FindCommandRoom(
                new[] { lower, upper },
                new Vector3(0.2f, FloorMath.BaseYFor(1), 0.2f));

            Assert.That(picked, Is.SameAs(upper));
        }

        [Test]
        public void GetCommandRoomCandidates_FiltersToInspectionRoomsAcrossFloors()
        {
            var dirty = CreateRoom(101, new Vector3(0f, FloorMath.BaseYFor(0), 0f), Room2DState.Dirty);
            var inspectA = CreateRoom(201, new Vector3(0f, FloorMath.BaseYFor(1), 0f), Room2DState.AwaitingInspection);
            var inspectB = CreateRoom(301, new Vector3(0f, FloorMath.BaseYFor(2), 0f), Room2DState.AwaitingInspection);

            var candidates = ManagerInteraction.GetCommandRoomCandidates(
                new[] { dirty, inspectB, inspectA },
                StaffRole.Inspector);

            Assert.That(candidates, Is.EqualTo(new[] { inspectA, inspectB }));
        }

        [Test]
        public void GetCommandRoomCandidates_FiltersToDirtyRoomsForHousekeeper()
        {
            var dirtyA = CreateRoom(102, new Vector3(0f, FloorMath.BaseYFor(0), 0f), Room2DState.Dirty);
            var ready = CreateRoom(103, new Vector3(0f, FloorMath.BaseYFor(0), 0f), Room2DState.Ready);
            var dirtyB = CreateRoom(202, new Vector3(0f, FloorMath.BaseYFor(1), 0f), Room2DState.Dirty);

            var candidates = ManagerInteraction.GetCommandRoomCandidates(
                new[] { dirtyB, ready, dirtyA },
                StaffRole.Housekeeper);

            Assert.That(candidates, Is.EqualTo(new[] { dirtyA, dirtyB }));
        }

        private Room2DEntity CreateRoom(int roomNumber, Vector3 position, Room2DState state = Room2DState.Ready)
        {
            var go = new GameObject("room-" + roomNumber);
            go.transform.SetParent(_root.transform);
            go.transform.position = position;
            var room = go.AddComponent<Room2DEntity>();
            room.roomNumber = roomNumber;
            room.currentState = state;
            return room;
        }
    }
}
