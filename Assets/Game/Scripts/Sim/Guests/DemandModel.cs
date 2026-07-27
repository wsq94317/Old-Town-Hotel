using System;

// 需求模型（架构 B.4 + 修订版 3 客群风险模型）。纯 C#，不引用 UnityEngine。
//
// 取代 v1 的"星级 → 每日客数"直连：现在客量由 价格弹性 × 星级 × 周末 决定，
// 而**价格档改变的是客群混合**，不是简单地"引来差评"。清仓价不是惩罚，
// 是用一种经营压力（清洁负担/噪音）换另一种（入住率/现金流）。

public enum GuestSegment
{
    Budget,    // 预算客：价格敏感，容忍旧装修，但高周转+额外清洁负担
    Business,  // 商务客：要效率，排队惩罚翻倍
    Party,     // 派对客：消费高，但噪音与损坏概率高
    Vip        // VIP：付得多，期待高、差评权重高
}

/// <summary>各客群带来的压力系数（每种客人换一种麻烦）。</summary>
public readonly struct GuestSegmentProfile
{
    public readonly GuestSegment segment;
    public readonly float tierExpectation;        // 期待的房间档位（0=不挑，1=必须翻新过）
    public readonly float waitPenaltyMultiplier;  // check-in 等待的满意度惩罚倍率
    public readonly float extraCleaningLoad;      // 退房后额外清洁负担倍率
    public readonly float incidentMultiplier;     // 事件/损坏概率倍率
    public readonly float satisfactionWeight;     // 差评对星级的权重
    public readonly float spendMultiplier;        // 设施消费倍率

    public GuestSegmentProfile(GuestSegment segment, float tierExpectation, float waitPenaltyMultiplier,
                               float extraCleaningLoad, float incidentMultiplier,
                               float satisfactionWeight, float spendMultiplier)
    {
        this.segment = segment;
        this.tierExpectation = tierExpectation;
        this.waitPenaltyMultiplier = waitPenaltyMultiplier;
        this.extraCleaningLoad = extraCleaningLoad;
        this.incidentMultiplier = incidentMultiplier;
        this.satisfactionWeight = satisfactionWeight;
        this.spendMultiplier = spendMultiplier;
    }

    public static GuestSegmentProfile For(GuestSegment segment)
    {
        switch (segment)
        {
            // tierExpectation 与 ExpectedQualityOf 同一把尺（交付水平 0~1），M-C2 一起下调
            case GuestSegment.Budget:
                return new GuestSegmentProfile(segment, tierExpectation: 0.08f, waitPenaltyMultiplier: 0.6f,
                                               extraCleaningLoad: 1.35f, incidentMultiplier: 1.1f,
                                               satisfactionWeight: 0.8f, spendMultiplier: 0.6f);
            case GuestSegment.Business:
                return new GuestSegmentProfile(segment, tierExpectation: 0.35f, waitPenaltyMultiplier: 2.0f,
                                               extraCleaningLoad: 0.9f, incidentMultiplier: 0.7f,
                                               satisfactionWeight: 1.0f, spendMultiplier: 1.0f);
            case GuestSegment.Party:
                return new GuestSegmentProfile(segment, tierExpectation: 0.20f, waitPenaltyMultiplier: 0.5f,
                                               extraCleaningLoad: 1.6f, incidentMultiplier: 2.2f,
                                               satisfactionWeight: 0.7f, spendMultiplier: 1.8f);
            default: // Vip
                return new GuestSegmentProfile(segment, tierExpectation: 0.70f, waitPenaltyMultiplier: 1.6f,
                                               extraCleaningLoad: 1.0f, incidentMultiplier: 0.8f,
                                               satisfactionWeight: 2.0f, spendMultiplier: 1.6f);
        }
    }
}

/// <summary>某天的客群混合权重（和为 1）。</summary>
public readonly struct SegmentMix
{
    public readonly float budget, business, party, vip;

    public SegmentMix(float budget, float business, float party, float vip)
    {
        float sum = budget + business + party + vip;
        if (sum <= 0f) { this.budget = 1f; this.business = this.party = this.vip = 0f; return; }
        this.budget = budget / sum;
        this.business = business / sum;
        this.party = party / sum;
        this.vip = vip / sum;
    }

    public float WeightOf(GuestSegment s)
    {
        switch (s)
        {
            case GuestSegment.Business: return business;
            case GuestSegment.Party: return party;
            case GuestSegment.Vip: return vip;
            default: return budget;
        }
    }

    /// <summary>按权重挑一个客群（roll 外部注入=可测）。</summary>
    public GuestSegment Pick(double roll)
    {
        double r = SimMath.Clamp01(roll);
        if ((r -= budget) < 0d) return GuestSegment.Budget;
        if ((r -= business) < 0d) return GuestSegment.Business;
        if ((r -= party) < 0d) return GuestSegment.Party;
        return GuestSegment.Vip;
    }
}

