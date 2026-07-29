// 信用（用户拍板简化）。纯 C#，不引用 UnityEngine。
//
// 还贷从"自动按净利分成扣"改成玩家亲手按之后就必须有不还的代价，
// 否则最优解永远是永不还款。但第一版把代价做复杂了：惩罚利率 + 评级两套机制
// 耦合在一起，玩家算不清，而且**利滚利会真的杀掉存档**。
// 用户原话："贷款惩罚不要用非常复杂的算法，不然玩家无法理解也不行，
// 也不要作为杀死玩家存档的主要问题。"
//
// 所以现在只有**一个数字**和**两条规则**：
//
//     信用 0-100，开局 100
//     没还钱  → -20
//     还了钱  → +10（上限 100）
//
// 信用只决定**能借多少新钱**，**完全不影响利率**。
// 于是它不可能滚成死亡螺旋：最坏结果是"借不到新钱"，
// 而经营那条路永远走得通（和"胶带糊家具""清垃圾免费"同一条铁律）。
// 而且它会**自己长回来**——一个糟糕的星期不会永久刻在档案上。
public enum CreditRating
{
    Good,        // 银行喜欢你：想借多少借多少
    Shaky,       // 他们盯着你：额度打折
    Bad,         // 很难借到新钱
    Blacklisted  // 借不到了（但游戏继续——靠经营自己爬出来）
}

public static class CreditPolicy
{
    public const int MaxScore = 100;
    public const int StartingScore = 100;

    /// <summary>没还钱扣多少分。五次逾期从满分掉到零——够狠但看得见。</summary>
    public const int MissPenalty = 20;

    /// <summary>按时还款回多少分。**必须能恢复**：
    /// 只扣不回的话一个糟糕的星期就永久刻在档案上，玩家再也没有翻身的叙事。
    /// 回得比扣得慢（10 vs 20），所以按时还款是件要坚持的事，不是随手能刷的。</summary>
    public const int PaymentReward = 10;

    // 档位阈值（一个词一个含义，玩家一眼知道自己在哪一档）
    public const int GoodAtOrAbove = 80;
    public const int ShakyAtOrAbove = 50;
    public const int BadAtOrAbove = 20;

    public static CreditRating RatingFor(int score)
    {
        if (score >= GoodAtOrAbove) return CreditRating.Good;
        if (score >= ShakyAtOrAbove) return CreditRating.Shaky;
        if (score >= BadAtOrAbove) return CreditRating.Bad;
        return CreditRating.Blacklisted;
    }

    /// <summary>这个档位还能借到基准额度的多少（0 = 借不到）。
    /// **这是信用唯一的作用**——不碰利率，所以不会利滚利杀掉存档。</summary>
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
            case CreditRating.Good: return "GOOD - borrow what you need";
            case CreditRating.Shaky: return "SHAKY - smaller loans only";
            case CreditRating.Bad: return "BAD - they barely lend at all";
            default: return "BLACKLISTED - no new loans (keep trading your way out)";
        }
    }

    /// <summary>一句话规则说明（UI 直接显示，玩家不用猜）。</summary>
    public static string RuleLine => "Miss a payment -20, pay on time +10. Credit only limits new loans.";
}

/// <summary>贷款的信用状态：一个 0-100 的分数，只影响借贷额度。</summary>
public sealed class CreditStanding
{
    public int Score { get; private set; } = CreditPolicy.StartingScore;

    /// <summary>今天该还的钱付过了吗（UI 靠它决定还要不要显示还款按钮）。</summary>
    public bool PaidToday { get; private set; }

    public CreditRating Rating => CreditPolicy.RatingFor(Score);

    /// <summary>历史逾期次数（只作展示，不参与任何计算）。</summary>
    public int MissedPayments { get; private set; }

    /// <summary>玩家按了还款。</summary>
    public void RecordPayment() => PaidToday = true;

    /// <summary>一天过去了。付了加分，该付没付扣分。
    /// **只在真的欠钱时判定**——债务已清的日子既不加也不扣。</summary>
    public void CloseDay(bool paymentWasDue)
    {
        if (paymentWasDue)
        {
            if (PaidToday)
            {
                Score += CreditPolicy.PaymentReward;
                if (Score > CreditPolicy.MaxScore) Score = CreditPolicy.MaxScore;
            }
            else
            {
                MissedPayments++;
                Score -= CreditPolicy.MissPenalty;
                if (Score < 0) Score = 0;
            }
        }
        PaidToday = false;
    }

    /// <summary>按当前档位能借多少。</summary>
    public int BorrowingLimitFor(int baseLimit)
    {
        if (baseLimit <= 0) return 0;
        return SimMath.RoundToInt(baseLimit * CreditPolicy.BorrowingHeadroomFor(Rating));
    }

    public void Clear()
    {
        Score = CreditPolicy.StartingScore;
        MissedPayments = 0;
        PaidToday = false;
    }

    public void Restore(int score, int missedPayments)
    {
        Score = SimMath.Clamp(score, 0, CreditPolicy.MaxScore);
        MissedPayments = missedPayments < 0 ? 0 : missedPayments;
        PaidToday = false;
    }
}
