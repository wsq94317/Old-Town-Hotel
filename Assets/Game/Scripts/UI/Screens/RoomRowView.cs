using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One room row in the Renovation detail list. The action button upgrades the room
// to the next tier; disabled while renovating or already top tier.
[DisallowMultipleComponent]
public sealed class RoomRowView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI actionLabel;
    [SerializeField] private Button actionButton;

    private int _room;
    private RoomTier _next;
    private bool _actionable;
    private Action<int, RoomTier> _onUpgrade;

    private void Awake()
    {
        if (actionButton != null)
            actionButton.onClick.AddListener(() => { if (_actionable) _onUpgrade?.Invoke(_room, _next); });
    }

    public void Bind(int room, RoomTier tier, bool renovating, int daysLeft,
                     RenovationConfigSO cfg, Action<int, RoomTier> onUpgrade)
    {
        _room = room;
        _onUpgrade = onUpgrade;

        if (label != null) label.text = $"Room {room} — {tier}";

        if (renovating)
        {
            _actionable = false;
            if (actionLabel != null) actionLabel.text = $"Renovating · {daysLeft}d";
        }
        else if (tier == RoomTier.Old)
        {
            _actionable = true; _next = RoomTier.Basic;
            if (actionLabel != null) actionLabel.text = $"→ Basic (${cfg.CostFor(RoomTier.Basic):N0})";
        }
        else if (tier == RoomTier.Basic)
        {
            _actionable = true; _next = RoomTier.Better;
            if (actionLabel != null) actionLabel.text = $"→ Better (${cfg.CostFor(RoomTier.Better):N0})";
        }
        else
        {
            _actionable = false;
            if (actionLabel != null) actionLabel.text = "Top tier";
        }

        if (actionButton != null) actionButton.interactable = _actionable;
    }
}
