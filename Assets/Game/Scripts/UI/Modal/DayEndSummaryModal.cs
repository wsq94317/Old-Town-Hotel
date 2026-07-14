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
    [SerializeField] private TextMeshProUGUI wagesLabel; // optional; null-safe (income/wages breakdown)
    [SerializeField] private TextMeshProUGUI satisfactionDeltaLabel;
    [SerializeField] private GameObject unlockedSection;
    [SerializeField] private TextMeshProUGUI unlockedListLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button replayButton;

    public override bool DismissOnBackdropTap => false;

    public event Action OnContinueClicked;
    public event Action OnReplayClicked;

    public void Setup(int dayJustCompleted, int servicedCount, int income, int wages, int interest,
                      int satisfactionStart, int satisfactionEnd, string unlockedAchievementsCsv)
    {
        int net = income - wages - interest;
        if (titleLabel != null) titleLabel.text = $"Day {dayJustCompleted} Summary";
        if (servicedCountLabel != null) servicedCountLabel.text = $"Guests served: {servicedCount}";
        if (earningsLabel != null)
        {
            earningsLabel.text = net >= 0 ? $"Net: +${net:N0}" : $"Net: -${(-net):N0}";
            earningsLabel.color = net >= 0 ? new Color(0.416f, 0.624f, 0.361f)   // profit green
                                           : new Color(0.659f, 0.267f, 0.180f);  // loss red
        }
        if (wagesLabel != null)
            wagesLabel.text = interest > 0
                ? $"Income ${income:N0}   Wages -${wages:N0}   Interest -${interest:N0}"
                : $"Income ${income:N0}   Wages -${wages:N0}";
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
