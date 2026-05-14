using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试：Room2DDayPhaseStateMachine.CurrentTimeOfDayLabel（ui-spec.md §6 顶部时钟）。
// 当前原型没有真实日内时钟，因此 getter 返回每阶段对应的占位标签。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class Room2DDayPhaseStateMachine_UiAccessors_Test
    {
        private GameObject _go;
        private Room2DDayPhaseStateMachine _sm;

        [SetUp]
        public void SetUp()
        {
            // Arrange：AddComponent 触发 Awake，静默初始化为 Preparation（不广播事件）。
            _go = new GameObject("TestPhaseStateMachine_UiAccessors");
            _sm = _go.AddComponent<Room2DDayPhaseStateMachine>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void test_currentTimeOfDayLabel_in_preparation_returns_PREP()
        {
            // Arrange / Act
            string label = _sm.CurrentTimeOfDayLabel;

            // Assert
            Assert.That(label, Is.EqualTo("PREP"),
                "初始 Preparation 阶段应返回占位标签 PREP。");
        }

        [Test]
        public void test_currentTimeOfDayLabel_in_checkInPeak_returns_PEAK()
        {
            // Arrange
            _sm.InitialiseForTesting();
            _sm.RequestAdvancePhase(); // Preparation → CheckInPeak

            // Act
            string label = _sm.CurrentTimeOfDayLabel;

            // Assert
            Assert.That(label, Is.EqualTo("PEAK"),
                "CheckInPeak 阶段应返回占位标签 PEAK。");
        }

        [Test]
        public void test_currentTimeOfDayLabel_in_recovery_returns_RECOVERY()
        {
            // Arrange
            _sm.InitialiseForTesting();
            _sm.RequestAdvancePhase(); // → CheckInPeak
            _sm.RequestAdvancePhase(); // → Recovery

            // Act
            string label = _sm.CurrentTimeOfDayLabel;

            // Assert
            Assert.That(label, Is.EqualTo("RECOVERY"),
                "Recovery 阶段应返回占位标签 RECOVERY。");
        }

        [Test]
        public void test_currentTimeOfDayLabel_in_ended_returns_ENDED()
        {
            // Arrange
            _sm.InitialiseForTesting();
            _sm.ForceJumpToEnded();

            // Act
            string label = _sm.CurrentTimeOfDayLabel;

            // Assert
            Assert.That(label, Is.EqualTo("ENDED"),
                "Ended 阶段应返回占位标签 ENDED。");
        }
    }
}
