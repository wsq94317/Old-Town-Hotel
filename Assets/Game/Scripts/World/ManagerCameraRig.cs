using UnityEngine;

// 固定 45° 正交跟随相机。输入判定在 WorldInputController，这里只接收窥视增量：
//   窥视中：ApplyPeekDelta 累积偏移（上限 peekRadius）
//   松手后：以较慢的 peekReturnLerp 回正到经理位置（用户要求"回去慢一点"）
//   楼层切换：瞬间跳到新层高度（不插值穿楼板）
public class ManagerCameraRig : MonoBehaviour
{
    [SerializeField] private Transform target;                 // Manager
    [SerializeField] private FloorVisibilityController floors;
    [SerializeField] private float followLerp = 6f;
    [SerializeField] private float peekReturnLerp = 2.5f;      // 回正慢速
    [SerializeField] private float peekHoldSeconds = 1.5f;     // 松手后视角停留时长，之后才开始回正
    [SerializeField] private Vector3 cameraOffset = new Vector3(-8f, 10f, -8f);
    [Tooltip("窥视焦点允许到达的地图 XZ 边界（整层楼可达，但不出图）")]
    [SerializeField] private Vector2 worldMinXZ = new Vector2(-10f, -6f);
    [SerializeField] private Vector2 worldMaxXZ = new Vector2(10f, 6f);

    private Camera _cam;

    private Vector3 _peekOffset;
    private bool _peeking;
    private bool _snapNextFrame;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (floors == null) floors = FindFirstObjectByType<FloorVisibilityController>();
        if (floors != null) floors.OnFloorChanged += HandleFloorChanged;
    }

    private void OnDestroy()
    {
        if (floors != null) floors.OnFloorChanged -= HandleFloorChanged;
    }

    private void HandleFloorChanged(int _) => _snapNextFrame = true;

    /// <summary>
    /// 跟手窥视：把手指前后两个屏幕点投射到当前楼层地面平面，用世界差值平移——
    /// 手指按住的地面点始终留在手指下，与分辨率/渲染倍率无关。
    /// </summary>
    public void ApplyPeekScreenDrag(Vector2 fromScreen, Vector2 toScreen)
    {
        if (_cam == null || target == null) return;
        var plane = new Plane(Vector3.up, new Vector3(0f, target.position.y, 0f));
        Ray r1 = _cam.ScreenPointToRay(fromScreen);
        Ray r2 = _cam.ScreenPointToRay(toScreen);
        if (!plane.Raycast(r1, out float d1) || !plane.Raycast(r2, out float d2)) return;

        Vector3 delta = r1.GetPoint(d1) - r2.GetPoint(d2); // 世界随手指反向移动
        delta.y = 0f;
        _peekOffset += delta;

        // 焦点钳制到地图边界：整层楼可达，但不出图。
        Vector3 focus = target.position + _peekOffset;
        focus.x = Mathf.Clamp(focus.x, worldMinXZ.x, worldMaxXZ.x);
        focus.z = Mathf.Clamp(focus.z, worldMinXZ.y, worldMaxXZ.y);
        _peekOffset = focus - target.position;
    }

    private float _holdUntil;

    /// <summary>是否处于窥视中；结束窥视后先停留 peekHoldSeconds，再慢速回正到经理。</summary>
    public void SetPeeking(bool peeking)
    {
        if (_peeking && !peeking)
        {
            _holdUntil = Time.time + peekHoldSeconds; // 松手瞬间起算停留窗口
        }
        _peeking = peeking;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (!_peeking && Time.time >= _holdUntil)
        {
            _peekOffset = Vector3.Lerp(_peekOffset, Vector3.zero, Time.deltaTime * peekReturnLerp);
        }

        Vector3 desired = target.position + cameraOffset + _peekOffset;
        if (_snapNextFrame || _peeking)
        {
            // 切层瞬跳；窥视拖动中直接贴合（插值会让手指下的地面点漂移，不跟手）。
            transform.position = desired;
            _snapNextFrame = false;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * followLerp);
        }
    }
}
