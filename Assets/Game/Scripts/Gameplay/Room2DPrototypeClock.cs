using UnityEngine;

public class Room2DPrototypeClock : MonoBehaviour
{
    public bool autoFindReferences = true;
    public bool advanceTimeDuringPlay;
    public float gameHoursPerRealSecond = 1f;
    public Room2DOverview roomOverview;
    public Room2DEntity[] rooms;
    public float elapsedGameHours;

    private void Start()
    {
        FindRoomsIfNeeded();
    }

    private void Update()
    {
        if (!advanceTimeDuringPlay)
        {
            return;
        }

        AdvanceTime(Time.deltaTime * gameHoursPerRealSecond);
    }

    [ContextMenu("Find Rooms In Scene")]
    public void FindRoomsInScene()
    {
        rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }
    }

    [ContextMenu("Advance One Game Hour")]
    public void AdvanceOneGameHour()
    {
        AdvanceTime(1f);
    }

    [ContextMenu("Advance One Game Day")]
    public void AdvanceOneGameDay()
    {
        AdvanceTime(24f);
    }

    public void AdvanceTime(float gameHours)
    {
        if (gameHours <= 0f)
        {
            return;
        }

        FindRoomsIfNeeded();
        elapsedGameHours += gameHours;

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] != null)
            {
                rooms[i].AdvanceBlockTime(gameHours);
            }
        }

        if (roomOverview != null)
        {
            roomOverview.RefreshAllRoomVisuals();
        }
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
}
