// 信用评级与还款违约（用户让我定惩罚方案）。纯 C#，不引用 UnityEngine。
//
// 还贷从"自动按净利分成扣"改成**玩家亲手按**（用户要求：要肉疼）。
// 一旦能不按，就必须有不按的代价，否则最优解永远是永不还款。
//
// 惩罚阶梯（我的设计，三条原则）：
//   ① **先罚未来，再罚现在**。逾期第一刀砍的是利率和信用评级，不是当场没收现金——
//      当场抽钱会重演 §C3 那台"抽水泵"（玩家攒不出任何首付，做什么都一样）。
//   ② **每一档都看得见**。评级是一个词（Good/Shaky/Bad/Blacklisted），
//      利率涨幅写在脸上，玩家能预判下一刀。
//   ③ **永远留一条自救路**。评级最差也只是借不到新钱 + 利率封顶，
//      绝不做成即死（和"胶带糊家具""清垃圾免费"同一条铁律）。
public enum CreditRating
{
    Good,        // 按时还款
    Shaky,       // 逾期过，银行开始盯着你
    Bad,         // 屡次逾期：借新钱很贵很难
    Blacklisted  // 没人再借给你了（但游戏继续——靠经营自己爬出来）
}

public static class CreditPolicy
{
    /// <summary>每次逾期，日利率**加**多少个绝对点数。
    /// 用加法而不是乘法：连续三次逾期不该把利率翻成 8 倍（那是即死）。</summary>
    public const float PenaltyRateStep = 0.0002f;   // 约 +0.6%/月

    /// <summary>惩罚利率的天花板：再怎么烂也不能变成高利贷绞索。</summary>
    public const float MaxPenaltyRate = 0.0012f;    // 约 +3.6%/月

    /// <summary>攒到几次逾期掉到下一档。</summary>
    public const int MissesForShaky = 1;
    public const int MissesForBad = 3;
    public const int MissesForBlacklist = 6;

    public static CreditRating RatingFor(int missedPayments)
    {
        if (missedPayments >= MissesForBlacklist) return CreditRating.Blacklisted;
        if (missedPayments >= MissesForBad) return CreditRating.Bad;
        if (missedPayments >= MissesForShaky) return CreditRating.Shaky;
        return CreditRating.Good;
    }

    /// <summary>逾期累积出的惩罚日利率（加在原始利率上，有上限）。
    ///
    /// **第一次逾期只警告，不涨息**：Play 实测里第一天日结一过评级就掉了，
    /// 而玩家那时还没搞懂晨报上有个还款按钮——在人看懂规则之前就扣钱是设计事故。
    /// 评级照样掉到 Shaky（一个看得见的警告），钱包从第二次起才疼。</summary>
    public static float PenaltyRateFor(int missedPayments)
    {
        int chargeable = missedPayments - GraceMisses;
        if (chargeable <= 0) return 0f;
        float rate = PenaltyRateStep * chargeable;
        return rate > MaxPenaltyRate ? MaxPenaltyRate : rate;
    }

    /// <summary>头几次逾期不涨息（只掉评级作为警告）。</summary>
    public const int GraceMisses = 1;

    /// <summary>这个评级还能借到本金的多少倍空间（0 = 借不到）。
    /// 评级差不是"不能玩了"，是"这条路走不通了，去走经营那条"。</summary>
    public static float BorrowingHeadroomFor(CreditRating rating)
    {
        switch (rating)
        {
            case CreditRating.Good: return 1f;
            case CreditRating.Shaky: return 0.6f;
            case CreditRating.Bad: return 0.25f;
            default: return 0f;
        }
    }

    public static string LabelOf(CreditRating rating)
    {
        switch (rating)
        {
            case CreditRating.Good: return "GOOD - the bank likes you";
            case CreditRating.Shaky: return "SHAKY - they are watching";
            case CreditRating.Bad: return "BAD - new money costs a fortune";
            default: return "BLACKLISTED - nobody will lend to you";
        }
    }
}

/// <summary>贷款的信用状态：逾期次数 → 评级 → 惩罚利率与借贷额度。</summary>
public sealed class CreditStanding
{
    public int MissedPayments { get; private set; }

    /// <summary>今天该还的钱付清了吗（UI 靠它决定要不要亮红）。</summary>
    public bool PaidToday { get; private set; }

    public CreditRating Rating => CreditPolicy.RatingFor(MissedPayments);

    /// <summary>原始利率之上要加的惩罚利率。</summary>
    public float PenaltyRate => CreditPolicy.PenaltyRateFor(MissedPayments);

    /// <summary>玩家按了还款。</summary>
    public void RecordPayment() => PaidToday = true;

    /// <summary>一天过去了。当天没还就记一次逾期——**只在真的欠钱时**记，
    /// 债务已清或今天不必还款的日子不算逾期。</summary>
    public void CloseDay(bool paymentWasDue)
    {
        if (paymentWasDue && !PaidToday) MissedPayments++;
        PaidToday = false;
    }

    /// <summary>还能借多少（按当前评级给的额度空间）。</summary>
    public int BorrowingLimitFor(int baseLimit)
    {
        if (baseLimit <= 0) return 0;
        return SimMath.RoundToInt(baseLimit * CreditPolicy.BorrowingHeadroomFor(Rating));
    }

    public void Clear()
    {
        MissedPayments = 0;
        PaidToday = false;
    }

    public void Restore(int missedPayments)
    {
        MissedPayments = missedPayments < 0 ? 0 : missedPayments;
        PaidToday = false;
    }
}
