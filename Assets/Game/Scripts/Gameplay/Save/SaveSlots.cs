using System.IO;
using UnityEngine;

// 三个存档槽位 + 摘要 + 跨场景重载的"待读取"意向。
//
// 为什么需要"待读取意向"这个东西（这是整块最关键的设计）：
// **读档不能在原地做**。玩家在第 12 天读一个第 3 天的档，场上有走到一半的客人、
// 排到一半的队、在途的施工单、正在打扫的管家——逐个系统"改回去"是不可能改干净的，
// 漏一个就是一个幽灵。可靠的做法只有一条：**重载场景，让一切从零开始，再把存档灌进去**。
// 于是需要一个能跨越场景重载活下来的静态意向，`PendingLoad` 就是它。
//
// 另一个坑（并行审计抓到的）：`SaveCoordinator.LoadGame()` 在 `Start()`，而
// `HotelSimSceneBridge.Sim` 是在 `Update() → TryBuild()` 里**惰性建**的。
// Unity 所有 Start 都先于任何 Update，所以读档那一刻 Sim 保证是 null。
// 照直觉接线不是"没生效"，是**存档损坏**。所以 Sim 那一段也走延迟应用：
// SaveCoordinator 把它挂在 `PendingSimRestore` 上，桥建完账立刻取用。
public static class SaveSlots
{
    public const int Count = 3;

    /// <summary>槽位号合法吗（1..3）。</summary>
    public static bool IsValid(int slot) => slot >= 1 && slot <= Count;

    /// <summary>槽位文件名。旧的单槽存档是 slot0.json，**保留不动**——
    /// 老玩家的进度不能因为加了槽位就消失（迁移见 MigrateLegacySlotIfNeeded）。</summary>
    public static string FileNameOf(int slot) => "slot" + slot + ".json";

    public static string PathOf(int slot) =>
        Path.Combine(Application.persistentDataPath, FileNameOf(slot));

    public static bool Exists(int slot) => IsValid(slot) && File.Exists(PathOf(slot));

    /// <summary>当前正在玩的槽位。自动存盘往它写，所以它必须一直有值。</summary>
    public static int ActiveSlot { get; private set; } = 1;

    public static void SetActiveSlot(int slot)
    {
        if (IsValid(slot)) ActiveSlot = slot;
    }

    /// <summary>玩家要求读取的槽位，跨场景重载活着。-1 = 没有请求。</summary>
    public static int PendingLoad { get; private set; } = -1;

    /// <summary>请求读取某个槽位：记下意向，由调用方重载场景。</summary>
    public static void RequestLoad(int slot)
    {
        if (!IsValid(slot)) return;
        PendingLoad = slot;
    }

    /// <summary>取出并清掉待读取意向（只该被 SaveCoordinator 消费一次）。</summary>
    public static int ConsumePendingLoad()
    {
        int slot = PendingLoad;
        PendingLoad = -1;
        return slot;
    }

    /// <summary>Sim 那一段的延迟应用：桥建完账之后来取。
    /// **不能在 Start 阶段直接灌**——那时 Sim 还不存在（见文件头注释）。</summary>
    public static SimState PendingSimRestore { get; private set; }

    public static void QueueSimRestore(SimState state) => PendingSimRestore = state;

    public static SimState ConsumeSimRestore()
    {
        SimState state = PendingSimRestore;
        PendingSimRestore = null;
        return state;
    }

    /// <summary>整局重来时把静态意向清干净，否则上一局的残留会渗进新档
    /// （同 ManagerReputation.ResetForNewGame 的理由）。</summary>
    public static void ResetForNewGame()
    {
        PendingLoad = -1;
        PendingSimRestore = null;
    }

    /// <summary>老的单槽存档（slot0.json）搬进 1 号槽。
    /// 只在 1 号槽还空着时搬，绝不覆盖玩家已有的档。</summary>
    public static void MigrateLegacySlotIfNeeded()
    {
        string legacy = Path.Combine(Application.persistentDataPath, "slot0.json");
        if (!File.Exists(legacy) || Exists(1)) return;
        try { File.Copy(legacy, PathOf(1)); }
        catch { /* 搬不动就算了：老档还在原地，读不到只是回到新局 */ }
    }
}

/// <summary>一个槽位在 UI 上的样子。三个槽长得一样的话玩家没法选，
/// 所以摘要必须带上**足以区分两局游戏的信息**：第几天、多少钱、几星、几间房。</summary>
public readonly struct SaveSlotSummary
{
    public readonly int slot;
    public readonly bool exists;
    public readonly int day;
    public readonly int cash;
    public readonly float stars;
    public readonly int openRooms;
    public readonly int version;

    public SaveSlotSummary(int slot, bool exists, int day, int cash, float stars,
                           int openRooms, int version)
    {
        this.slot = slot;
        this.exists = exists;
        this.day = day;
        this.cash = cash;
        this.stars = stars;
        this.openRooms = openRooms;
        this.version = version;
    }

    public static SaveSlotSummary Empty(int slot) =>
        new SaveSlotSummary(slot, false, 0, 0, 0f, 0, 0);

    /// <summary>从存档算摘要。星级从声誉样本平均值折算（和 ReputationLedger 同一把尺），
    /// 现金取 Sim 的（世界场景真正在花的那份钱）并回落到 v1 经济体。</summary>
    public static SaveSlotSummary From(int slot, GameState state)
    {
        if (state == null) return Empty(slot);

        int day = state.sim != null && state.sim.day > 0 ? state.sim.day
                : state.progress != null ? state.progress.day : 0;
        int cash = state.sim != null && state.sim.cash > 0 ? state.sim.cash
                 : state.economy != null ? state.economy.cash : 0;

        // 星级优先读 **Sim 的**声誉样本：经理模式的口碑走 Sim 的 ReputationLedger，
        // v1 经济体那份（economy.reputationSamples）在这个模式下压根不填——
        // 只读它的话三个槽位一律显示 0.0 星（实测踩到）。
        var samples = state.sim != null && state.sim.reputationSamples != null
                      && state.sim.reputationSamples.Count > 0
            ? state.sim.reputationSamples
            : (state.economy != null ? state.economy.reputationSamples : null);

        // **必须用 ReputationLedger 的同一条公式**（avg 0.5→1★、1.0→3★、1.5→5★）。
        // 我一开始写成"平均 × 5"，实测摘要显示 6.25 星——满意度区间是 [0.5,1.5]、
        // 1.0 是中性，不是 [0,1]。星级这种玩家天天看的数字，两处算法不一致
        // 比算错更糟：他会以为存档界面在骗他。
        float stars = 0f;
        if (samples != null && samples.Count > 0)
        {
            float sum = 0f;
            for (int i = 0; i < samples.Count; i++) sum += samples[i];
            float average = sum / samples.Count;
            stars = 1f + (average - ReputationLedger.MinSatisfaction) * 4f;
            if (stars < 1f) stars = 1f;
            else if (stars > 5f) stars = 5f;
        }

        int openRooms = state.sim != null && state.sim.roomStates != null
                      ? state.sim.roomStates.Count : 0;

        return new SaveSlotSummary(slot, true, day, cash, stars, openRooms,
                                   state.version);
    }
}
