using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One roster card in the Staff detail list. Bound at runtime from a StaffMember;
// Raise / Fire buttons call back into ManagerOfficeScreenController.
[DisallowMultipleComponent]
public sealed class StaffCardView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button raiseButton;
    [SerializeField] private Button fireButton;

    private StaffMember _member;
    private Action<StaffMember> _onRaise;
    private Action<StaffMember> _onFire;

    private void Awake()
    {
        if (raiseButton != null) raiseButton.onClick.AddListener(() => _onRaise?.Invoke(_member));
        if (fireButton != null) fireButton.onClick.AddListener(() => _onFire?.Invoke(_member));
    }

    public void Bind(StaffMember m, Action<StaffMember> onRaise, Action<StaffMember> onFire)
    {
        _member = m;
        _onRaise = onRaise;
        _onFire = onFire;

        if (nameText != null) nameText.text = m.DisplayName;
        if (roleText != null)
        {
            string traits = m.Traits.Count > 0
                ? string.Join(", ", m.Traits.Select(t => StaffTraits.Label(t)))
                : "No traits";
            roleText.text = $"{m.Role} · {traits}";
        }
        if (statsText != null) statsText.text = $"${m.DailyWage}/day · Morale {m.Morale}";
    }
}
