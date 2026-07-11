using UnityEngine;

// Tunable economy constants (Phase 1). Balance by editing the asset, not code.
[CreateAssetMenu(fileName = "EconomyConfig", menuName = "Old Town Hotel/Economy Config", order = 102)]
public sealed class EconomyConfigSO : ScriptableObject
{
    [Header("Cash")]
    public int startingCash = 2450;

    [Header("Income")]
    [Tooltip("Revenue credited per successfully-served guest at day end (Phase 1 simplification).")]
    public int roomRevenuePerGuest = 80;

    [Header("Daily wages by role")]
    public int receptionDailyWage = 50;
    public int housekeeperDailyWage = 45;
    public int managerDailyWage = 90;
    public int inspectorDailyWage = 55;

    [Header("Loan & finance (Phase 3)")]
    [Tooltip("Debt the player inherits when taking over the old hotel.")]
    public int startingLoan = 183000;
    [Tooltip("Simple daily interest on the outstanding loan (0.0005 = 0.05%/day). 0.0015 bled ~$200/day in P1 — see tuning-knobs red flag 2026-07-04.")]
    public float dailyInterestRate = 0.0005f;
    [Tooltip("Bank will lend up to hotelValue * this factor (minus current debt).")]
    public float creditLimitFactor = 0.5f;

    [Header("Hotel valuation (Phase 3)")]
    public int baseHotelValue = 50000;
    public int perRoomValue = 8000;
    public int renovatedRoomBonus = 12000;

    [Header("Reputation & guest flow (Phase 6)")]
    [Tooltip("Rolling window of recent checkouts feeding the star rating.")]
    public int reputationWindowSize = 20;
    [Tooltip("Guests per day by star bracket: <2★ / <3★ / <4★ / <4.5★ / top.")]
    public int guestsBelow2Stars = 3;
    public int guestsBelow3Stars = 5;
    public int guestsBelow4Stars = 7;
    public int guestsBelow45Stars = 9;
    public int guestsTopStars = 12;

    [Header("Checkout satisfaction multipliers (Phase 6)")]
    [Tooltip("Room revenue multiplier when the stay matched the guest's preferences well.")]
    public float goodMatchMultiplier = 1.3f;
    [Tooltip("Multiplier for an acceptable but non-ideal match.")]
    public float normalMatchMultiplier = 1.0f;
    [Tooltip("Multiplier when preferences were poorly matched (complaints).")]
    public float poorMatchMultiplier = 0.7f;

    public int GuestsPerDayFor(float stars)
    {
        if (stars < 2f) return guestsBelow2Stars;
        if (stars < 3f) return guestsBelow3Stars;
        if (stars < 4f) return guestsBelow4Stars;
        if (stars < 4.5f) return guestsBelow45Stars;
        return guestsTopStars;
    }

    public int WageFor(StaffRole role)
    {
        switch (role)
        {
            case StaffRole.Reception: return receptionDailyWage;
            case StaffRole.Housekeeper: return housekeeperDailyWage;
            case StaffRole.Manager: return managerDailyWage;
            case StaffRole.Inspector: return inspectorDailyWage;
            default: return 0;
        }
    }
}
