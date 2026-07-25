using System.Collections.Generic;

// 员工模拟总账（架构 A.2 / 修订版 3 强化项）。纯 C#，不引用 UnityEngine。
//
// 修订版 3 的关键裁决：**摸鱼本身是 Sim 事实，不只是后果**。
//   Sim 决定：是否摸鱼、摸多久、产能损失、被抓后的士气与效率后果
//   World 决定：走厕所还是休息室、播什么动画、站哪、玩家点击怎么演
// 修订版 2 允许 SlackFsm 在场景层自主掷骰，存在一致性洞——玩家停在剖面层不进巡查层时
// 场景 agent 是否在跑？在线/巡查/离线会掷出三种结果。现在统一由这里掷。
public enum StaffOperationalState
{
    OffShift,   // 下班/未排班
    Available,  // 在班待命
    Assigned,   // 已派活，赶路中
    Working,    // 正在干活（唯一产出状态）
    Break,      // 正当休息（合理的，不是摸鱼）
    Slacking,   // 摸鱼（产能归零，可被抓）
    Absent      // 旷工/离场
}

/// <summary>一名员工的模拟状态。用 class 而非可变 struct——员工数量小，不值得承担副本风险。</summary>
public sealed class StaffSimEntry
{
    public int staffId;
    public StaffMember member;
    public StaffOperationalState state = StaffOperationalState.OffShift;
    public float fatigue;              // 0..1，跨日累积/恢复
    public int workedMinutesToday;
    public int slackMinutesToday;
    public int caughtCountToday;
    public int slackMinutesRemaining;  // 本次摸鱼剩余时长；归零自己回去干活

    public bool IsOnDuty => state != StaffOperationalState.OffShift && state != StaffOperationalState.Absent;

    /// <summary>只有 Working 算产出：摸鱼、待命、赶路、休息都不产出。</summary>
    public bool IsProductive => state == StaffOperationalState.Working;
}

public sealed class StaffRoster
{
    private readonly List<StaffSimEntry> _entries = new List<StaffSimEntry>();
    private readonly Dictionary<int, StaffSimEntry> _byId = new Dictionary<int, StaffSimEntry>();
    private int _nextId;

    public int Count => _entries.Count;

    public IReadOnlyList<StaffSimEntry> Entries => _entries;

    /// <summary>登记一名员工，返回稳定 id。id 单调递增且**不复用**——存档/事件引用不会张冠李戴。</summary>
    public int Register(StaffMember member)
    {
        if (member == null) return 0;
        int id = ++_nextId;
        var entry = new StaffSimEntry { staffId = id, member = member };
        _entries.Add(entry);
        _byId[id] = entry;
        return id;
    }

    public bool Remove(int staffId)
    {
        if (!_byId.TryGetValue(staffId, out StaffSimEntry entry)) return false;
        _byId.Remove(staffId);
        _entries.Remove(entry);
        return true;
    }

    public bool TryGet(int staffId, out StaffSimEntry entry) => _byId.TryGetValue(staffId, out entry);

    public StaffOperationalState StateOf(int staffId) =>
        _byId.TryGetValue(staffId, out StaffSimEntry e) ? e.state : StaffOperationalState.OffShift;

    public void SetState(int staffId, StaffOperationalState state)
    {
        if (!_byId.TryGetValue(staffId, out StaffSimEntry e)) return;
        e.state = state;

        // 防坑：不经 RollSlackDecision 直接置成 Slacking（巡查层/调试也可能这么干）时
        // 补一个默认时长，否则 remaining=0 会让它在下一 tick 立刻过期。
        if (state == StaffOperationalState.Slacking && e.slackMinutesRemaining <= 0)
            e.slackMinutesRemaining = StaffDayModel.SlackDurationMinutes(0.5d);
        else if (state != StaffOperationalState.Slacking)
            e.slackMinutesRemaining = 0;
    }

    public void StartShift(int staffId) => SetState(staffId, StaffOperationalState.Available);

    public void EndShift(int staffId) => SetState(staffId, StaffOperationalState.OffShift);

    // ── 统计（服务压力面板的输入） ────────────────────────────────────────────

