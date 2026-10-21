using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OldTownHotel.Tests.PlayMode
{
    public class CourtyardHotelPlayTest
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [UnitySetUp] public IEnumerator LoadHotel()
        {
            Time.timeScale=1;SceneManager.LoadScene("Assets/Game/Scenes/Hotel_Manager_25D.unity");yield return null;yield return null;
            typeof(SaveCoordinator).GetField("autoSaveOnDayEnd",Private).SetValue(Object.FindFirstObjectByType<SaveCoordinator>(),false);
            var hud=Object.FindFirstObjectByType<WorldManagementHud>();typeof(WorldManagementHud).GetField("_sessionInitialized",Private).SetValue(hud,true);
            typeof(WorldManagementHud).GetMethod("CloseSessionMenu",Private).Invoke(hud,null);HotelSimSceneBridge.Instance.IsSuspended=true;
        }
        [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;yield return null;}
        [UnityTest] public IEnumerator EveryStoreyUsesTheSameFootprintAndAlignedLiftShaft()
        {
            var world=GameObject.Find("World").transform;
            for(int floor=0;floor<FloorMath.FloorCount;floor++){
                var root=world.Find("Floor"+(floor+1));var ground=root.Find("Ground");
                Assert.That(ground.localScale.x,Is.EqualTo(86));Assert.That(ground.localScale.z,Is.EqualTo(40));
                Assert.That(root.Find("CourtyardLayout_v1"),Is.Not.Null);
                if(floor==1||floor==2)foreach(Transform fitting in root.Find("ArtPass_LowPoly"))
                    Assert.That(fitting.name,Is.Not.EqualTo("WallLamp").And.Not.EqualTo("RoomPlaque"),"Legacy strip fittings must not float in the central hall.");
                Assert.That(root.Find("ElevatorPad").position.x,Is.EqualTo(9.3f).Within(.01f));
                Assert.That(root.Find("ElevatorPad").position.z,Is.EqualTo(0).Within(.01f));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator AllHundredRoomsAndBothLinenClosetsHaveCompleteNavigationPaths()
        {
            var path=new NavMeshPath();
            foreach(int floor in new[]{1,2}){
                var start=FloorNavigator.StairExitOf(floor);
                Assert.That(NavMesh.SamplePosition(start,out var from,.6f,NavMesh.AllAreas),Is.True);
                foreach(var room in Object.FindObjectsByType<RoomDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(d=>FloorMath.FloorIndexForY(d.InteriorCenter.y)==floor)){
                    Assert.That(NavMesh.SamplePosition(room.ExteriorInteractionPoint,out var entry,.6f,NavMesh.AllAreas),Is.True,room.name+" entry");
                    Assert.That(NavMesh.CalculatePath(from.position,entry.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True,room.name+" unreachable");
                    Assert.That(NavMesh.SamplePosition(room.InteriorCenter,out var inside,1.2f,NavMesh.AllAreas),Is.True,room.name+" interior");
                    Assert.That(NavMesh.CalculatePath(entry.position,inside.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True,room.name+" door blocked");
                }
                var node=GameObject.Find("World").transform.Find("Floor"+(floor+1)+"/Floor"+(floor+1)+"HskSupportAnchor");
                Assert.That(node.localPosition,Is.EqualTo(HotelFloorPlan.Linen));
                var approach=node.position+Vector3.back*1.4f;
                Assert.That(NavMesh.SamplePosition(approach,out var linen,.6f,NavMesh.AllAreas),Is.True);
                Assert.That(NavMesh.CalculatePath(from.position,linen.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True,"Linen closet unreachable");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator UpperLightWellsHaveNoWalkableFloorAndBothEndGalleriesRemainConnected()
        {
            foreach(int floor in new[]{1,2})foreach(int side in new[]{-1,1}){
                float y=FloorMath.BaseYFor(floor);var centre=new Vector3(side*22.5f,y,0);
                Assert.That(NavMesh.SamplePosition(centre,out _,.5f,NavMesh.AllAreas),Is.False,"A guest must not walk over the courtyard void.");
                Assert.That(NavMesh.SamplePosition(new Vector3(side*41,y,10),out var a,.5f,NavMesh.AllAreas),Is.True);
                Assert.That(NavMesh.SamplePosition(new Vector3(side*41,y,-10),out var b,.5f,NavMesh.AllAreas),Is.True);
                var path=new NavMeshPath();Assert.That(NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator EntranceIsOnTheFacadeAndGroundStaffFacilitiesAreOutsideGuestCirculation()
        {
            var path=new NavMeshPath();Assert.That(NavMesh.SamplePosition(HotelFloorPlan.Entrance,out var entry,.6f,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(new Vector3(.9f,0,2.2f),out var desk,.6f,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.CalculatePath(entry.position,desk.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True);
            var anchor=GameObject.Find("World").transform.Find("Floor1/LobbyStaffBreakRoomAnchor");
            Assert.That(anchor.localPosition.x,Is.LessThan(-15));Assert.That(anchor.localPosition.z,Is.LessThan(-8));
            yield return null;
        }
        [UnityTest] public IEnumerator HousekeepingReplenishesAtTheGuestLinenRoomAndGetsAWorkingIntervalAfterReturning()
        {
            var worker=Object.FindObjectsByType<StaffAgent>(FindObjectsSortMode.None).First(x=>x.Member.Role==StaffRole.Housekeeper);
            typeof(StaffAgent).GetMethod("CompleteArrival",Private).Invoke(worker,null);
            var target=(Vector3)typeof(StaffAgent).GetField("_routineTarget",Private).GetValue(worker);
            Assert.That(FloorMath.FloorIndexForY(target.y),Is.EqualTo(1),"Linen must not be collected from the fourth-floor kitchen.");
            typeof(StaffAgent).GetField("_nextPersonalNeedAt",Private).SetValue(worker,Time.time-100);
            typeof(StaffAgent).GetMethod("EnterDefaultActivity",Private).Invoke(worker,null);
            Assert.That((float)typeof(StaffAgent).GetField("_nextPersonalNeedAt",Private).GetValue(worker),Is.GreaterThan(Time.time+20));
            yield return null;
        }
        [UnityTest] public IEnumerator HousekeeperPhysicallyWalksIntoTheNewWingAndCompletesCleaning()
        {
            var bridge=HotelSimSceneBridge.Instance;bridge.enabled=false;bridge.IsSuspended=false;
            var worker=Object.FindObjectsByType<StaffAgent>(FindObjectsSortMode.None).First(x=>x.Member.Role==StaffRole.Housekeeper);
            worker.AbortTask();worker.GetComponent<NavMeshAgent>().Warp(FloorNavigator.StairExitOf(1));
            typeof(StaffAgent).GetField("_shiftState",Private).SetValue(worker,StaffShiftState.OnDuty);
            typeof(StaffAgent).GetField("_observedDay",Private).SetValue(worker,Object.FindFirstObjectByType<Room2DDemoDayController>().CurrentDay);
            typeof(StaffAgent).GetField("_slack",Private).SetValue(worker,null);
            typeof(StaffAgent).GetMethod("EnterDefaultActivity",Private).Invoke(worker,null);
            var room=Object.FindObjectsByType<Room2DEntity>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.roomNumber==201);
            room.SetState(Room2DState.Dirty);
            Assert.That(Object.FindFirstObjectByType<TaskDispatcher>().ForceAssign(worker,room),Is.True);
            typeof(WorldManagementHud).GetMethod("SetSpeed",Private).Invoke(Object.FindFirstObjectByType<WorldManagementHud>(),new object[]{6f});
            float deadline=Time.unscaledTime+7;
            while((room.currentState==Room2DState.Dirty||room.currentState==Room2DState.Cleaning)&&Time.unscaledTime<deadline)yield return null;
            var nav=worker.GetComponent<NavMeshAgent>();
            Assert.That(room.currentState,Is.Not.EqualTo(Room2DState.Dirty).And.Not.EqualTo(Room2DState.Cleaning),
                worker.State+" / "+worker.ActivityState+" at "+worker.transform.position+" -> "+nav.destination+", remaining "+nav.remainingDistance+", shift "+worker.ShiftState);
            bridge.enabled=true;bridge.IsSuspended=true;
        }
    }
}
