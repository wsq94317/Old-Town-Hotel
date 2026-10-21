using UnityEngine;

public sealed partial class WorldManagementHud
{
    public static void NotifyMessage(string message)
    {
        if (ActiveInstance != null) ActiveInstance.ShowToast(message);
    }
    private bool _showJourneyHistory;
    private int _seenJourneyStage = -1;
    private int _seenRestorations = -1;
    private int _seenRenovations = -1;

    private string JourneyTitle(int stage)
    {
        switch (stage) {
            case 0: return L("WELCOME YOUR FIRST GUEST", "接待第一位客人");
            case 1: return L("COLLECT YOUR FIRST RECEIPTS", "收取第一笔营业回款");
            case 2: return L("BRING A DERELICT ROOM BACK", "让第一间破败房重新营业");
            case 3: return L("COMPLETE YOUR FIRST RENOVATION", "完成第一次客房装修");
            case 4: return L("THREE PROFITABLE DAYS", "连续三个营业日盈利");
            default: return HotelEmpireProgress.HotelCount > 1
                ? L("THE HOTEL IS BACK IN BUSINESS", "酒店已经重获新生")
                : L("BUILD YOUR SECOND HOTEL", "收购第二家酒店");
        }
    }

    private string JourneyHint(int stage)
    {
        switch (stage) {
            case 0: return L("Keep a receptionist at the desk and clean rooms ready. Arrivals grow after 10:00.",
                "保持前台有人值守、客房清洁可售；10:00 后客人开始陆续到店。");
            case 1: return L("Orders are paid at checkout. At day close, collect receipts from the safe; reserve wages first.",
                "订单在退房时结算；日结后从保险箱收款，记得预留员工工资。");
            case 2: return L("Choose free housekeeping clearance or pay for restoration. A cleared room still needs cleaning.",
                "选择免费派客房部清垃圾，或付费复原；恢复后的房间还需清洁验房。");
            case 3: return L("Choose an empty room, compare materials and closure days, then wait for handover. Set an honest price band afterwards.",
                "选一间空房，比较材料和停业天数，等待装修交付；完成后按实际品质调整挂牌档。");
            case 4: return L("Revenue must cover wages and interest for three consecutive days. Repairs and investments still consume cash separately.",
                "连续三天让营收覆盖佣金、工资和利息；维修与投资仍会额外消耗现金。");
            default: return L("Balance local investment and debt before spending $25,000 on expansion.",
                "先平衡本店投资与债务，再考虑投入 $25,000 扩张。");
        }
    }

