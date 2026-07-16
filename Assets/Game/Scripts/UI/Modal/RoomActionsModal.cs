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
    public event Action OnAssignInspectorClicked;
    public event Action OnDoItYourselfClicked;
    public event Action OnSetPriorityClicked;
    public event Action OnRoomInfoClicked;

    public Room2DEntity BoundRoom { get; private set; }

    // 弹窗主行动：主按钮按房态复用——Dirty 派保洁，AwaitingInspection 派主管。
    public enum RoomAction { None, AssignHsk, AssignInspector }

    /// <summary>当前主行动（Setup 时按房态决定；主按钮点击按它路由）。</summary>
    public RoomAction CurrentAction { get; private set; } = RoomAction.None;

    /// <summary>房态 → 主行动映射。Dirty 派保洁，AwaitingInspection 派主管，其余无行动。</summary>
    public static RoomAction ActionModeFor(Room2DState state)
    {
        switch (state)
        {
            case Room2DState.Dirty:               return RoomAction.AssignHsk;
            case Room2DState.AwaitingInspection:  return RoomAction.AssignInspector;
            default:                               return RoomAction.None;
        }
    }

    public void Setup(Room2DEntity room, string floorText, string dirtySinceText, string priorityText)
    {
        BoundRoom = room;
        if (titleLabel != null) titleLabel.text = room != null ? room.roomNumber.ToString() : "—";
        if (stateLabel != null) stateLabel.text = room != null ? RoomStateUiMap.GetLabel(room.currentState) : "";
        if (bedTypeLabel != null) bedTypeLabel.text = room != null ? room.roomCategory.ToString() : "";
        if (floorLabel != null) floorLabel.text = floorText ?? "";
        if (dirtySinceLabel != null) dirtySinceLabel.text = dirtySinceText ?? "";
        if (priorityLabel != null) priorityLabel.text = priorityText ?? "";

        CurrentAction = room != null ? ActionModeFor(room.currentState) : RoomAction.None;
        bool canAct = CurrentAction != RoomAction.None;
        // "Do It Yourself" 只覆盖打扫（BossCover 不会验房）。
        bool canBossCover = CurrentAction == RoomAction.AssignHsk;

        if (assignHskButton != null)
        {
            assignHskButton.interactable = canAct;
            var mainLabel = assignHskButton.GetComponentInChildren<TextMeshProUGUI>();
            if (mainLabel != null)
                mainLabel.text = CurrentAction == RoomAction.AssignInspector ? "Assign Inspector" : "Assign HSK";
        }
        if (doItYourselfButton != null) doItYourselfButton.interactable = canBossCover;
        // Button tint only greys the background — grey the labels too, or a
        // disabled action reads as a broken button.
        TintLabel(assignHskButton, canAct, Color.white);
        TintLabel(doItYourselfButton, canBossCover, new Color(0.227f, 0.165f, 0.110f));
        if (stateLabel != null && room != null && !canAct)
            stateLabel.text += "  ·  no action needed";
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
            assignHskButton.onClick.AddListener(() =>
            {
                // 主按钮按 CurrentAction 路由：Dirty 派保洁，AwaitingInspection 派主管。
                if (CurrentAction == RoomAction.AssignInspector) OnAssignInspectorClicked?.Invoke();
                else OnAssignHskClicked?.Invoke();
                Close();
            });
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
