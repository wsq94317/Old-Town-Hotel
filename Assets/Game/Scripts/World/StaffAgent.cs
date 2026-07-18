using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = System.Random;

[RequireComponent(typeof(NavMeshAgent))]
public class StaffAgent : MonoBehaviour
{
    public enum AgentState
    {
        Idle,
        Traverse,
        WalkToRoom,
        Working
    }

    [SerializeField] private float cleanSeconds = 5f;
    [SerializeField] private float inspectSeconds = 4f;

    private NavMeshAgent _agent;
    private AgentState _state = AgentState.Idle;
    private StaffTask _task;
    private bool _hasTask;
    private List<(int from, int to)> _hops;
    private int _hopIndex;
    private float _workTimer;
    private float _wallTimer;
    private float _speedBuffUntil;
    private float _baseSpeed;

    private SlackFsm _slack;
    private Random _rng;
    private ManagerController _manager;
    private EmoteBubble _emote;
    private Vector3 _idleAnchor;
    private bool _hasIdleAnchor;
    private bool _returningToIdle;

    public StaffMember Member { get; private set; }
    public AgentState State => _state;
    public bool IsIdle => _state == AgentState.Idle;
    public bool HasDelayMark { get; private set; }
    public bool IsSlacking => _slack != null && _slack.Current == SlackFsm.State.Slacking;

    private float _grudgeUntil;
    public bool IsGrudging => Time.time < _grudgeUntil;
    public Room2DEntity CurrentTaskRoom => _hasTask ? _task.Room : null;

    public event Action<StaffAgent, Room2DEntity> OnTaskFinished;
    public static event Action<StaffAgent> OnAnyCaught;

    public void Init(StaffMember member, Random rng)
    {
        Member = member;
        _rng = rng ?? new Random();
        if (member != null && (member.Role == StaffRole.Housekeeper || member.Role == StaffRole.Inspector))
        {
            _slack = new SlackFsm(_rng, member.HasTrait(StaffTrait.Lazy), () => member.Morale);
            _slack.OnCaught += HandleCaught;
        }

        _emote = EmoteBubble.Attach(transform);
    }

    public void SetIdleAnchor(Vector3 worldPosition)
    {
        _idleAnchor = worldPosition;
        _hasIdleAnchor = true;
        if (_state == AgentState.Idle)
            ReturnToIdleAnchor();
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _manager = FindFirstObjectByType<ManagerController>();
        _baseSpeed = 3f;
    }

    public int CurrentFloor => FloorMath.FloorIndexForY(transform.position.y);

    public bool AssignTask(StaffTask task)
    {
        if (_state != AgentState.Idle || task.Room == null) return false;
        if (IsGrudging) return false;

        _returningToIdle = false;
        _task = task;
        _hasTask = true;
        _slack?.ResetShiftRecord();
        HasDelayMark = false;

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
        if (_state != AgentState.Idle && _task.Room == null)
        {
            FinishTask();
            return;
        }

        if (_agent != null)
            _agent.speed = Time.time < _speedBuffUntil ? _baseSpeed * 1.35f : _baseSpeed;

        switch (_state)
        {
            case AgentState.Traverse:
                TickTraverse();
                break;
            case AgentState.WalkToRoom:
                TickWalk();
                break;
            case AgentState.Working:
                TickWork();
                break;
        }

        if (_state == AgentState.Idle && _returningToIdle)
            TickIdleReturn();

        UpdateEmote();
    }

    private bool Arrived() =>
        !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.15f;

    private void TickTraverse()
    {
        if (!Arrived()) return;

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

        if (_task.Kind == StaffTaskKind.Clean && !_task.Room.StartCleaning())
        {
            FinishTask();
            return;
        }

        if (_task.Kind == StaffTaskKind.Inspect && !_task.Room.CanApproveInspection())
        {
            FinishTask();
            return;
        }

        _workTimer = 0f;
        _wallTimer = 0f;
        _state = AgentState.Working;
    }

