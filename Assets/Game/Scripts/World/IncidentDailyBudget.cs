using System.Collections.Generic;

/// <summary>
/// One shared attention budget for all unscripted incidents. Systems may schedule
/// freely, but only one or two incidents are allowed to become active per day.
/// </summary>
public static class IncidentDailyBudget
{
    private static readonly HashSet<string> Claims = new HashSet<string>();
    private static int _day = int.MinValue;
    private static int _limit;

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime() => ResetForTests();

    public static int LimitForDay(int day)
    {
        EnsureDay(day);
        return _limit;
    }

    public static int ClaimedForDay(int day)
    {
        EnsureDay(day);
        return Claims.Count;
    }

    public static bool TryClaim(int day, string incidentId)
    {
        EnsureDay(day);
        if (string.IsNullOrEmpty(incidentId)) incidentId = "incident";
        if (Claims.Contains(incidentId)) return true;
        if (Claims.Count >= _limit) return false;
        Claims.Add(incidentId);
        return true;
    }

    public static void ResetForTests()
    {
        _day = int.MinValue;
        _limit = 0;
        Claims.Clear();
    }

    private static void EnsureDay(int day)
    {
        if (_day == day) return;
        _day = day;
        Claims.Clear();

        // Stable per-day variation: roughly one third of days allow a second event.
        uint hash = unchecked((uint)(day * 1103515245 + 12345));
        _limit = 1 + (hash % 100u < 35u ? 1 : 0);
    }
}
