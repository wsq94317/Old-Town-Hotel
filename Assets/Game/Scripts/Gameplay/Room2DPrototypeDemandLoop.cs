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
    public Room2DSelectionManager selectionManager;

    [Header("Demand")]
    // 每隔多少现实秒产生一个简单入住需求。
    public float demandIntervalSeconds = 8f;
    public float demandTimerSeconds;
    public bool alternateDemandTypes = true;
    public Room2DDemandType nextDemandType = Room2DDemandType.Normal;
    public int generatedDemandCount;
    public int successfulDemandCount;
    public int unmetDemandCount;

    [Header("Upcoming Demand Preview")]
    // 最小 ETA 原型：只预告下一个入住需求，不做完整客人列表或预分配。
    public bool useUpcomingDemandPreview = true;
    public Room2DDemandType upcomingDemandType = Room2DDemandType.Normal;
    public float upcomingDemandEtaSeconds;
    public string upcomingDemandPreviewText = "None";
    public string lastActivatedUpcomingDemandText = "None";
    public int activatedUpcomingDemandCount;

    [Header("Upcoming Demand Reservation")]
    // 当前 upcoming demand 只允许预留一间房；这不是完整预分配系统。
    public Room2DEntity reservedRoomForUpcomingDemand;
    public string reservedRoomName = "None";
    public string lastReservationResult = "None";

    [Header("Prototype Preparation")]
    // 最小准备面板数据：只记录玩家当前准备优先处理哪几间房，不做正式排班系统。
    public Room2DEntity priorityDirtyRoom;
    public string priorityDirtyRoomName = "None";
    public Room2DEntity priorityInspectionRoom;
    public string priorityInspectionRoomName = "None";
    public string lastPreparationAction = "None";
    public int preparationActionCount;

    [Header("Manual Active Demand Assignment")]
    // 最小手动分房原型：ETA 到 0 后先变成一个 active demand，等待玩家选 Ready 房。
    public bool useManualActiveDemand = true;
    public bool activeDemandWaitingForManualAssignment;
    public Room2DDemandType activeDemandType = Room2DDemandType.Normal;
    public float activeDemandWaitSeconds;
    public float manualAssignmentFallbackDelaySeconds = 8f;
    public Room2DEntity activeReservedRoomForFallback;
    public string activeReservedRoomName = "None";
    public string activeDemandStatus = "None";
    public string lastManualAssignmentResult = "None";
    public string lastAssignmentMode = "None";

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
        ScheduleUpcomingDemandPreview();
        RefreshPrototypeDaySummary();
    }

    private void Update()
    {
        if (!runDuringPlay)
        {
            return;
        }

        if (useUpcomingDemandPreview)
        {
            TickUpcomingDemandPreview();
        }
        else
        {
            demandTimerSeconds += Time.deltaTime;
            if (demandTimerSeconds >= demandIntervalSeconds)
            {
                demandTimerSeconds = 0f;
                GenerateOneDemand();
            }
        }

        TickActiveDemand();
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

        if (useUpcomingDemandPreview)
        {
            ScheduleUpcomingDemandPreview();
        }
    }

    [ContextMenu("Schedule Upcoming Demand Preview")]
    public void ScheduleUpcomingDemandPreview()
    {
        upcomingDemandType = nextDemandType;
        upcomingDemandEtaSeconds = Mathf.Max(0f, demandIntervalSeconds);
        upcomingDemandPreviewText = BuildUpcomingDemandPreviewText("Incoming");
    }

    [ContextMenu("Activate Upcoming Demand Now")]
    public void ActivateUpcomingDemandNow()
    {
        ActivateUpcomingDemand();
    }

    [ContextMenu("Reserve Selected Room For Upcoming Demand")]
    public void ReserveSelectedRoomForUpcomingDemand()
    {
        FindReferencesIfNeeded();

        if (selectionManager == null || selectionManager.selectedRoom == null)
        {
            lastReservationResult = "Reserve failed: no selected room";
            lastPreparationAction = lastReservationResult;
            return;
        }

        ReserveRoomForUpcomingDemand(selectionManager.selectedRoom.roomEntity);
    }

    [ContextMenu("Clear Upcoming Demand Reservation")]
    public void ClearUpcomingDemandReservation()
    {
        reservedRoomForUpcomingDemand = null;
        reservedRoomName = "None";
        lastReservationResult = "Reservation cleared";
        lastPreparationAction = lastReservationResult;
    }

    public void ReserveRoomForUpcomingDemand(Room2DEntity room)
    {
        if (room == null)
        {
            lastReservationResult = "Reserve failed: room is None";
            lastPreparationAction = lastReservationResult;
            return;
        }

        reservedRoomForUpcomingDemand = room;
        reservedRoomName = room.roomName;
        lastReservationResult = "Reserved " + room.roomName + " for " + upcomingDemandType;
        lastPreparationAction = lastReservationResult;
        preparationActionCount++;
    }

    [ContextMenu("Mark Selected Dirty Room As Priority")]
    public void MarkSelectedDirtyRoomAsPriority()
    {
        FindReferencesIfNeeded();

        Room2DEntity room = GetSelectedRoomEntity();
        if (room == null)
        {
            lastPreparationAction = "Dirty priority failed: no selected room";
            return;
        }

        if (!room.CanStartCleaning())
        {
            lastPreparationAction = "Dirty priority failed: " + room.roomName + " is " + room.GetStateDisplayName();
            return;
        }

        if (priorityDirtyRoom != null && priorityDirtyRoom != room)
        {
            priorityDirtyRoom.ClearPreparationPriority();
            RefreshRoomVisual(priorityDirtyRoom);
        }

        priorityDirtyRoom = room;
        priorityDirtyRoomName = room.roomName;
        room.MarkCleaningPriorityForPreparation();
        lastPreparationAction = "Dirty priority: " + room.roomName;
        preparationActionCount++;
        RefreshRoomVisual(room);
    }

    [ContextMenu("Mark Selected Inspection Room As Priority")]
    public void MarkSelectedInspectionRoomAsPriority()
    {
        FindReferencesIfNeeded();

        Room2DEntity room = GetSelectedRoomEntity();
        if (room == null)
        {
            lastPreparationAction = "Inspect priority failed: no selected room";
            return;
        }

        if (!room.CanApproveInspection())
        {
            lastPreparationAction = "Inspect priority failed: " + room.roomName + " is " + room.GetStateDisplayName();
            return;
        }

        if (priorityInspectionRoom != null && priorityInspectionRoom != room)
        {
            priorityInspectionRoom.ClearPreparationPriority();
            RefreshRoomVisual(priorityInspectionRoom);
        }

        priorityInspectionRoom = room;
        priorityInspectionRoomName = room.roomName;
        room.MarkInspectionPriorityForPreparation();
        lastPreparationAction = "Inspect priority: " + room.roomName;
        preparationActionCount++;
        RefreshRoomVisual(room);
    }

    [ContextMenu("Assign Selected Room To Active Demand")]
    public void AssignSelectedRoomToActiveDemand()
    {
        FindReferencesIfNeeded();

        if (selectionManager == null || selectionManager.selectedRoom == null)
        {
            lastManualAssignmentResult = "Manual failed: no selected room";
            return;
        }

        AssignRoomToActiveDemand(selectionManager.selectedRoom.roomEntity);
    }

    public bool AssignRoomToActiveDemand(Room2DEntity room)
    {
        if (!activeDemandWaitingForManualAssignment)
        {
            lastManualAssignmentResult = "Manual failed: no active demand";
            return false;
        }

        if (room == null)
        {
            lastManualAssignmentResult = "Manual failed: room is None";
            return false;
        }

        if (!room.CanSimulateCheckIn())
        {
            lastManualAssignmentResult = "Manual failed: " + room.roomName + " is " + room.GetStateDisplayName();
            return false;
        }

        lastManualAssignmentResult = "Manual assigned " + room.roomName;
        GenerateDemand(activeDemandType, null, false, room, "Manual");
        CompleteActiveDemand();
        return true;
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
        GenerateDemand(demandType, null, false, null, "Fallback");
    }

    private void GenerateDemand(Room2DDemandType demandType, Room2DEntity reservedRoom, bool useReservationFirst)
    {
        GenerateDemand(demandType, reservedRoom, useReservationFirst, null, useReservationFirst ? "Reservation/Fallback" : "Fallback");
    }

    private void GenerateDemand(
        Room2DDemandType demandType,
        Room2DEntity reservedRoom,
        bool useReservationFirst,
        Room2DEntity forcedRoom,
        string assignmentMode)
    {
        FindRoomsIfNeeded();
        generatedDemandCount++;
        lastDemandType = demandType;
        lastAssignmentMode = assignmentMode;

        Room2DEntity readyRoom = FindRoomForDemand(demandType, reservedRoom, useReservationFirst, forcedRoom);
        if (readyRoom == null)
        {
            unmetDemandCount++;
            lastDemandResult = assignmentMode + ": " + demandType + " unmet: no Ready room";
            lastChangedRoomName = "None";
            lastMatchQuality = Room2DMatchQuality.PoorMatch;
            lastMatchQualityLabel = "No Match";
            lastCleanlinessSuitability = 0;
            lastWearSuitability = 0;
            CompleteReservationResultAfterUnmet(reservedRoom, useReservationFirst);
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
            lastDemandResult = assignmentMode + ": " + demandType + " unmet: check-in guard blocked";
            lastChangedRoomName = readyRoom.roomName;
            lastMatchQualityLabel = "No Match";
            CompleteReservationResultAfterBlockedCheckIn(readyRoom, reservedRoom, useReservationFirst);
            RecordUnmetDemandOutcome(demandType, "Check-in blocked");
            RefreshPrototypeDaySummary();
            return;
        }

        successfulDemandCount++;
        lastDemandResult = assignmentMode + ": " + demandType + " -> " + readyRoom.roomName + " / " + lastMatchQualityLabel;
        lastChangedRoomName = readyRoom.roomName;
        RecordSuccessfulAssignmentOutcome(demandType, readyRoom, matchQuality);
        CompleteReservationResultAfterSuccess(readyRoom, reservedRoom, useReservationFirst);

        RefreshRoomVisual(readyRoom);
        RefreshOverview();
        RefreshPrototypeDaySummary();
    }

    private void TickUpcomingDemandPreview()
    {
        if (activeDemandWaitingForManualAssignment)
        {
            // 已经有一个 active demand 在等玩家手动分房时，暂停 ETA，避免重复生成新需求。
            upcomingDemandPreviewText = "Paused: active demand waiting";
            return;
        }

        upcomingDemandEtaSeconds = Mathf.Max(0f, upcomingDemandEtaSeconds - Time.deltaTime);
        upcomingDemandPreviewText = BuildUpcomingDemandPreviewText("Incoming");

        if (upcomingDemandEtaSeconds > 0f)
        {
            return;
        }

        ActivateUpcomingDemand();
    }

    private void ActivateUpcomingDemand()
    {
        if (activeDemandWaitingForManualAssignment)
        {
            // 已有 active demand 时不允许再激活新的需求，避免把玩家正在处理的需求覆盖掉。
            lastActivatedUpcomingDemandText = "Blocked: active demand waiting";
            return;
        }

        Room2DDemandType demandType = upcomingDemandType;
        Room2DEntity reservedRoom = reservedRoomForUpcomingDemand;

        activatedUpcomingDemandCount++;
        lastActivatedUpcomingDemandText = demandType + " activated";

        if (useManualActiveDemand)
        {
            StartActiveDemand(demandType, reservedRoom);
            return;
        }

        GenerateDemand(demandType, reservedRoom, true);
        FinishActivatedUpcomingDemand(demandType);
    }

    private void AdvanceNextDemandTypeAfterUpcomingDemand(Room2DDemandType activatedDemandType)
    {
        if (!alternateDemandTypes)
        {
            nextDemandType = activatedDemandType;
            return;
        }

        nextDemandType = activatedDemandType == Room2DDemandType.Normal
            ? Room2DDemandType.HighExpectation
            : Room2DDemandType.Normal;
    }

    private void StartActiveDemand(Room2DDemandType demandType, Room2DEntity reservedRoom)
    {
        activeDemandWaitingForManualAssignment = true;
        activeDemandType = demandType;
        activeDemandWaitSeconds = 0f;
        activeReservedRoomForFallback = reservedRoom;
        activeReservedRoomName = reservedRoom != null ? reservedRoom.roomName : "None";
        activeDemandStatus = "Waiting for manual room assignment";
        lastAssignmentMode = "Waiting";

        reservedRoomForUpcomingDemand = null;
        reservedRoomName = "None";
        upcomingDemandPreviewText = "Paused: active demand waiting";
        upcomingDemandEtaSeconds = 0f;
    }

    private void TickActiveDemand()
    {
        if (!activeDemandWaitingForManualAssignment)
        {
            return;
        }

        activeDemandWaitSeconds += Time.deltaTime;
        activeDemandStatus = "Waiting for manual assignment";

        if (manualAssignmentFallbackDelaySeconds <= 0f || activeDemandWaitSeconds < manualAssignmentFallbackDelaySeconds)
        {
            return;
        }

        ResolveActiveDemandWithFallback();
    }

    private void ResolveActiveDemandWithFallback()
    {
        Room2DEntity reservedRoom = activeReservedRoomForFallback;
        lastManualAssignmentResult = "No manual assignment: fallback used";
        GenerateDemand(activeDemandType, reservedRoom, reservedRoom != null, null, "Fallback");
        CompleteActiveDemand();
    }

    private void CompleteActiveDemand()
    {
        Room2DDemandType completedDemandType = activeDemandType;

        activeDemandWaitingForManualAssignment = false;
        activeDemandWaitSeconds = 0f;
        activeReservedRoomForFallback = null;
        activeReservedRoomName = "None";
        activeDemandStatus = "Resolved";

        FinishActivatedUpcomingDemand(completedDemandType);
    }

    private void FinishActivatedUpcomingDemand(Room2DDemandType demandType)
    {
        reservedRoomForUpcomingDemand = null;
        reservedRoomName = "None";
        AdvanceNextDemandTypeAfterUpcomingDemand(demandType);
        ScheduleUpcomingDemandPreview();
    }

    public string GetUpcomingDemandPreviewText()
    {
        if (!useUpcomingDemandPreview)
        {
            return "Upcoming\nPreview: Off\nNext in: " + FormatSeconds(Mathf.Max(0f, demandIntervalSeconds - demandTimerSeconds));
        }

        return "Upcoming\n"
            + "Type: " + upcomingDemandType + "\n"
            + "ETA: " + FormatSeconds(upcomingDemandEtaSeconds) + "\n"
            + "Status: " + upcomingDemandPreviewText + "\n"
            + "Reserved: " + reservedRoomName + "\n"
            + "Reserve result: " + lastReservationResult + "\n"
            + "Activated: " + activatedUpcomingDemandCount + "\n"
            + "Last active: " + lastActivatedUpcomingDemandText;
    }

    public string GetPreparationText()
    {
        FindRoomsIfNeeded();

        int dirtyCount;
        int cleaningCount;
        int awaitingInspectionCount;
        int readyCount;
        int occupiedCount;
        int blockedCount;
        CountRoomStates(out dirtyCount, out cleaningCount, out awaitingInspectionCount, out readyCount, out occupiedCount, out blockedCount);

        return "Preparation\n"
            + "Rooms D/C/I/R/O/B: "
            + dirtyCount + "/" + cleaningCount + "/" + awaitingInspectionCount + "/"
            + readyCount + "/" + occupiedCount + "/" + blockedCount + "\n"
            + "Upcoming 1/1: " + GetPreparationUpcomingLine() + "\n"
            + "Reserved: " + reservedRoomName + "\n"
            + "Dirty Priority: " + GetPriorityRoomLine(priorityDirtyRoom, Room2DState.Dirty, priorityDirtyRoomName) + "\n"
            + "Inspect Priority: " + GetPriorityRoomLine(priorityInspectionRoom, Room2DState.AwaitingInspection, priorityInspectionRoomName) + "\n"
            + "Warnings: " + GetPreparationWarningsText(dirtyCount, awaitingInspectionCount, readyCount) + "\n"
            + "Last Prep: " + lastPreparationAction;
    }

    public string GetManualAssignmentText()
    {
        return "Active Demand\n"
            + "Status: " + activeDemandStatus + "\n"
            + "Type: " + (activeDemandWaitingForManualAssignment ? activeDemandType.ToString() : "None") + "\n"
            + "Wait: " + FormatSeconds(activeDemandWaitSeconds) + " / " + FormatSeconds(manualAssignmentFallbackDelaySeconds) + "\n"
            + "Fallback Reserved: " + activeReservedRoomName + "\n"
            + "Last Assign: " + lastAssignmentMode + "\n"
            + "Manual: " + lastManualAssignmentResult + "\n"
            + GetCandidateReadyRoomsText();
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

        if (selectionManager == null)
        {
            selectionManager = FindFirstObjectByType<Room2DSelectionManager>();
        }
    }

    private void FindReferencesIfNeeded()
    {
        FindRoomsIfNeeded();
    }

    private Room2DEntity GetSelectedRoomEntity()
    {
        if (selectionManager == null || selectionManager.selectedRoom == null)
        {
            return null;
        }

        return selectionManager.selectedRoom.roomEntity;
    }

    private Room2DEntity FindRoomForDemand(
        Room2DDemandType demandType,
        Room2DEntity reservedRoom,
        bool useReservationFirst,
        Room2DEntity forcedRoom)
    {
        if (forcedRoom != null)
        {
            return forcedRoom.CanSimulateCheckIn() ? forcedRoom : null;
        }

        if (!useReservationFirst || reservedRoom == null)
        {
            return FindBestReadyRoomForDemand(demandType);
        }

        // 预留房只有 Ready 时才会被使用；否则保留原来的自动分配作为 fallback。
        if (reservedRoom.CanSimulateCheckIn())
        {
            lastReservationResult = "Succeeded: used " + reservedRoom.roomName;
            return reservedRoom;
        }

        lastReservationResult = "Failed: " + reservedRoom.roomName + " was " + reservedRoom.GetStateDisplayName();
        return FindBestReadyRoomForDemand(demandType);
    }

    private void CountRoomStates(
        out int dirtyCount,
        out int cleaningCount,
        out int awaitingInspectionCount,
        out int readyCount,
        out int occupiedCount,
        out int blockedCount)
    {
        dirtyCount = 0;
        cleaningCount = 0;
        awaitingInspectionCount = 0;
        readyCount = 0;
        occupiedCount = 0;
        blockedCount = 0;

        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null)
            {
                continue;
            }

            switch (room.currentState)
            {
                case Room2DState.Cleaning:
                    cleaningCount++;
                    break;
                case Room2DState.AwaitingInspection:
                    awaitingInspectionCount++;
                    break;
                case Room2DState.Ready:
                    readyCount++;
                    break;
                case Room2DState.Occupied:
                    occupiedCount++;
                    break;
                case Room2DState.Blocked:
                    blockedCount++;
                    break;
                default:
                    dirtyCount++;
                    break;
            }
        }
    }

    private string GetPreparationUpcomingLine()
    {
        if (!useUpcomingDemandPreview)
        {
            return nextDemandType + " in " + FormatSeconds(Mathf.Max(0f, demandIntervalSeconds - demandTimerSeconds));
        }

        if (activeDemandWaitingForManualAssignment)
        {
            return "Active " + activeDemandType + " waiting";
        }

        return upcomingDemandType + " in " + FormatSeconds(upcomingDemandEtaSeconds);
    }

    private string GetPriorityRoomLine(Room2DEntity room, Room2DState expectedState, string cachedName)
    {
        if (room == null)
        {
            return cachedName;
        }

        if (room.currentState != expectedState)
        {
            return room.roomName + " now " + room.GetStateDisplayName();
        }

        return room.roomName + " " + FormatSeconds(room.stateElapsedSeconds);
    }

    private string GetPreparationWarningsText(int dirtyCount, int awaitingInspectionCount, int readyCount)
    {
        if (activeDemandWaitingForManualAssignment)
        {
            return "Assign active demand now";
        }

        if (readyCount == 0)
        {
            return "No Ready room for next demand";
        }

        if (awaitingInspectionCount >= 2)
        {
            return "Inspection backlog";
        }

        if (dirtyCount >= 3)
        {
            return "Dirty backlog";
        }

        return "None";
    }

    private string GetCandidateReadyRoomsText()
    {
        FindRoomsIfNeeded();

        if (!activeDemandWaitingForManualAssignment)
        {
            return "Candidates: None";
        }

        string candidateText = "Candidates";
        int shownCount = 0;

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null || !room.CanSimulateCheckIn())
            {
                continue;
            }

            if (shownCount < 5)
            {
                candidateText += "\n- " + room.roomName
                    + ": " + GetMatchDisplayName(EvaluateMatchQuality(room, activeDemandType))
                    + " C/W " + GetCleanlinessSuitability(room)
                    + "/" + GetWearSuitability(room);
            }

            shownCount++;
        }

        if (shownCount == 0)
        {
            return "Candidates: no Ready rooms";
        }

        if (shownCount > 5)
        {
            candidateText += "\n- +" + (shownCount - 5) + " more";
        }

        return candidateText;
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

    private void CompleteReservationResultAfterSuccess(Room2DEntity assignedRoom, Room2DEntity reservedRoom, bool usedReservationFlow)
    {
        if (!usedReservationFlow || reservedRoom == null)
        {
            return;
        }

        if (assignedRoom == reservedRoom)
        {
            lastReservationResult = "Succeeded: used " + reservedRoom.roomName;
            return;
        }

        lastReservationResult += ", fallback used " + assignedRoom.roomName;
    }

    private void CompleteReservationResultAfterUnmet(Room2DEntity reservedRoom, bool usedReservationFlow)
    {
        if (!usedReservationFlow || reservedRoom == null)
        {
            return;
        }

        if (lastReservationResult.StartsWith("Failed:"))
        {
            lastReservationResult += ", no fallback Ready room";
        }
    }

    private void CompleteReservationResultAfterBlockedCheckIn(Room2DEntity attemptedRoom, Room2DEntity reservedRoom, bool usedReservationFlow)
    {
        if (!usedReservationFlow || reservedRoom == null)
        {
            return;
        }

        if (attemptedRoom == reservedRoom)
        {
            lastReservationResult = "Failed: reserved check-in blocked";
            return;
        }

        lastReservationResult += ", fallback check-in blocked";
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

    private string BuildUpcomingDemandPreviewText(string status)
    {
        return status + " " + upcomingDemandType + " in " + FormatSeconds(upcomingDemandEtaSeconds);
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
