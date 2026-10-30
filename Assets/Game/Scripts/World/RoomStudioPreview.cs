using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Isolated 3D studio. It renders actual game models, never changes the live hotel or its furniture ledger.</summary>
public sealed class RoomStudioPreview:IDisposable
{
    public const int PreviewLayer=30;
    public Camera Camera { get; private set; }
    public RenderTexture Texture { get; private set; }
    public Transform Root { get; private set; }
    public float Yaw=35,Pitch=50,Zoom=3.45f;
    private Transform _shell;private string _surfaceKey="";
    private readonly Dictionary<int,GameObject> _models=new Dictionary<int,GameObject>();
    private readonly Dictionary<int,string> _keys=new Dictionary<int,string>();
    private readonly Dictionary<string,Texture2D> _thumbnails=new Dictionary<string,Texture2D>();
    private readonly Dictionary<Light,int> _lightMasks=new Dictionary<Light,int>();
    private LineRenderer _outline;private Material _outlineMaterial;private GameObject _grid;
    public RoomStudioPreview()
    {
        Root=new GameObject("RoomDesignPreviewStage").transform;Root.gameObject.hideFlags=HideFlags.DontSave;Root.position=new Vector3(0,-2000,0);
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(light.type==LightType.Directional){_lightMasks[light]=light.cullingMask;light.cullingMask&=~(1<<PreviewLayer);}
        var camera=new GameObject("RoomDesignPreviewCamera",typeof(Camera));camera.transform.SetParent(Root,false);Camera=camera.GetComponent<Camera>();
        Camera.orthographic=true;Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.15f,.22f,.20f,1);Camera.cullingMask=1<<PreviewLayer;
        Camera.nearClipPlane=.1f;Camera.farClipPlane=40;Camera.depth=-50;Camera.allowHDR=false;Camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        AddLight("StudioKey",new Vector3(48,-30,0),new Color(1,.88f,.70f),1.0f);
        AddLight("StudioFill",new Vector3(60,145,0),new Color(.72f,.84f,1),.38f);
        var line=new GameObject("DesignSelection",typeof(LineRenderer));line.transform.SetParent(Root,false);line.layer=PreviewLayer;
        _outline=line.GetComponent<LineRenderer>();_outline.positionCount=4;_outline.loop=true;_outline.useWorldSpace=false;_outline.widthMultiplier=.035f;
        _outlineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));_outline.sharedMaterial=_outlineMaterial;_outline.shadowCastingMode=ShadowCastingMode.Off;
        Resize(960,640);UpdateCamera();
    }
    private void AddLight(string name,Vector3 angle,Color colour,float intensity)
    {
        var go=new GameObject(name,typeof(Light));go.transform.SetParent(Root,false);go.transform.rotation=Quaternion.Euler(angle);
        var light=go.GetComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<PreviewLayer;light.color=colour;light.intensity=intensity;light.shadows=LightShadows.None;
    }
    public void Resize(int width,int height)
    {
        width=Mathf.Clamp(width,320,1280);height=Mathf.Clamp(height,240,1024);if(Texture!=null&&Mathf.Abs(Texture.width-width)<12&&Mathf.Abs(Texture.height-height)<12)return;
        Camera.targetTexture=null;if(Texture!=null){Texture.Release();UnityEngine.Object.Destroy(Texture);}
        Texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="RoomDesignLivePreview",antiAliasing=2};Texture.Create();Camera.targetTexture=Texture;Camera.aspect=width/(float)height;
    }
    public void UpdateCamera()
    {
        Pitch=Mathf.Clamp(Pitch,35,80);Zoom=Mathf.Clamp(Zoom,2.6f,5.5f);var rotation=Quaternion.Euler(Pitch,Yaw,0);if(_outline!=null)_outline.widthMultiplier=.03f+Mathf.Sin(Time.unscaledTime*4)*.005f;
        Camera.transform.rotation=rotation;Camera.transform.position=Root.position+new Vector3(0,.4f,0)-rotation*Vector3.forward*13;Camera.orthographicSize=Zoom*Mathf.Max(1,1.12f/Camera.aspect);
    }
    public void Show(RoomDesignPlan plan,int selected,bool showGrid=true)
    {
        if(plan==null)return;
        string surface=plan.wallStyle+"|"+plan.floorStyle+"|"+plan.lighting;
        if(surface!=_surfaceKey){_surfaceKey=surface;if(_shell!=null){_shell.gameObject.SetActive(false);UnityEngine.Object.Destroy(_shell.gameObject);}_shell=HotelRoomDesignArt.CreateShell(Root,plan,false,Camera);LowPolyHotelKit.SetLayerRecursively(_shell.gameObject,PreviewLayer);
            var key=Root.Find("StudioKey").GetComponent<Light>();key.color=plan.lighting==1?new Color(1,.72f,.4f):plan.lighting==2?new Color(.88f,.96f,1):plan.lighting==3?new Color(1,.68f,.76f):new Color(1,.88f,.70f);key.intensity=plan.lighting==2?1.25f:1;}
        var ids=new HashSet<int>();foreach(var piece in plan.pieces){
            ids.Add(piece.localId);string key=piece.kindId+":"+piece.variantId;
            if(!_models.TryGetValue(piece.localId,out var model)||model==null||!_keys.TryGetValue(piece.localId,out var old)||old!=key){
                if(model!=null){model.SetActive(false);UnityEngine.Object.Destroy(model);}model=HotelRoomDesignArt.CreatePiece(piece);model.transform.SetParent(Root,false);
                LowPolyHotelKit.SetLayerRecursively(model,PreviewLayer);LowPolyHotelKit.RemoveColliders(model);model.AddComponent<RoomDesignObjectMotion>();_models[piece.localId]=model;_keys[piece.localId]=key;
            }
            model.transform.localPosition=new Vector3(piece.x,0,piece.z);model.transform.localRotation=Quaternion.Euler(0,piece.yaw,0);
        }
        var gone=new List<int>();foreach(var pair in _models)if(!ids.Contains(pair.Key))gone.Add(pair.Key);
        foreach(int id in gone){if(_models[id]!=null){_models[id].SetActive(false);UnityEngine.Object.Destroy(_models[id]);}_models.Remove(id);_keys.Remove(id);}
        if(_grid==null)BuildGrid();_grid.SetActive(showGrid);
        var item=plan.pieces.Find(x=>x.localId==selected);_outline.enabled=item!=null;
        if(item!=null){RoomDesignGeometry.OrientedSize(item,out var w,out var d);float y=RoomDesignGeometry.Mount(item.kindId)==RoomDecorMount.Wall?.10f:.09f;
            _outline.SetPositions(new[]{new Vector3(item.x-w*.5f,y,item.z-d*.5f),new Vector3(item.x-w*.5f,y,item.z+d*.5f),new Vector3(item.x+w*.5f,y,item.z+d*.5f),new Vector3(item.x+w*.5f,y,item.z-d*.5f)});
            _outlineMaterial.color=RoomDesignGeometry.CanFit(plan,item)?new Color(1,.75f,.3f):new Color(1,.25f,.22f);
        }
        UpdateCamera();
    }
    private void BuildGrid()
    {
        _grid=new GameObject("PlacementGrid");_grid.transform.SetParent(Root,false);_grid.layer=PreviewLayer;
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.45f,.60f,.51f)};_grid.AddComponent<RoomStudioOwnedMaterial>().material=material;
        for(int i=-4;i<=4;i++)foreach(bool vertical in new[]{false,true}){
            var go=new GameObject("GridLine",typeof(LineRenderer));go.layer=PreviewLayer;go.transform.SetParent(_grid.transform,false);var l=go.GetComponent<LineRenderer>();l.useWorldSpace=false;l.positionCount=2;l.widthMultiplier=.008f;l.sharedMaterial=material;l.shadowCastingMode=ShadowCastingMode.Off;
            l.SetPosition(0,vertical?new Vector3(i*.5f,.072f,-2.3f):new Vector3(-2.3f,.072f,i*.5f));l.SetPosition(1,vertical?new Vector3(i*.5f,.072f,2.3f):new Vector3(2.3f,.072f,i*.5f));
        }
    }
    public bool Project(Vector2 uv,out Vector2 position)
    {
        var ray=Camera.ViewportPointToRay(new Vector3(uv.x,uv.y));var plane=new Plane(Vector3.up,Root.position);if(plane.Raycast(ray,out var distance)){var p=ray.GetPoint(distance)-Root.position;position=new Vector2(p.x,p.z);return true;}position=default;return false;
    }
    public int Pick(Vector2 uv)
    {
        var ray=Camera.ViewportPointToRay(uv);float nearest=float.PositiveInfinity;int selected=0;
        foreach(var pair in _models)if(pair.Value!=null)foreach(var r in pair.Value.GetComponentsInChildren<Renderer>())if(r.enabled&&r.bounds.IntersectRay(ray,out var distance)&&distance<nearest){nearest=distance;selected=pair.Key;}
        return selected;
    }
    public Texture2D Thumbnail(int kind,int variant=0)
    {
        string key=kind+":"+variant;if(_thumbnails.TryGetValue(key,out var cached)&&cached!=null)return cached;
        var piece=new RoomDesignPiece{kindId=kind,variantId=variant};var model=HotelRoomDesignArt.CreatePiece(piece);model.transform.SetParent(Root,false);model.transform.localPosition=Vector3.right*25;
        LowPolyHotelKit.SetLayerRecursively(model,PreviewLayer);var renderers=model.GetComponentsInChildren<Renderer>();var bounds=new Bounds(model.transform.position,Vector3.zero);bool found=false;
        foreach(var r in renderers)if(r.enabled){if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);}
        var old=Camera.targetTexture;var oldActive=RenderTexture.active;var position=Camera.transform.position;var rotation=Camera.transform.rotation;float zoom=Camera.orthographicSize,aspect=Camera.aspect;Color colour=Camera.backgroundColor;
        var rt=RenderTexture.GetTemporary(160,128,16,RenderTextureFormat.ARGB32);var image=new Texture2D(160,128,TextureFormat.RGBA32,false);
        try{Camera.targetTexture=rt;Camera.aspect=1.25f;Camera.backgroundColor=new Color(.91f,.87f,.76f,1);Camera.transform.rotation=Quaternion.Euler(35,35,0);Camera.transform.position=bounds.center-Camera.transform.forward*10;Camera.orthographicSize=Mathf.Max(.48f,Mathf.Max(bounds.size.y*.68f,Mathf.Max(bounds.size.x,bounds.size.z)*.75f));Camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,160,128),0,0);image.Apply();}
        finally{Camera.targetTexture=old;RenderTexture.active=oldActive;Camera.transform.SetPositionAndRotation(position,rotation);Camera.orthographicSize=zoom;Camera.aspect=aspect;Camera.backgroundColor=colour;RenderTexture.ReleaseTemporary(rt);model.SetActive(false);UnityEngine.Object.Destroy(model);}
        image.name="StudioThumbnail_"+key;_thumbnails[key]=image;return image;
    }
    public void Dispose()
    {
        foreach(var pair in _lightMasks)if(pair.Key!=null)pair.Key.cullingMask=pair.Value;
        if(Camera!=null)Camera.targetTexture=null;if(Texture!=null){Texture.Release();UnityEngine.Object.Destroy(Texture);}
        foreach(var image in _thumbnails.Values)if(image!=null)UnityEngine.Object.Destroy(image);_thumbnails.Clear();
        if(_outlineMaterial!=null)UnityEngine.Object.Destroy(_outlineMaterial);if(Root!=null){Root.gameObject.SetActive(false);UnityEngine.Object.Destroy(Root.gameObject);}Root=null;Camera=null;Texture=null;
    }
}
public sealed class RoomStudioOwnedMaterial:MonoBehaviour
{
    public Material material;private void OnDestroy(){if(material!=null)Destroy(material);}
}
