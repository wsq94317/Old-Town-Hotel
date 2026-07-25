// 家具磨损/故障模型（设计文档 §1/§3/§4）。纯静态函数，不引用 UnityEngine。
//
// **崭新度与健康度是两个概念，严格解耦**（用户第二轮明确要求）：
//   崭新度 newness = 还剩多少寿命/价值。只按衰减走，**维修一动不动**，
//                    只有翻新性装修（必须 Block 房间）能重置。
//   健康度 health  = 现在能不能正常用。**维修即可回满**。
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
