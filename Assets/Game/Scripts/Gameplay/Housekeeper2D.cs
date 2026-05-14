using UnityEngine;

// 最小保洁原型。
// 目标不是做真实 AI 或寻路，而是证明“一个保洁同一时间只能清一个房间”会制造玩家选择压力。
public class Housekeeper2D : MonoBehaviour
{
    public enum HousekeeperState
    {
        Idle,
        Traveling,
        Working
    }

    // 当前保洁状态。Idle 可以接新房间，Busy 不能接新房间。
    public HousekeeperState currentState = HousekeeperState.Idle;

    // 可选：自动寻找场景里的选择器和总览，减少手动拖引用。
    public bool autoFindReferences = true;

    // 可选：如果你已经用 Room2DSelectionManager 选择房间，可以直接分配当前选中的房间。
    public Room2DSelectionManager selectionManager;

    // 可选：用于 Inspector 测试。把一个 Dirty 房间拖到这里，再右键执行 Assign Target Room For Testing。
    public Room2DEntity targetRoomForTesting;

    // 当前正在清洁的房间。Busy 时这里应该有值。
    public Room2DEntity assignedRoom;

    // 方便 Inspector 查看当前房间名。
    public string assignedRoomName = "None";

    // 清洁需要的现实秒数。原型阶段先用固定时长。
    public float cleaningDurationSeconds = 5f;
    public float cleaningTimerSeconds;
    // 先把 Traveling 的数据位准备好，后续再接真实移动/路径时间。
    public float travelDurationSeconds;
    public float travelTimerSeconds;

    // 可选：清洁开始/结束后刷新总览。
    public Room2DOverview roomOverview;

    [Header("Prototype Best Target")]
    // 原型用：显示当前“最应该派 HSK 去清洁”的房间，方便测试准备标记是否真的影响选择。
    public Room2DEntity bestHousekeepingTarget;
    public string bestHousekeepingTargetName = "None";
    public string bestHousekeepingTargetReason = "None";
    public bool preparationPriorityChangedTarget;
    public string lastBestAssignmentResult = "None";

    private void Start()
    {
        FindReferencesIfNeeded();
    }

    private void Update()
    {
        if (currentState == HousekeeperState.Traveling)
        {
            TickTravelingPlaceholder();
            return;
        }

        if (currentState != HousekeeperState.Working)
        {
            return;
        }

        cleaningTimerSeconds += Time.deltaTime;
        if (cleaningTimerSeconds >= cleaningDurationSeconds)
        {
            FinishCurrentRoom();
        }
    }

    [ContextMenu("Assign Selected Room")]
    public void AssignSelectedRoom()
    {
        FindReferencesIfNeeded();

        if (selectionManager == null || selectionManager.selectedRoom == null)
        {
            return;
        }

        AssignRoom(selectionManager.selectedRoom.roomEntity);
    }

    [ContextMenu("Assign Target Room For Testing")]
    public void AssignTargetRoomForTesting()
    {
        AssignRoom(targetRoomForTesting);
    }

    [ContextMenu("Assign Best Housekeeping Target")]
    public void AssignBestRoom()
    {
        FindReferencesIfNeeded();

        Room2DEntity bestRoom = FindBestHousekeepingTarget(true);
        if (bestRoom == null)
        {
            lastBestAssignmentResult = "Best HSK failed: no Dirty room";
            RefreshBestTargetInfo();
            return;
        }

        bool assigned = AssignRoom(bestRoom);
        lastBestAssignmentResult = assigned
            ? "Best HSK assigned " + bestRoom.roomName
            : "Best HSK failed: " + bestRoom.roomName;

        RefreshBestTargetInfo();
    }

    public bool AssignRoom(Room2DEntity room)
    {
        if (!IsAvailableForAssignment() || room == null)
        {
            return false;
        }

        // 使用 Room2DEntity 自己的 guard，避免保洁接到 Occupied / Ready / Blocked 房间。
        if (!room.StartCleaning())
        {
            return false;
        }

        assignedRoom = room;
        assignedRoomName = room.roomName;
        cleaningTimerSeconds = 0f;
        currentState = HousekeeperState.Working;

        RefreshRoomVisual(room);
        RefreshOverview();
        return true;
    }

    // Rooms 页面以后会直接读这些方法，不需要自己猜 Busy/Idle 规则。
    public bool IsAvailableForAssignment()
    {
        return currentState == HousekeeperState.Idle;
    }

    // ── UI 只读访问器（ui-spec.md §6 — HSK 卡片 / Modal 4 → Modal 5 门控） ───
    //
    // 这些 getter 仅暴露既有运行时字段，不引入新游戏状态：
    //   IsBusy                  → 非 Idle 即视为 Busy（Traveling / Working）
    //   CurrentActivityLabel    → 中文活动标签，便于 UI 直接绑定
    //   RemainingSeconds        → 当前 Working 剩余清洁秒数，类型 float 与现有字段一致
    //   AssignedRoomNumber      → 当前清洁中房号（null 表示空闲）

    /// <summary>HSK 是否处于 Busy 状态（非 Idle）。Modal 4 → Modal 5 门控读取此值。</summary>
    public bool IsBusy => currentState != HousekeeperState.Idle;

    /// <summary>Current activity label (English — editor has no CJK font, see memory: in-game-text-english-only).</summary>
    public string CurrentActivityLabel
    {
        get
        {
            switch (currentState)
            {
                case HousekeeperState.Working:   return "Cleaning";
                case HousekeeperState.Traveling: return "En route";
                default:                          return "Idle";
            }
        }
    }

