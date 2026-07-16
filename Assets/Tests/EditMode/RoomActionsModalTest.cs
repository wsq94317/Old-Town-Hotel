using NUnit.Framework;
using UnityEngine;

// RoomActions 弹窗的主行动按房态切换：Dirty 派保洁，AwaitingInspection 派主管——
// 修复"Inspector 完全没有派活入口"的缺口。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class RoomActionsModalTest
    {
        private GameObject _root;
        private RoomActionsModal _modal;
        private Room2DEntity _room;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("modal-test-root");
            _modal = _root.AddComponent<RoomActionsModal>();
            var roomGo = new GameObject("room-101");
            roomGo.transform.SetParent(_root.transform);
            _room = roomGo.AddComponent<Room2DEntity>();
            _room.roomNumber = 101;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void ActionModeFor_MapsStatesToActions()
        {
            Assert.AreEqual(RoomActionsModal.RoomAction.AssignHsk,
                RoomActionsModal.ActionModeFor(Room2DState.Dirty));
            Assert.AreEqual(RoomActionsModal.RoomAction.AssignInspector,
                RoomActionsModal.ActionModeFor(Room2DState.AwaitingInspection));
            Assert.AreEqual(RoomActionsModal.RoomAction.None,
                RoomActionsModal.ActionModeFor(Room2DState.Ready));
            Assert.AreEqual(RoomActionsModal.RoomAction.None,
                RoomActionsModal.ActionModeFor(Room2DState.Occupied));
            Assert.AreEqual(RoomActionsModal.RoomAction.None,
                RoomActionsModal.ActionModeFor(Room2DState.Cleaning));
            Assert.AreEqual(RoomActionsModal.RoomAction.None,
                RoomActionsModal.ActionModeFor(Room2DState.Blocked));
        }

        [Test]
        public void Setup_DirtyRoom_SelectsHskAction()
        {
            _room.currentState = Room2DState.Dirty;
            _modal.Setup(_room, "Floor: 1", "—", "Normal");
            Assert.AreEqual(RoomActionsModal.RoomAction.AssignHsk, _modal.CurrentAction);
        }

        [Test]
        public void Setup_AwaitingInspectionRoom_SelectsInspectorAction()
        {
            _room.currentState = Room2DState.AwaitingInspection;
            _modal.Setup(_room, "Floor: 1", "—", "Normal");
            Assert.AreEqual(RoomActionsModal.RoomAction.AssignInspector, _modal.CurrentAction);
        }

        [Test]
        public void Setup_ReadyRoom_NoAction()
        {
            _room.currentState = Room2DState.Ready;
            _modal.Setup(_room, "Floor: 1", "—", "Normal");
            Assert.AreEqual(RoomActionsModal.RoomAction.None, _modal.CurrentAction);
        }

        [Test]
        public void Inspector_CanBeAssigned_ToAwaitingInspectionRoom()
        {
            // 端到端最短链：AwaitingInspection 房 + 空闲主管 → AssignRoom 成功。
            _room.currentState = Room2DState.AwaitingInspection;
            var inspGo = new GameObject("insp");
            inspGo.transform.SetParent(_root.transform);
            var insp = inspGo.AddComponent<Inspector2D>();

            Assert.IsTrue(insp.AssignRoom(_room));
            Assert.IsTrue(insp.IsBusy);
        }
    }
}
