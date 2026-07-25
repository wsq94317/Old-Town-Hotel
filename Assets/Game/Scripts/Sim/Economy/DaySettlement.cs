// 日结顺序（架构 B.3，修订版 3 的关键裁决）。纯函数，不引用 UnityEngine。
//
// **固定成本前置**：当日毛收入 → 扣佣金 → 扣固定成本（工资+利息+计划还款+基础补货）
//                  → 净利润入保险箱（装不下的进溢出账本）
// 收入不足由现金垫付；现金也不够才欠薪。
//
// 为什么不是"钱在保险箱所以发不出工资"（初版设计，已推翻）：
//   ① 叙事荒唐——前台自己收的钱自己不能拆开发薪？
//   ② 留存自杀——离线几天回来全员士气归零集体离职，是在惩罚回归玩家
//   ③ 有趣的取舍是"现金拿去提前还款还是装修"，不是"记得按收取键"
// 回归动力改由正向提供：利润堆在箱里等你做投资决策 + 溢出在漏钱（损失厌恶）。
// 主动支出（装修/材料/提前还款/升级/招聘）仍只能花现金，现金只能靠收箱补充。

public readonly struct DaySettlementInput
{
    public readonly int grossIncome;         // 当日毛收入（房费+设施+小费）
    public readonly int commission;          // 渠道佣金
    public readonly int wages;               // 在班员工工资
    public readonly int interest;            // 贷款利息
    public readonly int scheduledRepayment;  // 计划还款（自动扣——"忘按还款键"不是有趣的失败）
    public readonly int supplies;            // 基础补货（按 PolicyPreset）
    public readonly int cashOnHand;

    public DaySettlementInput(int grossIncome, int commission, int wages, int interest,
                              int scheduledRepayment, int supplies, int cashOnHand)
    {
        this.grossIncome = grossIncome;
        this.commission = commission;
        this.wages = wages;
        this.interest = interest;
        this.scheduledRepayment = scheduledRepayment;
        this.supplies = supplies;
        this.cashOnHand = cashOnHand;
    }

    public int FixedCosts => wages + interest + scheduledRepayment + supplies;
    public int IncomeAfterCommission => grossIncome - commission;
}

public readonly struct DaySettlementResult
{
    public readonly int netToSafebox;         // 实际入箱
    public readonly int overflowed;           // 装不下的（→ OverflowLedger）
    public readonly int cashPaidFromReserve;  // 从储备现金垫付了多少
    public readonly int cashAfter;
    public readonly bool wagesPaid;
    public readonly int unpaidAmount;         // 欠了多少（触发欠薪士气链）

    public DaySettlementResult(int netToSafebox, int overflowed, int cashPaidFromReserve,
                               int cashAfter, bool wagesPaid, int unpaidAmount)
    {
        this.netToSafebox = netToSafebox;
        this.overflowed = overflowed;
        this.cashPaidFromReserve = cashPaidFromReserve;
        this.cashAfter = cashAfter;
        this.wagesPaid = wagesPaid;
        this.unpaidAmount = unpaidAmount;
    }

    /// <summary>当日净利（晨报展示：赚了还是亏了）。</summary>
    public int NetProfit => netToSafebox + overflowed - cashPaidFromReserve;
}

public static class DaySettlement
{
    /// <summary>结算一天。safeboxRoom = 保险箱剩余容量。</summary>
    public static DaySettlementResult Resolve(DaySettlementInput input, int safeboxRoom)
    {
        int income = input.IncomeAfterCommission;
        int costs = input.FixedCosts;

        if (income >= costs)
        {
            // 赚钱的日子：净利入箱，储备现金不动
            int net = income - costs;
            int room = safeboxRoom < 0 ? 0 : safeboxRoom;
            int deposited = net < room ? net : room;
            return new DaySettlementResult(
                netToSafebox: deposited,
                overflowed: net - deposited,
                cashPaidFromReserve: 0,
                cashAfter: input.cashOnHand,
                wagesPaid: true,
                unpaidAmount: 0);
        }

        // 亏损的日子：缺口从现金垫付
        int shortfall = costs - income;
        int cash = input.cashOnHand < 0 ? 0 : input.cashOnHand;
        int paid = shortfall < cash ? shortfall : cash;
        int unpaid = shortfall - paid;

        return new DaySettlementResult(
            netToSafebox: 0,
            overflowed: 0,
            cashPaidFromReserve: paid,
            cashAfter: cash - paid,
            wagesPaid: unpaid <= 0,
            unpaidAmount: unpaid);
    }
}
