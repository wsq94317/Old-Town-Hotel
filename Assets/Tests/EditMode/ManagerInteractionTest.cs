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

        private Room2DEntity CreateRoom(int roomNumber, Vector3 position)
        {
            var go = new GameObject("room-" + roomNumber);
            go.transform.SetParent(_root.transform);
            go.transform.position = position;
            var room = go.AddComponent<Room2DEntity>();
            room.roomNumber = roomNumber;
            return room;
        }
    }
}
