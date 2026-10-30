using UnityEngine.EventSystems;
public sealed class RoomStudioPointer:UnityEngine.MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler,IScrollHandler
{
    public WorldManagementHud owner;
    public void OnPointerDown(PointerEventData e)=>owner.StudioPointerDown(e);
    public void OnDrag(PointerEventData e)=>owner.StudioPointerDrag(e);
    public void OnPointerUp(PointerEventData e)=>owner.StudioPointerUp(e);
    public void OnScroll(PointerEventData e)=>owner.StudioScroll(e);
}
