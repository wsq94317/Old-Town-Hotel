using System;
using System.Collections.Generic;

// 客人不是"进屋睡觉直到退房"——那样大堂永远是空的，酒店看着像停尸房。
// 真实的住客一天要进出好几趟：出去吃饭、出去办事、深夜回来。
// 这里排的就是每位住客当天的**外出行程**（纯逻辑，随机可注入，全测）。
//
// 四类客人各有作息，这是"每种客人换一种麻烦"在表现层的延续：
//   Budget   出去找便宜饭，趟数多、每趟短（大堂人来人往最勤）
//   Business 白天出去办事，一趟很长（白天大堂空，傍晚一起回来）
//   Party    傍晚才出门，深夜才回来，一趟极长（夜里前台最忙）
//   Vip      很少出门（酒店里就该有他要的一切），出去也只是短暂露面

/// <summary>一次外出：几点出门、几点回来（游戏内分钟，与 SimClock 同一把尺）。</summary>
public struct GuestOuting
{
    public int leaveMinute;
    public int returnMinute;

    public GuestOuting(int leaveMinute, int returnMinute)
    {
        this.leaveMinute = leaveMinute;
        this.returnMinute = returnMinute;
    }

    public bool IsOutAt(int minute) => minute >= leaveMinute && minute < returnMinute;
}

public static class GuestOutingPlan
{
    /// <summary>每位客人当天最多几趟（表现层的上限，防止大堂被同一个人刷屏）。</summary>
    public const int MaxOutingsPerDay = 3;

    /// <summary>客人回房后至少待多久才会再出门（游戏内分钟）。
    /// 没有这个下限的话两趟会挨在一起，看着像客人在门口鬼畜。</summary>
    public const int MinMinutesInRoomBetweenOutings = 45;

    /// <summary>一趟外出至少多久才值得演（走出去再走回来本身就要时间）。
    /// **这个下限是"傍晚也有人气"的关键**：入住高峰在下午，按整趟时长排的话
    /// 17:00 之后住进来的客人一趟都排不进 22:00 之前，当天大堂就此死寂——
    /// 窗口不够就退化成"出去吃个快餐"，而不是干脆不出门。</summary>
    public const int MinOutingMinutes = 30;

    /// <summary>排出这位客人今天的外出行程。
    ///
    /// <paramref name="checkInMinute"/> 入住时刻——不能安排在入住之前出门（放行李要时间）。
    /// <paramref name="lastReturnMinute"/> 最晚必须回来的时刻（打烊结账要在房里找到人）。</summary>
    public static List<GuestOuting> For(GuestSegment segment, int checkInMinute, int lastReturnMinute,
                                        Func<double> nextRandom)
    {
        var plan = new List<GuestOuting>();
        if (nextRandom == null) return plan;

        Profile profile = ProfileFor(segment);

        // 放好行李才出门：给一段"安顿时间"，否则客人前脚进房后脚就走，很傻
        int earliest = checkInMinute + profile.settleInMinutes;
        int cursor = earliest;

        for (int i = 0; i < profile.maxOutings && i < MaxOutingsPerDay; i++)
        {
            if (nextRandom() > profile.outingChance) continue;

            int wait = profile.minGapMinutes
                     + (int)(nextRandom() * (profile.maxGapMinutes - profile.minGapMinutes));
            int leave = cursor + wait;

            int duration = profile.minOutMinutes
                         + (int)(nextRandom() * (profile.maxOutMinutes - profile.minOutMinutes));

            // 剩下的时间不够走完整趟就缩短它（快餐而不是大餐）——
            // 打烊结账时客人必须在房里，否则退房动画会从空房里冒人
            int window = lastReturnMinute - leave;
            if (window < MinOutingMinutes) break;
            if (duration > window) duration = window;

            int back = leave + duration;

            plan.Add(new GuestOuting(leave, back));
            cursor = back + MinMinutesInRoomBetweenOutings;
        }

        return plan;
    }

    /// <summary>这位客人在这一刻是不是不在房间里。</summary>
    public static bool IsOutAt(IReadOnlyList<GuestOuting> plan, int minute)
    {
        if (plan == null) return false;
        for (int i = 0; i < plan.Count; i++)
            if (plan[i].IsOutAt(minute)) return true;
        return false;
    }

    private struct Profile
    {
        public int settleInMinutes;   // 放行李、洗把脸
        public int maxOutings;
        public double outingChance;   // 每一趟真的成行的概率
        public int minGapMinutes, maxGapMinutes;   // 上一次回房到下一次出门的间隔
        public int minOutMinutes, maxOutMinutes;   // 在外面待多久
    }

    private static Profile ProfileFor(GuestSegment segment)
    {
        switch (segment)
        {
            case GuestSegment.Business:
                // 出去办事：一趟顶别人三趟，白天大堂因此空得有道理
                return new Profile
                {
                    settleInMinutes = 20, maxOutings = 2, outingChance = 0.85d,
                    minGapMinutes = 30, maxGapMinutes = 90,
                    minOutMinutes = 150, maxOutMinutes = 300,
                };

            case GuestSegment.Party:
                // 傍晚才出门，深夜才回来——夜里的前台最热闹就是这群人造的
                return new Profile
                {
                    settleInMinutes = 45, maxOutings = 2, outingChance = 0.9d,
                    minGapMinutes = 120, maxGapMinutes = 240,
                    minOutMinutes = 180, maxOutMinutes = 330,
                };

            case GuestSegment.Vip:
                // 酒店里就该有他要的一切，出门只是短暂露面
                return new Profile
                {
                    settleInMinutes = 30, maxOutings = 1, outingChance = 0.5d,
                    minGapMinutes = 60, maxGapMinutes = 180,
                    minOutMinutes = 60, maxOutMinutes = 120,
                };

            default:
                // Budget：出去找便宜饭，趟数最多、每趟最短，大堂人来人往主要靠他们
                return new Profile
                {
                    settleInMinutes = 15, maxOutings = 3, outingChance = 0.8d,
                    minGapMinutes = 20, maxGapMinutes = 70,
                    minOutMinutes = 45, maxOutMinutes = 120,
                };
        }
    }
}
