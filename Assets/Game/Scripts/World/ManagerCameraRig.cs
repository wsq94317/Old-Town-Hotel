using UnityEngine;
using UnityEngine.InputSystem;

// 固定 45° 正交跟随相机：平滑跟随经理；单指拖屏=临时窥视偏移（松手回弹）；
// 楼层切换时瞬间跳到新层高度（不插值穿楼板）。
// 编辑器里用鼠标右键拖动模拟窥视（左键留给点击寻路）；真机手势细分 M6 打磨。
public class ManagerCameraRig : MonoBehaviour
{
    [SerializeField] private Transform target;                 // Manager
    [SerializeField] private FloorVisibilityController floors;
    [SerializeField] private float followLerp = 6f;
    [SerializeField] private float peekRadius = 4f;
    [SerializeField] private float peekReturnLerp = 8f;
    [SerializeField] private float dragToWorld = 0.02f;        // 像素→世界系数
    [SerializeField] private Vector3 cameraOffset = new Vector3(-8f, 10f, -8f); // 45° 视角回退量

    private Vector3 _peekOffset;
    private Vector2? _lastDragPos;
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

    private void LateUpdate()
    {
        if (target == null) return;
        UpdatePeek();

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

    private void UpdatePeek()
    {
        Vector2? drag = ReadDrag();
        if (drag.HasValue && _lastDragPos.HasValue)
        {
            Vector2 delta = drag.Value - _lastDragPos.Value;
            // 屏幕拖动映射到相机右/前的水平面（XZ）。
            Vector3 right = transform.right; right.y = 0f; right.Normalize();
            Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
            _peekOffset -= (right * delta.x + fwd * delta.y) * dragToWorld;
            _peekOffset = Vector3.ClampMagnitude(_peekOffset, peekRadius);
        }
        else if (!drag.HasValue)
        {
            _peekOffset = Vector3.Lerp(_peekOffset, Vector3.zero, Time.deltaTime * peekReturnLerp);
        }
        _lastDragPos = drag;
    }

    private Vector2? ReadDrag()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            return Mouse.current.position.ReadValue();
        return null;
    }
}
