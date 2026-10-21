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
    public class CompleteHotelPlayTest
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp]
        public IEnumerator LoadHotel()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene("Assets/Game/Scenes/Hotel_Manager_25D.unity");
            yield return null; yield return null;
            var save = Object.FindFirstObjectByType<SaveCoordinator>();
            typeof(SaveCoordinator).GetField("autoSaveOnDayEnd", Private).SetValue(save,false);
        }

        [UnityTearDown]
        public IEnumerator RestoreTime()
        { Time.timeScale = 1; yield return null; }

        [UnityTest]
        public IEnumerator HotelHasOneHundredReachableRoomsAndRealCharacters()
        {
            var bridge = HotelSimSceneBridge.Instance;
            Assert.That(bridge.Rooms.Count, Is.EqualTo(100));
            Assert.That(Object.FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Select(x=>x.RoomNumber).Distinct().Count(), Is.EqualTo(100));
            Assert.That(NavMesh.SamplePosition(HotelFloorPlan.Room(250).Door-HotelFloorPlan.Room(250).Outward,out _,.75f,NavMesh.AllAreas), Is.True);
            foreach(var actor in Object.FindObjectsByType<StaffAgent>(FindObjectsSortMode.None))
                {
                    var art=actor.GetComponentInChildren<HotelCharacterArt>();
                    Assert.That(art,Is.Not.Null);
                    Assert.That(art.transform.Find("Body/Head/Eyes"),Is.Not.Null);
                    Assert.That(art.transform.Find("LeftLeg"),Is.Not.Null);
                }
            Assert.That(Time.timeScale, Is.Zero, "The welcome menu pauses hotel operations.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewInterfaceHasRenderedIconsReadableMenuAndPortraitCards()
        {
            var hud=Object.FindFirstObjectByType<WorldManagementHud>();
            Assert.That(hud.GetComponentsInChildren<UnityEngine.UI.RawImage>().Any(x=>x.texture!=null),Is.True,"Menu illustration must be connected.");
            Assert.That(hud.GetComponentsInChildren<HotelGlyph>().All(x=>x.GetComponent<CanvasRenderer>()!=null),Is.True);
            typeof(WorldManagementHud).GetMethod("CloseSessionMenu",Private).Invoke(hud,null);
            HotelSimSceneBridge.Instance.IsSuspended=true;
            var tab=typeof(WorldManagementHud).GetNestedType("DeskTab",BindingFlags.NonPublic);
            typeof(WorldManagementHud).GetMethod("SelectTab",Private).Invoke(hud,new[]{System.Enum.Parse(tab,"Staff")});
            yield return null;
            Assert.That(hud.GetComponentsInChildren<UnityEngine.UI.RawImage>().Count(x=>x.name=="Image"&&x.texture!=null),Is.GreaterThan(0));
            Assert.That(hud.GetComponentsInChildren<HotelGlyph>().Any(x=>x.canvasRenderer.GetMesh().vertexCount>0),Is.True);
        }

        [UnityTest]
        public IEnumerator WalkingUsesArticulatedLegsAndMenuFreezesTheirPose()
        {
            var hud=Object.FindFirstObjectByType<WorldManagementHud>();
            typeof(WorldManagementHud).GetMethod("CloseSessionMenu",Private).Invoke(hud,null);
            HotelSimSceneBridge.Instance.IsSuspended=true;
            var manager=Object.FindFirstObjectByType<ManagerController>();
            Assert.That(manager.TryRestorePosition(new Vector3(0,.05f,0)),Is.True);
            var art=manager.GetComponentInChildren<HotelCharacterArt>();
            var leg=art.transform.Find("LeftLeg");
            Assert.That(manager.TryMoveTo(new Vector3(-4,0,0),out _),Is.True);
            yield return new WaitForSeconds(.2f);
            Assert.That(art.MotionState,Is.EqualTo("Walk"));
            var first=leg.localRotation;
            yield return new WaitForSeconds(.17f);
            Assert.That(Quaternion.Angle(first,leg.localRotation),Is.GreaterThan(2));
            typeof(WorldManagementHud).GetMethod("ShowSessionMenu",Private).Invoke(hud,new object[]{"main"});
            var frozen=leg.localRotation;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Quaternion.Angle(frozen,leg.localRotation),Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator InspectingLobbyStaffCannotMoveTheManagerOnAHiddenFloor()
        {
            var hud = Object.FindFirstObjectByType<WorldManagementHud>();
            typeof(WorldManagementHud).GetMethod("CloseSessionMenu",Private).Invoke(hud,null);
            var bridge = HotelSimSceneBridge.Instance; bridge.IsSuspended = true;
            var manager = Object.FindFirstObjectByType<ManagerController>();
            Assert.That(manager.TryRestorePosition(new Vector3(0,4.05f,0)), Is.True);
            var before = manager.transform.position;
            var rig = Object.FindFirstObjectByType<ManagerCameraRig>();
            rig.FocusOnPoint(new Vector3(0,0,3.9f),true);
            yield return null;
            Assert.That(manager.GetComponentsInChildren<Renderer>().All(x=>!x.enabled),Is.True);
            Assert.That(Object.FindFirstObjectByType<WorldInputController>().ReturnToManagerIfViewingOtherFloor(),Is.True);
            Assert.That(manager.transform.position,Is.EqualTo(before));
            Assert.That(rig.ViewedFloor,Is.EqualTo(1));
            Assert.That(manager.TryMoveTo(new Vector3(-2,4,0),out var target),Is.True);
            yield return new WaitForSeconds(.8f);
            Assert.That(Mathf.Abs(manager.transform.position.y-target.y),Is.LessThan(.1f));
            Assert.That(manager.transform.position.x,Is.LessThan(before.x-.5f));
        }

        [UnityTest]
        public IEnumerator WarehouseExpansionChangesTheLobbyAndSurvivesJsonRoundTrip()
        {
            var hud=Object.FindFirstObjectByType<WorldManagementHud>();
            typeof(WorldManagementHud).GetMethod("CloseSessionMenu",Private).Invoke(hud,null);
            var bridge=HotelSimSceneBridge.Instance;bridge.IsSuspended=true;
            var sim=bridge.Sim;sim.Warehouse.SetCapacity(40);sim.RecordCashCredit(2000);
            int cash=sim.Cash,stock=sim.Materials.Stock;
            Assert.That(sim.TryExpandWarehouse(out var reason),Is.True,reason);
            Assert.That(cash-sim.Cash,Is.EqualTo(1500));
            Object.FindFirstObjectByType<ManagerCameraRig>().FocusOnPoint(new Vector3(-35,0,-16),true);
            yield return null;
            var view=Object.FindFirstObjectByType<WarehouseWorldView>(FindObjectsInactive.Include);
            int shelves=0;
            foreach(Transform child in view.transform)
                if(child.name.StartsWith("Shelf_") && child.gameObject.activeSelf)shelves++;
            Assert.That(shelves,Is.EqualTo(2));
            var save=Object.FindFirstObjectByType<SaveCoordinator>().Capture();
            var loaded=JsonUtility.FromJson<GameState>(JsonUtility.ToJson(save));
            sim.Warehouse.SetCapacity(40);sim.RestoreFrom(loaded.sim);
            Assert.That(sim.Warehouse.Capacity,Is.EqualTo(80));Assert.That(sim.Materials.Stock,Is.EqualTo(stock));
        }

        [UnityTest]
        public IEnumerator PaidRestorationShowsProgressAndCleansUpItsScenePresentation()
        {
            var hud = Object.FindFirstObjectByType<WorldManagementHud>();
            typeof(WorldManagementHud).GetMethod("CloseSessionMenu",Private).Invoke(hud,null);
            var bridge = HotelSimSceneBridge.Instance; bridge.IsSuspended = true;
            var sim = bridge.Sim;
            int room = 213;
            sim.RecordCashCredit(4000); sim.Materials.Add(4);
            int cash = sim.Cash;
            Assert.That(sim.TryStartReclaim(ReclaimPlanKind.Refit,new[]{room},out var reason),Is.True,reason);
            Assert.That(cash-sim.Cash,Is.EqualTo(sim.QuoteReclaim(ReclaimPlanKind.Refit,1)));
            Object.FindFirstObjectByType<ManagerCameraRig>().FocusOnPoint(HotelFloorPlan.Room(213).Centre,true);
            yield return new WaitForSeconds(.3f);
            var binder = Object.FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(x=>x.RoomNumber==room);
            Assert.That(binder.transform.Find("ConstructionFeedback").gameObject.activeSelf,Is.True);
            var crew=binder.GetComponentInChildren<HotelCharacterArt>();
            Assert.That(crew,Is.Not.Null);Assert.That(crew.construction,Is.True);
            Assert.That(crew.MotionState,Is.EqualTo("Work"));
            var advance = typeof(HotelSim).GetMethod("AdvanceRenovations",Private);
            advance.Invoke(sim,null);
            yield return new WaitForSeconds(.3f);
            Assert.That(sim.Renovations.JobFor(room).Progress,Is.EqualTo(.25f));
            var snapshot = new SimState(); sim.CaptureTo(snapshot);
            Assert.That(snapshot.buildJobs.First(x=>x.rooms.Contains(room)).totalDays,Is.EqualTo(4));
            advance.Invoke(sim,null); advance.Invoke(sim,null); advance.Invoke(sim,null);
            yield return new WaitForSeconds(.3f);
            Assert.That(sim.Rooms.At(room).state,Is.EqualTo(RoomSimState.Dirty));
            Assert.That(binder.transform.Find("ConstructionFeedback").gameObject.activeSelf,Is.False);
        }
    }
}
