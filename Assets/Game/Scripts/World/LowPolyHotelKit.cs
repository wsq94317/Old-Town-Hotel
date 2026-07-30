using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum HotelArtMaterial
{
    WoodDark,
    WoodWarm,
    WoodLight,
    Brass,
    FabricTeal,
    FabricRed,
    FabricMustard,
    Cream,
    Porcelain,
    MetalDark,
    MetalLight,
    GlassBlue,
    TileBlue,
    TileCream,
    CarpetRed,
    CarpetBlue,
    Rubber,
    Green,
    Water,
    Black,
    White,
    Warning,
    WallSage,
    Stone,
    LeatherTan,
    AccentRose
}

public static class HotelArtPalette
{
    public static Color ColorOf(HotelArtMaterial material)
    {
        switch (material)
        {
            case HotelArtMaterial.WoodDark: return new Color(0.20f, 0.11f, 0.07f);
            case HotelArtMaterial.WoodWarm: return new Color(0.46f, 0.25f, 0.12f);
            case HotelArtMaterial.WoodLight: return new Color(0.68f, 0.46f, 0.25f);
            case HotelArtMaterial.Brass: return new Color(0.80f, 0.55f, 0.16f);
            case HotelArtMaterial.FabricTeal: return new Color(0.12f, 0.38f, 0.38f);
            case HotelArtMaterial.FabricRed: return new Color(0.48f, 0.13f, 0.12f);
            case HotelArtMaterial.FabricMustard: return new Color(0.63f, 0.43f, 0.10f);
            case HotelArtMaterial.Cream: return new Color(0.82f, 0.75f, 0.62f);
            case HotelArtMaterial.Porcelain: return new Color(0.84f, 0.88f, 0.86f);
            case HotelArtMaterial.MetalDark: return new Color(0.13f, 0.15f, 0.16f);
            case HotelArtMaterial.MetalLight: return new Color(0.50f, 0.55f, 0.55f);
            case HotelArtMaterial.GlassBlue: return new Color(0.30f, 0.58f, 0.64f);
            case HotelArtMaterial.TileBlue: return new Color(0.22f, 0.42f, 0.47f);
            case HotelArtMaterial.TileCream: return new Color(0.66f, 0.59f, 0.48f);
            case HotelArtMaterial.CarpetRed: return new Color(0.38f, 0.10f, 0.10f);
            case HotelArtMaterial.CarpetBlue: return new Color(0.12f, 0.20f, 0.30f);
            case HotelArtMaterial.Rubber: return new Color(0.08f, 0.09f, 0.09f);
            case HotelArtMaterial.Green: return new Color(0.17f, 0.38f, 0.19f);
            case HotelArtMaterial.Water: return new Color(0.10f, 0.55f, 0.66f);
            case HotelArtMaterial.Black: return new Color(0.025f, 0.025f, 0.03f);
            case HotelArtMaterial.White: return new Color(0.92f, 0.90f, 0.84f);
            case HotelArtMaterial.Warning: return new Color(0.95f, 0.65f, 0.08f);
            case HotelArtMaterial.WallSage: return new Color(0.31f, 0.43f, 0.39f);
            case HotelArtMaterial.Stone: return new Color(0.48f, 0.44f, 0.38f);
            case HotelArtMaterial.LeatherTan: return new Color(0.58f, 0.31f, 0.14f);
            case HotelArtMaterial.AccentRose: return new Color(0.60f, 0.25f, 0.25f);
            default: return Color.magenta;
        }
    }

    public static float SmoothnessOf(HotelArtMaterial material)
    {
        switch (material)
        {
            case HotelArtMaterial.Brass:
            case HotelArtMaterial.MetalLight:
            case HotelArtMaterial.Porcelain:
                return 0.65f;
            case HotelArtMaterial.GlassBlue:
            case HotelArtMaterial.Water:
                return 0.82f;
            case HotelArtMaterial.MetalDark:
            case HotelArtMaterial.Black:
                return 0.38f;
            case HotelArtMaterial.LeatherTan:
            case HotelArtMaterial.Stone:
                return 0.30f;
            default:
                return 0.12f;
        }
    }
}

/// <summary>
/// Procedural low-poly kit shared by scene dressing and runtime furniture views.
/// Art objects never carry colliders and live on Ignore Raycast so gameplay remains authoritative.
/// </summary>
public static class LowPolyHotelKit
{
    public const string ResourceRoot = "HotelArt/";
    public const string PrefabResourceRoot = ResourceRoot + "Prefabs/";
    public const int ArtLayer = 2;

    private static readonly Dictionary<HotelArtMaterial, Material> RuntimeMaterials =
        new Dictionary<HotelArtMaterial, Material>();

