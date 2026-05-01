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
            + "Status: " + GetPhaseHint() + "\n"
            + "Last: " + lastDemoAction;
    }

    public string GetCompactDemoDayText()
    {
        return "Demo: " + currentPhase
            + " " + FormatSeconds(operatingTimerSeconds)
            + "/" + FormatSeconds(operatingDurationSeconds);
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
