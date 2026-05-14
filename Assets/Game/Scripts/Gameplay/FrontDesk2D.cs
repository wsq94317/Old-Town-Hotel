using UnityEngine;

// 轻量前台压力原型。
// 它不创建真实客人队列，只观察 Active Demand 等待时间，并把等待过久记为 delayed check-in。
public class FrontDesk2D : MonoBehaviour
{
    public enum WaitingGuestPatienceState
    {
        Normal,
        Impatient,
        Critical
    }

    [Header("References")]
    public bool autoFindReferences = true;
    public Room2DPrototypeDemandLoop demandLoop;

    [Header("Waiting Pressure")]
    // Demo Day 控制器会在准备/结束阶段关闭它，避免结算时继续扣分。
    public bool runDuringPlay = true;

    // Active Demand 等待超过这个时间后，就算前台出现延迟入住压力。
    public float delayedCheckInThresholdSeconds = 6f;
    public int pressurePenaltyScore = -1;
    public int impatientPenaltyScore = -1;
    public int criticalPenaltyScore = -2;

    [Header("Guest Patience")]
    // 等待越久，前台越应该催促玩家去 Rooms 解决房间准备问题。
    public float impatientThresholdSeconds = 8f;
    public float criticalThresholdSeconds = 16f;

    [Header("Runtime State")]
    public int currentQueueCount;
    public float waitingTimePressureSeconds;
    public float complaintWaitingPressureSeconds;
    public int totalDelayedCheckIns;
    public int totalImpatientGuests;
    public int totalCriticalGuests;
    public WaitingGuestPatienceState activeGuestPatienceState = WaitingGuestPatienceState.Normal;
    public string activeGuestBlockerText = "No waiting guest";
    public string activeGuestNextActionText = "Call the next guest when ready.";
    public string lastFrontDeskResult = "None";

    private int observedActiveDemandId = -1;
    private bool delayRecordedForCurrentDemand;
    private WaitingGuestPatienceState highestPenalizedPatienceState = WaitingGuestPatienceState.Normal;

    // ── UI 只读访问器（ui-spec.md §6 / §3.2 顶部状态栏 mood %） ──────────────
    //
    // FrontDesk2D 自身并不持有满意度分数；分数的权威所有者是 Room2DPrototypeDemandLoop
    // 上的 prototypeSatisfactionScore（int）。ui-spec.md §6 仍将 FrontDesk2D 列为绑定
    // 路径，因此此处暴露一个直通 getter，零行为变更（不缓存、不偏移、不舍入）。
    // 当 demandLoop 引用未连接时返回 0，避免 UI 绑定时 NullReference。
    /// <summary>
    /// 顶部状态栏 mood % 数据源。直通 demand loop 的 prototypeSatisfactionScore。
    /// 类型为 int 与现有字段保持一致；如果 demandLoop 未连接返回 0。
    /// </summary>
    public int SatisfactionScore => demandLoop != null ? demandLoop.prototypeSatisfactionScore : 0;

    private void Start()
    {
        FindReferencesIfNeeded();
    }

    private void Update()
    {
        FindReferencesIfNeeded();

        if (!runDuringPlay)
        {
            return;
        }

        TickFrontDeskPressure();
    }

    [ContextMenu("Reset Prototype Front Desk")]
    public void ResetPrototypeFrontDesk()
    {
        currentQueueCount = 0;
        waitingTimePressureSeconds = 0f;
        complaintWaitingPressureSeconds = 0f;
        totalDelayedCheckIns = 0;
        totalImpatientGuests = 0;
        totalCriticalGuests = 0;
        activeGuestPatienceState = WaitingGuestPatienceState.Normal;
        activeGuestBlockerText = "No waiting guest";
        activeGuestNextActionText = "Call the next guest when ready.";
        lastFrontDeskResult = "None";
        observedActiveDemandId = -1;
        delayRecordedForCurrentDemand = false;
        highestPenalizedPatienceState = WaitingGuestPatienceState.Normal;
    }

