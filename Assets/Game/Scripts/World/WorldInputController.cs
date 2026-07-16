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
    [SerializeField] private float dragThresholdPixels = 30f;

    private TapDragClassifier _classifier;
    private Vector2 _lastPos;
    private bool _pressedLastFrame;
    private bool _pressStartedOverUi;
    private Camera _cam;

    private void Awake()
    {
        _classifier = new TapDragClassifier(dragThresholdPixels);
        _cam = Camera.main;
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
        if (cameraRig == null) cameraRig = FindFirstObjectByType<ManagerCameraRig>();
    }

    private void Update()
    {
        (bool pressed, Vector2 pos) = ReadPointer();

        if (pressed && !_pressedLastFrame)
        {
            _pressStartedOverUi = IsOverUi();
            if (!_pressStartedOverUi) _classifier.Press(pos);
            _lastPos = pos;
        }
        else if (pressed && _pressedLastFrame && !_pressStartedOverUi)
        {
            _classifier.Move(pos);
            if (_classifier.IsDragging && cameraRig != null)
            {
                cameraRig.SetPeeking(true);
                cameraRig.ApplyPeekScreenDrag(_lastPos, pos);
            }
            _lastPos = pos;
        }
        else if (!pressed && _pressedLastFrame)
        {
            if (cameraRig != null) cameraRig.SetPeeking(false);
            if (!_pressStartedOverUi)
            {
                var result = _classifier.Release(_lastPos);
                if (result == TapDragClassifier.Result.Tap) HandleTap(_lastPos);
            }
        }

        _pressedLastFrame = pressed;
    }

    private (bool, Vector2) ReadPointer()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return (true, Touchscreen.current.primaryTouch.position.ReadValue());
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return (true, Mouse.current.position.ReadValue());
        return (false, _lastPos);
    }

    private static bool IsOverUi() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private ManagerInteraction _interaction;
    private ComplaintInteraction _complaint;
    private DailyEventInteraction _events;
    private HiringInteraction _hiring;

    private void HandleTap(Vector2 screenPos)
    {
        // OnGUI 决策面板打开时吞掉世界点击（OnGUI 不走 EventSystem）。
        if (_interaction == null) _interaction = FindFirstObjectByType<ManagerInteraction>();
        if (_interaction != null && _interaction.PanelOpen) return;
        if (_complaint == null) _complaint = FindFirstObjectByType<ComplaintInteraction>();
        if (_complaint != null && _complaint.PanelOpen) return;
        if (_events == null) _events = FindFirstObjectByType<DailyEventInteraction>();
        if (_events != null && _events.PanelOpen) return;
        if (_hiring == null) _hiring = FindFirstObjectByType<HiringInteraction>();
        if (_hiring != null && _hiring.PanelOpen) return;

        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
        Ray ray = _cam.ScreenPointToRay(screenPos);
        // 忽略 trigger（楼梯触发盒）；NavBlock 隐形墙在 Ignore Raycast 层，同样不拦射线。
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)) return;

        // 指挥模式：这次点击=指定房间
        if (_interaction != null && _interaction.InCommandMode)
        {
            _interaction.CommandTarget(hit.point);
            return;
        }

        // 点到员工 → 交给监督交互（近=开面板，远=走过去）
        var staff = hit.collider.GetComponentInParent<StaffAgent>();
        if (staff != null)
        {
            var interaction = FindFirstObjectByType<ManagerInteraction>();
            if (interaction != null) { interaction.OnStaffTapped(staff); return; }
        }

        if (manager != null) manager.MoveTo(hit.point);
        ClickMarkerFx.Spawn(hit.point);
    }
}
