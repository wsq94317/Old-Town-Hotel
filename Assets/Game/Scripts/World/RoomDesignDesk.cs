using TMPro;
using UnityEngine;

/// <summary>A visible lobby entry to room decoration, including on touch devices.</summary>
public sealed class RoomDesignDesk:MonoBehaviour
{
    public static void Install()
    {
        var floor=GameObject.Find("World")?.transform.Find("Floor1");if(floor==null||floor.Find("RoomDesignDesk")!=null)return;
        var prefab=Resources.Load<GameObject>("HotelArt/Decor/RoomDesignDesk");if(prefab!=null){var placed=Object.Instantiate(prefab,floor,false);placed.name="RoomDesignDesk";return;}
        var root=new GameObject("RoomDesignDesk");root.transform.SetParent(floor,false);root.transform.localPosition=new Vector3(4.5f,0,-1.8f);root.AddComponent<RoomDesignDesk>();
        Box(root.transform,"OakWorktop",new Vector3(0,.9f,0),new Vector3(2,.12f,1),"#AC8253");
        foreach(int side in new[]{-1,1})Box(root.transform,"Trestle",new Vector3(side*.72f,.43f,0),new Vector3(.14f,.86f,.75f),"#53776C");
        Box(root.transform,"FloorPlan",new Vector3(-.3f,.971f,0),new Vector3(.8f,.012f,.65f),"#F3E9CF");
        for(int i=0;i<4;i++)Box(root.transform,"PaintSwatch",new Vector3(.35f+i*.13f,.98f,-.15f),new Vector3(.11f,.025f,.26f),new[]{"#668E7D","#50687F","#C68E76","#E4D9C4"}[i]);
        Box(root.transform,"SampleBoard",new Vector3(0,1.45f,.4f),new Vector3(1.45f,.9f,.07f),"#35655E");
        var label=new GameObject("DesignDeskSign",typeof(TextMeshPro));label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,1.5f,.35f);label.transform.localRotation=Quaternion.Euler(0,180,0);
        var text=label.GetComponent<TextMeshPro>();text.font=WorldManagementHud.PresentationFont;text.text=GameText.UseChinese?"客房工作室\n点击 · 设计房间":"ROOM STUDIO\nTAP TO DESIGN";text.fontSize=2.2f;text.alignment=TextAlignmentOptions.Center;text.color=new Color(1,.9f,.65f);text.rectTransform.sizeDelta=new Vector2(1.35f,.8f);
        var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.9f,0);collider.size=new Vector3(2,1.8f,1.1f);
        var obstacle=root.AddComponent<UnityEngine.AI.NavMeshObstacle>();obstacle.shape=UnityEngine.AI.NavMeshObstacleShape.Box;obstacle.center=new Vector3(0,.6f,0);obstacle.size=new Vector3(2,1.2f,1.1f);obstacle.carving=true;
    }
    private static void Box(Transform parent,string name,Vector3 p,Vector3 size,string colour){var go=HotelVisualStyle.Box(parent,name,p,size,HotelVisualStyle.C(colour));LowPolyHotelKit.RemoveColliders(go);}
}
