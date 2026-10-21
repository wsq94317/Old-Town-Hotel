using System;

// Deliberately slower than active play: unattended management advances one
// business day per real hour, capped at eight days. The UI states this contract.
public static class DelegationPolicy
{
    public const int RealMinutesPerDay = 60;
    public const int MaxDays = 8;
    public static int MinutesFor(double realSeconds)
    {
        if (double.IsNaN(realSeconds) || realSeconds <= 0) return 0;
        return (int)Math.Min(MaxDays * SimClock.MinutesPerDay,
            realSeconds / (RealMinutesPerDay * 60d) * SimClock.MinutesPerDay);
    }
}

[Serializable]
public sealed class DelegationReport
{
    public int minutes, daysClosed, guests, receipts, wagesPaid, debtBefore, debtAfter;
    public int safeBefore, safeAfter, overflowAfter, brokenRooms;
    public bool acknowledged;
}
