using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryCardView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Image progressFill;
    [SerializeField] private GameObject lowStockTag;

    [Header("Theme")]
    [SerializeField] private UITheme theme;

    [Header("Editor preview")]
    [SerializeField] private Sprite previewIcon;
    [SerializeField] private string previewLabel = "Milk";
    [SerializeField] private int previewCurrent = 8;
    [SerializeField] private int previewMax = 20;
    [SerializeField] private int previewLowThreshold = 5;

    public void Bind(Sprite icon, string label, int current, int max, int lowThreshold)
    {
        if (iconImage != null) iconImage.sprite = icon;
        if (labelText != null) labelText.text = label ?? string.Empty;
        if (countText != null) countText.text = $"{current} / {max}";

        float ratio = (max > 0) ? Mathf.Clamp01((float)current / max) : 0f;
        if (progressFill != null) progressFill.fillAmount = ratio;

        bool low = current <= lowThreshold;
        if (lowStockTag != null) lowStockTag.SetActive(low);
        if (progressFill != null && theme != null)
            progressFill.color = low ? theme.warnRed : theme.brownDeep;
        if (countText != null && theme != null)
            countText.color = low ? theme.warnRed : theme.brownDeep;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (iconImage != null) iconImage.sprite = previewIcon;
        Bind(previewIcon, previewLabel, previewCurrent, previewMax, previewLowThreshold);
    }
#endif
}
