// M3 监督玩法占位数值（集中一处，日后迁 ScriptableObject）。
public static class SupervisionTuning
{
    // ── 偷懒 ────────────────────────────────────────────────────────────────
    public const float BaseSlackChancePerSecond = 0.02f; // 经理不在场时每秒进入偷懒的基础概率
    public const float LazyTraitMultiplier = 2f;
    public const float LowMoraleMultiplier = 1.5f;
    public const int LowMoraleThreshold = 40;

    // 偷懒会自己结束——**这不是妥协，是防死锁的硬约束**。原设计只有"经理进层"
    // 一条出路，于是经理不去的那层员工永远懒下去：实测验房员卡在 3.78/4 秒的
    // 进度上耗掉 205 秒，两间房整天回不到可售。100 间房的酒店经理不可能无处
    // 不在，那样整栈房态都会死锁。
    // 经理到场依然重要：它把"偷懒"变成"被抓"（掉士气、涨威望、留记录），
    // 也就是从"慢一点"升级成"有代价"——监督是加速器，不是唯一的解药。
    public const float SlackMinSeconds = 6f;
    public const float SlackMaxSeconds = 18f;
    public const float LazySlackMultiplier = 1.6f;   // Lazy 特质懒得更久

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
