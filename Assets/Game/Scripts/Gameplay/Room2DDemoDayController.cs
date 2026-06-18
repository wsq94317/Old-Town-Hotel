using UnityEngine;

// 垂直切片 Demo Day 控制器。
// 它只负责把现有原型组织成一轮可录屏的流程：准备 -> 营业 -> 结束。
//
// B+A 混合冲刺 Story 1 改造说明：
//   - 新增 Room2DDayPhaseStateMachine 作为实际状态持有者。
//   - 遗留三态枚举 DemoDayPhase 保持不变，供 DebugHud / ShowcaseViewController 继续使用。
//   - currentPhase 从公开字段改为只读属性，映射自 phaseStateMachine.CurrentPhase：
//       Preparation → DemoDayPhase.Preparation
//       CheckInPeak → DemoDayPhase.Operating
//       Recovery    → DemoDayPhase.Operating
//       Ended       → DemoDayPhase.Ended
//   - 遗留方法（EnterPreparationPhase / StartOperatingPeriod / EndDemoDay / RestartDemoDay）
//     保持公开签名不变，内部转发给状态机，并保留原有副作用（SetDemandRunning 等）。
//   - 新阶段（CheckInPeak / Recovery）通过 HUD 按钮触发时，副作用由 OnPhaseEntered 订阅驱动。
public class Room2DDemoDayController : MonoBehaviour
{
    // 遗留三态枚举，保持不变以兼容 DebugHud / ShowcaseViewController。
    public enum DemoDayPhase
    {
        Preparation,
        Operating,
        Ended
    }

    [Header("References")]
    public bool autoFindReferences = true;
    public Room2DPrototypeDemandLoop demandLoop;
    public FrontDesk2D frontDesk;
    public Lounge2D lounge;
    public Room2DOverview roomOverview;
    public EconomySystem economy;

    // 新增：四态状态机引用；autoFindReferences 逻辑自动查找，也可 Inspector 手动赋值。
    [SerializeField] private Room2DDayPhaseStateMachine phaseStateMachine;

    [Header("Demo Timing")]
    // 原型营业时长，到了以后自动进入 Ended，方便录制完整一轮。
    public bool startInPreparation = true;
    public bool autoEndOperatingPeriod = true;
    public float operatingDurationSeconds = 180f;
    public float operatingTimerSeconds;

    [Header("Runtime")]
    // currentPhase 现在是只读属性，映射自 phaseStateMachine；不再是可写字段。
    public DemoDayPhase currentPhase => MapToDemoDayPhase(
        phaseStateMachine != null
            ? phaseStateMachine.CurrentPhase
            : Room2DDayPhaseStateMachine.Room2DDayPhase.Preparation);

    public int demoDayIndex = 1;
    public string lastDemoAction = "None";

    // ── UI 只读访问器（ui-spec.md §6 / §3.2） ────────────────────────────────
    // 顶部状态栏使用这些 getter；它们不改变游戏语义，仅暴露既有字段供 UI 绑定。

    // 顶部状态栏 DAY 计数 — 直接映射现有 demoDayIndex（保持现有字段名，零行为变更）。
    public int CurrentDay => demoDayIndex;

    // 顶部状态栏 money — ui-spec.md §3.2 ratified Room2DDemoDayController 为 PlayerCash 所有者。
    // 这是唯一新增的后端字段；默认 2450 匹配 mockup 数值。
    [Header("Player Cash (UI top bar)")]
    [SerializeField] private int playerCash = 2450;

    // 公开只读 getter，UI 顶部状态栏绑定。
    public int PlayerCash => playerCash;

    private void Start()
    {
        FindReferencesIfNeeded();

        // 订阅状态机事件，让 HUD 按钮触发的阶段推进也能运行对应副作用。
        if (phaseStateMachine != null)
        {
            phaseStateMachine.OnPhaseEntered += HandlePhaseEntered;
        }

        if (startInPreparation)
        {
            EnterPreparationPhase();
        }
    }

    private void OnDestroy()
    {
        // 与 Start 订阅配对，防止场景重载时残留悬空委托引用。
        if (phaseStateMachine != null)
        {
            phaseStateMachine.OnPhaseEntered -= HandlePhaseEntered;
        }
    }

