using System.Collections.Generic;
using UnityEngine;

// 房间对当前客人的适配等级（UI 适合/一般/不适合 徽标）。
// Suitable = 床型完全匹配；SoSo = 床型不匹配但仍 Ready（fallback）；
// Unsuitable = 已被过滤，不会出现在返回列表里（保留枚举值便于将来扩展）。
public enum RoomSuitabilityRank
{
    // 适合 —— 床型匹配（未来：+ soft prefs 也匹配）。
    Suitable,

    // 一般 —— 床型不匹配但房间是 Ready；玩家仍可强制入住（fallback）。
    // 未来 soft prefs 失败但床型匹配的情况也走这一档。
    SoSo,

    // 不适合 —— 当前用作 sentinel；GetReadyRoomsForGuest 不会返回此档（已过滤）。
    Unsuitable
}

// (Room, Rank) 元组型 value type，给 UI Modal 1 列表使用。
// 只读 struct：UI 侧不需要也不应该改 Rank。
public readonly struct RoomSuitability
{
    public readonly Room2DEntity Room;
    public readonly RoomSuitabilityRank Rank;

    public RoomSuitability(Room2DEntity room, RoomSuitabilityRank rank)
    {
        Room = room;
        Rank = rank;
    }
}

// 最小外部需求循环。
// 它不创建真实客人对象，只模拟”有人想入住”和”住满一段时间后退房”。
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

    [Header("Guest Identity")]
    // B+A Hybrid sprint：每位客人随机赋一个 type（3 种）+ preference（3 种）。
    // 这些字段与 upcomingDemand* / activeDemand* 平行追加，不替换或重命名现有字段，
    // 以避免触发 Room2DPrototypeDemandLoop（2080+ 行）里大量读取点的回归风险。
    // 详见 ADR 0006 — 3-Phase Day Structure。
    public Room2DGuestType upcomingGuestType = Room2DGuestType.Business;
    public Room2DGuestPreference upcomingGuestPreference = Room2DGuestPreference.QuietFloor;
    public Room2DGuestType activeGuestType = Room2DGuestType.Business;
    public Room2DGuestPreference activeGuestPreference = Room2DGuestPreference.QuietFloor;

    [Header("Multi-Slot Upcoming Queue (Story 3)")]
    // Story 3 Q1 方案 B：把 upcoming demand 升级为最多 N 个 slot 的队列。
    // 新 list 字段与旧 single 字段并行存在；旧字段镜像 slot[0]，保持 Story 2 测试绿色。
    // Phase 4（UI run）完成后再删除旧字段。
    [SerializeField] private int upcomingQueueCapacity = 2;

    // 各 upcoming slot 的队列数据（index 与 reservedRoomsForUpcomingDemands 一一对应）。
    private List<Room2DDemandType> _upcomingDemandTypes;
    private List<Room2DRoomPreference> _upcomingDemandRoomPreferences;
    private List<Room2DFloorPreference> _upcomingDemandFloorPreferences;
    private List<Room2DFacingPreference> _upcomingDemandFacingPreferences;
    private List<Room2DGuestType> _upcomingGuestTypes;
    private List<Room2DGuestPreference> _upcomingGuestPreferences;
    private List<Room2DBedTypePreference> _upcomingBedTypePreferences;

    // null 表示对应 slot 尚未预分配房间。
    private List<Room2DEntity> _reservedRoomsForUpcomingDemands;

    /// <summary>当前 upcoming 队列里有多少个 slot（≤ upcomingQueueCapacity）。</summary>
    public int UpcomingQueueCount => _upcomingGuestTypes != null ? _upcomingGuestTypes.Count : 0;

    /// <summary>只读视图，供 UI 和 Controller 读取当前所有预留房间（null = 未分配）。</summary>
    public IReadOnlyList<Room2DEntity> ReservedRoomsForUpcomingDemands =>
        _reservedRoomsForUpcomingDemands;

    // 默认关闭自动兜底分房：垂直切片里必须由玩家回到 Front Desk 手动完成入住。
    public bool allowAutomaticFallbackAssignment;
    public float manualAssignmentFallbackDelaySeconds = 8f;
    public Room2DEntity activeReservedRoomForFallback;
    public string activeReservedRoomName = "None";
    public string activeDemandStatus = "None";
    public string lastManualAssignmentResult = "None";
    public string lastAssignmentMode = "None";
    public string lastResolvedAssignmentMode = "None";

    [Header("Occupancy")]
    // Occupied 房间住满多少现实秒后自动退房。过夜模型：设为远大于一个营业日，
    // 让退房统一发生在次日清晨的退房潮（BeginMorningCheckoutWave）。
    public float occupiedDurationSeconds = 100000f;

    [Header("Morning checkout wave (overnight stays)")]
    [Tooltip("Seconds after the day opens before the first overnight guest checks out.")]
    public float checkoutWaveFirstDelaySeconds = 3f;
    [Tooltip("Stagger between successive overnight checkouts.")]
    public float checkoutWaveIntervalSeconds = 4f;
    [Tooltip("When a day opens with no overnight guests (fresh boot or loaded save — room occupancy isn't persisted), seed this many dirty rooms so housekeeping still has a morning.")]
    public int fallbackMorningDirtyRooms = 3;
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
    [Header("Economy checkout revenue (Phase 6)")]
    public EconomySystem economySystem;
    public RenovationSystem renovationSystem;

    // 入住时按房号记下匹配质量，退房时结算成收入 = 档次房价 × 满意度系数（economy GDD §4.1）。
    private readonly Dictionary<int, Room2DMatchQuality> _stayQualityByRoom =
        new Dictionary<int, Room2DMatchQuality>();
    private bool _economyRefsSearched;

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
        InitialiseUpcomingQueue();
        FindRoomsIfNeeded();
        ScheduleUpcomingDemandPreview();
        RefreshPrototypeDaySummary();
    }

    // 确保多 slot 列表在 Start 之前已分配；AddComponent 测试环境里 Awake 之后会立刻调用 public 方法，
    // 所以用惰性初始化保护所有入口（EnsureQueuesInitialised）。
    private void InitialiseUpcomingQueue()
    {
        if (_upcomingDemandTypes != null)
        {
            return;
        }

        int cap = Mathf.Max(1, upcomingQueueCapacity);
        _upcomingDemandTypes = new List<Room2DDemandType>(cap);
        _upcomingDemandRoomPreferences = new List<Room2DRoomPreference>(cap);
        _upcomingDemandFloorPreferences = new List<Room2DFloorPreference>(cap);
        _upcomingDemandFacingPreferences = new List<Room2DFacingPreference>(cap);
        _upcomingGuestTypes = new List<Room2DGuestType>(cap);
        _upcomingGuestPreferences = new List<Room2DGuestPreference>(cap);
        _upcomingBedTypePreferences = new List<Room2DBedTypePreference>(cap);
        _reservedRoomsForUpcomingDemands = new List<Room2DEntity>(cap);
    }

    // 惰性初始化保护：任何 public 方法在 Start 之前被调用时（EditMode 测试、AddComponent 立即调用）都能安全使用列表。
    private void EnsureQueuesInitialised()
    {
        if (_upcomingDemandTypes == null)
        {
            InitialiseUpcomingQueue();
        }
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
        EnsureQueuesInitialised();

        // 旧 single 字段：slot[0] 的快照，保持 Story 2 测试路径和 UI 不变。
        upcomingDemandType = nextDemandType;
        upcomingDemandRoomPreference = nextDemandRoomPreference;
        upcomingDemandFloorPreference = nextDemandFloorPreference;
        upcomingDemandFacingPreference = nextDemandFacingPreference;
        // 客人 minimal identity：每生成一个 upcoming demand，随机赋 type + preference。
        // PickRandom* 暴露 public 是为了 EditMode 测试可在不依赖场景的情况下校验分布。
        upcomingGuestType = PickRandomGuestType();
        upcomingGuestPreference = PickRandomGuestPreference();
        upcomingDemandEtaSeconds = Mathf.Max(0f, demandIntervalSeconds);
        upcomingDemandPreviewText = BuildUpcomingDemandPreviewText("Incoming");

        // 多 slot 队列填充：只补充缺少的 slot，不重置已存在的（避免覆盖玩家的预分配）。
        // 每次调用负责让队列达到 upcomingQueueCapacity 容量上限。
        int cap = Mathf.Max(1, upcomingQueueCapacity);
        int existing = _upcomingGuestTypes.Count;

        // slot[0] 镜像旧 single 字段（保证向后兼容）。
        if (existing == 0)
        {
            _upcomingDemandTypes.Add(upcomingDemandType);
            _upcomingDemandRoomPreferences.Add(upcomingDemandRoomPreference);
            _upcomingDemandFloorPreferences.Add(upcomingDemandFloorPreference);
            _upcomingDemandFacingPreferences.Add(upcomingDemandFacingPreference);
            _upcomingGuestTypes.Add(upcomingGuestType);
            _upcomingGuestPreferences.Add(upcomingGuestPreference);
            _upcomingBedTypePreferences.Add(PickRandomBedTypePreference(upcomingGuestType));
            _reservedRoomsForUpcomingDemands.Add(null);
        }
        else
        {
            // 更新 slot[0] 数据以保持与旧字段同步（仅在 slot 刚被 pop 后重建时发生）。
            _upcomingDemandTypes[0] = upcomingDemandType;
            _upcomingDemandRoomPreferences[0] = upcomingDemandRoomPreference;
            _upcomingDemandFloorPreferences[0] = upcomingDemandFloorPreference;
            _upcomingDemandFacingPreferences[0] = upcomingDemandFacingPreference;
            _upcomingGuestTypes[0] = upcomingGuestType;
            _upcomingGuestPreferences[0] = upcomingGuestPreference;
            _upcomingBedTypePreferences[0] = PickRandomBedTypePreference(upcomingGuestType);
            // 注意：不重置 slot[0] 的 reservedRoom，保留玩家已配对的状态。
        }

        // 填满剩余 slot（slot[1] ... slot[cap-1]）。
        for (int i = _upcomingGuestTypes.Count; i < cap; i++)
        {
            Room2DGuestType extraGuestType = PickRandomGuestType();
            Room2DGuestPreference extraGuestPref = PickRandomGuestPreference();
            _upcomingDemandTypes.Add(nextDemandType);
            _upcomingDemandRoomPreferences.Add(nextDemandRoomPreference);
            _upcomingDemandFloorPreferences.Add(nextDemandFloorPreference);
            _upcomingDemandFacingPreferences.Add(nextDemandFacingPreference);
            _upcomingGuestTypes.Add(extraGuestType);
            _upcomingGuestPreferences.Add(extraGuestPref);
            _upcomingBedTypePreferences.Add(PickRandomBedTypePreference(extraGuestType));
            _reservedRoomsForUpcomingDemands.Add(null);
        }
    }

    // ── 客人身份随机生成（测试种子） ──────────────────────────────────────
    // PickRandomGuestType / PickRandomGuestPreference 是 public 的最小测试 seam：
    //   - EditMode 测试用 UnityEngine.Random.InitState() 固定种子后调用 100 次
    //   - 不需要 rooms / overview / scene 依赖，纯粹返回枚举值
    //   - 修改随机分布逻辑（例如以后引入加权）只需改这两个方法

    public Room2DGuestType PickRandomGuestType()
    {
        int index = UnityEngine.Random.Range(0, 3);
        switch (index)
        {
            case 0: return Room2DGuestType.Business;
            case 1: return Room2DGuestType.Family;
            default: return Room2DGuestType.VIP;
        }
    }

    public Room2DGuestPreference PickRandomGuestPreference()
    {
        int index = UnityEngine.Random.Range(0, 3);
        switch (index)
        {
            case 0: return Room2DGuestPreference.QuietFloor;
            case 1: return Room2DGuestPreference.HighFloor;
            default: return Room2DGuestPreference.GroundFloor;
        }
    }

    // Story 3 Q2 方案 C：按客人类型生成 BedType 偏好分布。
    //   Business → Any 50% / Single 50%（经济舱客户，无硬约束或最低标准）
    //   Family   → Family 70% / Twin 30%（家庭客倾向 Family 房，少数能接受 Twin）
    //   VIP      → Single 60% / Family 40%（高端单人或套房）
    // 测试覆盖：DemandLoopMultiSlotTest 的 3 个分布断言。
    public Room2DBedTypePreference PickRandomBedTypePreference(Room2DGuestType guestType)
    {
        // UnityEngine.Random.value 区间 [0, 1)，便于配置百分比阈值。
        float roll = UnityEngine.Random.value;
        switch (guestType)
        {
            case Room2DGuestType.Business:
                // 50/50：Any vs Single
                return roll < 0.5f ? Room2DBedTypePreference.Any : Room2DBedTypePreference.Single;

            case Room2DGuestType.Family:
                // 70/30：Family vs Twin
                return roll < 0.7f ? Room2DBedTypePreference.Family : Room2DBedTypePreference.Twin;

            case Room2DGuestType.VIP:
                // 60/40：Single vs Family
                return roll < 0.6f ? Room2DBedTypePreference.Single : Room2DBedTypePreference.Family;

            default:
                // 未来若新增枚举值，默认 Any（无约束），便于平滑兼容。
                return Room2DBedTypePreference.Any;
        }
    }

    // Story 3 Phase 2：slot-indexed 读 seam，供 EditMode multi-slot 测试断言队列状态。
    // 读不到（index 越界或队列未初始化）时返回 enum 默认值并不抛异常 —— 测试侧自己判断。
    public Room2DGuestType GetUpcomingGuestType(int slotIndex)
    {
        if (_upcomingGuestTypes == null || slotIndex < 0 || slotIndex >= _upcomingGuestTypes.Count)
        {
            return default;
        }
        return _upcomingGuestTypes[slotIndex];
    }

    public Room2DBedTypePreference GetUpcomingBedTypePreference(int slotIndex)
    {
        if (_upcomingBedTypePreferences == null
            || slotIndex < 0
            || slotIndex >= _upcomingBedTypePreferences.Count)
        {
            return default;
        }
        return _upcomingBedTypePreferences[slotIndex];
    }

    public Room2DEntity GetReservedRoomAt(int slotIndex)
    {
        if (_reservedRoomsForUpcomingDemands == null
            || slotIndex < 0
            || slotIndex >= _reservedRoomsForUpcomingDemands.Count)
        {
            return null;
        }
        return _reservedRoomsForUpcomingDemands[slotIndex];
    }

    // AC6 回归保护用的 public 测试 seam：暴露 private DoesRoomMatchRoomTypePreference()。
    // 添加 guest type/preference 字段不应影响既有 room-type 匹配逻辑；
    // 该 seam 让 EditMode 测试无需 InternalsVisibleTo 即可断言行为不退化。
    public bool DoesRoomMatchRoomTypePreferenceForTesting(Room2DEntity room, Room2DRoomPreference roomPreference)
    {
        return DoesRoomMatchRoomTypePreference(room, roomPreference);
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
        // Story 3 Phase 2：保持 slot[0] mirror 同步（编辑器手动清空时多 slot 列表也要清，否则 Inspector 显示混淆）。
        EnsureQueuesInitialised();
        if (_reservedRoomsForUpcomingDemands.Count > 0)
        {
            _reservedRoomsForUpcomingDemands[0] = null;
        }
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

        // Slot 0 mirror（保持 Story 2 调用点不变）。
        EnsureQueuesInitialised();
        if (_reservedRoomsForUpcomingDemands.Count > 0)
        {
            _reservedRoomsForUpcomingDemands[0] = room;
        }
    }

    // Story 3 Phase 2：slot-indexed 预分配 overload。
    // 返回值 ok=true 表示成功;false 表示 slotIndex 越界或 room 为 null。
    //
    // 同房不可占两 slot 规则:若 room 已在另一个 slot 占用,先释放那个 slot。
    // 设计依据见 Story 3 AC5 第二段(切换 slot 后再点同房 → 旧 slot 解除)。
    //
    // 副作用契约与既有 1-arg 版本保持一致:更新 `lastReservationResult` / `lastPreparationAction` /
    // `preparationActionCount`。slot 0 的写入同时镜像到 legacy `reservedRoomForUpcomingDemand` 字段。
    public bool ReserveRoomForUpcomingDemand(int slotIndex, Room2DEntity room)
    {
        EnsureQueuesInitialised();

        if (room == null)
        {
            lastReservationResult = "Reserve failed: room is None";
            lastPreparationAction = lastReservationResult;
            return false;
        }

        if (slotIndex < 0 || slotIndex >= _reservedRoomsForUpcomingDemands.Count)
        {
            lastReservationResult = "Reserve failed: slot " + slotIndex + " out of range";
            lastPreparationAction = lastReservationResult;
            return false;
        }

        // 同房转移:若 room 已在另一个 slot,先释放旧 slot。
        for (int i = 0; i < _reservedRoomsForUpcomingDemands.Count; i++)
        {
            if (i != slotIndex && _reservedRoomsForUpcomingDemands[i] == room)
            {
                _reservedRoomsForUpcomingDemands[i] = null;
            }
        }

        _reservedRoomsForUpcomingDemands[slotIndex] = room;

        // Slot 0 镜像到 legacy 字段,保 Story 2 Showcase UI 行为。
        if (slotIndex == 0)
        {
            reservedRoomForUpcomingDemand = room;
            reservedRoomName = room.roomName;
        }
        else if (_reservedRoomsForUpcomingDemands[0] == null)
        {
            // Slot 0 此时是空的(可能刚被同房转移清掉),把 legacy 也清掉避免陈旧数据。
            reservedRoomForUpcomingDemand = null;
            reservedRoomName = "None";
        }

        lastReservationResult = "Reserved " + room.roomName + " for slot " + slotIndex;
        lastPreparationAction = lastReservationResult;
        preparationActionCount++;
        return true;
    }

    // Story 3 Phase 2:解除指定 slot 的房间预分配。
    // slotIndex 越界则静默 no-op(测试与 UI 均依赖此防御性行为,避免越界 throw)。
    public void ClearReservedRoom(int slotIndex)
    {
        EnsureQueuesInitialised();

        if (slotIndex < 0 || slotIndex >= _reservedRoomsForUpcomingDemands.Count)
        {
            return;
        }

        _reservedRoomsForUpcomingDemands[slotIndex] = null;

        if (slotIndex == 0)
        {
            // Slot 0 与 legacy 字段同步,UI 立即看到 "None"。
            reservedRoomForUpcomingDemand = null;
            reservedRoomName = "None";
            lastReservationResult = "Reservation cleared (slot 0)";
            lastPreparationAction = lastReservationResult;
        }
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

        EnsureQueuesInitialised();

        // FIFO pop：从多 slot 队列取 slot[0]，其余 slot 向前移一位（RemoveAt 做位移）。
        // 同时写回旧 single 字段（向后兼容 Story 2 路径）。
        Room2DDemandType demandType;
        Room2DRoomPreference roomPreference;
        Room2DFloorPreference floorPreference;
        Room2DFacingPreference facingPreference;
        Room2DEntity reservedRoom;

        if (_upcomingGuestTypes.Count > 0)
        {
            // 从多 slot 队列取值。
            demandType = _upcomingDemandTypes[0];
            roomPreference = _upcomingDemandRoomPreferences[0];
            floorPreference = _upcomingDemandFloorPreferences[0];
            facingPreference = _upcomingDemandFacingPreferences[0];
            reservedRoom = _reservedRoomsForUpcomingDemands[0];

            // 客人身份从 slot[0] 同步到 active 字段（旧路径兼容）。
            activeGuestType = _upcomingGuestTypes[0];
            activeGuestPreference = _upcomingGuestPreferences[0];

            // FIFO：移除 slot[0]，队列向左移位。
            _upcomingDemandTypes.RemoveAt(0);
            _upcomingDemandRoomPreferences.RemoveAt(0);
            _upcomingDemandFloorPreferences.RemoveAt(0);
            _upcomingDemandFacingPreferences.RemoveAt(0);
            _upcomingGuestTypes.RemoveAt(0);
            _upcomingGuestPreferences.RemoveAt(0);
            _upcomingBedTypePreferences.RemoveAt(0);
            _reservedRoomsForUpcomingDemands.RemoveAt(0);
        }
        else
        {
            // 队列为空时退回旧 single 字段（极端边界情况）。
            demandType = upcomingDemandType;
            roomPreference = upcomingDemandRoomPreference;
            floorPreference = upcomingDemandFloorPreference;
            facingPreference = upcomingDemandFacingPreference;
            reservedRoom = reservedRoomForUpcomingDemand;
            activeGuestType = upcomingGuestType;
            activeGuestPreference = upcomingGuestPreference;
        }

        // 旧 single 字段同步（保持 Story 2 调用点不变）。
        upcomingDemandType = demandType;
        upcomingDemandRoomPreference = roomPreference;
        upcomingDemandFloorPreference = floorPreference;
        upcomingDemandFacingPreference = facingPreference;

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

        if (!allowAutomaticFallbackAssignment
            || manualAssignmentFallbackDelaySeconds <= 0f
            || activeDemandWaitSeconds < manualAssignmentFallbackDelaySeconds)
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
            // 投诉离店按 PoorMatch 结算房费（住了但不满意）。
            SettleCheckoutRevenue(originalRoom, Room2DMatchQuality.PoorMatch);
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
        // B+A Hybrid sprint：Front Desk 调用此文本渲染 active demand 卡（Peak 阶段可见），
        // 因此在原 Type/Prefs 上方追加 Guest 和 Needs 两行，与 GetActiveDemandCardText() 对齐，
        // 满足 AC5「Front Desk 等候卡显示 3 项标签」。
        return "Active Demand\n"
            + "Status: " + activeDemandStatus + "\n"
            + "Guest: " + (activeDemandWaitingForManualAssignment
                ? activeGuestType + " / " + activeGuestPreference
                : "None") + "\n"
            + "Needs: " + (activeDemandWaitingForManualAssignment
                ? activeDemandRoomPreference.ToString()
                : "None") + "\n"
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
        // B+A Hybrid sprint：在已有 Type/Prefs/ETA 之外增加 Guest 行（type + preference）
        // 和 Needs 行（room preference 显式化），以满足 AC4「upcoming-guest 卡显示 3 项标签」。
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
            + "Guest: " + upcomingGuestType + " / " + upcomingGuestPreference + "\n"
            + "Needs: " + upcomingDemandRoomPreference + "\n"
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
        // B+A Hybrid sprint：在 Type/Prefs 之外增加 Guest（type+preference）与 Needs（room preference）
        // 两行，以满足 AC5「Front Desk 等候卡显示 3 项标签」。
        string demandTypeText = activeDemandWaitingForManualAssignment ? activeDemandType.ToString() : "None";
        string preferenceText = activeDemandWaitingForManualAssignment
            ? BuildDemandPreferenceSummary(activeDemandRoomPreference, activeDemandFloorPreference, activeDemandFacingPreference)
            : "None";
        string guestText = activeDemandWaitingForManualAssignment
            ? activeGuestType + " / " + activeGuestPreference
            : "None";
        string needsText = activeDemandWaitingForManualAssignment
            ? activeDemandRoomPreference.ToString()
            : "None";
        string waitText = activeDemandWaitingForManualAssignment
            ? FormatSeconds(activeDemandWaitSeconds) + " / " + FormatSeconds(manualAssignmentFallbackDelaySeconds)
            : "0s / " + FormatSeconds(manualAssignmentFallbackDelaySeconds);

        return "[Active Demand]\n"
            + "Status: " + activeDemandStatus + "\n"
            + "Guest: " + guestText + "\n"
            + "Needs: " + needsText + "\n"
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

    public bool HasReadyRoomForActiveFrontDeskDemand()
    {
        // 前台只关心“现在有没有能手动入住的 Ready 房”。
        // 房型/偏好风险不会阻止入住，只会影响结果和后续投诉。
        FindRoomsIfNeeded();

        if (complaintWaitingForReassignment)
        {
            return FindBestReadyRoomForDemand(
                complaintDemandType,
                complaintRoomPreference,
                complaintFloorPreference,
                complaintFacingPreference) != null;
        }

        if (activeDemandWaitingForManualAssignment)
        {
            return FindBestReadyRoomForDemand(
                activeDemandType,
                activeDemandRoomPreference,
                activeDemandFloorPreference,
                activeDemandFacingPreference) != null;
        }

        return FindBestReadyRoomForDemand(
            upcomingDemandType,
            upcomingDemandRoomPreference,
            upcomingDemandFloorPreference,
            upcomingDemandFacingPreference) != null;
    }

    // 前台 CTA 用：当前客人（以 bed-type 偏好表征）是否有任何可入住的 Ready 房？
    //
    // 与 HasReadyRoomForActiveFrontDeskDemand 不同：那个方法走匹配质量评估流程；
    // 这个方法只看 "Ready"，因为 UI Modal 1 允许玩家选 SoSo 房（床型不符也行）。
    // 因此只要有任意 Ready 房，结果就是 true。
    //
    // TODO: soft prefs —— 未来若 guest config 引入楼层/朝向硬约束，可在这里加 filter。
    public bool HasReadyRoomForGuest(Room2DBedTypePreference bedTypePreference)
    {
        FindRoomsIfNeeded();

        if (rooms == null)
        {
            return false;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] != null && rooms[i].CanSimulateCheckIn())
            {
                // bedTypePreference 在这里其实不影响布尔结果（SoSo 也算 fallback），
                // 但保留参数以便未来引入硬过滤时不破坏调用方签名。
                _ = bedTypePreference;
                return true;
            }
        }

        return false;
    }

    // UI Modal 1 列表用：返回每个 Ready 房 + 其适配等级，按 Suitable→SoSo、
    // 同档内按 roomNumber 升序排序。非 Ready 房直接过滤掉，不在结果中出现。
    //
    // 适配规则（与文档同步；prototype 暂只看床型）：
    //   - Ready 且 room.roomCategory 匹配 bedTypePreference → Suitable（适合）
    //   - Ready 且 不匹配                                  → SoSo  （一般）
    //   - bedTypePreference == Any → 全部 Ready 房算 Suitable（Business 无约束）
    //
    // TODO: soft prefs —— 未来 floor / facing / quiet 偏好失败可把 Suitable 降级为 SoSo。
    public IReadOnlyList<RoomSuitability> GetReadyRoomsForGuest(Room2DBedTypePreference bedTypePreference)
    {
        FindRoomsIfNeeded();

        var result = new List<RoomSuitability>();

        if (rooms == null)
        {
            return result;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null || !room.CanSimulateCheckIn())
            {
                continue;
            }

            RoomSuitabilityRank rank = EvaluateBedTypeSuitability(room, bedTypePreference);
            result.Add(new RoomSuitability(room, rank));
        }

        // 排序：Suitable 优先（rank 枚举值升序 = Suitable < SoSo < Unsuitable），
        // 同档内 roomNumber 升序。沿用项目内手写排序惯例（SortRoomsByRoomNumber 同款），
        // 不引入 LINQ —— 本仓库 Gameplay/ 下当前无 OrderBy/ThenBy 使用。
        for (int i = 0; i < result.Count - 1; i++)
        {
            for (int j = i + 1; j < result.Count; j++)
            {
                bool jWins = false;

                if ((int)result[j].Rank < (int)result[i].Rank)
                {
                    jWins = true;
                }
                else if (result[j].Rank == result[i].Rank
                    && GetRoomNumber(result[j].Room) < GetRoomNumber(result[i].Room))
                {
                    jWins = true;
                }

                if (jWins)
                {
                    RoomSuitability temp = result[i];
                    result[i] = result[j];
                    result[j] = temp;
                }
            }
        }

        return result;
    }

    // 床型适配判定 —— 单房视图。Any 视为无约束 → Suitable；
    // 其它 bedType 与 room.roomCategory 精确比对（与 Room2DPreAssignmentRules.MatchesCategory 同语义）。
    private RoomSuitabilityRank EvaluateBedTypeSuitability(
        Room2DEntity room,
        Room2DBedTypePreference bedTypePreference)
    {
        if (room == null)
        {
            return RoomSuitabilityRank.SoSo;
        }

        if (bedTypePreference == Room2DBedTypePreference.Any)
        {
            return RoomSuitabilityRank.Suitable;
        }

        switch (bedTypePreference)
        {
            case Room2DBedTypePreference.Single:
                return room.roomCategory == Room2DRoomCategory.Single
                    ? RoomSuitabilityRank.Suitable
                    : RoomSuitabilityRank.SoSo;
            case Room2DBedTypePreference.Twin:
                return room.roomCategory == Room2DRoomCategory.Twin
                    ? RoomSuitabilityRank.Suitable
                    : RoomSuitabilityRank.SoSo;
            case Room2DBedTypePreference.Family:
                return room.roomCategory == Room2DRoomCategory.Family
                    ? RoomSuitabilityRank.Suitable
                    : RoomSuitabilityRank.SoSo;
            default:
                return RoomSuitabilityRank.SoSo;
        }
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

    // 开门营业时调用：昨晚过夜的客人在清晨错峰退房 —— 逐间结算房费、产生脏房，
    // 保洁的一天从退房潮开始。没有过夜客时（新开局/读档）用"昨晚的烂摊子"垫场。
    public void BeginMorningCheckoutWave()
    {
        FindRoomsIfNeeded();
        if (rooms == null) return;

        int waveIndex = 0;
        foreach (var room in rooms)
        {
            if (room == null || room.currentState != Room2DState.Occupied) continue;
            // 让 ProcessOccupiedCheckouts 在 first + interval*i 秒后触发该房退房。
            room.stateElapsedSeconds = occupiedDurationSeconds
                - (checkoutWaveFirstDelaySeconds + checkoutWaveIntervalSeconds * waveIndex);
            waveIndex++;
        }

        if (waveIndex == 0 && fallbackMorningDirtyRooms > 0)
        {
            int seeded = 0;
            foreach (var room in rooms)
            {
                if (seeded >= fallbackMorningDirtyRooms) break;
                if (room == null || room.currentState != Room2DState.Ready) continue;
                room.currentState = Room2DState.Dirty;
                room.guestCheckedOut = true;
                room.stateElapsedSeconds = 0f;
                RefreshRoomVisual(room);
                seeded++;
            }
            if (seeded > 0) RefreshOverview();
        }
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
                SettleCheckoutRevenue(room);
                RefreshRoomVisual(room);
            }
        }

        RefreshOverview();
        RefreshPrototypeDaySummary();
    }

    // 结算一次退房：房间档次的 nightly rate × 该次入住的匹配质量系数，记入经济系统。
    // 场景里没有 EconomySystem 时静默跳过（日结走旧的按人头固定价路径）。
    private void SettleCheckoutRevenue(Room2DEntity room, Room2DMatchQuality? forcedQuality = null)
    {
        if (room == null) return;
        if (!_economyRefsSearched)
        {
            _economyRefsSearched = true;
            if (economySystem == null) economySystem = FindFirstObjectByType<EconomySystem>();
            if (renovationSystem == null) renovationSystem = FindFirstObjectByType<RenovationSystem>();
        }

        Room2DMatchQuality quality = Room2DMatchQuality.NormalMatch;
        if (_stayQualityByRoom.TryGetValue(room.roomNumber, out Room2DMatchQuality stored))
        {
            quality = stored;
            _stayQualityByRoom.Remove(room.roomNumber);
        }
        if (forcedQuality.HasValue) quality = forcedQuality.Value;

        if (economySystem == null || economySystem.Config == null) return;
        EconomyConfigSO cfg = economySystem.Config;

        int nightly = renovationSystem != null && renovationSystem.Config != null
            ? renovationSystem.Config.NightlyRevenueFor(renovationSystem.TierOf(room.roomNumber))
            : cfg.roomRevenuePerGuest;

        float mult;
        switch (quality)
        {
            case Room2DMatchQuality.GoodMatch:
                mult = cfg.goodMatchMultiplier;
                break;
            case Room2DMatchQuality.PoorMatch:
                mult = cfg.poorMatchMultiplier;
                break;
            default:
                mult = cfg.normalMatchMultiplier;
                break;
        }
        economySystem.RecordCheckout(nightly, mult);
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
        if (room != null) _stayQualityByRoom[room.roomNumber] = matchQuality;

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
