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

    public ShiftTier DefaultTier { get; set; } = ShiftTier.Normal;

    public void SetTier(StaffRole role, ShiftTier tier) => _tiers[role] = tier;

    public ShiftTier TierOf(StaffRole role) =>
        _tiers.TryGetValue(role, out ShiftTier t) ? t : DefaultTier;

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

        var placed = new Dictionary<StaffRole, int>();
        int onDuty = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.member == null) continue;
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

    /// <summary>当日工资成本：只算在班的人（下班的是临时工，不上班不给钱）。</summary>
    public int DailyWageCost(StaffRoster roster)
    {
        if (roster == null) return 0;
        int total = 0;
        var entries = roster.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.member == null || !e.IsOnDuty) continue;
            total += e.member.DailyWage;
        }
        return total;
    }
}
