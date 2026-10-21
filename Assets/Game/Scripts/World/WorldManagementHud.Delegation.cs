using UnityEngine;

public sealed partial class WorldManagementHud
{
    private string DelegationText(DelegationReport report)
    {
        return L("Days closed ", "完成日结 ") + report.daysClosed
            + L(" · new arrivals ", " · 接待客人 ") + report.guests
            + "\n" + L("Receipts $", "营业回款 $") + report.receipts
            + L(" · wages paid $", " · 已付工资 $") + report.wagesPaid
            + "\n" + L("Safe $", "保险箱 $") + report.safeAfter
            + L(" · overflow $", " · 溢出账本 $") + report.overflowAfter
            + "\n" + L("Debt $", "债务 $") + report.debtBefore + " → $" + report.debtAfter
            + L(" · blocked rooms ", " · 封房 ") + report.brokenRooms;
    }

    private void BuildDelegationDesk()
    {
        var bridge = HotelSimSceneBridge.Instance;
        if (bridge == null) return;
        AddSection(L("MANAGER DELEGATION", "经理托管"),
            L("Offline: 1 real hour = 1 operating day, up to 8 days. Wages and affordable debt payments come first; no new investments or casino gambling.",
                "离线每 1 小时推进 1 个营业日，最多 8 日。优先支付工资与力所能及的还款；不自行投资或参与赌场赌博。"));
        if (bridge.LastDelegation != null)
            AddInfoCard(L("LAST DELEGATION REPORT", "上次托管报告"), DelegationText(bridge.LastDelegation), Gold, 145f);
        AddActionButton(L("DELEGATE UNTIL DAY CLOSE", "委托经理经营到打烊"), () => {
            bridge.DelegateUntilTomorrow();
            ShowToast(L("Delegation finished. Review the new morning ledger.", "托管结束，请查看新的晨间经营报告。"));
            ForceAllRefresh();
        }, Gold, true);
    }

    private void AddMorningDelegation()
    {
        var bridge = HotelSimSceneBridge.Instance;
        var report = bridge != null ? bridge.LastDelegation : null;
        if (report == null || report.acknowledged) return;
        AddMorningInfo(L("WHILE YOU WERE AWAY", "你不在时发生了什么"), DelegationText(report), Gold, 158f);
        AddMorningAction(L("REPORT REVIEWED", "已查看托管报告"), () => {
            report.acknowledged = true;
            ForceAllRefresh();
        }, Teal, true);
    }
}
