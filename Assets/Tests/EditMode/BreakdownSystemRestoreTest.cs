using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class BreakdownSystemRestoreTest
    {
        private GameObject _root;
        private Room2DPrototypeDemandLoop _loop;
        private BreakdownSystem _breakdowns;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("breakdown-restore-root");
            _loop = _root.AddComponent<Room2DPrototypeDemandLoop>();
            _loop.autoFindReferences = false;
            _breakdowns = new GameObject("breakdowns").AddComponent<BreakdownSystem>();
            typeof(BreakdownSystem)
                .GetField("demandLoop", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_breakdowns, _loop);
        }

        [TearDown]
        public void TearDown()
        {
            if (_breakdowns != null) Object.DestroyImmediate(_breakdowns.gameObject);
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void RestoreFrom_WaitsForRooms_ThenAppliesLockedAndTapedState()
        {
            var world = new WorldState();
            world.lockedRooms.Add(101);
            world.tapedBreakdowns.Add(new TapedBreakdownEntry
            {
                room = 101,
                x = 1.5f,
                y = 0f,
                z = -2f,
                kind = "LEAKY PIPE"
            });

            _breakdowns.RestoreFrom(world);

            var capturedBeforeRooms = new WorldState();
            _breakdowns.CaptureTo(capturedBeforeRooms);
            Assert.That(capturedBeforeRooms.lockedRooms, Is.EquivalentTo(new[] { 101 }));
            Assert.That(capturedBeforeRooms.tapedBreakdowns.Count, Is.EqualTo(1));
            Assert.That(capturedBeforeRooms.tapedBreakdowns[0].room, Is.EqualTo(101));

            var roomGo = new GameObject("room-101");
            roomGo.transform.SetParent(_root.transform);
            var room = roomGo.AddComponent<Room2DEntity>();
            room.roomNumber = 101;
            room.currentState = Room2DState.Ready;
            _loop.rooms = new[] { room };

            _breakdowns.RestoreFrom(capturedBeforeRooms);
            Assert.That(room.currentState, Is.EqualTo(Room2DState.Blocked));

            var captured = new WorldState();
            _breakdowns.CaptureTo(captured);
            Assert.That(captured.lockedRooms, Is.EquivalentTo(new[] { 101 }));
            Assert.That(captured.tapedBreakdowns.Count, Is.EqualTo(1));
            Assert.That(captured.tapedBreakdowns[0].room, Is.EqualTo(101));
            Assert.That(captured.tapedBreakdowns[0].kind, Is.EqualTo("LEAKY PIPE"));
        }
    }
}
