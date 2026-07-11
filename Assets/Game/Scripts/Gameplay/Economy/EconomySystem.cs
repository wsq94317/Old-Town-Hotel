using System.Collections.Generic;
using UnityEngine;

// Owns the live cash balance + payroll ledger and settles one economic day.
// Phase 1: income = servedGuests * config.roomRevenuePerGuest.
[DisallowMultipleComponent]
public sealed class EconomySystem : MonoBehaviour
{
    [SerializeField] private EconomyConfigSO config;

    public EconomyConfigSO Config => config;
    public PayrollLedger Payroll { get; private set; }
    public LoanAccount Loan { get; private set; }
    public ReputationLedger Reputation { get; private set; }
    public int Cash { get; private set; }
    public DayLedger LastDayLedger { get; private set; }

    private int _pendingCheckoutIncome;
    private int _pendingCheckoutCount;

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
        Reputation = new ReputationLedger(cfg.reputationWindowSize);
        _pendingCheckoutIncome = 0;
        _pendingCheckoutCount = 0;
        Payroll = new PayrollLedger();
        Payroll.Hire(new StaffMember(StaffRole.Reception, "Reception", cfg.WageFor(StaffRole.Reception)));
        Payroll.Hire(new StaffMember(StaffRole.Housekeeper, "Housekeeper", cfg.WageFor(StaffRole.Housekeeper)));
        Payroll.Hire(new StaffMember(StaffRole.Manager, "Manager", cfg.WageFor(StaffRole.Manager)));
    }

    public void InitializeForTest(EconomyConfigSO cfg) => Initialize(cfg);

    // ── Checkout income (Phase 6: tier price × satisfaction) ─────────────────
    // Credit one checked-out guest: nightly rate (caller resolves the room's tier
    // via RenovationConfigSO.NightlyRevenueFor) times the stay's satisfaction
    // multiplier. Banked into income at day close; also feeds the star rating.
    // Returns the credited amount (for UI toasts / coin fly).
    public int RecordCheckout(int nightlyRate, float satisfactionMult)
    {
        float mult = Mathf.Clamp(satisfactionMult,
                                 ReputationLedger.MinSatisfaction,
                                 ReputationLedger.MaxSatisfaction);
        int amount = Mathf.RoundToInt(Mathf.Max(0, nightlyRate) * mult);
        _pendingCheckoutIncome += amount;
        _pendingCheckoutCount++;
        Reputation?.RecordGuest(mult);
        return amount;
    }

    // How many guests today's star rating attracts (demand-loop spawn budget).
    public int DailyGuestTarget
        => config != null && Reputation != null ? config.GuestsPerDayFor(Reputation.Stars) : 0;

    // Settle the day: credit room revenue, debit wages + accrued loan interest, update cash.
    // Income prefers per-checkout tiered revenue (RecordCheckout); the flat
    // servedGuests × roomRevenuePerGuest path remains as fallback until the
    // checkout hook is wired everywhere.
    public DayLedger CloseEconomicDay(int servedGuests)
    {
        int income = _pendingCheckoutCount > 0
            ? _pendingCheckoutIncome
            : Mathf.Max(0, servedGuests) * config.roomRevenuePerGuest;
        _pendingCheckoutIncome = 0;
        _pendingCheckoutCount = 0;
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

    // ── Save / load (capture + restore full economy state) ───────────────────
    public EconomyState CaptureState()
    {
        var s = new EconomyState
        {
            cash = Cash,
            loanBalance = Loan != null ? Loan.Balance : 0,
            loanRate = Loan != null ? Loan.DailyInterestRate : 0f,
            reputationSamples = Reputation != null ? Reputation.ExportSamples() : new List<float>()
        };
        if (Payroll != null)
        {
            foreach (var m in Payroll.Roster)
            {
                var ss = new StaffState
                {
                    role = (int)m.Role,
                    name = m.DisplayName,
                    wage = m.DailyWage,
                    speed = m.Attributes.Speed,
                    quality = m.Attributes.Quality,
                    stamina = m.Attributes.Stamina,
                    education = m.EducationLevel,
                    morale = m.Morale
                };
                foreach (var tr in m.Traits) ss.traits.Add((int)tr);
                s.staff.Add(ss);
            }
        }
        return s;
    }

    public void RestoreState(EconomyState s)
    {
        if (s == null) return;
        Cash = s.cash;
        Loan = new LoanAccount(s.loanBalance, s.loanRate);
        Reputation = new ReputationLedger(config != null ? config.reputationWindowSize : 20);
        Reputation.ImportSamples(s.reputationSamples);
        _pendingCheckoutIncome = 0;
        _pendingCheckoutCount = 0;
        Payroll = new PayrollLedger();
        foreach (var ss in s.staff)
        {
            var traits = new List<StaffTrait>();
            foreach (var t in ss.traits) traits.Add((StaffTrait)t);
            var member = new StaffMember((StaffRole)ss.role, ss.name, ss.wage,
                                         new StaffAttributes(ss.speed, ss.quality, ss.stamina),
                                         ss.education, traits);
            member.AdjustMorale(ss.morale - StaffMember.DefaultMorale); // set exact saved morale
            Payroll.Hire(member);
        }
    }

    // ── Generic spend (renovation, etc., Phase 5) ────────────────────────────
    // Deduct `amount` from cash if affordable. Returns true if paid.
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (Cash < amount) return false;
        Cash -= amount;
        return true;
    }
}
