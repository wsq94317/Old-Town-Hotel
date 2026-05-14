using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试：Inspector2D 的 UI 只读访问器（ui-spec.md §6 — INSP 卡片）。
// 与 Housekeeper2D 测试对称，覆盖 IsBusy / CurrentActivityLabel /
// RemainingSeconds / AssignedRoomNumber。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class Inspector2D_UiAccessors_Test
    {
        private GameObject _inspGo;
        private Inspector2D _insp;
        private GameObject _roomGo;
        private Room2DEntity _room;

        [SetUp]
        public void SetUp()
        {
            _inspGo = new GameObject("TestInspector");
            _insp = _inspGo.AddComponent<Inspector2D>();
            _insp.autoFindReferences = false;

            _roomGo = new GameObject("TestRoom");
            _room = _roomGo.AddComponent<Room2DEntity>();
            _room.roomNumber = 305;
            _room.roomName = "Room 305";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_inspGo);
            Object.DestroyImmediate(_roomGo);
        }

        // ── IsBusy ──────────────────────────────────────────────────────────

        [Test]
        public void test_isBusy_when_idle_returns_false()
        {
            _insp.currentState = Inspector2D.InspectorState.Idle;
            Assert.That(_insp.IsBusy, Is.False);
        }

        [Test]
        public void test_isBusy_when_working_returns_true()
        {
            _insp.currentState = Inspector2D.InspectorState.Working;
            Assert.That(_insp.IsBusy, Is.True);
        }

        [Test]
        public void test_isBusy_when_traveling_returns_true()
        {
            _insp.currentState = Inspector2D.InspectorState.Traveling;
            Assert.That(_insp.IsBusy, Is.True);
        }

        // ── CurrentActivityLabel ────────────────────────────────────────────

        [Test]
        public void test_currentActivityLabel_idle_returns_kongxian()
        {
            _insp.currentState = Inspector2D.InspectorState.Idle;
            Assert.That(_insp.CurrentActivityLabel, Is.EqualTo("空闲"));
        }

        [Test]
        public void test_currentActivityLabel_working_returns_jianchazhong()
        {
            _insp.currentState = Inspector2D.InspectorState.Working;
            Assert.That(_insp.CurrentActivityLabel, Is.EqualTo("检查中"));
        }

        [Test]
        public void test_currentActivityLabel_traveling_returns_qianwang()
        {
            _insp.currentState = Inspector2D.InspectorState.Traveling;
            Assert.That(_insp.CurrentActivityLabel, Is.EqualTo("前往房间"));
        }

        // ── RemainingSeconds ────────────────────────────────────────────────

        [Test]
        public void test_remainingSeconds_when_idle_returns_zero()
        {
            // Arrange
            _insp.currentState = Inspector2D.InspectorState.Idle;
            _insp.inspectionDurationSeconds = 4f;
            _insp.inspectionTimerSeconds = 1f;

            // Act / Assert
            Assert.That(_insp.RemainingSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void test_remainingSeconds_when_working_reflects_duration_minus_timer()
        {
            // Arrange
            _insp.currentState = Inspector2D.InspectorState.Working;
            _insp.inspectionDurationSeconds = 4f;
            _insp.inspectionTimerSeconds = 1f;

            // Act
            float remaining = _insp.RemainingSeconds;

            // Assert
            Assert.That(remaining, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void test_remainingSeconds_clamps_to_zero_when_timer_overshoots()
        {
            // Arrange
            _insp.currentState = Inspector2D.InspectorState.Working;
            _insp.inspectionDurationSeconds = 4f;
            _insp.inspectionTimerSeconds = 5f;

            // Act / Assert
            Assert.That(_insp.RemainingSeconds, Is.EqualTo(0f));
        }

        // ── AssignedRoomNumber ──────────────────────────────────────────────

        [Test]
        public void test_assignedRoomNumber_when_no_room_returns_null()
        {
            _insp.assignedRoom = null;
            Assert.That(_insp.AssignedRoomNumber, Is.Null);
        }

        [Test]
        public void test_assignedRoomNumber_returns_room_number()
        {
            // Arrange
            _insp.assignedRoom = _room;

            // Act
            int? roomNumber = _insp.AssignedRoomNumber;

            // Assert
            Assert.That(roomNumber.HasValue, Is.True);
            Assert.That(roomNumber.Value, Is.EqualTo(305));
        }
    }
}
