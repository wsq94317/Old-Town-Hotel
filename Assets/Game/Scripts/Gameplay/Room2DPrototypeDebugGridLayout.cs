using UnityEngine;

// 临时调试脚本：把多个 Room2D 房间自动摆到 Game 窗口内，方便测试多房间优先级。
// 这个脚本不是正式布局系统；等房间 prefab 和调试流程稳定后可以删除。
public class Room2DPrototypeDebugGridLayout : MonoBehaviour
{
    // 自动寻找场景里的房间，避免手动拖 8-12 个引用。
    public bool autoFindRooms = true;
    public Room2DController[] rooms;

    // 用哪个相机的 Game 窗口范围来摆放。为空时会自动找 Main Camera。
    public Camera targetCamera;

    // 网格列数。9 个房间时 3 列比较容易看。
    public int columns = 3;

    // 只使用屏幕中间偏右区域，避开左上角的调试 UI。
    [Range(0f, 1f)] public float leftViewport = 0.35f;
    [Range(0f, 1f)] public float rightViewport = 0.92f;
    [Range(0f, 1f)] public float bottomViewport = 0.18f;
    [Range(0f, 1f)] public float topViewport = 0.78f;

    // 是否按摆放顺序重新分配房号，方便复制 prefab 后快速得到 Room 101, 102...
    public bool assignRoomNumbersAfterArrange = true;
    public int floorNumber = 1;
    public int startRoomNumber = 101;

    // Story 3.5 多层编号支持。设为 true 时:按 floorPlan 数组依次拆分房间到各楼层,
    // 每层独立编号(楼层 N 起号 = N*100+1)。设为 false 时:沿用旧单层连续编号。
    //
    // 示例:floorPlan = [6, 4, 2] 表示 12 房按 6/4/2 拆到 floor 1/2/3,得到:
    //   floor 1 → 101..106(6 房,与 Rule 0 Single 对应)
    //   floor 2 → 201..204(4 房,与 Rule 1 Twin 对应)
    //   floor 3 → 301..302(2 房,与 Rule 2 Family 对应)
    [Header("Multi-Floor Plan (Story 3.5)")]
    public bool useMultiFloorNumbering = false;
    public int[] floorPlan = { 6, 4, 2 };

    // 保留每个房间当前 Z，避免破坏你现有的 2D 层级/相机关系。
    public bool preserveRoomZ = true;
    public float fallbackRoomZ;

    // 摆放后自动刷新颜色和总览。
    public bool refreshVisualsAfterArrange = true;
    public Room2DOverview roomOverview;

