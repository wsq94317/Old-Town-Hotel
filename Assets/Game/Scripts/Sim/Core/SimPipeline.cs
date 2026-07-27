using System;

// 每 tick（1 游戏分钟）的固定顺序推进器（架构 B.1）。纯 C#，不引用 UnityEngine。
//
// 压力网不是四个互相通知的系统，而是**这条管线的数据流**：上游子系统的输出
// 就是下游的输入，耦合天然成立。M-A 立住骨架与服务段，其余段按里程碑接入。
//
//   ① Demand    需求/到店曲线                          [M-B]
//   ② Arrivals  前台队列消化 → checkInWait              [M-B]
//   ③ Service   清洁/检查池推进 → 可售房、脏房积压        ✔ M-A
//   ④ Asset     磨损累积、损坏掷骰                       [M-C]
//   ⑤ Cash      收入入保险箱、时点支出                    [M-C]
//
// **确定性铁律**：同一起始状态 + 同一 tick 数 + 同一种子 ⇒ 同一结果，
// 与真实帧率、倍速、分批消化方式无关。这是"倍速/跳段不改变业务结果"的根据，
// 也是在线 tick 与离线闭式结算能互相校验的前提。
public sealed class SimPipeline
{
    private readonly SimClock _clock;
    private readonly RoomLedger _rooms;
    private readonly StaffRoster _staff;
    private readonly Random _rng;

    // 未满一间房的清洁/检查进度（间）。按分钟累积，够一间就转一间。
    private double _cleanCredit;
    private double _inspectCredit;

    /// <summary>物资供给系数（M-B 接库存系统前恒为 1）。</summary>
    public float SupplyFactor { get; set; } = 1f;

    /// <summary>经理是否在楼上盯着（巡查层每帧上报；影响摸鱼判定）。</summary>
    public bool ManagerOnFloor { get; set; }

    /// <summary>服务段开关。镜像期（M-A）关掉：房态权威还在 v1 DemandLoop 手里，
    /// 两边同时改会互相打架。M-B 需求层迁过来后打开，Sim 成为房态权威。</summary>
    public bool ServiceEnabled { get; set; } = true;

    /// <summary>累计推进的 tick 数（诊断/测试用）。</summary>
    public long TicksRun { get; private set; }

    /// <summary>今日已打扫完的房间数（晨报/客房部面板展示进度）。</summary>
    public int RoomsCleanedToday { get; private set; }

    /// <summary>清空当日计数（BeginDay 调）。</summary>
    public void BeginDay() => RoomsCleanedToday = 0;

    public SimPipeline(SimClock clock, RoomLedger rooms, StaffRoster staff, int rngSeed)
    {
        _clock = clock;
        _rooms = rooms;
        _staff = staff;
        _rng = new Random(rngSeed);
    }

    /// <summary>推进一个游戏分钟。调用方负责先 clock.TryConsumeTick()。</summary>
    public void StepMinute()
    {
        TicksRun++;

        // ④' 员工状态判定先行：本分钟谁在摸鱼，决定了本分钟的产能
        RollStaffStates();

        // ③ 服务段：清洁 → （有 Inspector 则过质量闸门）→ 可售
        if (ServiceEnabled) StepService();

        // 员工分钟记账（本分钟实际干了/摸了多少）
        _staff?.TickMinute();
    }

    /// <summary>日结（宿主在 clock.DayEndReached 后调用）。</summary>
    public void SettleDay(bool wagesPaid)
    {
        _staff?.SettleDay(wagesPaid);
        _cleanCredit = 0d;
        _inspectCredit = 0d;
    }

    private void RollStaffStates()
    {
        if (_staff == null) return;

        // 经理在场：先把摸鱼的惊醒（v2 语义），本分钟也不会有人新开始摸
        if (ManagerOnFloor) _staff.WakeSlackers();

        var entries = _staff.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];

            // 在班待命的人自动开工（M-B 由 TaskDispatcher 派活取代）
            if (e.state == StaffOperationalState.Available)
            {
                e.state = StaffOperationalState.Working;
                continue;
            }
            if (e.state != StaffOperationalState.Working) continue;

