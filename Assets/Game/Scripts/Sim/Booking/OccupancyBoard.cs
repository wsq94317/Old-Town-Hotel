using System.Collections.Generic;

// 入住情况看板（玩家要求："今天10间房可以卖，卖了6间，要显示成 6/10"
// 以及"未来搞成一个日历UI，每天都是已售/全部"）。纯 C#，不引用 UnityEngine。
//
// **今晚和未来的"已售"不是同一个数，这一点必须在代码里说清楚**，否则又会出现
// 上一轮那种"两个数字看起来自相矛盾"的困惑（玩家原话："UI 看是可售有几个，
// 但是结算的时候说没房可卖"）：
//
//   今晚已售 = 在住的房间数（含上门客与连住客）+ 今天还没到店的预订数
//              —— 也就是"今晚会有人睡在里面的房间数"。
//   未来已售 = 那一晚卖出的**间夜**（预订簿里还活着的单，含已入住的连住客）
//              —— 上门客还没发生，未来不可能知道，所以未来只算预订。
//
// 分母都是**那一天的库存容量**：非破败、且当天不在装修中的房间数。
// 用日历的容量而不是"此刻 Ready 的间数"——后者一天之内会随打扫/入住上下跳，
// 拿它当分母的话玩家会看到分母自己在动，那比没有这个数字更糟。
public readonly struct NightOccupancy
{
    public readonly int day;
    public readonly int sold;
    public readonly int capacity;

    /// <summary>今晚（分子含上门客）还是未来某晚（分子只有预订）。
    /// UI 要据此换措辞，不然两种口径混在一张表里没人看得懂。</summary>
    public readonly bool isTonight;

    /// <summary>这一晚**真的一间都接不了了**。
    ///
    /// 刻意不是 `sold >= capacity` 算出来的：三个档位是**各自独立的库存**，
    /// Old 超售 2 间而 Better 空着 2 间时，总数看着刚好满，其实还能卖 Better。
    /// 所以这个字段直接取 `HotelSim.FullyBookedNights()` 的判据（每一档的
    /// remaining 都 ≤ 0），跟拒单提示同一个源头——**上一轮玩家的困惑正是
    /// 两个数字各算一遍**，同源才不会再自相矛盾。</summary>
    public readonly bool soldOut;

    public NightOccupancy(int day, int sold, int capacity, bool isTonight, bool soldOut)
    {
        this.day = day;
        this.sold = sold;
        this.capacity = capacity;
        this.isTonight = isTonight;
        this.soldOut = soldOut;
    }

    /// <summary>还能卖几间（超售时返回 0，不返回负数——负数要玩家自己换算）。</summary>
    public int Left => capacity - sold < 0 ? 0 : capacity - sold;

    public bool IsFull => soldOut;

    /// <summary>超售了几间（0 = 没超）。日历上要标出来，那是要处置的事件。</summary>
    public int Oversold => sold - capacity > 0 ? sold - capacity : 0;

    /// <summary>入住率 0-1（容量为 0 时返回 0，不做除零）。</summary>
    public float Rate => capacity <= 0 ? 0f : (float)sold / capacity;

    /// <summary>"6/10" —— 玩家点名要的那个形状。</summary>
    public string Fraction => sold + "/" + capacity;
}

public static class OccupancyBoard
{
    /// <summary>日历默认铺几天（一周 + 今天，正好一屏放得下）。</summary>
    public const int DefaultDays = 8;

    /// <summary>这一串夜晚的整体入住率（0-100 的整数）。
    /// **刻意只返回数字而不返回句子**：Sim 层不能引用 GameText，
    /// 在这里拼英文串等于给自己留一处永远漏译的文案。措辞归 UI。</summary>
    public static int PercentBookedAcross(IReadOnlyList<NightOccupancy> nights)
    {
        if (nights == null || nights.Count == 0) return 0;
        int sold = 0, capacity = 0;
        for (int i = 0; i < nights.Count; i++)
        {
            sold += nights[i].sold;
            capacity += nights[i].capacity;
        }
        return capacity <= 0 ? 0 : SimMath.RoundToInt(sold * 100f / capacity);
    }

    /// <summary>这一串里订满了几晚。</summary>
    public static int FullNightsAcross(IReadOnlyList<NightOccupancy> nights)
    {
        if (nights == null) return 0;
        int full = 0;
        for (int i = 0; i < nights.Count; i++)
            if (nights[i].IsFull) full++;
        return full;
    }
}
