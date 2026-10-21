using System.Collections.Generic;
using UnityEngine;

public sealed partial class WorldManagementHud
{
    private StaffMember _confirmDismiss;

    private void BuildEmployeeActions()
    {
        AddSection(L("PEOPLE & MORALE", "员工管理 · 士气与去留"),
            L("Raises increase future payroll. Keep at least one employee in each service role.",
                "加薪将增加之后的工资；每个服务岗位至少保留一名员工。"));
        var economy = FindFirstObjectByType<EconomySystem>();
        if (economy == null) return;
        foreach (var member in new List<StaffMember>(economy.Payroll.Roster))
        {
            if (member.Role == StaffRole.Manager) continue;
            var captured = member;
            AddEmployeeVisualCard(captured);
            var actions = CreateEmployeeActionRow();
            AddEmployeeAction(actions, L("LOCATE", "定位"), () => {
                var agents = FindObjectsByType<StaffAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var agent in agents) if (ReferenceEquals(agent.Member, captured)) {
                    SetDrawerOpen(false); FocusWorldPoint(agent.transform.position); return;
                }
                ShowToast(L("Off shift: this employee has left the hotel.", "该员工已下班离场。"));
            }, Ink, true);
            AddEmployeeAction(actions, L("RAISE +$5", "加薪 +$5"), () => {
                economy.GiveRaise(captured, captured.DailyWage + 5);
                ShowToast(L("Raise agreed. Morale improved; future payroll is higher.", "加薪已执行，士气提升，之后工资支出增加。"));
                ForceDrawerRefresh();
            }, Ink, true);
            AddEmployeeAction(actions, _confirmDismiss == captured ? L("CONFIRM", "确认解雇") : L("DISMISS", "解雇"), () => {
                if (_confirmDismiss != captured) { _confirmDismiss = captured; ForceDrawerRefresh(); return; }
                if (CountStaff(captured.Role) <= 1) return;
                economy.FireStaff(captured); _confirmDismiss = null;
                ShowToast(L("Employee dismissed. They will leave through the lobby.", "已解除雇佣，员工将从大堂离场。"));
                ForceDrawerRefresh();
            }, Coral, CountStaff(captured.Role) > 1);
        }
    }

    private void BuildBorrowingActions()
    {
        var economy = FindFirstObjectByType<EconomySystem>();
        if (economy == null || economy.Loan == null) return;
        int baseLimit = economy.CreditLimit(Sim.Rooms.OpenRoomCount, Sim.Journey.State.renovated.Count);
        int available = Sim.Credit.BorrowingLimitFor(baseLimit);
        AddInfoCard(L("BANK CREDIT LINE", "银行授信 · 扩张资金"),
            L("Hotel value $", "酒店估值 $") + economy.ComputeHotelValue(Sim.Rooms.OpenRoomCount, Sim.Journey.State.renovated.Count)
            + L(" · available new credit $", " · 可新增借款 $") + available
            + "\n" + L("Credit ", "信用 ") + Sim.Credit.Score + "/100"
            + L(". Loans add cash and debt; interest accrues daily.", "。借款同时增加现金和债务，利息按日累计。"), Gold, 110);
        var amounts = new HashSet<int>();
        foreach (int target in new[] { 500, 2000, 10000 })
        {
            int amount = Mathf.Min(target, available);
            if (amount <= 0 || !amounts.Add(amount)) continue;
            int captured = amount;
            AddActionButton(L("BORROW $", "新增借款 $") + amount, () => {
                int current = Sim.Credit.BorrowingLimitFor(economy.CreditLimit(Sim.Rooms.OpenRoomCount, Sim.Journey.State.renovated.Count));
                if (captured > current) { ForceDrawerRefresh(); return; }
                economy.Borrow(captured);
                ShowToast(L("Loan received; the principal and future interest increased.", "借款已到账，待还本金和之后利息同步增加。"));
                ForceAllRefresh();
            }, Ink, true);
        }
    }
}
