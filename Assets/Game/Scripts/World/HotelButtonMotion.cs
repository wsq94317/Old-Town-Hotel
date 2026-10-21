using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HotelButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private bool _pressed;
    public void OnPointerDown(PointerEventData e)=>_pressed=true;
    public void OnPointerUp(PointerEventData e)=>_pressed=false;
    public void OnPointerExit(PointerEventData e)=>_pressed=false;
    private void OnDisable(){_pressed=false;transform.localScale=Vector3.one;}
    private void Update()=>transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*(_pressed?.96f:1f),Time.unscaledDeltaTime*20);
}
