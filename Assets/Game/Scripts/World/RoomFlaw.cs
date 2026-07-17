using UnityEngine;

// 打扫瑕疵标记：挂在房间锚点上；经理同层可见（棕色小色块占位）。
// INSP 漏检后瑕疵存活到 Ready——经理走近可"打回重扫"（挑毛病玩法）。
public class RoomFlaw : MonoBehaviour
{
    public StaffMember OriginCleaner { get; private set; }

    private GameObject _visual;
    private FloorVisibilityController _floors;
    private static Material _flawMat; // 颜色固定，静态共享——每个瑕疵 new 一份会泄漏

    public static RoomFlaw Add(Room2DEntity room, StaffMember cleaner)
    {
        if (room == null) return null;
        var existing = room.GetComponent<RoomFlaw>();
        if (existing != null) return existing;
        var flaw = room.gameObject.AddComponent<RoomFlaw>();
        flaw.OriginCleaner = cleaner;
        flaw.BuildVisual();
        return flaw;
    }

    public static RoomFlaw Get(Room2DEntity room) =>
        room != null ? room.GetComponent<RoomFlaw>() : null;

    public static void Clear(Room2DEntity room)
    {
        var flaw = Get(room);
        if (flaw != null)
        {
            if (flaw._visual != null) Destroy(flaw._visual);
            Destroy(flaw);
        }
    }

    private void BuildVisual()
    {
        _floors = FindFirstObjectByType<FloorVisibilityController>();
        _visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(_visual.GetComponent<Collider>());
        _visual.name = "FlawMark";
        _visual.transform.SetParent(transform);
        _visual.transform.localPosition = new Vector3(-0.9f, 0.05f, -0.9f); // 房间角落污渍
        _visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        _visual.transform.localScale = Vector3.one * 0.55f;
        if (_flawMat == null)
            _flawMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.45f, 0.3f, 0.15f) };
        _visual.GetComponent<Renderer>().sharedMaterial = _flawMat;
    }

    private void LateUpdate()
    {
        if (_visual == null || _floors == null) return;
        _visual.GetComponent<Renderer>().enabled =
            FloorMath.FloorIndexForY(transform.position.y) == _floors.CurrentFloor;
    }
}
