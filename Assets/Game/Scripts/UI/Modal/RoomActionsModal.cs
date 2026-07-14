using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoomActionsModal : ModalBase
{
    [Header("Modal 3 content")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI stateLabel;
    [SerializeField] private TextMeshProUGUI bedTypeLabel;
    [SerializeField] private TextMeshProUGUI floorLabel;
    [SerializeField] private TextMeshProUGUI dirtySinceLabel;
    [SerializeField] private TextMeshProUGUI priorityLabel;
    [SerializeField] private Button assignHskButton;
    [SerializeField] private Button doItYourselfButton; // boss cover (ADR 0008) — optional until prefab wired
    [SerializeField] private Button setPriorityButton;
    [SerializeField] private Button roomInfoButton;
    [SerializeField] private Button closeButton;

    public event Action OnAssignHskClicked;
    public event Action OnDoItYourselfClicked;
    public event Action OnSetPriorityClicked;
    public event Action OnRoomInfoClicked;

    public Room2DEntity BoundRoom { get; private set; }

    public void Setup(Room2DEntity room, string floorText, string dirtySinceText, string priorityText)
    {
        BoundRoom = room;
        if (titleLabel != null) titleLabel.text = room != null ? room.roomNumber.ToString() : "—";
        if (stateLabel != null) stateLabel.text = room != null ? RoomStateUiMap.GetLabel(room.currentState) : "";
        if (bedTypeLabel != null) bedTypeLabel.text = room != null ? room.roomCategory.ToString() : "";
        if (floorLabel != null) floorLabel.text = floorText ?? "";
        if (dirtySinceLabel != null) dirtySinceLabel.text = dirtySinceText ?? "";
        if (priorityLabel != null) priorityLabel.text = priorityText ?? "";

        bool canAssignHsk = room != null && room.currentState == Room2DState.Dirty;
        if (assignHskButton != null) assignHskButton.interactable = canAssignHsk;
        if (doItYourselfButton != null) doItYourselfButton.interactable = canAssignHsk;
        // Button tint only greys the background — grey the labels too, or a
        // disabled action reads as a broken button.
        TintLabel(assignHskButton, canAssignHsk, Color.white);
        TintLabel(doItYourselfButton, canAssignHsk, new Color(0.227f, 0.165f, 0.110f));
        if (stateLabel != null && room != null && !canAssignHsk)
            stateLabel.text += "  ·  nothing to clean";
    }

    private static void TintLabel(Button button, bool enabled, Color enabledColor)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = enabled ? enabledColor : new Color(0.62f, 0.58f, 0.52f);
    }

    protected override void OnOpened()
    {
        if (assignHskButton != null)
        {
            assignHskButton.onClick.RemoveAllListeners();
            assignHskButton.onClick.AddListener(() => { OnAssignHskClicked?.Invoke(); Close(); });
        }
        if (doItYourselfButton != null)
        {
            doItYourselfButton.onClick.RemoveAllListeners();
            doItYourselfButton.onClick.AddListener(() => { OnDoItYourselfClicked?.Invoke(); Close(); });
        }
        if (setPriorityButton != null)
        {
            setPriorityButton.onClick.RemoveAllListeners();
            setPriorityButton.onClick.AddListener(() => { OnSetPriorityClicked?.Invoke(); });
        }
        if (roomInfoButton != null)
        {
            roomInfoButton.onClick.RemoveAllListeners();
            roomInfoButton.onClick.AddListener(() => { OnRoomInfoClicked?.Invoke(); });
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }
}
