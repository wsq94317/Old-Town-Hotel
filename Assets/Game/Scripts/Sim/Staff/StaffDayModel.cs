// 员工数值模型（架构 A.2 / 修订版 3）。纯函数，不引用 UnityEngine。
// 士气数值变化的**唯一出处**：谁想改士气都得走这里，否则数值来源散落就再也调不平。
public static class StaffDayModel
{
    /// <summary>基础清洁吞吐（间/小时），属性 50 的普通员工。</summary>
    public const float BaseRoomsPerHour = 2f;

    /// <summary>欠薪一天的士气代价（真实经营亏损才会发生，不因玩家没点收取键而发生）。</summary>
    public const int UnpaidWageMoralePenalty = -15;

    /// <summary>被抓到摸鱼的士气代价。</summary>
    public const int CaughtSlackingMoralePenalty = -6;

    /// <summary>错怪好人的士气代价（比抓对更狠——v2 质询玩法的语义）。</summary>
    public const int FalseAccusationMoralePenalty = -10;

    /// <summary>士气见底的离职门槛与每日离职概率。</summary>
    public const int QuitMoraleThreshold = 15;
    public const double DailyQuitChance = 0.35d;

    private const int FullShiftMinutes = 600; // 10 小时算满班

    /// <summary>士气 → 效率系数 0.7~1.1。低士气不是归零，是拖慢（酒店不该被一个坏心情锁死）。</summary>
    public static float MoraleFactor(int morale)
    {
        float m = SimMath.Clamp(morale, 0, 100) / 100f;
        return 0.7f + 0.4f * m;
    }

    /// <summary>疲劳 → 效率系数 1.0~0.7。</summary>
    public static float FatigueFactor(float fatigue) => 1f - 0.3f * SimMath.Clamp01(fatigue);

    /// <summary>某员工每小时能清几间房。属性/士气/疲劳三重相乘，永不归零。</summary>
    public static float CleanRoomsPerHour(StaffMember member, float fatigue)
    {
        if (member == null) return 0f;
        float speedNorm = SimMath.Clamp(member.Attributes.Speed, 1, 100) / 50f; // 50=基准
        float rate = BaseRoomsPerHour * speedNorm * MoraleFactor(member.Morale) * FatigueFactor(fatigue);
        return rate < 0.2f ? 0.2f : rate;
    }

    /// <summary>每分钟摸鱼概率。经理在场归零（在场就是最好的管理）。</summary>
    public static double SlackChancePerMinute(StaffMember member, bool managerOnFloor)
    {
        if (member == null || managerOnFloor) return 0d;
        double basis = 0.01d;                                     // 普通员工每分钟 1%
        if (member.HasTrait(StaffTrait.Lazy)) basis *= 3d;        // 懒惰特质三倍
        double moraleSlack = (100 - SimMath.Clamp(member.Morale, 0, 100)) / 100d; // 士气越低越摸
        return SimMath.Clamp(basis * (0.5d + moraleSlack), 0d, 0.5d);
    }

    /// <summary>一次摸鱼持续多久（分钟）。摸鱼必须有终点——否则一天下来全员永久摸鱼、酒店猝死。</summary>
    public const int MinSlackMinutes = 5;
    public const int MaxSlackMinutes = 20;

    public static int SlackDurationMinutes(double roll)
    {
        double r = SimMath.Clamp01(roll);
        return MinSlackMinutes + (int)(r * (MaxSlackMinutes - MinSlackMinutes));
    }

    /// <summary>一天下来的疲劳增量（满班 +0.35，休息不足会跨日累积）。</summary>
    public static float FatigueGainFor(int workedMinutes)
    {
        float ratio = SimMath.Clamp01(workedMinutes / (float)FullShiftMinutes);
        return 0.35f * ratio;
    }

    /// <summary>夜间恢复的疲劳量（下班休息）。</summary>
    public static float FatigueRecovery() => 0.25f;

    /// <summary>士气见底 + 低骰 → 主动离职。</summary>
    public static bool WantsToQuit(int morale, double roll)
        => morale <= QuitMoraleThreshold && roll < DailyQuitChance;
}
