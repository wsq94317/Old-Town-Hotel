using UnityEngine;
using UnityEngine.AI;

// 员工纸片人：Idle → Traverse(跨层 hops) → WalkTo(房间) → Working(工期) → Idle。
// 房态转换只调用 Room2DEntity 的 guard 方法（StartCleaning/FinishCleaning/
// ApproveInspection）——规则零重写；guard 失败即放弃任务（房态已被别人改变）。
[RequireComponent(typeof(NavMeshAgent))]
public class StaffAgent : MonoBehaviour
{
    public enum AgentState { Idle, Traverse, WalkToRoom, Working }

    [SerializeField] private float cleanSeconds = 5f;
    [SerializeField] private float inspectSeconds = 4f;

    private NavMeshAgent _agent;
    private AgentState _state = AgentState.Idle;
    private StaffTask _task;
    private bool _hasTask;
    private System.Collections.Generic.List<(int from, int to)> _hops;
    private int _hopIndex;
    private float _workTimer;

    public StaffMember Member { get; private set; }
    public AgentState State => _state;
    public bool IsIdle => _state == AgentState.Idle;

    /// <summary>任务结束（完成或放弃）——TaskDispatcher 订阅以释放 claim。</summary>
    public event System.Action<StaffAgent, Room2DEntity> OnTaskFinished;

    public void Init(StaffMember member) => Member = member;

    private void Awake() => _agent = GetComponent<NavMeshAgent>();

    public int CurrentFloor => FloorMath.FloorIndexForY(transform.position.y);

    /// <summary>接受任务：规划跨层 hops 后出发。仅 Idle 时可接。</summary>
    public bool AssignTask(StaffTask task)
    {
        if (_state != AgentState.Idle || task.Room == null) return false;
        _task = task;
        _hasTask = true;
        int targetFloor = FloorMath.FloorIndexForY(task.Room.transform.position.y);
        _hops = FloorNavigator.PlanHops(CurrentFloor, targetFloor);
        _hopIndex = 0;
        if (_hops.Count > 0)
        {
            _state = AgentState.Traverse;
            _agent.SetDestination(FloorNavigator.StairPadOf(CurrentFloor));
        }
        else
        {
            _state = AgentState.WalkToRoom;
            _agent.SetDestination(task.Room.transform.position);
        }
        return true;
    }

    private void Update()
    {
        switch (_state)
        {
            case AgentState.Traverse: TickTraverse(); break;
            case AgentState.WalkToRoom: TickWalk(); break;
            case AgentState.Working: TickWork(); break;
        }
    }

    private bool Arrived() =>
        !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.15f;

    private void TickTraverse()
    {
        if (!Arrived()) return;
        // 到达本层楼梯 → 瞬移到下一层出口
        var hop = _hops[_hopIndex];
        _agent.Warp(FloorNavigator.StairExitOf(hop.to));
        _hopIndex++;
        if (_hopIndex < _hops.Count)
        {
            _agent.SetDestination(FloorNavigator.StairPadOf(CurrentFloor));
        }
        else
        {
            _state = AgentState.WalkToRoom;
            _agent.SetDestination(_task.Room.transform.position);
        }
    }

    private void TickWalk()
    {
        if (!Arrived()) return;
        // 到房：Clean 需先通过 StartCleaning guard；Inspect 直接开始计时。
        if (_task.Kind == StaffTaskKind.Clean && !_task.Room.StartCleaning())
        {
            FinishTask(); // 房态已变（比如老板亲自扫了），放弃
            return;
        }
        if (_task.Kind == StaffTaskKind.Inspect && !_task.Room.CanApproveInspection())
        {
            FinishTask();
            return;
        }
        _workTimer = 0f;
        _state = AgentState.Working;
    }

    private void TickWork()
    {
        _workTimer += Time.deltaTime;
        float duration = _task.Kind == StaffTaskKind.Clean ? cleanSeconds : inspectSeconds;
        if (_workTimer < duration) return;

        if (_task.Kind == StaffTaskKind.Clean)
        {
            if (_task.Room.CanFinishCleaning()) _task.Room.FinishCleaning();
        }
        else
        {
            if (_task.Room.CanApproveInspection()) _task.Room.ApproveInspection();
        }
        FinishTask();
    }

    private void FinishTask()
    {
        var room = _hasTask ? _task.Room : null;
        _hasTask = false;
        _state = AgentState.Idle;
        _agent.ResetPath();
        OnTaskFinished?.Invoke(this, room);
    }
}
