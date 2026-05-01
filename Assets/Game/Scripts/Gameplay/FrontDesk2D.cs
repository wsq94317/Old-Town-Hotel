using UnityEngine;

// 轻量前台压力原型。
// 它不创建真实客人队列，只观察 Active Demand 等待时间，并把等待过久记为 delayed check-in。
public class FrontDesk2D : MonoBehaviour
{
    [Header("References")]
    public bool autoFindReferences = true;
    public Room2DPrototypeDemandLoop demandLoop;

    [Header("Waiting Pressure")]
    // Active Demand 等待超过这个时间后，就算前台出现延迟入住压力。
    public float delayedCheckInThresholdSeconds = 6f;
    public int pressurePenaltyScore = -1;

    [Header("Runtime State")]
    public int currentQueueCount;
    public float waitingTimePressureSeconds;
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
        TickFrontDeskPressure();
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
            return;
        }

        if (!demandLoop.activeDemandWaitingForManualAssignment)
        {
            currentQueueCount = 0;
            waitingTimePressureSeconds = 0f;
            delayRecordedForCurrentDemand = false;
            return;
        }

        currentQueueCount = 1;
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
            + "Delayed Check-ins: " + totalDelayedCheckIns + "\n"
            + "Last: " + lastFrontDeskResult;
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
