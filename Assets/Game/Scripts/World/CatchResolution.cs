// 抓包/质询的决策与效果（纯 C#）。
public enum CatchChoice
{
    Urge,   // 督促：恢复干活+短期加速，士气小降
    Scold,  // 训斥：加速更多，士气大降，Diva（记仇担当）额外再降
    Ignore  // 睁只眼闭只眼：士气小升，偷懒传染信号
}

public readonly struct CatchOutcome
{
    public readonly int MoraleDelta;
    public readonly bool SpeedBuff;       // 督促/训斥 → 短期加速
    public readonly bool ContagionSignal; // 无视 → 附近员工偷懒率上升（宿主处理）
    public readonly bool GrudgeTriggered; // Diva 记仇

    public CatchOutcome(int morale, bool speed, bool contagion, bool grudge)
    {
        MoraleDelta = morale;
        SpeedBuff = speed;
        ContagionSignal = contagion;
        GrudgeTriggered = grudge;
    }
}

public static class CatchResolutionLogic
{
    /// <summary>抓包三选一的效果（应用士气由调用方执行，便于测试）。</summary>
    public static CatchOutcome Resolve(CatchChoice choice, StaffMember member)
    {
        bool diva = member != null && member.HasTrait(StaffTrait.Diva);
        switch (choice)
        {
            case CatchChoice.Urge:
                return new CatchOutcome(SupervisionTuning.UrgeMoraleDelta, true, false, false);
            case CatchChoice.Scold:
                int delta = SupervisionTuning.ScoldMoraleDelta + (diva ? -10 : 0);
                return new CatchOutcome(delta, true, false, diva);
            default:
                return new CatchOutcome(SupervisionTuning.IgnoreMoraleDelta, false, true, false);
        }
    }
}

// 质询（点 🐌 拖延标记）判定。
public enum InterrogationVerdict { Caught, WrongAccusation }

public static class InterrogateLogic
{
    public static InterrogationVerdict Verdict(bool hasSlackRecord) =>
        hasSlackRecord ? InterrogationVerdict.Caught : InterrogationVerdict.WrongAccusation;
}
