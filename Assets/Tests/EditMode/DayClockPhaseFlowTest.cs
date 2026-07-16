using NUnit.Framework;
using UnityEngine;
using DayPhase = Room2DDayPhaseStateMachine.Room2DDayPhase;

// day-cycle v2：时钟驱动相位流转回归——8:00 Preparation → 10:00 CheckInPeak →
// 18:00 Recovery → 22:00 Ended，全程无按钮，纯时钟里程碑推进。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DayClockPhaseFlowTest
    {
        private GameObject _root;
        private Room2DDemoDayController _controller;
        private Room2DDayPhaseStateMachine _phases;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("day-clock-root");
            _phases = _root.AddComponent<Room2DDayPhaseStateMachine>();
            _controller = _root.AddComponent<Room2DDemoDayController>();
            _controller.autoFindReferences = false;
            // EditMode 不跑 Start()：手动完成 Start 里做的接线。
            _controller.WireForTesting(_phases);
            _phases.InitialiseForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        // 默认配置：180 秒一天。用固定步长 tick 到目标时刻。
        private void TickUntilHour(float hour)
        {
            int guard = 0;
            while (_controller.Clock.CurrentHour < hour && guard++ < 10000)
            {
                _controller.TickClock(0.5f);
            }
        }

        [Test]
        public void Clock_StartsAtEight_InPreparation()
        {
            Assert.AreEqual(DayPhase.Preparation, _phases.CurrentPhase);
            Assert.AreEqual(8f, _controller.Clock.CurrentHour, 0.01f);
        }

        [Test]
        public void TenOClock_OpensDoors_EntersCheckInPeak()
        {
            TickUntilHour(10f);
            Assert.AreEqual(DayPhase.CheckInPeak, _phases.CurrentPhase);
        }

        [Test]
        public void SixPm_EntersRecovery()
        {
            TickUntilHour(18f);
            Assert.AreEqual(DayPhase.Recovery, _phases.CurrentPhase);
        }

        [Test]
        public void TenPm_EndsDay()
        {
            TickUntilHour(22f);
            _controller.TickClock(0.5f); // 到点后再 tick 一次触发日结
            Assert.AreEqual(DayPhase.Ended, _phases.CurrentPhase);
        }

        [Test]
        public void EndedPhase_ClockStopsTicking()
        {
            TickUntilHour(22f);
            _controller.TickClock(0.5f);
            Assert.AreEqual(DayPhase.Ended, _phases.CurrentPhase);
            float frozen = _controller.Clock.CurrentHour;
            _controller.TickClock(10f);
            Assert.AreEqual(frozen, _controller.Clock.CurrentHour, 0.0001f);
        }

        [Test]
        public void RestartDemoDay_RewindsClockToMorning()
        {
            TickUntilHour(22f);
            _controller.TickClock(0.5f);
            _controller.RestartDemoDay();
            Assert.AreEqual(DayPhase.Preparation, _phases.CurrentPhase);
            Assert.AreEqual(8f, _controller.Clock.CurrentHour, 0.01f);
            Assert.AreEqual(2, _controller.CurrentDay);
        }

        [Test]
        public void TimeLabel_ShowsClockFace()
        {
            Assert.AreEqual("08:00", _phases.CurrentTimeOfDayLabel);
            TickUntilHour(10f);
            StringAssert.Contains(":", _phases.CurrentTimeOfDayLabel);
        }
    }
}