    private void Update()
    {
        FindReferencesIfNeeded();

        if (currentPhase != DemoDayPhase.Operating)
        {
            return;
        }

        operatingTimerSeconds += Time.deltaTime;
        if (autoEndOperatingPeriod && operatingTimerSeconds >= operatingDurationSeconds)
        {
            EndDemoDay();
        }
    }

    // 当状态机进入某阶段时（包括 HUD 按钮触发路径）执行副作用。
    // 遗留的 EnterPreparationPhase / StartOperatingPeriod / EndDemoDay 方法自己管理副作用，
    // 避免重复执行：它们先设置内部标志，再调用状态机。
    // 这里仅响应"由 HUD 按钮直接触发"的阶段变化（即不经过遗留方法的路径）。
    private bool _sideEffectsHandledByLegacyMethod = false;

    private void HandlePhaseEntered(Room2DDayPhaseStateMachine.Room2DDayPhase phase)
    {
        // 遗留方法已自行处理副作用时，跳过，防止重复执行。
        if (_sideEffectsHandledByLegacyMethod)
        {
            _sideEffectsHandledByLegacyMethod = false;
            return;
        }

        switch (phase)
        {
            case Room2DDayPhaseStateMachine.Room2DDayPhase.Preparation:
                // ResetToPreparation 路径：副作用由 EnterPreparationPhase 负责，此处无需重复。
                break;

            case Room2DDayPhaseStateMachine.Room2DDayPhase.CheckInPeak:
                // HUD "Start Operating" 按钮触发：执行营业副作用。
                operatingTimerSeconds = 0f;
                lastDemoAction = "Operating period started";
                SetDemandRunning(true);
                SetFrontDeskRunning(true);
                SetLoungeRunning(true);
                if (demandLoop != null && demandLoop.useUpcomingDemandPreview)
                {
                    demandLoop.ScheduleUpcomingDemandPreview();
                }
                RefreshOverview();
                break;

            case Room2DDayPhaseStateMachine.Room2DDayPhase.Recovery:
                // HUD "Begin Recovery" 按钮触发：当前无额外副作用（系统保持运行）。
                lastDemoAction = "Recovery phase started";
                break;

            case Room2DDayPhaseStateMachine.Room2DDayPhase.Ended:
                // HUD "End Day" 按钮 或 ForceJumpToEnded 触发：执行结束副作用。
                lastDemoAction = "Demo day ended";
                SetDemandRunning(false);
                SetFrontDeskRunning(false);
                SetLoungeRunning(false);
                if (demandLoop != null)
                {
                    demandLoop.RefreshPrototypeDaySummary();
                }
                RefreshOverview();
                break;
        }
    }

    // ── 遗留公开方法（保持签名不变） ─────────────────────────────────────────

    [ContextMenu("Enter Preparation Phase")]
    public void EnterPreparationPhase()
    {
        operatingTimerSeconds = 0f;
        lastDemoAction = "Preparation started";

        // 副作用先于状态机调用，HandlePhaseEntered 将跳过重复处理。
        _sideEffectsHandledByLegacyMethod = true;
        SetDemandRunning(false);
        SetFrontDeskRunning(false);
        SetLoungeRunning(false);
        RefreshOverview();

        if (phaseStateMachine != null)
        {
            phaseStateMachine.ResetToPreparation();
        }
    }

    [ContextMenu("Start Operating Period")]
    public void StartOperatingPeriod()
    {
        operatingTimerSeconds = 0f;
        lastDemoAction = "Operating period started";

        // 副作用先于状态机调用，HandlePhaseEntered 将跳过重复处理。
        _sideEffectsHandledByLegacyMethod = true;
        SetDemandRunning(true);
        SetFrontDeskRunning(true);
        SetLoungeRunning(true);

        if (demandLoop != null && demandLoop.useUpcomingDemandPreview)
        {
            demandLoop.ScheduleUpcomingDemandPreview();
        }

        RefreshOverview();

        // 如果状态机当前在 Preparation，推进到 CheckInPeak。
        if (phaseStateMachine != null &&
            phaseStateMachine.CurrentPhase == Room2DDayPhaseStateMachine.Room2DDayPhase.Preparation)
        {
            phaseStateMachine.RequestAdvancePhase();
        }
    }

