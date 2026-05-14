using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QuickActionButtonView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Editor preview")]
    [SerializeField] private Sprite previewIcon;
    [SerializeField] private string previewLabel = "清洗杯子";
    [SerializeField] private bool previewEnabled = true;

    public event Action OnClicked;

    public void Bind(Sprite icon, string label, bool enabled)
    {
        if (iconImage != null) iconImage.sprite = icon;
        if (labelText != null) labelText.text = label ?? string.Empty;
        SetInteractable(enabled);
    }

    public void SetInteractable(bool value)
    {
        if (button != null) button.interactable = value;
        if (canvasGroup != null) canvasGroup.alpha = value ? 1f : 0.5f;
    }

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick() => OnClicked?.Invoke();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        Bind(previewIcon, previewLabel, previewEnabled);
    }
#endif
}
