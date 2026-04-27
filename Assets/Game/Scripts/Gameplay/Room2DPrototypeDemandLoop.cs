using UnityEngine;

// 最小外部需求循环。
// 它不创建真实客人对象，只模拟“有人想入住”和“住满一段时间后退房”。
public class Room2DPrototypeDemandLoop : MonoBehaviour
{
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

    private void Start()
    {
        FindRoomsIfNeeded();
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
        FindRoomsIfNeeded();
        generatedDemandCount++;

        Room2DEntity readyRoom = FindFirstReadyRoom();
        if (readyRoom == null)
        {
            unmetDemandCount++;
            lastDemandResult = "Unmet: no Ready room";
            lastChangedRoomName = "None";
            return;
        }

        // 使用 Room2DEntity 自己的入住 guard，避免把 Dirty / Blocked / Occupied 房间错误入住。
        if (!readyRoom.SimulateCheckIn())
        {
            unmetDemandCount++;
            lastDemandResult = "Unmet: check-in guard blocked";
            lastChangedRoomName = readyRoom.roomName;
            return;
        }

        successfulDemandCount++;
        lastDemandResult = "Assigned to " + readyRoom.roomName;
        lastChangedRoomName = readyRoom.roomName;

        RefreshRoomVisual(readyRoom);
        RefreshOverview();
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

    private Room2DEntity FindFirstReadyRoom()
    {
        Room2DEntity firstReadyRoom = null;

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null || !rooms[i].CanSimulateCheckIn())
            {
                continue;
            }

            if (firstReadyRoom == null || rooms[i].roomNumber < firstReadyRoom.roomNumber)
            {
                firstReadyRoom = rooms[i];
            }
        }

        return firstReadyRoom;
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
