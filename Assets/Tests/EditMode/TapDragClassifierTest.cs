using NUnit.Framework;
using UnityEngine;

// 世界输入路由的判定核心：按下→(位移小+松手)=Tap；位移超阈值=Drag（此后本次按压不再是 Tap）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class TapDragClassifierTest
    {
        private TapDragClassifier _c;

        [SetUp]
        public void SetUp() => _c = new TapDragClassifier(dragThresholdPixels: 30f);

        [Test]
        public void PressAndReleaseWithoutMoving_IsTap()
        {
            _c.Press(new Vector2(100, 100));
            var result = _c.Release(new Vector2(102, 101)); // 微小抖动
            Assert.AreEqual(TapDragClassifier.Result.Tap, result);
        }

        [Test]
        public void MoveBeyondThreshold_BecomesDrag()
        {
            _c.Press(new Vector2(100, 100));
            Assert.IsFalse(_c.IsDragging);
            _c.Move(new Vector2(150, 100)); // 50px > 30px
            Assert.IsTrue(_c.IsDragging);
        }

        [Test]
        public void ReleaseAfterDrag_IsNotTap()
        {
            _c.Press(new Vector2(100, 100));
            _c.Move(new Vector2(150, 100));
            var result = _c.Release(new Vector2(101, 100)); // 拖回原点也不算 Tap
            Assert.AreEqual(TapDragClassifier.Result.Drag, result);
        }

        [Test]
        public void SmallMove_StaysTapEligible()
        {
            _c.Press(new Vector2(100, 100));
            _c.Move(new Vector2(110, 105)); // ~11px < 30px
            Assert.IsFalse(_c.IsDragging);
            Assert.AreEqual(TapDragClassifier.Result.Tap, _c.Release(new Vector2(110, 105)));
        }

        [Test]
        public void ReleaseWithoutPress_IsNone()
        {
            Assert.AreEqual(TapDragClassifier.Result.None, _c.Release(new Vector2(0, 0)));
        }

        [Test]
        public void NewPress_ResetsDragState()
        {
            _c.Press(new Vector2(100, 100));
            _c.Move(new Vector2(200, 100));
            _c.Release(new Vector2(200, 100));

            _c.Press(new Vector2(50, 50));
            Assert.IsFalse(_c.IsDragging);
            Assert.AreEqual(TapDragClassifier.Result.Tap, _c.Release(new Vector2(51, 50)));
        }
    }
}
