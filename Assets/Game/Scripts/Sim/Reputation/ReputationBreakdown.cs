using System.Collections.Generic;

// 当日声誉明细：**今天为什么涨、为什么掉**。纯 C#，不引用 UnityEngine。
//
// 星级本身是一个 20 人滑动窗口的平均值，玩家看着它上下动却完全不知道原因——
// 试玩反馈的原话是"没玩明白"。满意度是由六七项加减算出来的（挂牌 vs 交付、
// 排队、瑕疵房、客群标准、家具讨喜……），这些项本来算完就扔了。
// 这个账本把每一项按原因累加起来，晨报据此告诉玩家"排队害了你 −0.42，
// 家具讨喜帮了你 +0.15"。
//
// 记的是**相对中性（1.0）的偏移量**：正数是加分项，负数是扣分项。
public enum ReputationCause
{
    PriceExpectation,   // 定价模板高于市场：客人本来就带着更高期待进门
    BandVsDelivered,    // 挂牌档 vs 实际交付（双向：虚报扣分，超额交付加分）
    SegmentStandards,   // 客群自己的标准（VIP 挑剔）
    QueueWait,          // 前台排队
    FlawedRoom,         // 没验房就上架
    FurnitureAppeal,    // 家具对上了客群口味
    RefundRefused,      // 退款申请被拒
    RefundIgnored,      // 退款申请拖到日结
    OverbookingWalked,  // 超售客被赶走
    OverbookingPaid,    // 超售客赔钱送走
    OverbookingUpgraded,// 超售客升级安顿（正面）
    NightDeskClosed,    // 夜里到店发现前台没人（比"没来"更伤：人家真到了门口）
    NightDeskSaved,     // 夜班前台通宵接住了客人（正面）
    GuestInjured        // 家具塌了压伤客人（送医 + 赔偿）
}

public sealed class ReputationBreakdown
{
    public readonly struct Entry
    {
        public readonly ReputationCause cause;
        public readonly float total;   // 该原因当日累计偏移（正=加分）
        public readonly int count;     // 发生了几次

        public Entry(ReputationCause cause, float total, int count)
        {
            this.cause = cause;
            this.total = total;
            this.count = count;
        }
    }

    private readonly Dictionary<ReputationCause, float> _totals = new Dictionary<ReputationCause, float>();
    private readonly Dictionary<ReputationCause, int> _counts = new Dictionary<ReputationCause, int>();

    /// <summary>记一笔。delta 为相对中性的偏移（正=加分，负=扣分）；0 也计次数，
    /// 因为"发生了但没造成影响"本身是有用的信息（比如宽容窗口内的短暂排队）。</summary>
    public void Add(ReputationCause cause, float delta)
    {
        _totals.TryGetValue(cause, out float t);
        _totals[cause] = t + delta;
        _counts.TryGetValue(cause, out int c);
        _counts[cause] = c + 1;
    }

    public float TotalOf(ReputationCause cause) =>
        _totals.TryGetValue(cause, out float t) ? t : 0f;

    public int CountOf(ReputationCause cause) =>
        _counts.TryGetValue(cause, out int c) ? c : 0;

    /// <summary>所有加分项之和。</summary>
    public float PositiveTotal
    {
        get
        {
            float sum = 0f;
            foreach (var kv in _totals) if (kv.Value > 0f) sum += kv.Value;
            return sum;
        }
    }

    /// <summary>所有扣分项之和（负数）。</summary>
    public float NegativeTotal
    {
        get
        {
            float sum = 0f;
            foreach (var kv in _totals) if (kv.Value < 0f) sum += kv.Value;
            return sum;
        }
    }

    /// <summary>按影响绝对值从大到小排（晨报只展示前几项，所以顺序要稳）。
    /// 同样大小时按枚举序，保证同种子两次跑出来的报告一模一样。</summary>
    public List<Entry> Ranked(bool positive)
    {
        var list = new List<Entry>();
        foreach (var kv in _totals)
        {
            if (positive && kv.Value <= 0f) continue;
            if (!positive && kv.Value >= 0f) continue;
            list.Add(new Entry(kv.Key, kv.Value, CountOf(kv.Key)));
        }
        list.Sort((a, b) =>
        {
            float ma = a.total < 0f ? -a.total : a.total;
            float mb = b.total < 0f ? -b.total : b.total;
            int byMagnitude = mb.CompareTo(ma);
            return byMagnitude != 0 ? byMagnitude : ((int)a.cause).CompareTo((int)b.cause);
        });
        return list;
    }

    public void Reset()
    {
        _totals.Clear();
        _counts.Clear();
    }

    /// <summary>玩家看得懂的短标签（游戏内英文，UI 层再过一次翻译）。</summary>
    public static string LabelOf(ReputationCause cause)
    {
        switch (cause)
        {
            case ReputationCause.PriceExpectation: return "Priced above market";
            case ReputationCause.BandVsDelivered: return "What you promised vs delivered";
            case ReputationCause.SegmentStandards: return "Guests' own standards";
            case ReputationCause.QueueWait: return "Queue at the desk";
            case ReputationCause.FlawedRoom: return "Rooms sold unchecked";
            case ReputationCause.FurnitureAppeal: return "Furniture they liked";
            case ReputationCause.RefundRefused: return "Refunds refused";
            case ReputationCause.RefundIgnored: return "Refunds left hanging";
            case ReputationCause.OverbookingWalked: return "Overbooked guests sent away";
            case ReputationCause.OverbookingPaid: return "Overbooked guests paid off";
            case ReputationCause.OverbookingUpgraded: return "Overbooked guests upgraded";
            case ReputationCause.NightDeskClosed: return "Locked door at 1am";
            case ReputationCause.NightDeskSaved: return "Night desk took them in";
            default: return "A guest got hurt";
        }
    }
}
