using UnityEngine;

// 轻量 Lounge 支援压力原型。
// 只模拟干净杯子、脏杯子、牛奶和茶咖库存，不做完整库存/采购/员工路线系统。
public class Lounge2D : MonoBehaviour
{
    [Header("References")]
    public bool autoFindReferences = true;
    public Room2DPrototypeDemandLoop demandLoop;

    [Header("Resources")]
    public int cleanCups = 8;
    public int dirtyCups;
    public int milkStock = 12;
    public int teaCoffeeStock = 12;

    [Header("Service")]
    public bool runDuringPlay = true;
    public float serviceIntervalSeconds = 7f;
    public float serviceTimerSeconds;
    public int servedDrinkCount;
    public int missedServiceCount;

    [Header("Washing")]
    public bool washing;
    public float washDurationSeconds = 5f;
    public float washTimerSeconds;
    public int cupsInWashing;
    public int maxCupsPerWash = 4;

    [Header("Warnings")]
    public int lowCleanCupThreshold = 2;
    public int lowStockThreshold = 2;
    public int loungeWarningCount;
    public int pressurePenaltyScore = -1;
    public bool applyWarningPressure = true;
    public int lowCleanCupPenaltyScore = -1;
    public int lowStockPenaltyScore = -1;
    public int lowCleanCupWarningPressureCount;
    public int lowStockWarningPressureCount;
    public string loungeWarning = "None";
    public string lastLoungeResult = "None";

    private bool lowCleanCupPressureRecorded;
    private bool lowStockPressureRecorded;

    private void Start()
    {
        FindReferencesIfNeeded();
        RefreshWarning();
    }

    private void Update()
    {
        FindReferencesIfNeeded();

        if (runDuringPlay)
        {
            TickServiceDemand();
        }

        TickWashing();
        RefreshWarning();
    }

    [ContextMenu("Serve One Lounge Demand")]
    public void ServeOneLoungeDemand()
    {
        if (cleanCups <= 0)
        {
            RecordLoungeProblem("No clean cups");
            return;
        }

        if (milkStock <= 0 || teaCoffeeStock <= 0)
        {
            RecordLoungeProblem("Stock too low");
            return;
        }

        cleanCups--;
        dirtyCups++;
        milkStock--;
        teaCoffeeStock--;
        servedDrinkCount++;
        lastLoungeResult = "Served lounge drink";
        RefreshWarning();
    }

    [ContextMenu("Start Washing Cups")]
    public void StartWashingCups()
    {
        if (washing)
        {
            lastLoungeResult = "Wash already running";
            return;
        }

        if (dirtyCups <= 0)
        {
            lastLoungeResult = "No dirty cups to wash";
            return;
        }

        cupsInWashing = Mathf.Min(dirtyCups, Mathf.Max(1, maxCupsPerWash));
        dirtyCups -= cupsInWashing;
        washTimerSeconds = 0f;
        washing = true;
        lastLoungeResult = "Washing " + cupsInWashing + " cups";
    }

    [ContextMenu("Restock Prototype Lounge")]
    public void RestockPrototypeLounge()
    {
        milkStock = Mathf.Max(milkStock, 12);
        teaCoffeeStock = Mathf.Max(teaCoffeeStock, 12);
        lastLoungeResult = "Prototype restock";
        RefreshWarning();
    }

