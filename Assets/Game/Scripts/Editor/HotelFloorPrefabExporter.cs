#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HotelFloorPrefabExporter
{
    public const string TargetScene = "Assets/Game/Scenes/Hotel_Manager_25D.unity";
    public const string PrefabFolder = "Assets/Game/Prefabs/Maps/Floors";
    private const string PortableMaterialFolder = "Assets/Game/Materials/FloorMaps/Extracted";

    private readonly struct FloorSpec
    {
        public readonly int Number;
        public readonly string RootName;
        public readonly string AssetName;
        public readonly string Label;

        public FloorSpec(int number, string rootName, string assetName, string label)
        {
            Number = number;
            RootName = rootName;
            AssetName = assetName;
            Label = label;
        }

        public string AssetPath => PrefabFolder + "/" + AssetName + ".prefab";
    }

    private static readonly FloorSpec[] Floors =
    {
        new FloorSpec(1, "Floor1", "Floor1", "Lobby and staff facilities"),
        new FloorSpec(2, "Floor2", "Floor2", "Guest rooms 201-212"),
        new FloorSpec(3, "Floor3", "Floor3", "Guest rooms 301-312"),
        new FloorSpec(4, "Floor4", "Floor4", "Restaurant and kitchen"),
        new FloorSpec(5, "Floor5", "Floor5", "Gym"),
        new FloorSpec(6, "Floor6", "Floor6", "Casino"),
        new FloorSpec(7, "Floor7", "Floor7", "Pool deck")
    };

    [MenuItem("Old Town Hotel/Maps/Create or Update Floor Prefabs")]
    private static void ExportFromMenu()
    {
        if (!CanEditTargetScene()) return;

        bool hasExistingAssets = false;
        for (int i = 0; i < Floors.Length; i++)
            hasExistingAssets |= AssetDatabase.LoadAssetAtPath<GameObject>(Floors[i].AssetPath) != null;

        if (hasExistingAssets && !EditorUtility.DisplayDialog(
                "Update floor prefabs?",
                "This writes the current Hotel_Manager_25D floor hierarchy back to all seven floor prefabs. "
                + "Use this only when scene-instance edits should become the new prefab defaults.",
                "Update prefabs",
                "Cancel"))
        {
            return;
        }

        ExportSceneFloors(true, true);
    }

    [MenuItem("Old Town Hotel/Maps/Validate Floor Prefabs")]
    private static void ValidateFromMenu()
    {
        ValidateFloorPrefabs(true);
    }

    [MenuItem("Old Town Hotel/Maps/Select Floor Prefab Folder")]
    private static void SelectFolder()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(PrefabFolder);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Old Town Hotel/Maps/Open Floor Prefab/Floor 1 - Lobby")]
    private static void OpenFloor1() => OpenFloor(1);

    [MenuItem("Old Town Hotel/Maps/Open Floor Prefab/Floor 2 - Guest Rooms")]
    private static void OpenFloor2() => OpenFloor(2);

    [MenuItem("Old Town Hotel/Maps/Open Floor Prefab/Floor 3 - Guest Rooms")]
    private static void OpenFloor3() => OpenFloor(3);

    [MenuItem("Old Town Hotel/Maps/Open Floor Prefab/Floor 4 - Restaurant")]
    private static void OpenFloor4() => OpenFloor(4);

    [MenuItem("Old Town Hotel/Maps/Open Floor Prefab/Floor 5 - Gym")]
    private static void OpenFloor5() => OpenFloor(5);

    [MenuItem("Old Town Hotel/Maps/Open Floor Prefab/Floor 6 - Casino")]
    private static void OpenFloor6() => OpenFloor(6);

    [MenuItem("Old Town Hotel/Maps/Open Floor Prefab/Floor 7 - Pool")]
    private static void OpenFloor7() => OpenFloor(7);

    public static int ExportSceneFloors(bool overwriteExisting, bool saveScene)
    {
        if (!CanEditTargetScene()) return 0;

        EnsureFolder(PrefabFolder);
        Scene scene = SceneManager.GetActiveScene();
        Transform world = FindRoot(scene, "World");
        if (world == null)
        {
            Debug.LogError("Floor prefab export failed: World root was not found.");
            return 0;
        }

        if (overwriteExisting)
        {
            UnpackSceneFloorPrefabs();
            HotelSceneExpansion.EnsureEditableGuestFloorVisuals(
                world.Find("Floor2"),
                world.Find("Floor3"));
        }

        int exported = 0;
        for (int i = 0; i < Floors.Length; i++)
        {
            Transform floor = world.Find(Floors[i].RootName);
            if (floor == null)
            {
                Debug.LogError("Floor prefab export skipped missing root: " + Floors[i].RootName);
                continue;
            }

            if (!overwriteExisting
                && AssetDatabase.LoadAssetAtPath<GameObject>(Floors[i].AssetPath) != null)
            {
                continue;
            }

            if (ExportFloor(floor.gameObject, Floors[i])) exported++;
        }

        ReconnectFloorVisibility(world);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene) EditorSceneManager.SaveScene(scene);

        Debug.Log("Floor prefab export complete: " + exported + "/" + Floors.Length
                  + " floors connected under " + PrefabFolder + ".");
        return exported;
    }

    public static void UnpackSceneFloorPrefabs()
    {
        if (!CanEditTargetScene()) return;

        Scene scene = SceneManager.GetActiveScene();
        Transform world = FindRoot(scene, "World");
        if (world == null) return;

        for (int i = 0; i < Floors.Length; i++)
        {
            Transform floor = world.Find(Floors[i].RootName);
            if (floor == null || !PrefabUtility.IsAnyPrefabInstanceRoot(floor.gameObject)) continue;

            PrefabUtility.UnpackPrefabInstance(
                floor.gameObject,
                PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction);
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static bool ValidateFloorPrefabs(bool logResult)
    {
        var errors = new List<string>();
        for (int i = 0; i < Floors.Length; i++)
        {
            FloorSpec spec = Floors[i];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.AssetPath);
            if (prefab == null)
            {
                errors.Add(spec.AssetPath + " is missing");
                continue;
            }

            if (prefab.name != spec.RootName)
                errors.Add(spec.AssetPath + " root must be named " + spec.RootName);
            if (prefab.transform.localPosition.sqrMagnitude > 0.0001f)
                errors.Add(spec.AssetPath + " root position must be zero");
            if (prefab.transform.Find("Ground") == null)
                errors.Add(spec.AssetPath + " has no Ground child");
            if (prefab.transform.Find("ArtPass_LowPoly") == null)
                errors.Add(spec.AssetPath + " has no ArtPass_LowPoly child");
            if (prefab.transform.Find("ElevatorPad") == null)
                errors.Add(spec.AssetPath + " has no ElevatorPad child");

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                    {
                        errors.Add(spec.AssetPath + " has a missing material on "
                                   + HierarchyPath(renderers[rendererIndex].transform));
                        continue;
                    }
                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
                        errors.Add(spec.AssetPath + " has a non-persistent material on "
                                   + HierarchyPath(renderers[rendererIndex].transform));
                }
            }

            if (spec.Number == 2 || spec.Number == 3)
            {
                const int expectedRooms = 12;
                int roomCount = prefab.GetComponentsInChildren<RoomSceneBinder>(true).Length;
                int doorCount = prefab.GetComponentsInChildren<RoomDoor>(true).Length;
                if (roomCount != expectedRooms)
                    errors.Add(spec.AssetPath + " has " + roomCount + " room binders; expected 12");
                if (doorCount != expectedRooms)
                    errors.Add(spec.AssetPath + " has " + doorCount + " room doors; expected 12");
            }
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded && scene.path == TargetScene)
        {
            Transform world = FindRoot(scene, "World");
            for (int i = 0; world != null && i < Floors.Length; i++)
            {
                Transform floor = world.Find(Floors[i].RootName);
                if (floor == null) continue;
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(floor.gameObject);
                if (sourcePath != Floors[i].AssetPath)
                    errors.Add(Floors[i].RootName + " scene instance is not connected to "
                               + Floors[i].AssetPath);
                float expectedY = FloorMath.BaseYFor(Floors[i].Number - 1);
                if (Mathf.Abs(floor.localPosition.y - expectedY) > 0.001f)
                    errors.Add(Floors[i].RootName + " scene height is " + floor.localPosition.y
                               + "; expected " + expectedY);
            }
        }

        if (logResult)
        {
            if (errors.Count == 0)
                Debug.Log("Floor prefab validation passed: all seven editable maps are healthy and connected.");
            else
                Debug.LogError("Floor prefab validation failed:\n- " + string.Join("\n- ", errors));
        }
        return errors.Count == 0;
    }

    private static bool ExportFloor(GameObject floorRoot, FloorSpec spec)
    {
        if (PrefabUtility.IsAnyPrefabInstanceRoot(floorRoot))
        {
            PrefabUtility.UnpackPrefabInstance(
                floorRoot,
                PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction);
        }

        Transform parent = floorRoot.transform.parent;
        int siblingIndex = floorRoot.transform.GetSiblingIndex();
        Transform rootTransform = floorRoot.transform;
        Vector3 scenePosition = new Vector3(
            rootTransform.localPosition.x,
            FloorMath.BaseYFor(spec.Number - 1),
            rootTransform.localPosition.z);
        Quaternion sceneRotation = rootTransform.localRotation;
        Vector3 sceneScale = rootTransform.localScale;
        bool sceneActive = floorRoot.activeSelf;

        EnsurePortableRendererMaterials(rootTransform, spec.Number);

        // Prefab contents author at the origin. The vertical floor offset remains
        // a scene-instance override, so opening a floor asset is easy to frame.
        rootTransform.localPosition = Vector3.zero;
        rootTransform.localRotation = Quaternion.identity;
        rootTransform.localScale = Vector3.one;
        floorRoot.SetActive(true);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            floorRoot,
            spec.AssetPath,
            out bool success);

        if (!success || prefab == null)
        {
            rootTransform.localPosition = scenePosition;
            rootTransform.localRotation = sceneRotation;
            rootTransform.localScale = sceneScale;
            floorRoot.SetActive(sceneActive);
            Debug.LogError("Could not export " + spec.RootName + " to " + spec.AssetPath);
            return false;
        }

        GameObject connected = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        connected.name = spec.RootName;
        connected.transform.SetSiblingIndex(siblingIndex);
        connected.transform.localPosition = scenePosition;
        connected.transform.localRotation = sceneRotation;
        connected.transform.localScale = sceneScale;
        connected.SetActive(sceneActive);
        UnityEngine.Object.DestroyImmediate(floorRoot);

        PrefabUtility.RecordPrefabInstancePropertyModifications(connected.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(connected);
        EditorUtility.SetDirty(connected);
        Debug.Log("Exported " + spec.RootName + " (" + spec.Label + ") -> " + spec.AssetPath);
        return true;
    }

    private static void EnsurePortableRendererMaterials(Transform floorRoot, int floorNumber)
    {
        EnsureFolder(PortableMaterialFolder);
        Renderer[] renderers = floorRoot.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null) continue;

            Material[] current = renderer.sharedMaterials;
            int slotCount = ExpectedMaterialSlotCount(renderer, current.Length);
            var portable = new Material[slotCount];
            bool changed = current.Length != slotCount;
            for (int slot = 0; slot < slotCount; slot++)
            {
                Material material = slot < current.Length ? current[slot] : null;
                if (renderer.name == "StateChip")
                {
                    material = LoadPaletteMaterial(HotelArtMaterial.White);
                    changed = true;
                }
                else if (material == null)
                {
                    material = FallbackMaterialFor(renderer);
                    changed = true;
                }
                else if (material.shader == null)
                {
                    material = FallbackMaterialFor(renderer);
                    changed = true;
                }
                else if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
                {
                    material = ExtractMaterial(material, renderer, floorNumber, slot);
                    changed = true;
                }
                portable[slot] = material;
            }

            if (!changed) continue;
            renderer.sharedMaterials = portable;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static int ExpectedMaterialSlotCount(Renderer renderer, int currentCount)
    {
        if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            return Mathf.Max(1, skinned.sharedMesh.subMeshCount);
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            return Mathf.Max(1, filter.sharedMesh.subMeshCount);
        return Mathf.Max(1, currentCount);
    }

    private static Material ExtractMaterial(
        Material source,
        Renderer renderer,
        int floorNumber,
        int slot)
    {
        string identity = HierarchyPath(renderer.transform) + "|" + slot + "|"
                          + source.name + "|" + source.shader.name + "|" + source.color;
        string assetName = "Mat_F" + floorNumber + "_" + Sanitize(renderer.name)
                           + "_" + StableHash(identity).ToString("x8") + "_" + slot + ".mat";
        string path = PortableMaterialFolder + "/" + assetName;
        Material extracted = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (extracted != null) return extracted;

        extracted = new Material(source) { name = assetName.Substring(0, assetName.Length - 4) };
        AssetDatabase.CreateAsset(extracted, path);
        return extracted;
    }

    private static Material FallbackMaterialFor(Renderer renderer)
    {
        string identity = renderer.name.ToLowerInvariant();
        HotelArtMaterial material;
        if (identity.Contains("water")) material = HotelArtMaterial.Water;
        else if (identity.Contains("darkness") || identity.Contains("screen"))
            material = HotelArtMaterial.Black;
        else if (identity.Contains("doorpanel")) material = HotelArtMaterial.WoodWarm;
        else if (identity.Contains("elevatorshaft")) material = HotelArtMaterial.MetalDark;
        else if (identity.Contains("elevatorpad")) material = HotelArtMaterial.Brass;
        else if (identity.Contains("wall")) material = HotelArtMaterial.WallSage;
        else if (identity.Contains("ground") || identity.Contains("floor")
                 || identity.Contains("slab")) material = HotelArtMaterial.TileCream;
        else if (identity.Contains("sofa")) material = HotelArtMaterial.FabricRed;
        else if (identity.Contains("mat") || identity.Contains("rug"))
            material = HotelArtMaterial.CarpetBlue;
        else if (identity.Contains("chair") || identity.Contains("lounger"))
            material = HotelArtMaterial.FabricTeal;
        else if (identity.Contains("basin") || identity.Contains("kettle")
                 || identity.Contains("cup")) material = HotelArtMaterial.MetalLight;
        else if (identity.Contains("table") || identity.Contains("counter")
                 || identity.Contains("rack") || identity.Contains("stand")
                 || identity.Contains("bar") || identity.Contains("bench"))
            material = HotelArtMaterial.WoodWarm;
        else if (identity.Contains("slot") || identity.Contains("casino"))
            material = HotelArtMaterial.Brass;
        else material = HotelArtMaterial.Stone;

        return LoadPaletteMaterial(material);
    }

    private static Material LoadPaletteMaterial(HotelArtMaterial material)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Game/Resources/HotelArt/Mat_" + material + ".mat");
    }

    private static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        while (transform != null)
        {
            names.Add(transform.name);
            transform = transform.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 16777619;
            return hash;
        }
    }

    private static string Sanitize(string value)
    {
        var result = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            result.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }
        return result.ToString();
    }

    private static void ReconnectFloorVisibility(Transform world)
    {
        FloorVisibilityController visibility = world.GetComponent<FloorVisibilityController>();
        if (visibility == null) return;

        var serialized = new SerializedObject(visibility);
        SerializedProperty roots = serialized.FindProperty("floorRoots");
        if (roots == null) return;
        roots.arraySize = Floors.Length;
        for (int i = 0; i < Floors.Length; i++)
        {
            Transform floor = world.Find(Floors[i].RootName);
            roots.GetArrayElementAtIndex(i).objectReferenceValue =
                floor != null ? floor.gameObject : null;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(visibility);
    }

    private static void OpenFloor(int floorNumber)
    {
        for (int i = 0; i < Floors.Length; i++)
        {
            if (Floors[i].Number != floorNumber) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Floors[i].AssetPath);
            if (prefab == null)
            {
                Debug.LogError("Floor prefab is missing: " + Floors[i].AssetPath);
                return;
            }
            AssetDatabase.OpenAsset(prefab);
            return;
        }
    }

    private static bool CanEditTargetScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Floor prefabs cannot be changed while entering or running Play Mode.");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != TargetScene)
        {
            Debug.LogError("Open " + TargetScene + " before editing floor prefabs.");
            return false;
        }
        return true;
    }

    private static Transform FindRoot(Scene scene, string rootName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == rootName) return roots[i].transform;
        return null;
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
