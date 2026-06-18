// Immutable result of closing one economic day.
public readonly struct DayLedger
{
    public readonly int Income;
    public readonly int Wages;
    public readonly int OpeningBalance;
    public readonly int ClosingBalance;
    public int Net => Income - Wages;

    public DayLedger(int income, int wages, int openingBalance)
    {
        Income = income;
        Wages = wages;
        OpeningBalance = openingBalance;
        ClosingBalance = openingBalance + income - wages;
    }
}