/// <summary>需求参数（纯数据 DTO，可从 ScriptableObject 转入）。</summary>
public readonly struct DemandConfig
{
    /// <summary>"位置极好"的基础需求：营业房量 × 这个比例。</summary>
    public readonly float locationBaseOccupancy;
    /// <summary>保底到店人数——硬件再差也有人来，防前期死局（spec §2）。</summary>
    public readonly int guaranteedArrivals;
    /// <summary>随机抖动幅度（±）。</summary>
    public readonly float jitter;
    /// <summary>到店人数上限相对房量的倍数（需求可超房量，到店有物理上限）。</summary>
    public readonly float maxArrivalsFactor;

    public DemandConfig(float locationBaseOccupancy, int guaranteedArrivals, float jitter, float maxArrivalsFactor)
    {
        this.locationBaseOccupancy = locationBaseOccupancy;
        this.guaranteedArrivals = guaranteedArrivals;
        this.jitter = jitter;
        this.maxArrivalsFactor = maxArrivalsFactor;
    }

    public static DemandConfig Default => new DemandConfig(0.55f, 2, 0.15f, 1.4f);

    public int MaxArrivalsFor(int openRooms) =>
        Math.Max(guaranteedArrivals, SimMath.RoundToInt(openRooms * maxArrivalsFactor));
}

public static class DemandModel
{
    /// <summary>价格弹性指数：ε=1.8（清仓 0.7x → ×1.9；榨利润 1.25x → ×0.67）。</summary>
    public const float PriceElasticity = 1.8f;

    /// <summary>等待多少分钟以内不扣满意度（宽容窗口）。
    ///
    /// 从 10 分钟收紧到 5 分钟（试玩调参）：10 分钟宽容 + 每超 10 分钟只扣 0.05 的斜率，
    /// 实测 8 位客人一共只扣 0.06（人均 0.008），等于**排队根本不影响评价**——
    /// 而用户明确要求"CI 等待时间和酒店的评价要相关"。在酒店前台干等 10 分钟
    /// 本来就该不高兴了。</summary>
    public const int WaitGraceMinutes = 5;

    /// <summary>超出宽容窗口后，每 10 分钟的满意度惩罚基准（再乘客群倍率）。
    /// 商务客倍率 2.0，所以等 20 分钟对他是 −0.30，是能感觉到的一记。</summary>
    public const float WaitPenaltyPer10Minutes = 0.10f;

    public static float Elasticity(float priceRatio)
    {
        float r = SimMath.Clamp(priceRatio, 0.2f, 3f); // 防零价爆炸
        return (float)Math.Pow(r, -PriceElasticity);
    }

    /// <summary>星级 → 需求乘数（0.6 ~ 1.6）。评分→客源的正循环。</summary>
    public static float StarsMultiplier(float stars) => 0.6f + 0.2f * SimMath.Clamp(stars, 0f, 5f);

    /// <summary>酒店整体档位 → 需求乘数（1.0 ~ 1.6）。
    /// M-C 调参发现的必要耦合：只让翻新房"每人收更多"的话，需求与房量成正比，
    /// 解锁破房（$800 换 +0.55 客/天）永远比装修（$900 换 +$50/晚但占用率仅 55%）划算，
    /// 装修从核心玩法变成陷阱。现实里更好的酒店本就**更有人来**，不只是更贵。</summary>
    public static float TierMultiplier(float averageTierNormalised) =>
        1f + 0.6f * SimMath.Clamp01(averageTierNormalised);

    public static float WeekendDemandMultiplier(bool weekend) => weekend ? 1.25f : 1f;

    /// <summary>今日到店人数。roll 外部注入（可复现）。
    /// averageTierNormalised：营业房的平均档位归一到 0..1（全 Old=0，全 Better=1）。</summary>
    public static int ArrivalsFor(DemandConfig cfg, int openRooms, float stars, float priceRatio,
                                 bool weekend, double roll, float averageTierNormalised = 0f)
    {
        if (openRooms <= 0) return 0;

        float demand = openRooms
                     * cfg.locationBaseOccupancy
                     * StarsMultiplier(stars)
                     * TierMultiplier(averageTierNormalised)
                     * WeekendDemandMultiplier(weekend)
                     * Elasticity(priceRatio);

        // 抖动：roll 0→−jitter，1→+jitter
        float jitterFactor = 1f + cfg.jitter * (float)(SimMath.Clamp01(roll) * 2d - 1d);
        int arrivals = SimMath.RoundToInt(demand * jitterFactor);

        arrivals = SimMath.Clamp(arrivals, cfg.guaranteedArrivals, cfg.MaxArrivalsFor(openRooms));
        return arrivals;
    }

