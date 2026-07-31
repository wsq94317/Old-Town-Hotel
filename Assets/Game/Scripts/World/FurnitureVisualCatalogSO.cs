using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class FurnitureVisualVariant
{
    public int variantId;
    public string displayName = "Default";
    public GameObject prefab;
    [Tooltip("One optional material override per Renderer, in hierarchy order. Null entries keep the prefab material.")]
    public Material[] rendererMaterialOverrides = Array.Empty<Material>();
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public Vector3 scale = Vector3.one;

    public bool IsConfigured => prefab != null;

    public static FurnitureVisualVariant[] CreateReservedSlots(int count)
    {
        var slots = new FurnitureVisualVariant[Mathf.Max(1, count)];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new FurnitureVisualVariant
            {
                variantId = i,
                displayName = i == 0 ? "Default" : "Variant " + (i + 1),
                scale = Vector3.one,
            };
        }
        return slots;
    }
}

[Serializable]
public sealed class FurnitureVisualEntry
{
    public int kindId;
    public FurnitureVisualVariant[] variants =
        FurnitureVisualVariant.CreateReservedSlots(FurnitureVisualCatalogSO.ReservedSlotsPerKind);

    public void EnsureSlots()
    {
        int count = Mathf.Max(FurnitureVisualCatalogSO.ReservedSlotsPerKind,
            variants == null ? 0 : variants.Length);
        var normalized = FurnitureVisualVariant.CreateReservedSlots(count);
        if (variants != null)
        {
            for (int i = 0; i < variants.Length; i++)
                if (variants[i] != null) normalized[i] = variants[i];
        }
        for (int i = 0; i < normalized.Length; i++) normalized[i].variantId = i;
        variants = normalized;
    }
}

/// <summary>
/// Maps simulation furniture kinds to swappable art. Slot zero is the current
/// placeholder; the reserved empty slots are ready for future models.
/// </summary>
[CreateAssetMenu(fileName = "FurnitureVisualCatalog", menuName = "Old Town Hotel/Furniture Visual Catalog")]
public sealed class FurnitureVisualCatalogSO : ScriptableObject
{
    public const int ReservedSlotsPerKind = 4;
    public const string ResourcesPath = "HotelArt/FurnitureVisualCatalog";

    [SerializeField] private List<FurnitureVisualEntry> entries = new List<FurnitureVisualEntry>();
    private static FurnitureVisualCatalogSO _default;

    public IReadOnlyList<FurnitureVisualEntry> Entries => entries;

    public static FurnitureVisualCatalogSO LoadDefault()
    {
        if (_default == null) _default = Resources.Load<FurnitureVisualCatalogSO>(ResourcesPath);
        return _default;
    }

    public bool TryGetVariant(int kindId, int variantId, out FurnitureVisualVariant variant)
    {
        FurnitureVisualEntry entry = FindEntry(kindId);
        if (entry != null && entry.variants != null)
        {
            for (int i = 0; i < entry.variants.Length; i++)
            {
                FurnitureVisualVariant candidate = entry.variants[i];
                if (candidate != null && candidate.variantId == variantId && candidate.IsConfigured)
                {
                    variant = candidate;
                    return true;
                }
            }
        }
        variant = null;
        return false;
    }

    public List<FurnitureVisualVariant> ConfiguredVariantsFor(int kindId)
    {
        var result = new List<FurnitureVisualVariant>();
        FurnitureVisualEntry entry = FindEntry(kindId);
        if (entry == null || entry.variants == null) return result;
        for (int i = 0; i < entry.variants.Length; i++)
            if (entry.variants[i] != null && entry.variants[i].IsConfigured)
                result.Add(entry.variants[i]);
        return result;
    }

    public void EnsureDefaultEntry(int kindId, GameObject defaultPrefab)
    {
        FurnitureVisualEntry entry = FindEntry(kindId);
        if (entry == null)
        {
            entry = new FurnitureVisualEntry { kindId = kindId };
            entries.Add(entry);
        }
        entry.EnsureSlots();
        if (entry.variants[0].prefab == null) entry.variants[0].prefab = defaultPrefab;
        entry.variants[0].displayName = "Default";
    }

    private FurnitureVisualEntry FindEntry(int kindId)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].kindId == kindId) return entries[i];
        return null;
    }

    private void OnValidate()
    {
        for (int i = 0; i < entries.Count; i++) entries[i]?.EnsureSlots();
    }
}

public static class FurnitureVisualLibrary
{
    public static GameObject Create(FurnitureKind kind, int visualVariantId)
    {
        FurnitureVisualCatalogSO catalog = FurnitureVisualCatalogSO.LoadDefault();
        FurnitureVisualVariant variant = null;
        if (catalog != null && !catalog.TryGetVariant(kind.kindId, visualVariantId, out variant))
            catalog.TryGetVariant(kind.kindId, 0, out variant);

        if (variant == null || variant.prefab == null)
            return LowPolyHotelKit.CreateFurniture(kind);

        var root = new GameObject(kind.name + "_Variant_" + variant.variantId);
        GameObject visual = UnityEngine.Object.Instantiate(variant.prefab, root.transform);
        visual.name = "Visual";
        visual.transform.localPosition = variant.positionOffset;
        visual.transform.localEulerAngles = variant.rotationOffset;
        visual.transform.localScale = variant.scale == Vector3.zero ? Vector3.one : variant.scale;
        ApplyMaterialOverrides(visual, variant.rendererMaterialOverrides);
        return root;
    }

    private static void ApplyMaterialOverrides(GameObject root, Material[] overrides)
    {
        if (overrides == null || overrides.Length == 0) return;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length && i < overrides.Length; i++)
        {
            if (overrides[i] == null) continue;
            Material[] materials = renderers[i].sharedMaterials;
            if (materials.Length == 0) continue;
            materials[0] = overrides[i];
            renderers[i].sharedMaterials = materials;
        }
    }
}
