// 预订渠道表（架构 §B.4 ChannelPortfolio）。纯 C#，不引用 UnityEngine。
//
// 三个渠道 + 直营构成一组取舍，没有严格更优的选项：
//   流量越大 ⇒ 抽成越高、取消率越高（平台把不确定性转嫁给你）
//   直营零抽成、最稳，但几乎不带流量——它是"口碑"的兑现出口
// 佣金在退房入账时扣（保险箱只收净额，晨报列明细）。
public readonly struct BookingChannel
{
    public readonly int id;
    public readonly string name;          // 游戏内英文
    public readonly float commission;     // 抽成比例
    public readonly float trafficWeight;  // 相对流量权重
    /// <summary>**一张单在到店前取消的总概率**（不是每日概率）。
    /// 每日风险率由 BookingBook 按提前期换算——写成每日会在 13 天提前期下复利成八成取消。</summary>
    public readonly float cancellationRate;
    public readonly float vipBias;        // 带来高端客群的偏置（0=不偏，1=强偏）

    public BookingChannel(int id, string name, float commission, float trafficWeight,
                          float cancellationRate, float vipBias)
    {
        this.id = id;
        this.name = name;
        this.commission = commission;
        this.trafficWeight = trafficWeight;
        this.cancellationRate = cancellationRate;
        this.vipBias = vipBias;
    }
}

public static class BookingChannels
{
    public const int DirectId = 0;
    public const int PlatformAId = 1;
    public const int PlatformBId = 2;
    public const int PlatformCId = 3;

    private static readonly BookingChannel[] All =
    {
        //                       id            name              抽成   流量  总取消率 VIP偏置
        new BookingChannel(DirectId,    "Walk-in & Direct", 0f,    0.15f, 0.02f, 0.1f),
        new BookingChannel(PlatformAId, "MegaBooker",       0.18f, 1.00f, 0.12f, 0.2f),
        new BookingChannel(PlatformBId, "BizTravel Desk",   0.12f, 0.55f, 0.08f, 0.4f),
        new BookingChannel(PlatformCId, "Boutique List",    0.08f, 0.25f, 0.03f, 0.8f),
    };

    /// <summary>把"一张单的总取消率"摊成每日风险率，使整个提前期累计下来正好等于总率。
    ///
    /// 直接把总率当每日率用会出大事：12% × 13 天提前期 ⇒ 存活率 0.88^13 ≈ 19%，
    /// 八成订单在到店前蒸发，酒店永远只有两三成入住率，人手压力与超售玩法双双失效
    /// （M-D 实测：56 天里 40 间房只卖出 10 间/天，抠客房部完全没有代价）。
    /// 1 − (1 − total)^(1/leadDays) 才是对应的每日风险率。</summary>
    public static double DailyCancellationHazard(float totalRate, int leadDays)
    {
        double total = SimMath.Clamp01(totalRate);
        if (total <= 0d) return 0d;
        if (leadDays < 1) return total;          // 明天就到店：只剩一次机会，就是总率
        return 1d - System.Math.Pow(1d - total, 1d / leadDays);
    }

    public static int Count => All.Length;

    public static BookingChannel AtIndex(int index) => All[index];

    /// <summary>取渠道档案。未知 id 回落直营而不是抛异常——存档里可能有被删掉的渠道 id。</summary>
    public static BookingChannel Get(int channelId)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == channelId) return All[i];
        return All[0];
    }
}
