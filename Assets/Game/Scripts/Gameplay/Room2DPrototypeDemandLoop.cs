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

    public enum Room2DRoomPreference
    {
        AnyRoom,
        BetterRoomPreferred
    }

    public enum Room2DFloorPreference
    {
        NoPreference,
        HighFloorPreferred,
        LowFloorPreferred
    }

    public enum Room2DFacingPreference
    {
        NoPreference,
        QuietPreferred,
        ViewPreferred
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
    public bool alternateDemandRoomPreferences = true;
    public Room2DRoomPreference nextDemandRoomPreference = Room2DRoomPreference.AnyRoom;
    public Room2DFloorPreference nextDemandFloorPreference = Room2DFloorPreference.NoPreference;
    public Room2DFacingPreference nextDemandFacingPreference = Room2DFacingPreference.NoPreference;
    public int generatedDemandCount;
    public int successfulDemandCount;
    public int unmetDemandCount;

    [Header("Upcoming Demand Preview")]
    // 最小 ETA 原型：只预告下一个入住需求，不做完整客人列表或预分配。
    public bool useUpcomingDemandPreview = true;
    public Room2DDemandType upcomingDemandType = Room2DDemandType.Normal;
    public Room2DRoomPreference upcomingDemandRoomPreference = Room2DRoomPreference.AnyRoom;
    public Room2DFloorPreference upcomingDemandFloorPreference = Room2DFloorPreference.NoPreference;
    public Room2DFacingPreference upcomingDemandFacingPreference = Room2DFacingPreference.NoPreference;
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
    public Room2DRoomPreference activeDemandRoomPreference = Room2DRoomPreference.AnyRoom;
    public Room2DFloorPreference activeDemandFloorPreference = Room2DFloorPreference.NoPreference;
    public Room2DFacingPreference activeDemandFacingPreference = Room2DFacingPreference.NoPreference;
    public float activeDemandWaitSeconds;
    public float manualAssignmentFallbackDelaySeconds = 8f;
    public Room2DEntity activeReservedRoomForFallback;
    public string activeReservedRoomName = "None";
    public string activeDemandStatus = "None";
    public string lastManualAssignmentResult = "None";
    public string lastAssignmentMode = "None";
    public string lastResolvedAssignmentMode = "None";

    [Header("Occupancy")]
    // Occupied 房间住满多少现实秒后自动退房，重新变成 Dirty。
    public float occupiedDurationSeconds = 20f;
    public int simulatedCheckoutCount;

    [Header("Prototype Complaint Reassignment")]
    // 原型投诉重分房：房型/偏好不合不会阻止入住，但可能在入住后触发前台投诉。
    public bool enableComplaintReassignment = true;
    public float complaintDelaySeconds = 60f;
    public float complaintPatienceSeconds = 20f;
    public float complaintPatienceLossMultiplier = 2f;
    public int complaintCompensationPenaltyScore = -6;
    public int complaintPatienceExpiredPenaltyScore = -4;
    public Room2DEntity pendingComplaintRoom;
    public string pendingComplaintRoomName = "None";
    public float pendingComplaintTimerSeconds;
    public bool complaintWaitingForReassignment;
    public float complaintReassignmentWaitSeconds;
    public float complaintPatienceRemainingSeconds;
    public bool complaintPatiencePenaltyApplied;
    public Room2DDemandType complaintDemandType = Room2DDemandType.Normal;
    public Room2DRoomPreference complaintRoomPreference = Room2DRoomPreference.AnyRoom;
    public Room2DFloorPreference complaintFloorPreference = Room2DFloorPreference.NoPreference;
    public Room2DFacingPreference complaintFacingPreference = Room2DFacingPreference.NoPreference;
    public int roomComplaintCount;
    public int complaintReassignmentCount;
    public int compensationRequestCount;
    public string complaintStatus = "None";
    public string lastComplaintResult = "None";

    [Header("Debug")]
    public string lastDemandResult = "None";
    public string lastChangedRoomName = "None";
    public Room2DDemandType lastDemandType = Room2DDemandType.Normal;
    public Room2DRoomPreference lastDemandRoomPreference = Room2DRoomPreference.AnyRoom;
    public Room2DFloorPreference lastDemandFloorPreference = Room2DFloorPreference.NoPreference;
    public Room2DFacingPreference lastDemandFacingPreference = Room2DFacingPreference.NoPreference;
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
    public int servicePressurePenaltyCount;
    public string lastServicePressureResult = "None";

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
        TickComplaintReassignment();
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
        Room2DRoomPreference roomPreference = nextDemandRoomPreference;
        Room2DFloorPreference floorPreference = nextDemandFloorPreference;
        Room2DFacingPreference facingPreference = nextDemandFacingPreference;
        GenerateDemand(demandType, roomPreference, floorPreference, facingPreference);

        if (alternateDemandTypes)
        {
            nextDemandType = demandType == Room2DDemandType.Normal
                ? Room2DDemandType.HighExpectation
                : Room2DDemandType.Normal;
        }

        AdvanceNextDemandRoomPreferenceAfterDemand(roomPreference);

        if (useUpcomingDemandPreview)
        {
            ScheduleUpcomingDemandPreview();
        }
    }

    [ContextMenu("Schedule Upcoming Demand Preview")]
    public void ScheduleUpcomingDemandPreview()
    {
        upcomingDemandType = nextDemandType;
        upcomingDemandRoomPreference = nextDemandRoomPreference;
        upcomingDemandFloorPreference = nextDemandFloorPreference;
        upcomingDemandFacingPreference = nextDemandFacingPreference;
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
        lastReservationResult = "Reserved " + room.roomName + " for "
            + upcomingDemandType + " / " + BuildDemandPreferenceSummary(
                upcomingDemandRoomPreference,
                upcomingDemandFloorPreference,
                upcomingDemandFacingPreference)
            + GetRoomTypeRiskSuffix(room, upcomingDemandRoomPreference);
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
        if (complaintWaitingForReassignment)
        {
            return AssignRoomToComplaintReassignment(room);
        }

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

        lastManualAssignmentResult = "Manual assigned " + room.roomName
            + GetRoomTypeRiskSuffix(room, activeDemandRoomPreference);
        GenerateDemand(
            activeDemandType,
            activeDemandRoomPreference,
            activeDemandFloorPreference,
            activeDemandFacingPreference,
            null,
            false,
            room,
            "Manual");
        CompleteActiveDemand();
        return true;
    }

    [ContextMenu("Generate Normal Demand")]
    public void GenerateNormalDemandForTesting()
    {
        GenerateDemand(Room2DDemandType.Normal, nextDemandRoomPreference, nextDemandFloorPreference, nextDemandFacingPreference);
    }

    [ContextMenu("Generate High Expectation Demand")]
    public void GenerateHighExpectationDemandForTesting()
    {
        GenerateDemand(Room2DDemandType.HighExpectation, nextDemandRoomPreference, nextDemandFloorPreference, nextDemandFacingPreference);
    }

    [ContextMenu("Generate Any Room Demand")]
    public void GenerateAnyRoomDemandForTesting()
    {
        GenerateDemand(nextDemandType, Room2DRoomPreference.AnyRoom, nextDemandFloorPreference, nextDemandFacingPreference);
    }

    [ContextMenu("Generate Better Room Preferred Demand")]
    public void GenerateBetterRoomPreferredDemandForTesting()
    {
        GenerateDemand(nextDemandType, Room2DRoomPreference.BetterRoomPreferred, nextDemandFloorPreference, nextDemandFacingPreference);
    }

    [ContextMenu("Generate High Floor Preferred Demand")]
    public void GenerateHighFloorPreferredDemandForTesting()
    {
        GenerateDemand(nextDemandType, nextDemandRoomPreference, Room2DFloorPreference.HighFloorPreferred, nextDemandFacingPreference);
    }

    [ContextMenu("Generate Low Floor Preferred Demand")]
    public void GenerateLowFloorPreferredDemandForTesting()
    {
        GenerateDemand(nextDemandType, nextDemandRoomPreference, Room2DFloorPreference.LowFloorPreferred, nextDemandFacingPreference);
    }

    [ContextMenu("Generate Quiet Preferred Demand")]
    public void GenerateQuietPreferredDemandForTesting()
    {
        GenerateDemand(nextDemandType, nextDemandRoomPreference, nextDemandFloorPreference, Room2DFacingPreference.QuietPreferred);
    }

    [ContextMenu("Generate View Preferred Demand")]
    public void GenerateViewPreferredDemandForTesting()
    {
        GenerateDemand(nextDemandType, nextDemandRoomPreference, nextDemandFloorPreference, Room2DFacingPreference.ViewPreferred);
    }

    private void GenerateDemand(Room2DDemandType demandType)
    {
        GenerateDemand(demandType, nextDemandRoomPreference, nextDemandFloorPreference, nextDemandFacingPreference, null, false, null, "Fallback");
    }

    private void GenerateDemand(Room2DDemandType demandType, Room2DRoomPreference roomPreference)
    {
        GenerateDemand(demandType, roomPreference, nextDemandFloorPreference, nextDemandFacingPreference, null, false, null, "Fallback");
    }

    private void GenerateDemand(
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference)
    {
        GenerateDemand(demandType, roomPreference, floorPreference, facingPreference, null, false, null, "Fallback");
    }

    private void GenerateDemand(
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference,
        Room2DEntity reservedRoom,
        bool useReservationFirst)
    {
        GenerateDemand(demandType, roomPreference, floorPreference, facingPreference, reservedRoom, useReservationFirst, null, useReservationFirst ? "Reservation/Fallback" : "Fallback");
    }

    private void GenerateDemand(
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference,
        Room2DEntity reservedRoom,
        bool useReservationFirst,
        Room2DEntity forcedRoom,
        string assignmentMode)
    {
        FindRoomsIfNeeded();
        generatedDemandCount++;
        lastDemandType = demandType;
        lastDemandRoomPreference = roomPreference;
        lastDemandFloorPreference = floorPreference;
        lastDemandFacingPreference = facingPreference;
        lastAssignmentMode = assignmentMode;
        lastResolvedAssignmentMode = assignmentMode;

        Room2DEntity readyRoom = FindRoomForDemand(demandType, roomPreference, floorPreference, facingPreference, reservedRoom, useReservationFirst, forcedRoom);
        if (readyRoom == null)
        {
            unmetDemandCount++;
            lastDemandResult = assignmentMode + ": " + demandType
                + " / " + BuildDemandPreferenceSummary(roomPreference, floorPreference, facingPreference)
                + " unmet: no Ready room";
            lastChangedRoomName = "None";
            lastMatchQuality = Room2DMatchQuality.PoorMatch;
            lastMatchQualityLabel = "No Match";
            lastCleanlinessSuitability = 0;
            lastWearSuitability = 0;
            CompleteReservationResultAfterUnmet(reservedRoom, useReservationFirst);
            RecordUnmetDemandOutcome(demandType, roomPreference, floorPreference, facingPreference, "No Ready room");
            RefreshPrototypeDaySummary();
            return;
        }

        Room2DMatchQuality matchQuality = EvaluateMatchQuality(readyRoom, demandType, roomPreference, floorPreference, facingPreference);
        lastMatchQuality = matchQuality;
        lastMatchQualityLabel = GetMatchDisplayName(matchQuality);
        lastCleanlinessSuitability = GetCleanlinessSuitability(readyRoom);
        lastWearSuitability = GetWearSuitability(readyRoom);

        // 使用 Room2DEntity 自己的入住 guard，避免把 Dirty / Blocked / Occupied 房间错误入住。
        if (!readyRoom.SimulateCheckIn())
        {
            unmetDemandCount++;
            lastDemandResult = assignmentMode + ": " + demandType
                + " / " + BuildDemandPreferenceSummary(roomPreference, floorPreference, facingPreference)
                + " unmet: check-in guard blocked";
            lastChangedRoomName = readyRoom.roomName;
            lastMatchQualityLabel = "No Match";
            CompleteReservationResultAfterBlockedCheckIn(readyRoom, reservedRoom, useReservationFirst);
            RecordUnmetDemandOutcome(demandType, roomPreference, floorPreference, facingPreference, "Check-in blocked");
            RefreshPrototypeDaySummary();
            return;
        }

        successfulDemandCount++;
        lastDemandResult = assignmentMode + ": " + demandType
            + " / " + BuildDemandPreferenceSummary(roomPreference, floorPreference, facingPreference)
            + " -> " + readyRoom.roomName + " / " + lastMatchQualityLabel;
        lastChangedRoomName = readyRoom.roomName;
        RecordSuccessfulAssignmentOutcome(demandType, roomPreference, floorPreference, facingPreference, readyRoom, matchQuality);
        CompleteReservationResultAfterSuccess(readyRoom, reservedRoom, useReservationFirst);
        ScheduleComplaintIfNeeded(demandType, roomPreference, floorPreference, facingPreference, readyRoom, matchQuality);

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
        Room2DRoomPreference roomPreference = upcomingDemandRoomPreference;
        Room2DFloorPreference floorPreference = upcomingDemandFloorPreference;
        Room2DFacingPreference facingPreference = upcomingDemandFacingPreference;
        Room2DEntity reservedRoom = reservedRoomForUpcomingDemand;

        activatedUpcomingDemandCount++;
        lastActivatedUpcomingDemandText = demandType + " / "
            + BuildDemandPreferenceSummary(roomPreference, floorPreference, facingPreference)
            + " activated";

        if (useManualActiveDemand)
        {
            StartActiveDemand(demandType, roomPreference, floorPreference, facingPreference, reservedRoom);
            return;
        }

        GenerateDemand(demandType, roomPreference, floorPreference, facingPreference, reservedRoom, true);
        FinishActivatedUpcomingDemand(demandType, roomPreference, floorPreference, facingPreference);
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

    private void AdvanceNextDemandRoomPreferenceAfterDemand(Room2DRoomPreference activatedRoomPreference)
    {
        if (!alternateDemandRoomPreferences)
        {
            nextDemandRoomPreference = activatedRoomPreference;
            return;
        }

        nextDemandRoomPreference = activatedRoomPreference == Room2DRoomPreference.AnyRoom
            ? Room2DRoomPreference.BetterRoomPreferred
            : Room2DRoomPreference.AnyRoom;
    }

    private void StartActiveDemand(
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference,
        Room2DEntity reservedRoom)
    {
        activeDemandWaitingForManualAssignment = true;
        activeDemandType = demandType;
        activeDemandRoomPreference = roomPreference;
        activeDemandFloorPreference = floorPreference;
        activeDemandFacingPreference = facingPreference;
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

    private void TickComplaintReassignment()
    {
        if (!enableComplaintReassignment)
        {
            return;
        }

        TickPendingComplaintTimer();
        TickComplaintWaitingPressure();
    }

    private void TickPendingComplaintTimer()
    {
        if (pendingComplaintRoom == null || complaintWaitingForReassignment)
        {
            return;
        }

        if (pendingComplaintRoom.currentState != Room2DState.Occupied)
        {
            ClearPendingComplaint();
            return;
        }

        pendingComplaintTimerSeconds += Time.deltaTime;
        complaintStatus = "Complaint in " + FormatSeconds(Mathf.Max(0f, complaintDelaySeconds - pendingComplaintTimerSeconds));

        if (pendingComplaintTimerSeconds < complaintDelaySeconds)
        {
            return;
        }

        StartComplaintReassignment();
    }

    private void TickComplaintWaitingPressure()
    {
        if (!complaintWaitingForReassignment)
        {
            return;
        }

        complaintReassignmentWaitSeconds += Time.deltaTime;
        complaintPatienceRemainingSeconds = Mathf.Max(
            0f,
            complaintPatienceRemainingSeconds - Time.deltaTime * Mathf.Max(1f, complaintPatienceLossMultiplier));
        complaintStatus = "Complaint waiting: patience " + FormatSeconds(complaintPatienceRemainingSeconds);

        if (complaintPatiencePenaltyApplied || complaintPatienceRemainingSeconds > 0f)
        {
            return;
        }

        complaintPatiencePenaltyApplied = true;
        lastComplaintResult = "Complaint patience expired, extra compensation requested";
        ApplyPrototypeServicePressure(lastComplaintResult, complaintPatienceExpiredPenaltyScore);
    }

    private void ScheduleComplaintIfNeeded(
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference,
        Room2DEntity assignedRoom,
        Room2DMatchQuality matchQuality)
    {
        if (!enableComplaintReassignment || assignedRoom == null)
        {
            return;
        }

        bool roomTypeMismatch = !DoesRoomMatchRoomTypePreference(assignedRoom, roomPreference);
        if (!roomTypeMismatch && matchQuality != Room2DMatchQuality.PoorMatch)
        {
            return;
        }

        pendingComplaintRoom = assignedRoom;
        pendingComplaintRoomName = assignedRoom.roomName;
        pendingComplaintTimerSeconds = 0f;
        complaintDemandType = demandType;
        complaintRoomPreference = roomPreference;
        complaintFloorPreference = floorPreference;
        complaintFacingPreference = facingPreference;
        complaintStatus = "Scheduled: " + assignedRoom.roomName + " may complain";
        lastComplaintResult = "Complaint scheduled after poor assignment";
    }

    private void StartComplaintReassignment()
    {
        if (pendingComplaintRoom == null)
        {
            return;
        }

        Room2DEntity originalRoom = pendingComplaintRoom;
        roomComplaintCount++;
        compensationRequestCount++;
        complaintWaitingForReassignment = true;
        complaintReassignmentWaitSeconds = 0f;
        complaintPatienceRemainingSeconds = complaintPatienceSeconds;
        complaintPatiencePenaltyApplied = false;
        complaintStatus = "Waiting for reassignment";
        lastComplaintResult = originalRoom.roomName + " complaint: ask compensation and reassign";

        // 投诉客人离开原房间，原房间变 Dirty，需要重新走 HSK -> Inspector -> Ready。
        if (originalRoom.currentState == Room2DState.Occupied)
        {
            originalRoom.SimulateCheckout();
            RefreshRoomVisual(originalRoom);
        }

        pendingComplaintRoom = null;
        pendingComplaintRoomName = "None";
        pendingComplaintTimerSeconds = 0f;
        ApplyPrototypeServicePressure(lastComplaintResult, complaintCompensationPenaltyScore);
        RefreshOverview();
    }

    public bool AssignRoomToComplaintReassignment(Room2DEntity room)
    {
        if (!complaintWaitingForReassignment)
        {
            lastManualAssignmentResult = "Complaint reassign failed: no complaint";
            return false;
        }

        if (room == null)
        {
            lastManualAssignmentResult = "Complaint reassign failed: no selected room";
            return false;
        }

        if (!room.CanSimulateCheckIn())
        {
            lastManualAssignmentResult = "Complaint reassign failed: " + room.roomName + " is " + room.GetStateDisplayName();
            return false;
        }

        Room2DMatchQuality matchQuality = EvaluateMatchQuality(
            room,
            complaintDemandType,
            complaintRoomPreference,
            complaintFloorPreference,
            complaintFacingPreference);

        if (!room.SimulateCheckIn())
        {
            lastManualAssignmentResult = "Complaint reassign failed: check-in blocked";
            return false;
        }

        complaintReassignmentCount++;
        lastManualAssignmentResult = "Complaint reassigned " + room.roomName
            + " / " + GetMatchDisplayName(matchQuality)
            + GetRoomTypeRiskSuffix(room, complaintRoomPreference);
        lastComplaintResult = lastManualAssignmentResult;
        lastChangedRoomName = room.roomName;
        lastDemandType = complaintDemandType;
        lastDemandRoomPreference = complaintRoomPreference;
        lastDemandFloorPreference = complaintFloorPreference;
        lastDemandFacingPreference = complaintFacingPreference;
        lastMatchQuality = matchQuality;
        lastMatchQualityLabel = GetMatchDisplayName(matchQuality);
        lastCleanlinessSuitability = GetCleanlinessSuitability(room);
        lastWearSuitability = GetWearSuitability(room);
        lastResolvedAssignmentMode = "Complaint Reassign";
        RecordSuccessfulAssignmentOutcome(
            complaintDemandType,
            complaintRoomPreference,
            complaintFloorPreference,
            complaintFacingPreference,
            room,
            matchQuality);

        complaintWaitingForReassignment = false;
        complaintReassignmentWaitSeconds = 0f;
        complaintPatienceRemainingSeconds = 0f;
        complaintStatus = "Resolved";

        RefreshRoomVisual(room);
        RefreshOverview();
        RefreshPrototypeDaySummary();
        return true;
    }

    private void ClearPendingComplaint()
    {
        pendingComplaintRoom = null;
        pendingComplaintRoomName = "None";
        pendingComplaintTimerSeconds = 0f;
        complaintStatus = "None";
    }

    private void ResolveActiveDemandWithFallback()
    {
        Room2DEntity reservedRoom = activeReservedRoomForFallback;
        lastManualAssignmentResult = "No manual assignment: fallback used";
        GenerateDemand(
            activeDemandType,
            activeDemandRoomPreference,
            activeDemandFloorPreference,
            activeDemandFacingPreference,
            reservedRoom,
            reservedRoom != null,
            null,
            "Fallback");
        CompleteActiveDemand();
    }

    private void CompleteActiveDemand()
    {
        Room2DDemandType completedDemandType = activeDemandType;
        Room2DRoomPreference completedRoomPreference = activeDemandRoomPreference;
        Room2DFloorPreference completedFloorPreference = activeDemandFloorPreference;
        Room2DFacingPreference completedFacingPreference = activeDemandFacingPreference;

        activeDemandWaitingForManualAssignment = false;
        activeDemandWaitSeconds = 0f;
        activeReservedRoomForFallback = null;
        activeReservedRoomName = "None";
        activeDemandStatus = "Resolved";

        FinishActivatedUpcomingDemand(
            completedDemandType,
            completedRoomPreference,
            completedFloorPreference,
            completedFacingPreference);
    }

    private void FinishActivatedUpcomingDemand(
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference)
    {
        reservedRoomForUpcomingDemand = null;
        reservedRoomName = "None";
        AdvanceNextDemandTypeAfterUpcomingDemand(demandType);
        AdvanceNextDemandRoomPreferenceAfterDemand(roomPreference);
        ScheduleUpcomingDemandPreview();
    }

    public string GetUpcomingDemandPreviewText()
    {
        if (!useUpcomingDemandPreview)
        {
            return "Upcoming\n"
                + "Preview: Off\n"
                + "Type: " + nextDemandType + "\n"
                + "Prefs: " + BuildDemandPreferenceSummary(
                    nextDemandRoomPreference,
                    nextDemandFloorPreference,
                    nextDemandFacingPreference) + "\n"
                + "Next in: " + FormatSeconds(Mathf.Max(0f, demandIntervalSeconds - demandTimerSeconds));
        }

        return "Upcoming\n"
            + "Type: " + upcomingDemandType + "\n"
            + "Prefs: " + BuildDemandPreferenceSummary(
                upcomingDemandRoomPreference,
                upcomingDemandFloorPreference,
                upcomingDemandFacingPreference) + "\n"
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
            + "Prefs: " + (activeDemandWaitingForManualAssignment
                ? BuildDemandPreferenceSummary(activeDemandRoomPreference, activeDemandFloorPreference, activeDemandFacingPreference)
                : "None") + "\n"
            + "Wait: " + FormatSeconds(activeDemandWaitSeconds) + " / " + FormatSeconds(manualAssignmentFallbackDelaySeconds) + "\n"
            + "Fallback Reserved: " + activeReservedRoomName + "\n"
            + "Last Assign: " + lastAssignmentMode + "\n"
            + "Manual: " + lastManualAssignmentResult + "\n"
            + GetCandidateReadyRoomsText();
    }

    public string GetUpcomingDemandCardText()
    {
        // 只把已有 upcoming demand 数据整理成卡片文本，不创建真实客人或队列。
        if (!useUpcomingDemandPreview)
        {
            return "[Upcoming Demand]\n"
                + "Type: " + nextDemandType + "\n"
                + "Prefs: " + BuildDemandPreferenceSummary(
                    nextDemandRoomPreference,
                    nextDemandFloorPreference,
                    nextDemandFacingPreference) + "\n"
                + "ETA: " + FormatSeconds(Mathf.Max(0f, demandIntervalSeconds - demandTimerSeconds)) + "\n"
                + "Reserved: None\n"
                + "Status: Preview off";
        }

        return "[Upcoming Demand]\n"
            + "Type: " + upcomingDemandType + "\n"
            + "Prefs: " + BuildDemandPreferenceSummary(
                upcomingDemandRoomPreference,
                upcomingDemandFloorPreference,
                upcomingDemandFacingPreference) + "\n"
            + "ETA: " + FormatSeconds(upcomingDemandEtaSeconds) + "\n"
            + "Reserved: " + reservedRoomName + "\n"
            + "Reserve Result: " + lastReservationResult + "\n"
            + "Status: " + upcomingDemandPreviewText;
    }

    public string GetActiveDemandCardText()
    {
        // Active demand 是当前等待玩家分房的需求；没有 active 时也显示空卡片，便于观察状态切换。
        string demandTypeText = activeDemandWaitingForManualAssignment ? activeDemandType.ToString() : "None";
        string preferenceText = activeDemandWaitingForManualAssignment
            ? BuildDemandPreferenceSummary(activeDemandRoomPreference, activeDemandFloorPreference, activeDemandFacingPreference)
            : "None";
        string waitText = activeDemandWaitingForManualAssignment
            ? FormatSeconds(activeDemandWaitSeconds) + " / " + FormatSeconds(manualAssignmentFallbackDelaySeconds)
            : "0s / " + FormatSeconds(manualAssignmentFallbackDelaySeconds);

        return "[Active Demand]\n"
            + "Status: " + activeDemandStatus + "\n"
            + "Type: " + demandTypeText + "\n"
            + "Prefs: " + preferenceText + "\n"
            + "Wait: " + waitText + "\n"
            + "Fallback Reserved: " + activeReservedRoomName + "\n"
            + "Assignment: " + lastAssignmentMode + "\n"
            + "Manual: " + lastManualAssignmentResult + "\n"
            + GetCandidateReadyRoomsText();
    }

    public string GetResolvedDemandCardText()
    {
        // Latest resolved demand 用来快速判断上一单是手动分房、fallback，还是无人可住。
        if (generatedDemandCount == 0)
        {
            return "[Latest Resolved]\n"
                + "Status: None yet";
        }

        return "[Latest Resolved]\n"
            + "Mode: " + lastResolvedAssignmentMode + "\n"
            + "Type: " + lastDemandType + "\n"
            + "Prefs: " + BuildDemandPreferenceSummary(
                lastDemandRoomPreference,
                lastDemandFloorPreference,
                lastDemandFacingPreference) + "\n"
            + "Room: " + lastChangedRoomName + "\n"
            + "Match: " + lastMatchQualityLabel + "\n"
            + "Clean/Wear: " + lastCleanlinessSuitability + " / " + lastWearSuitability + "\n"
            + "Outcome: " + lastOutcomeLabel + "\n"
            + "Result: " + lastOutcomeSummary;
    }

    public string GetComplaintReassignmentCardText()
    {
        string complaintDemandText = complaintWaitingForReassignment || pendingComplaintRoom != null
            ? complaintDemandType + " / " + BuildDemandPreferenceSummary(
                complaintRoomPreference,
                complaintFloorPreference,
                complaintFacingPreference)
            : "None";
        string complaintInText = pendingComplaintRoom != null
            ? FormatSeconds(Mathf.Max(0f, complaintDelaySeconds - pendingComplaintTimerSeconds))
            : "None";

        return "[Complaint Reassign]\n"
            + "Status: " + complaintStatus + "\n"
            + "Demand: " + complaintDemandText + "\n"
            + "Pending Room: " + pendingComplaintRoomName + "\n"
            + "Complaint In: " + complaintInText + "\n"
            + "Waiting: " + FormatSeconds(complaintReassignmentWaitSeconds) + "\n"
            + "Patience: " + FormatSeconds(complaintPatienceRemainingSeconds) + "\n"
            + "Complaints/Reassigns: " + roomComplaintCount + " / " + complaintReassignmentCount + "\n"
            + "Compensation: " + compensationRequestCount + "\n"
            + "Last: " + lastComplaintResult;
    }

    public int GetPrototypeCleanlinessSuitability(Room2DEntity room)
    {
        // 给 HUD 读取用，避免 Selected Room 卡片自己复制一套房间质量算法。
        return GetCleanlinessSuitability(room);
    }

    public int GetPrototypeWearSuitability(Room2DEntity room)
    {
        // 给 HUD 读取用，避免 Selected Room 卡片自己复制一套房间质量算法。
        return GetWearSuitability(room);
    }

    public string GetPrototypeMatchHintForRoom(Room2DEntity room)
    {
        if (room == null)
        {
            return "Match Hint: None";
        }

        Room2DDemandType demandType = GetCurrentVisibleDemandType();
        Room2DRoomPreference roomPreference = GetCurrentVisibleDemandRoomPreference();
        Room2DFloorPreference floorPreference = GetCurrentVisibleDemandFloorPreference();
        Room2DFacingPreference facingPreference = GetCurrentVisibleDemandFacingPreference();
        string demandStage = activeDemandWaitingForManualAssignment ? "Active" : "Upcoming";
        string readyNote = room.CanSimulateCheckIn() ? "" : " (not Ready)";
        string roomTypeNote = DoesRoomMatchRoomTypePreference(room, roomPreference) ? "" : " (type risk)";

        return "Match Hint: " + demandStage + " " + demandType + " / "
            + BuildDemandPreferenceSummary(roomPreference, floorPreference, facingPreference)
            + " -> " + GetMatchDisplayName(EvaluateMatchQuality(room, demandType, roomPreference, floorPreference, facingPreference))
            + readyNote
            + roomTypeNote;
    }

    public string GetShowcaseCurrentGuestHeadline()
    {
        if (complaintWaitingForReassignment)
        {
            return "Complaint guest needs a new room";
        }

        if (activeDemandWaitingForManualAssignment)
        {
            return activeDemandType + " guest is waiting";
        }

        if (useUpcomingDemandPreview)
        {
            return upcomingDemandType + " guest arrives in " + FormatSeconds(upcomingDemandEtaSeconds);
        }

        return "No guest is waiting";
    }

    public string GetShowcaseCurrentGuestPreferenceLine()
    {
        if (complaintWaitingForReassignment)
        {
            return BuildDemandPreferenceSummary(
                complaintRoomPreference,
                complaintFloorPreference,
                complaintFacingPreference);
        }

        if (activeDemandWaitingForManualAssignment)
        {
            return BuildDemandPreferenceSummary(
                activeDemandRoomPreference,
                activeDemandFloorPreference,
                activeDemandFacingPreference);
        }

        return BuildDemandPreferenceSummary(
            upcomingDemandRoomPreference,
            upcomingDemandFloorPreference,
            upcomingDemandFacingPreference);
    }

    public string GetShowcaseRoomFitText(Room2DEntity room)
    {
        if (room == null)
        {
            return "No room";
        }

        Room2DDemandType demandType = GetCurrentVisibleDemandType();
        Room2DRoomPreference roomPreference = GetCurrentVisibleDemandRoomPreference();
        Room2DFloorPreference floorPreference = GetCurrentVisibleDemandFloorPreference();
        Room2DFacingPreference facingPreference = GetCurrentVisibleDemandFacingPreference();
        Room2DMatchQuality matchQuality = EvaluateMatchQuality(room, demandType, roomPreference, floorPreference, facingPreference);

        return GetMatchDisplayName(matchQuality)
            + " / " + BuildDemandPreferenceSummary(roomPreference, floorPreference, facingPreference)
            + GetRoomTypeRiskSuffix(room, roomPreference);
    }

    public bool IsRoomReservedForPrototypeDemand(Room2DEntity room)
    {
        if (room == null)
        {
            return false;
        }

        return room == reservedRoomForUpcomingDemand
            || room == activeReservedRoomForFallback;
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
            + "Service Pressure: " + servicePressurePenaltyCount + "\n"
            + "Complaints/Reassigns: " + roomComplaintCount + "/" + complaintReassignmentCount + "\n"
            + "Compensation: " + compensationRequestCount + "\n"
            + "Score: " + prototypeSatisfactionScore + " (" + prototypeSatisfactionTrend + ")\n"
            + "Dirty: " + summaryDirtyCount + "\n"
            + "Inspect Wait: " + summaryAwaitingInspectionCount + "\n"
            + "Oldest Dirty: " + FormatSeconds(summaryOldestDirtySeconds) + "\n"
            + "Last Pressure: " + lastServicePressureResult + "\n"
            + "Hint: " + summaryStatusHint;
    }

    // 前台和 Lounge 这类轻量压力系统通过这个入口影响原型满意度。
    // 它不是最终评分系统，只让垂直切片能看到“服务压力会造成结果变差”。
    public void ApplyPrototypeServicePressure(string reason, int scoreDelta)
    {
        servicePressurePenaltyCount++;
        lastServicePressureResult = reason;
        RecordOutcome(Room2DOutcomeResult.Negative, scoreDelta);
        lastOutcomeSummary = reason + " -> " + lastOutcomeLabel;
        RefreshPrototypeDaySummary();
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
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference,
        Room2DEntity reservedRoom,
        bool useReservationFirst,
        Room2DEntity forcedRoom)
    {
        if (forcedRoom != null)
        {
            return forcedRoom.CanSimulateCheckIn()
                ? forcedRoom
                : null;
        }

        if (!useReservationFirst || reservedRoom == null)
        {
            return FindBestReadyRoomForDemand(demandType, roomPreference, floorPreference, facingPreference);
        }

        // 预留房只有 Ready 时才会被使用；房型不合也允许入住，但会被 Match/投诉逻辑惩罚。
        if (reservedRoom.CanSimulateCheckIn())
        {
            lastReservationResult = "Succeeded: used " + reservedRoom.roomName
                + GetRoomTypeRiskSuffix(reservedRoom, roomPreference);
            return reservedRoom;
        }

        lastReservationResult = "Failed: " + reservedRoom.roomName
            + " was " + reservedRoom.GetStateDisplayName()
            + " / " + reservedRoom.GetPrototypeRoomTypeDisplayName();
        return FindBestReadyRoomForDemand(demandType, roomPreference, floorPreference, facingPreference);
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
            return nextDemandType + " / " + nextDemandRoomPreference
                + " / " + nextDemandFloorPreference
                + " / " + nextDemandFacingPreference
                + " in " + FormatSeconds(Mathf.Max(0f, demandIntervalSeconds - demandTimerSeconds));
        }

        if (activeDemandWaitingForManualAssignment)
        {
            return "Active " + activeDemandType + " / "
                + BuildDemandPreferenceSummary(
                    activeDemandRoomPreference,
                    activeDemandFloorPreference,
                    activeDemandFacingPreference)
                + " waiting";
        }

        return upcomingDemandType + " / "
            + BuildDemandPreferenceSummary(
                upcomingDemandRoomPreference,
                upcomingDemandFloorPreference,
                upcomingDemandFacingPreference)
            + " in " + FormatSeconds(upcomingDemandEtaSeconds);
    }

    private Room2DDemandType GetCurrentVisibleDemandType()
    {
        if (activeDemandWaitingForManualAssignment)
        {
            return activeDemandType;
        }

        if (useUpcomingDemandPreview)
        {
            return upcomingDemandType;
        }

        return nextDemandType;
    }

    private Room2DRoomPreference GetCurrentVisibleDemandRoomPreference()
    {
        if (activeDemandWaitingForManualAssignment)
        {
            return activeDemandRoomPreference;
        }

        if (useUpcomingDemandPreview)
        {
            return upcomingDemandRoomPreference;
        }

        return nextDemandRoomPreference;
    }

    private Room2DFloorPreference GetCurrentVisibleDemandFloorPreference()
    {
        if (activeDemandWaitingForManualAssignment)
        {
            return activeDemandFloorPreference;
        }

        if (useUpcomingDemandPreview)
        {
            return upcomingDemandFloorPreference;
        }

        return nextDemandFloorPreference;
    }

    private Room2DFacingPreference GetCurrentVisibleDemandFacingPreference()
    {
        if (activeDemandWaitingForManualAssignment)
        {
            return activeDemandFacingPreference;
        }

        if (useUpcomingDemandPreview)
        {
            return upcomingDemandFacingPreference;
        }

        return nextDemandFacingPreference;
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
        int typeRiskCount = 0;

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null || !room.CanSimulateCheckIn())
            {
                continue;
            }

            bool typeRisk = !DoesRoomMatchRoomTypePreference(room, activeDemandRoomPreference);
            if (typeRisk)
            {
                typeRiskCount++;
            }

            if (shownCount < 5)
            {
                candidateText += "\n- " + room.roomName
                    + " (" + room.GetPrototypeRoomTypeDisplayName() + ")"
                    + " " + room.GetPrototypeFloorDisplayName()
                    + " / " + room.GetPrototypeFacingDisplayName()
                    + ": " + GetMatchDisplayName(EvaluateMatchQuality(
                        room,
                        activeDemandType,
                        activeDemandRoomPreference,
                        activeDemandFloorPreference,
                        activeDemandFacingPreference))
                    + " C/W " + GetCleanlinessSuitability(room)
                    + "/" + GetWearSuitability(room)
                    + " F/Fa " + GetFloorPreferenceScore(room, activeDemandFloorPreference)
                    + "/" + GetFacingPreferenceScore(room, activeDemandFacingPreference)
                    + (typeRisk ? " Type Risk" : "");
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

        if (typeRiskCount > 0)
        {
            candidateText += "\n- Type risk: " + typeRiskCount + " still assignable";
        }

        return candidateText;
    }

    private Room2DEntity FindBestReadyRoomForDemand(
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference)
    {
        Room2DEntity bestRoom = null;
        Room2DMatchQuality bestMatchQuality = Room2DMatchQuality.PoorMatch;
        int bestSuitabilityScore = -1;

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null
                || !rooms[i].CanSimulateCheckIn())
            {
                continue;
            }

            Room2DMatchQuality matchQuality = EvaluateMatchQuality(rooms[i], demandType, roomPreference, floorPreference, facingPreference);
            int suitabilityScore = GetCleanlinessSuitability(rooms[i])
                + GetWearSuitability(rooms[i])
                + GetRoomTypeSuitabilityBonus(rooms[i], roomPreference)
                + GetFloorPreferenceScore(rooms[i], floorPreference)
                + GetFacingPreferenceScore(rooms[i], facingPreference);

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

    private Room2DMatchQuality EvaluateMatchQuality(
        Room2DEntity room,
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference)
    {
        if (!DoesRoomMatchRoomTypePreference(room, roomPreference))
        {
            return Room2DMatchQuality.PoorMatch;
        }

        int cleanlinessSuitability = GetCleanlinessSuitability(room);
        int wearSuitability = GetWearSuitability(room);
        int thresholdModifier = GetRoomTypeThresholdModifier(room, roomPreference)
            + GetPreferenceThresholdModifier(GetFloorPreferenceScore(room, floorPreference))
            + GetPreferenceThresholdModifier(GetFacingPreferenceScore(room, facingPreference));

        // HighExpectation 对清洁和老旧程度都更敏感；Normal 只要求不要太差。
        if (demandType == Room2DDemandType.HighExpectation)
        {
            if (cleanlinessSuitability >= 80 + thresholdModifier && wearSuitability >= 75 + thresholdModifier)
            {
                return Room2DMatchQuality.GoodMatch;
            }

            if (cleanlinessSuitability >= 60 + thresholdModifier && wearSuitability >= 55 + thresholdModifier)
            {
                return Room2DMatchQuality.NormalMatch;
            }

            return Room2DMatchQuality.PoorMatch;
        }

        if (cleanlinessSuitability >= 70 + thresholdModifier && wearSuitability >= 60 + thresholdModifier)
        {
            return Room2DMatchQuality.GoodMatch;
        }

        if (cleanlinessSuitability >= 45 + thresholdModifier && wearSuitability >= 40 + thresholdModifier)
        {
            return Room2DMatchQuality.NormalMatch;
        }

        return Room2DMatchQuality.PoorMatch;
    }

    private int GetRoomTypeThresholdModifier(Room2DEntity room, Room2DRoomPreference roomPreference)
    {
        if (room == null)
        {
            return 0;
        }

        // Better 房在原型里代表更舒适/更体面，所以会稍微降低匹配门槛。
        if (room.prototypeRoomType == Room2DPrototypeRoomType.Better)
        {
            return roomPreference == Room2DRoomPreference.BetterRoomPreferred ? -10 : -4;
        }

        // 如果需求偏好 Better 房，而玩家选择 Standard 房，就稍微提高门槛。
        if (roomPreference == Room2DRoomPreference.BetterRoomPreferred)
        {
            return 8;
        }

        return 0;
    }

    private bool DoesRoomMatchRoomTypePreference(Room2DEntity room, Room2DRoomPreference roomPreference)
    {
        if (room == null)
        {
            return false;
        }

        // 当前垂直切片里，房型不再阻止入住，只作为投诉和匹配质量风险。
        if (roomPreference == Room2DRoomPreference.BetterRoomPreferred)
        {
            return room.prototypeRoomType == Room2DPrototypeRoomType.Better;
        }

        return true;
    }

    private string GetRoomTypeRiskSuffix(Room2DEntity room, Room2DRoomPreference roomPreference)
    {
        if (DoesRoomMatchRoomTypePreference(room, roomPreference))
        {
            return "";
        }

        return " (type risk)";
    }

    private int GetRoomTypeSuitabilityBonus(Room2DEntity room, Room2DRoomPreference roomPreference)
    {
        if (room == null)
        {
            return 0;
        }

        if (room.prototypeRoomType == Room2DPrototypeRoomType.Better)
        {
            return roomPreference == Room2DRoomPreference.BetterRoomPreferred ? 20 : 6;
        }

        return roomPreference == Room2DRoomPreference.BetterRoomPreferred ? -8 : 0;
    }

    private int GetFloorPreferenceScore(Room2DEntity room, Room2DFloorPreference floorPreference)
    {
        if (room == null || floorPreference == Room2DFloorPreference.NoPreference)
        {
            return 0;
        }

        // 当前原型没有完整楼层配置，先用 floorNumber 直接判断高/低楼层倾向。
        if (floorPreference == Room2DFloorPreference.HighFloorPreferred)
        {
            if (room.floorNumber >= 3)
            {
                return 12;
            }

            return room.floorNumber <= 1 ? -8 : 4;
        }

        if (room.floorNumber <= 1)
        {
            return 12;
        }

        return room.floorNumber >= 3 ? -8 : 4;
    }

    private int GetFacingPreferenceScore(Room2DEntity room, Room2DFacingPreference facingPreference)
    {
        if (room == null || facingPreference == Room2DFacingPreference.NoPreference)
        {
            return 0;
        }

        // BackFacing 代表更安静，StreetFacing 代表视野/临街感更强。
        if (facingPreference == Room2DFacingPreference.QuietPreferred)
        {
            return room.prototypeFacing == Room2DPrototypeFacing.BackFacing ? 12 : -8;
        }

        return room.prototypeFacing == Room2DPrototypeFacing.StreetFacing ? 12 : -8;
    }

    private int GetPreferenceThresholdModifier(int preferenceScore)
    {
        if (preferenceScore >= 10)
        {
            return -5;
        }

        if (preferenceScore < 0)
        {
            return 5;
        }

        return 0;
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

    private void RecordSuccessfulAssignmentOutcome(
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference,
        Room2DEntity room,
        Room2DMatchQuality matchQuality)
    {
        Room2DOutcomeResult outcomeResult = GetOutcomeFromMatchQuality(matchQuality);

        IncrementMatchCount(matchQuality);
        RecordOutcome(outcomeResult, GetScoreDelta(outcomeResult));

        string roomName = room != null ? room.roomName : "None";
        string roomTypeName = room != null ? room.GetPrototypeRoomTypeDisplayName() : "None";
        string floorName = room != null ? room.GetPrototypeFloorDisplayName() : "None";
        string facingName = room != null ? room.GetPrototypeFacingDisplayName() : "None";
        lastOutcomeSummary = demandType
            + " / " + BuildDemandPreferenceSummary(roomPreference, floorPreference, facingPreference)
            + " / " + roomName
            + " (" + roomTypeName + ") / " + lastMatchQualityLabel + " -> " + lastOutcomeLabel;
        lastOutcomeSummary += " / " + floorName + " / " + facingName;
    }

    private void RecordUnmetDemandOutcome(
        Room2DDemandType demandType,
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference,
        string reason)
    {
        // 没有 Ready 房时也算负面后果，因为玩家没有满足入住需求。
        RecordOutcome(Room2DOutcomeResult.Negative, -3);
        lastOutcomeSummary = demandType
            + " / " + BuildDemandPreferenceSummary(roomPreference, floorPreference, facingPreference)
            + " unmet / " + reason + " -> " + lastOutcomeLabel;
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

        if (roomComplaintCount > 0 && complaintReassignmentCount < roomComplaintCount)
        {
            return "Main issue: room complaint waiting";
        }

        if (compensationRequestCount > 0)
        {
            return "Main issue: compensation pressure";
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
        return status + " " + upcomingDemandType + " / " + upcomingDemandRoomPreference
            + " / " + upcomingDemandFloorPreference
            + " / " + upcomingDemandFacingPreference
            + " in " + FormatSeconds(upcomingDemandEtaSeconds);
    }

    private string BuildDemandPreferenceSummary(
        Room2DRoomPreference roomPreference,
        Room2DFloorPreference floorPreference,
        Room2DFacingPreference facingPreference)
    {
        return roomPreference + " / " + floorPreference + " / " + facingPreference;
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
