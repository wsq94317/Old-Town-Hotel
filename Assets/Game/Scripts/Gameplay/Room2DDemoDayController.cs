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

    [Header("Demo Timing (legacy)")]
    // day-cycle v2：日终由 GameClock 22:00 里程碑驱动（见 TickClock），
    // autoEndOperatingPeriod / operatingDurationSeconds 不再参与日终判定，仅保留序列化兼容。
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

    // ── 游戏内时钟（day-cycle v2）────────────────────────────────────────────
    [Header("Game Clock (day-cycle v2)")]
    [SerializeField] private Room2DBalanceConfigSO balanceConfig;
    private GameClock _clock;
    private bool _morningWaveFired;
    private bool _daySettled;

    /// <summary>游戏内时钟（惰性创建；config 缺省时用硬编码默认值）。</summary>
    public GameClock Clock { get { EnsureClock(); return _clock; } }

    private float DayLengthRealSeconds => balanceConfig != null ? balanceConfig.dayLengthRealSeconds : 180f;
    private float DayStartHour => balanceConfig != null ? balanceConfig.dayStartHour : 8f;
    private float OpenDoorsHour => balanceConfig != null ? balanceConfig.openDoorsHour : 10f;
    private float StopArrivalsHour => balanceConfig != null ? balanceConfig.stopArrivalsHour : 18f;
    private float CloseHour => balanceConfig != null ? balanceConfig.closeHour : 22f;

    private void EnsureClock()
    {
        if (_clock == null) _clock = new GameClock(DayLengthRealSeconds, DayStartHour, CloseHour);
    }

    /// <summary>EditMode 测试接缝：完成 Start() 里做的事件订阅 + 时钟接线。</summary>
    public void WireForTesting(Room2DDayPhaseStateMachine stateMachine)
    {
        phaseStateMachine = stateMachine;
        phaseStateMachine.OnPhaseEntered += HandlePhaseEntered;
        EnsureClock();
        phaseStateMachine.TimeLabelProvider = () => _clock.CurrentTimeFormatted;
    }

    private void Start()
    {
        FindReferencesIfNeeded();

        // 订阅状态机事件，让 HUD 按钮触发的阶段推进也能运行对应副作用。
        if (phaseStateMachine != null)
        {
            phaseStateMachine.OnPhaseEntered += HandlePhaseEntered;
            // 顶栏 wall-clock 走真实时钟（day-cycle v2）。
            EnsureClock();
            phaseStateMachine.TimeLabelProvider = () => _clock.CurrentTimeFormatted;
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
        TickClock(Time.deltaTime);
    }

    /// <summary>时钟推进 + 里程碑检查。Update 的可测试化身；EditMode 测试直接调用。</summary>
    public void TickClock(float deltaSeconds)
    {
        if (currentPhase == DemoDayPhase.Ended) return;
        EnsureClock();

        // 早晨退房潮：延迟到日内第一次 tick 触发，确保 boot 时 SaveCoordinator
        // 的占用恢复（Start 阶段）先于退房潮执行。
        if (!_morningWaveFired)
        {
            _morningWaveFired = true;
            if (demandLoop != null) demandLoop.BeginMorningCheckoutWave();
        }

        _clock.Advance(deltaSeconds);
        if (currentPhase == DemoDayPhase.Operating) operatingTimerSeconds += deltaSeconds; // 遗留 HUD 文本仍读它

        if (phaseStateMachine == null) return;
        var phase = phaseStateMachine.CurrentPhase;

        if (phase == Room2DDayPhaseStateMachine.Room2DDayPhase.Preparation
            && _clock.HasReachedHour(OpenDoorsHour))
        {
            StartOperatingPeriod(); // 10:00 开门迎客
            return;
        }

        if (phase == Room2DDayPhaseStateMachine.Room2DDayPhase.CheckInPeak
            && _clock.HasReachedHour(StopArrivalsHour))
        {
            phaseStateMachine.RequestAdvancePhase(); // 18:00 → Recovery（副作用走 HandlePhaseEntered）
            return;
        }

        if (phase == Room2DDayPhaseStateMachine.Room2DDayPhase.Recovery && _clock.DayEndReached)
        {
            EndDemoDay(); // 22:00 自动日结
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
                SetAcceptingNewGuests(true);
                SetFrontDeskRunning(true);
                SetLoungeRunning(true);
                if (demandLoop != null && demandLoop.useUpcomingDemandPreview)
                {
                    demandLoop.ScheduleUpcomingDemandPreview();
                }
                RefreshOverview();
                break;

            case Room2DDayPhaseStateMachine.Room2DDayPhase.Recovery:
                // 18:00 打烊前清尾：不收新客，送走等待中的客人；员工继续打扫。
                lastDemoAction = "Recovery - doors closed to new arrivals";
                SetAcceptingNewGuests(false);
                if (demandLoop != null) demandLoop.ClearWaitingGuestsForClosing();
                break;

            case Room2DDayPhaseStateMachine.Room2DDayPhase.Ended:
                // HUD "End Day" 按钮 或 ForceJumpToEnded 触发：执行结束副作用。
                lastDemoAction = "Demo day ended";
                SetDemandRunning(false);
                SetFrontDeskRunning(false);
                SetLoungeRunning(false);
                CollapseRoomWorkForDayEnd();
                if (demandLoop != null)
                {
                    demandLoop.RefreshPrototypeDaySummary();
                }
                RefreshOverview();
                SettleEconomicDay();   // settle-once guard 保证与 EndDemoDay 路径不重复结算
                break;
        }
    }

    // ── 遗留公开方法（保持签名不变） ─────────────────────────────────────────

    [ContextMenu("Enter Preparation Phase")]
    public void EnterPreparationPhase()
    {
        EnsureClock();
        _clock.ResetToDayStart();
        _morningWaveFired = false;
        _daySettled = false;
        operatingTimerSeconds = 0f;
        lastDemoAction = "Preparation started";

        // 副作用先于状态机调用，HandlePhaseEntered 将跳过重复处理。
        _sideEffectsHandledByLegacyMethod = true;
        SetDemandRunning(true);        // 退房潮/在场客人照常运转……
        SetAcceptingNewGuests(false);  // ……但 10:00 开门前不收新客
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
        SetAcceptingNewGuests(true);   // 10:00 开门迎客——从现在起才收新客
        SetFrontDeskRunning(true);
        SetLoungeRunning(true);

        if (demandLoop != null)
        {
            if (demandLoop.useUpcomingDemandPreview) demandLoop.ScheduleUpcomingDemandPreview();
            // 早晨退房潮已移到 Preparation（TickClock 首帧触发），此处不再重复。
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

        CollapseRoomWorkForDayEnd();

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

    // 22:00 收尾：当班工序自然完成——打扫中的完成到待检，检查中的完成放行；
    // Dirty / AwaitingInspection / Occupied / Ready / Blocked 原样过夜，任何房态都不卡死。
    // （BossCover 内部包的也是 Housekeeper2D，一并覆盖。）
    private void CollapseRoomWorkForDayEnd()
    {
        foreach (var hsk in FindObjectsByType<Housekeeper2D>(FindObjectsSortMode.None))
        {
            if (hsk != null && hsk.IsWorkingOnRoom()) hsk.FinishCurrentRoom();
        }
        foreach (var insp in FindObjectsByType<Inspector2D>(FindObjectsSortMode.None))
        {
            if (insp != null && insp.IsWorkingOnRoom()) insp.FinishCurrentRoom();
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
        if (_daySettled) return;   // 日终统一收口：无论从哪条路径进 Ended，只结算一次
        _daySettled = true;
        int served = demandLoop != null ? demandLoop.successfulDemandCount : 0;
        // 房费以退房结算为准（过夜模型）；按人头的兜底路径会让过夜客人被收两次。
        bool checkoutRevenueLive = demandLoop != null && demandLoop.economySystem != null;
        LastDayLedger = economy.CloseEconomicDay(checkoutRevenueLive ? 0 : served);
        playerCash = economy.Cash;
        OnDaySettled?.Invoke(demoDayIndex, served, LastDayLedger);
    }

    [ContextMenu("Restart Demo Day")]
    public void RestartDemoDay()
    {
        demoDayIndex++;
        ResetPressureSystemsForNewDemoDay();
        if (demandLoop != null) demandLoop.successfulDemandCount = 0; // 每日入住数，勿跨天累加
        EnterPreparationPhase();
        lastDemoAction = "Demo day restarted";
    }

    // ── 文本辅助方法（签名不变） ──────────────────────────────────────────────

    public string GetDemoDaySummaryText()
    {
        return "[Demo Day]\n"
            + "Day: " + demoDayIndex + "\n"
            + "Phase: " + currentPhase + "\n"
            + "Time: " + Clock.CurrentTimeFormatted + "\n"
            + GetFrontDeskLine() + "\n"
            + GetLoungeLine() + "\n"
            + "Status: " + GetPhaseHint() + "\n"
            + "Last: " + lastDemoAction;
    }

    public string GetCompactDemoDayText()
    {
        return "Demo: " + currentPhase + " " + Clock.CurrentTimeFormatted;
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
            + "Time: " + Clock.CurrentTimeFormatted + "\n"
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

    // day-cycle v2：新客闸门——只有 CheckInPeak（10:00-18:00）收新客。
    private void SetAcceptingNewGuests(bool accepting)
    {
        if (demandLoop != null) demandLoop.acceptingNewGuests = accepting;
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
