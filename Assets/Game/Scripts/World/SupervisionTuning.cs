// M3 监督玩法占位数值（集中一处，日后迁 ScriptableObject）。
public static class SupervisionTuning
{
    // ── 偷懒 ────────────────────────────────────────────────────────────────
    public const float BaseSlackChancePerSecond = 0.02f; // 经理不在场时每秒进入偷懒的基础概率
    public const float LazyTraitMultiplier = 2f;
    public const float LowMoraleMultiplier = 1.5f;
    public const int LowMoraleThreshold = 40;

    // ── 发现窗口 ─────────────────────────────────────────────────────────────
    public const float WakeDelaySeconds = 1.5f;      // 经理进层后惊醒延迟
    public const float WakeDelayLazySeconds = 3f;    // Lazy 特质更迟钝
    public const float PanicFakeSeconds = 2f;        // 慌张装忙窗口
    public const float CatchRadius = 2.5f;           // 抓包判定半径（水平）

    // ── 拖延痕迹 ─────────────────────────────────────────────────────────────
    public const float DelayMarkThresholdMultiplier = 1.5f; // 实际工期超过预期×此倍数挂🐌

    // ── 抓包决策效果 ─────────────────────────────────────────────────────────
    public const int UrgeMoraleDelta = -5;        // 督促
    public const int ScoldMoraleDelta = -15;      // 训斥
    public const int IgnoreMoraleDelta = +3;      // 睁只眼闭只眼
    public const int WrongAccusationMoraleDelta = -20; // 错怪好人

    // ── 瑕疵 ────────────────────────────────────────────────────────────────
    public const float FlawChanceAtZeroQuality = 0.40f;  // quality=0 时瑕疵率
    public const float FlawChanceAtFullQuality = 0.05f;  // quality=100 时瑕疵率
    public const float InspectorMissAtZeroQuality = 0.50f;
    public const float InspectorMissAtFullQuality = 0.05f;
}
