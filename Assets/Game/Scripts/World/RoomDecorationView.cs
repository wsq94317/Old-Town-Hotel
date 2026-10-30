using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class RoomDecorationView:MonoBehaviour
{
    private int _room,_revision=-1;private float _next;
    private Transform _shell,_decor;private bool _custom;
    private Color[] _lightColours;private float[] _lightIntensities;
    public void RefreshNow(){_revision=-1;Tick();}
    private void OnEnable(){_room=GetComponent<RoomSceneBinder>().RoomNumber;_next=0;}
    private void Update(){_next-=Time.unscaledDeltaTime;if(_next>0)return;_next=.2f;Tick();}
    private void Tick()
    {
        var sim=HotelSimSceneBridge.Instance?.Sim;if(sim==null)return;
        _room=GetComponent<RoomSceneBinder>().RoomNumber;var plan=sim.RoomDesigns.For(_room);
        if(plan==null){if(_custom){Clear();SetOriginal(true);}_custom=false;_revision=-1;return;}
        if(_revision==plan.revision)return;_revision=plan.revision;Clear();SetOriginal(false);_custom=true;
        bool positive=HotelFloorPlan.LocalDoorSide(transform)>0;
        _shell=HotelRoomDesignArt.CreateShell(transform,plan,positive);
        _decor=new GameObject("CustomDecorations").transform;_decor.SetParent(transform,false);
        foreach(var item in plan.pieces)if(item.IsDecoration&&RoomDecorCatalog.TryGet(item.kindId,out _)&&RoomDesignGeometry.Finite(item.x)&&RoomDesignGeometry.Finite(item.z)){
            var model=HotelRoomDesignArt.CreatePiece(item);model.transform.SetParent(_decor,false);
            model.transform.localPosition=HotelRoomDesignArt.LocalPosition(item,positive);model.transform.localRotation=Quaternion.Euler(0,HotelRoomDesignArt.LocalYaw(item,positive),0);
            LowPolyHotelKit.SetLayerRecursively(model,LowPolyHotelKit.ArtLayer);AddObstacle(model,item.kindId);
        }
        ApplyLighting(plan.lighting);
    }
    public static void AddObstacle(GameObject model,int kind)
    {
        var obstacle=model.GetComponent<NavMeshObstacle>();bool required=RoomDesignGeometry.Mount(kind)==RoomDecorMount.Floor;
        if(!required){if(obstacle!=null)obstacle.enabled=false;return;}
        if(obstacle==null)obstacle=model.AddComponent<NavMeshObstacle>();
        RoomDesignGeometry.Size(kind,out var w,out var d);obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=new Vector3(0,.65f,0);obstacle.size=new Vector3(w,1.3f,d);
        obstacle.carving=true;obstacle.carveOnlyStationary=true;obstacle.carvingMoveThreshold=.05f;obstacle.carvingTimeToStationary=.05f;obstacle.enabled=true;
    }
    private void ApplyLighting(int style)
    {
        var root=transform.Find("InteriorFixtures");if(root==null)return;var lights=root.GetComponentsInChildren<Light>(true);
        if(_lightColours==null||_lightColours.Length!=lights.Length){_lightColours=new Color[lights.Length];_lightIntensities=new float[lights.Length];for(int i=0;i<lights.Length;i++){_lightColours[i]=lights[i].color;_lightIntensities[i]=lights[i].intensity;}}
        for(int i=0;i<lights.Length;i++){
            lights[i].color=style==0?_lightColours[i]:style==1?new Color(1,.76f,.49f):style==2?new Color(.88f,.96f,1):new Color(1,.68f,.76f);
            lights[i].intensity=_lightIntensities[i]*(style==2?1.18f:style==3?.9f:1);
        }
    }
    private void SetOriginal(bool visible)
    {
        var art=transform.Find("RevivalArchitecture");if(art!=null)art.gameObject.SetActive(visible);
        foreach(string name in new[]{"Wall_Div","Wall_Corr_L","Wall_Corr_R"}){var part=transform.Find(name);if(part!=null&&part.TryGetComponent<Renderer>(out var renderer))renderer.enabled=visible;}
    }
    private void Clear(){if(_shell!=null){_shell.gameObject.SetActive(false);Destroy(_shell.gameObject);}if(_decor!=null){_decor.gameObject.SetActive(false);Destroy(_decor.gameObject);}}
}
