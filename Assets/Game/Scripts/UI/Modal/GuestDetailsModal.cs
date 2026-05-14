using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GuestDetailsModal : ModalBase
{
    [Header("Modal 2 content")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI typeLabel;
    [SerializeField] private TextMeshProUGUI requiredRoomLabel;
    [SerializeField] private TextMeshProUGUI preferenceLabel;
    [SerializeField] private TextMeshProUGUI waitLabel;
    [SerializeField] private TextMeshProUGUI moodLabel;
    [SerializeField] private TextMeshProUGUI budgetLabel;
    [SerializeField] private TextMeshProUGUI plannedNightsLabel;
    [SerializeField] private Button assignRoomButton;
    [SerializeField] private Button refuseCheckInButton;
    [SerializeField] private Button closeButton;

    public event Action OnAssignRoomClicked;
    public event Action OnRefuseCheckInClicked;

    public object BoundGuestRef { get; private set; }

    public void Setup(object guestRef, Sprite portrait, string type, string requiredRoom,
                      string preference, string waitText, string moodText,
                      string budgetText, string plannedNightsText)
    {
        BoundGuestRef = guestRef;
        if (portraitImage != null) portraitImage.sprite = portrait;
        if (typeLabel != null) typeLabel.text = type ?? "";
        if (requiredRoomLabel != null) requiredRoomLabel.text = requiredRoom ?? "";
        if (preferenceLabel != null) preferenceLabel.text = preference ?? "";
        if (waitLabel != null) waitLabel.text = waitText ?? "";
        if (moodLabel != null) moodLabel.text = moodText ?? "";
        if (budgetLabel != null) budgetLabel.text = budgetText ?? "";
        if (plannedNightsLabel != null) plannedNightsLabel.text = plannedNightsText ?? "";
    }

    protected override void OnOpened()
    {
        if (assignRoomButton != null)
        {
            assignRoomButton.onClick.RemoveAllListeners();
            assignRoomButton.onClick.AddListener(() => { OnAssignRoomClicked?.Invoke(); Close(); });
        }
        if (refuseCheckInButton != null)
        {
            refuseCheckInButton.onClick.RemoveAllListeners();
            refuseCheckInButton.onClick.AddListener(() => { OnRefuseCheckInClicked?.Invoke(); Close(); });
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }
}
