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

    [Header("Theme")]
    [SerializeField] private UITheme theme;

    [Header("Editor preview (no effect at runtime)")]
    [SerializeField] private Room2DState previewState = Room2DState.Ready;
    [SerializeField] private int previewRoomNumber = 301;
    [SerializeField] private string previewBedLetter = "K";
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
        ApplyVisuals(state, boundRoom.roomNumber, ExtractBedLetter(boundRoom));
    }

    public void SetTimerText(string text)
    {
        if (timerLabel == null) return;
        bool hasText = !string.IsNullOrEmpty(text);
        timerLabel.gameObject.SetActive(hasText);
        if (hasText) timerLabel.text = text;
    }

    private void ApplyVisuals(Room2DState state, int roomNumber, string bedLetter)
    {
        if (backgroundImage != null && theme != null)
            backgroundImage.color = RoomStateUiMap.GetColor(state, theme);

        if (roomNumberLabel != null)
            roomNumberLabel.text = roomNumber.ToString();

        if (stateLabel != null)
            stateLabel.text = RoomStateUiMap.GetLabel(state);

        if (bedTypeLabel != null)
            bedTypeLabel.text = bedLetter;
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
        ApplyVisuals(previewState, previewRoomNumber, previewBedLetter);
        SetTimerText(previewTimer);
    }
#endif
}
