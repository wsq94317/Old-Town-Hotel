#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HotelFurniturePrefabExporter
{
    private const string RoomPrefabFolder = "Assets/Game/Resources/HotelArt/Prefabs";
    private const string ScenePrefabFolder = "Assets/Game/Prefabs/Furniture/Scene";
    private const string CatalogPath = "Assets/Game/Resources/HotelArt/FurnitureVisualCatalog.asset";
    private const string ArtRootName = "ArtPass_LowPoly";

    private static readonly string[] FurniturePrefixes =
    {
        "ReceptionDesk", "LobbySofa", "LobbyCoffeeTable", "CoffeeTable", "CoffeeCup",
        "Plant", "LobbyPlant", "LuggageCart", "LobbyRug", "EntryRunner", "QueuePost",
        "BarStool", "LoungeBar", "GuestLounge", "HousekeepingCart", "DiningRug",
        "RestaurantBar", "BarChair", "DiningSet", "ServiceStation", "HostStand",
        "Treadmill", "WeightBench", "WeightRack", "WaterCooler", "TowelRack",
        "SpinBike", "PunchingBag", "ExerciseBall", "YogaMat", "TableRug",
        "CasinoTable", "SlotMachine", "SlotChair", "CasinoCashier", "VelvetRope",
        "RopePost", "RopeCap", "PoolLounger", "Umbrella", "PoolSideTable",
        "LifeRing", "PoolBar"
    };

    [MenuItem("Old Town Hotel/Art/Export All Furniture Prefabs")]
    public static void ExportAllFurniturePrefabs()
    {
        EnsureFolder(RoomPrefabFolder);
        EnsureFolder(ScenePrefabFolder);
        int roomKinds = BuildRoomVisualCatalog();
        int sceneItems = ExportSceneFurniture();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Furniture prefab export complete: " + roomKinds
                  + " room kinds catalogued, " + sceneItems + " scene placements processed.");
    }

    private static int BuildRoomVisualCatalog()
    {
        FurnitureVisualCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<FurnitureVisualCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<FurnitureVisualCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        int count = 0;
        foreach (FurnitureKind kind in FurnitureCatalog.All)
        {
            string prefabPath = RoomPrefabFolder + "/LP_Furniture_" + kind.kindId + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject model = LowPolyHotelKit.CreateFurnitureProcedural(kind);
                model.name = "LP_Furniture_" + kind.kindId;
                LowPolyHotelKit.SetLayerRecursively(model, LowPolyHotelKit.ArtLayer);
                LowPolyHotelKit.RemoveColliders(model);
                prefab = PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
                UnityEngine.Object.DestroyImmediate(model);
            }
            catalog.EnsureDefaultEntry(kind.kindId, prefab);
            count++;
        }
        EditorUtility.SetDirty(catalog);
        return count;
    }

    private static int ExportSceneFurniture()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return 0;

        var artRoots = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
            FindArtRoots(root.transform, artRoots);

        int processed = 0;
        bool sceneChanged = false;
        for (int rootIndex = 0; rootIndex < artRoots.Count; rootIndex++)
        {
            Transform artRoot = artRoots[rootIndex];
            string floorName = artRoot.parent == null ? "Shared" : artRoot.parent.name;
            string floorFolder = ScenePrefabFolder + "/" + Sanitize(floorName);
            EnsureFolder(floorFolder);

            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            var candidates = new List<GameObject>();
            for (int i = 0; i < artRoot.childCount; i++)
            {
                GameObject item = artRoot.GetChild(i).gameObject;
                if (IsFurnitureName(item.name)) candidates.Add(item);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject item = candidates[i];
                string baseName = CleanName(item.name);
                occurrences.TryGetValue(baseName, out int occurrence);
                occurrences[baseName] = occurrence + 1;
                string stableId = floorName + "/" + baseName + "#" + occurrence;
                string path = floorFolder + "/" + Sanitize(baseName) + "_" + occurrence.ToString("00") + ".prefab";

                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(item);
                if (prefab == null)
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        item = PrefabUtility.SaveAsPrefabAssetAndConnect(
                            item, path, InteractionMode.AutomatedAction, out bool success);
                        if (!success || item == null)
                        {
                            Debug.LogError("Could not export furniture prefab: " + stableId);
                            continue;
                        }
                        prefab = PrefabUtility.GetCorrespondingObjectFromSource(item);
                        sceneChanged = true;
                    }
                    else
                    {
                        item = ReplaceWithPrefab(item, prefab);
                        sceneChanged = true;
                    }
                }

                SceneFurnitureSlot slot = item.GetComponent<SceneFurnitureSlot>();
                if (slot == null)
                {
                    slot = item.AddComponent<SceneFurnitureSlot>();
                    sceneChanged = true;
                }
                if (!slot.HasDefaultConfiguration(stableId, prefab))
                {
                    slot.ConfigureDefault(stableId, prefab);
                    EditorUtility.SetDirty(slot);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
                    sceneChanged = true;
                }
                processed++;
            }
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        return processed;
    }

    private static GameObject ReplaceWithPrefab(GameObject source, GameObject prefab)
    {
        Transform parent = source.transform.parent;
        int sibling = source.transform.GetSiblingIndex();
        Vector3 position = source.transform.localPosition;
        Quaternion rotation = source.transform.localRotation;
        Vector3 scale = source.transform.localScale;
        string name = source.name;

        GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        replacement.name = name;
        replacement.transform.SetSiblingIndex(sibling);
        replacement.transform.localPosition = position;
        replacement.transform.localRotation = rotation;
        replacement.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(source);
        return replacement;
    }

    private static bool IsFurnitureName(string rawName)
    {
        string name = CleanName(rawName);
        for (int i = 0; i < FurniturePrefixes.Length; i++)
            if (name.StartsWith(FurniturePrefixes[i], StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void FindArtRoots(Transform root, List<Transform> results)
    {
        if (root.name == ArtRootName) results.Add(root);
        for (int i = 0; i < root.childCount; i++) FindArtRoots(root.GetChild(i), results);
    }

    private static string CleanName(string name)
    {
        const string clone = "(Clone)";
        return name.EndsWith(clone, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - clone.Length).Trim()
            : name;
    }

    private static string Sanitize(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return name.Replace(' ', '_');
    }

    private static void EnsureFolder(string folder)
    {
        string[] segments = folder.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
}
#endif
