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
    public readonly float cancellationRate; // 每日取消概率
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
        //                       id            name              抽成   流量  取消率  VIP偏置
        new BookingChannel(DirectId,    "Walk-in & Direct", 0f,    0.15f, 0.02f, 0.1f),
        new BookingChannel(PlatformAId, "MegaBooker",       0.18f, 1.00f, 0.12f, 0.2f),
        new BookingChannel(PlatformBId, "BizTravel Desk",   0.12f, 0.55f, 0.08f, 0.4f),
        new BookingChannel(PlatformCId, "Boutique List",    0.08f, 0.25f, 0.03f, 0.8f),
    };

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
