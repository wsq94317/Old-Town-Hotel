using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ChooseRoomRowView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TextMeshProUGUI roomNumberLabel;
    [SerializeField] private TextMeshProUGUI roomTypeLabel;
    [SerializeField] private TextMeshProUGUI preferenceLabel;
    [SerializeField] private TextMeshProUGUI suitabilityBadgeLabel;
    [SerializeField] private Image suitabilityBadgeBackground;
    [SerializeField] private Button rowButton;

    [Header("Theme")]
    [SerializeField] private UITheme theme;

    public event Action<Room2DEntity> OnRowTapped;

    public Room2DEntity BoundRoom { get; private set; }

    public void Setup(Room2DEntity room, RoomSuitabilityRank rank)
    {
        BoundRoom = room;
        if (room == null) return;

        if (roomNumberLabel != null) roomNumberLabel.text = room.roomNumber.ToString();
        if (roomTypeLabel != null) roomTypeLabel.text = $"{room.roomCategory} ROOM";
        if (preferenceLabel != null) preferenceLabel.text = ""; // soft prefs not modelled yet

        ApplySuitabilityBadge(rank);
    }

    private void ApplySuitabilityBadge(RoomSuitabilityRank rank)
    {
        if (suitabilityBadgeLabel == null) return;
        switch (rank)
        {
            case RoomSuitabilityRank.Suitable:
                suitabilityBadgeLabel.text = "适合 ✓";
                if (suitabilityBadgeBackground != null && theme != null)
                    suitabilityBadgeBackground.color = theme.stateReady;
                break;
            case RoomSuitabilityRank.SoSo:
                suitabilityBadgeLabel.text = "一般";
                if (suitabilityBadgeBackground != null && theme != null)
                    suitabilityBadgeBackground.color = theme.goldAccent;
                break;
            case RoomSuitabilityRank.Unsuitable:
                suitabilityBadgeLabel.text = "不适合";
                if (suitabilityBadgeBackground != null && theme != null)
                    suitabilityBadgeBackground.color = theme.secondaryGrey;
                break;
        }
    }

    private void Awake()
    {
        if (rowButton != null) rowButton.onClick.AddListener(HandleTap);
    }

    private void OnDestroy()
    {
        if (rowButton != null) rowButton.onClick.RemoveListener(HandleTap);
    }

    private void HandleTap() => OnRowTapped?.Invoke(BoundRoom);
}
