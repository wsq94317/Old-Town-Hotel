using UnityEngine;

// 最小查房主管原型。
// 目标不是做真实 AI 或失败率，而是证明“一个主管同一时间只能检查一个房间”会形成第二个瓶颈。
public class Inspector2D : MonoBehaviour
{
    public enum InspectorState
    {
        Idle,
        Traveling,
        Working
    }

    // 当前主管状态。Idle 可以接新房间，Busy 不能接新房间。
    public InspectorState currentState = InspectorState.Idle;

    // 自动寻找场景里的选择器和总览，减少手动拖引用。
    public bool autoFindReferences = true;

    // 可选：如果你已经用 Room2DSelectionManager 选择房间，可以直接分配当前选中的房间。
    public Room2DSelectionManager selectionManager;

    // 可选：用于 Inspector 测试。把 AwaitingInspection 房间拖到这里，再右键执行 Assign Target Room For Testing。
    public Room2DEntity targetRoomForTesting;

    // 当前正在检查的房间。Busy 时这里应该有值。
    public Room2DEntity assignedRoom;

    // 方便 Inspector 查看当前房间名。
    public string assignedRoomName = "None";

    // 检查需要的现实秒数。原型阶段先用固定时长。
    public float inspectionDurationSeconds = 4f;
    public float inspectionTimerSeconds;
    // 先预留 Traveling 数据位，后续可以在到房前加移动时间。
    public float travelDurationSeconds;
    public float travelTimerSeconds;

    // 可选：检查开始/结束后刷新总览。
    public Room2DOverview roomOverview;

    [Header("Prototype Best Target")]
    // 原型用：显示当前“最应该派主管检查”的房间，方便测试准备标记是否真的影响选择。
    public Room2DEntity bestInspectionTarget;
    public string bestInspectionTargetName = "None";
    public string bestInspectionTargetReason = "None";
    public bool preparationPriorityChangedTarget;
    public string lastBestAssignmentResult = "None";

    private void Start()
    {
        FindReferencesIfNeeded();
    }

    private void Update()
    {
        if (currentState == InspectorState.Traveling)
        {
            TickTravelingPlaceholder();
            return;
        }

        if (currentState != InspectorState.Working)
        {
            return;
        }

        inspectionTimerSeconds += Time.deltaTime;
        if (inspectionTimerSeconds >= inspectionDurationSeconds)
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

    [ContextMenu("Assign Best Inspection Target")]
    public void AssignBestRoom()
    {
        FindReferencesIfNeeded();

        Room2DEntity bestRoom = FindBestInspectionTarget(true);
        if (bestRoom == null)
        {
            lastBestAssignmentResult = "Best Insp failed: no AwaitingInspection room";
            RefreshBestTargetInfo();
            return;
        }

        bool assigned = AssignRoom(bestRoom);
        lastBestAssignmentResult = assigned
            ? "Best Insp assigned " + bestRoom.roomName
            : "Best Insp failed: " + bestRoom.roomName;

        RefreshBestTargetInfo();
    }

    public bool AssignRoom(Room2DEntity room)
    {
        if (!IsAvailableForAssignment() || room == null)
        {
            return false;
        }

        // 使用 Room2DEntity 自己的 guard，避免主管接到 Dirty / Cleaning / Ready / Occupied / Blocked 房间。
        if (!room.CanApproveInspection())
        {
            return false;
        }

        assignedRoom = room;
        assignedRoomName = room.roomName;
        inspectionTimerSeconds = 0f;
        currentState = InspectorState.Working;

        RefreshRoomVisual(room);
        RefreshOverview();
        return true;
    }

    public bool IsAvailableForAssignment()
    {
        return currentState == InspectorState.Idle;
    }

    public bool IsWorkingOnRoom()
    {
        return currentState == InspectorState.Working && assignedRoom != null;
    }

    public string GetCurrentTargetRoomName()
    {
        return assignedRoom != null ? assignedRoom.roomName : "None";
    }

    public string GetStatusDisplayName()
    {
        switch (currentState)
        {
            case InspectorState.Traveling:
                return "Traveling";
            case InspectorState.Working:
                return "Inspecting";
            default:
                return "Idle";
        }
    }

    public string GetBestTargetText()
    {
        RefreshBestTargetInfo();

        return "Best Insp: " + bestInspectionTargetName + "\n"
            + "Reason: " + bestInspectionTargetReason + "\n"
            + "Prep changed: " + (preparationPriorityChangedTarget ? "Yes" : "No") + "\n"
            + "Last best: " + lastBestAssignmentResult;
    }

    public void FinishCurrentRoom()
    {
        if (currentState != InspectorState.Working)
        {
            return;
        }

        Room2DEntity roomToFinish = assignedRoom;

        assignedRoom = null;
        assignedRoomName = "None";
        inspectionTimerSeconds = 0f;
        travelTimerSeconds = 0f;
        currentState = InspectorState.Idle;

        if (roomToFinish != null && roomToFinish.CanApproveInspection())
        {
            roomToFinish.ApproveInspection();
        }

        RefreshRoomVisual(roomToFinish);
        RefreshOverview();
    }

    private void TickTravelingPlaceholder()
    {
        // 本轮不实现真实移动；只保留 Traveling -> Working 的扩展入口。
        travelTimerSeconds += Time.deltaTime;

        if (travelDurationSeconds <= 0f)
        {
            currentState = InspectorState.Working;
            travelTimerSeconds = 0f;
            return;
        }

        if (travelTimerSeconds >= travelDurationSeconds)
        {
            currentState = InspectorState.Working;
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
        Room2DEntity preparedBestRoom = FindBestInspectionTarget(true);
        Room2DEntity normalBestRoom = FindBestInspectionTarget(false);

        bestInspectionTarget = preparedBestRoom;
        bestInspectionTargetName = preparedBestRoom != null ? preparedBestRoom.roomName : "None";
        bestInspectionTargetReason = GetInspectionTargetReason(preparedBestRoom);
        preparationPriorityChangedTarget = preparedBestRoom != null
            && normalBestRoom != null
            && preparedBestRoom != normalBestRoom;
    }

    private Room2DEntity FindBestInspectionTarget(bool usePreparationPriority)
    {
        Room2DEntity[] rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
        Room2DEntity bestRoom = null;

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null || !room.CanApproveInspection())
            {
                continue;
            }

            if (bestRoom == null || IsBetterInspectionTarget(room, bestRoom, usePreparationPriority))
            {
                bestRoom = room;
            }
        }

        return bestRoom;
    }

    private bool IsBetterInspectionTarget(Room2DEntity candidate, Room2DEntity currentBest, bool usePreparationPriority)
    {
        // 简单规则：准备标记 > 等待检查时间 > 房号。
        if (usePreparationPriority && candidate.markedInspectionPriority != currentBest.markedInspectionPriority)
        {
            return candidate.markedInspectionPriority;
        }

        if (!Mathf.Approximately(candidate.stateElapsedSeconds, currentBest.stateElapsedSeconds))
        {
            return candidate.stateElapsedSeconds > currentBest.stateElapsedSeconds;
        }

        return candidate.roomNumber < currentBest.roomNumber;
    }

    private string GetInspectionTargetReason(Room2DEntity room)
    {
        if (room == null)
        {
            return "None";
        }

        if (room.markedInspectionPriority)
        {
            return "INSP PRIO";
        }

        return "Oldest Inspect / " + Mathf.FloorToInt(room.stateElapsedSeconds) + "s";
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
