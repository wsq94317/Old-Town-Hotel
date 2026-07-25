// 债务服务策略（纯静态函数，不引用 UnityEngine）。
//
// **为什么不是每天扣一个固定数**（M-C2 试玩定论，架构文档 §C3）：
// 固定 $150/天配上"日结时收入不够就从 Cash 垫付"，等于装了一台抽水泵——
// 玩家从保险箱收上来多少，当天就被抽走多少，现金永远钉在 $0。
// 100 天实测卡死在 14 间房、第 40 天起债务反涨：玩家做什么都一样，
// 因为攒不出任何一笔投资的首付。一笔超出赚钱能力的无条件支出会抽干全部能动性。
//
// 改成**按昨日净利分成、固定额作上限**：
//   · 永远还一点（"忘按还款键"依然不是有趣的失败——不需要玩家记得按）
//   · 赚得多就还得多，赚得少就少还，绝不把玩家泵到 0
//   · 于是"这笔钱拿去还贷还是拿去装修"重新成为真取舍（§C 的核心张力）
// 利息照常全额计提，不打折——债务不会因为你穷就停止生长，只是不再夺走首付。
public static class DebtPolicy
{
    /// <summary>昨日净利里拿多少去还本金。</summary>
    public const float DefaultProfitShare = 0.35f;

    /// <summary>净利再少也象征性还这么多（有钱才扣，避免"欠一块钱"的噪音）。</summary>
    public const int MinimumTokenRepayment = 10;

    /// <summary>今日计划还款额。
    /// yesterdayNetProfit：昨日入箱净利（亏损日传 ≤0）。
    /// cap：单日还款上限（玩家可调的"激进程度"，也是老的固定还款额）。</summary>
    public static int ScheduledRepaymentFor(int loanBalance, int yesterdayNetProfit, int cashOnHand,
                                            int cap, float profitShare = DefaultProfitShare)
    {
        if (loanBalance <= 0 || cap <= 0) return 0;

        int fromProfit = yesterdayNetProfit > 0
            ? SimMath.RoundToInt(yesterdayNetProfit * SimMath.Clamp01(profitShare))
            : 0;

        // 亏损日也别完全停手：有闲钱就象征性还一点，让债务曲线始终在动
        if (fromProfit < MinimumTokenRepayment && cashOnHand > MinimumTokenRepayment)
            fromProfit = MinimumTokenRepayment;

        int repayment = SimMath.Clamp(fromProfit, 0, cap);
        return repayment > loanBalance ? loanBalance : repayment;   // 不多还
    }
}
