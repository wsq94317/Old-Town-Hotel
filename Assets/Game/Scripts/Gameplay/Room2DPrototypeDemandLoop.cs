using UnityEngine;

// 最小外部需求循环。
// 它不创建真实客人对象，只模拟“有人想入住”和“住满一段时间后退房”。
public class Room2DPrototypeDemandLoop : MonoBehaviour
{
    public enum Room2DDemandType
    {
        Normal,
        HighExpectation
    }

    public enum Room2DMatchQuality
    {
        PoorMatch,
        NormalMatch,
        GoodMatch
    }

    public enum Room2DOutcomeResult
    {
        Negative,
        Neutral,
        Positive
    }

    // 打开后，Play 模式会自动定期产生需求并处理 Occupied 房间退房。
    public bool runDuringPlay = true;

    // 自动寻找场景里的房间和总览，减少手动拖引用。
    public bool autoFindReferences = true;
    public Room2DEntity[] rooms;
    public Room2DOverview roomOverview;

    [Header("Demand")]
    // 每隔多少现实秒产生一个简单入住需求。
    public float demandIntervalSeconds = 8f;
    public float demandTimerSeconds;
    public bool alternateDemandTypes = true;
    public Room2DDemandType nextDemandType = Room2DDemandType.Normal;
    public int generatedDemandCount;
    public int successfulDemandCount;
    public int unmetDemandCount;

    [Header("Occupancy")]
    // Occupied 房间住满多少现实秒后自动退房，重新变成 Dirty。
    public float occupiedDurationSeconds = 20f;
    public int simulatedCheckoutCount;

    [Header("Debug")]
    public string lastDemandResult = "None";
    public string lastChangedRoomName = "None";
    public Room2DDemandType lastDemandType = Room2DDemandType.Normal;
    public Room2DMatchQuality lastMatchQuality = Room2DMatchQuality.NormalMatch;
    public string lastMatchQualityLabel = "Normal Match";
    public int lastCleanlinessSuitability;
    public int lastWearSuitability;

    [Header("Prototype Outcome")]
    // 原型满意度不是最终评分系统，只用来确认“匹配好坏会产生后果”。
    public int prototypeSatisfactionScore;
    public string prototypeSatisfactionTrend = "Neutral";
    public Room2DOutcomeResult lastOutcomeResult = Room2DOutcomeResult.Neutral;
    public string lastOutcomeLabel = "Neutral";
    public string lastOutcomeSummary = "None";
    public int goodMatchCount;
    public int normalMatchCount;
    public int poorMatchCount;
    public int positiveOutcomeCount;
    public int neutralOutcomeCount;
    public int negativeOutcomeCount;

    [Header("Prototype Day Summary")]
    // 原型日结摘要：不是最终报表，只用来判断一轮测试主要卡在哪里。
    public int summaryDirtyCount;
    public int summaryAwaitingInspectionCount;
    public float summaryOldestDirtySeconds;
    public string summaryStatusHint = "No demands yet";

    private void Start()
    {
        FindRoomsIfNeeded();
        RefreshPrototypeDaySummary();
    }

    private void Update()
    {
        if (!runDuringPlay)
        {
            return;
        }

        demandTimerSeconds += Time.deltaTime;
        if (demandTimerSeconds >= demandIntervalSeconds)
        {
            demandTimerSeconds = 0f;
            GenerateOneDemand();
        }

        ProcessOccupiedCheckouts();
        RefreshPrototypeDaySummary();
    }

