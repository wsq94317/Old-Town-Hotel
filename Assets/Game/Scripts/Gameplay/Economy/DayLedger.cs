// Immutable result of closing one economic day.
public readonly struct DayLedger
{
    public readonly int Income;
    public readonly int Wages;
    public readonly int Interest;     // daily loan interest (Phase 3)
    public readonly int Maintenance;  // daily upkeep (Phase 3+; 0 until wired)
    public readonly int OpeningBalance;
    public readonly int ClosingBalance;
    public int Net => Income - Wages - Interest - Maintenance;

    public DayLedger(int income, int wages, int interest, int maintenance, int openingBalance)
    {
        Income = income;
        Wages = wages;
        Interest = interest;
        Maintenance = maintenance;
        OpeningBalance = openingBalance;
        ClosingBalance = openingBalance + income - wages - interest - maintenance;
    }

    // Backward-compatible: wages-only day (no interest/maintenance).
    public DayLedger(int income, int wages, int openingBalance)
        : this(income, wages, 0, 0, openingBalance) { }
}
