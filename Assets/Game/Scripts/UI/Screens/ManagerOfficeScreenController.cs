using System;
using TMPro;
using UnityEngine;

// Manager Office hub: reads the EconomySystem and shows live summaries
// (Finance / Staff) on the hub cards. (Economy UI — Phase 2 shell.)
[DisallowMultipleComponent]
public sealed class ManagerOfficeScreenController : MonoBehaviour
{
    [Header("Data source")]
    [SerializeField] private EconomySystem economy;

    [Header("Finance card")]
    [SerializeField] private TextMeshProUGUI cashValue;
    [SerializeField] private TextMeshProUGUI loanValue;
    [SerializeField] private TextMeshProUGUI netValue;

    [Header("Staff card")]
    [SerializeField] private TextMeshProUGUI staffValue;

    [Header("Buttons")]
    [SerializeField] private UnityEngine.UI.Button closeButton;

    public event Action OnCloseRequested;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(HandleClose);
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(HandleClose);
    }

    private void OnEnable()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        Refresh();
    }

    public void Refresh()
    {
        if (economy == null) return;

        if (cashValue != null) cashValue.text = $"${economy.Cash:N0}";

        int loan = economy.Loan != null ? economy.Loan.Balance : 0;
        if (loanValue != null) loanValue.text = loan > 0 ? $"-${loan:N0}" : "$0";

        if (staffValue != null)
            staffValue.text = economy.Payroll != null ? $"{economy.Payroll.Count}" : "0";

        if (netValue != null)
        {
            int net = economy.LastDayLedger.Net;
            netValue.text = net >= 0 ? $"+${net:N0}" : $"-${-net:N0}";
        }
    }

    private void HandleClose() => OnCloseRequested?.Invoke();
}
