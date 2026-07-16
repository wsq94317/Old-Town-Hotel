using UnityEngine;
using UnityEngine.AI;

// 客人纸片人：只做"走到目标点→(可选跨层)→到达后回调/销毁"的一次性行程。
// 客人不参与楼梯触发器；跨层走到楼梯点后代码瞬移（与员工一致）。
[RequireComponent(typeof(NavMeshAgent))]
public class GuestAgent : MonoBehaviour
{
    private NavMeshAgent _agent;
    private System.Collections.Generic.List<(int from, int to)> _hops;
    private int _hopIndex;
    private Vector3 _finalTarget;
    private bool _traveling;
    private System.Action _onArrived;

    private void Awake() => _agent = GetComponent<NavMeshAgent>();

    public int CurrentFloor => FloorMath.FloorIndexForY(transform.position.y);

    /// <summary>出发去某个世界点（自动跨层）；到达后回调（可为 null）。</summary>
    public void TravelTo(Vector3 worldPos, System.Action onArrived)
    {
        _finalTarget = worldPos;
        _onArrived = onArrived;
        int targetFloor = FloorMath.FloorIndexForY(worldPos.y);
        _hops = FloorNavigator.PlanHops(CurrentFloor, targetFloor);
        _hopIndex = 0;
        _traveling = true;
        _agent.SetDestination(_hops.Count > 0
            ? FloorNavigator.StairPadOf(CurrentFloor)
            : _finalTarget);
    }

    private void Update()
    {
        if (!_traveling) return;
        if (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance + 0.15f) return;

        if (_hops != null && _hopIndex < _hops.Count)
        {
            var hop = _hops[_hopIndex];
            _agent.Warp(FloorNavigator.StairExitOf(hop.to));
            _hopIndex++;
            _agent.SetDestination(_hopIndex < _hops.Count
                ? FloorNavigator.StairPadOf(CurrentFloor)
                : _finalTarget);
            return;
        }

        _traveling = false;
        var cb = _onArrived;
        _onArrived = null;
        cb?.Invoke();
    }

    /// <summary>建一个客人纸片人（黄色）。</summary>
    public static GuestAgent Spawn(Vector3 pos, string label)
    {
        var go = new GameObject("Guest_" + label);
        go.transform.position = pos;
        var nav = go.AddComponent<NavMeshAgent>();
        nav.speed = 2.6f;
        nav.radius = 0.3f;
        nav.height = 1.6f;
        nav.angularSpeed = 720f;
        nav.acceleration = 14f;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Visual";
        quad.transform.SetParent(go.transform);
        quad.transform.localPosition = new Vector3(0, 0.75f, 0);
        quad.transform.localScale = new Vector3(0.65f, 1.4f, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = GuestMaterial();
        quad.AddComponent<BillboardSprite>();

        go.AddComponent<AgentFloorVisibility>();
        return go.AddComponent<GuestAgent>();
    }

    private static Material _mat;
    private static Material GuestMaterial()
    {
        if (_mat == null)
        {
            _mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.95f, 0.8f, 0.25f) };
        }
        return _mat;
    }
}
