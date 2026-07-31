using UnityEngine;

// Fixed-angle orthographic camera with explicit Follow and FreeLook modes.
// Dragging enters FreeLook; releasing the pointer keeps the current world focus.
[DefaultExecutionOrder(100)]
public class ManagerCameraRig : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private FloorVisibilityController floors;
    [SerializeField] private float followLerp = 6f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(-8f, 10f, -8f);
    [SerializeField] private Vector2 worldMinXZ = new Vector2(-10f, -6f);
    [SerializeField] private Vector2 worldMaxXZ = new Vector2(10f, 6f);

    private Camera _cam;
    private Vector3 _focusPoint;
    private bool _focusInitialized;
    private bool _followingTarget = true;
    private bool _dragging;
    private bool _snapNextFrame;

    public bool IsFollowingTarget => _followingTarget;
    public Vector3 FocusPoint => _focusPoint;

    public void SetWorldBounds(Vector2 minXZ, Vector2 maxXZ)
    {
        worldMinXZ = Vector2.Min(minXZ, maxXZ);
        worldMaxXZ = Vector2.Max(minXZ, maxXZ);
        if (_focusInitialized) _focusPoint = ClampFocus(_focusPoint);
    }

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (floors == null) floors = FindFirstObjectByType<FloorVisibilityController>();
        if (floors != null) floors.OnFloorChanged += HandleFloorChanged;
    }

    private void Start()
    {
        EnsureFocusInitialized();
        _snapNextFrame = true;
    }

    private void OnDestroy()
    {
        if (floors != null) floors.OnFloorChanged -= HandleFloorChanged;
    }

    private void HandleFloorChanged(int floorIndex)
    {
        EnsureFocusInitialized();
        if (_followingTarget && target != null)
        {
            _focusPoint = ClampFocus(target.position);
        }
        else
        {
            _focusPoint.y = FloorMath.BaseYFor(floorIndex);
        }

        _snapNextFrame = true;
    }

    public void BeginFreeLook()
    {
        EnsureFocusInitialized();
        if (_followingTarget)
        {
            // Preserve the exact framing when control changes hands.
            _focusPoint = transform.position - cameraOffset;
            if (target != null) _focusPoint.y = target.position.y;
            _focusPoint = ClampFocus(_focusPoint);
        }

        _followingTarget = false;
        _dragging = true;
    }

    public void EndFreeLook()
    {
        _dragging = false;
    }

    public void ApplyScreenDrag(Vector2 fromScreen, Vector2 toScreen)
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        if (_cam == null) return;

        EnsureFocusInitialized();
        var plane = new Plane(Vector3.up, new Vector3(0f, _focusPoint.y, 0f));
        Ray fromRay = _cam.ScreenPointToRay(fromScreen);
        Ray toRay = _cam.ScreenPointToRay(toScreen);
        if (!plane.Raycast(fromRay, out float fromDistance) ||
            !plane.Raycast(toRay, out float toDistance))
        {
            return;
        }

        Vector3 delta = fromRay.GetPoint(fromDistance) - toRay.GetPoint(toDistance);
        delta.y = 0f;
        _focusPoint = ClampFocus(_focusPoint + delta);
    }

    // Hook this up to a future "locate manager" button or another explicit action.
    public void ReturnToTarget(bool instant = false)
    {
        if (target == null) return;

        _focusPoint = ClampFocus(target.position);
        _focusInitialized = true;
        _followingTarget = true;
        _dragging = false;
        _snapNextFrame = instant;
    }

    public void FocusOnPoint(Vector3 worldPoint, bool instant = false)
    {
        EnsureFocusInitialized();
        _focusPoint = ClampFocus(worldPoint);
        _focusInitialized = true;
        _followingTarget = false;
        _dragging = false;
        _snapNextFrame = instant;
    }

    private void LateUpdate()
    {
        EnsureFocusInitialized();
        if (!_focusInitialized) return;

        if (_followingTarget && target != null)
        {
            _focusPoint = ClampFocus(target.position);
        }

        Vector3 desired = _focusPoint + cameraOffset;
        if (_snapNextFrame || _dragging)
        {
            transform.position = desired;
            _snapNextFrame = false;
        }
        else
        {
            transform.position = Vector3.Lerp(
                transform.position,
                desired,
                Time.deltaTime * followLerp);
        }
    }

    private void EnsureFocusInitialized()
    {
        if (_focusInitialized) return;

        _focusPoint = target != null
            ? ClampFocus(target.position)
            : ClampFocus(transform.position - cameraOffset);
        _focusInitialized = true;
    }

    private Vector3 ClampFocus(Vector3 focus)
    {
        focus.x = Mathf.Clamp(focus.x, worldMinXZ.x, worldMaxXZ.x);
        focus.z = Mathf.Clamp(focus.z, worldMinXZ.y, worldMaxXZ.y);
        return focus;
    }
}
