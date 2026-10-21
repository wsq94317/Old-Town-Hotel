using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OldTownHotel.Tests.PlayMode
{
    public class RoomDoorClearancePlayTest
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [UnitySetUp]
        public IEnumerator LoadHotel()
        {
            Time.timeScale=1;
            SceneManager.LoadScene("Assets/Game/Scenes/Hotel_Manager_25D.unity");
            yield return null; yield return null;
            typeof(SaveCoordinator).GetField("autoSaveOnDayEnd",Private).SetValue(Object.FindFirstObjectByType<SaveCoordinator>(),false);
            var hud=Object.FindFirstObjectByType<WorldManagementHud>();
            typeof(WorldManagementHud).GetMethod("CloseSessionMenu",Private).Invoke(hud,null);
            HotelSimSceneBridge.Instance.IsSuspended=true;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1; yield return null; }

        [UnityTest]
        public IEnumerator EveryDoorClearsCorridorWallsThroughoutItsOpeningSweep()
        {
            var doors=Object.FindObjectsByType<RoomDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Assert.That(doors.Length,Is.EqualTo(100));
            var pose=typeof(RoomDoor).GetMethod("ApplyDoorPose",Private);
            foreach(var door in doors) {
                door.RefreshDoorVisuals();
                int number=RoomSceneBinder.ParseRoomNumber(door.name);
                var room=door.transform.parent.Find("Room_"+number);
                var walls=new[]{room.Find("Wall_Corr_L").GetComponent<BoxCollider>(),room.Find("Wall_Corr_R").GetComponent<BoxCollider>()};
                var panel=door.transform.Find("DoorPanel");
                Assert.That(panel.Find("DoorCraftwork"),Is.Not.Null,door.name);
                Assert.That(panel.GetComponentsInChildren<Renderer>(true).Length,Is.LessThanOrEqualTo(4),door.name+" must not duplicate the baked door artwork.");
                var boxes=panel.GetComponentsInChildren<MeshFilter>(true).Select(mesh=>{
                    var box=mesh.gameObject.AddComponent<BoxCollider>(); box.isTrigger=true; return box;
                }).ToArray();
                foreach(float opened in new[]{0f,.12f,.25f,.5f,.75f,.9f,1f}) {
                    pose.Invoke(door,new object[]{opened});
                    foreach(var box in boxes) foreach(var wall in walls)
                        Assert.That(Physics.ComputePenetration(box,box.transform.position,box.transform.rotation,
                            wall,wall.transform.position,wall.transform.rotation,out _,out float depth) && depth>.001f,
                            Is.False,door.name+" "+box.name+" intersects "+wall.name+" at "+opened);
                }
                pose.Invoke(door,new object[]{0f});
                foreach(var box in boxes) Object.Destroy(box);
                var frame=door.transform.parent.Find("ArtPass_LowPoly/Frame_"+number);
                Assert.That(frame.Find("PostL").localScale.z,Is.GreaterThan(.2f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ManagerCanSwipeAndWalkThroughTheHingedDoor()
        {
            var door=Object.FindObjectsByType<RoomDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.name=="Door_201");
            var rig=Object.FindFirstObjectByType<ManagerCameraRig>(); rig.FocusOnPoint(door.transform.position,true);
            var manager=Object.FindFirstObjectByType<ManagerController>();
            var start=door.ExteriorInteractionPoint; start.y=4.05f;
            Assert.That(manager.TryRestorePosition(start),Is.True);
            Assert.That(manager.TryMoveTo(door.InteriorCenter,out _),Is.True);
            yield return new WaitForSeconds(.2f);
            door.ConfirmEntry();
            yield return new WaitForSeconds(1.4f);
            Assert.That(door.IsOpen,Is.True);
            Assert.That(manager.transform.position.z,Is.GreaterThan(door.transform.position.z+.25f));
            Assert.That(manager.transform.position.y,Is.InRange(3.95f,4.15f));
        }
    }
}
