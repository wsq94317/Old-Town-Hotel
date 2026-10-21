using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OldTownHotel.Tests.PlayMode
{
    public sealed class HotelPresentationPlayTest
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale=1;SceneManager.LoadScene("Assets/Game/Scenes/Hotel_Manager_25D.unity");yield return null;yield return null;
            var save=Object.FindFirstObjectByType<SaveCoordinator>();
            typeof(SaveCoordinator).GetField("autoSaveOnDayEnd",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(save,false);
        }
        [UnityTearDown]public IEnumerator ResetTime(){Time.timeScale=1;yield return null;}
        [UnityTest]
        public IEnumerator TexturedRoomFurnitureAndFoldedCurtainsSurviveSceneLoading()
        {
            var rig=Object.FindFirstObjectByType<ManagerCameraRig>();rig.FocusOnPoint(new Vector3(7.5f,4,4),true);
            yield return new WaitForSecondsRealtime(.6f);
            var room=Object.FindObjectsByType<RoomSceneBinder>(FindObjectsSortMode.None).First(x=>x.RoomNumber==204);
            var furniture=room.transform.Find("Furniture").GetComponentsInChildren<MeshRenderer>();
            Assert.That(furniture.Any(r=>r.sharedMaterial.shader.name=="OldTownHotel/Crafted Surface"&&r.sharedMaterial.GetTexture("_BaseMap")!=null),Is.True);
            Assert.That(room.transform.Find("InteriorFixtures/WindowDaylight"),Is.Not.Null);
            Assert.That(room.transform.Find("InteriorFixtures/BedsideWarmLight"),Is.Not.Null);
            var meshes=room.GetComponentsInChildren<MeshFilter>();
            Assert.That(meshes.All(m=>m.sharedMesh!=null),Is.True,"Saved prefab meshes must remain valid after loading.");
            Assert.That(meshes.All(m=>m.sharedMesh.bounds.size.sqrMagnitude>0),Is.True);
        }
        [UnityTest]
        public IEnumerator InteriorLightBudgetFollowsCameraFloor()
        {
            var rig=Object.FindFirstObjectByType<ManagerCameraRig>();rig.FocusOnPoint(new Vector3(0,0,2),true);
            yield return new WaitForSecondsRealtime(.5f);
            var lobbyLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(x=>x.type==LightType.Spot&&x.enabled).ToArray();
            Assert.That(lobbyLights.Length,Is.InRange(1,8));
            rig.FocusOnPoint(new Vector3(7.5f,4,4),true);yield return new WaitForSecondsRealtime(.5f);
            var floor=GameObject.Find("World/Floor2").transform;
            var roomLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(x=>x.type==LightType.Spot&&x.enabled).ToArray();
            Assert.That(roomLights.Length,Is.InRange(1,8));
            Assert.That(roomLights.All(light=>light.transform.IsChildOf(floor)),Is.True,"Hidden floors must not keep realtime fixture lights active.");
            Assert.That(Object.FindFirstObjectByType<ReflectionProbe>(),Is.Not.Null);
        }
    }
}
