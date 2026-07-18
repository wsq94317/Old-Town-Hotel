using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RoomTileView : MonoBehaviour
{
    [Header("Required wiring")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI roomNumberLabel;
    [SerializeField] private TextMeshProUGUI stateLabel;
    [SerializeField] private TextMeshProUGUI bedTypeLabel;
    [SerializeField] private TextMeshProUGUI timerLabel;
    [SerializeField] private Button tapButton;
    [SerializeField] private Image stateBadgeImage; // colored strip behind stateLabel

    [Header("Theme")]
    [SerializeField] private UITheme theme;

    [Header("Interior sprites by bed type (optional decoration)")]
    [SerializeField] private Sprite interiorSingle;
    [SerializeField] private Sprite interiorTwin;
    [SerializeField] private Sprite interiorFamily;
    [SerializeField] private Sprite interiorKing;

    [Header("Editor preview (no effect at runtime)")]
    [SerializeField] private Room2DState previewState = Room2DState.Ready;
    [SerializeField] private int previewRoomNumber = 301;
    [SerializeField] private Room2DRoomCategory previewRoomCategory = Room2DRoomCategory.Single;
    [SerializeField] private string previewTimer = "";

    public event Action<RoomTileView> OnTapped;

    private Room2DEntity boundRoom;
    public Room2DEntity BoundRoom => boundRoom;

    public void Bind(Room2DEntity room)
    {
        boundRoom = room;
        Refresh();
    }

    public void Refresh()
    {
        if (boundRoom == null) return;

        var state = boundRoom.currentState;
        ApplyVisuals(state, boundRoom.roomNumber, ExtractBedLetter(boundRoom), boundRoom.roomCategory);
    }

    public void SetTimerText(string text)
    {
        if (timerLabel == null) return;
        bool hasText = !string.IsNullOrEmpty(text);
        timerLabel.gameObject.SetActive(hasText);
        if (hasText) timerLabel.text = text;
    }

    private void ApplyVisuals(Room2DState state, int roomNumber, string bedLetter, Room2DRoomCategory category)
    {
        if (backgroundImage != null)
        {
            // Keep the room photo readable: a light state-hued wash instead of a
            // full tint (the old full tint made every state look the same green).
            if (theme != null)
            {
                Color stateColor = RoomStateUiMap.GetColor(state, theme);
                backgroundImage.color = Color.Lerp(Color.white, stateColor, 0.25f);
                if (stateBadgeImage != null) stateBadgeImage.color = stateColor;
            }
            var interior = PickInteriorSprite(category);
            if (interior != null) backgroundImage.sprite = interior;
        }

        if (roomNumberLabel != null)
            roomNumberLabel.text = roomNumber.ToString();

        if (stateLabel != null)
            stateLabel.text = RoomStateUiMap.GetLabel(state);

        if (bedTypeLabel != null)
            bedTypeLabel.text = bedLetter;
    }

    private Sprite PickInteriorSprite(Room2DRoomCategory category)
    {
        Sprite generated = GeneratedPlaceholderArt.RoomInterior(category);
        if (generated != null) return generated;

        switch (category)
        {
            case Room2DRoomCategory.Single: return interiorSingle;
            case Room2DRoomCategory.Twin:   return interiorTwin;
            case Room2DRoomCategory.Family: return interiorFamily;
            default:                         return interiorSingle;
        }
    }

    private static string LetterForCategory(Room2DRoomCategory category)
    {
        switch (category)
        {
            case Room2DRoomCategory.Single: return "S";
            case Room2DRoomCategory.Twin:   return "T";
            case Room2DRoomCategory.Family: return "F";
            default:                         return "?";
        }
    }

    private static string ExtractBedLetter(Room2DEntity room)
    {
        string fullName = room.roomCategory.ToString();
        if (string.IsNullOrEmpty(fullName)) return "?";
        return fullName.Substring(0, 1).ToUpperInvariant();
    }

    private void Awake()
    {
        if (tapButton != null) tapButton.onClick.AddListener(HandleTap);
    }

    private void OnDestroy()
    {
        if (tapButton != null) tapButton.onClick.RemoveListener(HandleTap);
    }

    private void HandleTap() => OnTapped?.Invoke(this);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        ApplyVisuals(previewState, previewRoomNumber, LetterForCategory(previewRoomCategory), previewRoomCategory);
        SetTimerText(previewTimer);
    }
#endif
}
