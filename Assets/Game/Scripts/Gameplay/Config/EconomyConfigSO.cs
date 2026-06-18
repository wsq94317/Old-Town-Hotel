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
    [Tooltip("Simple daily interest on the outstanding loan (0.0015 = 0.15%/day).")]
    public float dailyInterestRate = 0.0015f;
    [Tooltip("Bank will lend up to hotelValue * this factor (minus current debt).")]
    public float creditLimitFactor = 0.5f;

    [Header("Hotel valuation (Phase 3)")]
    public int baseHotelValue = 50000;
    public int perRoomValue = 8000;
    public int renovatedRoomBonus = 12000;

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