    [ContextMenu("Find Rooms In Scene")]
    public void FindRoomsInScene()
    {
        rooms = FindObjectsByType<Room2DController>(FindObjectsSortMode.None);
        SortRoomsByNumberOrName();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            targetCamera = FindFirstObjectByType<Camera>();
        }

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }
    }

    [ContextMenu("Arrange Rooms In Camera Grid")]
    public void ArrangeRoomsInCameraGrid()
    {
        FindRoomsIfNeeded();

        if (rooms == null || rooms.Length == 0 || targetCamera == null)
        {
            return;
        }

        int safeColumns = Mathf.Max(1, columns);
        int rows = Mathf.CeilToInt((float)rooms.Length / safeColumns);

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null)
            {
                continue;
            }

            int row = i / safeColumns;
            int column = i % safeColumns;
            float xPercent = GetGridPercent(column, safeColumns);
            float yPercent = GetGridPercent(row, rows);
            float viewportX = Mathf.Lerp(leftViewport, rightViewport, xPercent);
            float viewportY = Mathf.Lerp(topViewport, bottomViewport, yPercent);

            Transform roomTransform = rooms[i].transform;
            float roomZ = preserveRoomZ ? roomTransform.position.z : fallbackRoomZ;
            roomTransform.position = GetWorldPositionFromViewport(viewportX, viewportY, roomZ);

            if (assignRoomNumbersAfterArrange && rooms[i].roomEntity != null)
            {
                // Story 3.5：可选多层编号。若 useMultiFloorNumbering = true,
                // 按 floorPlan 数组把房间拆到各楼层,每层独立起号。
                if (useMultiFloorNumbering)
                {
                    GetFloorAndRoomNumber(i, out int derivedFloor, out int derivedRoomNumber);
                    rooms[i].roomEntity.SetIdentity(derivedFloor, derivedRoomNumber);
                }
                else
                {
                    // 旧单层逻辑(向后兼容)。
                    rooms[i].roomEntity.SetIdentity(floorNumber, startRoomNumber + i);
                }
            }

            if (refreshVisualsAfterArrange)
            {
                rooms[i].ApplyStateVisual();
            }
        }

        if (roomOverview != null)
        {
            roomOverview.FindRoomsInScene();
            roomOverview.RefreshSummary();
        }
    }

    private void FindRoomsIfNeeded()
    {
        if (autoFindRooms || rooms == null || rooms.Length == 0)
        {
            FindRoomsInScene();
        }
    }

    // Story 3.5：根据 floorPlan 数组把全局 index 映射到 (floor, roomNumber)。
    //   - 从 floor 1 开始,逐层累加;index 落入哪层,该层的本地序号 + 100*层号 + 1 即为 roomNumber
    //   - floorPlan = [6,4,2] 时:index 0..5 → floor 1 101..106;index 6..9 → floor 2 201..204;index 10..11 → floor 3 301..302
    //   - floorPlan 数组容量不够覆盖所有 index 时,溢出的 index 全部归到最后一层(防御性兜底)
    private void GetFloorAndRoomNumber(int globalIndex, out int derivedFloor, out int derivedRoomNumber)
    {
        if (floorPlan == null || floorPlan.Length == 0)
        {
            // 退化为单层 fallback。
            derivedFloor = floorNumber;
            derivedRoomNumber = startRoomNumber + globalIndex;
            return;
        }

        int cumulative = 0;
        for (int f = 0; f < floorPlan.Length; f++)
        {
            int floorCapacity = Mathf.Max(0, floorPlan[f]);
            if (globalIndex < cumulative + floorCapacity)
            {
                int localIndex = globalIndex - cumulative;
                derivedFloor = f + 1; // floor 编号从 1 开始
                derivedRoomNumber = (f + 1) * 100 + 1 + localIndex;
                return;
            }
            cumulative += floorCapacity;
        }

        // 溢出 floorPlan 总容量 → 归到最后一层(防御)。
        int lastFloor = floorPlan.Length;
        int overflow = globalIndex - cumulative;
        derivedFloor = lastFloor;
        derivedRoomNumber = lastFloor * 100 + floorPlan[lastFloor - 1] + 1 + overflow;
    }

    private float GetGridPercent(int index, int count)
    {
        if (count <= 1)
        {
            return 0.5f;
        }

        return (float)index / (count - 1);
    }

    private Vector3 GetWorldPositionFromViewport(float viewportX, float viewportY, float roomZ)
    {
        float distanceFromCamera = Mathf.Abs(roomZ - targetCamera.transform.position.z);
        Vector3 worldPosition = targetCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, distanceFromCamera));
        return new Vector3(worldPosition.x, worldPosition.y, roomZ);
    }

    private void SortRoomsByNumberOrName()
    {
        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Length - 1; i++)
        {
            for (int j = i + 1; j < rooms.Length; j++)
            {
                if (ShouldRoomComeBefore(rooms[j], rooms[i]))
                {
                    Room2DController tempRoom = rooms[i];
                    rooms[i] = rooms[j];
                    rooms[j] = tempRoom;
                }
            }
        }
    }

    private bool ShouldRoomComeBefore(Room2DController candidateRoom, Room2DController currentRoom)
    {
        if (candidateRoom == null)
        {
            return false;
        }

        if (currentRoom == null)
        {
            return true;
        }

        int candidateNumber = GetRoomNumber(candidateRoom);
        int currentNumber = GetRoomNumber(currentRoom);

        if (candidateNumber != currentNumber)
        {
            return candidateNumber < currentNumber;
        }

        return string.CompareOrdinal(candidateRoom.name, currentRoom.name) < 0;
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
