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
    [SerializeField] private float peekRadius = 4f;
    [SerializeField] private float peekReturnLerp = 2.5f;      // 回正慢速
    [SerializeField] private float dragToWorld = 0.02f;        // 像素→世界系数
    [SerializeField] private Vector3 cameraOffset = new Vector3(-8f, 10f, -8f);

    private Vector3 _peekOffset;
    private bool _peeking;
    private bool _snapNextFrame;

    private void Awake()
    {
        if (floors == null) floors = FindFirstObjectByType<FloorVisibilityController>();
        if (floors != null) floors.OnFloorChanged += HandleFloorChanged;
    }

    private void OnDestroy()
    {
        if (floors != null) floors.OnFloorChanged -= HandleFloorChanged;
    }

    private void HandleFloorChanged(int _) => _snapNextFrame = true;

    /// <summary>窥视增量（屏幕像素），由 WorldInputController 在拖动时喂入。</summary>
    public void ApplyPeekDelta(Vector2 pixelDelta)
    {
        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        _peekOffset -= (right * pixelDelta.x + fwd * pixelDelta.y) * dragToWorld;
        _peekOffset = Vector3.ClampMagnitude(_peekOffset, peekRadius);
    }

    /// <summary>是否处于窥视中；false 时偏移慢速回零（回正到经理）。</summary>
    public void SetPeeking(bool peeking) => _peeking = peeking;

    private void LateUpdate()
    {
        if (target == null) return;

        if (!_peeking)
        {
            _peekOffset = Vector3.Lerp(_peekOffset, Vector3.zero, Time.deltaTime * peekReturnLerp);
        }

        Vector3 desired = target.position + cameraOffset + _peekOffset;
        if (_snapNextFrame)
        {
            transform.position = desired;
            _snapNextFrame = false;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * followLerp);
        }
    }
}