    /// <summary>
    /// 当前 Working 状态下剩余清洁秒数。Idle / Traveling 时返回 0。
    /// 类型为 float 与 cleaningTimerSeconds / cleaningDurationSeconds 保持一致。
    /// </summary>
    public float RemainingSeconds
    {
        get
        {
            if (currentState != HousekeeperState.Working)
            {
                return 0f;
            }

            float remaining = cleaningDurationSeconds - cleaningTimerSeconds;
            return remaining > 0f ? remaining : 0f;
        }
    }

    /// <summary>
    /// 当前分配房间的房号；HSK Idle 或无房间时返回 null。
    /// UI HSK 卡片 target 字段绑定此 getter。
    /// </summary>
    public int? AssignedRoomNumber => assignedRoom != null ? assignedRoom.roomNumber : (int?)null;

    public bool IsWorkingOnRoom()
    {
        return currentState == HousekeeperState.Working && assignedRoom != null;
    }

    public string GetCurrentTargetRoomName()
    {
        return assignedRoom != null ? assignedRoom.roomName : "None";
    }

    public string GetStatusDisplayName()
    {
        switch (currentState)
        {
            case HousekeeperState.Traveling:
                return "Traveling";
            case HousekeeperState.Working:
                return "Cleaning";
            default:
                return "Idle";
        }
    }

    public string GetBestTargetText()
    {
        RefreshBestTargetInfo();

        return "Best HSK: " + bestHousekeepingTargetName + "\n"
            + "Reason: " + bestHousekeepingTargetReason + "\n"
            + "Prep changed: " + (preparationPriorityChangedTarget ? "Yes" : "No") + "\n"
            + "Last best: " + lastBestAssignmentResult;
    }

    public void FinishCurrentRoom()
    {
        if (currentState != HousekeeperState.Working)
        {
            return;
        }

        Room2DEntity roomToFinish = assignedRoom;

        assignedRoom = null;
        assignedRoomName = "None";
        cleaningTimerSeconds = 0f;
        travelTimerSeconds = 0f;
        currentState = HousekeeperState.Idle;

        if (roomToFinish != null && roomToFinish.CanFinishCleaning())
        {
            roomToFinish.FinishCleaning();
        }

        RefreshRoomVisual(roomToFinish);
        RefreshOverview();
    }

    private void TickTravelingPlaceholder()
    {
        // 本轮不实现真实移动；这里先保留状态入口，后续可在进入房间前增加 travel time。
        travelTimerSeconds += Time.deltaTime;

        if (travelDurationSeconds <= 0f)
        {
            currentState = HousekeeperState.Working;
            travelTimerSeconds = 0f;
            return;
        }

        if (travelTimerSeconds >= travelDurationSeconds)
        {
            currentState = HousekeeperState.Working;
            travelTimerSeconds = 0f;
        }
    }

    private void FindReferencesIfNeeded()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (selectionManager == null)
        {
            selectionManager = FindFirstObjectByType<Room2DSelectionManager>();
        }

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }
    }

    private void RefreshBestTargetInfo()
    {
        Room2DEntity preparedBestRoom = FindBestHousekeepingTarget(true);
        Room2DEntity normalBestRoom = FindBestHousekeepingTarget(false);

        bestHousekeepingTarget = preparedBestRoom;
        bestHousekeepingTargetName = preparedBestRoom != null ? preparedBestRoom.roomName : "None";
        bestHousekeepingTargetReason = GetHousekeepingTargetReason(preparedBestRoom);
        preparationPriorityChangedTarget = preparedBestRoom != null
            && normalBestRoom != null
            && preparedBestRoom != normalBestRoom;
    }

    private Room2DEntity FindBestHousekeepingTarget(bool usePreparationPriority)
    {
        Room2DEntity[] rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
        Room2DEntity bestRoom = null;

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null || !room.CanStartCleaning())
            {
                continue;
            }

            room.RefreshCleaningPriority();

            if (bestRoom == null || IsBetterHousekeepingTarget(room, bestRoom, usePreparationPriority))
            {
                bestRoom = room;
            }
        }

        return bestRoom;
    }

    private bool IsBetterHousekeepingTarget(Room2DEntity candidate, Room2DEntity currentBest, bool usePreparationPriority)
    {
        // 简单规则：准备标记 > 清洁优先级 > Dirty 等待时间 > 房号。
        if (usePreparationPriority && candidate.markedCleaningPriority != currentBest.markedCleaningPriority)
        {
            return candidate.markedCleaningPriority;
        }

        if (candidate.cleaningPriorityLevel != currentBest.cleaningPriorityLevel)
        {
            return candidate.cleaningPriorityLevel > currentBest.cleaningPriorityLevel;
        }

        if (!Mathf.Approximately(candidate.stateElapsedSeconds, currentBest.stateElapsedSeconds))
        {
            return candidate.stateElapsedSeconds > currentBest.stateElapsedSeconds;
        }

        return candidate.roomNumber < currentBest.roomNumber;
    }

    private string GetHousekeepingTargetReason(Room2DEntity room)
    {
        if (room == null)
        {
            return "None";
        }

        if (room.markedCleaningPriority)
        {
            return "CLEAN PRIO";
        }

        return room.cleaningPriorityLabel + " / " + Mathf.FloorToInt(room.stateElapsedSeconds) + "s";
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
        FindReferencesIfNeeded();

        if (roomOverview != null)
        {
            roomOverview.RefreshSummary();
        }
    }
}
