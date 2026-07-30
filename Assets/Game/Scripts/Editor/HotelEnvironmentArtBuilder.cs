using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HotelEnvironmentArtBuilder
{
    private const string TargetScene = "Assets/Game/Scenes/Hotel_Manager_25D.unity";
    private const string MaterialFolder = "Assets/Game/Resources/HotelArt";
    private const string PrefabFolder = MaterialFolder + "/Prefabs";
    private const string ArtRootName = "ArtPass_LowPoly";
    private const float GuestWallHeight = 1.25f;
    private const float CutawayWallHeight = 0.80f;

    private static readonly string[] ReplacedGreyboxPaths =
    {
        "World/Floor1/FrontDesk",
        "World/Floor1/LoungeBar",
        "World/Floor1/Sofa_1",
        "World/Floor1/Sofa_2",
        "World/Floor1/GeneratedSetDressing",
        "World/Floor4/BarCounter",
        "World/Floor4/Table_A",
        "World/Floor4/Table_B",
        "World/Floor4/Table_C",
        "World/Floor4/Table_D",
        "World/Floor5/Mat_A",
        "World/Floor5/Mat_B",
        "World/Floor5/Rack",
        "World/Floor5/Bench",
        "World/Floor6/CasinoTable_A",
        "World/Floor6/CasinoTable_B",
        "World/Floor6/CasinoTable_C",
        "World/Floor6/Slots_A",
        "World/Floor6/Slots_B",
        "World/Floor6/Slots_C",
        "World/Floor7/PoolWater",
        "World/Floor7/DeckChair_A",
        "World/Floor7/DeckChair_B",
        "World/Floor7/PoolBar"
    };

    [MenuItem("Old Town Hotel/Art/Rebuild Low Poly Hotel")]
    public static void RebuildLowPolyHotel()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != TargetScene)
        {
            Debug.LogError("Hotel art pass requires the active scene: " + TargetScene);
            return;
        }

        EnsureFolders();
        EnsureMaterialAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        BuildFurniturePrefabs(false);
        TuneLighting();
        RemoveArtRoots();
        SetGreyboxReplacementRenderers(false);

        Transform floor1 = FindPath("World/Floor1");
        Transform floor2 = FindPath("World/Floor2");
        Transform floor3 = FindPath("World/Floor3");
        Transform floor4 = FindPath("World/Floor4");
        Transform floor5 = FindPath("World/Floor5");
        Transform floor6 = FindPath("World/Floor6");
        Transform floor7 = FindPath("World/Floor7");

        if (floor1 != null) BuildLobby(CreateArtRoot(floor1));
        if (floor2 != null) BuildGuestFloor(CreateArtRoot(floor2), 200, 8);
        if (floor3 != null) BuildGuestFloor(CreateArtRoot(floor3), 300, 4);
        if (floor4 != null) BuildRestaurant(CreateArtRoot(floor4));
        if (floor5 != null) BuildGym(CreateArtRoot(floor5));
        if (floor6 != null) BuildCasino(CreateArtRoot(floor6));
        if (floor7 != null) BuildPool(CreateArtRoot(floor7));

        MarkArtBatchingStatic();
        ValidateLowPolyHotelInternal(true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = FindPath("World/Floor1/" + ArtRootName);
        Debug.Log("Low-poly hotel art rebuilt from scene anchors and saved.");
    }

    [MenuItem("Old Town Hotel/Art/Validate Low Poly Hotel")]
    public static void ValidateLowPolyHotel()
    {
        ValidateLowPolyHotelInternal(true);
    }

    [MenuItem("Old Town Hotel/Art/Remove Low Poly Hotel")]
    public static void RemoveLowPolyHotel()
    {
        Scene scene = SceneManager.GetActiveScene();
        RemoveArtRoots();
        SetGreyboxReplacementRenderers(true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Low-poly hotel art removed; greybox renderers restored.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Game/Resources");
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        string parent = path.Substring(0, split);
        string name = path.Substring(split + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void TuneLighting()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null || lights[i].type != LightType.Directional) continue;
            lights[i].color = new Color(1.0f, 0.94f, 0.86f);
            lights[i].intensity = 1.18f;
            lights[i].shadows = LightShadows.Soft;
            lights[i].shadowStrength = 0.58f;
            EditorUtility.SetDirty(lights[i]);
            break;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.56f, 0.67f, 0.74f);
        RenderSettings.ambientEquatorColor = new Color(0.40f, 0.36f, 0.33f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.18f);
        RenderSettings.ambientIntensity = 1.0f;
    }

    private static void EnsureMaterialAssets()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("URP Lit shader is unavailable; hotel materials were not generated.");
            return;
        }

        foreach (HotelArtMaterial id in System.Enum.GetValues(typeof(HotelArtMaterial)))
        {
            string path = MaterialFolder + "/Mat_" + id + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Mat_" + id };
                AssetDatabase.CreateAsset(material, path);
            }

            Color color = HotelArtPalette.ColorOf(id);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", HotelArtPalette.SmoothnessOf(id));
            if (material.HasProperty("_Metallic"))
            {
                bool metallic = id == HotelArtMaterial.Brass
                                || id == HotelArtMaterial.MetalDark
                                || id == HotelArtMaterial.MetalLight;
                material.SetFloat("_Metallic", metallic ? 0.58f : 0f);
            }
            material.enableInstancing = true;

            bool emissive = id == HotelArtMaterial.Warning || id == HotelArtMaterial.Water;
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emissive ? color * 0.35f : Color.black);
                if (emissive)
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
            }
            EditorUtility.SetDirty(material);
        }
    }

    [MenuItem("Old Town Hotel/Art/Rebuild Furniture Prefabs")]
    public static void RebuildFurniturePrefabs()
    {
        EnsureFolders();
        EnsureMaterialAssets();
        BuildFurniturePrefabs(true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Low-poly furniture prefabs rebuilt.");
    }

    private static void BuildFurniturePrefabs(bool overwriteExisting)
    {
        foreach (FurnitureKind kind in FurnitureCatalog.All)
        {
            string prefabPath = PrefabFolder + "/LP_Furniture_" + kind.kindId + ".prefab";
            if (!overwriteExisting
                && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                continue;
            }

            GameObject model = LowPolyHotelKit.CreateFurnitureProcedural(kind);
            model.name = "LP_Furniture_" + kind.kindId;
            LowPolyHotelKit.SetLayerRecursively(model, LowPolyHotelKit.ArtLayer);
            LowPolyHotelKit.RemoveColliders(model);
            PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            Object.DestroyImmediate(model);
        }
        AssetDatabase.SaveAssets();
    }

    private static Transform CreateArtRoot(Transform floor)
    {
        var root = new GameObject(ArtRootName);
        root.layer = LowPolyHotelKit.ArtLayer;
        root.transform.SetParent(floor, false);
        return root.transform;
    }

    private static void BuildLobby(Transform root)
    {
        LowPolyHotelKit.BuildLobbyDecor(root);
        LowPolyHotelKit.BuildReceptionKeyWall(root, new Vector3(0f, 0f, 5.72f));
        AddVisibleFloorTrim(root);
        AddElevatorFrame(root);

        BuildWallPanel(root, new Vector3(-4.9f, 0f, 5.82f), 5.7f, 0f);
        BuildWallPanel(root, new Vector3(3.1f, 0f, 5.82f), 4.7f, 0f);

        for (int i = 0; i < 3; i++)
        {
            float x = -7.2f + i * 1.2f;
            LowPolyHotelKit.BuildChairAt(
                root, "BarStool_" + i, new Vector3(x, 0f, 2.35f), 180f,
                HotelArtMaterial.FabricMustard);
        }

        var loungeBar = LowPolyHotelKit.NewChild(root, "LoungeBar_LP", new Vector3(-6f, 0f, 3f));
        LowPolyHotelKit.Part(loungeBar.transform, "Body", PrimitiveType.Cube,
            new Vector3(0f, 0.48f, 0f), new Vector3(3.2f, 0.96f, 0.72f),
            HotelArtMaterial.WoodWarm);
        LowPolyHotelKit.Part(loungeBar.transform, "Top", PrimitiveType.Cube,
            new Vector3(0f, 1.01f, -0.04f), new Vector3(3.4f, 0.12f, 0.88f),
            HotelArtMaterial.Stone);
        LowPolyHotelKit.Part(loungeBar.transform, "FootRail", PrimitiveType.Cylinder,
            new Vector3(0f, 0.22f, -0.47f), new Vector3(0.045f, 1.42f, 0.045f),
            HotelArtMaterial.Brass, new Vector3(0f, 0f, 90f));
        for (int i = 0; i < 7; i++)
        {
            float x = -1.25f + i * 0.42f;
            LowPolyHotelKit.Part(loungeBar.transform, "Bottle_" + i, PrimitiveType.Cylinder,
                new Vector3(x, 1.22f, 0.18f), new Vector3(0.09f, 0.20f, 0.09f),
                i % 2 == 0 ? HotelArtMaterial.FabricRed : HotelArtMaterial.Green);
        }

        BuildWallLamp(root, new Vector3(-7.6f, 0.57f, 5.72f), 0f);
        BuildWallLamp(root, new Vector3(-3.0f, 0.57f, 5.72f), 0f);
        BuildWallLamp(root, new Vector3(2.5f, 0.57f, 5.72f), 0f);
        BuildWallLamp(root, new Vector3(6.2f, 0.57f, 5.72f), 0f);

        var entrance = LowPolyHotelKit.NewChild(root, "EntranceCanopy", new Vector3(0f, 0f, -5.55f));
        LowPolyHotelKit.Part(entrance.transform, "Mat", PrimitiveType.Cube,
            new Vector3(0f, 0.035f, 0.25f), new Vector3(2.3f, 0.07f, 0.85f),
            HotelArtMaterial.CarpetRed);
        LowPolyHotelKit.Part(entrance.transform, "PostL", PrimitiveType.Cylinder,
            new Vector3(-1.25f, 0.82f, 0f), new Vector3(0.10f, 0.82f, 0.10f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(entrance.transform, "PostR", PrimitiveType.Cylinder,
            new Vector3(1.25f, 0.82f, 0f), new Vector3(0.10f, 0.82f, 0.10f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(entrance.transform, "Header", PrimitiveType.Cube,
            new Vector3(0f, 1.62f, 0f), new Vector3(2.75f, 0.20f, 0.30f),
            HotelArtMaterial.WoodDark);
    }

    private static void BuildGuestFloor(Transform root, int roomBase, int roomCount)
    {
        NormalizeGuestFloorWalls(root.parent);
        AddVisibleFloorTrim(root);
        AddElevatorFrame(root);
        LowPolyHotelKit.Part(root, "CorridorRunner", PrimitiveType.Cube,
            new Vector3(-0.35f, 0.025f, 0f), new Vector3(17.2f, 0.05f, 1.32f),
            HotelArtMaterial.CarpetRed);
        LowPolyHotelKit.Part(root, "ElevatorLobbyRug", PrimitiveType.Cube,
            new Vector3(7.8f, 0.03f, 0f), new Vector3(1.75f, 0.06f, 2.25f),
            HotelArtMaterial.CarpetBlue);

        int northCount = Mathf.Min(4, roomCount);
        for (int i = 0; i < northCount; i++)
        {
            int room = roomBase + i + 1;
            float x = -7.5f + i * 5f;
            BuildDoorFrame(root, "Frame_" + room, new Vector3(x, 0f, 1f), false);
            BuildWallLamp(root, new Vector3(x + 1.65f, 0.82f, 0.95f), 0f);
            BuildRoomPlaque(root, new Vector3(x + 1.24f, 0f, 0.94f), false);
        }

        int southCount = Mathf.Max(0, roomCount - 4);
        for (int i = 0; i < southCount; i++)
        {
            int room = roomBase + i + 5;
            float x = -7.5f + i * 5f;
            BuildDoorFrame(root, "Frame_" + room, new Vector3(x, 0f, -1f), true);
            BuildWallLamp(root, new Vector3(x + 1.65f, 0.82f, -0.95f), 180f);
            BuildRoomPlaque(root, new Vector3(x + 1.24f, 0f, -0.94f), true);
        }

        BuildHousekeepingCart(root, new Vector3(-8.8f, 0f, 0.15f));
        for (int i = 0; i < 3; i++)
            LowPolyHotelKit.BuildPlant(root, new Vector3(5.8f + i * 0.65f, 0f, -0.55f), 0.58f);
    }

    private static void NormalizeGuestFloorWalls(Transform floor)
    {
        if (floor == null) return;
        foreach (Transform item in floor.GetComponentsInChildren<Transform>(true))
        {
            if (item == null || !item.name.StartsWith("Wall_")) continue;
            bool outerCutaway = item.parent == floor
                                && (item.name == "Wall_S" || item.name == "Wall_W");
            float height = outerCutaway ? CutawayWallHeight : GuestWallHeight;
            Vector3 scale = item.localScale;
            scale.y = height;
            item.localScale = scale;
            Vector3 position = item.localPosition;
            position.y = height * 0.5f;
            item.localPosition = position;
            EditorUtility.SetDirty(item);
        }
    }

    private static void BuildRestaurant(Transform root)
    {
        AddVisibleFloorTrim(root);
        AddElevatorFrame(root);
        LowPolyHotelKit.Part(root, "DiningRug", PrimitiveType.Cube,
            new Vector3(-0.5f, 0.025f, 0f), new Vector3(16.2f, 0.05f, 9.6f),
            HotelArtMaterial.CarpetRed);

        var bar = LowPolyHotelKit.NewChild(root, "RestaurantBar", new Vector3(-8f, 0f, 0f));
        LowPolyHotelKit.Part(bar.transform, "Counter", PrimitiveType.Cube,
            new Vector3(0f, 0.52f, 0f), new Vector3(0.86f, 1.04f, 5.1f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(bar.transform, "Top", PrimitiveType.Cube,
            new Vector3(0.08f, 1.08f, 0f), new Vector3(1.05f, 0.12f, 5.3f),
            HotelArtMaterial.Brass);
        for (int i = 0; i < 5; i++)
        {
            float z = -2.0f + i;
            LowPolyHotelKit.BuildChairAt(root, "BarChair_" + i,
                new Vector3(-6.95f, 0f, z), 90f, HotelArtMaterial.FabricTeal);
        }
        for (int row = 0; row < 3; row++)
        {
            LowPolyHotelKit.Part(bar.transform, "BottleShelf_" + row, PrimitiveType.Cube,
                new Vector3(-0.65f, 0.58f + row * 0.42f, 0f),
                new Vector3(0.16f, 0.06f, 4.6f), HotelArtMaterial.WoodWarm);
        }

        Vector3[] tablePositions =
        {
            new Vector3(-4f, 0f, 2f),
            new Vector3(-2f, 0f, -2.5f),
            new Vector3(2f, 0f, 1f),
            new Vector3(4.5f, 0f, -2f)
        };
        for (int i = 0; i < tablePositions.Length; i++)
        {
            Vector3 pos = tablePositions[i];
            BuildDiningSet(root, "DiningSet_" + i, pos);
        }

        BuildPlant(root, new Vector3(6.8f, 0f, 4.7f), 1.05f);
        BuildPlant(root, new Vector3(6.8f, 0f, -4.7f), 1.05f);
        BuildRestaurantServiceStation(root, new Vector3(6.7f, 0f, 0.2f));
        BuildHostStand(root, new Vector3(-0.2f, 0f, -4.7f));
    }

    private static void BuildGym(Transform root)
    {
        AddVisibleFloorTrim(root);
        AddElevatorFrame(root);
        LowPolyHotelKit.Part(root, "RubberFloor", PrimitiveType.Cube,
            new Vector3(-0.4f, 0.025f, 0f), new Vector3(16.8f, 0.05f, 9.8f),
            HotelArtMaterial.Rubber);
        LowPolyHotelKit.Part(root, "MirrorWall", PrimitiveType.Cube,
            new Vector3(-0.5f, 0.40f, 5.78f), new Vector3(15.8f, 0.72f, 0.04f),
            HotelArtMaterial.GlassBlue);

        BuildTreadmill(root, new Vector3(-5.5f, 0f, 1.7f));
        BuildTreadmill(root, new Vector3(-3.7f, 0f, 1.7f));
        BuildTreadmill(root, new Vector3(-1.9f, 0f, 1.7f));
        BuildWeightBench(root, new Vector3(1.0f, 0f, -1.8f));
        BuildWeightBench(root, new Vector3(2.7f, 0f, -1.8f));
        BuildWeightRack(root, new Vector3(4.2f, 0f, -3.8f));
        BuildWaterCooler(root, new Vector3(7.0f, 0f, 4.8f));
        BuildTowelRack(root, new Vector3(6.7f, 0f, 3.3f));
        BuildSpinBike(root, new Vector3(1.4f, 0f, 2.0f), 0f);
        BuildSpinBike(root, new Vector3(3.0f, 0f, 2.0f), 0f);
        BuildPunchingBag(root, new Vector3(6.4f, 0f, -0.8f));
        BuildExerciseBallRack(root, new Vector3(-0.1f, 0f, -4.3f));

        LowPolyHotelKit.Part(root, "CardioZoneLine", PrimitiveType.Cube,
            new Vector3(-1.2f, 0.065f, 3.15f), new Vector3(10.8f, 0.025f, 0.07f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(root, "StrengthZoneLine", PrimitiveType.Cube,
            new Vector3(3.8f, 0.065f, -2.85f), new Vector3(7.0f, 0.025f, 0.07f),
            HotelArtMaterial.Brass);

        for (int i = 0; i < 3; i++)
        {
            LowPolyHotelKit.Part(root, "YogaMat_" + i, PrimitiveType.Cube,
                new Vector3(-5.2f + i * 2.0f, 0.055f, -3.0f),
                new Vector3(1.0f, 0.05f, 2.0f),
                i % 2 == 0 ? HotelArtMaterial.FabricTeal : HotelArtMaterial.FabricRed);
        }
    }

    private static void BuildCasino(Transform root)
    {
        AddVisibleFloorTrim(root);
        AddElevatorFrame(root);
        LowPolyHotelKit.Part(root, "CasinoCarpet", PrimitiveType.Cube,
            new Vector3(-0.4f, 0.025f, 0f), new Vector3(16.8f, 0.05f, 9.8f),
            HotelArtMaterial.CarpetBlue);
        LowPolyHotelKit.Part(root, "CasinoWallBand", PrimitiveType.Cube,
            new Vector3(-0.4f, 0.38f, 5.77f), new Vector3(16.6f, 0.62f, 0.045f),
            HotelArtMaterial.AccentRose);
        LowPolyHotelKit.Part(root, "CasinoWallRail", PrimitiveType.Cube,
            new Vector3(-0.4f, 0.70f, 5.73f), new Vector3(16.8f, 0.05f, 0.035f),
            HotelArtMaterial.Brass);
        for (int i = 0; i < 12; i++)
        {
            float x = -7.5f + (i % 6) * 2.8f;
            float z = i < 6 ? 4.5f : -4.5f;
            LowPolyHotelKit.Part(root, "CarpetDot_" + i, PrimitiveType.Cylinder,
                new Vector3(x, 0.065f, z), new Vector3(0.18f, 0.018f, 0.18f),
                HotelArtMaterial.Brass);
        }

        Vector3[] casinoTables =
        {
            new Vector3(-4f, 0f, 1f),
            new Vector3(0f, 0f, -2f),
            new Vector3(4f, 0f, 1.5f)
        };
        float[] casinoYaw = { 20f, -15f, 12f };
        for (int i = 0; i < casinoTables.Length; i++)
        {
            LowPolyHotelKit.Part(root, "TableRug_" + i, PrimitiveType.Cylinder,
                casinoTables[i] + new Vector3(0f, 0.055f, 0f),
                new Vector3(2.30f, 0.018f, 1.85f), HotelArtMaterial.CarpetRed,
                new Vector3(0f, casinoYaw[i], 0f));
            BuildCasinoTable(root, casinoTables[i], casinoYaw[i]);
        }

        for (int i = 0; i < 5; i++)
        {
            BuildSlotMachine(root, new Vector3(-7.4f + i * 1.1f, 0f, -3.2f));
            LowPolyHotelKit.BuildChairAt(root, "SlotChair_" + i,
                new Vector3(-7.4f + i * 1.1f, 0f, -4.15f), 180f,
                HotelArtMaterial.LeatherTan);
        }

        BuildCasinoCashier(root, new Vector3(7.1f, 0f, -3.3f));
        BuildVelvetRope(root, new Vector3(3.8f, 0f, -4.35f), 4);
    }

    private static void BuildPool(Transform root)
    {
        AddVisibleFloorTrim(root);
        AddElevatorFrame(root);
        LowPolyHotelKit.Part(root, "PoolDeck", PrimitiveType.Cube,
            new Vector3(-0.4f, 0.025f, 0f), new Vector3(16.8f, 0.05f, 9.8f),
            HotelArtMaterial.TileCream);

        var pool = LowPolyHotelKit.NewChild(root, "Pool", new Vector3(-2f, 0f, 0.5f));
        LowPolyHotelKit.Part(pool.transform, "Water", PrimitiveType.Cube,
            new Vector3(0f, 0.07f, 0f), new Vector3(8.0f, 0.06f, 5.0f),
            HotelArtMaterial.Water);
        LowPolyHotelKit.Part(pool.transform, "CopingN", PrimitiveType.Cube,
            new Vector3(0f, 0.10f, 2.63f), new Vector3(8.5f, 0.20f, 0.26f),
            HotelArtMaterial.White);
        LowPolyHotelKit.Part(pool.transform, "CopingS", PrimitiveType.Cube,
            new Vector3(0f, 0.10f, -2.63f), new Vector3(8.5f, 0.20f, 0.26f),
            HotelArtMaterial.White);
        LowPolyHotelKit.Part(pool.transform, "CopingW", PrimitiveType.Cube,
            new Vector3(-4.13f, 0.10f, 0f), new Vector3(0.26f, 0.20f, 5.0f),
            HotelArtMaterial.White);
        LowPolyHotelKit.Part(pool.transform, "CopingE", PrimitiveType.Cube,
            new Vector3(4.13f, 0.10f, 0f), new Vector3(0.26f, 0.20f, 5.0f),
            HotelArtMaterial.White);
        for (int i = -1; i <= 1; i += 2)
            LowPolyHotelKit.Part(pool.transform, "LaneMarker_" + i, PrimitiveType.Cube,
                new Vector3(0f, 0.105f, i * 1.25f), new Vector3(7.55f, 0.012f, 0.055f),
                HotelArtMaterial.White);
        BuildPoolLadder(pool.transform, new Vector3(3.5f, 0f, -2.2f));

        BuildLounger(root, new Vector3(4.0f, 0f, 2.0f), -10f);
        BuildLounger(root, new Vector3(5.2f, 0f, 2.0f), -10f);
        BuildLounger(root, new Vector3(6.4f, 0f, 2.0f), -10f);
        BuildUmbrella(root, new Vector3(5.2f, 0f, 3.8f));
        BuildPoolSideTable(root, new Vector3(4.58f, 0f, 1.05f));
        BuildPoolSideTable(root, new Vector3(5.85f, 0f, 1.05f));
        BuildLifeRing(root, new Vector3(-6.9f, 0f, 4.85f));

        var bar = LowPolyHotelKit.NewChild(root, "PoolBar_LP", new Vector3(6.5f, 0f, -3.0f));
        LowPolyHotelKit.Part(bar.transform, "Body", PrimitiveType.Cube,
            new Vector3(0f, 0.45f, 0f), new Vector3(2.3f, 0.90f, 0.78f),
            HotelArtMaterial.WoodLight);
        LowPolyHotelKit.Part(bar.transform, "Top", PrimitiveType.Cube,
            new Vector3(0f, 0.95f, 0f), new Vector3(2.5f, 0.10f, 0.92f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(bar.transform, "Canopy", PrimitiveType.Cube,
            new Vector3(0f, 2.0f, 0f), new Vector3(2.8f, 0.14f, 1.4f),
            HotelArtMaterial.FabricRed);
        for (int x = -1; x <= 1; x += 2)
            LowPolyHotelKit.Part(bar.transform, "CanopyPost", PrimitiveType.Cylinder,
                new Vector3(x * 1.05f, 1.45f, 0.35f), new Vector3(0.06f, 0.55f, 0.06f),
                HotelArtMaterial.WoodDark);
        for (int i = 0; i < 3; i++)
        {
            LowPolyHotelKit.BuildChairAt(root, "PoolBarStool_" + i,
                new Vector3(5.75f + i * 0.75f, 0f, -2.30f), 0f,
                HotelArtMaterial.FabricMustard);
            LowPolyHotelKit.Part(bar.transform, "Bottle_" + i, PrimitiveType.Cylinder,
                new Vector3(-0.60f + i * 0.58f, 1.12f, 0f),
                new Vector3(0.08f, 0.16f, 0.08f),
                i == 1 ? HotelArtMaterial.Green : HotelArtMaterial.AccentRose);
        }
    }

    private static void AddVisibleFloorTrim(Transform root)
    {
        LowPolyHotelKit.Part(root, "CutawayTrim_S", PrimitiveType.Cube,
            new Vector3(0f, 0.13f, -5.86f), new Vector3(19.8f, 0.26f, 0.12f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(root, "CutawayTrim_W", PrimitiveType.Cube,
            new Vector3(-9.86f, 0.13f, 0f), new Vector3(0.12f, 0.26f, 11.7f),
            HotelArtMaterial.WoodDark);
        for (int i = 0; i < 5; i++)
        {
            LowPolyHotelKit.Part(root, "BrassInlay_" + i, PrimitiveType.Cube,
                new Vector3(-7.8f + i * 3.9f, 0.27f, -5.93f),
                new Vector3(1.7f, 0.04f, 0.025f), HotelArtMaterial.Brass);
        }
    }

    private static void AddElevatorFrame(Transform root)
    {
        var frame = LowPolyHotelKit.NewChild(root, "ElevatorFrame_LP", new Vector3(8.56f, 0f, 0f));
        LowPolyHotelKit.Part(frame.transform, "PostN", PrimitiveType.Cube,
            new Vector3(0f, 0.92f, 0.91f), new Vector3(0.16f, 1.84f, 0.13f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(frame.transform, "PostS", PrimitiveType.Cube,
            new Vector3(0f, 0.92f, -0.91f), new Vector3(0.16f, 1.84f, 0.13f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(frame.transform, "Header", PrimitiveType.Cube,
            new Vector3(0f, 1.84f, 0f), new Vector3(0.16f, 0.16f, 1.95f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(frame.transform, "CallButton", PrimitiveType.Cube,
            new Vector3(-0.12f, 1.02f, -1.05f), new Vector3(0.06f, 0.24f, 0.13f),
            HotelArtMaterial.Warning);
    }

    private static void BuildWallPanel(Transform root, Vector3 position, float width, float yaw)
    {
        var panel = LowPolyHotelKit.NewChild(root, "WallPanel", position, yaw);
        LowPolyHotelKit.Part(panel.transform, "Back", PrimitiveType.Cube,
            new Vector3(0f, 0.39f, 0f), new Vector3(width, 0.70f, 0.06f),
            HotelArtMaterial.AccentRose);
        LowPolyHotelKit.Part(panel.transform, "TopRail", PrimitiveType.Cube,
            new Vector3(0f, 0.73f, -0.04f), new Vector3(width + 0.08f, 0.05f, 0.04f),
            HotelArtMaterial.WoodLight);
        LowPolyHotelKit.Part(panel.transform, "BottomRail", PrimitiveType.Cube,
            new Vector3(0f, 0.08f, -0.04f), new Vector3(width + 0.08f, 0.05f, 0.04f),
            HotelArtMaterial.WoodLight);
        for (int i = 0; i < 4; i++)
        {
            float x = -width * 0.38f + i * width * 0.25f;
            LowPolyHotelKit.Part(panel.transform, "Moulding_" + i, PrimitiveType.Cube,
                new Vector3(x, 0.40f, -0.04f), new Vector3(0.035f, 0.52f, 0.035f),
                HotelArtMaterial.WoodLight);
        }
    }

    private static void BuildWallLamp(Transform root, Vector3 position, float yaw)
    {
        var lamp = LowPolyHotelKit.NewChild(root, "WallLamp", position, yaw);
        LowPolyHotelKit.Part(lamp.transform, "Backplate", PrimitiveType.Cylinder,
            Vector3.zero, new Vector3(0.18f, 0.04f, 0.18f),
            HotelArtMaterial.Brass, new Vector3(90f, 0f, 0f));
        LowPolyHotelKit.Part(lamp.transform, "Arm", PrimitiveType.Cylinder,
            new Vector3(0f, 0.08f, -0.16f), new Vector3(0.035f, 0.17f, 0.035f),
            HotelArtMaterial.Brass, new Vector3(90f, 0f, 0f));
        LowPolyHotelKit.Part(lamp.transform, "Shade", PrimitiveType.Sphere,
            new Vector3(0f, 0.10f, -0.34f), new Vector3(0.28f, 0.22f, 0.22f),
            HotelArtMaterial.Cream);
    }

    private static void BuildRoomPlaque(Transform root, Vector3 position, bool facesPositiveZ)
    {
        var plaque = LowPolyHotelKit.NewChild(
            root, "RoomPlaque", position, facesPositiveZ ? 180f : 0f);
        LowPolyHotelKit.Part(plaque.transform, "Base", PrimitiveType.Cube,
            new Vector3(0f, 0.50f, 0f), new Vector3(0.30f, 0.22f, 0.035f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(plaque.transform, "Inset", PrimitiveType.Cube,
            new Vector3(0f, 0.50f, -0.025f), new Vector3(0.22f, 0.14f, 0.018f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(plaque.transform, "Dot", PrimitiveType.Sphere,
            new Vector3(0f, 0.50f, -0.045f), new Vector3(0.055f, 0.055f, 0.025f),
            HotelArtMaterial.WoodDark);
    }

    private static void BuildDoorFrame(
        Transform root, string name, Vector3 position, bool facesPositiveZ)
    {
        var frame = LowPolyHotelKit.NewChild(root, name, position, facesPositiveZ ? 180f : 0f);
        LowPolyHotelKit.Part(frame.transform, "PostL", PrimitiveType.Cube,
            new Vector3(-0.96f, 0.55f, 0f), new Vector3(0.12f, 1.10f, 0.13f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(frame.transform, "PostR", PrimitiveType.Cube,
            new Vector3(0.96f, 0.55f, 0f), new Vector3(0.12f, 1.10f, 0.13f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(frame.transform, "Header", PrimitiveType.Cube,
            new Vector3(0f, 1.15f, 0f), new Vector3(2.04f, 0.12f, 0.13f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(frame.transform, "Threshold", PrimitiveType.Cube,
            new Vector3(0f, 0.035f, 0f), new Vector3(1.82f, 0.07f, 0.24f),
            HotelArtMaterial.Brass);
    }

    private static void BuildHousekeepingCart(Transform root, Vector3 position)
    {
        var cart = LowPolyHotelKit.NewChild(root, "HousekeepingCart", position);
        LowPolyHotelKit.Part(cart.transform, "Body", PrimitiveType.Cube,
            new Vector3(0f, 0.46f, 0f), new Vector3(0.70f, 0.82f, 1.05f),
            HotelArtMaterial.FabricTeal);
        LowPolyHotelKit.Part(cart.transform, "Top", PrimitiveType.Cube,
            new Vector3(0f, 0.91f, 0f), new Vector3(0.76f, 0.08f, 1.12f),
            HotelArtMaterial.MetalLight);
        for (int i = 0; i < 3; i++)
            LowPolyHotelKit.Part(cart.transform, "Towel_" + i, PrimitiveType.Cube,
                new Vector3(0f, 0.38f + i * 0.18f, -0.54f),
                new Vector3(0.56f, 0.13f, 0.08f), HotelArtMaterial.White);
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            LowPolyHotelKit.Part(cart.transform, "Wheel", PrimitiveType.Cylinder,
                new Vector3(x * 0.28f, 0.08f, z * 0.42f),
                new Vector3(0.12f, 0.05f, 0.12f), HotelArtMaterial.Rubber,
                new Vector3(90f, 0f, 0f));
    }

    private static void BuildDiningSet(Transform root, string name, Vector3 position)
    {
        var set = LowPolyHotelKit.NewChild(root, name, position);
        LowPolyHotelKit.Part(set.transform, "TableTop", PrimitiveType.Cylinder,
            new Vector3(0f, 0.72f, 0f), new Vector3(0.95f, 0.08f, 0.95f),
            HotelArtMaterial.WoodWarm);
        LowPolyHotelKit.Part(set.transform, "TableStem", PrimitiveType.Cylinder,
            new Vector3(0f, 0.36f, 0f), new Vector3(0.12f, 0.34f, 0.12f),
            HotelArtMaterial.MetalDark);
        LowPolyHotelKit.Part(set.transform, "TableFoot", PrimitiveType.Cylinder,
            new Vector3(0f, 0.06f, 0f), new Vector3(0.48f, 0.05f, 0.48f),
            HotelArtMaterial.MetalDark);
        for (int i = 0; i < 4; i++)
        {
            Vector3 chairPos = Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 0f, 1.05f);
            LowPolyHotelKit.BuildChairAt(set.transform, "Chair_" + i, chairPos,
                i * 90f, i % 2 == 0 ? HotelArtMaterial.FabricRed : HotelArtMaterial.FabricTeal);
            Vector3 settingPos = Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 0f, 0.50f);
            LowPolyHotelKit.Part(set.transform, "Plate_" + i, PrimitiveType.Cylinder,
                new Vector3(settingPos.x, 0.82f, settingPos.z),
                new Vector3(0.22f, 0.018f, 0.22f), HotelArtMaterial.Cream);
            Vector3 napkinPos = Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0.22f, 0f, 0.48f);
            LowPolyHotelKit.Part(set.transform, "Napkin_" + i, PrimitiveType.Cube,
                new Vector3(napkinPos.x, 0.84f, napkinPos.z),
                new Vector3(0.13f, 0.018f, 0.24f), HotelArtMaterial.AccentRose,
                new Vector3(0f, i * 90f, 0f));
        }
        LowPolyHotelKit.Part(set.transform, "CenterVase", PrimitiveType.Cylinder,
            new Vector3(0f, 0.96f, 0f), new Vector3(0.12f, 0.14f, 0.12f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(set.transform, "CenterFlower", PrimitiveType.Sphere,
            new Vector3(0f, 1.15f, 0f), new Vector3(0.30f, 0.18f, 0.30f),
            HotelArtMaterial.AccentRose);
    }

    private static void BuildRestaurantServiceStation(Transform root, Vector3 position)
    {
        var station = LowPolyHotelKit.NewChild(root, "ServiceStation", position, -90f);
        LowPolyHotelKit.Part(station.transform, "Cabinet", PrimitiveType.Cube,
            new Vector3(0f, 0.42f, 0f), new Vector3(1.45f, 0.84f, 0.58f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(station.transform, "Top", PrimitiveType.Cube,
            new Vector3(0f, 0.88f, 0f), new Vector3(1.55f, 0.08f, 0.66f),
            HotelArtMaterial.Stone);
        for (int i = 0; i < 3; i++)
            LowPolyHotelKit.Part(station.transform, "PlateStack_" + i, PrimitiveType.Cylinder,
                new Vector3(-0.48f + i * 0.48f, 0.97f, 0f),
                new Vector3(0.22f, 0.035f + i * 0.01f, 0.22f),
                HotelArtMaterial.Cream);
        LowPolyHotelKit.Part(station.transform, "Towel", PrimitiveType.Cube,
            new Vector3(0.56f, 0.52f, -0.32f), new Vector3(0.30f, 0.38f, 0.025f),
            HotelArtMaterial.White);
    }

    private static void BuildHostStand(Transform root, Vector3 position)
    {
        var stand = LowPolyHotelKit.NewChild(root, "HostStand", position);
        LowPolyHotelKit.Part(stand.transform, "Pedestal", PrimitiveType.Cube,
            new Vector3(0f, 0.45f, 0f), new Vector3(0.72f, 0.90f, 0.55f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(stand.transform, "Top", PrimitiveType.Cube,
            new Vector3(0f, 0.94f, -0.02f), new Vector3(0.82f, 0.09f, 0.64f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(stand.transform, "Book", PrimitiveType.Cube,
            new Vector3(0f, 1.02f, -0.08f), new Vector3(0.46f, 0.035f, 0.30f),
            HotelArtMaterial.AccentRose, new Vector3(0f, -8f, 0f));
    }

    private static void BuildPendant(Transform root, Vector3 position)
    {
        var pendant = LowPolyHotelKit.NewChild(root, "Pendant", position);
        LowPolyHotelKit.Part(pendant.transform, "Cord", PrimitiveType.Cylinder,
            new Vector3(0f, 0.35f, 0f), new Vector3(0.025f, 0.35f, 0.025f),
            HotelArtMaterial.MetalDark);
        LowPolyHotelKit.Part(pendant.transform, "Shade", PrimitiveType.Cylinder,
            new Vector3(0f, -0.02f, 0f), new Vector3(0.38f, 0.18f, 0.38f),
            HotelArtMaterial.Brass);
    }

    private static void BuildTreadmill(Transform root, Vector3 position)
    {
        var treadmill = LowPolyHotelKit.NewChild(root, "Treadmill", position);
        LowPolyHotelKit.Part(treadmill.transform, "Belt", PrimitiveType.Cube,
            new Vector3(0f, 0.16f, 0f), new Vector3(0.78f, 0.15f, 1.85f),
            HotelArtMaterial.MetalDark, new Vector3(-3f, 0f, 0f));
        LowPolyHotelKit.Part(treadmill.transform, "RunSurface", PrimitiveType.Cube,
            new Vector3(0f, 0.25f, -0.05f), new Vector3(0.64f, 0.04f, 1.50f),
            HotelArtMaterial.Rubber);
        for (int x = -1; x <= 1; x += 2)
            LowPolyHotelKit.Part(treadmill.transform, "Handle", PrimitiveType.Cylinder,
                new Vector3(x * 0.34f, 0.78f, 0.68f), new Vector3(0.04f, 0.56f, 0.04f),
                HotelArtMaterial.MetalLight, new Vector3(18f, 0f, 0f));
        LowPolyHotelKit.Part(treadmill.transform, "Console", PrimitiveType.Cube,
            new Vector3(0f, 1.18f, 0.64f), new Vector3(0.62f, 0.40f, 0.18f),
            HotelArtMaterial.MetalDark, new Vector3(-14f, 0f, 0f));
        LowPolyHotelKit.Part(treadmill.transform, "Display", PrimitiveType.Cube,
            new Vector3(0f, 1.20f, 0.535f), new Vector3(0.42f, 0.22f, 0.02f),
            HotelArtMaterial.GlassBlue, new Vector3(-14f, 0f, 0f));
    }

    private static void BuildWeightBench(Transform root, Vector3 position)
    {
        var bench = LowPolyHotelKit.NewChild(root, "WeightBench", position);
        LowPolyHotelKit.Part(bench.transform, "Pad", PrimitiveType.Cube,
            new Vector3(0f, 0.48f, 0f), new Vector3(0.62f, 0.16f, 1.55f),
            HotelArtMaterial.FabricRed, new Vector3(-8f, 0f, 0f));
        for (int z = -1; z <= 1; z += 2)
            LowPolyHotelKit.Part(bench.transform, "Leg", PrimitiveType.Cube,
                new Vector3(0f, 0.24f, z * 0.55f), new Vector3(0.48f, 0.48f, 0.08f),
                HotelArtMaterial.MetalLight);
        LowPolyHotelKit.Part(bench.transform, "Bar", PrimitiveType.Cylinder,
            new Vector3(0f, 1.15f, 0.62f), new Vector3(0.06f, 0.80f, 0.06f),
            HotelArtMaterial.MetalLight, new Vector3(0f, 0f, 90f));
        for (int x = -1; x <= 1; x += 2)
            LowPolyHotelKit.Part(bench.transform, "Plate", PrimitiveType.Cylinder,
                new Vector3(x * 0.68f, 1.15f, 0.62f), new Vector3(0.28f, 0.08f, 0.28f),
                HotelArtMaterial.Rubber, new Vector3(0f, 0f, 90f));
    }

    private static void BuildWeightRack(Transform root, Vector3 position)
    {
        var rack = LowPolyHotelKit.NewChild(root, "WeightRack", position);
        for (int x = -1; x <= 1; x += 2)
            LowPolyHotelKit.Part(rack.transform, "Post", PrimitiveType.Cube,
                new Vector3(x * 1.2f, 0.70f, 0f), new Vector3(0.10f, 1.40f, 0.50f),
                HotelArtMaterial.MetalDark);
        for (int y = 0; y < 3; y++)
        {
            LowPolyHotelKit.Part(rack.transform, "Shelf", PrimitiveType.Cube,
                new Vector3(0f, 0.28f + y * 0.42f, 0f), new Vector3(2.5f, 0.07f, 0.48f),
                HotelArtMaterial.MetalLight);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.92f + i * 0.46f;
                LowPolyHotelKit.Part(rack.transform, "Dumbbell", PrimitiveType.Cylinder,
                    new Vector3(x, 0.39f + y * 0.42f, 0f),
                    new Vector3(0.16f + y * 0.025f, 0.12f, 0.16f + y * 0.025f),
                    HotelArtMaterial.Rubber, new Vector3(0f, 0f, 90f));
            }
        }
    }

    private static void BuildWaterCooler(Transform root, Vector3 position)
    {
        var cooler = LowPolyHotelKit.NewChild(root, "WaterCooler", position);
        LowPolyHotelKit.Part(cooler.transform, "Body", PrimitiveType.Cube,
            new Vector3(0f, 0.42f, 0f), new Vector3(0.52f, 0.84f, 0.48f),
            HotelArtMaterial.White);
        LowPolyHotelKit.Part(cooler.transform, "Bottle", PrimitiveType.Cylinder,
            new Vector3(0f, 1.02f, 0f), new Vector3(0.27f, 0.38f, 0.27f),
            HotelArtMaterial.GlassBlue);
        LowPolyHotelKit.Part(cooler.transform, "Tap", PrimitiveType.Cube,
            new Vector3(0f, 0.62f, -0.27f), new Vector3(0.18f, 0.12f, 0.08f),
            HotelArtMaterial.FabricTeal);
    }

    private static void BuildTowelRack(Transform root, Vector3 position)
    {
        var rack = LowPolyHotelKit.NewChild(root, "TowelRack", position);
        for (int i = 0; i < 3; i++)
        {
            LowPolyHotelKit.Part(rack.transform, "Shelf_" + i, PrimitiveType.Cube,
                new Vector3(0f, 0.25f + i * 0.34f, 0f),
                new Vector3(1.25f, 0.06f, 0.42f), HotelArtMaterial.MetalDark);
            for (int j = 0; j < 3; j++)
                LowPolyHotelKit.Part(rack.transform, "Towel_" + i + "_" + j, PrimitiveType.Cube,
                    new Vector3(-0.39f + j * 0.39f, 0.33f + i * 0.34f, 0f),
                    new Vector3(0.30f, 0.12f, 0.34f),
                    i == 1 ? HotelArtMaterial.FabricTeal : HotelArtMaterial.White);
        }
    }

    private static void BuildSpinBike(Transform root, Vector3 position, float yaw)
    {
        var bike = LowPolyHotelKit.NewChild(root, "SpinBike", position, yaw);
        LowPolyHotelKit.Part(bike.transform, "Flywheel", PrimitiveType.Cylinder,
            new Vector3(0f, 0.38f, -0.24f), new Vector3(0.34f, 0.10f, 0.34f),
            HotelArtMaterial.MetalLight, new Vector3(90f, 0f, 0f));
        LowPolyHotelKit.Part(bike.transform, "Frame", PrimitiveType.Cylinder,
            new Vector3(0f, 0.58f, 0.05f), new Vector3(0.055f, 0.42f, 0.055f),
            HotelArtMaterial.FabricTeal, new Vector3(-28f, 0f, 0f));
        LowPolyHotelKit.Part(bike.transform, "SeatPost", PrimitiveType.Cylinder,
            new Vector3(0f, 0.86f, 0.35f), new Vector3(0.045f, 0.30f, 0.045f),
            HotelArtMaterial.MetalDark);
        LowPolyHotelKit.Part(bike.transform, "Seat", PrimitiveType.Cube,
            new Vector3(0f, 1.14f, 0.35f), new Vector3(0.36f, 0.10f, 0.28f),
            HotelArtMaterial.LeatherTan);
        LowPolyHotelKit.Part(bike.transform, "HandlePost", PrimitiveType.Cylinder,
            new Vector3(0f, 0.82f, -0.48f), new Vector3(0.045f, 0.38f, 0.045f),
            HotelArtMaterial.MetalDark, new Vector3(12f, 0f, 0f));
        LowPolyHotelKit.Part(bike.transform, "Handlebar", PrimitiveType.Cylinder,
            new Vector3(0f, 1.16f, -0.55f), new Vector3(0.04f, 0.32f, 0.04f),
            HotelArtMaterial.MetalLight, new Vector3(0f, 0f, 90f));
        LowPolyHotelKit.Part(bike.transform, "Base", PrimitiveType.Cube,
            new Vector3(0f, 0.08f, 0f), new Vector3(0.72f, 0.08f, 1.25f),
            HotelArtMaterial.MetalDark);
    }

    private static void BuildPunchingBag(Transform root, Vector3 position)
    {
        var station = LowPolyHotelKit.NewChild(root, "PunchingBagStation", position);
        LowPolyHotelKit.Part(station.transform, "Base", PrimitiveType.Cylinder,
            new Vector3(0f, 0.08f, 0f), new Vector3(0.64f, 0.08f, 0.64f),
            HotelArtMaterial.Rubber);
        LowPolyHotelKit.Part(station.transform, "Post", PrimitiveType.Cylinder,
            new Vector3(0.42f, 0.92f, 0f), new Vector3(0.06f, 0.84f, 0.06f),
            HotelArtMaterial.MetalDark);
        LowPolyHotelKit.Part(station.transform, "Arm", PrimitiveType.Cylinder,
            new Vector3(0f, 1.72f, 0f), new Vector3(0.055f, 0.48f, 0.055f),
            HotelArtMaterial.MetalDark, new Vector3(0f, 0f, 90f));
        LowPolyHotelKit.Part(station.transform, "Bag", PrimitiveType.Cylinder,
            new Vector3(-0.43f, 1.10f, 0f), new Vector3(0.30f, 0.56f, 0.30f),
            HotelArtMaterial.AccentRose);
        LowPolyHotelKit.Part(station.transform, "BagCap", PrimitiveType.Sphere,
            new Vector3(-0.43f, 1.67f, 0f), new Vector3(0.30f, 0.18f, 0.30f),
            HotelArtMaterial.AccentRose);
    }

    private static void BuildExerciseBallRack(Transform root, Vector3 position)
    {
        var rack = LowPolyHotelKit.NewChild(root, "ExerciseBallRack", position);
        LowPolyHotelKit.Part(rack.transform, "Rail", PrimitiveType.Cube,
            new Vector3(0f, 0.22f, 0f), new Vector3(2.6f, 0.10f, 0.50f),
            HotelArtMaterial.MetalDark);
        HotelArtMaterial[] colors =
        {
            HotelArtMaterial.FabricTeal,
            HotelArtMaterial.FabricRed,
            HotelArtMaterial.FabricMustard
        };
        for (int i = 0; i < 3; i++)
            LowPolyHotelKit.Part(rack.transform, "Ball_" + i, PrimitiveType.Sphere,
                new Vector3(-0.85f + i * 0.85f, 0.58f, 0f),
                new Vector3(0.62f, 0.62f, 0.62f), colors[i]);
    }

    private static void BuildCasinoCashier(Transform root, Vector3 position)
    {
        var cashier = LowPolyHotelKit.NewChild(root, "CasinoCashier", position);
        LowPolyHotelKit.Part(cashier.transform, "Counter", PrimitiveType.Cube,
            new Vector3(0f, 0.48f, 0f), new Vector3(1.75f, 0.96f, 0.72f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(cashier.transform, "Top", PrimitiveType.Cube,
            new Vector3(0f, 1.0f, 0f), new Vector3(1.92f, 0.10f, 0.86f),
            HotelArtMaterial.Brass);
        LowPolyHotelKit.Part(cashier.transform, "Front", PrimitiveType.Cube,
            new Vector3(0f, 0.50f, -0.38f), new Vector3(1.34f, 0.48f, 0.035f),
            HotelArtMaterial.Green);
        for (int i = 0; i < 3; i++)
            LowPolyHotelKit.Part(cashier.transform, "ChipTray_" + i, PrimitiveType.Cube,
                new Vector3(-0.48f + i * 0.48f, 1.08f, -0.10f),
                new Vector3(0.30f, 0.035f, 0.18f),
                i == 1 ? HotelArtMaterial.AccentRose : HotelArtMaterial.Warning);
    }

    private static void BuildCasinoTable(Transform root, Vector3 position, float yaw)
    {
        var table = LowPolyHotelKit.NewChild(root, "CasinoTable", position, yaw);
        LowPolyHotelKit.Part(table.transform, "Top", PrimitiveType.Cylinder,
            new Vector3(0f, 0.72f, 0f), new Vector3(1.55f, 0.12f, 1.05f),
            HotelArtMaterial.Green);
        LowPolyHotelKit.Part(table.transform, "Rail", PrimitiveType.Cylinder,
            new Vector3(0f, 0.82f, 0f), new Vector3(1.68f, 0.08f, 1.18f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(table.transform, "Felt", PrimitiveType.Cylinder,
            new Vector3(0f, 0.92f, 0f), new Vector3(1.38f, 0.025f, 0.88f),
            HotelArtMaterial.Green);
        LowPolyHotelKit.Part(table.transform, "Pedestal", PrimitiveType.Cylinder,
            new Vector3(0f, 0.35f, 0f), new Vector3(0.30f, 0.34f, 0.30f),
            HotelArtMaterial.WoodDark);
        for (int i = 0; i < 5; i++)
        {
            float angle = -90f + i * 45f;
            Vector3 p = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 1.45f);
            LowPolyHotelKit.BuildChairAt(table.transform, "CasinoChair_" + i, p,
                angle, HotelArtMaterial.FabricRed);
        }
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 p = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 0.52f);
            LowPolyHotelKit.Part(table.transform, "Chip_" + i, PrimitiveType.Cylinder,
                new Vector3(p.x, 0.98f, p.z), new Vector3(0.09f, 0.015f, 0.09f),
                i % 2 == 0 ? HotelArtMaterial.FabricRed : HotelArtMaterial.Brass);
        }
    }

    private static void BuildSlotMachine(Transform root, Vector3 position)
    {
        var slot = LowPolyHotelKit.NewChild(root, "SlotMachine", position);
        LowPolyHotelKit.Part(slot.transform, "Cabinet", PrimitiveType.Cube,
            new Vector3(0f, 0.78f, 0f), new Vector3(0.78f, 1.56f, 0.72f),
            HotelArtMaterial.WoodDark);
        LowPolyHotelKit.Part(slot.transform, "Screen", PrimitiveType.Cube,
            new Vector3(0f, 0.96f, -0.38f), new Vector3(0.58f, 0.48f, 0.03f),
            HotelArtMaterial.GlassBlue, new Vector3(8f, 0f, 0f));
        for (int i = 0; i < 3; i++)
            LowPolyHotelKit.Part(slot.transform, "Reel_" + i, PrimitiveType.Cube,
                new Vector3(-0.20f + i * 0.20f, 0.98f, -0.405f),
                new Vector3(0.16f, 0.30f, 0.018f),
                i % 2 == 0 ? HotelArtMaterial.Warning : HotelArtMaterial.White,
                new Vector3(8f, 0f, 0f));
        LowPolyHotelKit.Part(slot.transform, "TopLight", PrimitiveType.Sphere,
            new Vector3(0f, 1.72f, 0f), new Vector3(0.42f, 0.20f, 0.42f),
            HotelArtMaterial.Warning);
        LowPolyHotelKit.Part(slot.transform, "Lever", PrimitiveType.Cylinder,
            new Vector3(0.50f, 1.05f, 0f), new Vector3(0.04f, 0.34f, 0.04f),
            HotelArtMaterial.Brass, new Vector3(0f, 0f, -18f));
    }

    private static void BuildVelvetRope(Transform root, Vector3 position, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = position.x + i * 0.85f;
            LowPolyHotelKit.Part(root, "RopePost_" + i, PrimitiveType.Cylinder,
                new Vector3(x, 0.46f, position.z), new Vector3(0.07f, 0.46f, 0.07f),
                HotelArtMaterial.Brass);
            LowPolyHotelKit.Part(root, "RopeCap_" + i, PrimitiveType.Sphere,
                new Vector3(x, 0.94f, position.z), new Vector3(0.14f, 0.14f, 0.14f),
                HotelArtMaterial.Brass);
            if (i == 0) continue;
            LowPolyHotelKit.Part(root, "Velvet_" + i, PrimitiveType.Cylinder,
                new Vector3(x - 0.425f, 0.83f, position.z),
                new Vector3(0.055f, 0.43f, 0.055f), HotelArtMaterial.FabricRed,
                new Vector3(0f, 0f, 90f));
        }
    }

    private static void BuildLounger(Transform root, Vector3 position, float yaw)
    {
        var chair = LowPolyHotelKit.NewChild(root, "PoolLounger", position, yaw);
        LowPolyHotelKit.Part(chair.transform, "Seat", PrimitiveType.Cube,
            new Vector3(0f, 0.28f, -0.22f), new Vector3(0.62f, 0.12f, 1.25f),
            HotelArtMaterial.FabricTeal, new Vector3(-4f, 0f, 0f));
        LowPolyHotelKit.Part(chair.transform, "Back", PrimitiveType.Cube,
            new Vector3(0f, 0.66f, 0.57f), new Vector3(0.62f, 0.12f, 0.90f),
            HotelArtMaterial.FabricTeal, new Vector3(-46f, 0f, 0f));
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            LowPolyHotelKit.Part(chair.transform, "Leg", PrimitiveType.Cube,
                new Vector3(x * 0.24f, 0.13f, z * 0.46f),
                new Vector3(0.06f, 0.26f, 0.06f), HotelArtMaterial.WoodLight);
    }

    private static void BuildUmbrella(Transform root, Vector3 position)
    {
        var umbrella = LowPolyHotelKit.NewChild(root, "Umbrella", position);
        LowPolyHotelKit.Part(umbrella.transform, "Pole", PrimitiveType.Cylinder,
            new Vector3(0f, 1.12f, 0f), new Vector3(0.06f, 1.12f, 0.06f),
            HotelArtMaterial.WoodLight);
        LowPolyHotelKit.Part(umbrella.transform, "Canopy", PrimitiveType.Cylinder,
            new Vector3(0f, 2.22f, 0f), new Vector3(1.48f, 0.16f, 1.48f),
            HotelArtMaterial.FabricRed);
        LowPolyHotelKit.Part(umbrella.transform, "Cap", PrimitiveType.Sphere,
            new Vector3(0f, 2.42f, 0f), new Vector3(0.13f, 0.13f, 0.13f),
            HotelArtMaterial.Brass);
    }

    private static void BuildPoolLadder(Transform root, Vector3 position)
    {
        var ladder = LowPolyHotelKit.NewChild(root, "PoolLadder", position);
        for (int x = -1; x <= 1; x += 2)
            LowPolyHotelKit.Part(ladder.transform, "Rail", PrimitiveType.Cylinder,
                new Vector3(x * 0.28f, 0.46f, 0f), new Vector3(0.045f, 0.46f, 0.045f),
                HotelArtMaterial.MetalLight);
        for (int i = 0; i < 3; i++)
            LowPolyHotelKit.Part(ladder.transform, "Rung_" + i, PrimitiveType.Cylinder,
                new Vector3(0f, 0.16f + i * 0.25f, 0f),
                new Vector3(0.035f, 0.28f, 0.035f), HotelArtMaterial.MetalLight,
                new Vector3(0f, 0f, 90f));
    }

    private static void BuildPoolSideTable(Transform root, Vector3 position)
    {
        var table = LowPolyHotelKit.NewChild(root, "PoolSideTable", position);
        LowPolyHotelKit.Part(table.transform, "Top", PrimitiveType.Cylinder,
            new Vector3(0f, 0.45f, 0f), new Vector3(0.38f, 0.06f, 0.38f),
            HotelArtMaterial.WoodLight);
        LowPolyHotelKit.Part(table.transform, "Stem", PrimitiveType.Cylinder,
            new Vector3(0f, 0.23f, 0f), new Vector3(0.06f, 0.22f, 0.06f),
            HotelArtMaterial.MetalDark);
        LowPolyHotelKit.Part(table.transform, "Drink", PrimitiveType.Cylinder,
            new Vector3(0f, 0.58f, 0f), new Vector3(0.09f, 0.09f, 0.09f),
            HotelArtMaterial.GlassBlue);
    }

    private static void BuildLifeRing(Transform root, Vector3 position)
    {
        var ring = LowPolyHotelKit.NewChild(root, "LifeRing", position);
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f;
            float radians = angle * Mathf.Deg2Rad;
            LowPolyHotelKit.Part(ring.transform, "Segment_" + i, PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(radians) * 0.31f, 0.55f + Mathf.Sin(radians) * 0.31f, 0f),
                new Vector3(0.075f, 0.14f, 0.075f),
                i % 3 == 0 ? HotelArtMaterial.White : HotelArtMaterial.AccentRose,
                new Vector3(0f, 0f, angle));
        }
    }

    private static void RemoveArtRoots()
    {
        Transform world = FindPath("World");
        if (world == null) return;
        var remove = new List<GameObject>();
        foreach (Transform child in world.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == ArtRootName)
                remove.Add(child.gameObject);
        }
        for (int i = 0; i < remove.Count; i++)
            Object.DestroyImmediate(remove[i]);
    }

    private static void SetGreyboxReplacementRenderers(bool enabled)
    {
        for (int i = 0; i < ReplacedGreyboxPaths.Length; i++)
        {
            Transform target = FindPath(ReplacedGreyboxPaths[i]);
            if (target == null) continue;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null) renderer.enabled = enabled;
            }
        }
    }

    private static void MarkArtBatchingStatic()
    {
        Transform world = FindPath("World");
        if (world == null) return;
        foreach (Transform child in world.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child.name != ArtRootName) continue;
            foreach (Transform artObject in child.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(
                    artObject.gameObject,
                    StaticEditorFlags.BatchingStatic);
            }
        }
    }

    private static bool ValidateLowPolyHotelInternal(bool logResult)
    {
        Transform world = FindPath("World");
        if (world == null)
        {
            if (logResult) Debug.LogError("Hotel art validation failed: World root is missing.");
            return false;
        }

        int errors = 0;
        int artRoots = 0;
        int artObjects = 0;
        int doorFrames = 0;
        int checkedChairs = 0;

        foreach (Transform item in world.GetComponentsInChildren<Transform>(true))
        {
            if (item == null) continue;
            if (item.name == ArtRootName) artRoots++;
            if (!IsUnderArtRoot(item)) continue;
            artObjects++;

            if (item.gameObject.layer != LowPolyHotelKit.ArtLayer)
                ValidationError(ref errors, "Wrong art layer: " + GetPath(item));

            Collider collider = item.GetComponent<Collider>();
            if (collider != null)
                ValidationError(ref errors, "Generated art owns a collider: " + GetPath(item));

            if (item.name == "WallPanel"
                || item.name == "WallLamp"
                || item.name == "ReceptionKeyWall")
            {
                Transform artRoot = FindArtRoot(item);
                if (artRoot != null && TryGetBounds(item, out Bounds decorBounds))
                {
                    float relativeTop = decorBounds.max.y - artRoot.position.y;
                    bool guestFloor = artRoot.parent != null
                                      && (artRoot.parent.name == "Floor2"
                                          || artRoot.parent.name == "Floor3");
                    float wallLimit = guestFloor ? GuestWallHeight : 0.82f;
                    if (relativeTop > wallLimit)
                        ValidationError(ref errors,
                            GetPath(item) + " exceeds cutaway wall height: " + relativeTop.ToString("F2"));
                }
            }

            if (item.name.StartsWith("Frame_"))
            {
                doorFrames++;
                if (TryGetBounds(item, out Bounds frameBounds))
                {
                    Transform artRoot = FindArtRoot(item);
                    float relativeTop = frameBounds.max.y - (artRoot != null ? artRoot.position.y : 0f);
                    if (Mathf.Abs(frameBounds.size.x - 2.04f) > 0.08f
                        || Mathf.Abs(relativeTop - 1.21f) > 0.08f)
                    {
                        ValidationError(ref errors,
                            GetPath(item) + " does not match the 1.8 x 1.1 room door.");
                    }
                }
            }

            bool diningChair = item.name.StartsWith("Chair_")
                               && item.parent != null
                               && item.parent.name.StartsWith("DiningSet_");
            bool casinoChair = item.name.StartsWith("CasinoChair_");
            if (diningChair || casinoChair)
            {
                checkedChairs++;
                Vector3 toCenter = item.parent.position - item.position;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.001f)
                {
                    float alignment = Vector3.Dot(-item.forward, toCenter.normalized);
                    if (alignment < 0.96f)
                        ValidationError(ref errors,
                            GetPath(item) + " faces away from its table (dot "
                            + alignment.ToString("F2") + ").");
                }
            }
        }

        Transform lobbyArt = FindPath("World/Floor1/" + ArtRootName);
        if (lobbyArt != null)
        {
            Bounds receptionClearance = new Bounds(
                new Vector3(0f, lobbyArt.position.y + 0.82f, 3.92f),
                new Vector3(0.82f, 1.42f, 0.72f));
            Renderer[] renderers = lobbyArt.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].bounds.Intersects(receptionClearance)) continue;
                ValidationError(ref errors,
                    "Reception staff clearance is blocked by " + GetPath(renderers[i].transform));
            }
        }

        if (artRoots != 7)
            ValidationError(ref errors, "Expected 7 art roots, found " + artRoots + ".");
        if (doorFrames != 12)
            ValidationError(ref errors, "Expected 12 room door frames, found " + doorFrames + ".");
        if (checkedChairs != 31)
            ValidationError(ref errors, "Expected 31 inward-facing table chairs, found " + checkedChairs + ".");
        ValidateGuestWallHeights(ref errors, FindPath("World/Floor2"));
        ValidateGuestWallHeights(ref errors, FindPath("World/Floor3"));

        if (logResult)
        {
            if (errors == 0)
                Debug.Log("Hotel art validation passed: " + artObjects
                          + " objects, 7 floors, 12 door frames, 31 table chairs, no collider or clearance issues.");
            else
                Debug.LogError("Hotel art validation failed with " + errors + " issue(s).");
        }
        return errors == 0;
    }

    private static void ValidateGuestWallHeights(ref int errors, Transform floor)
    {
        if (floor == null)
        {
            ValidationError(ref errors, "Guest floor is missing.");
            return;
        }

        foreach (Transform item in floor.GetComponentsInChildren<Transform>(true))
        {
            if (item == null || !item.name.StartsWith("Wall_")) continue;
            bool outerCutaway = item.parent == floor
                                && (item.name == "Wall_S" || item.name == "Wall_W");
            float expected = outerCutaway ? CutawayWallHeight : GuestWallHeight;
            if (Mathf.Abs(item.localScale.y - expected) > 0.01f
                || Mathf.Abs(item.localPosition.y - expected * 0.5f) > 0.01f)
            {
                ValidationError(ref errors,
                    GetPath(item) + " has incorrect guest-floor wall height.");
            }
        }
    }

    private static bool IsUnderArtRoot(Transform item)
    {
        return FindArtRoot(item) != null;
    }

    private static Transform FindArtRoot(Transform item)
    {
        Transform current = item;
        while (current != null)
        {
            if (current.name == ArtRootName) return current;
            current = current.parent;
        }
        return null;
    }

    private static bool TryGetBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (!found)
            {
                bounds = renderers[i].bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }
        return found;
    }

    private static string GetPath(Transform item)
    {
        if (item == null) return "<missing>";
        string path = item.name;
        Transform current = item.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private static void ValidationError(ref int errors, string message)
    {
        errors++;
        Debug.LogError("[Hotel Art] " + message);
    }

    private static Transform FindPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string[] parts = path.Split('/');
        GameObject root = null;
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != parts[0]) continue;
            root = roots[i];
            break;
        }
        if (root == null) return null;
        Transform current = root.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null) return null;
        }
        return current;
    }

    private static void BuildPlant(Transform root, Vector3 position, float height)
    {
        LowPolyHotelKit.BuildPlant(root, position, height);
    }
}
