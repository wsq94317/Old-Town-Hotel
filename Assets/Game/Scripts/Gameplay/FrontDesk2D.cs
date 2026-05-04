using UnityEngine;

// 轻量前台压力原型。
// 它不创建真实客人队列，只观察 Active Demand 等待时间，并把等待过久记为 delayed check-in。
public class FrontDesk2D : MonoBehaviour
{
    [Header("References")]
    public bool autoFindReferences = true;
    public Room2DPrototypeDemandLoop demandLoop;

    [Header("Waiting Pressure")]
    // Demo Day 控制器会在准备/结束阶段关闭它，避免结算时继续扣分。
    public bool runDuringPlay = true;

    // Active Demand 等待超过这个时间后，就算前台出现延迟入住压力。
    public float delayedCheckInThresholdSeconds = 6f;
    public int pressurePenaltyScore = -1;

    [Header("Runtime State")]
    public int currentQueueCount;
    public float waitingTimePressureSeconds;
    public float complaintWaitingPressureSeconds;
    public int totalDelayedCheckIns;
    public string lastFrontDeskResult = "None";

    private int observedActiveDemandId = -1;
    private bool delayRecordedForCurrentDemand;

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
        lastFrontDeskResult = "None";
        observedActiveDemandId = -1;
        delayRecordedForCurrentDemand = false;
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
            return;
        }

        currentQueueCount = 0;
        waitingTimePressureSeconds = 0f;
        complaintWaitingPressureSeconds = 0f;

        if (demandLoop.complaintWaitingForReassignment)
        {
            currentQueueCount++;
            complaintWaitingPressureSeconds = demandLoop.complaintReassignmentWaitSeconds;
        }

        if (!demandLoop.activeDemandWaitingForManualAssignment)
        {
            delayRecordedForCurrentDemand = false;
            return;
        }

        currentQueueCount++;
        waitingTimePressureSeconds = demandLoop.activeDemandWaitSeconds;

        int activeDemandId = demandLoop.activatedUpcomingDemandCount;
        if (activeDemandId != observedActiveDemandId)
        {
            observedActiveDemandId = activeDemandId;
            delayRecordedForCurrentDemand = false;
        }

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
            + "Wait Pressure: " + FormatSeconds(waitingTimePressureSeconds)
            + " / " + FormatSeconds(delayedCheckInThresholdSeconds) + "\n"
            + "Complaint Wait: " + FormatSeconds(complaintWaitingPressureSeconds) + "\n"
            + "Delayed Check-ins: " + totalDelayedCheckIns + "\n"
            + "Last: " + lastFrontDeskResult;
    }

    // Showcase UI 用：把前台压力整理成更像玩家能理解的短结论。
    public string GetShowcasePressureLabel()
    {
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
            return "Reassign the complaint guest first.";
        }

        if (currentQueueCount > 0)
        {
            return "Open a guest card and assign a room.";
        }

        return "Call the next guest when you are ready.";
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