    [ContextMenu("End Demo Day")]
    public void EndDemoDay()
    {
        lastDemoAction = "Demo day ended";

        // 副作用先于状态机调用，HandlePhaseEntered 将跳过重复处理。
        _sideEffectsHandledByLegacyMethod = true;
        SetDemandRunning(false);
        SetFrontDeskRunning(false);
        SetLoungeRunning(false);

        if (demandLoop != null)
        {
            demandLoop.RefreshPrototypeDaySummary();
        }

        RefreshOverview();

        SettleEconomicDay();

        // ForceJumpToEnded 绕过中间阶段，供遗留直接结束路径和自动计时器使用。
        if (phaseStateMachine != null)
        {
            phaseStateMachine.ForceJumpToEnded();
        }
    }

    // ── 经济结算（Phase 1） ───────────────────────────────────────────────────
    // 一天结束时：按当日成功服务客人数算收入、扣全员工资，结果写回 PlayerCash。
    public DayLedger LastDayLedger { get; private set; }

    // 日结完成事件：(day, servedGuests, ledger)。UI 层订阅以弹出 Day-End 损益。
    public event System.Action<int, int, DayLedger> OnDaySettled;

    private void SettleEconomicDay()
    {
        if (economy == null) return;
        int served = demandLoop != null ? demandLoop.successfulDemandCount : 0;
        LastDayLedger = economy.CloseEconomicDay(served);
        playerCash = economy.Cash;
        OnDaySettled?.Invoke(demoDayIndex, served, LastDayLedger);
    }

    [ContextMenu("Restart Demo Day")]
    public void RestartDemoDay()
    {
        demoDayIndex++;
        ResetPressureSystemsForNewDemoDay();
        EnterPreparationPhase();
        lastDemoAction = "Demo day restarted";
    }

    // ── 文本辅助方法（签名不变） ──────────────────────────────────────────────

    public string GetDemoDaySummaryText()
    {
        return "[Demo Day]\n"
            + "Day: " + demoDayIndex + "\n"
            + "Phase: " + currentPhase + "\n"
            + "Time: " + FormatSeconds(operatingTimerSeconds)
            + " / " + FormatSeconds(operatingDurationSeconds) + "\n"
            + GetFrontDeskLine() + "\n"
            + GetLoungeLine() + "\n"
            + "Status: " + GetPhaseHint() + "\n"
            + "Last: " + lastDemoAction;
    }

    public string GetCompactDemoDayText()
    {
        return "Demo: " + currentPhase
            + " " + FormatSeconds(operatingTimerSeconds)
            + "/" + FormatSeconds(operatingDurationSeconds);
    }

    // GetShowcasePhaseLabel 保持遗留三态映射，不改变 Room2DShowcaseViewController 行为。
    // 新阶段标签（Check-In Peak / Recovery / Day Ended）由 Room2DDayPhaseStateMachine 的 HUD 展示。
    public string GetShowcasePhaseLabel()
    {
        switch (currentPhase)
        {
            case DemoDayPhase.Operating:
                return "Operating";
            case DemoDayPhase.Ended:
                return "End Summary";
            default:
                return "Preparation";
        }
    }

    public string GetShowcaseFocusText()
    {
        return GetRecordingFocusText();
    }

    public string GetRecordingStatusText()
    {
        return "[Demo Flow]\n"
            + "Phase: " + currentPhase + "\n"
            + "Time: " + FormatSeconds(operatingTimerSeconds)
            + " / " + FormatSeconds(operatingDurationSeconds) + "\n"
            + "Focus: " + GetRecordingFocusText() + "\n"
            + GetFrontDeskLine() + "\n"
            + GetLoungeLine();
    }

