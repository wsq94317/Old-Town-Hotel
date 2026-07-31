using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
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
        private GameObject _floorRoot;
        private GameObject[] _floorObjects;
        private FloorVisibilityController _floors;
        private ManagerCameraRig _rig;

        [SetUp]
        public void SetUp()
        {
            _floorRoot = new GameObject("Floors");
            _floorObjects = new GameObject[3];
            for (int i = 0; i < _floorObjects.Length; i++)
            {
                _floorObjects[i] = new GameObject("Floor" + (i + 1));
                _floorObjects[i].transform.SetParent(_floorRoot.transform);
            }
            _floors = _floorRoot.AddComponent<FloorVisibilityController>();
            _floors.SetFloorsForTesting(_floorObjects);

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
            Object.DestroyImmediate(_floorRoot);
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

        [Test]
        public void FocusOnPoint_SwitchesVisibleFloor_AndReturnRestoresManagerFloor()
        {
            Vector3 secondFloorRoom = new Vector3(4f, FloorMath.BaseYFor(1), 2f);

            _rig.FocusOnPoint(secondFloorRoom, instant: true);

            Assert.AreEqual(1, _floors.CurrentFloor);
            Assert.IsFalse(_floorObjects[0].activeSelf);
            Assert.IsTrue(_floorObjects[1].activeSelf);
            Assert.AreEqual(secondFloorRoom, _rig.FocusPoint);

            _rig.ReturnToTarget(instant: true);

            Assert.AreEqual(0, _floors.CurrentFloor);
            Assert.IsTrue(_floorObjects[0].activeSelf);
            Assert.IsFalse(_floorObjects[1].activeSelf);
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

    [TestFixture]
    public class ManagerControllerNavigationTest
    {
        [Test]
        public void ProjectToCurrentFloor_RemovesRoomTapColliderHeight()
        {
            Vector3 roomColliderHit = new Vector3(2.5f, 10.4f, -1.25f);

            Vector3 projected = ManagerController.ProjectToCurrentFloor(
                roomColliderHit,
                managerY: FloorMath.BaseYFor(2));

            Assert.AreEqual(new Vector3(2.5f, 8f, -1.25f), projected);
        }

        [Test]
        public void ProjectToCurrentFloor_UsesManagersFloorInsteadOfHitFloor()
        {
            Vector3 tallColliderHit = new Vector3(-3f, 4.2f, 2f);

            Vector3 projected = ManagerController.ProjectToCurrentFloor(
                tallColliderHit,
                managerY: FloorMath.BaseYFor(0));

            Assert.AreEqual(new Vector3(-3f, 0f, 2f), projected);
        }

        [Test]
        public void FloorProjection_PreservesTheScreenRayInsteadOfWallHitCoordinates()
        {
            var ray = new Ray(
                new Vector3(2f, 10f, 4f),
                new Vector3(-0.2f, -1f, -0.4f).normalized);

            bool projected = WorldInputController.TryProjectRayToFloor(ray, 0f, out Vector3 point);

            Assert.IsTrue(projected);
            Assert.AreEqual(0f, point.y, 0.0001f);
            Assert.Less(Vector3.Cross(point - ray.origin, ray.direction).magnitude, 0.0001f);
        }

        [Test]
        public void CommandSnap_RejectsAVisibleJumpIntoAnAdjacentRoom()
        {
            Vector3 requested = new Vector3(0f, 0f, 0f);
            Vector3 adjacentRoom = new Vector3(1.2f, 0f, 0f);

            Assert.IsFalse(ManagerController.IsAcceptableCommandSnap(
                requested,
                adjacentRoom,
                0.75f));
        }

        [Test]
        public void CommandSnap_AllowsMinorNavMeshEdgeCorrection()
        {
            Vector3 requested = new Vector3(0f, 0f, 0f);
            Vector3 nearbyWalkable = new Vector3(0.3f, 0f, 0.2f);

            Assert.IsTrue(ManagerController.IsAcceptableCommandSnap(
                requested,
                nearbyWalkable,
                0.75f));
        }
    }

    [TestFixture]
    public class GuestExitPortalTest
    {
        private static readonly Vector3 Door = new Vector3(0f, 0f, -5.2f);

        [Test]
        public void GuestNearDoorButBlockedFromExactPoint_CanExit()
        {
            var crowdedPosition = new Vector3(0.72f, 0.05f, -4.48f);

            Assert.IsTrue(GuestAgent.IsWithinExitPortal(
                crowdedPosition,
                Door,
                Vector3.back));
        }

        [Test]
        public void GuestStillInsideLobby_CannotExitEarly()
        {
            var lobbyPosition = new Vector3(0f, 0.05f, -3.8f);

            Assert.IsFalse(GuestAgent.IsWithinExitPortal(
                lobbyPosition,
                Door,
                Vector3.back));
        }

        [Test]
        public void GuestOnAnotherFloor_CannotTriggerGroundFloorExit()
        {
            var upstairsPosition = new Vector3(0f, FloorMath.BaseYFor(1), -5.0f);

            Assert.IsFalse(GuestAgent.IsWithinExitPortal(
                upstairsPosition,
                Door,
                Vector3.back));
        }

        [Test]
        public void GuestBesideDoorOpening_CannotExitThroughWall()
        {
            var besideDoor = new Vector3(1.8f, 0.05f, -5.0f);

            Assert.IsFalse(GuestAgent.IsWithinExitPortal(
                besideDoor,
                Door,
                Vector3.back));
        }
    }

    [TestFixture]
    public class RoomInvestmentMathTest
    {
        [Test]
        public void TotalInvestment_IncludesMaterialMarketValue()
        {
            Assert.AreEqual(1580, RoomInvestmentMath.TotalInvestment(
                cashCost: 1400,
                materialUnits: 4,
                materialUnitPrice: 45));
        }

        [Test]
        public void Payback_UsesIncrementalRevenueAndRoundsUp()
        {
            Assert.AreEqual(20, RoomInvestmentMath.SoldNightsToPayback(
                totalInvestment: 990,
                nightlyGain: 50));
        }

        [Test]
        public void Payback_ReportsNoReturnWhenRevenueDoesNotIncrease()
        {
            Assert.AreEqual(999, RoomInvestmentMath.SoldNightsToPayback(
                totalInvestment: 900,
                nightlyGain: 0));
        }
    }

    [TestFixture]
    public class HotelSceneExpansionContractTest
    {
        [Test]
        public void Expansion_PreservesSmallOpeningStock()
        {
            Assert.AreEqual(24, HotelSceneExpansion.ExpandedRoomCount);
            Assert.AreEqual(
                8,
                HotelSceneExpansion.ExpandedRoomCount -
                HotelSceneExpansion.StartingDerelictRooms);
        }

        [Test]
        public void RoomTapArea_StaysAtDoorAndOutOfNavigationBuild()
        {
            var room = new GameObject("Room_209");
            room.transform.localPosition = new Vector3(-12.5f, 0f, 4f);

            try
            {
                RoomSceneBinder binder = room.AddComponent<RoomSceneBinder>();
                binder.ConfigureRoomNumber(209);

                BoxCollider box = room.GetComponent<BoxCollider>();
                Assert.NotNull(box);
                Assert.AreEqual(1.1f, box.size.z, 0.001f);
                Assert.AreEqual(-2.05f, box.center.z, 0.001f);

                NavMeshModifier modifier = room.GetComponent<NavMeshModifier>();
                Assert.NotNull(modifier);
                Assert.IsTrue(modifier.ignoreFromBuild);
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }
    }
}