    [ContextMenu("Find Rooms In Scene")]
    public void FindRoomsInScene()
    {
        rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
        SortRoomsByRoomNumber();

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }
    }

    [ContextMenu("Generate One Demand")]
    public void GenerateOneDemand()
    {
        Room2DDemandType demandType = nextDemandType;
        GenerateDemand(demandType);

        if (alternateDemandTypes)
        {
            nextDemandType = demandType == Room2DDemandType.Normal
                ? Room2DDemandType.HighExpectation
                : Room2DDemandType.Normal;
        }
    }

    [ContextMenu("Generate Normal Demand")]
    public void GenerateNormalDemandForTesting()
    {
        GenerateDemand(Room2DDemandType.Normal);
    }

    [ContextMenu("Generate High Expectation Demand")]
    public void GenerateHighExpectationDemandForTesting()
    {
        GenerateDemand(Room2DDemandType.HighExpectation);
    }

    private void GenerateDemand(Room2DDemandType demandType)
    {
        FindRoomsIfNeeded();
        generatedDemandCount++;
        lastDemandType = demandType;

        Room2DEntity readyRoom = FindBestReadyRoomForDemand(demandType);
        if (readyRoom == null)
        {
            unmetDemandCount++;
            lastDemandResult = demandType + " unmet: no Ready room";
            lastChangedRoomName = "None";
            lastMatchQuality = Room2DMatchQuality.PoorMatch;
            lastMatchQualityLabel = "No Match";
            lastCleanlinessSuitability = 0;
            lastWearSuitability = 0;
            RecordUnmetDemandOutcome(demandType, "No Ready room");
            RefreshPrototypeDaySummary();
            return;
        }

        Room2DMatchQuality matchQuality = EvaluateMatchQuality(readyRoom, demandType);
        lastMatchQuality = matchQuality;
        lastMatchQualityLabel = GetMatchDisplayName(matchQuality);
        lastCleanlinessSuitability = GetCleanlinessSuitability(readyRoom);
        lastWearSuitability = GetWearSuitability(readyRoom);

        // 使用 Room2DEntity 自己的入住 guard，避免把 Dirty / Blocked / Occupied 房间错误入住。
        if (!readyRoom.SimulateCheckIn())
        {
            unmetDemandCount++;
            lastDemandResult = demandType + " unmet: check-in guard blocked";
            lastChangedRoomName = readyRoom.roomName;
            lastMatchQualityLabel = "No Match";
            RecordUnmetDemandOutcome(demandType, "Check-in blocked");
            RefreshPrototypeDaySummary();
            return;
        }

        successfulDemandCount++;
        lastDemandResult = demandType + " -> " + readyRoom.roomName + " / " + lastMatchQualityLabel;
        lastChangedRoomName = readyRoom.roomName;
        RecordSuccessfulAssignmentOutcome(demandType, readyRoom, matchQuality);

        RefreshRoomVisual(readyRoom);
        RefreshOverview();
        RefreshPrototypeDaySummary();
    }

    [ContextMenu("Process Occupied Checkouts")]
    public void ProcessOccupiedCheckouts()
    {
        FindRoomsIfNeeded();

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null || room.currentState != Room2DState.Occupied)
            {
                continue;
            }

            if (room.stateElapsedSeconds < occupiedDurationSeconds)
            {
                continue;
            }

            // 使用 Room2DEntity 自己的退房 guard，退房成功后房间会进入 Dirty。
            if (room.SimulateCheckout())
            {
                simulatedCheckoutCount++;
                lastChangedRoomName = room.roomName;
                RefreshRoomVisual(room);
            }
        }

        RefreshOverview();
        RefreshPrototypeDaySummary();
    }

    [ContextMenu("Refresh Prototype Day Summary")]
    public void RefreshPrototypeDaySummary()
    {
        FindRoomsIfNeeded();

        summaryDirtyCount = 0;
        summaryAwaitingInspectionCount = 0;
        summaryOldestDirtySeconds = 0f;

        if (rooms != null)
        {
            for (int i = 0; i < rooms.Length; i++)
            {
                Room2DEntity room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                if (room.currentState == Room2DState.Dirty)
                {
                    summaryDirtyCount++;
                    summaryOldestDirtySeconds = Mathf.Max(summaryOldestDirtySeconds, room.stateElapsedSeconds);
                }
                else if (room.currentState == Room2DState.AwaitingInspection)
                {
                    summaryAwaitingInspectionCount++;
                }
            }
        }

        summaryStatusHint = BuildSummaryStatusHint();
    }

    public string GetPrototypeDaySummaryText()
    {
        RefreshPrototypeDaySummary();

        return "Day Summary\n"
            + "Total: " + generatedDemandCount + "\n"
            + "Success: " + successfulDemandCount + "\n"
            + "Unmet: " + unmetDemandCount + "\n"
            + "Match G/N/P: " + goodMatchCount + "/" + normalMatchCount + "/" + poorMatchCount + "\n"
            + "Out P/N/Neg: " + positiveOutcomeCount + "/" + neutralOutcomeCount + "/" + negativeOutcomeCount + "\n"
            + "Score: " + prototypeSatisfactionScore + " (" + prototypeSatisfactionTrend + ")\n"
            + "Dirty: " + summaryDirtyCount + "\n"
            + "Inspect Wait: " + summaryAwaitingInspectionCount + "\n"
            + "Oldest Dirty: " + FormatSeconds(summaryOldestDirtySeconds) + "\n"
            + "Hint: " + summaryStatusHint;
    }

    private void FindRoomsIfNeeded()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (rooms == null || rooms.Length == 0)
        {
            FindRoomsInScene();
        }

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }
    }

    private Room2DEntity FindBestReadyRoomForDemand(Room2DDemandType demandType)
    {
        Room2DEntity bestRoom = null;
        Room2DMatchQuality bestMatchQuality = Room2DMatchQuality.PoorMatch;
        int bestSuitabilityScore = -1;

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null || !rooms[i].CanSimulateCheckIn())
            {
                continue;
            }

            Room2DMatchQuality matchQuality = EvaluateMatchQuality(rooms[i], demandType);
            int suitabilityScore = GetCleanlinessSuitability(rooms[i]) + GetWearSuitability(rooms[i]);

            bool shouldUseRoom = bestRoom == null
                || matchQuality > bestMatchQuality
                || (matchQuality == bestMatchQuality && suitabilityScore > bestSuitabilityScore)
                || (matchQuality == bestMatchQuality && suitabilityScore == bestSuitabilityScore && rooms[i].roomNumber < bestRoom.roomNumber);

            if (shouldUseRoom)
            {
                bestRoom = rooms[i];
                bestMatchQuality = matchQuality;
                bestSuitabilityScore = suitabilityScore;
            }
        }

        return bestRoom;
    }

    private Room2DMatchQuality EvaluateMatchQuality(Room2DEntity room, Room2DDemandType demandType)
    {
        int cleanlinessSuitability = GetCleanlinessSuitability(room);
        int wearSuitability = GetWearSuitability(room);

        // HighExpectation 对清洁和老旧程度都更敏感；Normal 只要求不要太差。
        if (demandType == Room2DDemandType.HighExpectation)
        {
            if (cleanlinessSuitability >= 80 && wearSuitability >= 75)
            {
                return Room2DMatchQuality.GoodMatch;
            }

            if (cleanlinessSuitability >= 60 && wearSuitability >= 55)
            {
                return Room2DMatchQuality.NormalMatch;
            }

            return Room2DMatchQuality.PoorMatch;
        }

        if (cleanlinessSuitability >= 70 && wearSuitability >= 60)
        {
            return Room2DMatchQuality.GoodMatch;
        }

        if (cleanlinessSuitability >= 45 && wearSuitability >= 40)
        {
            return Room2DMatchQuality.NormalMatch;
        }

        return Room2DMatchQuality.PoorMatch;
    }

    private int GetCleanlinessSuitability(Room2DEntity room)
    {
        // 先用最少因素近似“干净感”：床、浴室、地板。
        return GetAverageCondition(room, Room2DAttributeType.Bed, Room2DAttributeType.Bathroom, Room2DAttributeType.Floor);
    }

    private int GetWearSuitability(Room2DEntity room)
    {
        // 先用最少因素近似“老旧感”：墙纸、地板、衣柜。
        return GetAverageCondition(room, Room2DAttributeType.Wallpaper, Room2DAttributeType.Floor, Room2DAttributeType.Wardrobe);
    }

    private int GetAverageCondition(Room2DEntity room, params Room2DAttributeType[] attributeTypes)
    {
        if (room == null || room.roomAttributes == null || room.roomAttributes.Length == 0)
        {
            // 没有生成属性时给中等偏上的默认值，避免原型直接全部 Poor。
            return 70;
        }

        int total = 0;
        int count = 0;

        for (int i = 0; i < room.roomAttributes.Length; i++)
        {
            Room2DAttribute attribute = room.roomAttributes[i];
            if (attribute == null || !IsTrackedAttributeType(attribute.type, attributeTypes))
            {
                continue;
            }

            total += attribute.condition;
            count++;
        }

        if (count == 0)
        {
            return 70;
        }

        return Mathf.RoundToInt((float)total / count);
    }

    private bool IsTrackedAttributeType(Room2DAttributeType type, Room2DAttributeType[] trackedTypes)
    {
        for (int i = 0; i < trackedTypes.Length; i++)
        {
            if (type == trackedTypes[i])
            {
                return true;
            }
        }

        return false;
    }

    private string GetMatchDisplayName(Room2DMatchQuality matchQuality)
    {
        switch (matchQuality)
        {
            case Room2DMatchQuality.GoodMatch:
                return "Good Match";
            case Room2DMatchQuality.PoorMatch:
                return "Poor Match";
            default:
                return "Normal Match";
        }
    }

    private void RecordSuccessfulAssignmentOutcome(Room2DDemandType demandType, Room2DEntity room, Room2DMatchQuality matchQuality)
    {
        Room2DOutcomeResult outcomeResult = GetOutcomeFromMatchQuality(matchQuality);

        IncrementMatchCount(matchQuality);
        RecordOutcome(outcomeResult, GetScoreDelta(outcomeResult));

        string roomName = room != null ? room.roomName : "None";
        lastOutcomeSummary = demandType + " / " + roomName + " / " + lastMatchQualityLabel + " -> " + lastOutcomeLabel;
    }

    private void RecordUnmetDemandOutcome(Room2DDemandType demandType, string reason)
    {
        // 没有 Ready 房时也算负面后果，因为玩家没有满足入住需求。
        RecordOutcome(Room2DOutcomeResult.Negative, -3);
        lastOutcomeSummary = demandType + " unmet / " + reason + " -> " + lastOutcomeLabel;
    }

    private Room2DOutcomeResult GetOutcomeFromMatchQuality(Room2DMatchQuality matchQuality)
    {
        switch (matchQuality)
        {
            case Room2DMatchQuality.GoodMatch:
                return Room2DOutcomeResult.Positive;
            case Room2DMatchQuality.PoorMatch:
                return Room2DOutcomeResult.Negative;
            default:
                return Room2DOutcomeResult.Neutral;
        }
    }

    private int GetScoreDelta(Room2DOutcomeResult outcomeResult)
    {
        switch (outcomeResult)
        {
            case Room2DOutcomeResult.Positive:
                return 2;
            case Room2DOutcomeResult.Negative:
                return -2;
            default:
                return 0;
        }
    }

    private void IncrementMatchCount(Room2DMatchQuality matchQuality)
    {
        switch (matchQuality)
        {
            case Room2DMatchQuality.GoodMatch:
                goodMatchCount++;
                break;
            case Room2DMatchQuality.PoorMatch:
                poorMatchCount++;
                break;
            default:
                normalMatchCount++;
                break;
        }
    }

    private void RecordOutcome(Room2DOutcomeResult outcomeResult, int scoreDelta)
    {
        lastOutcomeResult = outcomeResult;
        lastOutcomeLabel = GetOutcomeDisplayName(outcomeResult);
        prototypeSatisfactionScore += scoreDelta;
        prototypeSatisfactionTrend = GetSatisfactionTrendDisplayName();

        switch (outcomeResult)
        {
            case Room2DOutcomeResult.Positive:
                positiveOutcomeCount++;
                break;
            case Room2DOutcomeResult.Negative:
                negativeOutcomeCount++;
                break;
            default:
                neutralOutcomeCount++;
                break;
        }
    }

    private string GetOutcomeDisplayName(Room2DOutcomeResult outcomeResult)
    {
        switch (outcomeResult)
        {
            case Room2DOutcomeResult.Positive:
                return "Positive";
            case Room2DOutcomeResult.Negative:
                return "Negative";
            default:
                return "Neutral";
        }
    }

    private string GetSatisfactionTrendDisplayName()
    {
        if (prototypeSatisfactionScore > 0)
        {
            return "Positive";
        }

        if (prototypeSatisfactionScore < 0)
        {
            return "Negative";
        }

        return "Neutral";
    }

    private string BuildSummaryStatusHint()
    {
        if (generatedDemandCount == 0)
        {
            return "No demand tested yet";
        }

        if (unmetDemandCount > 0 && unmetDemandCount >= successfulDemandCount)
        {
            return "Main issue: unmet demand";
        }

        if (poorMatchCount > goodMatchCount && poorMatchCount > 0)
        {
            return "Main issue: poor room match";
        }

        if (summaryAwaitingInspectionCount >= 2)
        {
            return "Main issue: inspection backlog";
        }

        if (summaryDirtyCount >= 3 || summaryOldestDirtySeconds >= 60f)
        {
            return "Main issue: dirty backlog";
        }

        if (prototypeSatisfactionScore > 0)
        {
            return "Cycle looks healthy";
        }

        return "Cycle is stable";
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

    private void SortRoomsByRoomNumber()
    {
        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Length - 1; i++)
        {
            for (int j = i + 1; j < rooms.Length; j++)
            {
                if (GetRoomNumber(rooms[j]) < GetRoomNumber(rooms[i]))
                {
                    Room2DEntity tempRoom = rooms[i];
                    rooms[i] = rooms[j];
                    rooms[j] = tempRoom;
                }
            }
        }
    }

    private int GetRoomNumber(Room2DEntity room)
    {
        if (room != null)
        {
            return room.roomNumber;
        }

        return int.MaxValue;
    }

    private void RefreshRoomVisual(Room2DEntity room)
    {
        if (room == null)
        {
            return;
        }

        Room2DController controller = room.GetComponent<Room2DController>();
        if (controller != null)
        {
            controller.ApplyStateVisual();
        }
    }

    private void RefreshOverview()
    {
        if (roomOverview != null)
        {
            roomOverview.RefreshSummary();
        }
    }
}
