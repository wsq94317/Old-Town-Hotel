using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DayEndSummaryModal : ModalBase
{
    [Header("Modal 6 content")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI servicedCountLabel;
    [SerializeField] private TextMeshProUGUI earningsLabel;
    [SerializeField] private TextMeshProUGUI satisfactionDeltaLabel;
    [SerializeField] private GameObject unlockedSection;
    [SerializeField] private TextMeshProUGUI unlockedListLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button replayButton;

    public override bool DismissOnBackdropTap => false;

    public event Action OnContinueClicked;
    public event Action OnReplayClicked;

    public void Setup(int dayJustCompleted, int servicedCount, int earnings,
                      int satisfactionStart, int satisfactionEnd, string unlockedAchievementsCsv)
    {
        if (titleLabel != null) titleLabel.text = $"Day {dayJustCompleted} Summary";
        if (servicedCountLabel != null) servicedCountLabel.text = $"Guests served: {servicedCount}";
        if (earningsLabel != null) earningsLabel.text = earnings >= 0 ? $"Today: +${earnings:N0}" : $"Today: -${(-earnings):N0}";
        if (satisfactionDeltaLabel != null) satisfactionDeltaLabel.text = $"Satisfaction: {satisfactionStart}% -> {satisfactionEnd}%";

        bool hasUnlocks = !string.IsNullOrEmpty(unlockedAchievementsCsv);
        if (unlockedSection != null) unlockedSection.SetActive(hasUnlocks);
        if (unlockedListLabel != null && hasUnlocks) unlockedListLabel.text = unlockedAchievementsCsv;

        if (replayButton != null) replayButton.gameObject.SetActive(false); // P5+ scope
    }

    protected override void OnOpened()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() => { OnContinueClicked?.Invoke(); Close(); });
        }
        if (replayButton != null)
        {
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(() => { OnReplayClicked?.Invoke(); Close(); });
        }
    }
}
