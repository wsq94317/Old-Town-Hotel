using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Interior fixtures are authored in the scene; only nearby fixtures consume realtime lighting.</summary>
public sealed class HotelLightingDirector : MonoBehaviour
{
    private readonly List<Light> _fixtures=new List<Light>();private float _next;
    private ReflectionProbe _reflection;private Vector3 _reflectionFocus=new Vector3(999,999,999);
    public static void Install()
    {
        var existing=Object.FindFirstObjectByType<HotelLightingDirector>();
        if(existing==null)existing=new GameObject("HotelInteriorLighting").AddComponent<HotelLightingDirector>();
        foreach(var binder in Object.FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
            if(binder.transform.Find("InteriorFixtures")!=null)continue;
            var root=HotelCharacterArt.Joint(binder.transform,"InteriorFixtures",Vector3.zero);float side=-HotelFloorPlan.LocalDoorSide(binder.transform);
            AddSpot(root,"WindowDaylight",new Vector3(.3f,2.35f,side*2.08f),new Vector3(-.4f,.1f,-side*.3f),new Color(.86f,.93f,1),9f,6,83,true);
            AddSpot(root,"BedsideWarmLight",new Vector3(-1.7f,1.08f,side*1.25f),new Vector3(-1.7f,0,side*1.25f),new Color(1,.63f,.29f),2.4f,2.8f,115,false);
        }
        var floor=GameObject.Find("World/Floor1");
        if(floor!=null&&floor.transform.Find("InteriorFixtures")==null){
            var root=HotelCharacterArt.Joint(floor.transform,"InteriorFixtures",Vector3.zero);
            AddSpot(root,"LobbyWindowSun",new Vector3(-1,4.1f,5),new Vector3(-2,0,-1),new Color(1,.84f,.61f),5,12,100,true);
            AddSpot(root,"ReceptionLamp",new Vector3(-1.2f,1.6f,2.9f),new Vector3(-1.2f,.8f,2.9f),new Color(1,.62f,.23f),3,3.8f,120,false);
            for(int i=0;i<3;i++)AddSpot(root,"WallSconce",new Vector3(-3.4f+i*3.4f,2.1f,5.4f),new Vector3(-3.4f+i*3.4f,.1f,4.7f),new Color(1,.64f,.3f),2.5f,4.1f,105,false);
        }
        existing.Refresh();existing.ApplyMood();
    }
    private static void AddSpot(Transform parent,string name,Vector3 position,Vector3 target,Color color,float intensity,float range,float angle,bool shadows)
    {
        var go=HotelCharacterArt.Joint(parent,name,position);go.localRotation=Quaternion.LookRotation(target-position);
        var light=go.gameObject.AddComponent<Light>();light.type=LightType.Spot;light.color=color;light.intensity=intensity;light.range=range;light.spotAngle=angle;light.innerSpotAngle=angle*.55f;light.shadows=shadows?LightShadows.Soft:LightShadows.None;light.shadowBias=.035f;light.shadowNormalBias=.12f;light.shadowStrength=.7f;light.renderMode=LightRenderMode.ForcePixel;
        light.shadowCustomResolution=512;
        light.GetUniversalAdditionalLightData();
    }
    private void OnEnable(){Refresh();ApplyMood();}
    private void Refresh(){_fixtures.Clear();foreach(var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(light.type==LightType.Spot&&light.transform.parent!=null&&light.transform.parent.name=="InteriorFixtures")_fixtures.Add(light);_next=0;}
    public void ApplyMood()
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.42f,.46f,.5f);RenderSettings.ambientEquatorColor=new Color(.32f,.32f,.29f);RenderSettings.ambientGroundColor=new Color(.21f,.19f,.16f);RenderSettings.fog=false;
        foreach(var sun in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(sun.type==LightType.Directional&&sun.name!="InteriorFill"){sun.intensity=1.2f;sun.color=new Color(1,.88f,.70f);sun.transform.rotation=Quaternion.Euler(42,-32,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.8f;sun.shadowBias=.025f;sun.shadowNormalBias=.12f;}
        var fillObject=GameObject.Find("InteriorFill");if(fillObject==null){fillObject=new GameObject("InteriorFill",typeof(Light));fillObject.transform.SetParent(transform,false);}
        var fill=fillObject.GetComponent<Light>();fill.type=LightType.Directional;fill.color=new Color(.73f,.83f,1);fill.intensity=.42f;fill.shadows=LightShadows.None;fill.transform.rotation=Quaternion.Euler(55,145,0);
        DynamicGI.UpdateEnvironment();
        var camera=Camera.main;if(camera!=null){var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.None;camera.backgroundColor=new Color(.15f,.20f,.19f);}
    }
    private void Update()
    {
        _next-=Time.unscaledDeltaTime;if(_next>0)return;_next=.25f;var camera=Camera.main;if(camera==null)return;
        Vector3 focus=camera.transform.position+camera.transform.forward*14;
        var rig=camera.GetComponent<ManagerCameraRig>();if(rig!=null)focus=rig.FocusPoint;
        if(_reflection==null){
            var probeObject=new GameObject("InteriorReflection");probeObject.transform.SetParent(transform,false);_reflection=probeObject.AddComponent<ReflectionProbe>();
            _reflection.mode=ReflectionProbeMode.Realtime;_reflection.refreshMode=ReflectionProbeRefreshMode.ViaScripting;_reflection.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;_reflection.resolution=128;_reflection.size=new Vector3(20,6,16);_reflection.boxProjection=true;_reflection.intensity=.65f;_reflection.cullingMask=~(1<<5);
        }
        if(Mathf.Abs(_reflectionFocus.y-focus.y)>2||Vector3.Distance(_reflectionFocus,focus)>15){_reflectionFocus=focus;_reflection.transform.position=focus+Vector3.up*2.3f;_reflection.RenderProbe();}
        _fixtures.Sort((a,b)=>(a.transform.position-focus).sqrMagnitude.CompareTo((b.transform.position-focus).sqrMagnitude));
        int enabled=0;foreach(var fixture in _fixtures){if(fixture==null)continue;bool on=fixture.gameObject.activeInHierarchy&&(fixture.transform.position-focus).sqrMagnitude<130&&enabled<8;fixture.enabled=on;if(on)enabled++;}
    }
}
