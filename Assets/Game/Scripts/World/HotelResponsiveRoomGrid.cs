using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public sealed class HotelResponsiveRoomGrid : MonoBehaviour
{
    private void OnEnable()=>Resize();
    private void OnRectTransformDimensionsChange()=>Resize();
    private void Resize()
    {
        var grid=GetComponent<GridLayoutGroup>();var rect=(RectTransform)transform;
        if(grid==null||rect.rect.width<1||rect.rect.height<1)return;
        grid.cellSize=new Vector2(Mathf.Max(20,(rect.rect.width-20)/5),Mathf.Max(20,Mathf.Min(54,(rect.rect.height-45)/10)));
    }
}
