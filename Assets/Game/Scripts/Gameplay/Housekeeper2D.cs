using UnityEngine;

// 最小保洁原型。
// 目标不是做真实 AI 或寻路，而是证明“一个保洁同一时间只能清一个房间”会制造玩家选择压力。
public class Housekeeper2D : MonoBehaviour
{
    public enum HousekeeperState
    {
        Idle,
        Busy
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
        if (currentState != HousekeeperState.Busy)
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
        if (currentState != HousekeeperState.Idle || room == null)
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
        currentState = HousekeeperState.Busy;

        RefreshRoomVisual(room);
        RefreshOverview();
        return true;
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
        if (currentState != HousekeeperState.Busy)
        {
            return;
        }

        Room2DEntity roomToFinish = assignedRoom;

        assignedRoom = null;
        assignedRoomName = "None";
        cleaningTimerSeconds = 0f;
        currentState = HousekeeperState.Idle;

        if (roomToFinish != null && roomToFinish.CanFinishCleaning())
        {
            roomToFinish.FinishCleaning();
        }

        RefreshRoomVisual(roomToFinish);
        RefreshOverview();
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
