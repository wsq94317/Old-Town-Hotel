using UnityEngine;

// Owns the live cash balance + payroll ledger and settles one economic day.
// Phase 1: income = servedGuests * config.roomRevenuePerGuest.
[DisallowMultipleComponent]
public sealed class EconomySystem : MonoBehaviour
{
    [SerializeField] private EconomyConfigSO config;

    public PayrollLedger Payroll { get; private set; }
    public LoanAccount Loan { get; private set; }
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
        Loan = new LoanAccount(cfg.startingLoan, cfg.dailyInterestRate);
        Payroll = new PayrollLedger();
        Payroll.Hire(new StaffMember(StaffRole.Reception, "Reception", cfg.WageFor(StaffRole.Reception)));
        Payroll.Hire(new StaffMember(StaffRole.Housekeeper, "Housekeeper", cfg.WageFor(StaffRole.Housekeeper)));
        Payroll.Hire(new StaffMember(StaffRole.Manager, "Manager", cfg.WageFor(StaffRole.Manager)));
    }

    public void InitializeForTest(EconomyConfigSO cfg) => Initialize(cfg);

    // Settle the day: credit room revenue, debit wages + accrued loan interest, update cash.
    public DayLedger CloseEconomicDay(int servedGuests)
    {
        int income = Mathf.Max(0, servedGuests) * config.roomRevenuePerGuest;
        int interest = Loan != null ? Loan.AccrueDailyInterest() : 0;
        DayLedger ledger = new DayLedger(income, Payroll.TotalDailyWages, interest, 0, Cash);
        Cash = ledger.ClosingBalance;
        LastDayLedger = ledger;
        return ledger;
    }

    // ── Finance actions (Phase 3) ────────────────────────────────────────────
    public int ComputeHotelValue(int openRooms, int renovatedRooms)
        => HotelValuation.Compute(openRooms, renovatedRooms,
                                  config.baseHotelValue, config.perRoomValue, config.renovatedRoomBonus);

    public int CreditLimit(int openRooms, int renovatedRooms)
        => HotelValuation.CreditLimit(ComputeHotelValue(openRooms, renovatedRooms),
                                      config.creditLimitFactor, Loan != null ? Loan.Balance : 0);

    public int Borrow(int amount)
    {
        if (Loan == null || amount <= 0) return 0;
        Loan.Borrow(amount);
        Cash += amount;
        return amount;
    }

    // Repay from cash; capped at both available cash and outstanding balance. Returns repaid amount.
    public int RepayLoan(int amount)
    {
        if (Loan == null) return 0;
        int payable = Mathf.Min(amount, Cash);
        int repaid = Loan.Repay(payable);
        Cash -= repaid;
        return repaid;
    }

    // ── Hiring / firing / raises (Phase 4c) ──────────────────────────────────
    // Hire a candidate; if a signing cost is set, it must be affordable. Returns success.
    public bool HireCandidate(StaffMember candidate, int signingCost = 0)
    {
        if (candidate == null) return false;
        if (signingCost > 0)
        {
            if (Cash < signingCost) return false;
            Cash -= signingCost;
        }
        Payroll.Hire(candidate);
        return true;
    }

    public void FireStaff(StaffMember member) => Payroll.Fire(member);

    // Accept a staffer's raise: higher wage flows into TotalDailyWages, morale rises.
    public void GiveRaise(StaffMember member, int newDailyWage) => member?.GiveRaise(newDailyWage);

    // Refuse a raise: morale drops (risk of quitting later).
    public void RefuseRaise(StaffMember member, int moralePenalty = 20) => member?.AdjustMorale(-moralePenalty);
}
