using UnityEngine;

public sealed class HotelMapMarkerPulse : MonoBehaviour
{
    public bool Selected;
    private CanvasGroup _group;
    private float _celebratingUntil;
    private void Awake() { _group=gameObject.AddComponent<CanvasGroup>(); _group.blocksRaycasts=false; }
    public void Celebrate() { _celebratingUntil=Time.unscaledTime+2.4f; }
    private void Update()
    {
        bool celebrating=Time.unscaledTime<_celebratingUntil;
        _group.alpha=Selected || celebrating ? .65f+.35f*Mathf.Sin(Time.unscaledTime*4) : 0;
        transform.localScale=Vector3.one*(1+(celebrating ? .12f : .025f)*Mathf.Sin(Time.unscaledTime*4));
    }
}
