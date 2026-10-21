using UnityEngine;

public sealed class HotelPanelReveal : MonoBehaviour
{
    private CanvasGroup _group;private float _elapsed;
    private void OnEnable(){_group=GetComponent<CanvasGroup>();if(_group==null)_group=gameObject.AddComponent<CanvasGroup>();_elapsed=0;_group.alpha=0;}
    private void Update(){_elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(_elapsed/.18f);float eased=1-Mathf.Pow(1-t,3);_group.alpha=eased;transform.localScale=new Vector3(1,.96f+.04f*eased,1);}
    private void OnDisable(){transform.localScale=Vector3.one;}
}
