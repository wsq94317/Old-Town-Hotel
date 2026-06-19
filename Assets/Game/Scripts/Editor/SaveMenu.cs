using System.IO;
using UnityEditor;
using UnityEngine;

// Editor utilities for the save slot. Menu: "Old Town Hotel/Save".
public static class SaveMenu
{
    [MenuItem("Old Town Hotel/Save/Delete Save Slot")]
    public static void DeleteSave()
    {
        string path = SaveService.DefaultPath;
        if (!SaveService.HasSave())
        {
            EditorUtility.DisplayDialog("Old Town Hotel — Save",
                "No save file found. Nothing to delete.\n\n" + path, "OK");
            return;
        }

        if (EditorUtility.DisplayDialog("Delete Save Slot?",
            "This permanently deletes the save file:\n\n" + path +
            "\n\nTakes effect on the next Play (a fresh game starts).",
            "Delete", "Cancel"))
        {
            SaveService.Delete();
            Debug.Log("[OTH] Save slot deleted: " + path);
        }
    }

    [MenuItem("Old Town Hotel/Save/Reveal Save Location")]
    public static void Reveal()
    {
        string path = SaveService.DefaultPath;
        EditorUtility.RevealInFinder(File.Exists(path) ? path : Application.persistentDataPath);
    }

    [MenuItem("Old Town Hotel/Save/Log Save Status")]
    public static void LogStatus()
    {
        Debug.Log($"[OTH] HasSave={SaveService.HasSave()}\nPath={SaveService.DefaultPath}");
    }
}
