using UnityEngine;
using UnityEngine.AI;

// 经理寻路执行者：输入判定在 WorldInputController（tap/drag 分离），
// 这里只负责"走到那个世界坐标"。
[RequireComponent(typeof(NavMeshAgent))]
public class ManagerController : MonoBehaviour
{
    [SerializeField] private float sampleMaxDistance = 2f;

    private NavMeshAgent _agent;

    /// <summary>是否在寻路途中（相机/演出判断用）。</summary>
    public bool IsMoving => _agent != null && _agent.hasPath && _agent.remainingDistance > _agent.stoppingDistance;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
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
