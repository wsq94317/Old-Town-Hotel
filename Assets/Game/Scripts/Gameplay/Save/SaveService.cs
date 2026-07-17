using System.IO;
using UnityEngine;

// Single-slot JSON save to Application.persistentDataPath. Path-based overloads
// exist so tests can round-trip through a temp file without touching the real slot.
public static class SaveService
{
    private const string FileName = "slot0.json";

    public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool HasSave() => File.Exists(DefaultPath);

    public static void Save(GameState state) => SaveTo(DefaultPath, state);

    public static GameState Load() => LoadFrom(DefaultPath);

    public static void Delete()
    {
        if (File.Exists(DefaultPath)) File.Delete(DefaultPath);
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
        try { return JsonUtility.FromJson<GameState>(File.ReadAllText(path)); }
        catch { return null; }
    }
}
