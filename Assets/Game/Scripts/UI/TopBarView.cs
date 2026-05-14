using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TopBarView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TextMeshProUGUI dayLabel;
    [SerializeField] private TextMeshProUGUI timeLabel;
    [SerializeField] private Image moodIcon;
    [SerializeField] private TextMeshProUGUI moodPercentLabel;
    [SerializeField] private TextMeshProUGUI moneyLabel;
    [SerializeField] private Button settingsButton;

    [Header("Mood icon sprites (index 0=happy ... 3=angry)")]
    [SerializeField] private Sprite moodHappy;
    [SerializeField] private Sprite moodNormal;
    [SerializeField] private Sprite moodSad;
    [SerializeField] private Sprite moodAngry;

    [Header("Editor preview")]
    [SerializeField] private int previewDay = 1;
    [SerializeField] private string previewTimeLabel = "PEAK";
    [SerializeField, Range(0, 100)] private int previewMoodPercent = 78;
    [SerializeField] private int previewMoney = 2450;

    public event Action OnSettingsClicked;

    public void SetDay(int day)
    {
        if (dayLabel != null) dayLabel.text = $"DAY {day}";
    }

    public void SetTimeLabel(string label)
    {
        if (timeLabel != null) timeLabel.text = label ?? string.Empty;
    }

    public void SetMoodPercent(int percent)
    {
        percent = Mathf.Clamp(percent, 0, 100);
        if (moodPercentLabel != null) moodPercentLabel.text = $"{percent}%";
        if (moodIcon != null) moodIcon.sprite = PickMoodSprite(percent);
    }

    public void SetMoney(int amount)
    {
        if (moneyLabel != null) moneyLabel.text = $"${amount:N0}";
    }

    private Sprite PickMoodSprite(int percent)
    {
        if (percent >= 75) return moodHappy != null ? moodHappy : moodNormal;
        if (percent >= 50) return moodNormal;
        if (percent >= 25) return moodSad;
        return moodAngry != null ? moodAngry : moodSad;
    }

    private void Awake()
    {
        if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettingsClicked);
    }

    private void OnDestroy()
    {
        if (settingsButton != null) settingsButton.onClick.RemoveListener(HandleSettingsClicked);
    }

    private void HandleSettingsClicked() => OnSettingsClicked?.Invoke();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        SetDay(previewDay);
        SetTimeLabel(previewTimeLabel);
        SetMoodPercent(previewMoodPercent);
        SetMoney(previewMoney);
    }
#endif
}
