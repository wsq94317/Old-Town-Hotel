using UnityEngine;

public partial class RoomDoor
{
    private const float LeafWidth=1.68f, LeafHeight=1f, LeafDepth=.10f;
    private const float LeafCentreHeight=.58f;

    /// <summary>Shared by scene authoring, editable floor prefabs and runtime room expansion.</summary>
    public void RefreshDoorVisuals()
    {
        var panel=transform.Find("DoorPanel");
        if(panel==null) {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); KillCollider(cube);
            cube.name="DoorPanel"; panel=cube.transform; panel.SetParent(transform,false);
        }
        var billboard=panel.GetComponent<BillboardSprite>(); if(billboard!=null) billboard.enabled=false;
        var filter=panel.GetComponent<MeshFilter>();
        if(filter!=null && (filter.sharedMesh==null || filter.sharedMesh.bounds.size.z<.1f)) {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
            filter.sharedMesh=cube.GetComponent<MeshFilter>().sharedMesh;
            if(Application.isPlaying) Destroy(cube); else DestroyImmediate(cube);
        }
        panel.localScale=new Vector3(LeafWidth,LeafHeight,LeafDepth);
        panel.gameObject.layer=2;
        panel.GetComponent<Renderer>().sharedMaterial=HotelSurfaceLibrary.For("DoorPanelWalnut",new Color(.46f,.30f,.17f));
        _doorVisual=panel.gameObject;
        if(panel.Find("DoorCraftwork")==null) {
            var detail=new GameObject("DoorCraftwork").transform; detail.SetParent(panel,false);
            for(int side=-1;side<=1;side+=2) {
                Part(detail,"RecessedWoodPanel",new Vector3(0,0,side*.056f),new Vector3(1.38f,.75f,.016f),false);
                for(int edge=-1;edge<=1;edge+=2) {
                    Part(detail,"WalnutVerticalTrim",new Vector3(edge*.71f,0,side*.068f),new Vector3(.035f,.82f,.018f),false);
                    Part(detail,"WalnutCrossTrim",new Vector3(0,edge*.40f,side*.068f),new Vector3(1.45f,.035f,.018f),false);
                }
                Part(detail,"BrassHandlePlate",new Vector3(.60f,-.02f,side*.084f),new Vector3(.08f,.20f,.02f),true);
                Part(detail,"BrassHandle",new Vector3(.54f,-.02f,side*.12f),new Vector3(.18f,.032f,.035f),true);
            }
        }
        ApplyDoorPose(_openness);
        // The frame must stand proud of the 20 cm wall; coplanar faces flicker and hide its lower half.
        var floor=transform.parent;
        var frame=floor!=null ? floor.Find("ArtPass_LowPoly/Frame_"+RoomSceneBinder.ParseRoomNumber(name)) : null;
        if(frame!=null) foreach(Transform part in frame) {
            if(part.name!="PostL" && part.name!="PostR" && part.name!="Header") continue;
            var size=part.localScale; size.z=.32f; part.localScale=size;
        }
    }

    private static void Part(Transform parent,string name,Vector3 position,Vector3 size,bool brass)
    {
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); KillCollider(cube);
        cube.layer=2;
        cube.name=name; cube.transform.SetParent(parent,false);
        cube.transform.localPosition=new Vector3(position.x/LeafWidth,position.y/LeafHeight,position.z/LeafDepth);
        cube.transform.localScale=new Vector3(size.x/LeafWidth,size.y/LeafHeight,size.z/LeafDepth);
        cube.GetComponent<Renderer>().sharedMaterial=HotelSurfaceLibrary.For(name,
            brass ? new Color(.82f,.65f,.33f) : new Color(.55f,.37f,.21f));
    }

    private void ApplyDoorPose(float openness)
    {
        if(_doorVisual==null) return;
        Vector3 inward=transform.InverseTransformDirection(interiorCenter-transform.position);
        float angle=(inward.z>=0 ? -90f : 90f)*Mathf.Clamp01(openness);
        Quaternion rotation=Quaternion.Euler(0,angle,0);
        // Pivot 6 cm inside the jamb, with the leaf entirely inside the opening throughout its sweep.
        var hinge=new Vector3(-LeafWidth*.5f,LeafCentreHeight,0);
        _doorVisual.transform.localPosition=hinge+rotation*(Vector3.right*(LeafWidth*.5f));
        _doorVisual.transform.localRotation=rotation;
    }
}
