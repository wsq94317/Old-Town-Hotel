using UnityEngine;
using UnityEngine.AI;

// Lightweight paper-character navigation with cross-floor travel and portal exits.
[RequireComponent(typeof(NavMeshAgent))]
public class GuestAgent : MonoBehaviour
{
    private const float ExitApproachDepth = 0.95f;
    private const float ExitOutsideDepth = 1.6f;
    private const float ExitHalfWidth = 1.35f;
    private const float ExitAnimationDistance = 1.6f;
    private const float ExitAnimationSeconds = 0.35f;

    private NavMeshAgent _agent;
    private System.Collections.Generic.List<(int from, int to)> _hops;
    private int _hopIndex;
    private Vector3 _finalTarget;
    private bool _traveling;
    private System.Action _onArrived;
    private bool _isPortalExit;
    private Vector3 _exitPortal;
    private Vector3 _exitDirection;
    private System.Action _onExited;

    private static readonly System.Collections.Generic.List<GuestAgent> _all =
        new System.Collections.Generic.List<GuestAgent>();

    public static System.Collections.Generic.IReadOnlyList<GuestAgent> All => _all;
    public int CurrentFloor => FloorMath.FloorIndexForY(transform.position.y);

    private void Awake() => _agent = GetComponent<NavMeshAgent>();

    private void OnEnable() => _all.Add(this);

    private void OnDisable() => _all.Remove(this);

    public void TravelTo(Vector3 worldPos, System.Action onArrived)
    {
        _isPortalExit = false;
        _onExited = null;
        BeginTravel(worldPos, onArrived);
    }

    /// <summary>
    /// Walks to a doorway as a portal, then leaves the NavMesh and continues outside.
    /// A portal area is used instead of one exact point so a crowd cannot deadlock the exit.
    /// </summary>
    public void ExitVia(Vector3 portalPoint, System.Action onExited)
    {
        ExitVia(portalPoint, Vector3.back, onExited);
    }

    public void ExitVia(Vector3 portalPoint, Vector3 outwardDirection, System.Action onExited)
    {
        _isPortalExit = true;
        _exitPortal = portalPoint;
        _exitDirection = outwardDirection.sqrMagnitude > 0.001f
            ? outwardDirection.normalized
            : Vector3.back;
        _onExited = onExited;
        BeginTravel(portalPoint, null);
    }

    private void BeginTravel(Vector3 worldPos, System.Action onArrived)
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();

        _finalTarget = NavMesh.SamplePosition(worldPos, out NavMeshHit navHit, 2.5f, NavMesh.AllAreas)
            ? navHit.position
            : worldPos;
        _onArrived = onArrived;
        int targetFloor = FloorMath.FloorIndexForY(_finalTarget.y);
        _hops = FloorNavigator.PlanHops(CurrentFloor, targetFloor);
        _hopIndex = 0;
        _traveling = true;

        Vector3 firstTarget = _hops.Count > 0
            ? FloorNavigator.StairPadOf(CurrentFloor)
            : _finalTarget;

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || !_agent.SetDestination(firstTarget))
        {
            if (_isPortalExit) BeginPortalExit();
            else CompleteTravel();
        }
    }

    private void Update()
    {
        if (!_traveling) return;

        if (_isPortalExit && IsWithinExitPortal(
                transform.position,
                _exitPortal,
                _exitDirection,
                ExitApproachDepth,
                ExitOutsideDepth,
                ExitHalfWidth))
        {
            BeginPortalExit();
            return;
        }

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
        {
            if (_isPortalExit) BeginPortalExit();
            else CompleteTravel();
            return;
        }

        if (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance + 0.15f)
            return;

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

        if (_isPortalExit)
        {
            BeginPortalExit();
            return;
        }

        CompleteTravel();
    }

    private void CompleteTravel()
    {
        _traveling = false;
        var callback = _onArrived;
        _onArrived = null;
        callback?.Invoke();
    }

    private void BeginPortalExit()
    {
        if (!_isPortalExit) return;

        _traveling = false;
        _isPortalExit = false;
        _onArrived = null;
        var callback = _onExited;
        _onExited = null;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.ResetPath();

        StartCoroutine(AnimateOutside(callback));
    }

    private System.Collections.IEnumerator AnimateOutside(System.Action callback)
    {
        if (_agent != null && _agent.enabled)
            _agent.enabled = false;

        Vector3 start = transform.position;
        Vector3 end = start + _exitDirection * ExitAnimationDistance;
        float elapsed = 0f;

        while (elapsed < ExitAnimationSeconds)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(
                start,
                end,
                Mathf.Clamp01(elapsed / ExitAnimationSeconds));
            yield return null;
        }

        callback?.Invoke();
    }

    public static bool IsWithinExitPortal(
        Vector3 position,
        Vector3 portalPoint,
        Vector3 outwardDirection,
        float approachDepth = ExitApproachDepth,
        float outsideDepth = ExitOutsideDepth,
        float halfWidth = ExitHalfWidth)
    {
        Vector3 direction = outwardDirection.sqrMagnitude > 0.001f
            ? outwardDirection.normalized
            : Vector3.back;
        Vector3 delta = position - portalPoint;
        if (Mathf.Abs(delta.y) > 1.5f) return false;

        delta.y = 0f;
        float insideDepth = Vector3.Dot(delta, -direction);
        Vector3 lateral = delta - (-direction * insideDepth);
        return insideDepth <= approachDepth
               && insideDepth >= -outsideDepth
               && lateral.magnitude <= halfWidth;
    }

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
        var quadRenderer = quad.GetComponent<Renderer>();
        quad.transform.localScale = new Vector3(0.65f, 1.4f, 1f);
        if (!GeneratedPlaceholderArt.ApplyGuestSprite(quad.transform, quadRenderer, label))
            quadRenderer.sharedMaterial = GuestMaterial();
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
            {
                color = new Color(0.95f, 0.8f, 0.25f)
            };
        }

        return _mat;
    }
}
