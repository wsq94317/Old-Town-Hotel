using UnityEngine;

// 原型用客人循环。
// 它只模拟“入住”和“退房”，不代表最终前台/客人系统。
public class Room2DPrototypeLoop : MonoBehaviour
{
    [Header("Legacy Guard")]
    // 当前测试应使用 Room2DPrototypeDemandLoop。这个旧循环默认锁住，避免误触发入住/退房污染测试。
    public bool enableLegacyPrototypeLoop;
    public string legacyLoopStatus = "Disabled: use Room2DPrototypeDemandLoop";

    // 自动寻找场景引用，方便初学阶段快速测试。
    public bool autoFindReferences = true;

    // 执行动作后是否自动选中发生变化的房间。
    public bool selectChangedRoom = true;

    // 默认关闭。打开后会按间隔自动模拟入住/退房。
    public bool autoSimulateGuestFlowDuringPlay;
    public float autoGuestFlowIntervalSeconds = 12f;

    public Room2DOverview roomOverview;
    public Room2DSelectionManager selectionManager;

    // 原型调试计数。
    public int simulatedCheckInCount;
    public int simulatedCheckoutCount;

    private float autoGuestFlowTimer;

    private void Start()
    {
        FindReferencesIfNeeded();
    }

    private void Update()
    {
        if (!enableLegacyPrototypeLoop)
        {
            return;
        }

        if (!autoSimulateGuestFlowDuringPlay)
        {
            return;
        }

        autoGuestFlowTimer += Time.deltaTime;
        if (autoGuestFlowTimer < autoGuestFlowIntervalSeconds)
        {
            return;
        }

        autoGuestFlowTimer = 0f;
        SimulateNextGuestStep();
    }

    [ContextMenu("Simulate Next Guest Step")]
    public void SimulateNextGuestStep()
    {
        if (!CanUseLegacyLoop())
        {
            return;
        }

        FindReferencesIfNeeded();

        // 如果有客人在住，优先模拟退房；否则找一个 Ready 房间模拟入住。
        if (FindFirstOccupiedRoom() != null)
        {
            SimulateNextCheckout();
        }
        else
        {
            SimulateNextCheckIn();
        }
    }

    [ContextMenu("Simulate Next Checkout")]
    public void SimulateNextCheckout()
    {
        if (!CanUseLegacyLoop())
        {
            return;
        }

        FindReferencesIfNeeded();

        // 退房只能发生在 Occupied 房间。
        Room2DController room = FindFirstOccupiedRoom();
        if (room == null || room.roomEntity == null)
        {
            return;
        }

        if (!room.roomEntity.SimulateCheckout())
        {
            return;
        }

        simulatedCheckoutCount++;
        room.ApplyStateVisual();

        if (selectChangedRoom && selectionManager != null)
        {
            selectionManager.SelectRoom(room);
        }

        if (roomOverview != null)
        {
            roomOverview.RefreshSummary();
        }
    }

    [ContextMenu("Simulate Next Check In")]
    public void SimulateNextCheckIn()
    {
        if (!CanUseLegacyLoop())
        {
            return;
        }

        FindReferencesIfNeeded();

        // 入住只能发生在 Ready 房间。
        Room2DController room = FindFirstReadyRoom();
        if (room == null || room.roomEntity == null)
        {
            return;
        }

        if (!room.roomEntity.SimulateCheckIn())
        {
            return;
        }

        simulatedCheckInCount++;
        room.ApplyStateVisual();

        if (selectChangedRoom && selectionManager != null)
        {
            selectionManager.SelectRoom(room);
        }

        if (roomOverview != null)
        {
            roomOverview.RefreshSummary();
        }
    }

    public void SetAutoGuestFlowEnabled(bool isEnabled)
    {
        if (!enableLegacyPrototypeLoop)
        {
            autoSimulateGuestFlowDuringPlay = false;
            legacyLoopStatus = "Blocked: enable legacy loop first";
            return;
        }

        autoSimulateGuestFlowDuringPlay = isEnabled;
        autoGuestFlowTimer = 0f;
    }

    public void ToggleAutoGuestFlow()
    {
        SetAutoGuestFlowEnabled(!autoSimulateGuestFlowDuringPlay);
    }

    private bool CanUseLegacyLoop()
    {
        if (enableLegacyPrototypeLoop)
        {
            legacyLoopStatus = "Enabled: legacy loop can mutate room states";
            return true;
        }

        legacyLoopStatus = "Blocked: current testing uses Room2DPrototypeDemandLoop";
        return false;
    }

    private void FindReferencesIfNeeded()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }

        if (selectionManager == null)
        {
            selectionManager = FindFirstObjectByType<Room2DSelectionManager>();
        }

        if (roomOverview != null)
        {
            roomOverview.FindRoomsInScene();
        }
    }

    private Room2DController FindFirstReadyRoom()
    {
        Room2DController[] rooms = GetRoomControllers();
        Room2DController firstReadyRoom = null;

        // 原型阶段用房号最小的 Ready 房间，不做复杂分配策略。
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null || rooms[i].roomEntity == null)
            {
                continue;
            }

            if (!rooms[i].roomEntity.CanSimulateCheckIn())
            {
                continue;
            }

            if (firstReadyRoom == null || GetRoomNumber(rooms[i]) < GetRoomNumber(firstReadyRoom))
            {
                firstReadyRoom = rooms[i];
            }
        }

        return firstReadyRoom;
    }

    private Room2DController FindFirstOccupiedRoom()
    {
        Room2DController[] rooms = GetRoomControllers();
        Room2DController firstOccupiedRoom = null;

        // 原型阶段用房号最小的 Occupied 房间。
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null || rooms[i].roomEntity == null)
            {
                continue;
            }

            if (!rooms[i].roomEntity.CanSimulateCheckout())
            {
                continue;
            }

            if (firstOccupiedRoom == null || GetRoomNumber(rooms[i]) < GetRoomNumber(firstOccupiedRoom))
            {
                firstOccupiedRoom = rooms[i];
            }
        }

        return firstOccupiedRoom;
    }

    private Room2DController[] GetRoomControllers()
    {
        // 优先复用 Room2DOverview 已经找到的房间列表。
        if (roomOverview != null && roomOverview.roomControllers != null && roomOverview.roomControllers.Length > 0)
        {
            return roomOverview.roomControllers;
        }

        return FindObjectsByType<Room2DController>(FindObjectsSortMode.None);
    }

    private int GetRoomNumber(Room2DController room)
    {
        if (room != null && room.roomEntity != null)
        {
            return room.roomEntity.roomNumber;
        }

        return int.MaxValue;
    }
}