    public string GetEndOfDayRecordingSummaryText()
    {
        RefreshFinalSummarySources();

        return "[End Of Day Summary]\n"
            + "Result: " + GetFinalResultLine() + "\n"
            + "Demand: " + GetDemandSummaryLine() + "\n"
            + "Match: " + GetMatchSummaryLine() + "\n"
            + "Front Desk: " + GetFrontDeskSummaryLine() + "\n"
            + "Lounge: " + GetLoungeSummaryLine() + "\n"
            + "Rooms: " + GetRoomBacklogSummaryLine() + "\n"
            + "Main Hint: " + GetFinalHintLine() + "\n"
            + "Next Run: " + GetNextRunAdviceLine();
    }

    // ── 私有辅助 ─────────────────────────────────────────────────────────────

    // 将四态枚举映射为遗留三态枚举，保持向后兼容。
    private static DemoDayPhase MapToDemoDayPhase(Room2DDayPhaseStateMachine.Room2DDayPhase phase)
    {
        switch (phase)
        {
            case Room2DDayPhaseStateMachine.Room2DDayPhase.CheckInPeak:
            case Room2DDayPhaseStateMachine.Room2DDayPhase.Recovery:
                return DemoDayPhase.Operating;
            case Room2DDayPhaseStateMachine.Room2DDayPhase.Ended:
                return DemoDayPhase.Ended;
            default:
                return DemoDayPhase.Preparation;
        }
    }

    private void FindReferencesIfNeeded()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (phaseStateMachine == null)
        {
            phaseStateMachine = FindFirstObjectByType<Room2DDayPhaseStateMachine>();
        }

        if (demandLoop == null)
        {
            demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        }

        if (lounge == null)
        {
            lounge = FindFirstObjectByType<Lounge2D>();
        }

