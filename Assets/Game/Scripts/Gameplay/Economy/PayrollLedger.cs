using System.Collections.Generic;

// Pure-C# payroll: holds the roster and computes wages / daily P&L.
// No Unity dependency so it is fully unit-testable.
public sealed class PayrollLedger
{
    private readonly List<StaffMember> _roster = new List<StaffMember>();

    public IReadOnlyList<StaffMember> Roster => _roster;
    public int Count => _roster.Count;

    // Roster-change events so scene systems (StaffCrew) can sync worker instances.
    public event System.Action<StaffMember> OnHired;
    public event System.Action<StaffMember> OnFired;

    public int CountByRole(StaffRole role)
    {
        int n = 0;
        for (int i = 0; i < _roster.Count; i++) if (_roster[i].Role == role) n++;
        return n;
    }

    public int TotalDailyWages
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < _roster.Count; i++) sum += _roster[i].DailyWage;
            return sum;
        }
    }

    public void Hire(StaffMember member)
    {
        if (member == null || _roster.Contains(member)) return;
        _roster.Add(member);
        OnHired?.Invoke(member);
    }

    public void Fire(StaffMember member)
    {
        if (_roster.Remove(member)) OnFired?.Invoke(member);
    }

    // Settle one day: credit income, debit total wages, return the P&L + new balance.
    // Deficit (negative balance) is allowed here; the fail/loan rule lives in EconomySystem.
    public DayLedger CloseDay(int income, int startingBalance)
    {
        return new DayLedger(income, TotalDailyWages, startingBalance);
    }
}
