// 日内关键阶段（架构 B.2）。纯静态函数，不引用 UnityEngine。
//   CheckoutPeak  08:00-11:00  退房高峰（白天以 checkout 为主）
//   Midday        11:00-16:00  白天运营（清洁消化脏房积压的主战场）
//   CheckInPeak   16:00-21:00  入住高峰（晚上以 checkin 为主，前台吞吐吃紧）
//   Evening       21:00-22:00  夜间收尾
//   Settle        22:00        日结（钟面 clamp 在此，等宿主结算后 BeginNextDay）
// 晨报不是一个时间段：它是 BeginNextDay 之后的一次性弹窗（会话循环起点）。
public enum SimDayPhase
{
    CheckoutPeak,
    Midday,
    CheckInPeak,
    Evening,
    Settle
}

public static class PhaseScheduler
{
    public const int CheckoutPeakStart = 8 * 60;
    public const int MiddayStart = 11 * 60;
    public const int CheckInPeakStart = 16 * 60;
    public const int EveningStart = 21 * 60;
    public const int SettleStart = 22 * 60;

    public static SimDayPhase PhaseFor(int minute)
    {
        if (minute >= SettleStart) return SimDayPhase.Settle;
        if (minute >= EveningStart) return SimDayPhase.Evening;
        if (minute >= CheckInPeakStart) return SimDayPhase.CheckInPeak;
        if (minute >= MiddayStart) return SimDayPhase.Midday;
        return SimDayPhase.CheckoutPeak;
    }

    public static int StartMinuteOf(SimDayPhase phase)
    {
        switch (phase)
        {
            case SimDayPhase.Midday: return MiddayStart;
            case SimDayPhase.CheckInPeak: return CheckInPeakStart;
            case SimDayPhase.Evening: return EveningStart;
            case SimDayPhase.Settle: return SettleStart;
            default: return CheckoutPeakStart;
        }
    }

    /// <summary>下一个关键阶段的起始分钟（"跳到下一阶段"的目标）；已在结算点则返回结算点本身。</summary>
    public static int NextKeyMinuteAfter(int minute)
    {
        if (minute < MiddayStart) return MiddayStart;
        if (minute < CheckInPeakStart) return CheckInPeakStart;
        if (minute < EveningStart) return EveningStart;
        if (minute < SettleStart) return SettleStart;
        return SettleStart;
    }

    /// <summary>能否跳段。有阻塞事件待处置时禁止（否则玩家可以跳过所有麻烦）。</summary>
    public static bool CanSkip(int minute, int blockingIncidents, out string reason)
    {
        if (minute >= SettleStart)
        {
            reason = "The day is done. Settle up first.";
            return false;
        }
        if (blockingIncidents > 0)
        {
            reason = blockingIncidents == 1
                ? "Something needs you right now. Deal with it first."
                : blockingIncidents + " things need you right now. Time doesn't skip itself.";
            return false;
        }
        reason = "";
        return true;
    }

    /// <summary>顶栏/晨报用的阶段名（游戏内文案=英文）。</summary>
    public static string Label(SimDayPhase phase)
    {
        switch (phase)
        {
            // 纯 ASCII：破折号在部分字体下是豆腐块，且 TMP 占位字体不一定含 U+2014
            case SimDayPhase.CheckoutPeak: return "CHECKOUT RUSH";
            case SimDayPhase.Midday: return "MIDDAY - catch up on rooms";
            case SimDayPhase.CheckInPeak: return "CHECK-IN RUSH";
            case SimDayPhase.Evening: return "EVENING - winding down";
            default: return "CLOSING THE BOOKS";
        }
    }

    /// <summary>阶段权重：离线闭式结算按阶段分摊到店/清洁负载时用（在线逐 tick 无需它）。</summary>
    public static float ArrivalWeightOf(SimDayPhase phase)
    {
        switch (phase)
        {
            case SimDayPhase.CheckoutPeak: return 0.05f;  // 早上几乎没人入住
            case SimDayPhase.Midday: return 0.20f;
            case SimDayPhase.CheckInPeak: return 0.65f;   // 入住高峰吃掉大头
            case SimDayPhase.Evening: return 0.10f;
            default: return 0f;
        }
    }
}