        if (frontDesk == null)
        {
            frontDesk = FindFirstObjectByType<FrontDesk2D>();
        }

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }

        if (economy == null)
        {
            economy = FindFirstObjectByType<EconomySystem>();
        }
    }

    private void SetDemandRunning(bool shouldRun)
    {
        if (demandLoop != null)
        {
            demandLoop.runDuringPlay = shouldRun;
        }
    }

    private void SetFrontDeskRunning(bool shouldRun)
    {
        if (frontDesk != null)
        {
            frontDesk.runDuringPlay = shouldRun;
        }
    }

    private void SetLoungeRunning(bool shouldRun)
    {
        if (lounge != null)
        {
            lounge.runDuringPlay = shouldRun;
        }
    }

    private void RefreshOverview()
    {
        if (roomOverview != null)
        {
            roomOverview.RefreshSummary();
        }
    }

    private void ResetPressureSystemsForNewDemoDay()
    {
        if (frontDesk != null)
        {
            frontDesk.ResetPrototypeFrontDesk();
        }

        if (lounge != null)
        {
            lounge.ResetPrototypeLounge();
        }
    }

    private string GetFrontDeskLine()
    {
        if (frontDesk == null)
        {
            return "Front Desk: None";
        }

        return "Front Desk: Q" + frontDesk.currentQueueCount
            + " / Delay " + frontDesk.totalDelayedCheckIns;
    }

    private string GetLoungeLine()
    {
        if (lounge == null)
        {
            return "Lounge: None";
        }

        return "Lounge: Cups " + lounge.cleanCups
            + " / Warn " + lounge.loungeWarningCount
            + " / " + lounge.loungeWarning;
    }

    private string GetPhaseHint()
    {
        switch (currentPhase)
        {
            case DemoDayPhase.Operating:
                return "Handle demand, rooms, workers, front desk, and lounge.";
            case DemoDayPhase.Ended:
                return "Read summary and decide what went wrong.";
            default:
                return "Prepare rooms, reserve demand, and mark priorities.";
        }
    }

    private void RefreshFinalSummarySources()
    {
        if (demandLoop != null)
        {
            demandLoop.RefreshPrototypeDaySummary();
        }

        RefreshOverview();
    }

    private string GetRecordingFocusText()
    {
        switch (currentPhase)
        {
            case DemoDayPhase.Operating:
                return GetOperatingFocusText();
            case DemoDayPhase.Ended:
                return "Read the end summary.";
            default:
                return "Prepare: reserve, mark CLEAN PRIO, mark INSP PRIO.";
        }
    }

    private string GetOperatingFocusText()
    {
        if (demandLoop == null)
        {
            return "Run hotel operations.";
        }

        if (demandLoop.complaintWaitingForReassignment)
        {
            return "Resolve complaint reassignment.";
        }

        if (demandLoop.activeDemandWaitingForManualAssignment)
        {
            return "Assign the active demand.";
        }

        if (frontDesk != null && frontDesk.currentQueueCount > 0)
        {
            return "Reduce front desk waiting.";
        }

        if (lounge != null && lounge.loungeWarning != "None")
        {
            return "Fix lounge warning.";
        }

        return "Keep rooms turning over before demand arrives.";
    }

    private string GetFinalResultLine()
    {
        if (demandLoop == null)
        {
            return "No demand data";
        }

        return "Score " + demandLoop.prototypeSatisfactionScore
            + " / " + demandLoop.prototypeSatisfactionTrend;
    }

    private string GetDemandSummaryLine()
    {
        if (demandLoop == null)
        {
            return "None";
        }

        return "Total " + demandLoop.generatedDemandCount
            + ", Success " + demandLoop.successfulDemandCount
            + ", Unmet " + demandLoop.unmetDemandCount;
    }

    private string GetMatchSummaryLine()
    {
        if (demandLoop == null)
        {
            return "None";
        }

        return "G/N/P "
            + demandLoop.goodMatchCount + "/"
            + demandLoop.normalMatchCount + "/"
            + demandLoop.poorMatchCount;
    }

    private string GetFrontDeskSummaryLine()
    {
        if (frontDesk == null || demandLoop == null)
        {
            return "None";
        }

        return "Delay " + frontDesk.totalDelayedCheckIns
            + ", Complaints " + demandLoop.roomComplaintCount
            + ", Compensation " + demandLoop.compensationRequestCount;
    }

    private string GetLoungeSummaryLine()
    {
        if (lounge == null)
        {
            return "None";
        }

        return "Served " + lounge.servedDrinkCount
            + ", Missed " + lounge.missedServiceCount
            + ", Warnings " + lounge.loungeWarningCount;
    }

    private string GetRoomBacklogSummaryLine()
    {
        if (demandLoop == null)
        {
            return "None";
        }

        return "Dirty " + demandLoop.summaryDirtyCount
            + ", Inspect Wait " + demandLoop.summaryAwaitingInspectionCount
            + ", Oldest Dirty " + FormatSeconds(demandLoop.summaryOldestDirtySeconds);
    }

    private string GetFinalHintLine()
    {
        if (demandLoop == null)
        {
            return "No run data";
        }

        if (frontDesk != null && frontDesk.totalDelayedCheckIns > 0)
        {
            return "Front desk waited too long.";
        }

        if (demandLoop.roomComplaintCount > 0)
        {
            return "Room assignment caused complaints.";
        }

        if (lounge != null && (lounge.missedServiceCount > 0 || lounge.loungeWarningCount > 0))
        {
            return "Lounge support was neglected.";
        }

        return demandLoop.summaryStatusHint;
    }

    private string GetNextRunAdviceLine()
    {
        if (demandLoop == null)
        {
            return "Start operating and create demand.";
        }

        if (demandLoop.summaryDirtyCount >= 3)
        {
            return "Prioritize HSK earlier.";
        }

        if (demandLoop.summaryAwaitingInspectionCount >= 2)
        {
            return "Assign Inspector earlier.";
        }

        if (frontDesk != null && frontDesk.totalDelayedCheckIns > 0)
        {
            return "Prepare Ready rooms before ETA hits 0.";
        }

        if (lounge != null && lounge.loungeWarningCount > 0)
        {
            return "Wash cups before clean cups run out.";
        }

        return "Run looked stable.";
    }

    private string FormatSeconds(float seconds)
    {
        int wholeSeconds = Mathf.FloorToInt(seconds);
        int minutes = wholeSeconds / 60;
        int remainingSeconds = wholeSeconds % 60;

        if (minutes > 0)
        {
            return minutes + "m " + remainingSeconds + "s";
        }

        return remainingSeconds + "s";
    }
}
