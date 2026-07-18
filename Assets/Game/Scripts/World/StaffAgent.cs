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
        Working,
        RoutineTravel,
        OffShift
    }

    private enum RoutineTravelPurpose
    {
        None,
        Arrival,
        ReturnIdle,
        Patrol,
        BreakRoom,
        Toilet,
        Restock,
        Exit
    }

    [SerializeField] private float cleanSeconds = 5f;
    [SerializeField] private float inspectSeconds = 4f;

    private NavMeshAgent _agent;
    private AgentState _state = AgentState.OffShift;
    private StaffShiftState _shiftState = StaffShiftState.OffShift;
    private StaffActivityState _activityState = StaffActivityState.Leaving;
    private StaffTask _task;
    private bool _hasTask;
    private List<(int from, int to)> _hops;
    private int _hopIndex;
    private float _workTimer;
    private float _wallTimer;
    private float _speedBuffUntil;
    private float _baseSpeed;
    private float _activityUntil;
    private float _nextRoutineAt;
    private float _nextPersonalNeedAt;
    private int _patrolIndex;
    private int _observedDay = -1;

    private SlackFsm _slack;
    private Random _rng;
    private ManagerController _manager;
    private Room2DDemoDayController _dayController;
    private StaffFacilitySystem _facilities;
    private EmoteBubble _emote;
    private Vector3 _idleAnchor;
    private bool _hasIdleAnchor;
    private Vector3 _routineTarget;
    private RoutineTravelPurpose _routinePurpose;
    private bool _toiletHiding;
    private bool _toiletSlackRecord;

    public StaffMember Member { get; private set; }
    public AgentState State => _state;
    public StaffShiftState ShiftState => _shiftState;
    public StaffActivityState ActivityState => _activityState;
    public bool IsIdle => StaffRoutineLogic.CanAutoAcceptRoomTask(
        Member != null ? Member.Role : StaffRole.Manager,
        _shiftState,
        _activityState,
        _hasTask);
    public bool HasDelayMark { get; private set; }
    public bool IsSlacking => _activityState == StaffActivityState.HidingInToilet
        || (_slack != null && _slack.Current == SlackFsm.State.Slacking);
    public bool ProvidesFrontDeskCoverage => Member != null
        && Member.Role == StaffRole.Reception
        && _shiftState == StaffShiftState.OnDuty
        && _activityState == StaffActivityState.AtPost
        && FlatDistance(transform.position, _idleAnchor) <= 1.6f;

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

        _nextPersonalNeedAt = Time.time + StaffRoutineLogic.NextPersonalNeedDelaySeconds(member.Role, _rng);
        _emote = EmoteBubble.Attach(transform);
    }

    public void SetIdleAnchor(Vector3 worldPosition)
    {
        _idleAnchor = worldPosition;
        _hasIdleAnchor = true;
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _manager = FindFirstObjectByType<ManagerController>();
        _dayController = FindFirstObjectByType<Room2DDemoDayController>();
        _facilities = StaffFacilitySystem.EnsureInScene();
        _baseSpeed = 3f;
    }

    private void OnDestroy()
    {
        if (_slack != null) _slack.OnCaught -= HandleCaught;
        _facilities?.ReleaseToilet(this);
    }

    public int CurrentFloor => FloorMath.FloorIndexForY(transform.position.y);

    public bool AssignTask(StaffTask task)
    {
        if (task.Room == null || IsGrudging || !IsIdle) return false;

        CancelRoutineTravel();
        _task = task;
        _hasTask = true;
        _slack?.ResetShiftRecord();
        _toiletSlackRecord = false;
        HasDelayMark = false;
        _activityState = StaffActivityState.Travel;

        int targetFloor = FloorMath.FloorIndexForY(task.Room.transform.position.y);
        _hops = FloorNavigator.PlanHops(CurrentFloor, targetFloor);
        _hopIndex = 0;

        if (_hops.Count > 0)
        {
            _state = AgentState.Traverse;
            SetAgentDestination(FloorNavigator.StairPadOf(CurrentFloor));
        }
        else
        {
            _state = AgentState.WalkToRoom;
            SetAgentDestination(task.Room.transform.position);
        }

        return true;
    }

    private void Update()
    {
        ResolveReferences();
        SyncShiftWithClock();

        if (_shiftState == StaffShiftState.OffShift)
        {
            UpdateEmote();
            return;
        }

        if (_state != AgentState.Idle && _state != AgentState.RoutineTravel
            && _state != AgentState.OffShift && _task.Room == null)
        {
            FinishTask();
            return;
        }

        if (_agent != null)
            _agent.speed = Time.time < _speedBuffUntil ? _baseSpeed * 1.35f : _baseSpeed;

        switch (_state)
        {
            case AgentState.Traverse:
                TickTaskTraverse();
                break;
            case AgentState.WalkToRoom:
                TickWalkToRoom();
                break;
            case AgentState.Working:
                TickWork();
                break;
            case AgentState.RoutineTravel:
                TickRoutineTravel();
                break;
            case AgentState.Idle:
                TickRoutineActivity();
                break;
        }

        UpdateEmote();
    }

    private void ResolveReferences()
    {
        if (_manager == null) _manager = FindFirstObjectByType<ManagerController>();
        if (_dayController == null) _dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (_facilities == null) _facilities = StaffFacilitySystem.EnsureInScene();
    }

    private void SyncShiftWithClock()
    {
        if (_dayController == null || Member == null) return;

        if (_observedDay != _dayController.CurrentDay)
        {
            _observedDay = _dayController.CurrentDay;
            BeginShift();
            return;
        }

        if (_dayController.Clock.DayEndReached
            && _shiftState != StaffShiftState.EndShift
            && _shiftState != StaffShiftState.LeavingMap
            && _shiftState != StaffShiftState.OffShift)
        {
            _shiftState = StaffShiftState.EndShift;
            if (!_hasTask) BeginLeavingMap();
        }
    }

    private void BeginShift()
    {
        _facilities?.ReleaseToilet(this);
        _hasTask = false;
        _shiftState = StaffShiftState.Arriving;
        _activityState = StaffActivityState.Travel;
        _toiletSlackRecord = false;
        HasDelayMark = false;
        SetVisible(true);

        Vector3 entry = _facilities != null ? _facilities.ExitPoint : new Vector3(0f, 0f, -5.2f);
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh
            && NavMesh.SamplePosition(entry, out NavMeshHit entryHit, 2.5f, NavMesh.AllAreas))
            _agent.Warp(entryHit.position);
        else
            transform.position = entry;

        StartRoutineTravel(_hasIdleAnchor ? _idleAnchor : transform.position, RoutineTravelPurpose.Arrival);
        _nextPersonalNeedAt = Time.time + StaffRoutineLogic.NextPersonalNeedDelaySeconds(Member.Role, _rng);
    }

    private void CompleteArrival()
    {
        _shiftState = StaffShiftState.OnDuty;
        if (Member.Role == StaffRole.Housekeeper && _facilities != null && _facilities.Kitchen != null)
        {
            StartRoutineTravel(_facilities.Kitchen.Anchor, RoutineTravelPurpose.Restock);
            return;
        }

        EnterDefaultActivity();
    }

    private void BeginLeavingMap()
    {
        _facilities?.ReleaseToilet(this);
        _shiftState = StaffShiftState.LeavingMap;
        _activityState = StaffActivityState.Leaving;
        Vector3 exit = _facilities != null ? _facilities.ExitPoint : new Vector3(0f, 0f, -5.2f);
        StartRoutineTravel(exit, RoutineTravelPurpose.Exit);
    }

    private bool Arrived()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return true;
        return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.15f;
    }

    private void TickTaskTraverse()
    {
        if (!Arrived()) return;

        var hop = _hops[_hopIndex];
        _agent.Warp(FloorNavigator.StairExitOf(hop.to));
        _hopIndex++;

        if (_hopIndex < _hops.Count)
            SetAgentDestination(FloorNavigator.StairPadOf(CurrentFloor));
        else
        {
            _state = AgentState.WalkToRoom;
            SetAgentDestination(_task.Room.transform.position);
        }
    }

    private void TickWalkToRoom()
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
        _activityState = StaffActivityState.Working;
    }

    private void TickWork()
    {
        float duration = _task.Kind == StaffTaskKind.Clean ? cleanSeconds : inspectSeconds;
        if (Member != null)
        {
            float speedMultiplier = Mathf.Lerp(0.8f, 1.3f, Member.Attributes.Speed / 100f);
            duration /= speedMultiplier;
        }

        bool productive = true;
        if (_slack != null && _manager != null)
        {
            int managerFloor = FloorMath.FloorIndexForY(_manager.transform.position.y);
            bool onFloor = managerFloor == CurrentFloor;
            bool near = onFloor && FlatDistance(_manager.transform.position, transform.position) < SupervisionTuning.CatchRadius;
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
        else if (_task.Room.CanApproveInspection())
        {
            var flaw = RoomFlaw.Get(_task.Room);
            if (flaw != null && Member != null
                && !FlawPolicy.RollInspectorMiss(Member.Attributes.Quality, _rng))
                RoomFlaw.Clear(_task.Room);

            _task.Room.ApproveInspection();
        }

        FinishTask();
    }

    private void FinishTask()
    {
        var room = _hasTask ? _task.Room : null;
        _hasTask = false;
        _state = AgentState.Idle;
        HasDelayMark = false;
        ResetAgentPath();
        OnTaskFinished?.Invoke(this, room);

        if (_shiftState == StaffShiftState.EndShift)
            BeginLeavingMap();
        else
            ReturnToRoleIdle();
    }

    private void TickRoutineActivity()
    {
        if (_shiftState != StaffShiftState.OnDuty) return;

        if (_activityState == StaffActivityState.BreakRoom
            || _activityState == StaffActivityState.PublicToilet
            || _activityState == StaffActivityState.HidingInToilet
            || _activityState == StaffActivityState.Restock)
        {
            if (Time.time < _activityUntil) return;
            FinishTimedActivity();
            return;
        }

        if (Time.time >= _nextPersonalNeedAt && TryStartPersonalNeed()) return;

        if (Member.Role == StaffRole.Reception)
        {
            if (_activityState != StaffActivityState.AtPost || FlatDistance(transform.position, _idleAnchor) > 1.2f)
                StartRoutineTravel(_idleAnchor, RoutineTravelPurpose.ReturnIdle);
            return;
        }

        if (Member.Role == StaffRole.Inspector && Time.time >= _nextRoutineAt && _facilities != null)
        {
            int floor = 1 + (_patrolIndex % Mathf.Max(1, FloorMath.FloorCount - 1));
            Vector3 patrolPoint = _facilities.PatrolAnchor(floor, _patrolIndex++);
            StartRoutineTravel(patrolPoint, RoutineTravelPurpose.Patrol);
        }
    }

    private bool TryStartPersonalNeed()
    {
        _nextPersonalNeedAt = Time.time + StaffRoutineLogic.NextPersonalNeedDelaySeconds(Member.Role, _rng);
        if (_facilities == null) return false;

        bool preferToilet = _rng.NextDouble() < 0.72d;
        if (preferToilet && _facilities.TryReserveToilet(this))
        {
            _toiletHiding = StaffRoutineLogic.ShouldHideInToilet(Member, _rng);
            StartRoutineTravel(_facilities.PublicToilet.Anchor, RoutineTravelPurpose.Toilet);
            return true;
        }

        StartRoutineTravel(_facilities.BreakRoomAnchor, RoutineTravelPurpose.BreakRoom);
        return true;
    }

    private void FinishTimedActivity()
    {
        if (_activityState == StaffActivityState.PublicToilet || _activityState == StaffActivityState.HidingInToilet)
            _facilities?.ReleaseToilet(this);

        ReturnToRoleIdle();
    }

    private void ReturnToRoleIdle()
    {
        if (_shiftState != StaffShiftState.OnDuty) return;

        Vector3 target = _idleAnchor;
        if (Member != null && Member.Role == StaffRole.Housekeeper && _facilities != null)
            target = _facilities.SupportAnchorForFloor(CurrentFloor, GetInstanceID());

        StartRoutineTravel(target, RoutineTravelPurpose.ReturnIdle);
    }

    private void EnterDefaultActivity()
    {
        _state = AgentState.Idle;
        _activityState = StaffRoutineLogic.DefaultActivityFor(Member.Role);
        _nextRoutineAt = Time.time + RandomRange(5f, 10f);
        ResetAgentPath();
    }

    private void StartRoutineTravel(Vector3 target, RoutineTravelPurpose purpose)
    {
        _routineTarget = SampleDestination(target);
        _routinePurpose = purpose;
        _hops = FloorNavigator.PlanHops(CurrentFloor, FloorMath.FloorIndexForY(_routineTarget.y));
        _hopIndex = 0;
        _state = AgentState.RoutineTravel;

        if (purpose == RoutineTravelPurpose.ReturnIdle)
            _activityState = StaffRoutineLogic.DefaultActivityFor(Member.Role);
        else if (purpose == RoutineTravelPurpose.Patrol)
            _activityState = StaffActivityState.Patrol;
        else if (purpose == RoutineTravelPurpose.Exit)
            _activityState = StaffActivityState.Leaving;
        else
            _activityState = StaffActivityState.Travel;

        SetAgentDestination(_hops.Count > 0 ? FloorNavigator.StairPadOf(CurrentFloor) : _routineTarget);
    }

    private void TickRoutineTravel()
    {
        if (!Arrived()) return;

        if (_hops != null && _hopIndex < _hops.Count)
        {
            var hop = _hops[_hopIndex];
            _agent.Warp(FloorNavigator.StairExitOf(hop.to));
            _hopIndex++;
            SetAgentDestination(_hopIndex < _hops.Count
                ? FloorNavigator.StairPadOf(CurrentFloor)
                : _routineTarget);
            return;
        }

        RoutineTravelPurpose completed = _routinePurpose;
        _routinePurpose = RoutineTravelPurpose.None;
        _state = AgentState.Idle;
        ResetAgentPath();

        switch (completed)
        {
            case RoutineTravelPurpose.Arrival:
                CompleteArrival();
                break;
            case RoutineTravelPurpose.ReturnIdle:
                EnterDefaultActivity();
                break;
            case RoutineTravelPurpose.Patrol:
                _activityState = StaffActivityState.Patrol;
                _nextRoutineAt = Time.time + RandomRange(5f, 9f);
                break;
            case RoutineTravelPurpose.BreakRoom:
                _activityState = StaffActivityState.BreakRoom;
                _activityUntil = Time.time + RandomRange(5f, 9f);
                break;
            case RoutineTravelPurpose.Toilet:
                BeginToiletActivity();
                break;
            case RoutineTravelPurpose.Restock:
                _activityState = StaffActivityState.Restock;
                _activityUntil = Time.time + RandomRange(2.5f, 4.5f);
                break;
            case RoutineTravelPurpose.Exit:
                _shiftState = StaffShiftState.OffShift;
                _activityState = StaffActivityState.Leaving;
                _state = AgentState.OffShift;
                SetVisible(false);
                break;
        }
    }

    private void BeginToiletActivity()
    {
        if (_facilities == null || !_facilities.BeginStaffToiletUse(this, _toiletHiding))
        {
            ReturnToRoleIdle();
            return;
        }

        _activityState = _toiletHiding
            ? StaffActivityState.HidingInToilet
            : StaffActivityState.PublicToilet;
        if (_toiletHiding) _toiletSlackRecord = true;
        _activityUntil = Time.time + StaffRoutineLogic.ToiletDurationSeconds(_toiletHiding, _rng);
    }

    private void CancelRoutineTravel()
    {
        if (_routinePurpose == RoutineTravelPurpose.Toilet) _facilities?.ReleaseToilet(this);
        _routinePurpose = RoutineTravelPurpose.None;
        ResetAgentPath();
    }

    public void HandleToiletInspection(bool caughtHiding)
    {
        _toiletHiding = false;
        _state = AgentState.Idle;
        _activityState = StaffActivityState.Idle;

        if (caughtHiding)
        {
            _toiletSlackRecord = true;
            HasDelayMark = true;
            OnAnyCaught?.Invoke(this);
        }
        else
        {
            Member?.AdjustMorale(StaffRoutineLogic.LegitimateToiletInspectionMoralePenalty);
        }

        if (_shiftState == StaffShiftState.EndShift)
            BeginLeavingMap();
        else
            ReturnToRoleIdle();
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
        _toiletSlackRecord = false;
        HasDelayMark = false;
    }

    public void AbortTask()
    {
        if (!_hasTask)
        {
            if (_activityState == StaffActivityState.PublicToilet
                || _activityState == StaffActivityState.HidingInToilet)
                _facilities?.ReleaseToilet(this);
            CancelRoutineTravel();
            if (_shiftState == StaffShiftState.OnDuty) ReturnToRoleIdle();
            return;
        }

        if (_state == AgentState.Working
            && _task.Kind == StaffTaskKind.Clean
            && _task.Room != null
            && _task.Room.currentState == Room2DState.Cleaning)
            _task.Room.SetState(Room2DState.Dirty);

        FinishTask();
    }

    public InterrogationVerdict Interrogate()
    {
        bool hasSlackRecord = _toiletSlackRecord || (_slack != null && _slack.HasRecentSlackRecord);
        var verdict = InterrogateLogic.Verdict(hasSlackRecord);
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

    private void SetAgentDestination(Vector3 target)
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.SetDestination(SampleDestination(target));
    }

    private static Vector3 SampleDestination(Vector3 target)
    {
        return NavMesh.SamplePosition(target, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)
            ? hit.position
            : target;
    }

    private void ResetAgentPath()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.ResetPath();
    }

    private void SetVisible(bool visible)
    {
        var floorVisibility = GetComponent<AgentFloorVisibility>();
        if (floorVisibility != null)
        {
            floorVisibility.SetForceHidden(!visible);
            return;
        }
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
        foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = visible;
    }

    private float RandomRange(float min, float max)
    {
        if (_rng == null) return min;
        return min + (float)_rng.NextDouble() * (max - min);
    }

    private static float FlatDistance(Vector3 left, Vector3 right)
    {
        Vector3 delta = left - right;
        delta.y = 0f;
        return delta.magnitude;
    }
}
