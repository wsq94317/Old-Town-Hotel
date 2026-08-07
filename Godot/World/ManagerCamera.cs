using Godot;

// 固定角度的正交相机，带焦点跟随 / 自由拖拽两种模式。
// Unity 侧 ManagerCameraRig 的移植，参数从场景里读出来的实测值：
//   orthographicSize 6、offset (-8,10,-8)、世界边界 X[-10,10] Z[-6,6]、followLerp 6
//
// ══ 两处必须换算，不能照抄数字 ══
//
// 1. 偏移的 z 要翻号：Unity (-8,10,-8) -> Godot (-8,10,+8)。
//    照抄的话相机会跑到酒店的另一侧，而因为是等距视角，画面**看起来仍然像回事**，
//    只是你在看背面——这正是最难自查的那类错。
//
// 2. 正交尺寸要乘 2：Unity 的 orthographicSize 是**半高**，Godot 的 Camera3D.Size
//    是完整高度。直接填 6 会得到两倍的放大。
public partial class ManagerCamera : Camera3D
{
    // Unity: cameraOffset (-8, 10, -8)，z 翻号。
    // 公开出去是因为取景计算需要同一个朝向——两处各写一遍迟早会改岔。
    public static readonly Vector3 ViewOffset = new Vector3(-8f, 10f, 8f);
    private static Vector3 Offset => ViewOffset;
    private const float FollowLerp = 6f;
    private const float UnityOrthoSize = 6f;

    private Vector2 _minXZ = new Vector2(-10f, -6f);
    private Vector2 _maxXZ = new Vector2(10f, 6f);

    private Vector3 _focus;
    private bool _dragging;
    private bool _snapNextFrame = true;
    private Vector2 _lastMouse;

    public Node3D Target { get; set; }
    public bool FollowingTarget { get; private set; } = true;

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = UnityOrthoSize * 2f;     // 半高 -> 全高
        Near = 0.05f;
        Far = 500f;
        Current = true;

        _focus = Target != null ? Clamp(Target.GlobalPosition) : Vector3.Zero;
        ApplyImmediately();
    }

    public void SetWorldBounds(Vector2 minXZ, Vector2 maxXZ)
    {
        _minXZ = new Vector2(Mathf.Min(minXZ.X, maxXZ.X), Mathf.Min(minXZ.Y, maxXZ.Y));
        _maxXZ = new Vector2(Mathf.Max(minXZ.X, maxXZ.X), Mathf.Max(minXZ.Y, maxXZ.Y));
        _focus = Clamp(_focus);
    }

    /// <summary>切层时把焦点抬到该层地面高度并立即吸附，避免一路插值穿过楼板。</summary>
    public void OnFloorChanged(int floorIndex)
    {
        if (FollowingTarget && Target != null) _focus = Clamp(Target.GlobalPosition);
        else _focus.Y = FloorMath.BaseYFor(floorIndex);   // 与 Unity 同一份 FloorMath
        _snapNextFrame = true;
    }

    public void FocusOn(Vector3 worldPoint, bool instant = false)
    {
        _focus = Clamp(worldPoint);
        FollowingTarget = false;
        _dragging = false;
        _snapNextFrame = instant;
    }

    public void ReturnToTarget(bool instant = false)
    {
        if (Target == null) return;
        _focus = Clamp(Target.GlobalPosition);
        FollowingTarget = true;
        _dragging = false;
        _snapNextFrame = instant;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed) { BeginDrag(mb.Position); }
            else { _dragging = false; }
        }
        else if (@event is InputEventMouseMotion mm && _dragging)
        {
            DragTo(mm.Position);
        }
    }

    private void BeginDrag(Vector2 screen)
    {
        // 接管控制权时保持当前取景，否则松手瞬间画面会跳回目标身上。
        if (FollowingTarget)
        {
            _focus = Clamp(GlobalPosition - Offset);
            if (Target != null) _focus.Y = Target.GlobalPosition.Y;
        }
        FollowingTarget = false;
        _dragging = true;
        _lastMouse = screen;
    }

    /// <summary>
    /// 把上一帧和这一帧的屏幕点各投一条射线到「焦点高度的水平面」上，两个交点之差就是
    /// 世界空间的平移量。这样拖动速度和缩放级别自动一致，不需要手调灵敏度系数。
    /// </summary>
    private void DragTo(Vector2 screen)
    {
        if (TryHitFocusPlane(_lastMouse, out Vector3 from) &&
            TryHitFocusPlane(screen, out Vector3 to))
        {
            Vector3 delta = from - to;
            delta.Y = 0f;
            _focus = Clamp(_focus + delta);
        }
        _lastMouse = screen;
    }

    private bool TryHitFocusPlane(Vector2 screen, out Vector3 hit)
    {
        Vector3 origin = ProjectRayOrigin(screen);
        Vector3 dir = ProjectRayNormal(screen);
        var plane = new Plane(Vector3.Up, _focus.Y);
        Variant? result = plane.IntersectsRay(origin, dir);
        if (result.HasValue) { hit = result.Value.AsVector3(); return true; }
        hit = Vector3.Zero;
        return false;
    }

    public override void _Process(double delta)
    {
        if (FollowingTarget && Target != null) _focus = Clamp(Target.GlobalPosition);

        Vector3 desired = _focus + Offset;
        if (_snapNextFrame || _dragging)
        {
            GlobalPosition = desired;
            _snapNextFrame = false;
        }
        else
        {
            GlobalPosition = GlobalPosition.Lerp(desired, (float)delta * FollowLerp);
        }
        LookAt(_focus, Vector3.Up);
    }

    private void ApplyImmediately()
    {
        GlobalPosition = _focus + Offset;
        LookAt(_focus, Vector3.Up);
    }

    private Vector3 Clamp(Vector3 focus)
    {
        focus.X = Mathf.Clamp(focus.X, _minXZ.X, _maxXZ.X);
        focus.Z = Mathf.Clamp(focus.Z, _minXZ.Y, _maxXZ.Y);
        return focus;
    }
}