    private void TickWork()
    {
        float duration = _task.Kind == StaffTaskKind.Clean ? cleanSeconds : inspectSeconds;

        bool productive = true;
        if (_slack != null && _manager != null)
        {
            int managerFloor = FloorMath.FloorIndexForY(_manager.transform.position.y);
            bool onFloor = managerFloor == CurrentFloor;
            Vector3 delta = _manager.transform.position - transform.position;
            delta.y = 0f;
            bool near = onFloor && delta.magnitude < SupervisionTuning.CatchRadius;
            _slack.Tick(Time.deltaTime, onFloor, near);
            productive = _slack.IsProductive;
        }

        _wallTimer += Time.deltaTime;
        if (productive) _workTimer += Time.deltaTime;

        if (!HasDelayMark && _wallTimer > duration * SupervisionTuning.DelayMarkThresholdMultiplier)
            HasDelayMark = true;

        if (_workTimer < duration) return;

        if (_task.Kind == StaffTaskKind.Clean)
        {
            if (_task.Room.CanFinishCleaning())
            {
                _task.Room.FinishCleaning();
                if (Member != null && FlawPolicy.RollFlaw(Member.Attributes.Quality, _rng))
                    RoomFlaw.Add(_task.Room, Member);
            }
        }
        else
        {
            if (_task.Room.CanApproveInspection())
            {
                var flaw = RoomFlaw.Get(_task.Room);
                if (flaw != null && Member != null
                    && !FlawPolicy.RollInspectorMiss(Member.Attributes.Quality, _rng))
                {
                    RoomFlaw.Clear(_task.Room);
                }

                _task.Room.ApproveInspection();
            }
        }

        FinishTask();
    }

    private void FinishTask()
    {
        var room = _hasTask ? _task.Room : null;
        _hasTask = false;
        _state = AgentState.Idle;
        HasDelayMark = false;
        _agent.ResetPath();
        ReturnToIdleAnchor();
        OnTaskFinished?.Invoke(this, room);
    }

    private void HandleCaught() => OnAnyCaught?.Invoke(this);

    public void ApplyCatchChoice(CatchChoice choice)
    {
        var outcome = CatchResolutionLogic.Resolve(choice, Member);
        Member?.AdjustMorale(outcome.MoraleDelta);
        if (outcome.SpeedBuff) _speedBuffUntil = Time.time + 20f;

        if (outcome.GrudgeTriggered)
        {
            _grudgeUntil = Time.time + 30f;
            AbortTask();
            FloatingTextFx.Spawn(transform.position, "HMPH!", new Color(0.85f, 0.2f, 0.55f));
        }

        _slack?.ResetShiftRecord();
        HasDelayMark = false;
    }

    public void AbortTask()
    {
        if (_state == AgentState.Idle) return;

        if (_state == AgentState.Working && _hasTask
            && _task.Kind == StaffTaskKind.Clean
            && _task.Room != null
            && _task.Room.currentState == Room2DState.Cleaning)
        {
            _task.Room.SetState(Room2DState.Dirty);
        }

        FinishTask();
    }

    public InterrogationVerdict Interrogate()
    {
        var verdict = InterrogateLogic.Verdict(_slack != null && _slack.HasRecentSlackRecord);
        if (verdict == InterrogationVerdict.WrongAccusation)
        {
            Member?.AdjustMorale(SupervisionTuning.WrongAccusationMoraleDelta);
            HasDelayMark = false;
        }

        return verdict;
    }

    public void Hurry()
    {
        _speedBuffUntil = Time.time + 15f;
        Member?.AdjustMorale(-3);
    }

    private void UpdateEmote()
    {
        if (_emote == null) return;
        if (IsSlacking) _emote.Show(EmoteBubble.Emote.Sleep);
        else if (IsGrudging) _emote.Show(EmoteBubble.Emote.Grudge);
        else if (HasDelayMark) _emote.Show(EmoteBubble.Emote.Delay);
        else _emote.Show(EmoteBubble.Emote.None);
    }

    private void ReturnToIdleAnchor()
    {
        if (!_hasIdleAnchor || _agent == null || !_agent.isOnNavMesh) return;

        _returningToIdle = true;
        _hops = FloorNavigator.PlanHops(CurrentFloor, FloorMath.FloorIndexForY(_idleAnchor.y));
        _hopIndex = 0;

        if (_hops != null && _hops.Count > 0)
            _agent.SetDestination(FloorNavigator.StairPadOf(CurrentFloor));
        else
            _agent.SetDestination(_idleAnchor);
    }

    private void TickIdleReturn()
    {
        if (!Arrived()) return;

        if (_hops != null && _hopIndex < _hops.Count)
        {
            var hop = _hops[_hopIndex];
            _agent.Warp(FloorNavigator.StairExitOf(hop.to));
            _hopIndex++;

            if (_hopIndex < _hops.Count)
                _agent.SetDestination(FloorNavigator.StairPadOf(CurrentFloor));
            else
                _agent.SetDestination(_idleAnchor);

            return;
        }

        _returningToIdle = false;
        _agent.ResetPath();
    }
}
