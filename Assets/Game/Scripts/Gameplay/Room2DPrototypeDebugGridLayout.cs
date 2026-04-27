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
                rooms[i].roomEntity.SetIdentity(floorNumber, startRoomNumber + i);
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
