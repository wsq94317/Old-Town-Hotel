using TMPro;
using UnityEngine;

public class Room2DOverview : MonoBehaviour
{
    public bool autoFindRoomsOnRefresh = true;
    public Room2DEntity[] rooms;
    public Room2DController[] roomControllers;

    [Header("Optional UI")]
    public TMP_Text summaryLabelTextMeshPro;

    private void Start()
    {
        FindRoomsIfNeeded();
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

    public void RefreshSummary()
    {
        FindRoomsIfNeeded();

        int dirtyCount = 0;
        int cleaningCount = 0;
        int awaitingInspectionCount = 0;
        int readyCount = 0;

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
                default:
                    dirtyCount++;
                    break;
            }
        }

        string summaryText = "Rooms  Dirty: " + dirtyCount
            + "  Cleaning: " + cleaningCount
            + "  Inspect: " + awaitingInspectionCount
            + "  Ready: " + readyCount;

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
}