    public int OnDutyCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].IsOnDuty) n++;
            return n;
        }
    }

    public int OnDutyCountOfRole(StaffRole role)
    {
        int n = 0;
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].IsOnDuty && _entries[i].member != null && _entries[i].member.Role == role) n++;
        return n;
    }

    /// <summary>真正在产出的人数（摸鱼/待命/下班都不算）。</summary>
    public int ProductiveCountOfRole(StaffRole role)
    {
        int n = 0;
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].IsProductive && _entries[i].member != null && _entries[i].member.Role == role) n++;
        return n;
    }

    /// <summary>当前最长的摸鱼剩余时长（诊断/测试用）。</summary>
    public int MaxSlackMinutesRemaining
    {
        get
        {
            int max = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].slackMinutesRemaining > max) max = _entries[i].slackMinutesRemaining;
            return max;
        }
    }

    public float AverageMorale
    {
        get
        {
            if (_entries.Count == 0) return 0f;
            int sum = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].member != null) sum += _entries[i].member.Morale;
            return sum / (float)_entries.Count;
        }
    }

    // ── 每分钟推进 ───────────────────────────────────────────────────────────

    /// <summary>推进一分钟：按当前状态累计工作/摸鱼分钟，并消耗摸鱼时长。
    /// 由 SimPipeline 每 tick 调一次。</summary>
    public void TickMinute()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.state == StaffOperationalState.Working)
            {
                e.workedMinutesToday++;
            }
            else if (e.state == StaffOperationalState.Slacking)
            {
                e.slackMinutesToday++;
                if (--e.slackMinutesRemaining <= 0)
                {
                    e.slackMinutesRemaining = 0;
                    e.state = StaffOperationalState.Working; // 摸够了，自己回去干活
                }
            }
        }
    }

    /// <summary>经理进场：把摸鱼的人全部惊醒回去干活（v2"惊醒"语义的 Sim 版）。
    /// 返回被惊醒的人数——巡查层可据此播慌张装忙的演出。</summary>
    public int WakeSlackers()
    {
        int woken = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.state != StaffOperationalState.Slacking) continue;
            e.state = StaffOperationalState.Working;
            e.slackMinutesRemaining = 0;
            woken++;
        }
        return woken;
    }

    // ── 摸鱼：Sim 权威 ───────────────────────────────────────────────────────

    /// <summary>该员工当前的每分钟摸鱼概率（UI/调试可读）。</summary>
    public double SlackChanceOf(int staffId) =>
        _byId.TryGetValue(staffId, out StaffSimEntry e)
            ? StaffDayModel.SlackChancePerMinute(e.member, managerOnFloor: false)
            : 0d;

    /// <summary>掷一次摸鱼判定（roll 外部注入=可测）。只有正在干活的人才会开始摸鱼。
    /// durationRoll 决定这次摸多久（不传则取中位时长）。</summary>
    public bool RollSlackDecision(int staffId, double roll, bool managerOnFloor, double durationRoll = 0.5d)
    {
        if (!_byId.TryGetValue(staffId, out StaffSimEntry e)) return false;
        if (e.state != StaffOperationalState.Working) return false;
        double chance = StaffDayModel.SlackChancePerMinute(e.member, managerOnFloor);
        if (roll >= chance) return false;
        e.state = StaffOperationalState.Slacking;
        e.slackMinutesRemaining = StaffDayModel.SlackDurationMinutes(durationRoll);
        return true;
    }

    /// <summary>巡查层上报"玩家抓了这个人"。是否真在摸鱼由 Sim 判定——抓包查状态，不查动画。
    /// 抓对了掉士气+回去干活；错怪好人掉更多士气。返回是否真的抓到。</summary>
    public bool ReportCaught(int staffId)
    {
        if (!_byId.TryGetValue(staffId, out StaffSimEntry e) || e.member == null) return false;

        if (e.state == StaffOperationalState.Slacking)
        {
            e.caughtCountToday++;
            e.member.AdjustMorale(StaffDayModel.CaughtSlackingMoralePenalty);
            e.state = StaffOperationalState.Working;
            e.slackMinutesRemaining = 0;
            return true;
        }

        e.member.AdjustMorale(StaffDayModel.FalseAccusationMoralePenalty);
        return false;
    }

    /// <summary>巡查层上报的摸鱼分钟（演出层观测到的，用于对齐；Sim 已自行计数，这里只做补偿）。</summary>
    public void ReportSlackMinutes(int staffId, int minutes)
    {
        if (minutes <= 0) return;
        if (_byId.TryGetValue(staffId, out StaffSimEntry e)) e.slackMinutesToday += minutes;
    }

    // ── 日结 ─────────────────────────────────────────────────────────────────

    /// <summary>日结：累积疲劳、结算欠薪士气、清空当日计数、全员下班。</summary>
    public void SettleDay(bool wagesPaid)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            e.fatigue = SimMath.Clamp01(e.fatigue
                                        + StaffDayModel.FatigueGainFor(e.workedMinutesToday)
                                        - StaffDayModel.FatigueRecovery());
            if (!wagesPaid && e.member != null)
                e.member.AdjustMorale(StaffDayModel.UnpaidWageMoralePenalty);

            e.workedMinutesToday = 0;
            e.slackMinutesToday = 0;
            e.caughtCountToday = 0;
            e.slackMinutesRemaining = 0;
            e.state = StaffOperationalState.OffShift;
        }
    }

    // ── 存档 ─────────────────────────────────────────────────────────────────

    /// <summary>下一个将要分配的 id（存档要带上，否则读档后新雇员工的 id 会和旧的撞车）。</summary>
    public int NextIdSeed => _nextId;

    /// <summary>读档：恢复 id 计数器，保证新雇的人不会复用历史 id。</summary>
    public void RestoreIdSeed(int seed)
    {
        if (seed > _nextId) _nextId = seed;
    }

    /// <summary>读档：按名册序恢复第 index 名员工的 Sim 侧运营状态。</summary>
    public void RestoreEntryState(int index, int staffId, StaffOperationalState state,
                                  float fatigue, int slackMinutesRemaining)
    {
        if (index < 0 || index >= _entries.Count) return;
        var e = _entries[index];

        if (staffId > 0 && staffId != e.staffId)
        {
            _byId.Remove(e.staffId);
            e.staffId = staffId;
            _byId[staffId] = e;
            RestoreIdSeed(staffId);
        }
        e.state = state;
        e.fatigue = SimMath.Clamp01(fatigue);
        e.slackMinutesRemaining = slackMinutesRemaining < 0 ? 0 : slackMinutesRemaining;
    }

    /// <summary>日结后逐人掷离职（roll 外部注入）。返回离职者 id 列表。</summary>
    public List<int> RollQuits(System.Func<int, double> rollFor)
    {
        var quitters = new List<int>();
        if (rollFor == null) return quitters;
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.member == null) continue;
            if (StaffDayModel.WantsToQuit(e.member.Morale, rollFor(e.staffId)))
                quitters.Add(e.staffId);
        }
        return quitters;
    }
}
