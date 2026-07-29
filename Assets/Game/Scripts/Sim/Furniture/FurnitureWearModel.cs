// 家具磨损/故障模型（设计文档 §1/§3/§4）。纯静态函数，不引用 UnityEngine。
//
// **崭新度与健康度是两个概念，严格解耦**（用户第二轮明确要求）：
//   崭新度 newness = 还剩多少寿命/价值。只按衰减走，**维修一动不动**，
//                    只有翻新性装修（必须 Block 房间）能重置。
//   健康度 health  = 现在能不能正常用。维修能回，但**回不过这件家具剩下的寿命**
//                    （RepairedHealthCeiling）——修一张破床只能修成一张能用的破床。
//   崭新度低 ⇒ 健康度掉得快（最多 6 倍）⇒ 故障频繁。
// 于是"修理是买时间，换新才是根治"是字面意义上能算的账。
public static class FurnitureWearModel
{
    /// <summary>新家具每客人晚的健康度损耗基准。</summary>
    public const float BaseHealthLossPerNight = 0.012f;

    /// <summary>空置也会松动：每日健康度损耗。</summary>
    public const float IdleHealthLossPerDay = 0.0005f;

    /// <summary>空置的日历老化系数（相对一个客人晚）。堵"故意空置保鲜家具"的漏洞。</summary>
    public const float IdleNewnessFactor = 0.02f;

    /// <summary>健康度低于此值开始频繁出故障。</summary>
    public const float TroubleThreshold = 0.30f;

    /// <summary>健康良好时的日故障概率（意外总会有）。</summary>
    public const double HealthyFaultChance = 0.005d;

    /// <summary>装饰度的崭新度折扣下限：再旧的家具（能用的话）也还剩四成体面。</summary>
    public const float DecorFloorAtZeroNewness = 0.4f;

    /// <summary>残值比例（用户要求"不要太高"）。</summary>
    public const float SalvageRate = 0.20f;

    /// <summary>**修好之后的健康度上限 = 这件家具剩下的寿命**（崭新度）。
    ///
    /// 这一条把"修理是买时间，换新才是根治"从口号变成了一个能算的数：
    /// 崭新度 0.10 的破床修完只能到 0.10，**仍在故障线（0.30）以下**，
    /// 于是既有的 FaultChancePerDay 已经落在陡峭段、HealthLossPerNight 已经在
    /// 5 倍速上跑——"修好的床几天之内还会塌"是现有曲线的自然结果，
    /// 不需要新加脆弱标记、修理次数或第二套计时器（用户口述的需求，实现成一个函数）。
    /// 崭新度 0.8 的床修完回到 0.8，照旧健康——**爱惜家具的人不该被惩罚**。
    ///
    /// 下限 0.05：崭新度归零的家具修完也还能撑一下，因为它必须仍然可修——
    /// 现金归零时"修一下顶两天"是玩家的自救路之一（同胶带、同清垃圾免费）。
    public const float RepairedHealthFloor = 0.05f;

    public static float RepairedHealthCeiling(float newness) =>
        SimMath.Clamp(SimMath.Clamp01(newness), RepairedHealthFloor, 1f);

    /// <summary>健康度这么低的承重家具会**塌**（不只是坏）。塌了会伤到人。</summary>
    public const float CollapseHealthThreshold = 0.12f;

    /// <summary>塌掉的日概率上限。刻意不高：这该是"记得住的事故"，不是每晚抽奖。</summary>
    public const double MaxCollapseChancePerNight = 0.25d;

    /// <summary>只糊了胶带的承重家具塌得更容易（胶带撑不住一个人的体重）。</summary>
    public const double TapedCollapseFactor = 2.5d;

    /// <summary>这一晚这件承重家具塌掉的概率。
    /// 健康度在阈值之上返回 0——**经营良好的酒店永远见不到这套机制**，
    /// 它只惩罚"一直用胶带糊着不换"。</summary>
    public static double CollapseChancePerNight(float health, bool taped)
    {
        float h = SimMath.Clamp01(health);
        if (h >= CollapseHealthThreshold) return 0d;

        double severity = (CollapseHealthThreshold - h) / CollapseHealthThreshold;
        double chance = MaxCollapseChancePerNight * severity;
        if (taped) chance *= TapedCollapseFactor;
        return chance > MaxCollapseChancePerNight ? MaxCollapseChancePerNight : chance;
    }

    /// <summary>崭新度 → 健康度衰减倍率。崭新 1x，报废 6x。
    /// 平方曲线：前期几乎无感，快报废时急剧恶化——老酒店"忽然什么都开始坏"的手感。</summary>
    public static float WearMultiplier(float newness)
    {
        float aged = 1f - SimMath.Clamp01(newness);
        return 1f + 5f * aged * aged;
    }

    /// <summary>一个客人晚的崭新度损耗。</summary>
    public static float NewnessLossPerNight(int lifespanGuestNights, float segmentWear)
    {
        if (lifespanGuestNights <= 0) return 1f;
        return 1f / lifespanGuestNights * segmentWear;
    }

    /// <summary>一个客人晚的健康度损耗（崭新度越低掉得越快）。</summary>
    public static float HealthLossPerNight(float newness, float segmentWear) =>
        BaseHealthLossPerNight * WearMultiplier(newness) * segmentWear;

    /// <summary>空置一天的崭新度损耗。</summary>
    public static float NewnessLossPerIdleDay(int lifespanGuestNights)
    {
        if (lifespanGuestNights <= 0) return 0f;
        return IdleNewnessFactor / lifespanGuestNights;
    }

    /// <summary>空置一天的健康度损耗。</summary>
    public static float HealthLossPerIdleDay(float newness) =>
        IdleHealthLossPerDay * WearMultiplier(newness);

    /// <summary>某健康度下的日故障概率。健康度归零时 16%/天。</summary>
    public static double FaultChancePerDay(float health)
    {
        float h = SimMath.Clamp01(health);
        if (h >= TroubleThreshold) return HealthyFaultChance;
        return 0.01d + 0.15d * ((TroubleThreshold - h) / TroubleThreshold);
    }

    /// <summary>有效装饰度：旧家具就算能用也不体面；故障中的记 0。
    /// 这条把崭新度直接接上"挂牌档 vs 交付"的定价玩法。</summary>
    public static float EffectiveDecor(int decorPoints, float newness, bool faulted)
    {
        if (faulted) return 0f;
        float factor = DecorFloorAtZeroNewness + (1f - DecorFloorAtZeroNewness) * SimMath.Clamp01(newness);
        return decorPoints * factor;
    }

    /// <summary>卖掉家具能收回多少（残值压得很低）。</summary>
    public static int SalvageValue(int cashCost, float newness) =>
        SimMath.RoundToInt(cashCost * SimMath.Clamp01(newness) * SalvageRate);
}