    private void RefreshJourneyGoal()
    {
        int stage = Sim.Journey.Stage;
        _goalTitleLabel.text = L("YOUR COMEBACK  ·  ", "翻身目标  ·  ") + JourneyTitle(stage);
        _goalBenefitLabel.text = stage == 4
            ? Sim.Journey.State.profitableStreak + "/3  ·  " + JourneyHint(stage)
            : L("STEP ", "阶段 ") + (stage + 1) + "/6  ·  " + JourneyHint(stage);
        float progress = stage == 4 ? Sim.Journey.State.profitableStreak / 3f : stage / 6f;
        if (stage == 5 && HotelEmpireProgress.HotelCount > 1) progress = 1f;
        _goalProgressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    private void BuildJourneyDesk()
    {
        int stage = Sim.Journey.Stage;
        AddInfoCard(L("YOUR COMEBACK · ", "当前翻身目标 · ") + JourneyTitle(stage),
            JourneyHint(stage), Gold, 112f);
        AddActionButton(L("GO TO CURRENT OBJECTIVE", "前往当前目标"), OpenJourneyObjective, Gold, true);
        AddActionButton(_showJourneyHistory ? L("HIDE GROWTH RECORD", "收起成长档案")
            : L("VIEW GROWTH RECORD", "查看成长档案"), () => {
                _showJourneyHistory = !_showJourneyHistory; ForceDrawerRefresh();
            }, Ink, true);
        if (!_showJourneyHistory) return;
        for (int i = 0; i < 6; i++) {
            bool done = i < stage || (i == 5 && HotelEmpireProgress.HotelCount > 1);
            AddInfoCard((done ? L("DONE · ", "已完成 · ") : (i == stage ? L("NOW · ", "当前 · ") : L("LATER · ", "后续 · "))) + JourneyTitle(i),
                done ? L("Completed · permanently recorded", "已完成 · 已记入成长档案")
                     : i == stage ? JourneyHint(i) : L("A later milestone", "后续成长目标"),
                done ? Teal : i == stage ? Gold : Slate, i == stage ? 102f : 68f);
        }
    }

    private void OpenJourneyObjective()
    {
        if (Sim == null) return;
        int stage = Sim.Journey.Stage;
        if (stage == 2 || stage == 3) {
            for (int i = 0; i < Sim.Rooms.Count; i++) {
                var room = Sim.Rooms.Peek(i);
                bool candidate = stage == 2 ? room.state == RoomSimState.Ruined
                    : room.state == RoomSimState.Ready || room.state == RoomSimState.Dirty;
                if (!candidate || Sim.Renovations.IsRenovating(room.number)) continue;
                RoomSelection.Select(room.number);
                FocusRoom(room.number);
                _tab = DeskTab.Assets;
                SetDrawerOpen(true);
                ShowToast(JourneyHint(stage));
                return;
            }
        }
        _tab = stage == 5 ? DeskTab.Assets : DeskTab.Overview;
        SetDrawerOpen(true);
        ShowToast(JourneyHint(stage));
    }

    private void TickJourneyPresentation()
    {
        var state = Sim.Journey.State;
        if (_seenRestorations >= 0 && state.restored.Count > _seenRestorations) {
            int room = state.restored[state.restored.Count - 1];
            FindRoomBinder(room)?.Pulse(3f);
            FloatingTextFx.Spawn(RoomWorldPosition(room) + Vector3.up * 1.5f,
                "ROOM RESTORED! +1 ROOM", TealSoft, 3f);
            ShowMoneyBurst(L("ROOM ", "房间 ") + room + L(" IS BACK!", " 重获新生！"), Gold);
            ShowToast(L("Restoration finished. Housekeeping will clean and inspect it before sale.",
                "房间恢复完成！客房部会继续清洁验房，完成后即可接客。"));
        }
        if (_seenRenovations >= 0 && state.renovated.Count > _seenRenovations) {
            int room = state.renovated[state.renovated.Count - 1];
            FindRoomBinder(room)?.Pulse(3f);
            ShowMoneyBurst(L("RENOVATION COMPLETE · ", "装修交付 · ") + room, Gold);
            ShowToast(L("New furniture delivered. Clean the room, then review its price band.",
                "新家具已交付；清洁完成后，可按实际品质调整挂牌档。"));
        }
        if (_seenJourneyStage >= 0 && Sim.Journey.Stage > _seenJourneyStage)
            ShowToast(L("MILESTONE COMPLETE! Next: ", "阶段目标完成！下一步：") + JourneyTitle(Sim.Journey.Stage));
        _seenJourneyStage = Sim.Journey.Stage;
        _seenRestorations = state.restored.Count;
        _seenRenovations = state.renovated.Count;
    }

    private void AddMorningDiagnosis()
    {
        var bad = Sim.Breakdown.Ranked(false);
        var good = Sim.Breakdown.Ranked(true);
        string message = bad.Count > 0
            ? GameText.T(ReputationBreakdown.LabelOf(bad[0].cause))
                + " · " + bad[0].total.ToString("0.00") + "\n" + AdviceFor(bad[0].cause)
            : L("No recorded rating penalties. Keep your current service coverage.",
                "昨日没有记录到评分扣分项，继续保持服务覆盖。");
        if (good.Count > 0) message += "\n" + L("What worked: ", "有效决策：")
            + GameText.T(ReputationBreakdown.LabelOf(good[0].cause));
        AddMorningInfo(L("WHY IT HAPPENED · WHAT TO CHANGE", "经营归因 · 明天怎么改"), message, Gold, 116f);
    }

    private string AdviceFor(ReputationCause cause)
    {
        switch (cause) {
            case ReputationCause.QueueWait: return L("Cover reception before the arrival rush.", "在入住高峰前安排足够的前台人手。");
            case ReputationCause.BandVsDelivered:
            case ReputationCause.SegmentStandards:
            case ReputationCause.PriceExpectation: return L("Improve delivered quality or lower the promise made by your rate.", "提高实际品质，或降低价格与挂牌档承诺。");
            case ReputationCause.FlawedRoom: return L("Improve cleaning quality and inspection coverage.", "改善清洁质量与验房覆盖。");
            default: return L("Review guest claims and room condition before expanding.", "扩张前先检查退款、超售与房间状态。");
        }
    }
}
