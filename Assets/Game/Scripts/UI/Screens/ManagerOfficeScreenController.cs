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

    [Header("Renovation (hub summary + valuation source)")]
    [SerializeField] private RenovationSystem renovation;
    [SerializeField] private TextMeshProUGUI renovSummary;

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

    [Header("Staff detail panel")]
    [SerializeField] private GameObject staffPanel;
    [SerializeField] private Button staffOpenButton;       // the Staff card as a button
    [SerializeField] private Transform rosterContent;      // VerticalLayoutGroup container
    [SerializeField] private StaffCardView staffCardTemplate; // inactive template cloned per member
    [SerializeField] private int raiseStep = 5;

    [Header("Renovation detail panel")]
    [SerializeField] private GameObject renovationPanel;
    [SerializeField] private Button renovationOpenButton;  // the Renovation card as a button
    [SerializeField] private Transform roomListContent;    // ScrollRect content
    [SerializeField] private RoomRowView roomRowTemplate;  // inactive template cloned per room
    [SerializeField] private TextMeshProUGUI renovDetailSummary;

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
        if (staffOpenButton != null) staffOpenButton.onClick.AddListener(OpenStaffDetail);
        if (staffCardTemplate != null) staffCardTemplate.gameObject.SetActive(false);
        if (renovationOpenButton != null) renovationOpenButton.onClick.AddListener(OpenRenovationDetail);
        if (roomRowTemplate != null) roomRowTemplate.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(HandleClose);
        if (financeOpenButton != null) financeOpenButton.onClick.RemoveListener(OpenFinanceDetail);
        if (repayButton != null) repayButton.onClick.RemoveListener(DoRepay);
        if (borrowButton != null) borrowButton.onClick.RemoveListener(DoBorrow);
        if (staffOpenButton != null) staffOpenButton.onClick.RemoveListener(OpenStaffDetail);
        if (renovationOpenButton != null) renovationOpenButton.onClick.RemoveListener(OpenRenovationDetail);
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
        if (staffPanel != null) staffPanel.SetActive(false);
        if (renovationPanel != null) renovationPanel.SetActive(false);
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
        if (renovSummary != null && renovation != null)
            renovSummary.text = $"{renovation.RenovatedCount} / {renovation.TotalRooms} renovated";
    }

    // Rooms feeding valuation: live from RenovationSystem when present, else the serialized fallback.
    private (int open, int renovated) ValuationRooms()
        => renovation != null ? (renovation.OpenCount, renovation.RenovatedCount)
                              : (openRoomsForValue, renovatedRoomsForValue);

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
        var (open, renov) = ValuationRooms();
        int value = economy.ComputeHotelValue(open, renov);
        int credit = economy.CreditLimit(open, renov);

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
        var (open, renov) = ValuationRooms();
        int credit = economy.CreditLimit(open, renov);
        economy.Borrow(Mathf.Min(loanStep, credit));
        RefreshFinance();
    }

    private void OpenStaffDetail()
    {
        if (staffPanel == null) return;
        if (hubGrid != null) hubGrid.SetActive(false);
        staffPanel.SetActive(true);
        _openDetail = staffPanel;
        RebuildRoster();
    }

    public void RebuildRoster()
    {
        if (economy == null || economy.Payroll == null || rosterContent == null || staffCardTemplate == null) return;

        // Clear previous clones (keep the inactive template).
        for (int i = rosterContent.childCount - 1; i >= 0; i--)
        {
            var child = rosterContent.GetChild(i);
            if (child.gameObject != staffCardTemplate.gameObject) Destroy(child.gameObject);
        }

        foreach (var member in economy.Payroll.Roster)
        {
            var go = Instantiate(staffCardTemplate.gameObject, rosterContent);
            go.SetActive(true);
            var view = go.GetComponent<StaffCardView>();
            if (view != null) view.Bind(member, DoRaise, DoFire);
        }
    }

    private void DoRaise(StaffMember member)
    {
        if (economy == null || member == null) return;
        economy.GiveRaise(member, member.DailyWage + raiseStep);
        RebuildRoster();
        Refresh();
    }

    private void DoFire(StaffMember member)
    {
        if (economy == null || member == null) return;
        economy.FireStaff(member);
        RebuildRoster();
        Refresh();
    }

    private void OpenRenovationDetail()
    {
        if (renovationPanel == null) return;
        if (hubGrid != null) hubGrid.SetActive(false);
        renovationPanel.SetActive(true);
        _openDetail = renovationPanel;
        RebuildRooms();
    }

    public void RebuildRooms()
    {
        if (renovation == null || roomListContent == null || roomRowTemplate == null) return;

        for (int i = roomListContent.childCount - 1; i >= 0; i--)
        {
            var child = roomListContent.GetChild(i);
            if (child.gameObject != roomRowTemplate.gameObject) Destroy(child.gameObject);
        }

        foreach (var room in renovation.RoomNumbers)
        {
            var go = Instantiate(roomRowTemplate.gameObject, roomListContent);
            go.SetActive(true);
            var view = go.GetComponent<RoomRowView>();
            if (view != null)
                view.Bind(room, renovation.TierOf(room), renovation.IsRenovating(room),
                          renovation.DaysRemaining(room), renovation.Config, DoUpgrade);
        }

        if (renovDetailSummary != null)
            renovDetailSummary.text = $"{renovation.RenovatedCount} / {renovation.TotalRooms} renovated";
    }

    private void DoUpgrade(int room, RoomTier target)
    {
        if (renovation == null) return;
        renovation.StartRenovation(new[] { room }, target);
        RebuildRooms();
        Refresh();
    }

    private void HandleClose()
    {
        if (_openDetail != null) { ShowHub(); return; }
        OnCloseRequested?.Invoke();
    }
}
