using UnityEngine;

// 原型用游戏时钟。
// 目前只负责推进 Blocked 房间的剩余时间，不做完整日期、班次、客人 ETA。
public class Room2DPrototypeClock : MonoBehaviour
{
    // 自动在场景里寻找房间和总览，减少手动拖引用。
    public bool autoFindReferences = true;

    // 默认关闭，避免 Play 后时间自动飞快流逝。测试时可以手动打开。
    public bool advanceTimeDuringPlay;

    // 1 表示现实 1 秒 = 游戏 1 小时。
    public float gameHoursPerRealSecond = 1f;

    public Room2DOverview roomOverview;
    public Room2DEntity[] rooms;

    // 已经推进过的游戏小时数，方便 Inspector 里观察。
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

    // 推进游戏时间，并让所有 Blocked 房间减少剩余时长。
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

        // 时间变化后刷新房间颜色和总览。
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
