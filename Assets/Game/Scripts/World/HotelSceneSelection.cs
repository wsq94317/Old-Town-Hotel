using UnityEngine;

public sealed class HotelSceneSelection : MonoBehaviour
{
    private LineRenderer _line;
    private RoomSceneBinder _room;
    private int _selected=-1;
    private ManagerCameraRig _rig;
    private void Start()
    {
        _rig=GetComponent<ManagerCameraRig>();
        var root=new GameObject("SelectedRoomOutline");root.layer=2;root.transform.SetParent(transform,false);
        _line=root.AddComponent<LineRenderer>();_line.useWorldSpace=true;_line.loop=true;_line.positionCount=4;
        _line.sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(1,.75f,.3f)};
        _line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;_line.receiveShadows=false;
    }
    private void LateUpdate()
    {
        if(_line==null)return;
        if(_selected!=RoomSelection.Selected) {
            _selected=RoomSelection.Selected;_room=null;
            foreach(var binder in FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(binder.RoomNumber==_selected){_room=binder;break;}
        }
        bool visible=_room!=null&&(_rig==null||FloorMath.FloorIndexForY(_room.transform.position.y)==_rig.ViewedFloor);
        _line.enabled=visible;if(!visible)return;
        var centre=_room.transform.position+Vector3.up*.09f;
        _line.SetPositions(new[]{centre+new Vector3(-2.34f,0,-2.34f),centre+new Vector3(-2.34f,0,2.34f),centre+new Vector3(2.34f,0,2.34f),centre+new Vector3(2.34f,0,-2.34f)});
        _line.startWidth=_line.endWidth=.045f+.009f*Mathf.Sin(Time.unscaledTime*4);
    }
    private void OnDestroy() { if(_line!=null){Destroy(_line.sharedMaterial);Destroy(_line.gameObject);} }
}
