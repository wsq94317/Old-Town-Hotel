using UnityEngine;

// M4 客诉决策（纯 C#）：赔钱 / 冷处理 / 打架🥊。
// roll ∈ [0,1) 由调用方从注入的 Random 取出传入——逻辑全确定可测。
// 无厘头结果表按用户授权由 Claude 定调（见 NIGHT_LOG 假设清单）。
public enum ComplaintChoice { Pay, ColdShoulder, Fight }

public readonly struct ComplaintOutcome
{
    public readonly int CashDelta;
    public readonly int SatisfactionDelta;   // 接 prototypeSatisfactionScore
    public readonly int StaffMoraleDelta;    // 全场员工
    public readonly int PrestigeDelta;       // 街区威望（长线系统占位计数）
    public readonly float SkipGameHours;     // 进警局跳过的营业时间
    public readonly bool ManagerKnockedDown; // 经理躺地演出
    public readonly string Story;            // 结果文案

    public ComplaintOutcome(int cash, int satisfaction, int staffMorale, int prestige,
                            float skipHours, bool knockedDown, string story)
    {
        CashDelta = cash;
        SatisfactionDelta = satisfaction;
        StaffMoraleDelta = staffMorale;
        PrestigeDelta = prestige;
        SkipGameHours = skipHours;
        ManagerKnockedDown = knockedDown;
        Story = story;
    }
}

public static class ComplaintDecisionLogic
{
    // 打架结果表阈值（roll < 阈值即命中，从上到下）
    public const double FightWinThreshold = 0.40;   // 40% 打跑客人
    public const double FightLoseThreshold = 0.75;  // 35% 被反杀
    // 其余 25% 双双进警局
    public const double ColdEscalateThreshold = 0.5; // 冷处理 50% 闹大

    public static ComplaintOutcome Resolve(ComplaintChoice choice, int nightlyRate, double roll)
    {
        switch (choice)
        {
            case ComplaintChoice.Pay:
                return new ComplaintOutcome(
                    -Mathf.Max(10, nightlyRate / 2), +2, 0, 0, 0f, false,
                    "You slid half the night's rate across the desk. Money talks; the guest walks.");

            case ComplaintChoice.ColdShoulder:
                return roll < ColdEscalateThreshold
                    ? new ComplaintOutcome(0, -1, 0, 0, 0f, false,
                        "The guest grumbled into the void, gave up, and left. Free of charge.")
                    : new ComplaintOutcome(0, -4, 0, 0, 0f, false,
                        "The guest wrote a 1-star essay. With photos. And a sequel.");

            default: // Fight
                if (roll < FightWinThreshold)
                    return new ComplaintOutcome(0, -3, +5, +2, 0f, false,
                        "You won. The guest fled shoeless. The staff saw everything — morale is up, mysteriously.");
                if (roll < FightLoseThreshold)
                    return new ComplaintOutcome(-nightlyRate, -2, +8, 0, 0f, true,
                        "You lost. Badly. The staff cheered — for the guest. Still, they respect the attempt.");
                return new ComplaintOutcome(-Mathf.Max(10, nightlyRate / 2), -2, +2, +1, 3f, false,
                    "The police took you BOTH. One bail later you're back. The hotel ran fine without you. Worrying.");
        }
    }
}
