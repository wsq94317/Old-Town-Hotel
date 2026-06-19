using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Manager Office hub + detail panels. Reads EconomySystem for live data.
// Hub shows summary cards; tapping a card opens its detail panel. The shared
// Back button is context-aware: closes an open detail first, else closes the office.
[DisallowMultipleComponent]
public sealed class ManagerOfficeScreenController : MonoBehaviour
{
    [Header("Data source")]
    [SerializeField] private EconomySystem economy;

    [Header("Finance card (hub summary)")]
    [SerializeField] private TextMeshProUGUI cashValue;
    [SerializeField] private TextMeshProUGUI loanValue;
    [SerializeField] private TextMeshProUGUI netValue;

    [Header("Staff card (hub summary)")]
    [SerializeField] private TextMeshProUGUI staffValue;

    [Header("Navigation")]
    [SerializeField] private GameObject hubGrid;           // the 2x2 card grid (hidden while a detail is open)
    [SerializeField] private Button closeButton;           // shared Back
    [SerializeField] private Button financeOpenButton;     // the Finance card as a button

    [Header("Finance detail panel")]
    [SerializeField] private GameObject financePanel;
    [SerializeField] private TextMeshProUGUI finCash;
    [SerializeField] private TextMeshProUGUI finLoan;
    [SerializeField] private TextMeshProUGUI finInterest;
    [SerializeField] private TextMeshProUGUI finValue;
    [SerializeField] private TextMeshProUGUI finCredit;
    [SerializeField] private Button repayButton;
    [SerializeField] private Button borrowButton;

    [Header("Valuation inputs (until renovation feeds these)")]
    [SerializeField] private int openRoomsForValue = 4;
    [SerializeField] private int renovatedRoomsForValue = 0;
    [SerializeField] private int loanStep = 5000;

    public event Action OnCloseRequested;

    private GameObject _openDetail; // null = hub view

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(HandleClose);
        if (financeOpenButton != null) financeOpenButton.onClick.AddListener(OpenFinanceDetail);
        if (repayButton != null) repayButton.onClick.AddListener(DoRepay);
        if (borrowButton != null) borrowButton.onClick.AddListener(DoBorrow);
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(HandleClose);
        if (financeOpenButton != null) financeOpenButton.onClick.RemoveListener(OpenFinanceDetail);
        if (repayButton != null) repayButton.onClick.RemoveListener(DoRepay);
        if (borrowButton != null) borrowButton.onClick.RemoveListener(DoBorrow);
    }

    private void OnEnable()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        ShowHub();
    }

    private void ShowHub()
    {
        _openDetail = null;
        if (financePanel != null) financePanel.SetActive(false);
        if (hubGrid != null) hubGrid.SetActive(true);
        Refresh();
    }

    // Hub summary
    public void Refresh()
    {
        if (economy == null) return;
        if (cashValue != null) cashValue.text = $"${economy.Cash:N0}";
        int loan = economy.Loan != null ? economy.Loan.Balance : 0;
        if (loanValue != null) loanValue.text = loan > 0 ? $"-${loan:N0}" : "$0";
        if (staffValue != null) staffValue.text = economy.Payroll != null ? $"{economy.Payroll.Count}" : "0";
        if (netValue != null)
        {
            int net = economy.LastDayLedger.Net;
            netValue.text = net >= 0 ? $"+${net:N0}" : $"-${-net:N0}";
        }
    }

    private void OpenFinanceDetail()
    {
        if (financePanel == null) return;
        if (hubGrid != null) hubGrid.SetActive(false);
        financePanel.SetActive(true);
        _openDetail = financePanel;
        RefreshFinance();
    }

    public void RefreshFinance()
    {
        if (economy == null) return;
        int loan = economy.Loan != null ? economy.Loan.Balance : 0;
        float rate = economy.Loan != null ? economy.Loan.DailyInterestRate : 0f;
        int interest = Mathf.RoundToInt(loan * rate);
        int value = economy.ComputeHotelValue(openRoomsForValue, renovatedRoomsForValue);
        int credit = economy.CreditLimit(openRoomsForValue, renovatedRoomsForValue);

        if (finCash != null) finCash.text = $"${economy.Cash:N0}";
        if (finLoan != null) finLoan.text = loan > 0 ? $"-${loan:N0}" : "$0";
        if (finInterest != null) finInterest.text = $"-${interest:N0} / day";
        if (finValue != null) finValue.text = $"${value:N0}";
        if (finCredit != null) finCredit.text = $"${credit:N0}";
    }

    private void DoRepay()
    {
        if (economy == null) return;
        economy.RepayLoan(loanStep);
        RefreshFinance();
    }

    private void DoBorrow()
    {
        if (economy == null) return;
        int credit = economy.CreditLimit(openRoomsForValue, renovatedRoomsForValue);
        economy.Borrow(Mathf.Min(loanStep, credit));
        RefreshFinance();
    }

    private void HandleClose()
    {
        if (_openDetail != null) { ShowHub(); return; }
        OnCloseRequested?.Invoke();
    }
}