            // 摸鱼判定是 Sim 权威：与玩家在不在巡查层无关
            _staff.RollSlackDecision(e.staffId, _rng.NextDouble(), ManagerOnFloor, _rng.NextDouble());
        }
    }

    /// <summary>客房部工作时段：08:00-16:00（退房高峰 + 白天）。
    /// 现实里没人在晚上 9 点进房打扫；这也让人手真正成为稀缺资源——
    /// 若全天 14 小时都能清洁，一个管家每天能清 28 间，百房酒店以下永远不缺人。</summary>
    private bool InHousekeepingHours =>
        _clock != null && _clock.CurrentMinute < PhaseScheduler.CheckInPeakStart;

    private void StepService()
    {
        if (_rooms == null || _staff == null) return;
        if (!InHousekeepingHours)
        {
            ReleaseUnfinishedCleaning();
            return;
        }

        bool hasInspector = ServiceCapacityModel.HasInspectorOnDuty(_staff);

        // 每个在岗管家同时占住一间房：Dirty → Cleaning（进场干活）→ 待检/可售。
        //
        // 以前是 Dirty 直接跳到待检，**Cleaning 这个状态压根没被用过**，
        // 于是"现在正在打扫哪间房"没有任何数据可显示（试玩要求要看到这个）。
        // 让清洁真的占住房间既能显示，也更接近现实：一个管家一次只能在一间房里。
        int crew = _staff.ProductiveCountOfRole(StaffRole.Housekeeper);
        while (_rooms.CountOf(RoomSimState.Cleaning) < crew
               && _rooms.TryFindFirstInState(RoomSimState.Dirty, out int nextRoom))
            _rooms.SetState(nextRoom, RoomSimState.Cleaning);

        // 清洁：进度到了就把最早开工的那间交出去
        _cleanCredit += ServiceCapacityModel.CleanRoomsPerHour(_staff, SupplyFactor) / 60d;
        while (_cleanCredit >= 1d)
        {
            if (!_rooms.TryFindFirstInState(RoomSimState.Cleaning, out int roomNumber))
            {
                // 没房可打扫：进度封顶在"随时能开工一间"，不囤积也不清零。
                // 清零会造成假延迟（下一间脏房出现后还要空等一整间的工时）。
                _cleanCredit = 1d;
                break;
            }
            _cleanCredit -= 1d;
            _rooms.SetState(roomNumber, hasInspector ? RoomSimState.AwaitingInspection : RoomSimState.Ready);
            RoomsCleanedToday++;

            // 腾出手了就立刻接下一间，别让管家空等到下一分钟
            if (_rooms.CountOf(RoomSimState.Cleaning) < crew
                && _rooms.TryFindFirstInState(RoomSimState.Dirty, out int followUp))
                _rooms.SetState(followUp, RoomSimState.Cleaning);
        }

        ReleaseIfCrewShrank(crew);

        // 检查：待检 → 可售
        _inspectCredit += ServiceCapacityModel.InspectRoomsPerHour(_staff) / 60d;
        while (_inspectCredit >= 1d)
        {
            if (!_rooms.TryFindFirstInState(RoomSimState.AwaitingInspection, out int roomNumber))
            {
                _inspectCredit = 1d;
                break;
            }
            _inspectCredit -= 1d;
            _rooms.SetState(roomNumber, RoomSimState.Ready);
        }
    }

    /// <summary>客房部下班（或全员摸鱼/离岗）：没干完的房退回脏房。
    /// 不退的话它们会永远卡在 Cleaning——既不可售、也不算脏房积压，
    /// 又成了玩家看不见的"隐形房间"（这正是这次要修掉的那类问题）。</summary>
    private void ReleaseUnfinishedCleaning()
    {
        while (_rooms.TryFindFirstInState(RoomSimState.Cleaning, out int roomNumber))
            _rooms.SetState(roomNumber, RoomSimState.Dirty);
    }

    /// <summary>在岗人数变少（下班/摸鱼/离职）时，多占的房要还回去。</summary>
    private void ReleaseIfCrewShrank(int crew)
    {
        while (_rooms.CountOf(RoomSimState.Cleaning) > crew
               && _rooms.TryFindFirstInState(RoomSimState.Cleaning, out int roomNumber))
            _rooms.SetState(roomNumber, RoomSimState.Dirty);
    }
}
