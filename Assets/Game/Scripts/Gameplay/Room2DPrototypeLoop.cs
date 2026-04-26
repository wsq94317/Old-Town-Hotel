using UnityEngine;

public class Room2DPrototypeLoop : MonoBehaviour
{
    public bool autoFindReferences = true;
    public bool selectCheckedOutRoom = true;
    public bool autoSimulateCheckoutDuringPlay;
    public float autoCheckoutIntervalSeconds = 12f;
    public Room2DOverview roomOverview;
    public Room2DSelectionManager selectionManager;
    public int simulatedCheckoutCount;

    private float autoCheckoutTimer;

    private void Start()
    {
        FindReferencesIfNeeded();
    }

    private void Update()
    {
        if (!autoSimulateCheckoutDuringPlay)
        {
            return;
        }

        autoCheckoutTimer += Time.deltaTime;
        if (autoCheckoutTimer < autoCheckoutIntervalSeconds)
        {
            return;
        }

        autoCheckoutTimer = 0f;
        SimulateNextCheckout();
    }

    [ContextMenu("Simulate Next Checkout")]
    public void SimulateNextCheckout()
    {
        FindReferencesIfNeeded();

        Room2DController room = FindFirstReadyRoom();
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

        if (selectCheckedOutRoom && selectionManager != null)
        {
            selectionManager.SelectRoom(room);
        }

        if (roomOverview != null)
        {
            roomOverview.RefreshSummary();
        }
    }

    public void SetAutoCheckoutEnabled(bool isEnabled)
    {
        autoSimulateCheckoutDuringPlay = isEnabled;
        autoCheckoutTimer = 0f;
    }

    public void ToggleAutoCheckout()
    {
        SetAutoCheckoutEnabled(!autoSimulateCheckoutDuringPlay);
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

            if (!rooms[i].roomEntity.CanSimulateCheckout())
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
