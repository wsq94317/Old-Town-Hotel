// 日结搞笑评语（纯 C#，按净利分档）。
public static class DayVerdictLogic
{
    public static string Line(int net)
    {
        if (net >= 300) return "HOTEL MOGUL. The street trembles.";
        if (net >= 100) return "Solid day. The stapler stays.";
        if (net > 0)    return "Technically profit. Frame it.";
        if (net == 0)   return "Perfectly balanced. Suspiciously so.";
        if (net > -100) return "A rough day. The plants saw nothing.";
        return "The bank called. You let it ring.";
    }
}
