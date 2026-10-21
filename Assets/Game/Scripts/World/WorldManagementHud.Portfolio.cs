using UnityEngine;

public sealed partial class WorldManagementHud
{
    private void BuildFinanceActions()
    {
        BuildBorrowingActions();
        var economy = FindFirstObjectByType<EconomySystem>();
        if (economy == null || economy.Loan == null || economy.Loan.Balance <= 0) return;
        AddInfoCard(L("BANK DEBT", "银行债务"),
            L("Outstanding $", "待还本金及计息余额 $") + economy.Loan.Balance
            + "\n" + L("Available cash + safe $", "可用现金与保险箱 $") + (Sim.Cash + Sim.Safebox.Balance)
            + L(" · unpaid wages $", " · 待付工资 $") + Sim.Payroll.Owed,
            Gold, 94f);
        var offered = new System.Collections.Generic.HashSet<int>();
        foreach (int target in new[] { 100, 500, 2000 }) {
            int amount = Mathf.Min(target, economy.Loan.Balance);
            if (!offered.Add(amount)) continue;
            int captured = amount;
            AddActionButton(L("REPAY PRINCIPAL  $", "提前偿还债务  $") + amount, () => {
                PayBank(captured);
            }, Ink, Sim.Cash + Sim.Safebox.Balance >= amount);
        }
    }

    private void BuildPortfolioChoices()
    {
        AddActionButton(L("OPEN DISTRICT MAP", "打开帝国街区地图"), OpenEmpireMap, Teal, true);
        AddSection(L("CHOOSE YOUR NEXT PROPERTY", "选择下一家酒店的生意"),
            L("Delegated properties have separate operating costs. The resort has a five-day demand cycle; this desk manages them through reports.",
                "新物业由驻店经理托管，各自承担运营成本；度假店有五日淡旺季，通过此面板管理。"));
        int number = 1;
        foreach (var owned in HotelEmpireProgress.Properties) {
            number++;
            AddInfoCard(L("HOTEL ", "第 ") + number + " · " + PropertyName(owned),
                L("Gross $", "日营收 $") + HotelEmpireProgress.GrossOf(owned)
                + L(" · running cost $", " · 运营成本 $") + HotelEmpireProgress.UpkeepOf(owned), Teal, 70f);
        }
        AddInfoCard(L("TODAY'S PORTFOLIO", "今日物业组合"),
            L("Expected net receipts $", "预计净回款 $") + HotelEmpireProgress.IncomeForDay(Sim.Clock.CurrentDay)
            + L(" · paid into the hotel safe at day close", " · 日结时汇入酒店保险箱"), Gold, 78f);
        foreach (var kind in new[] { PortfolioHotelKind.Value, PortfolioHotelKind.Business, PortfolioHotelKind.Resort }) {
            HotelDistrictSite target = null;
            foreach(var site in HotelDistrictCatalog.Sites)
                if(!HotelEmpireProgress.IsOwned(site.Id) && site.Kind==kind && HotelEmpireProgress.IsUnlocked(site)) { target=site; break; }
            if(target==null) continue;
            var captured = target;
            int cost = target.Price;
            int net = HotelEmpireProgress.GrossOf(kind) - HotelEmpireProgress.UpkeepOf(kind);
            AddPlanButton(L(target.EnglishName,target.Name) + L(" · BUY $", " · 收购 $") + cost,
                L("Gross $", "日营收 $") + HotelEmpireProgress.GrossOf(kind)
                + L(" - running cost $", " - 运营费 $") + HotelEmpireProgress.UpkeepOf(kind)
                + L(" = net $", " = 日净回款 $") + net
                + "\n" + (kind == PortfolioHotelKind.Resort
                    ? L("Five-day season: net varies by -$300 to +$300/day.", "五日淡旺季：每日净回款在基准上下浮动 $300。")
                    : L("Stable delegated operations; less active management.", "稳定托管经营，管理负担较低。"))
                + "\n" + L("Cash after purchase $", "收购后现金 $") + (Sim.Cash - cost)
                + L(" · unpaid wages $", " · 待付工资 $") + Sim.Payroll.Owed,
                () => {
                    bool ok = HotelEmpireProgress.TryAcquireSite(Sim, captured.Id, out string message);
                    ShowToast(GameText.T(message));
                    if (ok) {
                        FindFirstObjectByType<SaveCoordinator>()?.RequestProgressAutosave();
                        ShowMoneyBurst(L("NEW PROPERTY ACQUIRED!", "新酒店收购成功！"), Gold);
                    }
                    ForceAllRefresh();
                }, Gold, Sim.Cash >= cost);
        }
    }

    private string PropertyName(PortfolioHotelKind kind) => kind == PortfolioHotelKind.Business
        ? L("BUSINESS HOTEL", "商务酒店") : kind == PortfolioHotelKind.Resort
        ? L("RESORT HOTEL", "度假酒店") : kind == PortfolioHotelKind.Legacy
        ? L("EXISTING PROPERTY", "既有物业") : L("VALUE HOTEL", "平价旅馆");

    private string FacilityImpactText(HotelBusinessKind kind)
    {
        switch (kind) {
            case HotelBusinessKind.Gym: return L("Business guest satisfaction +0.04 · upkeep $25/day. No guaranteed direct income.",
                "商务客满意度 +0.04 · 维护费 $25/日；不承诺直接营收。");
            case HotelBusinessKind.Casino: return L("Night session may gain or lose money · upkeep $50/day. Delegation skips gambling.",
                "夜场可能赚钱或亏钱 · 维护费 $50/日；离线托管不参与赌博。");
            case HotelBusinessKind.Pool: return L("VIP/party guest satisfaction +0.05 · upkeep $40/day. Party incidents remain possible.",
                "VIP/派对客满意度 +0.05 · 维护费 $40/日；仍可能发生派对事件。");
            default: return L("Bar receipts: $8 per occupied room at the evening service.", "酒吧晚市：每间在住房带来 $8 回款。");
        }
    }
}
