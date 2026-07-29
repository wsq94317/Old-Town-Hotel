// 工资账（用户设计：支付必须是玩家的动作）。纯 C#，不引用 UnityEngine。
//
// 玩家原话："我感觉你应该忽略了玩家支付贷款、支付员工工资的这些支付过程，
// 因为每天能看到现金都是上千。" ——以前工资在日结里自动扣掉，玩家全程无感，
// 一笔本该疼的支出变成了一行流水。现在工资先**记账**，等玩家亲手按下支付。
//
// 日付 vs 周付（用户要的选项）：
//   日付：每天结清，金额小、无风险。
//   周付：攒七天一次结清。**总额不变，变的是风险**——六天的现金留在手上
//         （够动一次装修），但发薪日凑不齐**整周**的钱，士气打击明显重于
//         日付漏一天：大家是指着这笔钱过日子的。
// 于是周付是"拿员工的钱做一次短期周转"，一个有真实赌注的选择，
// 而不是单纯更优或更差的选项（那种选项等于没有选项）。
public enum PayrollCycle
{
    Daily,   // 日付：每天结清
    Weekly   // 周付：攒七天，发薪日一次付清
}

/// <summary>一次支付的结果（UI 要据此说人话）。</summary>
public readonly struct PayrollPayment
{
    public readonly int paid;          // 实付
    public readonly int stillOwed;     // 付完还欠多少
    public readonly bool cleared;      // 付清了吗
    public readonly int moralePenalty; // 没付清时每人扣多少士气（0 = 没有惩罚）

    public PayrollPayment(int paid, int stillOwed, bool cleared, int moralePenalty)
    {
        this.paid = paid;
        this.stillOwed = stillOwed;
        this.cleared = cleared;
        this.moralePenalty = moralePenalty;
    }
}

public sealed class PayrollAccount
{
    /// <summary>周付的周期长度（和 PricingPolicy 的星期同一把尺）。</summary>
    public const int DaysPerPayCycle = 7;

    /// <summary>欠一天工资的士气代价。</summary>
    public const int MoralePenaltyPerMissedDay = 6;

    /// <summary>周付爆掉的额外代价：攒了一周的期待一次落空，比漏一天狠得多。
    /// 没有这一条，周付就是纯白拿的六天免息贷款（那样人人都选周付，选择就消失了）。</summary>
    public const int WeeklyBlowupExtraPenalty = 14;

    public PayrollCycle Cycle { get; set; } = PayrollCycle.Daily;

    /// <summary>累计未付工资（UI 常驻显示这个数——用户明确要求的提醒）。</summary>
    public int Owed { get; private set; }

    /// <summary>已经累计了几天的工资没付。</summary>
    public int DaysAccrued { get; private set; }

    /// <summary>历史上有几个发薪日没付清（信用/士气链读它）。</summary>
    public int MissedPaydays { get; private set; }

    /// <summary>日结时把当天工资记上账（还没付钱）。</summary>
    public void Accrue(int wages)
    {
        if (wages <= 0) return;
        Owed += wages;
        DaysAccrued++;
    }

    /// <summary>今天是发薪日吗。日付天天是，周付每 7 天一次。
    /// day 从 1 开始，所以第 7、14、21 天是发薪日。</summary>
    public bool IsPaydayOn(int day)
    {
        if (Owed <= 0) return false;
        if (Cycle == PayrollCycle.Daily) return true;
        return day > 0 && day % DaysPerPayCycle == 0;
    }

    /// <summary>还剩几天到发薪日（周付的提醒条用它；日付恒 0）。</summary>
    public int DaysUntilPayday(int day)
    {
        if (Cycle == PayrollCycle.Daily) return 0;
        if (day <= 0) return DaysPerPayCycle;
        int into = day % DaysPerPayCycle;
        return into == 0 ? 0 : DaysPerPayCycle - into;
    }

    /// <summary>玩家按下支付：能付多少付多少。
    /// 付不清就按缺口算士气惩罚——**周付爆掉要额外加一记**。
    /// 返回实付金额，调用方负责从现金里扣。</summary>
    public PayrollPayment Pay(int cashAvailable, bool isPayday)
    {
        if (Owed <= 0) return new PayrollPayment(0, 0, true, 0);

        int paid = cashAvailable < Owed ? cashAvailable : Owed;
        if (paid < 0) paid = 0;
        Owed -= paid;

        bool cleared = Owed <= 0;
        int penalty = 0;

        if (cleared)
        {
            DaysAccrued = 0;
        }
        else if (isPayday)
        {
            // 发薪日没付清才算欠薪。没到发薪日的余额只是"还没到期"，不罚。
            MissedPaydays++;
            penalty = MoralePenaltyPerMissedDay * (DaysAccrued < 1 ? 1 : DaysAccrued);
            if (Cycle == PayrollCycle.Weekly) penalty += WeeklyBlowupExtraPenalty;
        }

        return new PayrollPayment(paid, Owed, cleared, penalty);
    }

    /// <summary>切换支付周期。**切换时先把已欠的结清才生效**——
    /// 不然玩家可以在发薪日前一秒切成周付，无限期拖着不付。</summary>
    public bool TrySetCycle(PayrollCycle cycle, out string reason)
    {
        reason = "";
        if (Cycle == cycle) return true;
        if (Owed > 0)
        {
            reason = "Settle the outstanding wages first.";
            return false;
        }
        Cycle = cycle;
        return true;
    }

    public void Clear()
    {
        Owed = 0;
        DaysAccrued = 0;
        MissedPaydays = 0;
    }

    /// <summary>存档用（世界场景接存档要到 M-F，先留好接口）。</summary>
    public void Restore(PayrollCycle cycle, int owed, int daysAccrued, int missedPaydays)
    {
        Cycle = cycle;
        Owed = owed < 0 ? 0 : owed;
        DaysAccrued = daysAccrued < 0 ? 0 : daysAccrued;
        MissedPaydays = missedPaydays < 0 ? 0 : missedPaydays;
    }
}
