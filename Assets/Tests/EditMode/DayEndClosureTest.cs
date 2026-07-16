using NUnit.Framework;
using UnityEngine;

// day-cycle v2：日终统一收口回归——进入 Ended 的任何路径（EndDemoDay / 相位机推进 /
// ForceJumpToEnded）都恰好结算一次；日终房态按"当班完成"规则收尾。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DayEndClosureTest
    {
        private GameObject _root;
        private Room2DDemoDayController _controller;
        private Room2DDayPhaseStateMachine _phases;
        private EconomySystem _economy;
        private EconomyConfigSO _config;
        private int _settledCount;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("day-end-root");
            _phases = _root.AddComponent<Room2DDayPhaseStateMachine>();
            _controller = _root.AddComponent<Room2DDemoDayController>();
            _controller.autoFindReferences = false;

            _config = ScriptableObject.CreateInstance<EconomyConfigSO>();
            _config.startingCash = 1000;
            _config.startingLoan = 0;
            _config.dailyInterestRate = 0f;
            _economy = _root.AddComponent<EconomySystem>();
            _economy.InitializeForTest(_config);
            _controller.economy = _economy;

            _controller.WireForTesting(_phases);
            _phases.InitialiseForTesting();
            _settledCount = 0;
            _controller.OnDaySettled += CountSettle;
        }

        private void CountSettle(int day, int served, DayLedger ledger) => _settledCount++;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [Test]
        public void EndDemoDay_SettlesExactlyOnce()
        {
            _controller.EndDemoDay();
            Assert.AreEqual(1, _settledCount);
        }

        [Test]
        public void EndDemoDay_Twice_StillSettlesOnce()
        {
            _controller.EndDemoDay();
            _controller.EndDemoDay();
            Assert.AreEqual(1, _settledCount);
        }

        [Test]
        public void PhaseMachineAdvanceToEnded_AlsoSettles()
        {
            // 双路径 bug 回归测试：仅靠相位机推到 Ended（旧 HUD 按钮路径）也必须结算。
            _phases.RequestAdvancePhase(); // → CheckInPeak
            _phases.RequestAdvancePhase(); // → Recovery
            _phases.RequestAdvancePhase(); // → Ended
            Assert.AreEqual(1, _settledCount);
        }

        [Test]
        public void ForceJumpToEnded_AlsoSettles()
        {
            _phases.ForceJumpToEnded();
            Assert.AreEqual(1, _settledCount);
        }

        [Test]
        public void NextDay_SettlesAgain()
        {
            _controller.EndDemoDay();
            _controller.RestartDemoDay();
            _controller.EndDemoDay();
            Assert.AreEqual(2, _settledCount);
        }

        [Test]
        public void DayEnd_FinishesInProgressCleaning()
        {
            var roomGo = new GameObject("room-101");
            var room = roomGo.AddComponent<Room2DEntity>();
            room.roomNumber = 101;
            room.currentState = Room2DState.Dirty;
            var hskGo = new GameObject("hsk");
            var hsk = hskGo.AddComponent<Housekeeper2D>();
            Assert.IsTrue(hsk.AssignRoom(room)); // Dirty → Cleaning，HSK 直接进 Working
            Assert.AreEqual(Room2DState.Cleaning, room.currentState);

            _controller.EndDemoDay();

            // 当班完成：Cleaning → AwaitingInspection，员工归位 Idle。
            Assert.AreEqual(Room2DState.AwaitingInspection, room.currentState);
            Assert.IsFalse(hsk.IsBusy);

            Object.DestroyImmediate(roomGo);
            Object.DestroyImmediate(hskGo);
        }
    }
}
