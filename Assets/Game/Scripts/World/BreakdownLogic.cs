using UnityEngine;

// 老旧酒店损坏系统（纯逻辑层）：三级严重度 + 四种经理式处理的结果表。
// roll ∈ [0,1) 外部注入，全分支确定可测。文案按用户设计定调。
public enum BreakdownSeverity { Minor, Moderate, Severe } // 黄 / 橙 / 红

public enum BreakdownFix { DIY, SendStaff, DuctTape, LockRoom }

public readonly struct BreakdownOutcome
{
    public readonly bool Fixed;
    public readonly int SeverityDelta;    // 越修越坏 = +1
    public readonly int CashDelta;        // 小费/成本
    public readonly int SatisfactionDelta;
    public readonly bool TapedRecurrence; // 胶带：明天同址复发更严重
    public readonly bool LockedRoom;
    public readonly bool ManagerSlapstick; // 经理挨整演出（喷水/触电）
    public readonly string Story;

    public BreakdownOutcome(bool fix, int sev, int cash, int sat, bool taped, bool locked, bool slapstick, string story)
    {
        Fixed = fix; SeverityDelta = sev; CashDelta = cash; SatisfactionDelta = sat;
        TapedRecurrence = taped; LockedRoom = locked; ManagerSlapstick = slapstick; Story = story;
    }
}

public static class BreakdownLogic
{
    public const double DiySuccessChance = 0.55;
    public const double ClumsyWorsenChance = 0.30;
    public const float EscalateGameHours = 3f; // 不处理每 3 游戏小时升一级

    public static string SeverityLabel(BreakdownSeverity s) =>
        s == BreakdownSeverity.Minor ? "MINOR" : s == BreakdownSeverity.Moderate ? "TROUBLE" : "DISASTER!!";

    public static Color SeverityColor(BreakdownSeverity s) =>
        s == BreakdownSeverity.Minor ? new Color(0.95f, 0.85f, 0.2f)
        : s == BreakdownSeverity.Moderate ? new Color(0.95f, 0.55f, 0.15f)
        : new Color(0.95f, 0.2f, 0.15f);

    public static BreakdownOutcome Resolve(BreakdownFix fix, double roll, bool staffClumsy, bool staffFastHands)
    {
        switch (fix)
        {
            case BreakdownFix.DIY:
                return roll < DiySuccessChance
                    ? new BreakdownOutcome(true, 0, +20, +1, false, false, false,
                        "Fixed it yourself. A guest tipped you $20 and called you 'handy'.")
                    : new BreakdownOutcome(false, 0, 0, 0, false, false, true,
                        "It fought back. You're soaked, your hair is pointing at God, it's still broken.");

            case BreakdownFix.SendStaff:
                if (staffClumsy && roll < ClumsyWorsenChance)
                    return new BreakdownOutcome(false, +1, 0, -1, false, false, false,
                        "They 'fixed' it. It is now worse. They seem proud.");
                return new BreakdownOutcome(true, 0, 0, 0, false, false, false,
                    staffFastHands ? "Fixed before you finished the sentence. Show-off."
                                   : "Fixed. Grumbling included, free of charge.");

            case BreakdownFix.DuctTape:
                return new BreakdownOutcome(true, 0, 0, 0, true, false, false,
                    "Duct tape. The universal language. See you tomorrow, problem.");

            default: // LockRoom
                return new BreakdownOutcome(true, 0, 0, -1, false, true, false,
                    "Room sealed. If we can't see the problem, there is no problem.");
        }
    }
}
