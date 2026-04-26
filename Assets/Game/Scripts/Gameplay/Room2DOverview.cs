using TMPro;
using UnityEngine;

public class Room2DOverview : MonoBehaviour
{
    public bool autoFindRoomsOnRefresh = true;
    public bool refreshSummaryDuringPlay = true;
    public float summaryRefreshInterval = 1f;
    public int prototypeStartFloor = 1;
    public int prototypeStartRoomNumber = 101;
    public bool numberRoomsByScenePosition = true;
    public Room2DEntity[] rooms;
    public Room2DController[] roomControllers;

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

    [ContextMenu("Set All Rooms Dirty")]
    public void SetAllRoomsDirty()
    {
        SetAllRoomsState(Room2DState.Dirty);
    }

    [ContextMenu("Set All Rooms Ready")]
    public void SetAllRoomsReady()
    {
        SetAllRoomsState(Room2DState.Ready);
    }

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
                    oldestDirtySeconds = Mathf.Max(oldestDirtySeconds, rooms[i].stateElapsedSeconds);
                    break;
            }

            if (rooms[i].guestCheckedOut)
            {
                checkedOutCount++;
            }
        }

        string summaryText = "Rooms  Dirty: " + dirtyCount
            + "  Cleaning: " + cleaningCount
            + "  Inspect: " + awaitingInspectionCount
            + "  Ready: " + readyCount
            + "  Occupied: " + occupiedCount
            + "  Blocked: " + blockedCount
            + "  Checked Out: " + checkedOutCount
            + "  Oldest Dirty: " + FormatSeconds(oldestDirtySeconds);

        if (summaryLabelTextMeshPro != null)
        {
            summaryLabelTextMeshPro.text = summaryText;
        }
    }

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
