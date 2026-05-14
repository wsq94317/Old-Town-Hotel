using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试：FrontDesk2D.SatisfactionScore（ui-spec.md §6 / §3.2 mood %）。
//
// 注意：满意度分数的权威所有者是 Room2DPrototypeDemandLoop.prototypeSatisfactionScore；
// FrontDesk2D.SatisfactionScore 是直通 getter，零行为变更。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FrontDesk2D_UiAccessors_Test
    {
        private GameObject _frontDeskGo;
        private FrontDesk2D _frontDesk;
        private GameObject _demandLoopGo;
        private Room2DPrototypeDemandLoop _demandLoop;

        [SetUp]
        public void SetUp()
        {
            // Arrange：构造受测组件 + 一个 demand loop 作为分数源。
            _demandLoopGo = new GameObject("TestDemandLoop");
            _demandLoop = _demandLoopGo.AddComponent<Room2DPrototypeDemandLoop>();
            _demandLoop.runDuringPlay = false;
            _demandLoop.autoFindReferences = false;

            _frontDeskGo = new GameObject("TestFrontDesk");
            _frontDesk = _frontDeskGo.AddComponent<FrontDesk2D>();
            _frontDesk.autoFindReferences = false;
            _frontDesk.runDuringPlay = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_frontDeskGo);
            Object.DestroyImmediate(_demandLoopGo);
        }

        [Test]
        public void test_satisfactionScore_when_demand_loop_null_returns_zero()
        {
            // Arrange：故意不连接 demandLoop。
            _frontDesk.demandLoop = null;

            // Act
            int score = _frontDesk.SatisfactionScore;

            // Assert
            Assert.That(score, Is.EqualTo(0),
                "demandLoop 未连接时 SatisfactionScore 应回退到 0，避免 NullReference。");
        }

        [Test]
        public void test_satisfactionScore_reflects_demandLoop_prototypeSatisfactionScore()
        {
            // Arrange
            _frontDesk.demandLoop = _demandLoop;
            _demandLoop.prototypeSatisfactionScore = 78;

            // Act
            int score = _frontDesk.SatisfactionScore;

            // Assert
            Assert.That(score, Is.EqualTo(78),
                "SatisfactionScore 应直通 demandLoop.prototypeSatisfactionScore 当前值。");
        }

        [Test]
        public void test_satisfactionScore_reflects_negative_score()
        {
            // Arrange：分数可能为负（demand loop 内部允许扣分）。
            _frontDesk.demandLoop = _demandLoop;
            _demandLoop.prototypeSatisfactionScore = -5;

            // Act
            int score = _frontDesk.SatisfactionScore;

            // Assert
            Assert.That(score, Is.EqualTo(-5),
                "SatisfactionScore 必须忠实反映负数分数，不做截断。");
        }
    }
}
