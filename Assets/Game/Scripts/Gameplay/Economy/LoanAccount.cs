using System;

// Pure-C# loan: an outstanding balance that accrues simple daily interest.
// No Unity dependency -> fully unit-testable. (Phase 3 Finance.)
public sealed class LoanAccount
{
    public int Balance { get; private set; }      // outstanding principal owed (>= 0)
    public float DailyInterestRate { get; }        // e.g. 0.0015 = 0.15% per day

    public LoanAccount(int openingBalance, float dailyInterestRate)
    {
        Balance = Math.Max(0, openingBalance);
        DailyInterestRate = Math.Max(0f, dailyInterestRate);
    }

    public bool IsCleared => Balance <= 0;

    // Today's interest charge (rounded). Adds it onto the balance and returns it.
    public int AccrueDailyInterest()
    {
        int interest = (int)Math.Round(Balance * (double)DailyInterestRate, MidpointRounding.AwayFromZero);
        Balance += interest;
        return interest;
    }

    // Take on more debt.
    public void Borrow(int amount)
    {
        if (amount > 0) Balance += amount;
    }

    // Repay up to `amount`; returns the amount actually repaid (capped at the balance).
    // Caller is responsible for deducting the returned value from cash.
    public int Repay(int amount)
    {
        if (amount <= 0) return 0;
        int actual = Math.Min(amount, Balance);
        Balance -= actual;
        return actual;
    }
}
