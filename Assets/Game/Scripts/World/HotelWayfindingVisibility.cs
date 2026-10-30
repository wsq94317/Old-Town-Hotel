using UnityEngine;

/// <summary>Floor-wide ground lettering belongs in overview, not across a close-up room interior.</summary>
public sealed class HotelWayfindingVisibility:MonoBehaviour
{
    private Renderer _renderer;private ManagerCameraRig _rig;
    public static void Install()
    {
        var world=GameObject.Find("World");if(world==null)return;
        foreach(var text in world.GetComponentsInChildren<TextMesh>(true)){
            bool groundSign=false;for(var p=text.transform.parent;p!=null&&p!=world.transform;p=p.parent)if(p.name=="CourtyardLayout_v1"||p.name=="CourtyardGround"){groundSign=true;break;}
            if(groundSign&&text.GetComponent<HotelWayfindingVisibility>()==null)text.gameObject.AddComponent<HotelWayfindingVisibility>();
        }
    }
    private void LateUpdate()
    {
        if(_renderer==null)_renderer=GetComponent<Renderer>();if(_rig==null)_rig=Object.FindFirstObjectByType<ManagerCameraRig>();
        if(_renderer!=null)_renderer.enabled=_rig==null||_rig.Zoom>=12;
    }
}
