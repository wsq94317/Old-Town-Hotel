using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试：Housekeeper2D 的 UI 只读访问器（ui-spec.md §6 — HSK 卡片）。
// 覆盖：IsBusy / CurrentActivityLabel / RemainingSeconds / AssignedRoomNumber。
//
// 注意：这些 getter 仅暴露既有运行时字段，不引入新游戏状态；测试直接写入
// public 字段（currentState / cleaningDurationSeconds / cleaningTimerSeconds /
// assignedRoom），确保 getter 与字段一一对应。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class Housekeeper2D_UiAccessors_Test
    {
        private GameObject _hskGo;
        private Housekeeper2D _hsk;
        private GameObject _roomGo;
        private Room2DEntity _room;

        [SetUp]
        public void SetUp()
        {
            // Arrange
            _hskGo = new GameObject("TestHousekeeper");
            _hsk = _hskGo.AddComponent<Housekeeper2D>();
            _hsk.autoFindReferences = false;

            _roomGo = new GameObject("TestRoom");
            _room = _roomGo.AddComponent<Room2DEntity>();
            _room.roomNumber = 203;
            _room.roomName = "Room 203";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_hskGo);
            Object.DestroyImmediate(_roomGo);
        }

        // ── IsBusy ──────────────────────────────────────────────────────────

        [Test]
        public void test_isBusy_when_idle_returns_false()
        {
            // Arrange
            _hsk.currentState = Housekeeper2D.HousekeeperState.Idle;

            // Act / Assert
            Assert.That(_hsk.IsBusy, Is.False,
                "Idle 状态下 IsBusy 必须为 false。");
        }

        [Test]
        public void test_isBusy_when_working_returns_true()
        {
            // Arrange
            _hsk.currentState = Housekeeper2D.HousekeeperState.Working;

            // Act / Assert
            Assert.That(_hsk.IsBusy, Is.True,
                "Working 状态下 IsBusy 必须为 true。");
        }

        [Test]
        public void test_isBusy_when_traveling_returns_true()
        {
            // Arrange
            _hsk.currentState = Housekeeper2D.HousekeeperState.Traveling;

            // Act / Assert
            Assert.That(_hsk.IsBusy, Is.True,
                "Traveling 状态下 IsBusy 必须为 true（非 Idle 即 Busy）。");
        }

        // ── CurrentActivityLabel ────────────────────────────────────────────

        [Test]
        public void test_currentActivityLabel_idle_returns_kongxian()
        {
            _hsk.currentState = Housekeeper2D.HousekeeperState.Idle;
            Assert.That(_hsk.CurrentActivityLabel, Is.EqualTo("空闲"));
        }

        [Test]
        public void test_currentActivityLabel_working_returns_qingjie()
        {
            _hsk.currentState = Housekeeper2D.HousekeeperState.Working;
            Assert.That(_hsk.CurrentActivityLabel, Is.EqualTo("清洁中"));
        }

        [Test]
        public void test_currentActivityLabel_traveling_returns_qianwang()
        {
            _hsk.currentState = Housekeeper2D.HousekeeperState.Traveling;
            Assert.That(_hsk.CurrentActivityLabel, Is.EqualTo("前往房间"));
        }

        // ── RemainingSeconds ────────────────────────────────────────────────

        [Test]
        public void test_remainingSeconds_when_idle_returns_zero()
        {
            // Arrange
            _hsk.currentState = Housekeeper2D.HousekeeperState.Idle;
            _hsk.cleaningDurationSeconds = 5f;
            _hsk.cleaningTimerSeconds = 2f;

            // Act / Assert
            Assert.That(_hsk.RemainingSeconds, Is.EqualTo(0f),
                "非 Working 状态 RemainingSeconds 必须为 0。");
        }

        [Test]
        public void test_remainingSeconds_when_working_reflects_duration_minus_timer()
        {
            // Arrange
            _hsk.currentState = Housekeeper2D.HousekeeperState.Working;
            _hsk.cleaningDurationSeconds = 5f;
            _hsk.cleaningTimerSeconds = 2f;

            // Act
            float remaining = _hsk.RemainingSeconds;

            // Assert
            Assert.That(remaining, Is.EqualTo(3f).Within(0.0001f),
                "Working 时 RemainingSeconds = duration - timer = 3s。");
        }

        [Test]
        public void test_remainingSeconds_clamps_to_zero_when_timer_overshoots()
        {
            // Arrange：timer 超过 duration（FinishCurrentRoom 调用前的临界帧）。
            _hsk.currentState = Housekeeper2D.HousekeeperState.Working;
            _hsk.cleaningDurationSeconds = 5f;
            _hsk.cleaningTimerSeconds = 6f;

            // Act
            float remaining = _hsk.RemainingSeconds;

            // Assert
            Assert.That(remaining, Is.EqualTo(0f),
                "timer 超过 duration 时 RemainingSeconds 应钳到 0，不返回负数。");
        }

        // ── AssignedRoomNumber ──────────────────────────────────────────────

        [Test]
        public void test_assignedRoomNumber_when_no_room_returns_null()
        {
            // Arrange
            _hsk.assignedRoom = null;

            // Act / Assert
            Assert.That(_hsk.AssignedRoomNumber, Is.Null,
                "未分配房间时 AssignedRoomNumber 必须为 null。");
        }

        [Test]
        public void test_assignedRoomNumber_returns_room_number()
        {
            // Arrange
            _hsk.assignedRoom = _room;

            // Act
            int? roomNumber = _hsk.AssignedRoomNumber;

            // Assert
            Assert.That(roomNumber.HasValue, Is.True);
            Assert.That(roomNumber.Value, Is.EqualTo(203),
                "AssignedRoomNumber 必须返回 assignedRoom.roomNumber。");
        }
    }
}
