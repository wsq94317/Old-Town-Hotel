using System.Reflection;
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

    [TestFixture]
    public class ManagerCameraRigTest
    {
        private GameObject _cameraObject;
        private GameObject _targetObject;
        private ManagerCameraRig _rig;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("Camera");
            _cameraObject.AddComponent<Camera>().orthographic = true;
            _rig = _cameraObject.AddComponent<ManagerCameraRig>();

            _targetObject = new GameObject("Manager");
            _targetObject.transform.position = new Vector3(2f, 0f, 1f);
            SetPrivateField(_rig, "target", _targetObject.transform);
            _rig.ReturnToTarget(instant: true);
            InvokeLateUpdate();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cameraObject);
            Object.DestroyImmediate(_targetObject);
        }

        [Test]
        public void ReleasingFreeLook_DoesNotResumeFollowing()
        {
            _rig.BeginFreeLook();
            _rig.EndFreeLook();

            Assert.IsFalse(_rig.IsFollowingTarget);
        }

        [Test]
        public void TargetMovement_DoesNotMoveFreeLookFocus()
        {
            _rig.BeginFreeLook();
            _rig.EndFreeLook();
            Vector3 freeLookFocus = _rig.FocusPoint;

            _targetObject.transform.position = new Vector3(8f, 0f, 5f);
            InvokeLateUpdate();

            Assert.AreEqual(freeLookFocus, _rig.FocusPoint);
        }

        [Test]
        public void ReturnToTarget_ExplicitlyRestoresFollowMode()
        {
            _rig.BeginFreeLook();
            _rig.EndFreeLook();

            _rig.ReturnToTarget(instant: true);

            Assert.IsTrue(_rig.IsFollowingTarget);
            Assert.AreEqual(_targetObject.transform.position, _rig.FocusPoint);
        }

        private void InvokeLateUpdate()
        {
            typeof(ManagerCameraRig)
                .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(_rig, null);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            typeof(ManagerCameraRig)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(instance, value);
        }
    }
}
