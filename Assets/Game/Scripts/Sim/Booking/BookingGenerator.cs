using System;
using System.Collections.Generic;

// 预订生成（架构 §B.4）。纯 C#，不引用 UnityEngine。掷骰外部注入 = 同种子可复现。
//
// 需求怎么变成订单：
//   1. DemandModel 给出某日的总需求（弹性 × 星级 × 交付水平 × 周末）
//   2. 切一刀：大部分是**提前预订**，剩下 20% 是当天上门的 walk-in
//      （walk-in 保留为"剩余需求流"，喂巡查层演出——不是主流量）
//   3. 提前预订按渠道流量权重分配：平台 A 带来的人最多，抽成与取消率也最高
//   4. 每张单挑一个客群，再从**酒店真正挂出来的档位里**挑一个最接近其期待的
//      ——只卖你挂出来的东西：全店都挂 Old，来的人就都只订 Old
public readonly struct BookingIntent
{
    public readonly int channelId;
    public readonly int arrivalDay;
    public readonly int nights;
    public readonly RoomTier tier;
    public readonly GuestSegment segment;

    public BookingIntent(int channelId, int arrivalDay, int nights, RoomTier tier, GuestSegment segment)
    {
        this.channelId = channelId;
        this.arrivalDay = arrivalDay;
        this.nights = nights;
        this.tier = tier;
        this.segment = segment;
    }
}

public static class BookingGenerator
{
    /// <summary>当天上门的比例（其余为提前预订）。</summary>
    public const float WalkInShare = 0.20f;

    /// <summary>预订视野：只放到未来这么多天（含今天）。</summary>
    public const int HorizonDays = 14;

    /// <summary>一张单最多住几晚（NightsFor 的上限）。
    /// 容量表必须比视野多铺这么多天，否则**视野最后一天的连住单全部被拒**：
    /// CanAccept 要求每一晚都有位，而视野之外的天容量从未设置（=0），
    /// 于是三晚单永远接不下来，簿子一天天变薄且只剩单晚客（M-D 实测抓到）。</summary>
    public const int MaxNights = 3;

    public static int WalkInDemandFor(int totalDemand) =>
        totalDemand <= 0 ? 0 : SimMath.RoundToInt(totalDemand * WalkInShare);

    public static int AdvanceDemandFor(int totalDemand)
    {
        if (totalDemand <= 0) return 0;
        int advance = totalDemand - WalkInDemandFor(totalDemand);
        return advance < 0 ? 0 : advance;
    }

    /// <summary>某客群最想订哪一档：在**酒店实际挂出的档位**里取最接近其期待的那个。
    /// 挂得越高，来的人订得越高——也就越容易在交付不足时吃退款（M-C2 的枢纽机制）。</summary>
    public static RoomTier PreferredBandFor(GuestSegment segment, IList<RoomTier> offeredBands)
    {
        if (offeredBands == null || offeredBands.Count == 0) return RoomTier.Old;

        float wanted = GuestSegmentProfile.For(segment).tierExpectation;
        RoomTier best = offeredBands[0];
        float bestGap = float.MaxValue;
        for (int i = 0; i < offeredBands.Count; i++)
        {
            RoomTier band = offeredBands[i];
            float gap = Math.Abs(DemandModel.ExpectedQualityOf(band) - wanted);
            // 同样接近时取更便宜的一档：客人不会主动多付钱
            if (gap > bestGap || (gap == bestGap && (int)band >= (int)best)) continue;
            bestGap = gap;
            best = band;
        }
        return best;
    }

    /// <summary>住几晚：多数一晚，商务/VIP 更可能连住（连住会占多个间夜 = 库存压力）。</summary>
    public static int NightsFor(GuestSegment segment, double roll)
    {
        double r = SimMath.Clamp01(roll);
        bool longStayer = segment == GuestSegment.Business || segment == GuestSegment.Vip;
        if (longStayer)
        {
            if (r < 0.45d) return 1;
            if (r < 0.80d) return 2;
            return 3;
        }
        if (r < 0.75d) return 1;
        if (r < 0.95d) return 2;
        return 3;
    }

    /// <summary>把某日的提前预订需求摊成一张张订单意图（还没占库存，调用方决定接不接）。</summary>
    public static List<BookingIntent> IntentsFor(int targetDay, int advanceDemand, SegmentMix mix,
                                                 IList<RoomTier> offeredBands, Func<double> roll)
    {
        var intents = new List<BookingIntent>();
        if (advanceDemand <= 0 || roll == null) return intents;

        float totalWeight = 0f;
        for (int i = 0; i < BookingChannels.Count; i++)
            totalWeight += BookingChannels.AtIndex(i).trafficWeight;
        if (totalWeight <= 0f) return intents;

        for (int i = 0; i < BookingChannels.Count; i++)
        {
            BookingChannel channel = BookingChannels.AtIndex(i);
            int count = SimMath.RoundToInt(advanceDemand * (channel.trafficWeight / totalWeight));
            for (int k = 0; k < count; k++)
            {
                GuestSegment segment = mix.Pick(roll());
                // 渠道的高端偏置：精品渠道更容易带来 VIP（掷第二次骰，不复用客群那一次）
                if (channel.vipBias > 0f && roll() < channel.vipBias * 0.35f) segment = GuestSegment.Vip;

                intents.Add(new BookingIntent(channel.id, targetDay,
                                              NightsFor(segment, roll()),
                                              PreferredBandFor(segment, offeredBands),
                                              segment));
            }
        }
        return intents;
    }
}
