using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OldTownHotel.Tests.PlayMode
{
    public sealed class RoomStudioPlayTest
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private WorldManagementHud _hud;
        private HotelSim Sim=>HotelSimSceneBridge.Instance.Sim;
        private object Call(string method,params object[] args)=>typeof(WorldManagementHud).GetMethod(method,Private).Invoke(_hud,args);
        private T Field<T>(string field)=>(T)typeof(WorldManagementHud).GetField(field,Private).GetValue(_hud);
        private Button Button(string name)=>_hud.GetComponentsInChildren<Button>().Single(x=>x.name==name);
        private RoomDesignDraft Draft=>Field<RoomDesignDraft>("_studioDraft");
        [UnitySetUp]public IEnumerator Load()
        {
            Time.timeScale=1;SceneManager.LoadScene("Assets/Game/Scenes/Hotel_Manager_25D.unity");yield return null;yield return null;
            typeof(SaveCoordinator).GetField("autoSaveOnDayEnd",Private).SetValue(Object.FindFirstObjectByType<SaveCoordinator>(),false);
            _hud=Object.FindFirstObjectByType<WorldManagementHud>();typeof(WorldManagementHud).GetField("_sessionInitialized",Private).SetValue(_hud,true);Call("CloseSessionMenu");
            HotelSimSceneBridge.Instance.IsSuspended=true;Call("SetSpeed",3f);Sim.RecordCashCredit(10000);
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(WorldManagementHud.RoomStudioOpen)Call("EndRoomStudio",false);Time.timeScale=1;yield return null;}
        [UnityTest]public IEnumerator StudioPausesSimulationAndCancellationRestoresSpeedAndLighting()
        {
            var lights=Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional).ToArray();var masks=lights.Select(l=>l.cullingMask).ToArray();int cash=Sim.Cash;
            WorldManagementHud.OpenRoomStudio(201);yield return null;Assert.That(Time.timeScale,Is.Zero);Assert.That(WorldManagementHud.SceneCameraInputBlocked,Is.True);
            Call("StudioStyleStarter",0);yield return null;Assert.That(Sim.Cash,Is.EqualTo(cash));Assert.That(Sim.RoomDesigns.For(201),Is.Null);
            Button("StudioClose").onClick.Invoke();yield return null;Assert.That(Button("StudioDiscard").gameObject.activeInHierarchy,Is.True);Button("StudioDiscard").onClick.Invoke();yield return null;
            Assert.That(WorldManagementHud.RoomStudioOpen,Is.False);Assert.That(Time.timeScale,Is.EqualTo(3));Assert.That(Sim.Cash,Is.EqualTo(cash));
            for(int i=0;i<lights.Length;i++)Assert.That(lights[i].cullingMask,Is.EqualTo(masks[i]));Assert.That(GameObject.Find("RoomDesignPreviewStage"),Is.Null);
        }
        [UnityTest]public IEnumerator CatalogueButtonsAreTouchableAndUndoRedoKeepOneTransactionPerEdit()
        {
            WorldManagementHud.OpenRoomStudio(201);yield return null;Button("StudioTab_1").onClick.Invoke();yield return null;
            var button=Button("Decor_1005");var rect=(RectTransform)button.transform;var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))},hits);
            // First row is visible on portrait and wide screens; scroll to the art before checking its hit target.
            var fern=Button("Decor_1001");rect=(RectTransform)fern.transform;hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))},hits);Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(fern));
            fern.onClick.Invoke();yield return null;Assert.That(Draft.plan.pieces.Count(x=>x.IsDecoration),Is.EqualTo(1));Button("StudioUndo").onClick.Invoke();Assert.That(Draft.plan.pieces.Any(x=>x.IsDecoration),Is.False);Button("StudioRedo").onClick.Invoke();Assert.That(Draft.plan.pieces.Count(x=>x.IsDecoration),Is.EqualTo(1));Assert.That(Sim.RoomDesigns.For(201),Is.Null);
        }
        [UnityTest]public IEnumerator PointerDragMovesActualFurnitureAndUndoRestoresItsPose()
        {
            WorldManagementHud.OpenRoomStudio(201);yield return null;yield return null;var preview=Field<RoomStudioPreview>("_studioPreview");var viewport=Field<RectTransform>("_studioViewport");var piece=Draft.plan.pieces.First(p=>p.kindId==1);float before=piece.z;
            System.Func<Vector3,Vector2> screen=point=>{var uv=preview.Camera.WorldToViewportPoint(preview.Root.position+point);return RectTransformUtility.WorldToScreenPoint(null,viewport.TransformPoint(new Vector3(viewport.rect.xMin+uv.x*viewport.rect.width,viewport.rect.yMin+uv.y*viewport.rect.height)));};
            var pointer=viewport.GetComponent<RoomStudioPointer>();var data=new PointerEventData(EventSystem.current){position=screen(new Vector3(piece.x,.5f,piece.z)),button=PointerEventData.InputButton.Left};pointer.OnPointerDown(data);Assert.That(Field<int>("_studioSelected"),Is.EqualTo(piece.localId));
            data.position=screen(new Vector3(piece.x,.5f,piece.z-.5f));pointer.OnDrag(data);pointer.OnPointerUp(data);yield return null;
            Assert.That(Draft.plan.pieces.First(p=>p.kindId==1).z,Is.LessThan(before-.2f));Button("StudioUndo").onClick.Invoke();Assert.That(Draft.plan.pieces.First(p=>p.kindId==1).z,Is.EqualTo(before));
        }
        [UnityTest]public IEnumerator ApplyingDesignCreatesMirroredRealModelsAndKeepsTheDoorWalkable()
        {
            foreach(int room in new[]{201,202,249,250}){
                Sim.Rooms.SetState(room,RoomSimState.Ready);
                if(Sim.Furniture.InRoom(room).Count==0){Sim.Furniture.Place(room,1);Sim.Furniture.Place(room,10);}
                WorldManagementHud.OpenRoomStudio(room);yield return null;Call("StudioStyleStarter",0);var bed=Draft.plan.pieces.First(p=>!p.IsDecoration&&FurnitureCatalog.Get(p.kindId).slot==FurnitureSlot.Bed);bed.z=.75f;Call("RefreshRoomStudio");Assert.That(Button("StudioApply").interactable,Is.True,Sim.QuoteRoomDesign(Draft).reason);Button("StudioApply").onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);
                Assert.That(WorldManagementHud.RoomStudioOpen,Is.False);var binder=Object.FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(r=>r.RoomNumber==room);var plan=Sim.RoomDesigns.For(room);var model=binder.transform.Find("Furniture").Cast<Transform>().First(t=>t.name.EndsWith("_"+bed.instanceId));var expected=HotelRoomDesignArt.LocalPosition(bed,HotelFloorPlan.LocalDoorSide(binder.transform)>0);
                Assert.That(Vector3.Distance(model.localPosition,expected),Is.LessThan(.01f));Assert.That(binder.transform.Find("CustomDecorations").childCount,Is.EqualTo(2));Assert.That(binder.transform.Find("DesignedInterior"),Is.Not.Null);Assert.That(binder.transform.Find("RevivalArchitecture").gameObject.activeSelf,Is.False);
                Assert.That(NavMesh.SamplePosition(binder.transform.position,out var centre,.25f,NavMesh.AllAreas),Is.True,"Room centre must remain walkable");var pose=HotelFloorPlan.Room(room);Assert.That(NavMesh.SamplePosition(pose.Door-pose.Outward*.6f,out var door,.5f,NavMesh.AllAreas),Is.True);var path=new NavMeshPath();Assert.That(NavMesh.CalculatePath(door.position,centre.position,NavMesh.AllAreas,path),Is.True);Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
            }
        }
        [UnityTest]public IEnumerator SavedBlueprintCopiesAPreviewWithoutChargingOrBorrowingAnotherRoomsFurniture()
        {
            WorldManagementHud.OpenRoomStudio(201);yield return null;Call("StudioStyleStarter",2);Button("StudioTab_5").onClick.Invoke();yield return null;int cash=Sim.Cash;Button("StudioSavePlan_0").onClick.Invoke();yield return null;Assert.That(Sim.RoomDesigns.Blueprints.Count,Is.EqualTo(1));Call("EndRoomStudio",false);
            WorldManagementHud.OpenRoomStudio(202);yield return null;Button("StudioTab_5").onClick.Invoke();yield return null;Button("StudioLoadPlan_0").onClick.Invoke();yield return null;Assert.That(Draft.plan.wallStyle,Is.EqualTo(5));Assert.That(Sim.QuoteRoomDesign(Draft).valid,Is.True);Assert.That(Sim.Cash,Is.EqualTo(cash));Assert.That(Draft.plan.pieces.Where(p=>!p.IsDecoration).All(p=>p.instanceId==0||Sim.Furniture.All.Any(f=>f.instanceId==p.instanceId&&(f.roomNumber==0||f.roomNumber==202))),Is.True);
        }
        [UnityTest]public IEnumerator OccupiedRoomCanPreviewButCheckoutIsDisabled()
        {Sim.Rooms.SetState(201,RoomSimState.Occupied);WorldManagementHud.OpenRoomStudio(201);yield return null;Call("StudioStyleStarter",0);yield return null;Assert.That(Sim.QuoteRoomDesign(Draft).valid,Is.True);Assert.That(Button("StudioApply").interactable,Is.False);}
        [UnityTest]public IEnumerator GroundWayfindingStaysInOverviewAndDoesNotCoverCloseRoomInteriors()
        {
            var rig=Object.FindFirstObjectByType<ManagerCameraRig>();rig.FocusOnPoint(HotelFloorPlan.Room(204).Centre,true);rig.ZoomBy(6-rig.Zoom);yield return new WaitForSecondsRealtime(.1f);
            var signs=Object.FindObjectsByType<HotelWayfindingVisibility>(FindObjectsSortMode.None);Assert.That(signs.Length,Is.GreaterThan(0));Assert.That(signs.All(x=>!x.GetComponent<Renderer>().enabled),Is.True);
            rig.ZoomBy(20-rig.Zoom);yield return new WaitForSecondsRealtime(.1f);Assert.That(signs.All(x=>x.GetComponent<Renderer>().enabled),Is.True);
        }
    }
}
