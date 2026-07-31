using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

// 世界输入路由：一根手指两种语义——
//   Tap（按下→没滑→松手）：经理走到落点 + 绿色点击标记
//   Drag（按下后滑动超阈值）：拖动相机窥视；松手后相机慢速回正到经理
// 一旦进入 Drag，本次按压永久失去 Tap 资格（拖回原点松手也不移动）。
public class WorldInputController : MonoBehaviour
{
    [SerializeField] private ManagerController manager;
    [SerializeField] private ManagerCameraRig cameraRig;
    [SerializeField] private float dragThresholdPixels = 10f;

    private TapDragClassifier _classifier;
    private Vector2 _pressPos;
    private Vector2 _lastPos;
    private bool _pressedLastFrame;
    private bool _pressStartedOverUi;
    private Camera _cam;

    /// <summary>输入管线探针：最近一次 release 的判定链（诊断用，可随时删）。</summary>
    public static string TapDebug = "no input yet";

    private void Awake()
    {
        // 阈值按 DPI 缩放：高分屏上手指点击的自然抖动远超 30 物理像素
        _classifier = new TapDragClassifier(dragThresholdPixels * GuiScale.Factor);
        _cam = Camera.main;
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
        if (cameraRig == null) cameraRig = FindFirstObjectByType<ManagerCameraRig>();
    }

    private float _heldStillSeconds;
    private Vector2 _heldStillPos = new Vector2(-9999f, -9999f);
    private bool _ignoreTouchUntilRelease;

    private void Update()
    {
        (bool pressed, Vector2 pos) = ReadPointer();

        // ── 模拟触屏"卡指"看门狗 ──
        // Device Simulator 偶发吞掉抬起事件：primaryTouch 从此永远 isPressed，
        // 新点击不再产生按下事件，点击+拖动全聋（2026-07-18 实测抓到 phase=Moved 卡死）。
        // 按住且指针纹丝不动超过 3 秒判定卡指：强制取消本次按压（不判 Tap），
        // 并忽略触屏直到它真的报告松开。真人按住 3 秒不动的场景几乎不存在，误伤=白点一下。
        if (pressed)
        {
            if ((pos - _heldStillPos).sqrMagnitude > 25f) { _heldStillPos = pos; _heldStillSeconds = 0f; }
            else _heldStillSeconds += Time.unscaledDeltaTime;
            if (_heldStillSeconds >= 3f)
            {
                _heldStillSeconds = 0f;
                _ignoreTouchUntilRelease = true;
                if (cameraRig != null) cameraRig.EndFreeLook();
                _pressedLastFrame = false;
                TapDebug = "stuck touch force-cancelled";
                return;
            }
        }
        else
        {
            _heldStillSeconds = 0f;
        }

        if (pressed && !_pressedLastFrame)
        {
            // 每次按压重建分类器：热重载后 _classifier 为 null（Awake 不重跑），
            // 顺带跟上转屏/改分辨率后的 GuiScale.Factor（阈值别冻结在 Awake 时刻）
            _classifier = new TapDragClassifier(dragThresholdPixels * GuiScale.Factor);
            // IMGUI 热区（操作台抽屉/顶栏）也算 UI：IsOverUi 只认 UGUI 的 EventSystem，
            // 不加这条的话按住抽屉拖动会转动相机（点穿的拖动版）
            _pressStartedOverImGui = GuiInput.IsInReservedZone(pos);
            _pressStartedOverUi = IsOverUi()
                                  || WorldManagementHud.ContainsScreenPoint(pos)
                                  || _pressStartedOverImGui;
            if (!_pressStartedOverUi) _classifier.Press(pos);
            _pressPos = pos;
            _lastPos = pos;
        }
        else if (pressed && _pressedLastFrame && !_pressStartedOverUi)
        {
            if (_classifier == null) return; // 热重载发生在按压中途：这次按压作废
            bool wasDragging = _classifier.IsDragging;
            _classifier.Move(pos);
            if (_classifier.IsDragging && cameraRig != null)
            {
                if (!wasDragging)
                {
                    cameraRig.BeginFreeLook();
                    cameraRig.ApplyScreenDrag(_pressPos, pos);
                }
                else
                {
                    cameraRig.ApplyScreenDrag(_lastPos, pos);
                }
            }
            _lastPos = pos;
        }
        else if (!pressed && _pressedLastFrame)
        {
            if (cameraRig != null) cameraRig.EndFreeLook();
            if (!_pressStartedOverUi && _classifier != null)
            {
                var result = _classifier.Release(_lastPos);
                TapDebug = $"release@{_lastPos} result={result}";
                if (result == TapDragClassifier.Result.Tap) HandleTap(_lastPos);
            }
            else
            {
                // 从 IMGUI 热区开始的按压：世界不响应，但**点击本身要转发给 GUI**，
                // 否则抽屉按钮在触屏上又聋了（世界面板不再自取触点，全靠这条转发）
                if (_pressStartedOverImGui) GuiInput.PublishTap(_lastPos);
                TapDebug = "release ignored: pressStartedOverUi";
            }
        }

        _pressedLastFrame = pressed;
    }

