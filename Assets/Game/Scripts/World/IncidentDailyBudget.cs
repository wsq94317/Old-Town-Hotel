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

    // 本文件其余部分是纯 C#，唯一的引擎耦合就是这个属性：Unity 关闭 domain reload 后
    // 静态状态会跨 play 场次残留，靠它在进入运行时清零。引擎外（dotnet test / 将来的
    // Godot）不存在这个问题——每次都是全新进程——所以条件编译掉即可。
    //
    // UNITY_5_3_OR_NEWER 由 Unity 恒定义、被 OldTownHotel.Sim.csproj 恒不定义，
    // 因此两边各取所需，而共享的是同一份源码。
#if UNITY_5_3_OR_NEWER
    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
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
