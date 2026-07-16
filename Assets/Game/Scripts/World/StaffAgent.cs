using UnityEngine;
using UnityEngine.AI;

// 员工纸片人：Idle → Traverse(跨层 hops) → WalkTo(房间) → Working(工期) → Idle。
// 房态转换只调用 Room2DEntity 的 guard 方法——规则零重写；guard 失败即放弃任务。
//
// M3 监督层：Working 中嵌入 SlackFsm（经理不在场可能偷懒，进度冻结；惊醒/装忙
// 窗口内被经理靠近=现场抓包）；工期拖延超过阈值挂 🐌（质询入口）；
// 打扫完按 quality 概率留瑕疵，验房按 quality 概率漏检。
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
    private float _workTimer;      // 只在真正干活时累加（偷懒冻结）
    private float _wallTimer;      // Working 状态墙钟（拖延判定）
    private float _speedBuffUntil;
    private float _baseSpeed;

    private SlackFsm _slack;
    private System.Random _rng;
    private ManagerController _manager;
    private EmoteBubble _emote;

    public StaffMember Member { get; private set; }
    public AgentState State => _state;
    public bool IsIdle => _state == AgentState.Idle;
    public bool HasDelayMark { get; private set; }
    public bool IsSlacking => _slack != null && _slack.Current == SlackFsm.State.Slacking;

    private float _grudgeUntil;
    /// <summary>Diva 被训斥后的记仇窗口：拒接任何任务（💢）。</summary>
    public bool IsGrudging => Time.time < _grudgeUntil;

    /// <summary>当前任务目标房（无任务=null）。指挥抢占判定用。</summary>
    public Room2DEntity CurrentTaskRoom => _hasTask ? _task.Room : null;

    /// <summary>任务结束（完成或放弃）——TaskDispatcher 订阅以释放 claim。</summary>
    public event System.Action<StaffAgent, Room2DEntity> OnTaskFinished;

    /// <summary>现场抓包（任意员工）——ManagerInteraction 订阅弹决策面板。</summary>
    public static event System.Action<StaffAgent> OnAnyCaught;

    public void Init(StaffMember member, System.Random rng)
    {
        Member = member;
        _rng = rng ?? new System.Random();
        // 只有干活的角色需要偷懒状态机
        if (member != null && (member.Role == StaffRole.Housekeeper || member.Role == StaffRole.Inspector))
        {
            _slack = new SlackFsm(_rng, member.HasTrait(StaffTrait.Lazy), () => member.Morale);
            _slack.OnCaught += HandleCaught;
        }
        _emote = EmoteBubble.Attach(transform);
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _manager = FindFirstObjectByType<ManagerController>();
        _baseSpeed = 3f;
    }

    public int CurrentFloor => FloorMath.FloorIndexForY(transform.position.y);

    /// <summary>接受任务：规划跨层 hops 后出发。仅 Idle 时可接。</summary>
    public bool AssignTask(StaffTask task)
    {
        if (_state != AgentState.Idle || task.Room == null) return false;
        if (IsGrudging) return false; // 记仇中：老娘不干
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
        // 速度 buff 到期恢复
        if (_agent != null)
            _agent.speed = Time.time < _speedBuffUntil ? _baseSpeed * 1.35f : _baseSpeed;

        switch (_state)
        {
            case AgentState.Traverse: TickTraverse(); break;
            case AgentState.WalkToRoom: TickWalk(); break;
            case AgentState.Working: TickWork(); break;
        }
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

        // 偷懒状态机（经理在场判定）
        bool productive = true;
        if (_slack != null && _manager != null)
        {
            int managerFloor = FloorMath.FloorIndexForY(_manager.transform.position.y);
            bool onFloor = managerFloor == CurrentFloor;
            Vector3 d = _manager.transform.position - transform.position;
            d.y = 0f;
            bool near = onFloor && d.magnitude < SupervisionTuning.CatchRadius;
            _slack.Tick(Time.deltaTime, onFloor, near);
            productive = _slack.IsProductive;
        }

        _wallTimer += Time.deltaTime;
        if (productive) _workTimer += Time.deltaTime;

        // 拖延痕迹：墙钟超过预期×阈值 → 🐌（全场可见，事后可质询）
        if (!HasDelayMark && _wallTimer > duration * SupervisionTuning.DelayMarkThresholdMultiplier)
        {
            HasDelayMark = true;
        }

        if (_workTimer < duration) return;

        if (_task.Kind == StaffTaskKind.Clean)
        {
            if (_task.Room.CanFinishCleaning())
            {
                _task.Room.FinishCleaning();
                // 按打扫质量留瑕疵
                if (Member != null && FlawPolicy.RollFlaw(Member.Attributes.Quality, _rng))
                {
                    RoomFlaw.Add(_task.Room, Member);
                }
            }
        }
        else
        {
            if (_task.Room.CanApproveInspection())
            {
                // 验房：有瑕疵时按 quality 决定是否漏检——查出=修掉；漏检=瑕疵存活到 Ready
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
        OnTaskFinished?.Invoke(this, room);
    }

    // ── 监督交互 API（ManagerInteraction 调用） ──────────────────────────────

    private void HandleCaught() => OnAnyCaught?.Invoke(this);

    /// <summary>应用抓包三选一的效果。</summary>
    public void ApplyCatchChoice(CatchChoice choice)
    {
        var outcome = CatchResolutionLogic.Resolve(choice, Member);
        Member?.AdjustMorale(outcome.MoraleDelta);
        if (outcome.SpeedBuff) _speedBuffUntil = Time.time + 20f;
        if (outcome.GrudgeTriggered)
        {
            // Diva 记仇：30 秒拒接单，💢 挂头顶；正在干的活直接撂挑子
            _grudgeUntil = Time.time + 30f;
            AbortTask();
            FloatingTextFx.Spawn(transform.position, "HMPH!", new Color(0.85f, 0.2f, 0.55f));
        }
        _slack?.ResetShiftRecord();
        HasDelayMark = false;
        // ContagionSignal（无视→偷懒传染）仍为占位信号
    }

    /// <summary>中止当前任务（指挥插队/记仇撂挑子用）：打扫到一半的房间回 Dirty，claim 由事件释放。</summary>
    public void AbortTask()
    {
        if (_state == AgentState.Idle) return;
        if (_state == AgentState.Working && _hasTask
            && _task.Kind == StaffTaskKind.Clean && _task.Room != null
            && _task.Room.currentState == Room2DState.Cleaning)
        {
            _task.Room.SetState(Room2DState.Dirty); // 半途而废=白扫
        }
        FinishTask();
    }

    /// <summary>质询（点 🐌）：有偷懒记录=抓包成立；否则错怪好人。</summary>
    public InterrogationVerdict Interrogate()
    {
        var verdict = InterrogateLogic.Verdict(_slack != null && _slack.HasRecentSlackRecord);
        if (verdict == InterrogationVerdict.WrongAccusation)
        {
            Member?.AdjustMorale(SupervisionTuning.WrongAccusationMoraleDelta);
            HasDelayMark = false; // 问过就消标（无论对错）
        }
        return verdict;
    }

    /// <summary>催一催：短期加速，士气小降（连催会一直掉）。</summary>
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
}
