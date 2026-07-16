using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

// 经理点击寻路：点地面 raycast → NavMesh 采样 → SetDestination。
// 项目 activeInputHandler=1（纯新 Input System），旧 Input.* 不可用。
[RequireComponent(typeof(NavMeshAgent))]
public class ManagerController : MonoBehaviour
{
    [SerializeField] private float sampleMaxDistance = 2f;

    private NavMeshAgent _agent;
    private Camera _cam;

    /// <summary>是否在寻路途中（相机/演出判断用）。</summary>
    public bool IsMoving => _agent != null && _agent.hasPath && _agent.remainingDistance > _agent.stoppingDistance;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _cam = Camera.main;
    }

    private void Update()
    {
        Vector2? tap = ReadTap();
        if (tap.HasValue) TryMoveToScreenPoint(tap.Value);
    }

    private Vector2? ReadTap()
    {
        // UI 上的点击不当作移动指令。
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return Mouse.current.position.ReadValue();
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        return null;
    }

    private void TryMoveToScreenPoint(Vector2 screenPos)
    {
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
        Ray ray = _cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;
        MoveTo(hit.point);
    }

    /// <summary>寻路到世界坐标（先 NavMesh 采样，采不到就忽略本次指令）。</summary>
    public void MoveTo(Vector3 worldPos)
    {
        if (_agent == null) return;
        if (NavMesh.SamplePosition(worldPos, out NavMeshHit navHit, sampleMaxDistance, NavMesh.AllAreas))
        {
            _agent.SetDestination(navHit.position);
        }
    }
}
