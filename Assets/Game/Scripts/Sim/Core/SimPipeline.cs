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

    /// <summary>累计推进的 tick 数（诊断/测试用）。</summary>
    public long TicksRun { get; private set; }

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
        StepService();

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

    private void StepService()
    {
        if (_rooms == null || _staff == null) return;

        bool hasInspector = ServiceCapacityModel.HasInspectorOnDuty(_staff);

        // 清洁：脏房 → 待检（有验房员）/ 直接可售（没验房员，快但瑕疵率高）
        _cleanCredit += ServiceCapacityModel.CleanRoomsPerHour(_staff, SupplyFactor) / 60d;
        while (_cleanCredit >= 1d)
        {
            if (!_rooms.TryFindFirstInState(RoomSimState.Dirty, out int roomNumber))
            {
                // 没脏房可打扫：进度封顶在"随时能开工一间"，不囤积也不清零。
                // 清零会造成假延迟（下一间脏房出现后还要空等一整间的工时）。
                _cleanCredit = 1d;
                break;
            }
            _cleanCredit -= 1d;
            _rooms.SetState(roomNumber, hasInspector ? RoomSimState.AwaitingInspection : RoomSimState.Ready);
        }

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
}