    public static Material MaterialFor(HotelArtMaterial material)
    {
        if (RuntimeMaterials.TryGetValue(material, out Material cached) && cached != null)
            return cached;

        Material asset = Resources.Load<Material>(ResourceRoot + "Mat_" + material);
        if (asset != null)
        {
            RuntimeMaterials[material] = asset;
            return asset;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var fallback = shader != null ? new Material(shader) : null;
        if (fallback != null)
        {
            fallback.name = "Runtime_Mat_" + material;
            fallback.color = HotelArtPalette.ColorOf(material);
            if (fallback.HasProperty("_Smoothness"))
                fallback.SetFloat("_Smoothness", HotelArtPalette.SmoothnessOf(material));
        }
        RuntimeMaterials[material] = fallback;
        return fallback;
    }

    public static GameObject CreateFurniture(FurnitureKind kind)
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabResourceRoot + "LP_Furniture_" + kind.kindId);
        if (prefab != null)
        {
            GameObject instance = Object.Instantiate(prefab);
            instance.name = kind.name;
            SetLayerRecursively(instance, ArtLayer);
            RemoveColliders(instance);
            return instance;
        }
        return CreateFurnitureProcedural(kind);
    }

    public static GameObject CreateFurnitureProcedural(FurnitureKind kind)
    {
        var root = NewRoot(kind.name);
        switch (kind.kindId)
        {
            case FurnitureCatalog.SaggingBed:
                BuildBed(root.transform, HotelArtMaterial.FabricMustard, true, false);
                break;
            case FurnitureCatalog.ProperBed:
                BuildBed(root.transform, HotelArtMaterial.FabricTeal, false, false);
                break;
            case FurnitureCatalog.MemoryFoamBed:
                BuildBed(root.transform, HotelArtMaterial.FabricRed, false, true);
                break;
            case FurnitureCatalog.BasicBathroom:
                BuildBathroom(root.transform, false);
                break;
            case FurnitureCatalog.RenovatedBathroom:
                BuildBathroom(root.transform, true);
                break;
            case FurnitureCatalog.BoxyTv:
                BuildTv(root.transform, false);
                break;
            case FurnitureCatalog.BigFlatTv:
                BuildTv(root.transform, true);
                break;
            case FurnitureCatalog.Sofa:
                BuildSofa(root.transform, HotelArtMaterial.FabricTeal, 1.4f);
                break;
            case FurnitureCatalog.WorkDesk:
                BuildDesk(root.transform);
                break;
            case FurnitureCatalog.Rug:
                BuildRug(root.transform, HotelArtMaterial.CarpetRed, 1.45f, 1.45f);
                break;
            case FurnitureCatalog.WallArt:
                BuildWallArt(root.transform);
                break;
            default:
                Part(root.transform, "Unknown", PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f),
                    new Vector3(0.8f, 0.7f, 0.8f), HotelArtMaterial.Warning);
                break;
        }

        AddConditionDetails(root.transform);
        return root;
    }

    public static void ApplyFurnitureCondition(GameObject root, FurnitureInstance item)
    {
        if (root == null || item == null) return;

        float age = Mathf.Clamp01(item.newness);
        float health = Mathf.Clamp01(item.health);
        float brightness = Mathf.Lerp(0.48f, 1f, age);
        float brown = Mathf.Lerp(0.32f, 0f, age);

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.gameObject.name.StartsWith("Status_")) continue;
            Material shared = renderer.sharedMaterial;
            if (shared == null) continue;
            Color baseColor = shared.color;
            Color worn = Color.Lerp(baseColor * brightness, new Color(0.24f, 0.16f, 0.10f), brown);
            if (item.IsFaulted) worn = Color.Lerp(worn, new Color(0.46f, 0.12f, 0.08f), 0.28f);
            if (health < 0.35f) worn *= Mathf.Lerp(0.62f, 1f, health / 0.35f);

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (shared.HasProperty("_BaseColor")) block.SetColor("_BaseColor", worn);
            if (shared.HasProperty("_Color")) block.SetColor("_Color", worn);
            renderer.SetPropertyBlock(block);
        }

        Transform tape = root.transform.Find("Status_Tape");
        if (tape != null) tape.gameObject.SetActive(item.taped);
        Transform fault = root.transform.Find("Status_Fault");
        if (fault != null) fault.gameObject.SetActive(item.IsFaulted && !item.taped);
    }

    public static void BuildBreakRoom(Transform root)
    {
        if (root == null) return;
        Part(root, "Floor", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.15f),
            new Vector3(4.0f, 0.06f, 2.8f), HotelArtMaterial.CarpetBlue);
        Part(root, "BackWall", PrimitiveType.Cube, new Vector3(0f, 0.40f, 1.48f),
            new Vector3(4.0f, 0.80f, 0.10f), HotelArtMaterial.WallSage);
        Part(root, "SideWall", PrimitiveType.Cube, new Vector3(-1.95f, 0.40f, 0.15f),
            new Vector3(0.10f, 0.80f, 2.75f), HotelArtMaterial.WallSage);
        Part(root, "WallRail", PrimitiveType.Cube, new Vector3(0f, 0.72f, 1.41f),
            new Vector3(3.78f, 0.05f, 0.04f), HotelArtMaterial.WoodLight);

        BuildSofaAt(root, "BreakSofa", new Vector3(-1.15f, 0f, 0.78f), -90f,
            HotelArtMaterial.FabricTeal, 1.45f);
        BuildTableAt(root, "BreakTable", new Vector3(0.15f, 0f, 0.05f), 0.72f, 0.50f);
        BuildChairAt(root, "ChairA", new Vector3(0.15f, 0f, -0.62f), 180f, HotelArtMaterial.FabricMustard);
        BuildChairAt(root, "ChairB", new Vector3(0.85f, 0f, 0.05f), 90f, HotelArtMaterial.FabricMustard);

        var lockers = NewChild(root, "Lockers", new Vector3(1.48f, 0f, 1.10f));
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 0.38f;
            Part(lockers.transform, "Locker_" + i, PrimitiveType.Cube, new Vector3(x, 0.62f, 0f),
                new Vector3(0.34f, 1.24f, 0.42f), HotelArtMaterial.MetalLight);
            Part(lockers.transform, "Handle_" + i, PrimitiveType.Cube, new Vector3(x + 0.10f, 0.64f, -0.22f),
                new Vector3(0.04f, 0.14f, 0.03f), HotelArtMaterial.MetalDark);
        }

        BuildKitchenette(root, new Vector3(1.18f, 0f, -0.90f), 0f, 1.45f);
        BuildPlant(root, new Vector3(-1.55f, 0f, -0.95f), 0.75f);
        Part(root, "NoticeBoard", PrimitiveType.Cube, new Vector3(-0.45f, 0.52f, 1.40f),
            new Vector3(0.90f, 0.40f, 0.04f), HotelArtMaterial.WoodWarm);
        for (int i = 0; i < 3; i++)
            Part(root, "Notice_" + i, PrimitiveType.Cube,
                new Vector3(-0.72f + i * 0.27f, 0.52f + (i % 2) * 0.06f, 1.37f),
                new Vector3(0.19f, 0.22f, 0.015f),
                i == 1 ? HotelArtMaterial.AccentRose : HotelArtMaterial.Cream,
                new Vector3(0f, 0f, -5f + i * 4f));
    }

    public static void BuildFacility(
        Transform root,
        StaffFacilityKind kind,
        Vector3 size,
        float backSign,
        float sideSign)
    {
        if (root == null) return;
        var visual = NewChild(root, "Visual", Vector3.zero);
        visual.transform.localScale = new Vector3(sideSign, 1f, backSign);
        switch (kind)
        {
            case StaffFacilityKind.PublicToilet:
                BuildPublicToilet(visual.transform, size);
                break;
            case StaffFacilityKind.Kitchen:
                BuildKitchen(visual.transform, size);
                break;
            case StaffFacilityKind.FloorSupport:
                BuildFloorSupport(visual.transform, size);
                break;
            default:
                Part(visual.transform, "Floor", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f),
                    new Vector3(size.x, 0.06f, size.z), HotelArtMaterial.TileCream);
                break;
        }
    }

    public static void BuildLobbyDecor(Transform root)
    {
        if (root == null) return;
        BuildReceptionDesk(root, new Vector3(0f, 0f, 3.0f), 0f);
        BuildSofaAt(root, "LobbySofaA", new Vector3(-4.65f, 0f, 1.60f), 0f,
            HotelArtMaterial.FabricRed, 1.75f);
        BuildSofaAt(root, "LobbySofaB", new Vector3(-6.05f, 0f, 0.35f), -90f,
            HotelArtMaterial.FabricTeal, 1.45f);
        BuildTableAt(root, "LobbyCoffeeTable", new Vector3(-4.75f, 0f, 0.35f), 0.95f, 0.56f);
        Part(root, "CoffeeTableBook", PrimitiveType.Cube, new Vector3(-4.92f, 0.51f, 0.34f),
            new Vector3(0.32f, 0.035f, 0.24f), HotelArtMaterial.AccentRose,
            new Vector3(0f, 12f, 0f));
        Part(root, "CoffeeCup", PrimitiveType.Cylinder, new Vector3(-4.52f, 0.55f, 0.28f),
            new Vector3(0.11f, 0.08f, 0.11f), HotelArtMaterial.Cream);
        BuildPlant(root, new Vector3(-2.7f, 0f, 3.8f), 1.05f);
        BuildPlant(root, new Vector3(3.15f, 0f, 3.7f), 0.85f);
        BuildLuggageCart(root, new Vector3(4.2f, 0f, 2.3f), 0f);

        BuildRugAt(root, "LobbyRug", new Vector3(-4.85f, 0f, 1.05f),
            HotelArtMaterial.CarpetRed, 3.6f, 2.75f);
        BuildRugAt(root, "EntryRunner", new Vector3(0f, 0f, -2.55f),
            HotelArtMaterial.CarpetBlue, 2.1f, 5.1f);

        for (int i = 0; i < 4; i++)
        {
            float x = -1.35f + i * 0.9f;
            BuildStanchion(root, new Vector3(x, 0f, 1.15f));
        }
    }

    public static void BuildReceptionDesk(Transform root, Vector3 position, float yaw)
    {
        var desk = NewChild(root, "ReceptionDesk_LP", position, yaw);
        Part(desk.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.39f, 0f),
            new Vector3(2.75f, 0.78f, 0.72f), HotelArtMaterial.WoodDark);
        Part(desk.transform, "FrontInset", PrimitiveType.Cube, new Vector3(0f, 0.42f, -0.375f),
            new Vector3(2.28f, 0.46f, 0.035f), HotelArtMaterial.FabricTeal);
        Part(desk.transform, "FrontTrimTop", PrimitiveType.Cube, new Vector3(0f, 0.69f, -0.398f),
            new Vector3(2.40f, 0.045f, 0.025f), HotelArtMaterial.Brass);
        Part(desk.transform, "FrontTrimBottom", PrimitiveType.Cube, new Vector3(0f, 0.16f, -0.398f),
            new Vector3(2.40f, 0.045f, 0.025f), HotelArtMaterial.Brass);
        Part(desk.transform, "CounterTop", PrimitiveType.Cube, new Vector3(0f, 0.83f, -0.04f),
            new Vector3(2.98f, 0.11f, 0.88f), HotelArtMaterial.Stone);
        Part(desk.transform, "Bell", PrimitiveType.Sphere, new Vector3(-0.82f, 0.94f, -0.15f),
            new Vector3(0.18f, 0.10f, 0.18f), HotelArtMaterial.Brass);
        Part(desk.transform, "Ledger", PrimitiveType.Cube, new Vector3(0.42f, 0.93f, -0.10f),
            new Vector3(0.55f, 0.04f, 0.36f), HotelArtMaterial.AccentRose, new Vector3(0f, -8f, 0f));
        Part(desk.transform, "MonitorStand", PrimitiveType.Cylinder, new Vector3(0.82f, 0.94f, 0.18f),
            new Vector3(0.06f, 0.10f, 0.06f), HotelArtMaterial.MetalDark);
        Part(desk.transform, "Monitor", PrimitiveType.Cube, new Vector3(0.82f, 1.10f, 0.20f),
            new Vector3(0.42f, 0.27f, 0.06f), HotelArtMaterial.MetalDark,
            new Vector3(-8f, -8f, 0f));
        Part(desk.transform, "MonitorScreen", PrimitiveType.Cube, new Vector3(0.78f, 1.10f, 0.166f),
            new Vector3(0.31f, 0.18f, 0.012f), HotelArtMaterial.GlassBlue,
            new Vector3(-8f, -8f, 0f));
    }

    public static void BuildReceptionKeyWall(Transform root, Vector3 position)
    {
        var keys = NewChild(root, "ReceptionKeyWall", position);
        Part(keys.transform, "Back", PrimitiveType.Cube, new Vector3(0f, 0.39f, 0f),
            new Vector3(2.35f, 0.70f, 0.08f), HotelArtMaterial.WoodWarm);
        Part(keys.transform, "TopRail", PrimitiveType.Cube, new Vector3(0f, 0.72f, -0.055f),
            new Vector3(2.45f, 0.05f, 0.04f), HotelArtMaterial.Brass);
        for (int row = 0; row < 2; row++)
        for (int col = 0; col < 7; col++)
        {
            Part(keys.transform, "Slot_" + row + "_" + col, PrimitiveType.Cube,
                new Vector3(-0.93f + col * 0.31f, 0.25f + row * 0.28f, -0.055f),
                new Vector3(0.22f, 0.17f, 0.025f), HotelArtMaterial.WoodDark);
            Part(keys.transform, "Key_" + row + "_" + col, PrimitiveType.Cube,
                new Vector3(-0.93f + col * 0.31f, 0.25f + row * 0.28f, -0.075f),
                new Vector3(0.035f, 0.08f, 0.018f), HotelArtMaterial.Brass);
        }
    }

    public static void BuildSofaAt(Transform root, string name, Vector3 position, float yaw,
        HotelArtMaterial fabric, float width)
    {
        var sofa = NewChild(root, name, position, yaw);
        BuildSofa(sofa.transform, fabric, width);
    }

    public static void BuildTableAt(Transform root, string name, Vector3 position, float width, float depth)
    {
        var table = NewChild(root, name, position);
        Part(table.transform, "Top", PrimitiveType.Cube, new Vector3(0f, 0.43f, 0f),
            new Vector3(width, 0.10f, depth), HotelArtMaterial.WoodWarm);
        float x = width * 0.38f;
        float z = depth * 0.35f;
        for (int ix = -1; ix <= 1; ix += 2)
        for (int iz = -1; iz <= 1; iz += 2)
            Part(table.transform, "Leg", PrimitiveType.Cube, new Vector3(ix * x, 0.21f, iz * z),
                new Vector3(0.09f, 0.42f, 0.09f), HotelArtMaterial.WoodDark);
    }

    public static void BuildChairAt(Transform root, string name, Vector3 position, float yaw,
        HotelArtMaterial fabric)
    {
        var chair = NewChild(root, name, position, yaw);
        Part(chair.transform, "Seat", PrimitiveType.Cube, new Vector3(0f, 0.34f, 0f),
            new Vector3(0.48f, 0.12f, 0.48f), fabric);
        Part(chair.transform, "Back", PrimitiveType.Cube, new Vector3(0f, 0.67f, 0.21f),
            new Vector3(0.48f, 0.58f, 0.10f), fabric, new Vector3(-7f, 0f, 0f));
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(chair.transform, "Leg", PrimitiveType.Cube, new Vector3(x * 0.18f, 0.15f, z * 0.18f),
                new Vector3(0.06f, 0.30f, 0.06f), HotelArtMaterial.WoodDark);
    }

    public static void BuildPlant(Transform root, Vector3 position, float height)
    {
        var plant = NewChild(root, "Plant", position);
        Part(plant.transform, "Pot", PrimitiveType.Cylinder, new Vector3(0f, height * 0.17f, 0f),
            new Vector3(height * 0.38f, height * 0.17f, height * 0.38f), HotelArtMaterial.WoodWarm);
        Part(plant.transform, "Stem", PrimitiveType.Cylinder, new Vector3(0f, height * 0.50f, 0f),
            new Vector3(height * 0.08f, height * 0.28f, height * 0.08f), HotelArtMaterial.Green);
        for (int i = 0; i < 5; i++)
        {
            float angle = i * 72f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.22f * height, 0f, 0f);
            Part(plant.transform, "Leaf_" + i, PrimitiveType.Sphere,
                new Vector3(offset.x, height * (0.64f + (i % 2) * 0.12f), offset.z),
                new Vector3(height * 0.34f, height * 0.16f, height * 0.18f),
                HotelArtMaterial.Green, new Vector3(0f, -angle, 18f));
        }
    }

    public static GameObject Part(Transform parent, string name, PrimitiveType primitive,
        Vector3 localPosition, Vector3 localScale, HotelArtMaterial material)
    {
        return Part(parent, name, primitive, localPosition, localScale, material, Vector3.zero);
    }

    public static GameObject Part(Transform parent, string name, PrimitiveType primitive,
        Vector3 localPosition, Vector3 localScale, HotelArtMaterial material, Vector3 localEuler)
    {
        var part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.layer = ArtLayer;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localEulerAngles = localEuler;
        part.transform.localScale = localScale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null) DestroyObject(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = MaterialFor(material);
            renderer.shadowCastingMode = material == HotelArtMaterial.Water
                || material == HotelArtMaterial.GlassBlue
                ? ShadowCastingMode.Off
                : ShadowCastingMode.On;
            renderer.receiveShadows = material != HotelArtMaterial.Water;
        }
        return part;
    }

    public static GameObject NewRoot(string name)
    {
        var root = new GameObject(name);
        root.layer = ArtLayer;
        return root;
    }

    public static GameObject NewChild(Transform parent, string name, Vector3 localPosition)
    {
        return NewChild(parent, name, localPosition, 0f);
    }

    public static GameObject NewChild(Transform parent, string name, Vector3 localPosition, float yaw)
    {
        var child = NewRoot(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return child;
    }

    public static void RemoveColliders(GameObject root)
    {
        if (root == null) return;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            if (collider != null) DestroyObject(collider);
    }

    public static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null) return;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    private static void BuildBed(Transform root, HotelArtMaterial fabric, bool sagging, bool premium)
    {
        float width = premium ? 1.50f : 1.38f;
        float length = premium ? 1.95f : 1.82f;
        float baseY = sagging ? 0.19f : 0.24f;
        Part(root, "Frame", PrimitiveType.Cube, new Vector3(0f, baseY, 0f),
            new Vector3(width, 0.24f, length), HotelArtMaterial.WoodDark);
        Part(root, "Mattress", PrimitiveType.Cube, new Vector3(0f, baseY + 0.19f, sagging ? -0.03f : 0f),
            new Vector3(width * 0.94f, sagging ? 0.18f : 0.28f, length * 0.94f),
            HotelArtMaterial.Cream, sagging ? new Vector3(0f, 0f, 4f) : Vector3.zero);
        Part(root, "Blanket", PrimitiveType.Cube, new Vector3(0f, baseY + 0.35f, -0.28f),
            new Vector3(width * 0.96f, 0.08f, length * 0.52f), fabric);
        Part(root, "Headboard", PrimitiveType.Cube, new Vector3(0f, 0.68f, length * 0.48f),
            new Vector3(width + 0.10f, premium ? 1.10f : 0.86f, 0.13f),
            premium ? HotelArtMaterial.FabricRed : HotelArtMaterial.WoodWarm);
        Part(root, "PillowA", PrimitiveType.Cube, new Vector3(-width * 0.24f, baseY + 0.42f, length * 0.28f),
            new Vector3(width * 0.39f, 0.14f, 0.38f), HotelArtMaterial.White,
            new Vector3(sagging ? 4f : 0f, sagging ? -8f : 0f, 0f));
        Part(root, "PillowB", PrimitiveType.Cube, new Vector3(width * 0.24f, baseY + 0.42f, length * 0.28f),
            new Vector3(width * 0.39f, 0.14f, 0.38f), HotelArtMaterial.White,
            new Vector3(sagging ? -3f : 0f, sagging ? 5f : 0f, 0f));
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(root, "Leg", PrimitiveType.Cylinder, new Vector3(x * width * 0.42f, 0.08f, z * length * 0.42f),
                new Vector3(0.09f, sagging && x == 1 && z == -1 ? 0.035f : 0.08f, 0.09f),
                HotelArtMaterial.WoodDark);
    }

    private static void BuildBathroom(Transform root, bool renovated)
    {
        Part(root, "TileBase", PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f),
            new Vector3(1.48f, 0.05f, 1.48f), renovated ? HotelArtMaterial.TileBlue : HotelArtMaterial.TileCream);

        Part(root, "ToiletBowl", PrimitiveType.Cylinder, new Vector3(-0.40f, 0.25f, 0.34f),
            new Vector3(0.44f, 0.22f, 0.56f), HotelArtMaterial.Porcelain);
        Part(root, "ToiletTank", PrimitiveType.Cube, new Vector3(-0.40f, 0.50f, 0.56f),
            new Vector3(0.48f, 0.48f, 0.20f), HotelArtMaterial.Porcelain);
        Part(root, "Seat", PrimitiveType.Cylinder, new Vector3(-0.40f, 0.47f, 0.25f),
            new Vector3(0.39f, 0.035f, 0.50f), HotelArtMaterial.White);

        Part(root, "Vanity", PrimitiveType.Cube, new Vector3(0.42f, 0.38f, 0.46f),
            new Vector3(0.62f, 0.72f, 0.42f), renovated ? HotelArtMaterial.WoodDark : HotelArtMaterial.MetalLight);
        Part(root, "Basin", PrimitiveType.Cylinder, new Vector3(0.42f, 0.77f, 0.43f),
            new Vector3(0.53f, 0.09f, 0.38f), HotelArtMaterial.Porcelain);
        Part(root, "Mirror", PrimitiveType.Cube, new Vector3(0.42f, 1.10f, 0.70f),
            new Vector3(0.68f, 0.62f, 0.045f), HotelArtMaterial.GlassBlue);

        Part(root, "ShowerTray", PrimitiveType.Cube, new Vector3(0.29f, 0.06f, -0.42f),
            new Vector3(0.82f, 0.12f, 0.70f), HotelArtMaterial.Porcelain);
        Part(root, "ShowerGlassA", PrimitiveType.Cube, new Vector3(0.68f, 0.76f, -0.42f),
            new Vector3(0.035f, 1.40f, 0.72f), HotelArtMaterial.GlassBlue);
        Part(root, "ShowerGlassB", PrimitiveType.Cube, new Vector3(0.29f, 0.76f, -0.76f),
            new Vector3(0.82f, 1.40f, 0.035f), HotelArtMaterial.GlassBlue);
        Part(root, "ShowerPipe", PrimitiveType.Cylinder, new Vector3(0.02f, 0.89f, -0.69f),
            new Vector3(0.035f, 0.55f, 0.035f), HotelArtMaterial.MetalLight);
        Part(root, "ShowerHead", PrimitiveType.Cylinder, new Vector3(0.02f, 1.41f, -0.59f),
            new Vector3(0.16f, 0.035f, 0.16f), HotelArtMaterial.MetalLight,
            new Vector3(90f, 0f, 0f));
    }

    private static void BuildTv(Transform root, bool flat)
    {
        if (flat)
        {
            Part(root, "Screen", PrimitiveType.Cube, new Vector3(0f, 0.86f, 0f),
                new Vector3(1.38f, 0.78f, 0.10f), HotelArtMaterial.Black);
            Part(root, "ScreenGlow", PrimitiveType.Cube, new Vector3(0f, 0.86f, -0.058f),
                new Vector3(1.24f, 0.64f, 0.015f), HotelArtMaterial.GlassBlue);
            Part(root, "Stand", PrimitiveType.Cylinder, new Vector3(0f, 0.34f, 0.03f),
                new Vector3(0.10f, 0.29f, 0.10f), HotelArtMaterial.MetalDark);
            Part(root, "Foot", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.03f),
                new Vector3(0.64f, 0.08f, 0.32f), HotelArtMaterial.MetalDark);
        }
        else
        {
            Part(root, "Cabinet", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f),
                new Vector3(1.02f, 0.82f, 0.54f), HotelArtMaterial.WoodDark);
            Part(root, "Screen", PrimitiveType.Cube, new Vector3(0f, 0.61f, -0.285f),
                new Vector3(0.76f, 0.52f, 0.025f), HotelArtMaterial.Black);
            Part(root, "DialA", PrimitiveType.Cylinder, new Vector3(0.39f, 0.72f, -0.31f),
                new Vector3(0.08f, 0.035f, 0.08f), HotelArtMaterial.Brass, new Vector3(90f, 0f, 0f));
            Part(root, "AntennaA", PrimitiveType.Cylinder, new Vector3(-0.16f, 1.13f, 0f),
                new Vector3(0.025f, 0.30f, 0.025f), HotelArtMaterial.MetalDark, new Vector3(0f, 0f, -24f));
            Part(root, "AntennaB", PrimitiveType.Cylinder, new Vector3(0.16f, 1.13f, 0f),
                new Vector3(0.025f, 0.30f, 0.025f), HotelArtMaterial.MetalDark, new Vector3(0f, 0f, 24f));
        }
    }

    private static void BuildSofa(Transform root, HotelArtMaterial fabric, float width)
    {
        Part(root, "Base", PrimitiveType.Cube, new Vector3(0f, 0.29f, 0f),
            new Vector3(width, 0.38f, 0.72f), HotelArtMaterial.WoodDark);
        Part(root, "Seat", PrimitiveType.Cube, new Vector3(0f, 0.48f, -0.08f),
            new Vector3(width * 0.83f, 0.22f, 0.58f), fabric);
        Part(root, "Back", PrimitiveType.Cube, new Vector3(0f, 0.83f, 0.29f),
            new Vector3(width, 0.76f, 0.20f), fabric, new Vector3(-6f, 0f, 0f));
        Part(root, "ArmL", PrimitiveType.Cube, new Vector3(-width * 0.47f, 0.58f, -0.02f),
            new Vector3(0.20f, 0.46f, 0.78f), fabric);
        Part(root, "ArmR", PrimitiveType.Cube, new Vector3(width * 0.47f, 0.58f, -0.02f),
            new Vector3(0.20f, 0.46f, 0.78f), fabric);
        int cushions = width > 1.55f ? 3 : 2;
        for (int i = 0; i < cushions; i++)
        {
            float x = cushions == 2 ? (i == 0 ? -0.28f : 0.28f) : (i - 1) * 0.42f;
            Part(root, "Cushion_" + i, PrimitiveType.Cube, new Vector3(x, 0.70f, 0.17f),
                new Vector3(0.42f, 0.42f, 0.14f), HotelArtMaterial.Cream,
                new Vector3(-7f, 0f, i % 2 == 0 ? -3f : 3f));
        }
        for (int x = -1; x <= 1; x += 2)
            Part(root, "Foot", PrimitiveType.Cylinder, new Vector3(x * width * 0.38f, 0.08f, -0.15f),
                new Vector3(0.10f, 0.08f, 0.10f), HotelArtMaterial.Brass);
    }

    private static void BuildDesk(Transform root)
    {
        Part(root, "Top", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0f),
            new Vector3(1.28f, 0.12f, 0.64f), HotelArtMaterial.WoodWarm);
        Part(root, "Drawer", PrimitiveType.Cube, new Vector3(0.36f, 0.52f, -0.02f),
            new Vector3(0.42f, 0.30f, 0.58f), HotelArtMaterial.WoodDark);
        Part(root, "Handle", PrimitiveType.Cube, new Vector3(0.36f, 0.54f, -0.32f),
            new Vector3(0.16f, 0.04f, 0.03f), HotelArtMaterial.Brass);
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(root, "Leg", PrimitiveType.Cube, new Vector3(x * 0.53f, 0.34f, z * 0.23f),
                new Vector3(0.08f, 0.68f, 0.08f), HotelArtMaterial.WoodDark);
        BuildChairAt(root, "DeskChair", new Vector3(0f, 0f, -0.78f), 0f, HotelArtMaterial.FabricTeal);
        Part(root, "LampStem", PrimitiveType.Cylinder, new Vector3(-0.42f, 0.94f, 0.04f),
            new Vector3(0.035f, 0.20f, 0.035f), HotelArtMaterial.Brass);
        Part(root, "LampShade", PrimitiveType.Cylinder, new Vector3(-0.42f, 1.18f, 0.04f),
            new Vector3(0.28f, 0.14f, 0.28f), HotelArtMaterial.FabricMustard);
    }

    private static void BuildRug(Transform root, HotelArtMaterial material, float width, float depth)
    {
        Part(root, "Rug", PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f),
            new Vector3(width, 0.05f, depth), material);
        Part(root, "BorderN", PrimitiveType.Cube, new Vector3(0f, 0.055f, depth * 0.45f),
            new Vector3(width * 0.92f, 0.025f, 0.05f), HotelArtMaterial.Brass);
        Part(root, "BorderS", PrimitiveType.Cube, new Vector3(0f, 0.055f, -depth * 0.45f),
            new Vector3(width * 0.92f, 0.025f, 0.05f), HotelArtMaterial.Brass);
    }

    private static void BuildWallArt(Transform root)
    {
        Part(root, "Frame", PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f),
            new Vector3(0.88f, 0.72f, 0.09f), HotelArtMaterial.Brass);
        Part(root, "Canvas", PrimitiveType.Cube, new Vector3(0f, 0.92f, -0.055f),
            new Vector3(0.72f, 0.56f, 0.025f), HotelArtMaterial.FabricTeal);
        Part(root, "ShapeA", PrimitiveType.Cube, new Vector3(-0.18f, 0.98f, -0.075f),
            new Vector3(0.22f, 0.28f, 0.015f), HotelArtMaterial.FabricMustard, new Vector3(0f, 0f, -12f));
        Part(root, "ShapeB", PrimitiveType.Cylinder, new Vector3(0.20f, 0.83f, -0.078f),
            new Vector3(0.18f, 0.018f, 0.18f), HotelArtMaterial.FabricRed, new Vector3(90f, 0f, 0f));
    }

    private static void AddConditionDetails(Transform root)
    {
        var tape = NewChild(root, "Status_Tape", Vector3.zero);
        Part(tape.transform, "TapeA", PrimitiveType.Cube, new Vector3(0f, 0.62f, -0.42f),
            new Vector3(0.85f, 0.045f, 0.035f), HotelArtMaterial.Warning, new Vector3(0f, 0f, 14f));
        Part(tape.transform, "TapeB", PrimitiveType.Cube, new Vector3(0f, 0.62f, -0.44f),
            new Vector3(0.85f, 0.045f, 0.035f), HotelArtMaterial.Warning, new Vector3(0f, 0f, -14f));
        tape.SetActive(false);

        var fault = NewChild(root, "Status_Fault", new Vector3(0f, 1.25f, 0f));
        Part(fault.transform, "MarkStem", PrimitiveType.Cube, new Vector3(0f, 0.13f, 0f),
            new Vector3(0.08f, 0.28f, 0.08f), HotelArtMaterial.Warning);
        Part(fault.transform, "MarkDot", PrimitiveType.Sphere, new Vector3(0f, -0.08f, 0f),
            new Vector3(0.10f, 0.10f, 0.10f), HotelArtMaterial.Warning);
        fault.SetActive(false);
    }

    private static void BuildPublicToilet(Transform root, Vector3 size)
    {
        Part(root, "Floor", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f),
            new Vector3(size.x, 0.06f, size.z), HotelArtMaterial.TileBlue);
        Part(root, "BackWall", PrimitiveType.Cube, new Vector3(0f, 0.40f, size.z * 0.48f),
            new Vector3(size.x, 0.80f, 0.10f), HotelArtMaterial.WallSage);
        Part(root, "SideWall", PrimitiveType.Cube, new Vector3(size.x * 0.48f, 0.40f, 0f),
            new Vector3(0.10f, 0.80f, size.z), HotelArtMaterial.WallSage);
        Part(root, "BackWallTileBand", PrimitiveType.Cube, new Vector3(0f, 0.24f, size.z * 0.42f),
            new Vector3(size.x * 0.92f, 0.38f, 0.035f), HotelArtMaterial.TileCream);
        Part(root, "StallDivider", PrimitiveType.Cube, new Vector3(-0.30f, 0.40f, 0.40f),
            new Vector3(0.07f, 0.76f, 1.05f), HotelArtMaterial.MetalLight);
        Part(root, "StallDoor", PrimitiveType.Cube, new Vector3(-0.84f, 0.36f, -0.06f),
            new Vector3(0.88f, 0.66f, 0.06f), HotelArtMaterial.WallSage,
            new Vector3(0f, -24f, 0f));
        BuildPublicToiletFixture(root, new Vector3(-0.83f, 0f, 0.50f));
        BuildPublicToiletFixture(root, new Vector3(0.26f, 0f, 0.50f));
        Part(root, "Vanity", PrimitiveType.Cube, new Vector3(-0.14f, 0.39f, -0.52f),
            new Vector3(1.65f, 0.12f, 0.46f), HotelArtMaterial.Stone);
        for (int xSign = -1; xSign <= 1; xSign += 2)
            Part(root, "VanityLeg", PrimitiveType.Cube, new Vector3(-0.14f + xSign * 0.68f, 0.19f, -0.52f),
                new Vector3(0.08f, 0.38f, 0.08f), HotelArtMaterial.MetalDark);
        for (int i = 0; i < 2; i++)
        {
            float x = -0.52f + i * 0.76f;
            Part(root, "Sink_" + i, PrimitiveType.Cylinder, new Vector3(x, 0.46f, -0.52f),
                new Vector3(0.40f, 0.07f, 0.29f), HotelArtMaterial.Porcelain);
            Part(root, "Faucet_" + i, PrimitiveType.Cylinder, new Vector3(x, 0.58f, -0.63f),
                new Vector3(0.035f, 0.10f, 0.035f), HotelArtMaterial.MetalLight);
            Part(root, "Mirror_" + i, PrimitiveType.Cube, new Vector3(x, 0.66f, -0.72f),
                new Vector3(0.52f, 0.25f, 0.035f), HotelArtMaterial.GlassBlue);
        }
        Part(root, "WasteBin", PrimitiveType.Cylinder, new Vector3(0.95f, 0.18f, -0.62f),
            new Vector3(0.25f, 0.18f, 0.25f), HotelArtMaterial.MetalDark);
    }

    private static void BuildPublicToiletFixture(Transform root, Vector3 pos)
    {
        var toilet = NewChild(root, "Toilet", pos);
        Part(toilet.transform, "Pedestal", PrimitiveType.Cylinder, new Vector3(0f, 0.16f, 0.10f),
            new Vector3(0.26f, 0.16f, 0.28f), HotelArtMaterial.Porcelain);
        Part(toilet.transform, "Bowl", PrimitiveType.Cylinder, new Vector3(0f, 0.30f, -0.02f),
            new Vector3(0.42f, 0.10f, 0.54f), HotelArtMaterial.Porcelain);
        Part(toilet.transform, "Water", PrimitiveType.Cylinder, new Vector3(0f, 0.405f, -0.04f),
            new Vector3(0.25f, 0.012f, 0.35f), HotelArtMaterial.Water);
        Part(toilet.transform, "Tank", PrimitiveType.Cube, new Vector3(0f, 0.46f, 0.24f),
            new Vector3(0.46f, 0.34f, 0.20f), HotelArtMaterial.Porcelain);
        Part(toilet.transform, "Flush", PrimitiveType.Cube, new Vector3(0.14f, 0.66f, 0.24f),
            new Vector3(0.08f, 0.025f, 0.06f), HotelArtMaterial.MetalLight);
    }

    private static void BuildKitchen(Transform root, Vector3 size)
    {
        Part(root, "Floor", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f),
            new Vector3(size.x, 0.06f, size.z), HotelArtMaterial.TileCream);
        Part(root, "BackWall", PrimitiveType.Cube, new Vector3(0f, 0.40f, size.z * 0.48f),
            new Vector3(size.x, 0.80f, 0.10f), HotelArtMaterial.TileCream);
        Part(root, "SideWall", PrimitiveType.Cube, new Vector3(size.x * 0.48f, 0.40f, 0f),
            new Vector3(0.10f, 0.80f, size.z), HotelArtMaterial.TileCream);

        BuildKitchenette(root, new Vector3(-0.72f, 0f, 0.73f), 0f, 1.55f);
        var stove = NewChild(root, "Stove", new Vector3(0.80f, 0f, 0.73f));
        Part(stove.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f),
            new Vector3(0.78f, 0.84f, 0.56f), HotelArtMaterial.MetalLight);
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(stove.transform, "Hob", PrimitiveType.Cylinder, new Vector3(x * 0.20f, 0.86f, z * 0.13f),
                new Vector3(0.18f, 0.025f, 0.18f), HotelArtMaterial.Black);
        Part(stove.transform, "Backsplash", PrimitiveType.Cube, new Vector3(0f, 0.78f, 0.31f),
            new Vector3(0.82f, 0.42f, 0.035f), HotelArtMaterial.Stone);
        Part(stove.transform, "Hood", PrimitiveType.Cube, new Vector3(0f, 1.11f, 0.12f),
            new Vector3(0.78f, 0.18f, 0.52f), HotelArtMaterial.MetalLight,
            new Vector3(-8f, 0f, 0f));
        Part(stove.transform, "HoodDuct", PrimitiveType.Cylinder, new Vector3(0f, 1.39f, 0.19f),
            new Vector3(0.16f, 0.24f, 0.16f), HotelArtMaterial.MetalDark);

        var prep = NewChild(root, "PrepIsland", new Vector3(0f, 0f, -0.45f));
        Part(prep.transform, "Top", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0f),
            new Vector3(1.50f, 0.10f, 0.66f), HotelArtMaterial.MetalLight);
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(prep.transform, "Leg", PrimitiveType.Cube, new Vector3(x * 0.62f, 0.36f, z * 0.23f),
                new Vector3(0.07f, 0.72f, 0.07f), HotelArtMaterial.MetalDark);
        Part(root, "Fridge", PrimitiveType.Cube, new Vector3(-1.23f, 0.78f, -0.34f),
            new Vector3(0.60f, 1.56f, 0.62f), HotelArtMaterial.MetalLight);
        Part(root, "FridgeHandle", PrimitiveType.Cube, new Vector3(-1.03f, 0.82f, -0.67f),
            new Vector3(0.05f, 0.62f, 0.04f), HotelArtMaterial.MetalDark);
    }

    private static void BuildFloorSupport(Transform root, Vector3 size)
    {
        Part(root, "Floor", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f),
            new Vector3(size.x, 0.06f, size.z), HotelArtMaterial.TileCream);
        Part(root, "BackWall", PrimitiveType.Cube, new Vector3(0f, 0.40f, size.z * 0.48f),
            new Vector3(size.x, 0.80f, 0.08f), HotelArtMaterial.WallSage);
        var shelf = NewChild(root, "LinenShelf", new Vector3(0f, 0f, size.z * 0.28f));
        for (int x = -1; x <= 1; x += 2)
            Part(shelf.transform, "Post", PrimitiveType.Cube, new Vector3(x * size.x * 0.37f, 0.62f, 0f),
                new Vector3(0.06f, 1.24f, 0.42f), HotelArtMaterial.MetalDark);
        for (int y = 0; y < 3; y++)
        {
            Part(shelf.transform, "Shelf_" + y, PrimitiveType.Cube, new Vector3(0f, 0.22f + y * 0.42f, 0f),
                new Vector3(size.x * 0.80f, 0.06f, 0.42f), HotelArtMaterial.MetalLight);
            for (int x = -1; x <= 1; x += 2)
                Part(shelf.transform, "Linen_" + y + "_" + x, PrimitiveType.Cube,
                    new Vector3(x * size.x * 0.20f, 0.31f + y * 0.42f, -0.02f),
                    new Vector3(size.x * 0.30f, 0.14f, 0.34f), HotelArtMaterial.White);
        }
        BuildLuggageCart(root, new Vector3(0f, 0f, -size.z * 0.27f), 0f);
    }

    private static void BuildKitchenette(Transform root, Vector3 position, float yaw, float width)
    {
        var unit = NewChild(root, "Kitchenette", position, yaw);
        Part(unit.transform, "Cabinet", PrimitiveType.Cube, new Vector3(0f, 0.40f, 0f),
            new Vector3(width, 0.80f, 0.52f), HotelArtMaterial.WoodWarm);
        Part(unit.transform, "Counter", PrimitiveType.Cube, new Vector3(0f, 0.84f, -0.02f),
            new Vector3(width + 0.08f, 0.09f, 0.60f), HotelArtMaterial.MetalLight);
        Part(unit.transform, "Sink", PrimitiveType.Cylinder, new Vector3(width * 0.20f, 0.91f, -0.03f),
            new Vector3(0.36f, 0.05f, 0.28f), HotelArtMaterial.Black);
        Part(unit.transform, "Tap", PrimitiveType.Cylinder, new Vector3(width * 0.20f, 1.08f, 0.13f),
            new Vector3(0.035f, 0.18f, 0.035f), HotelArtMaterial.MetalLight);
        Part(unit.transform, "Microwave", PrimitiveType.Cube, new Vector3(-width * 0.26f, 1.12f, 0.08f),
            new Vector3(0.52f, 0.38f, 0.38f), HotelArtMaterial.MetalDark);
        Part(unit.transform, "MicrowaveDoor", PrimitiveType.Cube, new Vector3(-width * 0.29f, 1.12f, -0.12f),
            new Vector3(0.36f, 0.24f, 0.02f), HotelArtMaterial.GlassBlue);
    }

    private static void BuildLuggageCart(Transform root, Vector3 position, float yaw)
    {
        var cart = NewChild(root, "LuggageCart", position, yaw);
        Part(cart.transform, "Base", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f),
            new Vector3(0.88f, 0.16f, 0.55f), HotelArtMaterial.Brass);
        Part(cart.transform, "TopBar", PrimitiveType.Cube, new Vector3(0f, 1.28f, 0f),
            new Vector3(0.88f, 0.07f, 0.07f), HotelArtMaterial.Brass);
        for (int x = -1; x <= 1; x += 2)
            Part(cart.transform, "Post", PrimitiveType.Cylinder, new Vector3(x * 0.39f, 0.72f, 0f),
                new Vector3(0.05f, 0.58f, 0.05f), HotelArtMaterial.Brass);
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(cart.transform, "Wheel", PrimitiveType.Cylinder, new Vector3(x * 0.34f, 0.07f, z * 0.22f),
                new Vector3(0.13f, 0.05f, 0.13f), HotelArtMaterial.Rubber, new Vector3(90f, 0f, 0f));
        Part(cart.transform, "CaseA", PrimitiveType.Cube, new Vector3(-0.18f, 0.43f, 0f),
            new Vector3(0.42f, 0.48f, 0.34f), HotelArtMaterial.FabricRed);
        Part(cart.transform, "CaseB", PrimitiveType.Cube, new Vector3(0.20f, 0.38f, 0.02f),
            new Vector3(0.34f, 0.38f, 0.30f), HotelArtMaterial.FabricTeal);
    }

    private static void BuildRugAt(Transform root, string name, Vector3 position,
        HotelArtMaterial material, float width, float depth)
    {
        var rug = NewChild(root, name, position);
        BuildRug(rug.transform, material, width, depth);
    }

    private static void BuildStanchion(Transform root, Vector3 position)
    {
        var stanchion = NewChild(root, "QueuePost", position);
        Part(stanchion.transform, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f),
            new Vector3(0.28f, 0.04f, 0.28f), HotelArtMaterial.Brass);
        Part(stanchion.transform, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f),
            new Vector3(0.07f, 0.38f, 0.07f), HotelArtMaterial.Brass);
        Part(stanchion.transform, "Cap", PrimitiveType.Sphere, new Vector3(0f, 0.82f, 0f),
            new Vector3(0.14f, 0.14f, 0.14f), HotelArtMaterial.Brass);
    }

    private static void DestroyObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }
}
