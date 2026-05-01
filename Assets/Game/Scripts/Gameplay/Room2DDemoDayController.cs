using UnityEngine;

// 垂直切片 Demo Day 控制器。
// 它只负责把现有原型组织成一轮可录屏的流程：准备 -> 营业 -> 结束。
public class Room2DDemoDayController : MonoBehaviour
{
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

    [Header("Demo Timing")]
    // 原型营业时长，到了以后自动进入 Ended，方便录制完整一轮。
    public bool startInPreparation = true;
    public bool autoEndOperatingPeriod = true;
    public float operatingDurationSeconds = 180f;
    public float operatingTimerSeconds;

    [Header("Runtime")]
    public DemoDayPhase currentPhase = DemoDayPhase.Preparation;
    public int demoDayIndex = 1;
    public string lastDemoAction = "None";

    private void Start()
    {
        FindReferencesIfNeeded();

        if (startInPreparation)
        {
            EnterPreparationPhase();
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

    [ContextMenu("Enter Preparation Phase")]
    public void EnterPreparationPhase()
    {
        currentPhase = DemoDayPhase.Preparation;
        operatingTimerSeconds = 0f;
        lastDemoAction = "Preparation started";

        // 准备阶段允许玩家看房、预留、标记优先级，但不自动推进入住需求。
        SetDemandRunning(false);
        SetFrontDeskRunning(false);
        SetLoungeRunning(false);
        RefreshOverview();
    }

    [ContextMenu("Start Operating Period")]
    public void StartOperatingPeriod()
    {
        currentPhase = DemoDayPhase.Operating;
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
    }

    [ContextMenu("End Demo Day")]
    public void EndDemoDay()
    {
        currentPhase = DemoDayPhase.Ended;
        lastDemoAction = "Demo day ended";

        // 结束阶段冻结新需求和 Lounge 自动消耗，方便看 summary 和录屏结尾。
        SetDemandRunning(false);
        SetFrontDeskRunning(false);
        SetLoungeRunning(false);

        if (demandLoop != null)
        {
            demandLoop.RefreshPrototypeDaySummary();
        }

        RefreshOverview();
    }

    [ContextMenu("Restart Demo Day")]
    public void RestartDemoDay()
    {
        demoDayIndex++;
        ResetPressureSystemsForNewDemoDay();
        EnterPreparationPhase();
        lastDemoAction = "Demo day restarted";
    }

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
