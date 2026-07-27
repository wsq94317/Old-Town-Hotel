// 服务能力模型（架构 A.2）。纯函数，不引用 UnityEngine。
// 这是"服务压力"咬住"客流压力"的地方：排班精简 → 吞吐降 → 可售房少 → 入住率降。
// M-B 会在此扩前台吞吐与库存联动；M-A 先把清洁/检查吞吐立住。
public static class ServiceCapacityModel
{
    /// <summary>检查吞吐相对清洁的倍率：验房比打扫快。</summary>
    public const float InspectionSpeedFactor = 2.5f;

    /// <summary>缺货最多把吞吐砍半（不归零——酒店不该被一瓶清洁剂锁死）。</summary>
    public static float SupplyFactor(int inventory, int dailyNeed)
    {
        if (dailyNeed <= 0) return 1f;
        return 0.5f + 0.5f * SimMath.Clamp01(inventory / (float)dailyNeed);
    }

    /// <summary>全酒店每小时清洁吞吐（间/小时）。只算真正在干活的客房管家——
    /// 摸鱼的、待命的、下班的都不产出。
    ///
    /// **人数是非线性的**（HousekeepingTeamModel）：单人独干打折，二三人协作超线性，
    /// 人再多则走廊/货梯拥堵递减。以前是各人速率直接相加，两个人恰好两倍，
    /// 「再雇一个人」这个决策就没有任何取舍——试玩反馈说"没体会到人手的关键性"。</summary>
    public static float CleanRoomsPerHour(StaffRoster roster, float supplyFactor)
    {
        if (roster == null) return 0f;
        float total = 0f;
        int teamSize = 0;
        var entries = roster.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (!e.IsProductive || e.member == null) continue;
            if (e.member.Role != StaffRole.Housekeeper) continue;
            total += StaffDayModel.CleanRoomsPerHour(e.member, e.fatigue);
            teamSize++;
        }
        return total
             * HousekeepingTeamModel.PerPersonFactor(teamSize)
             * SimMath.Clamp(supplyFactor, 0f, 1f);
    }

    /// <summary>全酒店每小时检查吞吐（间/小时）。没有 Inspector 在班就是 0——
    /// 此时清洁完的房直接可售（快，但瑕疵率上浮）。</summary>
    public static float InspectRoomsPerHour(StaffRoster roster)
    {
        if (roster == null) return 0f;
        float total = 0f;
        var entries = roster.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (!e.IsProductive || e.member == null) continue;
            if (e.member.Role != StaffRole.Inspector) continue;
            total += StaffDayModel.CleanRoomsPerHour(e.member, e.fatigue) * InspectionSpeedFactor;
        }
        return total;
    }

    /// <summary>一名前台每小时能办多少入住。</summary>
    public const float BaseCheckInsPerHour = 6f;

    /// <summary>全酒店每小时办入住能力。前台无人=0，队伍只会越排越长。</summary>
    public static float CheckInsPerHour(StaffRoster roster)
    {
        if (roster == null) return 0f;
        float total = 0f;
        var entries = roster.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (!e.IsProductive || e.member == null) continue;
            if (e.member.Role != StaffRole.Reception) continue;
            float speedNorm = SimMath.Clamp(e.member.Attributes.Speed, 1, 100) / 50f;
            total += BaseCheckInsPerHour * speedNorm
                   * StaffDayModel.MoraleFactor(e.member.Morale)
                   * StaffDayModel.FatigueFactor(e.fatigue);
        }
        return total;
    }

    /// <summary>是否有 Inspector 在班（决定清洁完的房走不走质量闸门）。</summary>
    public static bool HasInspectorOnDuty(StaffRoster roster) =>
        roster != null && roster.OnDutyCountOfRole(StaffRole.Inspector) > 0;
}