    /// <summary>客群混合：价格档与周末改变的是"来的是哪种人"。</summary>
    public static SegmentMix SegmentMixFor(float priceRatio, bool weekend)
    {
        float r = SimMath.Clamp(priceRatio, 0.5f, 2f);
        float delta = r - 1f; // 负=便宜，正=贵

        float budget = SimMath.Clamp(0.45f - 0.9f * delta, 0.05f, 0.85f);
        float business = SimMath.Clamp(0.35f + 0.25f * delta, 0.05f, 0.6f);
        float party = SimMath.Clamp((weekend ? 0.22f : 0.08f) - 0.15f * delta, 0.02f, 0.45f);
        float vip = SimMath.Clamp(0.06f + 0.6f * Math.Max(0f, delta), 0.02f, 0.4f);

        return new SegmentMix(budget, business, party, vip);
    }

    /// <summary>等待队列造成的满意度惩罚（按客群放大/缩小）。</summary>
    public static float WaitSatisfactionPenalty(GuestSegment segment, int waitMinutes)
    {
        int over = waitMinutes - WaitGraceMinutes;
        if (over <= 0) return 0f;
        var profile = GuestSegmentProfile.For(segment);
        return WaitPenaltyPer10Minutes * (over / 10f) * profile.waitPenaltyMultiplier;
    }

    /// <summary>"榨利润"抬高期待：价格越高于市场，满意度门槛越高。</summary>
    public static float ExpectationPenalty(float priceRatio)
    {
        float over = priceRatio - 1f;
        return over <= 0f ? 0f : 0.2f * over;
    }

    /// <summary>挂牌档承诺的品质水平。RoomTier 现在表示"你声称它有多好"，
    /// 实际交付由家具装饰度决定（家具系统设计 §2）。
    ///
    /// **M-C2 调参**：期待值必须落在家具真能提供的范围内，否则档位就是骗局。
    /// 三段刚好对应三个投资阶段，玩家一看就懂：
    ///   Old   0.05 ← 继承的破家具（床+卫浴，崭新度 10%）交付约 0.05
    ///   Basic 0.35 ← 标准装修（必备升一档 + 崭新度回满）交付约 0.35
    ///   Better 0.70 ← 装修 + 摆两三件可选家具
    /// 早期版本用 0.10/0.55/0.90，结果连翻新过的房都够不上 Basic，装完也只能按老房价卖。</summary>
    public static float ExpectedQualityOf(RoomTier band)
    {
        switch (band)
        {
            case RoomTier.Better: return 0.70f;
            case RoomTier.Basic: return 0.35f;
            default: return 0.05f;
        }
    }

    /// <summary>挂牌 vs 交付的满意度增减（**双向**）。
    /// 超预期是惊喜但有上限；虚报的失望扣得更狠——人对失望的反应比对惊喜强烈。</summary>
    public static float PriceBandSatisfactionDelta(float deliveredQuality, RoomTier band)
    {
        float gap = SimMath.Clamp01(deliveredQuality) - ExpectedQualityOf(band);
        if (gap >= 0f)
        {
            float bonus = gap * 0.30f;
            return bonus > 0.25f ? 0.25f : bonus;
        }
        return gap * 0.50f;   // gap 为负，直接返回负值
    }

    /// <summary>客人个人标准没被满足的惩罚（VIP 对同一间房要求比预算客高）。</summary>
    public static float SegmentDisappointment(GuestSegment segment, float deliveredQuality)
    {
        float expected = GuestSegmentProfile.For(segment).tierExpectation;
        float gap = expected - SimMath.Clamp01(deliveredQuality);
        return gap <= 0f ? 0f : 0.3f * gap;
    }

    /// <summary>家具对上客群口味的额外满意度（有上限，避免堆家具刷满分）。</summary>
    public static float AppealBonus(float appealSum)
    {
        float bonus = SimMath.Clamp(appealSum, 0f, 10f) * 0.05f;
        return bonus > 0.15f ? 0.15f : bonus;
    }

    /// <summary>虚报价触发退款申请的概率。落差超过 0.20 才开始，最高 80%。</summary>
    public static double RefundChanceFor(float deliveredQuality, RoomTier band)
    {
        float gap = SimMath.Clamp01(deliveredQuality) - ExpectedQualityOf(band);
        if (gap >= -0.20f) return 0d;
        return SimMath.Clamp((-gap - 0.20f) * 2f, 0f, 0.8f);
    }
}