    private void FindReferencesIfNeeded()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (demandLoop == null)
        {
            demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        }
    }

    private void TickFrontDeskPressure()
    {
        if (demandLoop == null)
        {
            currentQueueCount = 0;
            waitingTimePressureSeconds = 0f;
            complaintWaitingPressureSeconds = 0f;
            activeGuestPatienceState = WaitingGuestPatienceState.Normal;
            activeGuestBlockerText = "No demand loop";
            activeGuestNextActionText = "Link the demand loop.";
            return;
        }

        currentQueueCount = 0;
        waitingTimePressureSeconds = 0f;
        complaintWaitingPressureSeconds = 0f;
        activeGuestPatienceState = WaitingGuestPatienceState.Normal;
        activeGuestBlockerText = "No waiting guest";
        activeGuestNextActionText = "Call the next guest when ready.";

        if (demandLoop.complaintWaitingForReassignment)
        {
            currentQueueCount++;
            complaintWaitingPressureSeconds = demandLoop.complaintReassignmentWaitSeconds;
            activeGuestPatienceState = GetPatienceState(complaintWaitingPressureSeconds);
            activeGuestBlockerText = "Complaint guest needs a new room";
            activeGuestNextActionText = HasReadyRoom()
                ? "Return to Front Desk: assign a Ready room."
                : "Go to Rooms and prepare a Ready room.";
        }

        if (!demandLoop.activeDemandWaitingForManualAssignment)
        {
            delayRecordedForCurrentDemand = false;
            return;
        }

        currentQueueCount++;
        waitingTimePressureSeconds = demandLoop.activeDemandWaitSeconds;
        activeGuestPatienceState = GetPatienceState(waitingTimePressureSeconds);
        activeGuestBlockerText = HasReadyRoom()
            ? "Ready room available"
            : "Room readiness is blocking check-in";
        activeGuestNextActionText = HasReadyRoom()
            ? "Return to Front Desk: assign/check in."
            : "Switch to Rooms: clean and inspect a room.";

        int activeDemandId = demandLoop.activatedUpcomingDemandCount;
        if (activeDemandId != observedActiveDemandId)
        {
            observedActiveDemandId = activeDemandId;
            delayRecordedForCurrentDemand = false;
            highestPenalizedPatienceState = WaitingGuestPatienceState.Normal;
        }

        RecordPatiencePressureIfNeeded();

        if (delayRecordedForCurrentDemand || waitingTimePressureSeconds < delayedCheckInThresholdSeconds)
        {
            return;
        }

        delayRecordedForCurrentDemand = true;
        totalDelayedCheckIns++;
        lastFrontDeskResult = "Late check-in pressure: "
            + demandLoop.activeDemandType
            + " waited " + FormatSeconds(waitingTimePressureSeconds);
        demandLoop.ApplyPrototypeServicePressure(lastFrontDeskResult, pressurePenaltyScore);
    }

    public string GetFrontDeskSummaryText()
    {
        return "[Front Desk]\n"
            + "Queue: " + currentQueueCount + "\n"
            + "Patience: " + GetShowcasePatienceLabel() + "\n"
            + "Wait Pressure: " + FormatSeconds(waitingTimePressureSeconds)
            + " / " + FormatSeconds(delayedCheckInThresholdSeconds) + "\n"
            + "Complaint Wait: " + FormatSeconds(complaintWaitingPressureSeconds) + "\n"
            + "Impatient / Critical: " + totalImpatientGuests + " / " + totalCriticalGuests + "\n"
            + "Delayed Check-ins: " + totalDelayedCheckIns + "\n"
            + "Last: " + lastFrontDeskResult;
    }

    // Showcase UI 用：把前台压力整理成更像玩家能理解的短结论。
    public string GetShowcasePressureLabel()
    {
        if (activeGuestPatienceState == WaitingGuestPatienceState.Critical)
        {
            return "Critical";
        }

        if (activeGuestPatienceState == WaitingGuestPatienceState.Impatient)
        {
            return "Impatient";
        }

        if (complaintWaitingPressureSeconds > 0f)
        {
            return "Complaint Waiting";
        }

        if (waitingTimePressureSeconds >= delayedCheckInThresholdSeconds)
        {
            return "Guest Delayed";
        }

        if (currentQueueCount > 0)
        {
            return "Guests Waiting";
        }

        return runDuringPlay ? "Stable" : "Standby";
    }

    public string GetShowcaseActionHint()
    {
        if (!runDuringPlay)
        {
            return "Start the day when rooms are ready.";
        }

        if (complaintWaitingPressureSeconds > 0f)
        {
            return activeGuestNextActionText;
        }

        if (currentQueueCount > 0)
        {
            return activeGuestNextActionText;
        }

        return "Call the next guest when you are ready.";
    }

    public string GetShowcasePatienceLabel()
    {
        switch (activeGuestPatienceState)
        {
            case WaitingGuestPatienceState.Critical:
                return "Critical";
            case WaitingGuestPatienceState.Impatient:
                return "Impatient";
            default:
                return currentQueueCount > 0 ? "Normal" : "None";
        }
    }

    public string GetShowcaseBlockerText()
    {
        return activeGuestBlockerText;
    }

    public string GetShowcaseConsequenceText()
    {
        if (string.IsNullOrEmpty(lastFrontDeskResult) || lastFrontDeskResult == "None")
        {
            return "No front desk penalty yet";
        }

        return lastFrontDeskResult;
    }

    private void RecordPatiencePressureIfNeeded()
    {
        // 每个等待客人只在进入 Impatient / Critical 时各扣一次分，避免每帧重复扣分。
        if ((int)activeGuestPatienceState <= (int)highestPenalizedPatienceState)
        {
            return;
        }

        if (activeGuestPatienceState == WaitingGuestPatienceState.Impatient)
        {
            highestPenalizedPatienceState = WaitingGuestPatienceState.Impatient;
            totalImpatientGuests++;
            lastFrontDeskResult = "Guest became impatient: "
                + demandLoop.activeDemandType
                + " waited " + FormatSeconds(waitingTimePressureSeconds);
            demandLoop.ApplyPrototypeServicePressure(lastFrontDeskResult, impatientPenaltyScore);
            return;
        }

        if (activeGuestPatienceState == WaitingGuestPatienceState.Critical)
        {
            if ((int)highestPenalizedPatienceState < (int)WaitingGuestPatienceState.Impatient)
            {
                totalImpatientGuests++;
            }

            highestPenalizedPatienceState = WaitingGuestPatienceState.Critical;
            totalCriticalGuests++;
            lastFrontDeskResult = "Guest became critical: "
                + demandLoop.activeDemandType
                + " waited " + FormatSeconds(waitingTimePressureSeconds);
            demandLoop.ApplyPrototypeServicePressure(lastFrontDeskResult, criticalPenaltyScore);
        }
    }

    private WaitingGuestPatienceState GetPatienceState(float waitingSeconds)
    {
        if (waitingSeconds >= criticalThresholdSeconds)
        {
            return WaitingGuestPatienceState.Critical;
        }

        if (waitingSeconds >= impatientThresholdSeconds)
        {
            return WaitingGuestPatienceState.Impatient;
        }

        return WaitingGuestPatienceState.Normal;
    }

    private bool HasReadyRoom()
    {
        if (demandLoop == null)
        {
            return false;
        }

        return demandLoop.HasReadyRoomForActiveFrontDeskDemand();
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
