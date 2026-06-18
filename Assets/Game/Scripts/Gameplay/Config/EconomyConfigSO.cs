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
