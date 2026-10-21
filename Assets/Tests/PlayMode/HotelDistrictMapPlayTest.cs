using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OldTownHotel.Tests.PlayMode
{
    public class HotelDistrictMapPlayTest
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private WorldManagementHud _hud;
        private object Invoke(string method,params object[] args) => typeof(WorldManagementHud).GetMethod(method,Private).Invoke(_hud,args);

        [UnitySetUp]
        public IEnumerator LoadHotel()
        {
            Time.timeScale=1;
            SceneManager.LoadScene("Assets/Game/Scenes/Hotel_Manager_25D.unity");
            yield return null; yield return null;
            var save=Object.FindFirstObjectByType<SaveCoordinator>();
            typeof(SaveCoordinator).GetField("autoSaveOnDayEnd",Private).SetValue(save,false);
            _hud=Object.FindFirstObjectByType<WorldManagementHud>();
            Invoke("CloseSessionMenu");
            HotelSimSceneBridge.Instance.IsSuspended=true;
            HotelEmpireProgress.ResetForNewGame();
            Invoke("OpenEmpireMap");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        { HotelEmpireProgress.ResetForNewGame(); Time.timeScale=1; yield return null; }

        private RectTransform Map => (RectTransform)typeof(WorldManagementHud).GetField("_empireMap",Private).GetValue(_hud);
        private Button Pin(string id) => Map.GetComponentsInChildren<Button>().Single(x=>x.name=="HotelPin_"+id);

        [UnityTest]
        public IEnumerator EmpireTabHasIllustrationSevenClickableSitesAndBlocksWorldInput()
        {
            var art=Map.GetComponentInChildren<RawImage>();
            Assert.That(art.texture,Is.Not.Null);
            Assert.That((float)art.texture.width/art.texture.height,Is.InRange(.78f,.82f),"The portrait illustration must retain its aspect ratio.");
            Assert.That(Map.GetComponentsInChildren<Button>().Count(x=>x.name.StartsWith("HotelPin_")),Is.EqualTo(7));
            foreach(var site in HotelDistrictCatalog.Sites) {
                var pin=Pin(site.Id); var rect=(RectTransform)pin.transform;
                var centre=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var hits=new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=centre},hits);
                Assert.That(hits.Count,Is.GreaterThan(0));
                Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(pin),site.Id+" must not be covered by another panel.");
                Assert.That(WorldManagementHud.ContainsScreenPoint(centre),Is.True);
            }
            Pin("grand").onClick.Invoke(); yield return null;
            Assert.That(Map.GetComponentsInChildren<Button>().Single(x=>x.name=="AcquireDistrictHotel").interactable,Is.False);
            Assert.That(Map.GetComponentInChildren<HotelDistrictRoutes>().canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator PurchaseThroughMapUpdatesCashOwnershipRoutesAndCapturedSave()
        {
            var sim=HotelSimSceneBridge.Instance.Sim; sim.RecordCashCredit(100000);
            Invoke("RefreshEmpireMap");
            Pin("station").onClick.Invoke();
            var acquire=Map.GetComponentsInChildren<Button>().Single(x=>x.name=="AcquireDistrictHotel");
            Assert.That(acquire.interactable,Is.True);
            int cash=sim.Cash; acquire.onClick.Invoke(); yield return null;
            Assert.That(sim.Cash,Is.EqualTo(cash-25000));
            Assert.That(HotelEmpireProgress.IsOwned("station"),Is.True);
            Assert.That(HotelEmpireProgress.IsUnlocked(HotelDistrictCatalog.Find("market")),Is.True);
            Assert.That(acquire.interactable,Is.False);
            Assert.That(Pin("station").GetComponent<Image>().color,Is.Not.EqualTo(Pin("market").GetComponent<Image>().color));
            var saved=Object.FindFirstObjectByType<SaveCoordinator>().Capture();
            Assert.That(saved.world.portfolioSiteIds,Does.Contain("station"));
            Assert.That(HotelEmpireProgress.DailyIncome,Is.EqualTo(480));
        }

        [UnityTest]
        public IEnumerator HomeInvestmentAndBottomNavigationRemainReachable()
        {
            var map=Map;
            map.GetComponentsInChildren<Button>().Single(x=>x.name=="HomeInvestment").onClick.Invoke(); yield return null;
            Assert.That(map.gameObject.activeSelf,Is.False);
            Assert.That(_hud.GetComponentsInChildren<RectTransform>().Any(x=>x.name=="ManagementDesk" && x.gameObject.activeInHierarchy),Is.True);
            var nav=(RectTransform)typeof(WorldManagementHud).GetField("_bottomNav",Private).GetValue(_hud);
            nav.GetComponentsInChildren<Button>().Single(x=>x.name=="Assets").onClick.Invoke(); yield return null;
            Assert.That(map.gameObject.activeInHierarchy,Is.True);
            nav.GetComponentsInChildren<Button>().Single(x=>x.name=="Rooms").onClick.Invoke(); yield return null;
            Assert.That(map.gameObject.activeSelf,Is.False);
            Assert.That((bool)typeof(WorldManagementHud).GetField("_empireMapOpen",Private).GetValue(_hud),Is.False);
        }
    }
}
