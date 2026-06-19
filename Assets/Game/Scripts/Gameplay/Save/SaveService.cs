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
        File.WriteAllText(path, JsonUtility.ToJson(state, true));
    }

    public static GameState LoadFrom(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try { return JsonUtility.FromJson<GameState>(File.ReadAllText(path)); }
        catch { return null; }
    }
}
