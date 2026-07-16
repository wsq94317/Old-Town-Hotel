using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// v2 M2：自动派活规则——HSK 拿最久的 Dirty，INSP 拿最久的 AwaitingInspection，
// 已被认领的房跳过，Reception/Manager 永远无任务。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class TaskDispatchLogicTest
    {
        private GameObject _root;
        private List<Room2DEntity> _rooms;
        private HashSet<Room2DEntity> _claimed;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("dispatch-root");
            _rooms = new List<Room2DEntity>();
            _claimed = new HashSet<Room2DEntity>();
        }

        private Room2DEntity Room(int number, Room2DState state, float waitSeconds)
        {
            var go = new GameObject("room-" + number);
            go.transform.SetParent(_root.transform);
            var r = go.AddComponent<Room2DEntity>();
            r.roomNumber = number;
            r.currentState = state;
            r.stateElapsedSeconds = waitSeconds;
            _rooms.Add(r);
            return r;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void Housekeeper_TakesOldestDirtyRoom()
        {
            Room(101, Room2DState.Dirty, 10f);
            var oldest = Room(102, Room2DState.Dirty, 99f);
            Room(103, Room2DState.Ready, 500f); // Ready 不参与

            var task = TaskDispatchLogic.NextTaskFor(StaffRole.Housekeeper, _rooms, _claimed);

            Assert.IsTrue(task.HasValue);
            Assert.AreSame(oldest, task.Value.Room);
            Assert.AreEqual(StaffTaskKind.Clean, task.Value.Kind);
        }

        [Test]
        public void Inspector_TakesOldestAwaitingInspection()
        {
            Room(101, Room2DState.Dirty, 99f); // 不归 INSP 管
            var target = Room(102, Room2DState.AwaitingInspection, 20f);

            var task = TaskDispatchLogic.NextTaskFor(StaffRole.Inspector, _rooms, _claimed);

            Assert.IsTrue(task.HasValue);
            Assert.AreSame(target, task.Value.Room);
            Assert.AreEqual(StaffTaskKind.Inspect, task.Value.Kind);
        }

        [Test]
        public void ClaimedRooms_AreSkipped()
        {
            var claimedRoom = Room(101, Room2DState.Dirty, 99f);
            var free = Room(102, Room2DState.Dirty, 10f);
            _claimed.Add(claimedRoom);

            var task = TaskDispatchLogic.NextTaskFor(StaffRole.Housekeeper, _rooms, _claimed);

            Assert.IsTrue(task.HasValue);
            Assert.AreSame(free, task.Value.Room);
        }

        [Test]
        public void NonFieldRoles_NeverGetTasks()
        {
            Room(101, Room2DState.Dirty, 99f);
            Room(102, Room2DState.AwaitingInspection, 99f);

            Assert.IsFalse(TaskDispatchLogic.NextTaskFor(StaffRole.Reception, _rooms, _claimed).HasValue);
            Assert.IsFalse(TaskDispatchLogic.NextTaskFor(StaffRole.Manager, _rooms, _claimed).HasValue);
        }

        [Test]
        public void NoCandidates_ReturnsEmpty()
        {
            Room(101, Room2DState.Ready, 99f);
            Assert.IsFalse(TaskDispatchLogic.NextTaskFor(StaffRole.Housekeeper, _rooms, _claimed).HasValue);
        }
    }
}