    private (bool, Vector2) ReadPointer()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            if (!_ignoreTouchUntilRelease)
                return (true, Touchscreen.current.primaryTouch.position.ReadValue());
            // 卡指隔离中：无视这根"僵尸手指"，鼠标通道照常可用
        }
        else
        {
            _ignoreTouchUntilRelease = false; // 触屏真松开了，恢复采信
        }
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return (true, Mouse.current.position.ReadValue());
        return (false, _lastPos);
    }

    private static bool IsOverUi()
    {
        if (EventSystem.current == null) return false;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            int touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            if (touchId >= 0) return EventSystem.current.IsPointerOverGameObject(touchId);
        }

        return Mouse.current != null &&
               Mouse.current.leftButton.isPressed &&
               EventSystem.current.IsPointerOverGameObject();
    }

    private bool _pressStartedOverImGui;

    private ManagerInteraction _interaction;
    private ComplaintInteraction _complaint;
    private DailyEventInteraction _events;
    private HiringInteraction _hiring;
    private FireAlarmIncident _fire;
    private BreakdownSystem _breakdowns;

    private void HandleTap(Vector2 screenPos)
    {
        // OnGUI 面板打开（或点中 HIRE 等常驻热区）时：点击不进世界，
        // 转发给 GuiInput 做按钮命中——纯新 Input System 下 IMGUI 收不到触摸，
        // 触屏设备全靠这条转发通道（编辑器鼠标则两条通道都通）。
        if (_interaction == null) _interaction = FindFirstObjectByType<ManagerInteraction>();
        if (_complaint == null) _complaint = FindFirstObjectByType<ComplaintInteraction>();
        if (_events == null) _events = FindFirstObjectByType<DailyEventInteraction>();
        if (_hiring == null) _hiring = FindFirstObjectByType<HiringInteraction>();
        if (_fire == null) _fire = FindFirstObjectByType<FireAlarmIncident>();
        if (_breakdowns == null) _breakdowns = FindFirstObjectByType<BreakdownSystem>();
        bool legacyWorldModal = !WorldManagementHud.IsActive;
        bool panelOpen =
            // 存档面板：静态查询，因为它是按需自动装的（场景文件里没有这个物体）
            legacyWorldModal && (
                SaveSlotPanel.AnyPanelOpen ||
                (_interaction != null && _interaction.PanelOpen) ||
                (_complaint != null && _complaint.PanelOpen) ||
                (_events != null && _events.PanelOpen) ||
                (_hiring != null && _hiring.PanelOpen) ||
                (_fire != null && _fire.PanelOpen) ||
                (_breakdowns != null && _breakdowns.PanelOpen) ||
                RoomDoor.AnyPanelOpen ||
                (ElevatorController.Instance != null && ElevatorController.Instance.PanelOpen) ||
                (ManagerPhone.Instance != null && ManagerPhone.Instance.PanelOpen));
        if (panelOpen || GuiInput.IsInReservedZone(screenPos))
        {
            GuiInput.PublishTap(screenPos);
            TapDebug += $" -> published(panelOpen={panelOpen})";
            return;
        }
        TapDebug += " -> world";

        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
        Ray ray = _cam.ScreenPointToRay(screenPos);
        // 忽略 trigger（楼梯触发盒）；layerMask 必须排除 Ignore Raycast 层（layer 2）——
        // NavBlock 隐形墙在那层，写 ~0 会把它们重新拉回来拦点击（踩过一次的坑）。
        int mask = ~(1 << 2);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, mask, QueryTriggerInteraction.Ignore)) return;

        float currentFloorY = manager != null
            ? FloorMath.BaseYFor(FloorMath.FloorIndexForY(manager.transform.position.y))
            : FloorMath.BaseYFor(FloorMath.FloorIndexForY(hit.point.y));
        Vector3 movementTarget = TryProjectRayToFloor(ray, currentFloorY, out Vector3 floorPoint)
            ? floorPoint
            : ManagerController.ProjectToCurrentFloor(hit.point, currentFloorY);

        // 指挥模式：这次点击=指定房间
        if (_interaction != null && _interaction.InCommandMode)
        {
            _interaction.CommandTarget(hit.point);
            return;
        }

        // 点到员工 → 交给监督交互（近=开面板，远=走过去）
        var business = hit.collider.GetComponentInParent<HotelBusinessHotspot>();
        if (business != null)
        {
            WorldManagementHud.SelectBusiness(business);
            business.Pulse();
            return;
        }

        var facility = hit.collider.GetComponentInParent<StaffFacilityNode>();
        if (facility != null)
        {
            StaffFacilitySystem.EnsureInScene().OnFacilityTapped(facility, manager);
            ClickMarkerFx.Spawn(facility.Anchor);
            return;
        }

        var staff = hit.collider.GetComponentInParent<StaffAgent>();
        if (staff != null)
        {
            var interaction = FindFirstObjectByType<ManagerInteraction>();
            if (interaction != null) { interaction.OnStaffTapped(staff); return; }
        }

        // Rooms are selected only through their visible doorway chips. Looking
        // up RoomSceneBinder from the hit collider's parents made every wall,
        // bed and tall invisible room box steal nearby corridor taps.
        if (TryFindRoomTap(ray, out RoomSceneBinder room))
        {
            WorldManagementHud.SelectRoom(room.RoomNumber);
            return;
        }

        WorldManagementHud.ClearWorldSelection();
        if (manager != null && manager.TryMoveTo(movementTarget, out Vector3 destination))
        {
            ClickMarkerFx.Spawn(movementTarget);
        }
        else
        {
            ClickMarkerFx.SpawnRejected(movementTarget);
        }
    }

    /// <summary>Selects the nearest visible doorway footprint intersected by the ray.</summary>
    public static bool TryFindRoomTap(Ray ray, out RoomSceneBinder selected)
    {
        selected = null;
        float nearestDistance = float.PositiveInfinity;
        RoomSceneBinder[] rooms = FindObjectsByType<RoomSceneBinder>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomSceneBinder candidate = rooms[i];
            if (candidate == null || candidate.RoomNumber <= 0
                || !candidate.TryGetTapDistance(ray, out float distance)
                || distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            selected = candidate;
        }

        return selected != null;
    }

    /// <summary>
    /// Projects a screen ray onto the manager's current floor. Object colliders
    /// remain useful for selection, but their height cannot move the destination.
    /// </summary>
    public static bool TryProjectRayToFloor(Ray ray, float floorY, out Vector3 point)
    {
        var floor = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        if (floor.Raycast(ray, out float distance) && distance >= 0f)
        {
            point = ray.GetPoint(distance);
            point.y = floorY;
            return true;
        }

        point = default;
        return false;
    }
}
