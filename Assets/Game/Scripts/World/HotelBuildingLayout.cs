using UnityEngine;

/// <summary>Visible, editable architecture for the compact four-wing hotel.</summary>
public static class HotelBuildingLayout
{
    private static Color C(string hex)=>HotelVisualStyle.C(hex);
    private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,string colour,bool solid=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=position;go.transform.localScale=size;go.layer=solid?0:2;
        go.GetComponent<Renderer>().sharedMaterial=HotelSurfaceLibrary.For(name,C(colour));
        if(!solid){var collider=go.GetComponent<Collider>();if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);}
        return go;
    }
    private static void Remove(Transform root)
    {if(root==null)return;root.gameObject.SetActive(false);if(Application.isPlaying)Object.Destroy(root.gameObject);else Object.DestroyImmediate(root.gameObject);}
    private static void Anchor(Transform floor,string name,Vector3 position)
    {var t=floor.Find(name);if(t!=null)t.localPosition=position;}
    public static void Apply(Transform world)
    {
        for(int i=0;i<FloorMath.FloorCount;i++){
            var floor=world.Find("Floor"+(i+1));if(floor==null)continue;
            if(floor.Find("CourtyardLayout_v1")==null)BuildFloor(floor,i);
        }
        // The garden belongs to the whole building, so looking down a light well still shows it
        // when the ground-floor interior has been hidden by the floor selector.
        if(world.Find("CourtyardGround")==null){
            var shared=HotelCharacterArt.Joint(world,"CourtyardGround",Vector3.zero);
            var old=world.Find("Floor1/CourtyardLayout_v1");
            var move=new System.Collections.Generic.List<Transform>();
            if(old!=null)foreach(Transform child in old)if(child.name.StartsWith("Garden")||child.name=="CourtyardTree")move.Add(child);
            foreach(var child in move)child.SetParent(shared,true);
            if(shared.childCount==0){Garden(shared,-22.5f);Garden(shared,22.5f);}
        }
        var rig=Object.FindFirstObjectByType<ManagerCameraRig>();if(rig!=null)rig.SetWorldBounds(HotelFloorPlan.Min,HotelFloorPlan.Max);
    }
    private static void BuildFloor(Transform floor,int index)
    {
        bool guest=index==1||index==2;
        var ground=floor.Find("Ground");
        if(ground!=null){ground.localPosition=new Vector3(0,-.1f,0);ground.localScale=new Vector3(HotelFloorPlan.Width,.2f,HotelFloorPlan.Depth);ground.GetComponent<Renderer>().enabled=false;foreach(var c in ground.GetComponents<Collider>())c.enabled=false;}
        foreach(string name in new[]{"Wall_N","Wall_S","Wall_E","Wall_W"})Remove(floor.Find(name));
        var legacy=floor.Find("ArtPass_LowPoly");
        if(legacy!=null&&guest){var remove=new System.Collections.Generic.List<Transform>();foreach(Transform c in legacy)if(c.name.StartsWith("CutawayTrim")||c.name.StartsWith("BrassInlay")||c.name=="CorridorRunner")remove.Add(c);foreach(var t in remove)Remove(t);}
        var root=HotelCharacterArt.Joint(floor,"CourtyardLayout_v1",Vector3.zero);
        // The same load-bearing floor plate on every storey, with two genuine light wells.
        Box(root,"NorthFloorSlab",new Vector3(0,-.1f,12.75f),new Vector3(86,.2f,14.5f),"#D3C6AB",true);
        Box(root,"SouthFloorSlab",new Vector3(0,-.1f,-12.75f),new Vector3(86,.2f,14.5f),"#D3C6AB",true);
        Box(root,"CoreFloorSlab",new Vector3(0,-.1f,0),new Vector3(20,.2f,11),"#D3C6AB",true);
        foreach(int s in new[]{-1,1})Box(root,"EndFloorSlab",new Vector3(s*39,-.1f,0),new Vector3(8,.2f,11),"#D3C6AB",true);
        Box(root,"Wall_N",new Vector3(0,.55f,20),new Vector3(86,1.1f,.2f),"#E4D6BB",true);
        Box(root,"Wall_E",new Vector3(43,.55f,0),new Vector3(.2f,1.1f,40),"#E4D6BB",true);
        Box(root,"Wall_W",new Vector3(-43,.55f,0),new Vector3(.2f,1.1f,40),"#E4D6BB",true);
        if(index==0){foreach(int s in new[]{-1,1})Box(root,"Wall_S",new Vector3(s*22,.55f,-20),new Vector3(42,1.1f,.2f),"#E4D6BB",true);}
        else Box(root,"Wall_S",new Vector3(0,.55f,-20),new Vector3(86,1.1f,.2f),"#E4D6BB",true);
        // Four short guest streets, joined at both ends to avoid a single endless dead end.
        string[] colours={"#3D736B","#546C84","#B28D53","#9B665B"};
        for(int wing=0;wing<4;wing++){
            float x=wing%2==0?-25:25,z=wing<2?12:-12;
            Box(root,"WingRunnerBorder",new Vector3(x,.028f,z),new Vector3(32,.045f,2.2f),"#CCA75D");
            Box(root,"WingRunner",new Vector3(x,.055f,z),new Vector3(31.8f,.025f,2.0f),colours[wing]);
            if(guest){int first=(index+1)*100+wing*12+1;Sign(root,"WingMarker_"+wing,((char)('A'+wing))+"  "+first+"–"+(first+11),new Vector3(wing%2==0?-10.3f:10.3f,.09f,z),.15f,colours[wing]);}
        }
        foreach(int s in new[]{-1,1}){
            Box(root,"EndGalleryRunner",new Vector3(s*41,.03f,0),new Vector3(2.0f,.045f,27),"#817661");
            // Core and end galleries look into the open courtyards, with low guard rails.
            foreach(float edge in new[]{10f,35f}){
                var rail=Box(root,"CourtyardGuardRail",new Vector3(s*edge,.50f,0),new Vector3(.14f,1,10.8f),"#647E72",true);
                Box(root,"BrassRailCap",new Vector3(s*edge,1.04f,0),new Vector3(.21f,.08f,10.8f),"#CCA75D");
            }
        }
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){
            Box(root,"StructuralColumn",new Vector3(x*9.4f,1.5f,z*11),new Vector3(.45f,3,.45f),"#C4B59A");
            Box(root,"ColumnBase",new Vector3(x*9.4f,.15f,z*11),new Vector3(.7f,.3f,.7f),"#7C6B52");
        }
        if(guest)BuildGuestCore(floor,root,index);
        if(index==0)BuildPublicGroundFloor(floor,root);
        if(index>2)BuildUpperTerraces(root,index);
    }
    private static void BuildGuestCore(Transform floor,Transform root,int index)
    {
        Anchor(floor,"Floor"+(index+1)+"HskSupportAnchor",HotelFloorPlan.Linen);
        var cart=floor.Find("ArtPass_LowPoly/HousekeepingCart");if(cart!=null)cart.localPosition=new Vector3(-7.25f,0,8);
        Box(root,"LinenTile",new Vector3(-6.5f,.025f,8),new Vector3(4,.04f,3.4f),"#819B90");
        Box(root,"LinenBackWall",new Vector3(-6.5f,.6f,9.7f),new Vector3(4,1.2f,.16f),"#C7D1BE",true);
        foreach(int s in new[]{-1,1})Box(root,"LinenSideWall",new Vector3(-6.5f+s*2,.6f,8),new Vector3(.16f,1.2f,3.4f),"#C7D1BE",true);
        foreach(int s in new[]{-1,1})Box(root,"LinenDoorJamb",new Vector3(-6.5f+s*1.55f,.6f,6.3f),new Vector3(.9f,1.2f,.16f),"#C7D1BE",true);
        Sign(root,"LinenSign","布草 / 员工后勤",new Vector3(-6.5f,.07f,5.8f),.10f,"#416A60");
        Sign(root,"CentralHallSign",(index+1)+"F  中央交通厅",new Vector3(0,.08f,0),.19f,"#416A60");
        Box(root,"HallRug",new Vector3(0,.028f,0),new Vector3(5.5f,.035f,8),"#BCA67E");
        if(FurnitureCatalog.TryGet(FurnitureCatalog.Sofa,out var sofa))foreach(int s in new[]{-1,1}){
            var model=HotelFurnitureArt.Create(sofa,index);model.name="LiftLobbySeating";model.transform.SetParent(root,false);model.transform.localPosition=new Vector3(-6.5f,0,s*2.5f);model.transform.localRotation=Quaternion.Euler(0,s<0?0:180,0);
        }
        LowPolyHotelKit.BuildPlant(root,new Vector3(5,0,8),1.7f);LowPolyHotelKit.BuildPlant(root,new Vector3(-6.5f,0,-8),1.7f);
    }
    private static void Garden(Transform root,float centre)
    {
        Box(root,"GardenPaving",new Vector3(centre,-.08f,0),new Vector3(25,.16f,11),"#B9B09A",true);
        Box(root,"GardenLawn",new Vector3(centre,.03f,0),new Vector3(22,.06f,8),"#718B67");
        Box(root,"GardenPath",new Vector3(centre,.08f,0),new Vector3(25,.06f,1.8f),"#DDD0B3");
        foreach(float dx in new[]{-7f,0,7f})foreach(int s in new[]{-1,1}){
            var plant=HotelCharacterArt.Joint(root,"CourtyardTree",new Vector3(centre+dx,0,s*3.1f));
            Box(plant,"PlanterStone",new Vector3(0,.2f,0),new Vector3(1.25f,.4f,1.25f),"#B8AC8B");
            LowPolyHotelKit.BuildPlant(plant,Vector3.zero,2.8f);
        }
        Sign(root,"GardenSign",centre<0?"西庭院 · 静园":"东庭院 · 茶园",new Vector3(centre,.12f,2.4f),.19f,"#375C4A");
    }
    private static void BuildPublicGroundFloor(Transform floor,Transform root)
    {
        // Existing reception, luggage and lounge interactions remain in the central entrance hall.
        Anchor(floor,"LobbyStaffBreakRoomAnchor",new Vector3(-25,0,-13.8f));
        Anchor(floor,"ManagerOfficeReservedAnchor",new Vector3(-35,0,-15));
        Sign(root,"EntrySign","主入口 / 前台",new Vector3(0,.12f,-14),.22f,"#365D53");
        Box(root,"EntranceCarpet",new Vector3(0,.03f,-12.5f),new Vector3(3.2f,.05f,14),"#964E43");
        PublicWing(root,-25,12,"会客厅 / 阅读休息区",0);
        PublicWing(root,25,12,"咖啡座 / 早餐休息区",1);
        PublicWing(root,-25,-12,"员工后勤 / 办公 / 仓储",2);
        PublicWing(root,25,-12,"旅行团集合 / 行李等候区",3);
        // A rear route around reception connects the north wings to the lifts and entrance.
        Box(root,"ReceptionHallRunner",new Vector3(0,.025f,12),new Vector3(20,.035f,2),"#817661");
    }
    private static void PublicWing(Transform root,float x,float z,string title,int kind)
    {
        var wing=HotelCharacterArt.Joint(root,"PublicWing_"+kind,new Vector3(x,0,z));
        Box(wing,"PublicOakFloor",new Vector3(0,.015f,0),new Vector3(28,.03f,12),kind==2?"#9EAA95":"#C0A77E");
        Sign(wing,"PublicWingSign",title,new Vector3(0,.09f,4.7f),.22f,"#365D53");
        if(kind==2){
            for(int i=0;i<4;i++){
                Box(wing,"StorageShelf",new Vector3(-10+i*5,.8f,4),new Vector3(3,1.6f,.6f),"#745A3F");
                for(int j=0;j<3;j++)Box(wing,"LinenStorageCrate",new Vector3(-10+i*5,.3f+j*.45f,3.5f),new Vector3(1.5f,.32f,.7f),j%2==0?"#E7DAC0":"#789283");
            }
            return;
        }
        if(kind==0||kind==3){
            for(int i=0;i<4;i++)foreach(int side in new[]{-1,1}){
                float px=-9+i*6,pz=side*3;
                LowPolyHotelKit.BuildSofaAt(wing,"WaitingSofa",new Vector3(px,0,pz),side<0?0:180,kind==0?HotelArtMaterial.FabricTeal:HotelArtMaterial.FabricRed,2.2f);
                LowPolyHotelKit.BuildTableAt(wing,"CoffeeTable",new Vector3(px,0,pz-side*1.3f),1.4f,.8f);
            }
            if(kind==0)for(int i=0;i<4;i++){
                Box(wing,"LibraryBookcase",new Vector3(-9+i*6,1,-5.5f),new Vector3(3,2,.5f),"#816342");
                for(int book=0;book<8;book++)Box(wing,"LibraryBooks",new Vector3(-10.2f+i*6+book*.34f,1.2f,-5.19f),new Vector3(.24f,.65f,.1f),book%2==0?"#638877":"#B49362");
            }
            foreach(int side in new[]{-1,1})LowPolyHotelKit.BuildPlant(wing,new Vector3(side*12,0,4.5f),2);
            return;
        }
        for(int i=0;i<5;i++)foreach(int s in new[]{-1,1}){
            float px=-10+i*5,pz=s*3;
            LowPolyHotelKit.BuildTableAt(wing,"CafeTable",new Vector3(px,0,pz),1.3f,1);
            foreach(int side in new[]{-1,1})LowPolyHotelKit.BuildChairAt(wing,"CafeChair",new Vector3(px+side*1.15f,0,pz),side<0?90:270,HotelArtMaterial.FabricTeal);
        }
        foreach(int s in new[]{-1,1})LowPolyHotelKit.BuildPlant(wing,new Vector3(s*12,0,4.5f),2);
    }
    private static void BuildUpperTerraces(Transform root,int index)
    {
        foreach(int s in new[]{-1,1}){
            Sign(root,"TerraceSign",index==3?"餐饮层 · 庭院回廊":"公共设施层 · 休息露台",new Vector3(s*25,.09f,12),.20f,"#416A60");
            for(int i=0;i<4;i++)LowPolyHotelKit.BuildPlant(root,new Vector3(s*(15+i*7),0,-16),1.8f);
        }
    }
    public static void Sign(Transform parent,string name,string text,Vector3 localPosition,float size,string colour)
    {
        var go=new GameObject(name);go.layer=2;go.transform.SetParent(parent,false);go.transform.localPosition=localPosition;go.transform.localRotation=Quaternion.Euler(90,0,0);
        var label=go.AddComponent<TextMesh>();label.text=text;label.fontSize=64;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=C(colour);
        var font=Resources.Load<Font>("HotelFonts/NotoSansSC");if(font!=null){label.font=font;go.GetComponent<Renderer>().sharedMaterial=font.material;}
    }
}
