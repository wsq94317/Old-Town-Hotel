using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase=UnityEngine.InputSystem.TouchPhase;

namespace OldTownHotel.Tests.PlayMode
{
    public class DirectorExperiencePlayTest
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private WorldManagementHud _hud;
        private ManagerCameraRig _rig;
        private readonly List<InputDevice> _devices=new List<InputDevice>();
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private InputSettings.UpdateMode _inputUpdateMode;
        private string _inputTrace="";
        private WorldInputController _manuallyPumpedInput;
        private void PumpCameraInput()
        {
            InputSystem.Update();
            var input=Object.FindFirstObjectByType<WorldInputController>();
            var result=typeof(WorldInputController).GetMethod("TickCameraInput",Private).Invoke(input,null);
            _inputTrace+=" [blocked="+WorldManagementHud.SceneCameraInputBlocked+", mouse="+Mouse.current?.name+", right="+Mouse.current?.rightButton.isPressed
                +", pos="+Mouse.current?.position.ReadValue()+", over="+(Mouse.current!=null&&WorldManagementHud.ContainsScreenPoint(Mouse.current.position.ReadValue()))
                +", orbit="+typeof(WorldInputController).GetField("_orbitMouse",Private).GetValue(input)+", touches="+(Touchscreen.current!=null?Touchscreen.current.touches.Count(x=>x.press.isPressed):0)
                +", result="+result+", yaw="+_rig.Yaw+", zoom="+_rig.Zoom+"]";
        }
        private object Hud(string method,params object[] args)=>typeof(WorldManagementHud).GetMethod(method,Private).Invoke(_hud,args);
        private Button Button(string name)=>_hud.GetComponentsInChildren<Button>(true).Single(x=>x.name==name);
        [UnitySetUp]
        public IEnumerator LoadHotel()
        {
            Time.timeScale=1;SceneManager.LoadScene("Assets/Game/Scenes/Hotel_Manager_25D.unity");
            _backgroundBehavior=InputSystem.settings.backgroundBehavior;
            _inputTrace="";
            _inputUpdateMode=InputSystem.settings.updateMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            yield return null;yield return null;
            typeof(SaveCoordinator).GetField("autoSaveOnDayEnd",Private).SetValue(Object.FindFirstObjectByType<SaveCoordinator>(),false);
            _hud=Object.FindFirstObjectByType<WorldManagementHud>();
            typeof(WorldManagementHud).GetField("_sessionInitialized",Private).SetValue(_hud,true);
            Hud("CloseSessionMenu");HotelSimSceneBridge.Instance.IsSuspended=true;RoomSelection.Clear();
            Object.FindFirstObjectByType<WorldInputController>().SetDirectorMode(true);
            _rig=Object.FindFirstObjectByType<ManagerCameraRig>();_rig.FocusOnPoint(HotelFloorPlan.Room(201).Centre,true);
            yield return null;Hud("RefreshSceneControls");
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach(var device in _devices)if(device.added)InputSystem.RemoveDevice(device);_devices.Clear();
            InputSystem.settings.backgroundBehavior=_backgroundBehavior;
            InputSystem.settings.updateMode=_inputUpdateMode;
            if(_manuallyPumpedInput!=null)_manuallyPumpedInput.enabled=true;
            Time.timeScale=1;yield return null;
        }

