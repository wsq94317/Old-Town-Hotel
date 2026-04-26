using TMPro;
using UnityEngine;

// 场景级房间总览。
// 负责统计所有房间的状态数量，并刷新总览 UI。
public class Room2DOverview : MonoBehaviour
{
    // 打开后，每次刷新都会重新在场景里找房间，适合原型阶段频繁复制房间。
    public bool autoFindRoomsOnRefresh = true;

    // Play 模式下定时刷新总览，让等待时间、Blocked 数量等能更新。
    public bool refreshSummaryDuringPlay = true;
    public float summaryRefreshInterval = 1f;

    // 原型房号生成设置。
    public int prototypeStartFloor = 1;
    public int prototypeStartRoomNumber = 101;
    public bool numberRoomsByScenePosition = true;

    // 房间数据和控制器列表。可以手动拖，也可以自动查找。
    public Room2DEntity[] rooms;
    public Room2DController[] roomControllers;

    [Header("Cleaning Priority")]
    // 当前最需要清洁的 Dirty 房。Blocked/Occupied/Ready 不会进入这个选择。
    public Room2DEntity highestPriorityDirtyRoom;
    public string highestPriorityDirtyRoomName = "None";
    public int highestPriorityDirtyLevel;
    public float highestPriorityDirtySeconds;

    [Header("Optional UI")]
    public TMP_Text summaryLabelTextMeshPro;

    private float summaryRefreshTimer;

    private void Start()
    {
        FindRoomsIfNeeded();
        RefreshSummary();
    }

    private void Update()
    {
        if (!refreshSummaryDuringPlay)
        {
            return;
        }

        summaryRefreshTimer += Time.deltaTime;
        if (summaryRefreshTimer < summaryRefreshInterval)
        {
            return;
        }

        summaryRefreshTimer = 0f;
        RefreshSummary();
    }

    [ContextMenu("Find Rooms In Scene")]
    public void FindRoomsInScene()
    {
        rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
        roomControllers = FindObjectsByType<Room2DController>(FindObjectsSortMode.None);
        RefreshSummary();
    }

    // 手动刷新所有房间视觉，适合 Inspector 改完数据后测试。
    [ContextMenu("Refresh All Room Visuals")]
    public void RefreshAllRoomVisuals()
    {
        FindRoomsIfNeeded();

        for (int i = 0; i < roomControllers.Length; i++)
        {
            if (roomControllers[i] != null)
            {
                roomControllers[i].ApplyStateVisual();
            }
        }

        RefreshSummary();
    }

    // 原型工具：把所有房间重置成 Dirty。
    [ContextMenu("Set All Rooms Dirty")]
    public void SetAllRoomsDirty()
    {
        SetAllRoomsState(Room2DState.Dirty);
    }

    // 原型工具：把所有房间重置成 Ready。
    [ContextMenu("Set All Rooms Ready")]
    public void SetAllRoomsReady()
    {
        SetAllRoomsState(Room2DState.Ready);
    }

