using UnityEngine;

/// <summary>Modeled furniture silhouettes; variants alter upholstery and headboard construction.</summary>
public static class HotelFurnitureArt
{
    private static Color C(string s)=>HotelVisualStyle.C(s);
    private static void B(Transform p,string n,Vector3 pos,Vector3 size,Color c,Vector3 angle=default)=>HotelVisualStyle.Box(p,n,pos,size,c,angle);
    private static void S(Transform p,string n,Vector3 pos,Vector3 size,Color c)=>HotelVisualStyle.Shape(p,n,PrimitiveType.Sphere,pos,size,c);
    public static GameObject Create(FurnitureKind kind,int variant)
    {
        int id=kind.kindId;
        if(id!=FurnitureCatalog.SaggingBed&&id!=FurnitureCatalog.ProperBed&&id!=FurnitureCatalog.MemoryFoamBed&&id!=FurnitureCatalog.Sofa&&id!=FurnitureCatalog.WorkDesk&&id!=FurnitureCatalog.Rug)return null;
        var root=new GameObject(kind.name+"_Revival_"+variant);root.layer=2;var p=root.transform;
        Color wood=C("#7D5639"),gold=C("#CEA45E"),cream=C("#F4E7C9"),white=C("#FFF1DC");
        Color fabric=variant==1?C("#7E9B73"):variant==2?C("#B46C5D"):variant==3?C("#4E677D"):C("#43897F");
        if(id==FurnitureCatalog.SaggingBed||id==FurnitureCatalog.ProperBed||id==FurnitureCatalog.MemoryFoamBed)
        {
            bool premium=id==FurnitureCatalog.MemoryFoamBed, old=id==FurnitureCatalog.SaggingBed;float w=premium?1.50f:1.38f;
            B(p,"OakFrame",new Vector3(0,.25f,0),new Vector3(w,.25f,1.95f),wood);
            // Rounded mattress and individually modeled bedding create a soft silhouette.
            B(p,"MattressCore",new Vector3(0,.47f,0),new Vector3(w-.07f,.22f,1.80f),cream);
            S(p,"MattressEdge",new Vector3(0,.51f,0),new Vector3(w,.27f,1.92f),cream);
            B(p,"Duvet",new Vector3(0,.62f,-.28f),new Vector3(w-.06f,.10f,1.12f),old?C("#B5A17C"):fabric);
            for(int side=-1;side<=1;side+=2) {
                B(p,"DuvetDrape",new Vector3(side*(w*.49f),.48f,-.28f),new Vector3(.08f,.31f,1.15f),fabric);
                S(p,"Pillow",new Vector3(side*w*.23f,.67f,.57f),new Vector3(w*.43f,.20f,.46f),white);
                B(p,"PillowSeam",new Vector3(side*w*.23f,.68f,.35f),new Vector3(w*.32f,.025f,.02f),cream);
                for(int z=-1;z<=1;z+=2)HotelVisualStyle.Shape(p,"TurnedFoot",PrimitiveType.Cylinder,new Vector3(side*w*.41f,.11f,z*.82f),new Vector3(.11f,.11f,.11f),wood);
            }
            B(p,"FoldedRunner",new Vector3(0,.68f,-.64f),new Vector3(w+.035f,.035f,.27f),gold);
            if(variant==2||premium) {
                S(p,"ArchedHeadboard",new Vector3(0,.96f,.94f),new Vector3(w+.18f,1.03f,.18f),wood);
                S(p,"UpholsteredHeadboard",new Vector3(0,.96f,.82f),new Vector3(w-.02f,.81f,.12f),fabric);
                for(int x=-1;x<=1;x++)for(int y=0;y<2;y++)S(p,"TuftButton",new Vector3(x*.37f,.85f+y*.27f,.75f),new Vector3(.045f,.045f,.03f),gold);
            }else{
                B(p,"HeadboardFrame",new Vector3(0,.95f,.94f),new Vector3(w+.16f,.94f,.15f),wood);
                B(p,"HeadboardInset",new Vector3(0,1.02f,.84f),new Vector3(w-.10f,.64f,.07f),variant==1?cream:fabric);
                for(int x=-1;x<=1;x++)B(p,"HeadboardFlute",new Vector3(x*.35f,1.02f,.79f),new Vector3(.055f,.58f,.03f),gold);
                B(p,"CrownRail",new Vector3(0,1.45f,.93f),new Vector3(w+.23f,.09f,.20f),gold);
            }
        }
        else if(id==FurnitureCatalog.Sofa)
        {
            B(p,"SofaBase",new Vector3(0,.27f,0),new Vector3(1.55f,.20f,.72f),wood);
            S(p,"SoftBack",new Vector3(0,.77f,.30f),new Vector3(1.58f,.76f,.32f),fabric);
            for(int s=-1;s<=1;s+=2){S(p,"SeatCushion",new Vector3(s*.35f,.48f,-.04f),new Vector3(.74f,.25f,.72f),fabric);S(p,"RolledArm",new Vector3(s*.81f,.58f,0),new Vector3(.30f,.47f,.83f),fabric);S(p,"ThrowPillow",new Vector3(s*.52f,.76f,.10f),new Vector3(.35f,.38f,.18f),s<0?gold:cream);for(int z=-1;z<=1;z+=2)B(p,"BrassFoot",new Vector3(s*.64f,.12f,z*.25f),new Vector3(.07f,.24f,.07f),gold);}
        }
        else if(id==FurnitureCatalog.WorkDesk)
        {
            B(p,"WritingSurface",new Vector3(0,.80f,0),new Vector3(1.3f,.10f,.64f),wood);
            for(int s=-1;s<=1;s+=2)B(p,"DeskLeg",new Vector3(s*.56f,.38f,.15f),new Vector3(.10f,.77f,.12f),wood);
            B(p,"Drawer",new Vector3(0,.65f,0),new Vector3(1.10f,.22f,.52f),wood);B(p,"Handle",new Vector3(0,.65f,-.28f),new Vector3(.24f,.04f,.06f),gold);
            B(p,"WritingMat",new Vector3(0,.86f,0),new Vector3(.58f,.018f,.39f),fabric);B(p,"Letter",new Vector3(0,.88f,0),new Vector3(.24f,.012f,.30f),cream,new Vector3(0,12,0));
            HotelArtDirection.Lamp(p,new Vector3(.43f,.86f,.1f));
            S(p,"ChairSeat",new Vector3(0,.47f,-.53f),new Vector3(.55f,.15f,.53f),fabric);B(p,"ChairBack",new Vector3(0,.72f,-.78f),new Vector3(.52f,.55f,.07f),wood);
            for(int s=-1;s<=1;s+=2)for(int z=-1;z<=1;z+=2)B(p,"ChairLeg",new Vector3(s*.19f,.23f,-.53f+z*.17f),new Vector3(.055f,.46f,.055f),wood);
        }
        else {
            B(p,"WovenRug",new Vector3(0,.025f,0),new Vector3(1.65f,.035f,1.65f),fabric);
            for(int s=-1;s<=1;s+=2){B(p,"RugBorder",new Vector3(s*.72f,.047f,0),new Vector3(.055f,.008f,1.48f),gold);B(p,"RugBorder",new Vector3(0,.047f,s*.72f),new Vector3(1.48f,.008f,.055f),gold);}
            B(p,"RugMedallion",new Vector3(0,.05f,0),new Vector3(.46f,.008f,.46f),cream,new Vector3(0,45,0));
        }
        return root;
    }
}