    [ContextMenu("Reset Prototype Lounge")]
    public void ResetPrototypeLounge()
    {
        cleanCups = 8;
        dirtyCups = 0;
        milkStock = 12;
        teaCoffeeStock = 12;
        serviceTimerSeconds = 0f;
        servedDrinkCount = 0;
        missedServiceCount = 0;
        washing = false;
        washTimerSeconds = 0f;
        cupsInWashing = 0;
        loungeWarningCount = 0;
        lowCleanCupWarningPressureCount = 0;
        lowStockWarningPressureCount = 0;
        lowCleanCupPressureRecorded = false;
        lowStockPressureRecorded = false;
        loungeWarning = "None";
        lastLoungeResult = "Reset prototype lounge";
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

    private void TickServiceDemand()
    {
        serviceTimerSeconds += Time.deltaTime;
        if (serviceTimerSeconds < serviceIntervalSeconds)
        {
            return;
        }

        serviceTimerSeconds = 0f;
        ServeOneLoungeDemand();
    }

    private void TickWashing()
    {
        if (!washing)
        {
            return;
        }

        washTimerSeconds += Time.deltaTime;
        if (washTimerSeconds < washDurationSeconds)
        {
            return;
        }

        cleanCups += cupsInWashing;
        lastLoungeResult = "Washed " + cupsInWashing + " cups";
        cupsInWashing = 0;
        washTimerSeconds = 0f;
        washing = false;
        RefreshWarning();
    }

    private void RecordLoungeProblem(string reason)
    {
        missedServiceCount++;
        loungeWarningCount++;
        lastLoungeResult = "Lounge problem: " + reason;
        loungeWarning = reason;

        if (demandLoop != null)
        {
            demandLoop.ApplyPrototypeServicePressure(lastLoungeResult, pressurePenaltyScore);
        }
    }

    private void RefreshWarning()
    {
        if (cleanCups <= lowCleanCupThreshold)
        {
            loungeWarning = "Low clean cups";
            RecordLowCleanCupPressureIfNeeded();
            return;
        }

        lowCleanCupPressureRecorded = false;

        if (milkStock <= lowStockThreshold || teaCoffeeStock <= lowStockThreshold)
        {
            loungeWarning = "Low lounge stock";
            RecordLowStockPressureIfNeeded();
            return;
        }

        lowStockPressureRecorded = false;

        if (washing)
        {
            loungeWarning = "Cups washing";
            return;
        }

        loungeWarning = "None";
    }

    private void RecordLowCleanCupPressureIfNeeded()
    {
        if (!ShouldRecordWarningPressure() || lowCleanCupPressureRecorded)
        {
            return;
        }

        lowCleanCupPressureRecorded = true;
        lowCleanCupWarningPressureCount++;
        loungeWarningCount++;
        lastLoungeResult = "Lounge warning: low clean cups";

        if (demandLoop != null)
        {
            demandLoop.ApplyPrototypeServicePressure(lastLoungeResult, lowCleanCupPenaltyScore);
        }
    }

    private void RecordLowStockPressureIfNeeded()
    {
        if (!ShouldRecordWarningPressure() || lowStockPressureRecorded)
        {
            return;
        }

        lowStockPressureRecorded = true;
        lowStockWarningPressureCount++;
        loungeWarningCount++;
        lastLoungeResult = "Lounge warning: low stock";

        if (demandLoop != null)
        {
            demandLoop.ApplyPrototypeServicePressure(lastLoungeResult, lowStockPenaltyScore);
        }
    }

    private bool ShouldRecordWarningPressure()
    {
        return applyWarningPressure && runDuringPlay && demandLoop != null;
    }

    public string GetLoungeSummaryText()
    {
        return "[Lounge]\n"
            + "Clean Cups: " + cleanCups + "\n"
            + "Dirty Cups: " + dirtyCups + "\n"
            + "Milk: " + milkStock + "\n"
            + "Tea/Coffee: " + teaCoffeeStock + "\n"
            + "Washing: " + GetWashingText() + "\n"
            + "Served/Missed: " + servedDrinkCount + " / " + missedServiceCount + "\n"
            + "Warnings: " + loungeWarningCount
            + " (Cup " + lowCleanCupWarningPressureCount
            + " / Stock " + lowStockWarningPressureCount + ")\n"
            + "Warning: " + loungeWarning + "\n"
            + "Last: " + lastLoungeResult;
    }

    private string GetWashingText()
    {
        if (!washing)
        {
            return "No";
        }

        return cupsInWashing + " cups " + FormatSeconds(washTimerSeconds)
            + " / " + FormatSeconds(washDurationSeconds);
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
