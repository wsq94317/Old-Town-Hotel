using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Native 3D room finishes and decorative models shared by the studio and the real hotel.</summary>
public static class HotelRoomDesignArt
{
    private static Color C(string hex)=>HotelVisualStyle.C(hex);
    private static Transform Root(Transform parent,string name,Vector3 position)=>HotelCharacterArt.Joint(parent,name,position);
    private static GameObject B(Transform p,string n,Vector3 pos,Vector3 size,string colour,Vector3 euler=default)=>HotelVisualStyle.Box(p,n,pos,size,C(colour),euler);
    private static GameObject S(Transform p,string n,PrimitiveType type,Vector3 pos,Vector3 size,string colour,Vector3 euler=default)=>HotelVisualStyle.Shape(p,n,type,pos,size,C(colour),euler);
    public static Vector3 LocalPosition(RoomDesignPiece piece,bool positiveDoor)=>new Vector3(piece.x,0,piece.z*(positiveDoor?-1:1));
    public static float LocalYaw(RoomDesignPiece piece,bool positiveDoor)=>positiveDoor?180-piece.yaw:piece.yaw;
    public static Transform CreateShell(Transform parent,RoomDesignPlan plan,bool positiveDoor,Camera camera=null)
    {
        var root=Root(parent,"DesignedInterior",Vector3.zero);var cut=root.gameObject.AddComponent<RoomDesignCutaway>();cut.cameraOverride=camera;
        float sign=positiveDoor?-1:1;var floor=Root(root,"FloorFinish",Vector3.zero);var f=RoomSurfaceCatalog.Floor(plan.floorStyle);var wall=RoomSurfaceCatalog.Wall(plan.wallStyle);
        B(floor,"OakFloor",new Vector3(0,.025f,0),new Vector3(4.8f,.04f,4.8f),f.colour);
        if(f.pattern==0){for(int x=0;x<9;x++)B(floor,"FloorboardSeam",new Vector3(-2.14f+x*.535f,.048f,0),new Vector3(.012f,.008f,4.8f),f.secondary);}
        else if(f.pattern==1||f.pattern==2){for(int x=0;x<6;x++)for(int z=0;z<6;z++)B(floor,f.pattern==2?"TerrazzoTile":"ParquetTile",new Vector3(-2+x*.8f,.052f,-2+z*.8f),new Vector3(.785f,.018f,.785f),(x+z)%2==0?f.colour:f.secondary,new Vector3(0,(x+z)%2*90,0));}
        else BuildHerringbone(floor,f);
        Combine(floor);
        foreach(int side in new[]{-1,1}){
            var face=Root(root,side<0?"WestWallFinish":"EastWallFinish",Vector3.zero);
            B(face,"PlasterWall",new Vector3(side*2.4f,.7f,0),new Vector3(.12f,1.4f,4.8f),wall.colour);
            B(face,"WalnutDado",new Vector3(side*2.32f,.23f,0),new Vector3(.10f,.46f,4.8f),wall.secondary);
            B(face,"BrassDadoRail",new Vector3(side*2.25f,.48f,0),new Vector3(.08f,.055f,4.82f),plan.wallStyle==5?"#D9B56E":"#F1D9AE");
            if(wall.pattern>0)for(int i=0;i<7;i++)B(face,"WallPanelling",new Vector3(side*2.30f,.96f,-2.05f+i*.68f),new Vector3(.025f,.88f,wall.pattern==2?.035f:.015f),wall.secondary);
            Combine(face);cut.Add(face,Vector3.right*side);
        }
        var back=Root(root,"WindowWallFinish",Vector3.zero);
        B(back,"PlasterBack",new Vector3(0,.66f,sign*2.4f),new Vector3(4.8f,1.32f,.12f),wall.colour);
        B(back,"WindowFrame",new Vector3(.35f,1.46f,sign*2.30f),new Vector3(1.9f,1.15f,.13f),"#F4DDB3");
        B(back,"DaylightGlass",new Vector3(.35f,1.46f,sign*2.20f),new Vector3(1.65f,.94f,.045f),"#A7D3D5");
        B(back,"WindowMullion",new Vector3(.35f,1.46f,sign*2.16f),new Vector3(.065f,.96f,.045f),"#F4DDB3");
        B(back,"WindowCrossbar",new Vector3(.35f,1.46f,sign*2.16f),new Vector3(1.67f,.055f,.045f),"#F4DDB3");
        for(int side=-1;side<=1;side+=2){B(back,"PleatedCurtain",new Vector3(.35f+side*1.0f,1.38f,sign*2.1f),new Vector3(.32f,1.3f,.15f),wall.colour);B(back,"CurtainTie",new Vector3(.35f+side*1.0f,1.1f,sign*2),new Vector3(.35f,.07f,.06f),"#D9B56E");}
        B(back,"CurtainRod",new Vector3(.35f,2.07f,sign*2.12f),new Vector3(2.5f,.06f,.06f),"#CCA75D");
        Combine(back);cut.Add(back,Vector3.forward*sign);
        var front=Root(root,"DoorwayFinish",Vector3.zero);
        foreach(int s in new[]{-1,1}){B(front,"DoorwayPlaster",new Vector3(s*1.7f,.275f,-sign*2.4f),new Vector3(1.4f,.55f,.13f),wall.colour);B(front,"DoorwayDado",new Vector3(s*1.7f,.12f,-sign*2.31f),new Vector3(1.4f,.20f,.06f),wall.secondary);}
        Combine(front);
        return root;
    }
    private static void BuildHerringbone(Transform floor,RoomSurfaceStyle style)
    {
        // A domino tiling: every cell belongs to exactly one horizontal or vertical plank.
        // Clip the boundary cells instead of drawing overlapping rotated rectangles.
        const int count=16;const float cell=.3f,gap=.012f;
        for(int x=0;x<count;x++)for(int z=0;z<count;z++){
            int diagonal=(x-z+64)%4;float px=-2.25f+x*cell,pz=-2.25f+z*cell;
            if(diagonal==0){bool pair=x+1<count;B(floor,"HerringboneOak",new Vector3(px+(pair?cell*.5f:0),.055f,pz),new Vector3(cell*(pair?2:1)-gap,.012f,cell-gap),style.colour);}
            else if(diagonal==2){bool pair=z>0;B(floor,"HerringboneOak",new Vector3(px,.055f,pz-(pair?cell*.5f:0)),new Vector3(cell-gap,.012f,cell*(pair?2:1)-gap),style.secondary);}
            else if(diagonal==1&&x==0||diagonal==3&&z==count-1)B(floor,"HerringboneEdge",new Vector3(px,.055f,pz),new Vector3(cell-gap,.012f,cell-gap),diagonal==1?style.colour:style.secondary);
        }
    }
    public static GameObject CreatePiece(RoomDesignPiece piece)
    {
        if(!piece.IsDecoration){
            if(!FurnitureCatalog.TryGet(piece.kindId,out var kind))return new GameObject("UnknownFurniture");
            var go=FurnitureVisualLibrary.Create(kind,piece.variantId);LowPolyHotelKit.RemoveColliders(go);return go;
        }
        var root=new GameObject("Decor_"+piece.kindId+"_"+piece.localId);var p=root.transform;int variant=piece.variantId;
        string wood=variant==1?"#A88558":variant==2?"#5B4C42":"#805C3D",fabric=variant==1?"#8A9C76":variant==2?"#A77561":variant==3?"#58768B":"#427B70";
        switch(piece.kindId){
            case RoomDecorCatalog.Fern:
            case RoomDecorCatalog.TallPlant:
                LowPolyHotelKit.BuildPlant(p,Vector3.zero,piece.kindId==RoomDecorCatalog.Fern?.78f:1.5f);break;
            case RoomDecorCatalog.Lamp:
                S(p,"BrassLampFoot",PrimitiveType.Cylinder,new Vector3(0,.035f,0),new Vector3(.38f,.035f,.38f),"#BE9B58");
                B(p,"LampStem",new Vector3(0,.65f,0),new Vector3(.055f,1.25f,.055f),"#B48D48");
                S(p,"WarmLinenShade",PrimitiveType.Cylinder,new Vector3(0,1.34f,0),new Vector3(.58f,.28f,.58f),variant==2?"#D7E7E0":"#FFE0A1");
                S(p,"ShadeTop",PrimitiveType.Sphere,new Vector3(0,1.58f,0),new Vector3(.20f,.05f,.20f),"#FFF1CD");break;
            case RoomDecorCatalog.Bookcase:
                B(p,"BookcaseBack",new Vector3(0,.65f,.14f),new Vector3(.86f,1.3f,.07f),wood);
                foreach(int s in new[]{-1,1})B(p,"BookcasePost",new Vector3(s*.43f,.65f,0),new Vector3(.07f,1.3f,.34f),wood);
                for(int row=0;row<4;row++){
                    B(p,"BookcaseShelf",new Vector3(0,.08f+row*.39f,0),new Vector3(.9f,.065f,.35f),wood);
                    if(row<3)for(int i=0;i<5;i++)B(p,"BookSpine",new Vector3(-.32f+i*.15f,.26f+row*.39f,-.02f),new Vector3(.105f,.28f+(i%2)*.035f,.23f),i%3==0?fabric:i%3==1?"#C5A968":"#E1D0AF",new Vector3(0,0,i==3?8:0));
                }break;
            case RoomDecorCatalog.Mirror:
                S(p,"BrassMirrorFrame",PrimitiveType.Cylinder,new Vector3(0,1.35f,0),new Vector3(.67f,.035f,.67f),"#CDAA62",new Vector3(90,0,0));
                S(p,"MirrorGlass",PrimitiveType.Cylinder,new Vector3(0,1.35f,-.04f),new Vector3(.57f,.015f,.57f),"#BDD3D0",new Vector3(90,0,0));
                B(p,"MirrorGleam",new Vector3(-.11f,1.42f,-.06f),new Vector3(.025f,.27f,.012f),"#F2F3DC",new Vector3(0,0,-32));break;
            case RoomDecorCatalog.BotanicalArt:
            case RoomDecorCatalog.AbstractArt:
                BuildPainting(p,piece.kindId,variant);break;
            case RoomDecorCatalog.SideTable:
                S(p,"RoundWalnutTop",PrimitiveType.Cylinder,new Vector3(0,.53f,0),new Vector3(.55f,.055f,.55f),wood);
                for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;B(p,"BrassTableLeg",new Vector3(Mathf.Cos(a)*.18f,.26f,Mathf.Sin(a)*.18f),new Vector3(.045f,.52f,.045f),"#B79A63");}
                S(p,"TableVase",PrimitiveType.Cylinder,new Vector3(0,.67f,0),new Vector3(.12f,.12f,.12f),fabric);break;
            case RoomDecorCatalog.Trunk:
                B(p,"TravelTrunk",new Vector3(0,.28f,0),new Vector3(.8f,.5f,.45f),wood);
                foreach(int s in new[]{-1,1}){B(p,"LeatherStrap",new Vector3(s*.22f,.535f,0),new Vector3(.055f,.025f,.46f),"#D3B576");B(p,"TrunkLatch",new Vector3(s*.22f,.34f,-.235f),new Vector3(.07f,.12f,.025f),"#D3B576");}
                B(p,"TrunkHandle",new Vector3(0,.3f,-.25f),new Vector3(.23f,.06f,.04f),"#3E433A");break;
            case RoomDecorCatalog.Nightstand:
                B(p,"OakCabinet",new Vector3(0,.31f,0),new Vector3(.55f,.58f,.45f),wood);
                for(int i=0;i<2;i++){B(p,"DrawerFace",new Vector3(0,.22f+i*.24f,-.23f),new Vector3(.47f,.19f,.025f),fabric);B(p,"BrassHandle",new Vector3(0,.22f+i*.24f,-.255f),new Vector3(.14f,.027f,.025f),"#DAB86C");}
                HotelArtDirection.Lamp(p,new Vector3(0,.61f,0));break;
            case RoomDecorCatalog.TeaCorner:
                B(p,"TeaCabinet",new Vector3(0,.38f,0),new Vector3(.95f,.73f,.5f),wood);
                B(p,"TeaCountertop",new Vector3(0,.78f,0),new Vector3(1,.08f,.55f),"#E7D9B7");
                foreach(int s in new[]{-1,1})S(p,"TeaCup",PrimitiveType.Cylinder,new Vector3(s*.28f,.91f,-.05f),new Vector3(.13f,.08f,.13f),"#F0E7D5");
                S(p,"Teapot",PrimitiveType.Sphere,new Vector3(0,.94f,.08f),new Vector3(.27f,.22f,.23f),fabric);
                B(p,"TeapotSpout",new Vector3(.15f,.95f,.08f),new Vector3(.18f,.07f,.07f),fabric,new Vector3(0,0,25));break;
            case RoomDecorCatalog.Clock:
                S(p,"WalnutClockFrame",PrimitiveType.Cylinder,new Vector3(0,1.45f,0),new Vector3(.5f,.035f,.5f),wood,new Vector3(90,0,0));
                S(p,"ClockFace",PrimitiveType.Cylinder,new Vector3(0,1.45f,-.04f),new Vector3(.44f,.015f,.44f),"#F5E4BD",new Vector3(90,0,0));
                B(p,"ClockMinuteHand",new Vector3(0,1.52f,-.06f),new Vector3(.02f,.17f,.015f),"#40564D");
                B(p,"ClockHourHand",new Vector3(-.05f,1.45f,-.06f),new Vector3(.11f,.025f,.015f),"#40564D");break;
            case RoomDecorCatalog.Runner:
                B(p,"WovenRug",new Vector3(0,.055f,0),new Vector3(1,.035f,1.8f),fabric);
                for(int i=0;i<7;i++)B(p,"RugStripe",new Vector3(0,.076f,-.72f+i*.24f),new Vector3(.92f,.008f,.035f),"#E3CAA0");break;
            case RoomDecorCatalog.Armchair:
                LowPolyHotelKit.BuildSofaAt(p,"Armchair",Vector3.zero,0,HotelArtMaterial.FabricTeal,.75f);break;
        }
        Combine(p);return root;
    }
    private static readonly Dictionary<string,Material> PaintingMaterials=new Dictionary<string,Material>();
    private static void BuildPainting(Transform p,int kind,int variant)
    {
        B(p,"GalleryFrame",new Vector3(0,1.35f,0),new Vector3(.8f,1.14f,.09f),variant==2?"#5C4B37":"#C6A15D");
        B(p,"IvoryMount",new Vector3(0,1.35f,-.055f),new Vector3(.72f,1.06f,.025f),"#F4E8CD");
        bool botanical=(kind==RoomDecorCatalog.BotanicalArt)^(variant%2==1);string key=botanical?"BotanicalPrint":"RiversidePrint";
        if(!PaintingMaterials.TryGetValue(key,out var mat)||mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.name="OriginalArt_"+key;mat.mainTexture=Resources.Load<Texture2D>("HotelArt/Decor/"+key);if(mat.HasProperty("_Cull"))mat.SetInt("_Cull",0);PaintingMaterials[key]=mat;}
        var canvas=GameObject.CreatePrimitive(PrimitiveType.Quad);canvas.name="OriginalPainting";canvas.transform.SetParent(p,false);canvas.transform.localPosition=new Vector3(0,1.35f,-.075f);canvas.transform.localScale=new Vector3(.64f,.96f,1);canvas.GetComponent<Renderer>().sharedMaterial=mat;
        var collider=canvas.GetComponent<Collider>();if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
    }
    public static void Combine(Transform root)
    {
        var groups=new Dictionary<Material,List<MeshFilter>>();foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
            var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||filter.sharedMesh==null||renderer.sharedMaterial==null)continue;
            if(!groups.TryGetValue(renderer.sharedMaterial,out var list)){list=new List<MeshFilter>();groups[renderer.sharedMaterial]=list;}list.Add(filter);
        }
        var remove=new List<GameObject>();
        foreach(var group in groups){
            if(group.Value.Count<2)continue;var combine=new CombineInstance[group.Value.Count];
            for(int i=0;i<combine.Length;i++){var f=group.Value[i];combine[i]=new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix};remove.Add(f.gameObject);}
            var mesh=new Mesh{name="DesignedRoomMesh",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine,true,true);
            var go=new GameObject("Combined_"+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer),typeof(RoomDesignOwnedMesh));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=group.Key;go.GetComponent<RoomDesignOwnedMesh>().owned=mesh;
        }
        foreach(var go in remove)if(go!=null){go.SetActive(false);if(Application.isPlaying)Object.Destroy(go);else Object.DestroyImmediate(go);}
    }
}
public sealed class RoomDesignOwnedMesh:MonoBehaviour
{
    public Mesh owned;
    private void OnDestroy(){if(owned!=null){if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);}}
}
public sealed class RoomDesignCutaway:MonoBehaviour
{
    public Camera cameraOverride;
    private readonly List<Transform> _faces=new List<Transform>();private readonly List<Vector3> _normals=new List<Vector3>();
    public void Add(Transform face,Vector3 normal){_faces.Add(face);_normals.Add(normal);}
    private void LateUpdate()
    {
        var camera=cameraOverride!=null?cameraOverride:Camera.main;if(camera==null)return;var toward=camera.transform.position-transform.position;toward.y=0;toward.Normalize();
        for(int i=0;i<_faces.Count;i++)if(_faces[i]!=null){bool show=Vector3.Dot(transform.TransformDirection(_normals[i]),toward)<.15f;foreach(var r in _faces[i].GetComponentsInChildren<Renderer>())r.enabled=show;}
    }
}
