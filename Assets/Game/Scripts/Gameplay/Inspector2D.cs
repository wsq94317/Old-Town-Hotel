using UnityEngine;

// 最小查房主管原型。
// 目标不是做真实 AI 或失败率，而是证明“一个主管同一时间只能检查一个房间”会形成第二个瓶颈。
public class Inspector2D : MonoBehaviour
{
    public enum InspectorState
    {
        Idle,
        Busy
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

    // 可选：检查开始/结束后刷新总览。
    public Room2DOverview roomOverview;

    private void Start()
    {
        FindReferencesIfNeeded();
    }

    private void Update()
    {
        if (currentState != InspectorState.Busy)
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

    public bool AssignRoom(Room2DEntity room)
    {
        if (currentState != InspectorState.Idle || room == null)
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
        currentState = InspectorState.Busy;

        RefreshRoomVisual(room);
        RefreshOverview();
        return true;
    }

    public void FinishCurrentRoom()
    {
        if (currentState != InspectorState.Busy)
        {
            return;
        }

        Room2DEntity roomToFinish = assignedRoom;

        assignedRoom = null;
        assignedRoomName = "None";
        inspectionTimerSeconds = 0f;
        currentState = InspectorState.Idle;

        if (roomToFinish != null && roomToFinish.CanApproveInspection())
        {
            roomToFinish.ApproveInspection();
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
