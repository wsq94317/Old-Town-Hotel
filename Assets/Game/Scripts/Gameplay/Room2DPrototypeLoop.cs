using UnityEngine;

public class Room2DPrototypeLoop : MonoBehaviour
{
    public bool autoFindReferences = true;
    public bool selectChangedRoom = true;
    public bool autoSimulateGuestFlowDuringPlay;
    public float autoGuestFlowIntervalSeconds = 12f;
    public Room2DOverview roomOverview;
    public Room2DSelectionManager selectionManager;
    public int simulatedCheckInCount;
    public int simulatedCheckoutCount;

    private float autoGuestFlowTimer;

    private void Start()
    {
        FindReferencesIfNeeded();
    }

    private void Update()
    {
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
        FindReferencesIfNeeded();

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
        FindReferencesIfNeeded();

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
        FindReferencesIfNeeded();

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
        autoSimulateGuestFlowDuringPlay = isEnabled;
        autoGuestFlowTimer = 0f;
    }

    public void ToggleAutoGuestFlow()
    {
        SetAutoGuestFlowEnabled(!autoSimulateGuestFlowDuringPlay);
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
