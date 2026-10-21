using System.Collections.Generic;
using UnityEngine;

/// <summary>Moves the cutaway to the camera side, so orbiting never traps the player behind a wall.</summary>
[DefaultExecutionOrder(110)]
public sealed class HotelAdaptiveCutaway : MonoBehaviour
{
    private struct Wall { public Renderer Renderer; public Vector3 Normal; }
    private readonly List<Wall> _walls=new List<Wall>();
    private ManagerCameraRig _rig;
    private int _frame;
    private static readonly HashSet<string> WindowGroups=new HashSet<string> {
        "Craft_Plaster_A3B3A0","Craft_Walnut_F4DDB3","Craft_Glass_A7D3D5","Craft_Plain_F4DDB3",
        "Craft_Linen_417D75","Craft_Linen_DAB76B","Craft_Brass_CCA75D",
        "Craft_Walnut_C19A58","Craft_Plain_F7E4BE","Craft_Plain_6E9A92"
    };
    private void Start() { _rig=GetComponent<ManagerCameraRig>(); Rebuild(); }
    public void Rebuild()
    {
        _walls.Clear();
        foreach(var room in FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            foreach(var renderer in room.GetComponentsInChildren<MeshRenderer>(true))
                if(WindowGroups.Contains(renderer.name)) _walls.Add(new Wall {Renderer=renderer,Normal=room.transform.position.z>=0 ? Vector3.forward : Vector3.back});
        var world=GameObject.Find("World"); if(world==null)return;
        foreach(var renderer in world.GetComponentsInChildren<MeshRenderer>(true)) {
            Vector3 normal=renderer.name=="Wall_N" ? Vector3.forward : renderer.name=="Wall_S" ? Vector3.back
                : renderer.name=="Wall_E" ? Vector3.right : renderer.name=="Wall_W" ? Vector3.left : Vector3.zero;
            if(normal!=Vector3.zero) _walls.Add(new Wall {Renderer=renderer,Normal=normal});
        }
    }
    private void LateUpdate()
    {
        if(_rig==null)return;
        if(++_frame==5)Rebuild(); // Room expansion finishes during the first update.
        Vector3 towardCamera=transform.position-_rig.FocusPoint;towardCamera.y=0;towardCamera.Normalize();
        foreach(var wall in _walls) if(wall.Renderer!=null)
            wall.Renderer.enabled=Vector3.Dot(wall.Normal,towardCamera)<.12f;
    }
    private void OnDisable() { foreach(var wall in _walls) if(wall.Renderer!=null)wall.Renderer.enabled=true; }
}
