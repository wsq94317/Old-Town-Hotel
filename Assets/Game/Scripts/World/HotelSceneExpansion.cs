using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>Preserves the room ledger while materializing the shared courtyard floor plan.</summary>
public static class HotelSceneExpansion
{
    public const int ExpandedRoomCount=100, StartingDerelictRooms=92;
    internal static void Install()
    {
        var bridge=Object.FindFirstObjectByType<HotelSimSceneBridge>();
        var world=GameObject.Find("World");var data=GameObject.Find("RoomData");
        if(bridge==null||world==null||data==null)return;
        EnsureEditableGuestFloorVisuals(world.transform.Find("Floor2"),world.transform.Find("Floor3"));
        var entities=new Dictionary<int,Room2DEntity>();
        foreach(var entity in data.GetComponentsInChildren<Room2DEntity>(true))entities[entity.roomNumber]=entity;
        foreach(int floor in new[]{2,3})for(int n=1;n<=50;n++){
            int number=floor*100+n;
            if(!entities.TryGetValue(number,out var entity)){
                var clone=Object.Instantiate(entities[floor*100+1].gameObject,data.transform,false);
                clone.name="Room_"+number+"_Anchor";entity=clone.GetComponent<Room2DEntity>();
                entity.SetIdentity(floor,number);entity.SetState(Room2DState.Ready);
                if(n>12){int mix=(n-1)%6;entity.roomCategory=mix<3?Room2DRoomCategory.Single:mix<5?Room2DRoomCategory.Twin:Room2DRoomCategory.Family;}
                entities[number]=entity;
            }
            entity.transform.position=HotelFloorPlan.Room(number).Centre;
            var root=world.transform.Find("Floor"+floor);
            root.Find("Door_"+number).GetComponent<RoomDoor>().Configure(entity,entity.transform.position);
        }
        bridge.SetDerelictRoomsAtStart(StartingDerelictRooms);
        var rig=Object.FindFirstObjectByType<ManagerCameraRig>();if(rig!=null)rig.SetWorldBounds(HotelFloorPlan.Min,HotelFloorPlan.Max);
        var demand=Object.FindFirstObjectByType<Room2DPrototypeDemandLoop>();if(demand!=null)demand.FindRoomsInScene();
        var overview=Object.FindFirstObjectByType<Room2DOverview>();if(overview!=null)overview.FindRoomsInScene();
        RebuildNavigation(world.transform);
        Debug.Log("[HotelExpansion] Courtyard hotel: 100 rooms, four wings per guest floor, aligned 86 x 40 floor plates. Opening stock remains 8 rooms; 92 require reclamation.");
    }
    public static void EnsureEditableGuestFloorVisuals(Transform floor2,Transform floor3)
    {
        if(floor2==null||floor3==null)return;
        foreach(var floor in new[]{floor2,floor3}){
            int prefix=floor==floor2?200:300;
            var roomSource=floor2.Find("Room_201");var doorSource=floor2.Find("Door_201");var art=floor.Find("ArtPass_LowPoly");
            for(int n=1;n<=50;n++){
                int number=prefix+n;var pose=HotelFloorPlan.Room(number);var room=floor.Find("Room_"+number);
                if(room==null){room=Object.Instantiate(roomSource,floor,false);room.name="Room_"+number;}
                float localBack=-HotelFloorPlan.LocalDoorSide(room);
                room.position=pose.Centre;room.rotation=Quaternion.LookRotation(pose.Outward*localBack);
                room.GetComponent<RoomSceneBinder>().ConfigureRoomNumber(number);
                var furniture=room.GetComponent<RoomFurnitureView>();if(furniture!=null)furniture.ConfigureRoomNumber(number);
                var door=floor.Find("Door_"+number);
                if(door==null){door=Object.Instantiate(doorSource,floor,false);door.name="Door_"+number;}
                door.position=pose.Door;door.rotation=room.rotation;
                var component=door.GetComponent<RoomDoor>();component.Configure(null,pose.Centre);component.RefreshDoorVisuals();
                var dark=door.Find("Darkness");if(dark!=null){dark.position=pose.Centre+Vector3.up*.07f;dark.rotation=Quaternion.Euler(90,0,0);}
                if(art!=null)PlaceDoorDecor(art,number,pose);
            }
            // The original eight-room strip used unnamed lamps and plaques. Keeping them
            // would leave a second row of fittings floating across the new central hall.
            if(art!=null){
                var obsolete=new List<Transform>();
                foreach(Transform child in art)if(child.name=="WallLamp"||child.name=="RoomPlaque")obsolete.Add(child);
                foreach(var child in obsolete){child.gameObject.SetActive(false);if(Application.isPlaying)Object.Destroy(child.gameObject);else Object.DestroyImmediate(child.gameObject);}
            }
        }
        HotelBuildingLayout.Apply(floor2.parent);
    }
    private static void PlaceDoorDecor(Transform art,int number,HotelFloorPlan.RoomPose pose)
    {
        var direction=Quaternion.LookRotation(pose.Outward);
        foreach(string prefix in new[]{"Frame_","WallLamp_","RoomPlaque_"}){
            var target=art.Find(prefix+number);Transform source=art.Find(prefix+"201");
            if(source==null)foreach(Transform child in art)if(child.name.StartsWith(prefix)){source=child;break;}
            if(target==null&&source!=null){target=Object.Instantiate(source,art,false);target.name=prefix+number;}
            if(target==null)continue;
            Vector3 offset=prefix=="Frame_"?Vector3.zero:prefix=="WallLamp_"?new Vector3(1.65f,.82f,-.05f):new Vector3(1.24f,0,-.06f);
            target.position=pose.Door+direction*offset;target.rotation=direction;
        }
    }
    public static void RebuildNavigation(Transform world)
    {
        var surface=world.GetComponentInChildren<NavMeshSurface>(true);if(surface==null)return;
        var states=new List<KeyValuePair<GameObject,bool>>();
        foreach(Transform floor in world)if(floor.name.StartsWith("Floor")){states.Add(new KeyValuePair<GameObject,bool>(floor.gameObject,floor.gameObject.activeSelf));floor.gameObject.SetActive(true);}
        surface.size=new Vector3(92,35,46);surface.center=new Vector3(0,12,0);surface.BuildNavMesh();
        foreach(var pair in states)pair.Key.SetActive(pair.Value);
    }
}
