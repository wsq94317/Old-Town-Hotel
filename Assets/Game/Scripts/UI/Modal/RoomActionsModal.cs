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
