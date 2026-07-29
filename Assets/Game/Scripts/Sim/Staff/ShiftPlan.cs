using System.Collections.Generic;

// 排班表（架构 A.2）。纯 C#，不引用 UnityEngine。
// 玩家不逐人排班（手机上太重），而是按岗位选**档位**：省钱 vs 服务质量的显性权衡。
// 只给在班的人发工资——精简排班省的钱是真的，代价立刻反映在吞吐上（压力网耦合边）。
public enum ShiftTier
{
    Skeleton,  // 骨架班：能开门就行，最省钱，周转极慢
    Lean,      // 精简班
    Normal,    // 正常班
    Full       // 全员上
}

public sealed class ShiftPlan
{
    private readonly Dictionary<StaffRole, ShiftTier> _tiers = new Dictionary<StaffRole, ShiftTier>();
    private readonly Dictionary<StaffRole, int> _nightCounts = new Dictionary<StaffRole, int>();
    private readonly HashSet<int> _nightStaffIds = new HashSet<int>();

    public ShiftTier DefaultTier { get; set; } = ShiftTier.Normal;

    public void SetTier(StaffRole role, ShiftTier tier) => _tiers[role] = tier;

    public ShiftTier TierOf(StaffRole role) =>
        _tiers.TryGetValue(role, out ShiftTier t) ? t : DefaultTier;

    // ── 夜班（用户要求加的）────────────────────────────────────────────────
    // 夜班的人**白天睡觉**：他们不进白班产能，但工资照发（还带 50% 加成）。
    // 排几个人是玩家的决定，代价一眼看得见，回报是接住夜间到店客（见 NightDeskModel）。

    /// <summary>某岗位排几个人上夜班（0 = 不排夜班）。</summary>
    public void SetNightCount(StaffRole role, int count) =>
        _nightCounts[role] = count < 0 ? 0 : count;

    public int NightCountOf(StaffRole role) =>
        _nightCounts.TryGetValue(role, out int n) ? n : 0;

    /// <summary>这个人今天上的是夜班吗（ApplyTo 之后才有意义）。</summary>
    public bool IsOnNightShift(int staffId) => _nightStaffIds.Contains(staffId);

    /// <summary>实际排上夜班的人数（想排 3 个但只雇了 1 个前台时，实际就是 1）。</summary>
    public int NightStaffOnDuty => _nightStaffIds.Count;

    /// <summary>实际上夜班的某岗位人数（夜间前台产能靠它算）。</summary>
    public int NightCountOnDuty(StaffRoster roster, StaffRole role)
    {
        if (roster == null) return 0;
        int n = 0;
        foreach (int id in _nightStaffIds)
            if (roster.TryGet(id, out StaffSimEntry e) && e.member != null && e.member.Role == role) n++;
        return n;
    }

    public static float FractionOf(ShiftTier tier)
    {
        switch (tier)
        {
            case ShiftTier.Skeleton: return 0.25f;
            case ShiftTier.Lean: return 0.5f;
            case ShiftTier.Normal: return 0.8f;
            default: return 1f;
        }
    }

    public static string LabelOf(ShiftTier tier)
    {
        switch (tier)
        {
            case ShiftTier.Skeleton: return "SKELETON - somebody has to open the door";
            case ShiftTier.Lean: return "LEAN - cheap, and it shows";
            case ShiftTier.Normal: return "NORMAL - the sane amount";
            default: return "FULL - everybody in";
        }
    }

    /// <summary>把班表落到员工总账上：按岗位档位决定谁上班。
    /// 名册序遍历（确定性=可复现）。返回在班总人数。</summary>
    public int ApplyTo(StaffRoster roster)
    {
        if (roster == null) return 0;

        var countByRole = new Dictionary<StaffRole, int>();
        var entries = roster.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var member = entries[i].member;
            if (member == null) continue;
            countByRole.TryGetValue(member.Role, out int c);
            countByRole[member.Role] = c + 1;
        }

        var targetByRole = new Dictionary<StaffRole, int>();
        foreach (var kv in countByRole)
        {
            float fraction = FractionOf(TierOf(kv.Key));
            int target = SimMath.RoundToInt(kv.Value * fraction);
            if (target < 1) target = 1;                    // 再抠也得留人看店
            if (target > kv.Value) target = kv.Value;
            targetByRole[kv.Key] = target;
        }

        // **先挑夜班的人**：从名册尾部往前挑（确定性），他们白天不上班。
        // 顺序要紧——先排白班的话，夜班只能从"已经上白班的人"里抢，
        // 于是同一个人白天黑夜连轴转，工资翻倍而产能没变（早期原型踩过这个逻辑坑）。
        _nightStaffIds.Clear();
        var nightPlaced = new Dictionary<StaffRole, int>();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.member == null) continue;
            int want = NightCountOf(e.member.Role);
            if (want <= 0) continue;
            nightPlaced.TryGetValue(e.member.Role, out int already);
            if (already >= want) continue;

            // 至少给白班留一个人：全店都去上夜班的话白天没人开门
            countByRole.TryGetValue(e.member.Role, out int total);
            if (total - already <= 1) continue;

            _nightStaffIds.Add(e.staffId);
            nightPlaced[e.member.Role] = already + 1;
        }

        var placed = new Dictionary<StaffRole, int>();
        int onDuty = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.member == null) continue;

            // 夜班的人白天在睡觉：不进白班产能，但工资照发（DailyWageCost 里带加成）
            if (_nightStaffIds.Contains(e.staffId))
            {
                roster.EndShift(e.staffId);
                continue;
            }

            placed.TryGetValue(e.member.Role, out int already);
            int target = targetByRole.TryGetValue(e.member.Role, out int t) ? t : 0;

            if (already < target)
            {
                roster.StartShift(e.staffId);
                placed[e.member.Role] = already + 1;
                onDuty++;
            }
            else
            {
                roster.EndShift(e.staffId);
            }
        }
        return onDuty;
    }

    /// <summary>当日工资成本：在班的白班 + 上夜班的（夜班白天不在岗但照发工资）。
    /// **周末与夜班带加成**（ShiftPremium）：周末 +30%、夜班 +50%、周末夜班 +80%。
    /// 不传 day 的旧调用按平日白班算——保持既有测试与原型场景不变。</summary>
    public int DailyWageCost(StaffRoster roster, int day = 0)
    {
        if (roster == null) return 0;
        bool weekend = day > 0 && PricingPolicy.IsWeekend(day);

        int total = 0;
        var entries = roster.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.member == null) continue;

            bool night = _nightStaffIds.Contains(e.staffId);
            if (!night && !e.IsOnDuty) continue;   // 白班不在岗 = 不上班不给钱

            total += ShiftPremium.WageFor(e.member.DailyWage,
                                          night ? ShiftSlot.Night : ShiftSlot.Day, weekend);
        }
        return total;
    }
}