    // 根据场景位置给房间分配房号，方便复制多个房间后快速整理。
    [ContextMenu("Assign Prototype Room Numbers")]
    public void AssignPrototypeRoomNumbers()
    {
        FindRoomsIfNeeded();
        SortRoomsForPrototypeNumbering();

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] != null)
            {
                rooms[i].SetIdentity(prototypeStartFloor, prototypeStartRoomNumber + i);
            }
        }

        RefreshAllRoomVisuals();
    }

    // 统计各类房态，并把文本写到 summaryLabelTextMeshPro。
    public void RefreshSummary()
    {
        FindRoomsIfNeeded();

        int dirtyCount = 0;
        int cleaningCount = 0;
        int awaitingInspectionCount = 0;
        int readyCount = 0;
        int occupiedCount = 0;
        int blockedCount = 0;
        int checkedOutCount = 0;
        float oldestDirtySeconds = 0f;
        highestPriorityDirtyRoom = null;
        highestPriorityDirtyRoomName = "None";
        highestPriorityDirtyLevel = 0;
        highestPriorityDirtySeconds = 0f;

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null)
            {
                continue;
            }

            switch (rooms[i].currentState)
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
                    rooms[i].RefreshCleaningPriority();
                    oldestDirtySeconds = Mathf.Max(oldestDirtySeconds, rooms[i].stateElapsedSeconds);
                    UpdateHighestPriorityDirtyRoom(rooms[i]);
                    break;
            }

            if (rooms[i].guestCheckedOut)
            {
                checkedOutCount++;
            }
        }

        // 这里先用一行文本，后面正式 UI 可以拆成多个 Text 或图标。
        string summaryText = "Rooms  Dirty: " + dirtyCount
            + "  Cleaning: " + cleaningCount
            + "  Inspect: " + awaitingInspectionCount
            + "  Ready: " + readyCount
            + "  Occupied: " + occupiedCount
            + "  Blocked: " + blockedCount
            + "  Checked Out: " + checkedOutCount
            + "  Oldest Dirty: " + FormatSeconds(oldestDirtySeconds)
            + "  Urgent: " + highestPriorityDirtyRoomName + " " + FormatSeconds(highestPriorityDirtySeconds);

        if (summaryLabelTextMeshPro != null)
        {
            summaryLabelTextMeshPro.text = summaryText;
        }
    }

    public Room2DEntity GetHighestPriorityDirtyRoom()
    {
        RefreshSummary();
        return highestPriorityDirtyRoom;
    }

    // 自动找房间，减少手动绑定成本。
    private void FindRoomsIfNeeded()
    {
        if (autoFindRoomsOnRefresh || rooms == null || rooms.Length == 0)
        {
            rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
        }

        if (autoFindRoomsOnRefresh || roomControllers == null || roomControllers.Length == 0)
        {
            roomControllers = FindObjectsByType<Room2DController>(FindObjectsSortMode.None);
        }
    }

    // 批量设置房态的共用方法。
    private void SetAllRoomsState(Room2DState newState)
    {
        FindRoomsIfNeeded();

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] != null)
            {
                rooms[i].SetState(newState);
                rooms[i].actionCount = 0;
            }
        }

        RefreshAllRoomVisuals();
    }

    // 原型阶段简单排序：上面的房间先编号，同一行左边先编号。
    private void SortRoomsForPrototypeNumbering()
    {
        if (!numberRoomsByScenePosition || rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Length - 1; i++)
        {
            for (int j = i + 1; j < rooms.Length; j++)
            {
                if (ShouldRoomComeBefore(rooms[j], rooms[i]))
                {
                    Room2DEntity tempRoom = rooms[i];
                    rooms[i] = rooms[j];
                    rooms[j] = tempRoom;
                }
            }
        }
    }

    // 判断 candidateRoom 是否应该排在 currentRoom 前面。
    private bool ShouldRoomComeBefore(Room2DEntity candidateRoom, Room2DEntity currentRoom)
    {
        if (candidateRoom == null)
        {
            return false;
        }

        if (currentRoom == null)
        {
            return true;
        }

        Vector3 candidatePosition = candidateRoom.transform.position;
        Vector3 currentPosition = currentRoom.transform.position;

        if (!Mathf.Approximately(candidatePosition.y, currentPosition.y))
        {
            return candidatePosition.y > currentPosition.y;
        }

        return candidatePosition.x < currentPosition.x;
    }

    // 用等待时间选择最紧急的 Dirty 房；同样时间时选房号更小的。
    private void UpdateHighestPriorityDirtyRoom(Room2DEntity room)
    {
        if (room == null || !room.IsWaitingForCleaning())
        {
            return;
        }

        bool shouldReplace = highestPriorityDirtyRoom == null
            || room.stateElapsedSeconds > highestPriorityDirtySeconds
            || (Mathf.Approximately(room.stateElapsedSeconds, highestPriorityDirtySeconds) && room.roomNumber < highestPriorityDirtyRoom.roomNumber);

        if (!shouldReplace)
        {
            return;
        }

        highestPriorityDirtyRoom = room;
        highestPriorityDirtyRoomName = room.roomName;
        highestPriorityDirtyLevel = room.cleaningPriorityLevel;
        highestPriorityDirtySeconds = room.stateElapsedSeconds;
    }

    // 把秒数格式化成 10s 或 2m 5s。
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