        [UnityTest]
        public IEnumerator WholeRoomSelectionWorksFromEverySideWithoutMovingManager()
        {
            var manager=Object.FindFirstObjectByType<ManagerController>();var position=manager.transform.position;
            var cam=_rig.GetComponent<Camera>();
            foreach(float angle in new[]{0f,90f,180f,270f}) {
                _rig.OrbitBy(Mathf.DeltaAngle(_rig.Yaw,angle));yield return new WaitForSecondsRealtime(.15f);
                var point=HotelFloorPlan.Room(201).Centre;
                Assert.That(WorldInputController.TryFindDirectorRoom(cam.ScreenPointToRay(cam.WorldToScreenPoint(point)),1,out var room),Is.True);
                Assert.That(room.RoomNumber,Is.EqualTo(201));
            }
            Assert.That(manager.transform.position,Is.EqualTo(position));
            _rig.SetOverhead(true);yield return new WaitForSecondsRealtime(.3f);
            Assert.That(_rig.Pitch,Is.EqualTo(80));
            var binder=Object.FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.RoomNumber==201);
            var label=binder.GetComponentInChildren<TextMesh>(true);
            Assert.That(Quaternion.Angle(label.transform.rotation,_rig.transform.rotation),Is.LessThan(2),"Room labels must remain readable in overhead view.");
        }

        [UnityTest]
        public IEnumerator RoomNavigationReachesTheFarWingAndHasFiftyAccessibleTargets()
        {
            Button("RoomNavigator").onClick.Invoke();yield return null;
            var navigator=(RectTransform)typeof(WorldManagementHud).GetField("_sceneNavigator",Private).GetValue(_hud);
            Assert.That(navigator.gameObject.activeInHierarchy,Is.True);
            Assert.That(navigator.GetComponentsInChildren<Button>().Count(x=>x.name.StartsWith("NavigateRoom_")),Is.EqualTo(50));
            var button=Button("NavigateRoom_50");var rect=(RectTransform)button.transform;
            var centre=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=centre},hits);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button));
            button.onClick.Invoke();yield return null;
            Assert.That(RoomSelection.Selected,Is.EqualTo(250));
            Assert.That(Vector3.Distance(_rig.FocusPoint,HotelFloorPlan.Room(250).Centre),Is.LessThan(.1f));Assert.That(_rig.Zoom,Is.EqualTo(6.2f));
            Assert.That(navigator.gameObject.activeSelf,Is.False);
        }

        [UnityTest]
        public IEnumerator PlanningPauseKeepsCameraInteractiveAndResumesTheChosenSpeed()
        {
            Button("PlanningPause").onClick.Invoke();Assert.That(Time.timeScale,Is.Zero);
            var before=_rig.transform.rotation;_rig.OrbitBy(90);_rig.ZoomBy(-1);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Quaternion.Angle(before,_rig.transform.rotation),Is.GreaterThan(25));
            Button("PlanningPause").onClick.Invoke();Assert.That(Time.timeScale,Is.EqualTo(1));
            Button("PlanningSpeed").onClick.Invoke();Assert.That(Time.timeScale,Is.EqualTo(3));
            Button("PlanningSpeed").onClick.Invoke();Assert.That(Time.timeScale,Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator ServicePrioritySurvivesCaptureWithoutChargingOrTeleportingManager()
        {
            var dispatcher=Object.FindFirstObjectByType<TaskDispatcher>();
            var room=Object.FindObjectsByType<Room2DEntity>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.roomNumber==201);
            room.SetState(Room2DState.Dirty);
            var manager=Object.FindFirstObjectByType<ManagerController>();var position=manager.transform.position;
            int cash=HotelSimSceneBridge.Instance.Sim.Cash;
            Assert.That(dispatcher.PrioritizeRoom(room,out var message),Is.True,message);
            var save=Object.FindFirstObjectByType<SaveCoordinator>().Capture();
            Assert.That(save.world.servicePriorityRooms,Does.Contain(201));
            dispatcher.RestoreFrom(JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(save.world)));
            Assert.That(dispatcher.IsPriority(201),Is.True);
            Assert.That(HotelSimSceneBridge.Instance.Sim.Cash,Is.EqualTo(cash));
            Assert.That(manager.transform.position,Is.EqualTo(position));yield return null;
        }

        [UnityTest]
        public IEnumerator OrbitingMovesTheWindowCutawayToTheNearSide()
        {
            var room=Object.FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.RoomNumber==201);
            var wall=room.GetComponentsInChildren<MeshRenderer>(true).Single(x=>x.name=="Craft_Plaster_A3B3A0");
            _rig.ResetView();yield return new WaitForSecondsRealtime(.3f);Assert.That(wall.enabled,Is.True);
            _rig.OrbitBy(180);yield return new WaitForSecondsRealtime(.5f);Assert.That(wall.enabled,Is.False);
        }

        [UnityTest]
        public IEnumerator PriorityWaitsForCurrentWorkThenTakesTheNextWorker()
        {
            var dispatcher=Object.FindFirstObjectByType<TaskDispatcher>();
            dispatcher.RestoreFrom(new WorldState());
            var agent=Object.FindObjectsByType<StaffAgent>(FindObjectsSortMode.None).First(x=>x.Member!=null&&x.Member.Role==StaffRole.Housekeeper);
            agent.AbortTask();
            typeof(StaffAgent).GetField("_shiftState",Private).SetValue(agent,StaffShiftState.OnDuty);
            typeof(StaffAgent).GetField("_activityState",Private).SetValue(agent,StaffActivityState.Idle);
            var rooms=Object.FindObjectsByType<Room2DEntity>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var first=rooms.Single(x=>x.roomNumber==201);var priority=rooms.Single(x=>x.roomNumber==203);
            first.SetState(Room2DState.Dirty);priority.SetState(Room2DState.Dirty);
            Assert.That(dispatcher.ForceAssign(agent,first),Is.True);
            Assert.That(dispatcher.PrioritizeRoom(priority,out _),Is.True);
            Assert.That(agent.AssignedRoomNumber,Is.EqualTo(201),"A priority order must not discard work already in progress.");
            agent.AbortTask();
            typeof(StaffAgent).GetField("_shiftState",Private).SetValue(agent,StaffShiftState.OnDuty);
            typeof(StaffAgent).GetField("_activityState",Private).SetValue(agent,StaffActivityState.Idle);
            typeof(TaskDispatcher).GetMethod("Dispatch",Private).Invoke(dispatcher,null);
            Assert.That(agent.AssignedRoomNumber,Is.EqualTo(203));yield return null;
        }

        [UnityTest]
        public IEnumerator RemoteStaffInspectionDoesNotCommandACrossFloorWalk()
        {
            var manager=Object.FindFirstObjectByType<ManagerController>();
            Assert.That(manager.TryRestorePosition(new Vector3(0,4.05f,0)),Is.True);
            var before=manager.transform.position;_rig.ViewFloor(0);
            var staff=Object.FindObjectsByType<StaffAgent>(FindObjectsSortMode.None).First(x=>x.Member!=null&&x.Member.Role==StaffRole.Reception);
            var interaction=Object.FindFirstObjectByType<ManagerInteraction>();interaction.InspectStaffRemotely(staff);
            yield return null;
            Assert.That(interaction.ActiveHudAgent,Is.SameAs(staff));
            Assert.That(manager.IsMoving,Is.False);Assert.That(manager.transform.position,Is.EqualTo(before));
            Assert.That(_rig.ViewedFloor,Is.Zero);
        }

        [UnityTest]
        public IEnumerator RightMouseDragOrbitsWithoutGeneratingARoomTap()
        {
            // Pump the router alongside the synthetic events; Editor updates otherwise swap the input state buffers between coroutine steps.
            _manuallyPumpedInput=Object.FindFirstObjectByType<WorldInputController>();_manuallyPumpedInput.enabled=false;
            InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            var mouse=InputSystem.AddDevice<Mouse>();_devices.Add(mouse);
            var centre=new Vector2(Screen.width*.5f,Screen.height*.5f);float yaw=_rig.Yaw;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=centre}.WithButton(MouseButton.Right));PumpCameraInput();yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=centre+new Vector2(100,0)}.WithButton(MouseButton.Right));PumpCameraInput();yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=centre+new Vector2(100,0)});PumpCameraInput();yield return null;
            _manuallyPumpedInput.enabled=true;yield return null;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw,_rig.Yaw)),Is.GreaterThan(10),_inputTrace);
            Assert.That(RoomSelection.Selected,Is.Zero);
        }

        [UnityTest]
        public IEnumerator TwoFingerPinchZoomsWithoutLeakingATapAfterRelease()
        {
            _manuallyPumpedInput=Object.FindFirstObjectByType<WorldInputController>();_manuallyPumpedInput.enabled=false;
            InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            var touch=InputSystem.AddDevice<Touchscreen>();_devices.Add(touch);
            var centre=new Vector2(Screen.width*.5f,Screen.height*.5f);float zoom=_rig.Zoom;
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=TouchPhase.Began,position=centre-Vector2.right*45});
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=2,phase=TouchPhase.Began,position=centre+Vector2.right*45});PumpCameraInput();yield return null;
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=TouchPhase.Moved,position=centre-Vector2.right*90});
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=2,phase=TouchPhase.Moved,position=centre+Vector2.right*90});PumpCameraInput();yield return null;
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=TouchPhase.Ended,position=centre-Vector2.right*90});
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=2,phase=TouchPhase.Ended,position=centre+Vector2.right*90});PumpCameraInput();yield return null;yield return null;
            _manuallyPumpedInput.enabled=true;yield return null;
            Assert.That(_rig.Zoom,Is.LessThan(zoom),_inputTrace);Assert.That(RoomSelection.Selected,Is.Zero);
        }
    }
}
