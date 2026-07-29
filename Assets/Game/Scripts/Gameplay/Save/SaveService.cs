using System.IO;
using UnityEngine;

// JSON 存档，写到 Application.persistentDataPath。
// **三个槽位**（SaveSlots）：Save()/Load() 走当前槽位，SaveToSlot/LoadSlot 指定槽位。
// 基于路径的重载留着，测试靠它在临时文件里往返而不碰玩家的真档。
public static class SaveService
{
    /// <summary>老的单槽文件名。保留常量是为了迁移（SaveSlots.MigrateLegacySlotIfNeeded）。</summary>
    private const string LegacyFileName = "slot0.json";

    /// <summary>当前槽位的路径。加槽位之前这里是 slot0.json，
    /// 老档由 MigrateLegacySlotIfNeeded 搬进 1 号槽。</summary>
    public static string DefaultPath => SaveSlots.PathOf(SaveSlots.ActiveSlot);

    public static string LegacyPath => Path.Combine(Application.persistentDataPath, LegacyFileName);

    public static bool HasSave() => File.Exists(DefaultPath) || File.Exists(LegacyPath);

    public static void Save(GameState state) => SaveTo(DefaultPath, state);

    /// <summary>读当前槽位。槽位文件不存在时回落到老的单槽档
    /// （老玩家第一次进新版本，进度不能凭空消失）。</summary>
    public static GameState Load()
    {
        GameState state = LoadFrom(DefaultPath);
        return state ?? LoadFrom(LegacyPath);
    }

    public static void Delete()
    {
        if (File.Exists(DefaultPath)) File.Delete(DefaultPath);
    }

    // ── 槽位 ─────────────────────────────────────────────────────────────────

    public static void SaveToSlot(int slot, GameState state)
    {
        if (!SaveSlots.IsValid(slot)) return;
        SaveTo(SaveSlots.PathOf(slot), state);
    }

    public static GameState LoadSlot(int slot) =>
        SaveSlots.IsValid(slot) ? LoadFrom(SaveSlots.PathOf(slot)) : null;

    public static void DeleteSlot(int slot)
    {
        if (!SaveSlots.IsValid(slot)) return;
        string path = SaveSlots.PathOf(slot);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>槽位摘要（UI 用）。**读不出来就当空槽**，不要让一个坏文件
    /// 把整个存档界面炸掉——玩家至少还能存到别的槽位自救。</summary>
    public static SaveSlotSummary SummaryOf(int slot)
    {
        if (!SaveSlots.IsValid(slot)) return SaveSlotSummary.Empty(slot);
        GameState state = LoadSlot(slot);
        return state == null ? SaveSlotSummary.Empty(slot)
                             : SaveSlotSummary.From(slot, state);
    }

    public static void SaveTo(string path, GameState state)
    {
        if (state == null || string.IsNullOrEmpty(path)) return;
        // 原子写：先写临时文件再替换——移动端进程被杀在写一半时，旧档还在
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonUtility.ToJson(state, true));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    public static GameState LoadFrom(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            var state = JsonUtility.FromJson<GameState>(File.ReadAllText(path));
            // 旧档补齐（v2/v3 → v4）：只补默认值，不丢任何已有数据
            state?.MigrateToCurrentVersion();
            return state;
        }
        catch { return null; }
    }
}
