using UnityEngine;

// Owns the live cash balance + payroll ledger and settles one economic day.
// Phase 1: income = servedGuests * config.roomRevenuePerGuest.
[DisallowMultipleComponent]
public sealed class EconomySystem : MonoBehaviour
{
    [SerializeField] private EconomyConfigSO config;

    public PayrollLedger Payroll { get; private set; }
    public int Cash { get; private set; }
    public DayLedger LastDayLedger { get; private set; }

    private void Awake()
    {
        if (config != null) Initialize(config);
    }

    // Production init (Awake) and explicit test init share this.
    public void Initialize(EconomyConfigSO cfg)
    {
        config = cfg;
        Cash = cfg.startingCash;
        Payroll = new PayrollLedger();
        Payroll.Hire(new StaffMember(StaffRole.Reception, "Reception", cfg.WageFor(StaffRole.Reception)));
        Payroll.Hire(new StaffMember(StaffRole.Housekeeper, "Housekeeper", cfg.WageFor(StaffRole.Housekeeper)));
        Payroll.Hire(new StaffMember(StaffRole.Manager, "Manager", cfg.WageFor(StaffRole.Manager)));
    }

    public void InitializeForTest(EconomyConfigSO cfg) => Initialize(cfg);

    // Settle the day: credit room revenue, debit wages, update cash. Returns the P&L.
    public DayLedger CloseEconomicDay(int servedGuests)
    {
        int income = Mathf.Max(0, servedGuests) * config.roomRevenuePerGuest;
        DayLedger ledger = Payroll.CloseDay(income, Cash);
        Cash = ledger.ClosingBalance;
        LastDayLedger = ledger;
        return ledger;
    }
}
