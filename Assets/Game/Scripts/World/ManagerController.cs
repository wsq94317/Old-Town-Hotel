using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ManagerController : MonoBehaviour
{
    private const float MaxPlayerCommandSnapDistance = 0.75f;

    [SerializeField] private float sampleMaxDistance = 2f;

    private NavMeshAgent _agent;
    private NavMeshPath _path;

    public bool IsMoving =>
        _agent != null &&
        _agent.enabled &&
        _agent.isOnNavMesh &&
        _agent.hasPath &&
        _agent.remainingDistance > _agent.stoppingDistance;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _path = new NavMeshPath();
    }

    public void MoveTo(Vector3 worldPos)
    {
        TryMoveTo(worldPos, out _);
    }

    public bool TryMoveTo(Vector3 worldPos, out Vector3 resolvedDestination)
    {
        resolvedDestination = ProjectToCurrentFloor(worldPos, transform.position.y);
        EnsureRuntimeReferences();
        if (_agent == null || !_agent.enabled || !EnsureAgentOnNavMesh())
        {
            return false;
        }

        if (!NavMesh.SamplePosition(
                resolvedDestination,
                out NavMeshHit navHit,
                Mathf.Min(sampleMaxDistance, MaxPlayerCommandSnapDistance),
                _agent.areaMask))
        {
            return false;
        }

        if (!IsAcceptableCommandSnap(
                resolvedDestination,
                navHit.position,
                MaxPlayerCommandSnapDistance))
        {
            return false;
        }

        resolvedDestination = navHit.position;
        if (!_agent.CalculatePath(resolvedDestination, _path) ||
            _path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        // Direct player commands recover from interrupted door/elevator routines.
        _agent.isStopped = false;
        return _agent.SetDestination(resolvedDestination);
    }

    public static bool IsAcceptableCommandSnap(
        Vector3 requested,
        Vector3 resolved,
        float maxDistance)
    {
        requested.y = 0f;
        resolved.y = 0f;
        return Vector3.Distance(requested, resolved) <= Mathf.Max(0f, maxDistance);
    }

    public static Vector3 ProjectToCurrentFloor(Vector3 worldPos, float managerY)
    {
        worldPos.y = FloorMath.BaseYFor(FloorMath.FloorIndexForY(managerY));
        return worldPos;
    }

    private bool EnsureAgentOnNavMesh()
    {
        if (_agent.isOnNavMesh) return true;

        Vector3 recoveryPoint = ProjectToCurrentFloor(transform.position, transform.position.y);
        if (!NavMesh.SamplePosition(
                recoveryPoint,
                out NavMeshHit navHit,
                sampleMaxDistance,
                _agent.areaMask))
        {
            return false;
        }

        return _agent.Warp(navHit.position);
    }

    private void EnsureRuntimeReferences()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (_path == null) _path = new NavMeshPath();
    }
}
