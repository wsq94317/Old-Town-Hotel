using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Replacement metadata for lobby and amenity furniture prefab instances.
/// Four options are reserved by default and the array can grow in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneFurnitureSlot : MonoBehaviour
{
    public const int ReservedOptionCount = 4;

    [SerializeField] private string stableId;
    [SerializeField] private int selectedOption;
    [SerializeField] private FurnitureVisualVariant[] options =
        FurnitureVisualVariant.CreateReservedSlots(ReservedOptionCount);

    private readonly List<Renderer> _defaultRenderers = new List<Renderer>();
    private GameObject _replacement;

    public string StableId => stableId;
    public int SelectedOption => selectedOption;
    public FurnitureVisualVariant[] Options => options;

    public bool HasDefaultConfiguration(string id, GameObject defaultPrefab)
    {
        return stableId == id
               && options != null
               && options.Length >= ReservedOptionCount
               && options[0] != null
               && options[0].prefab == defaultPrefab;
    }

    public void ConfigureDefault(string id, GameObject defaultPrefab)
    {
        stableId = id;
        EnsureSlots();
        if (options[0].prefab == null) options[0].prefab = defaultPrefab;
        options[0].displayName = "Default";
    }

    public bool TrySelect(int optionIndex)
    {
        EnsureSlots();
        if (optionIndex < 0 || optionIndex >= options.Length || !options[optionIndex].IsConfigured)
            return false;
        selectedOption = optionIndex;
        ApplySelectedOption();
        return true;
    }

    public void ApplySelectedOption()
    {
        EnsureSlots();
        CaptureDefaultRenderers();
        DestroyReplacement();

        FurnitureVisualVariant option = options[selectedOption];
        bool useDefaultModel = selectedOption == 0;
        for (int i = 0; i < _defaultRenderers.Count; i++)
            if (_defaultRenderers[i] != null) _defaultRenderers[i].enabled = useDefaultModel;

        if (useDefaultModel)
        {
            ApplyMaterialOverrides(_defaultRenderers, option.rendererMaterialOverrides);
            return;
        }

        _replacement = Instantiate(option.prefab, transform);
        _replacement.name = "FurnitureVariant_" + selectedOption;
        _replacement.transform.localPosition = option.positionOffset;
        _replacement.transform.localEulerAngles = option.rotationOffset;
        _replacement.transform.localScale = option.scale == Vector3.zero ? Vector3.one : option.scale;
        ApplyMaterialOverrides(
            new List<Renderer>(_replacement.GetComponentsInChildren<Renderer>(true)),
            option.rendererMaterialOverrides);
    }

    private void Awake()
    {
        CaptureDefaultRenderers();
        if (selectedOption > 0 && selectedOption < options.Length && options[selectedOption].IsConfigured)
            ApplySelectedOption();
    }

    private void Reset()
    {
        options = FurnitureVisualVariant.CreateReservedSlots(ReservedOptionCount);
    }

    private void OnValidate()
    {
        EnsureSlots();
        selectedOption = Mathf.Clamp(selectedOption, 0, options.Length - 1);
    }

    private void EnsureSlots()
    {
        int count = Mathf.Max(ReservedOptionCount, options == null ? 0 : options.Length);
        var normalized = FurnitureVisualVariant.CreateReservedSlots(count);
        if (options != null)
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i] != null) normalized[i] = options[i];
        }
        for (int i = 0; i < normalized.Length; i++) normalized[i].variantId = i;
        options = normalized;
    }

    private void CaptureDefaultRenderers()
    {
        if (_defaultRenderers.Count > 0) return;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++) _defaultRenderers.Add(renderers[i]);
    }

    private static void ApplyMaterialOverrides(
        List<Renderer> renderers,
        Material[] overrides)
    {
        if (overrides == null) return;
        for (int i = 0; i < renderers.Count && i < overrides.Length; i++)
        {
            if (renderers[i] == null || overrides[i] == null) continue;
            Material[] materials = renderers[i].sharedMaterials;
            if (materials.Length == 0) continue;
            materials[0] = overrides[i];
            renderers[i].sharedMaterials = materials;
        }
    }

    private void DestroyReplacement()
    {
        if (_replacement == null) return;
        if (Application.isPlaying) Destroy(_replacement);
        else DestroyImmediate(_replacement);
        _replacement = null;
    }
}
