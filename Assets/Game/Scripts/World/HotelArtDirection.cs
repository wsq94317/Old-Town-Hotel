using UnityEngine;

/// <summary>Visible architecture pass shared by the authored scene and runtime room expansion.</summary>
public static class HotelArtDirection
{
    private static Color C(string s)=>HotelVisualStyle.C(s);
    private static void B(Transform p,string n,Vector3 pos,Vector3 size,string color,Vector3 angle=default)=>HotelVisualStyle.Box(p,n,pos,size,C(color),angle);
    public static void Install()
    {
        foreach(var room in Object.FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None)) DressRoom(room.transform);
        var lobby=GameObject.Find("World/Floor1");if(lobby!=null) DressLobby(lobby.transform);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=C("#C9D5D4");RenderSettings.ambientEquatorColor=C("#B9AD97");RenderSettings.ambientGroundColor=C("#72614B");
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light.type==LightType.Directional){light.color=C("#FFF0D7");light.intensity=1.35f;light.shadows=LightShadows.Soft;light.shadowStrength=.65f;}
        var cam=Camera.main;if(cam!=null)cam.backgroundColor=C("#DDD1B8");
    }
    public static void DressRoom(Transform room)
    {
        foreach(Transform wall in room) {
            if(wall.name=="Wall_Div"||wall.name=="Wall_Corr_L"||wall.name=="Wall_Corr_R"){
                var size=wall.localScale;size.y=.55f;wall.localScale=size;
                var position=wall.localPosition;position.y=.275f;wall.localPosition=position;
            }
        }
        if(room.Find("RevivalArchitecture")!=null)return;
        var root=HotelCharacterArt.Joint(room,"RevivalArchitecture",Vector3.zero);
        float back=room.localPosition.z>=0?1:-1;
        // The cutaway keeps the corridor open and frames the room with tactile woodwork.
        B(root,"OakFloor",new Vector3(0,.018f,0),new Vector3(4.78f,.035f,4.8f),"#AF8A5D");
        for(int i=0;i<9;i++)B(root,"FloorboardSeam",new Vector3(-2.13f+i*.53f,.039f,0),new Vector3(.018f,.009f,4.8f),"#8B6949");
        B(root,"SageWallpaper",new Vector3(-2.37f,.55f,0),new Vector3(.055f,.18f,4.8f),"#7F9B87");
        B(root,"CreamDadoRail",new Vector3(-2.32f,.46f,0),new Vector3(.095f,.075f,4.85f),"#F1D9AE");
        B(root,"WalnutSkirting",new Vector3(-2.32f,.14f,0),new Vector3(.1f,.22f,4.85f),"#644A37");
        for(int i=0;i<6;i++)B(root,"WainscotPanel",new Vector3(-2.32f,.29f,-2+i*.8f),new Vector3(.08f,.30f,.67f),"#8D6A46");
        // Low back wall/window avoid hiding guests in the oblique camera.
        B(root,"BackWall",new Vector3(0,.65f,back*2.40f),new Vector3(4.8f,1.3f,.13f),"#A3B3A0");
        B(root,"WindowFrame",new Vector3(.4f,1.38f,back*2.31f),new Vector3(1.95f,1.15f,.14f),"#F4DDB3");
        B(root,"DaylightGlass",new Vector3(.4f,1.40f,back*2.21f),new Vector3(1.69f,.91f,.06f),"#A7D3D5");
        B(root,"WindowMullion",new Vector3(.4f,1.40f,back*2.16f),new Vector3(.07f,.95f,.06f),"#F4DDB3");
        B(root,"WindowCrossbar",new Vector3(.4f,1.40f,back*2.16f),new Vector3(1.72f,.065f,.06f),"#F4DDB3");
        for(int s=-1;s<=1;s+=2){
            B(root,"Curtain",new Vector3(.4f+s*1.02f,1.35f,back*2.13f),new Vector3(.35f,1.30f,.20f),"#417D75");
            B(root,"CurtainTie",new Vector3(.4f+s*1.02f,1.13f,back*2.0f),new Vector3(.39f,.08f,.05f),"#DAB76B");
        }
        B(root,"CurtainRod",new Vector3(.4f,2.06f,back*2.12f),new Vector3(2.65f,.065f,.065f),"#CCA75D");
        B(root,"BedsideCabinet",new Vector3(-1.7f,.36f,back*1.25f),new Vector3(.53f,.65f,.48f),"#88613C");
        B(root,"Drawer",new Vector3(-1.7f,.43f,back*1.0f),new Vector3(.45f,.21f,.02f),"#B58950");
        B(root,"DrawerPull",new Vector3(-1.7f,.43f,back*.98f),new Vector3(.15f,.035f,.04f),"#E9C97B");
        Lamp(root,new Vector3(-1.7f,.70f,back*1.25f));
        B(root,"FramedArt",new Vector3(-1.6f,1.37f,back*2.20f),new Vector3(.65f,.60f,.09f),"#C19A58");
        B(root,"ArtMount",new Vector3(-1.6f,1.37f,back*2.14f),new Vector3(.53f,.48f,.025f),"#F7E4BE");
        B(root,"ArtLandscape",new Vector3(-1.6f,1.35f,back*2.12f),new Vector3(.41f,.28f,.025f),"#6E9A92");
        var rubble=HotelCharacterArt.Joint(room,"RevivalRuins",Vector3.zero);
        for(int i=0;i<7;i++){
            float x=-1.45f+(i*1.13f%2.9f),z=-1.3f+(i*.83f%2.5f);
            B(rubble,"BrokenPlank",new Vector3(x,.09f,z),new Vector3(.63f,.10f,.17f),i%2==0?"#75614D":"#997F61",new Vector3(0,i*39,7));
            HotelVisualStyle.Shape(rubble,"Rubble",PrimitiveType.Sphere,new Vector3(x+.15f,.10f,z+.2f),new Vector3(.28f,.16f,.21f),C("#8D8C7C"));
        }
        B(rubble,"AbandonedTrunk",new Vector3(1.45f,.26f,1.45f),new Vector3(.66f,.48f,.45f),"#7B6750");
        B(rubble,"TrunkStrap",new Vector3(1.45f,.51f,1.45f),new Vector3(.09f,.025f,.46f),"#413C36");
        if(room.GetComponent<HotelRoomAtmosphere>()==null)room.gameObject.AddComponent<HotelRoomAtmosphere>();
    }
    public static void Lamp(Transform p,Vector3 pos)
    {
        var root=HotelCharacterArt.Joint(p,"TableLamp",pos);
        HotelVisualStyle.Shape(root,"BrassFoot",PrimitiveType.Cylinder,new Vector3(0,.025f,0),new Vector3(.25f,.025f,.25f),C("#D8B56D"));
        B(root,"Stem",new Vector3(0,.18f,0),new Vector3(.05f,.32f,.05f),"#B38B44");
        HotelVisualStyle.Shape(root,"WarmShade",PrimitiveType.Cylinder,new Vector3(0,.37f,0),new Vector3(.42f,.29f,.42f),C("#FFE0A1"));
        HotelVisualStyle.Shape(root,"ShadeTop",PrimitiveType.Sphere,new Vector3(0,.49f,0),new Vector3(.19f,.05f,.19f),C("#FFEBCD"));
    }
    private static void DressLobby(Transform lobby)
    {
        if(lobby.Find("RevivalArchitecture")!=null)return;
        var root=HotelCharacterArt.Joint(lobby,"RevivalArchitecture",Vector3.zero);
        for(int x=-9;x<=9;x++)for(int z=-5;z<=5;z++)
            B(root,"TerrazzoTile",new Vector3(x,.012f,z),new Vector3(.975f,.025f,.975f),(x+z)%2==0?"#C9BEA6":"#DED3BB");
        B(root,"EntryCarpetBorder",new Vector3(0,.040f,-1.8f),new Vector3(3.1f,.045f,6.4f),"#CCA45E");
        B(root,"EntryCarpet",new Vector3(0,.067f,-1.8f),new Vector3(2.88f,.025f,6.18f),"#964E43");
        for(int z=-4;z<=0;z++)for(int s=-1;s<=1;s+=2)
            B(root,"CarpetMedallion",new Vector3(s*.95f,.084f,z+.2f),new Vector3(.18f,.012f,.18f),"#D1AF6B",new Vector3(0,45,0));
        B(root,"ReceptionFeatureWall",new Vector3(0,1.4f,5.82f),new Vector3(9,2.8f,.14f),"#4F776B");
        B(root,"WallCornice",new Vector3(0,2.82f,5.68f),new Vector3(9.25f,.16f,.28f),"#E8C995");
        B(root,"WallDado",new Vector3(0,.84f,5.65f),new Vector3(9.1f,.10f,.20f),"#E8C995");
        for(int i=-4;i<=4;i++)B(root,"WallPanel",new Vector3(i,.41f,5.67f),new Vector3(.88f,.68f,.12f),"#775436");
        B(root,"HotelSignFrame",new Vector3(-.2f,2.05f,5.59f),new Vector3(3.75f,1.05f,.13f),"#D0A55A");
        B(root,"HotelSign",new Vector3(-.2f,2.05f,5.50f),new Vector3(3.60f,.90f,.08f),"#294E46");
        var sign=HotelCharacterArt.Joint(root,"HotelName",new Vector3(-.2f,2.05f,5.43f));sign.localRotation=Quaternion.identity;
        var text=sign.gameObject.AddComponent<TextMesh>();text.text="OLD TOWN\nH O T E L";text.fontSize=64;text.characterSize=.075f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=C("#F2D79D");
        var font=Resources.Load<Font>("HotelFonts/NotoSansSC");if(font!=null){text.font=font;sign.GetComponent<Renderer>().sharedMaterial=font.material;}
        // Detailed front apron, lamp and register accent the existing interactive reception counter.
        var counter=HotelCharacterArt.Joint(root,"ConciergeCounter",new Vector3(0,0,2.94f));
        B(counter,"CounterFront",new Vector3(0,.49f,-.48f),new Vector3(3.28f,.96f,.12f),"#754D31");
        for(int i=-1;i<=1;i++) {
            B(counter,"InsetPanel",new Vector3(i*1.03f,.49f,-.55f),new Vector3(.91f,.64f,.04f),"#446E61");
            B(counter,"PanelTopTrim",new Vector3(i*1.03f,.83f,-.58f),new Vector3(.96f,.055f,.04f),"#D8B56D");
            B(counter,"PanelBottomTrim",new Vector3(i*1.03f,.15f,-.58f),new Vector3(.96f,.055f,.04f),"#D8B56D");
        }
        B(counter,"MarbleCountertop",new Vector3(0,1.02f,-.04f),new Vector3(3.62f,.16f,1.1f),"#E9D6AC");
        Lamp(counter,new Vector3(-1.2f,1.1f,0));
        for(int s=-1;s<=1;s+=2) LowPolyHotelKit.BuildPlant(root,new Vector3(s*3.5f,0,4.8f),1.8f);
        if(FurnitureCatalog.TryGet(FurnitureCatalog.Sofa,out var sofaKind)) {
            var sofaA=HotelFurnitureArt.Create(sofaKind,2);sofaA.name="ConciergeLoungeSofa";sofaA.transform.SetParent(root,false);sofaA.transform.localPosition=new Vector3(-4.65f,0,1.6f);sofaA.transform.localScale=new Vector3(1.2f,1,1);
            var sofaB=HotelFurnitureArt.Create(sofaKind,0);sofaB.name="ConciergeLoungeArmchair";sofaB.transform.SetParent(root,false);sofaB.transform.localPosition=new Vector3(-6.05f,0,.35f);sofaB.transform.localRotation=Quaternion.Euler(0,-90,0);sofaB.transform.localScale=new Vector3(.85f,1,1);
        }
        // Clock with modeled face and hands.
        var clock=HotelCharacterArt.Joint(root,"LobbyClock",new Vector3(3.5f,2,5.55f));
        HotelVisualStyle.Shape(clock,"ClockFrame",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.8f,.05f,.8f),C("#C49B54"),new Vector3(90,0,0));
        HotelVisualStyle.Shape(clock,"ClockFace",PrimitiveType.Cylinder,new Vector3(0,0,-.06f),new Vector3(.70f,.02f,.70f),C("#F7E6C5"),new Vector3(90,0,0));
        B(clock,"MinuteHand",new Vector3(0,.12f,-.09f),new Vector3(.025f,.27f,.025f),"#3A4942");
        B(clock,"HourHand",new Vector3(.07f,0,-.10f),new Vector3(.20f,.035f,.025f),"#3A4942");
    }
}

