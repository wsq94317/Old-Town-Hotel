using UnityEngine;

// Story 3.7 重构(2026-05-14):场景房间初始化的唯一权威组件。
//
// 替代品(都可以从场景删除):
//   - Room2DPrototypeDebugGridLayout(老 debug 工具,职责重叠)
//   - Room2DSceneRoomGridLayout(我中途加的 Story 3 helper,职责被吸收)
//
// 保留(职责清晰):
//   - Room2DOverview(统计 / 状态汇总,不再做编号)
//   - Room2DPrototypeRoomConfigApplier(规则应用引擎,被本组件 composition)
//   - Room2DLevelConfigSO(唯一配置源)
//
// Pipeline(一键完成,不允许中途打断):
//   1. Find all Room2DController in scene
//   2. Sort them by Hierarchy 顺序(或按场景位置,可配)
//   3. Position 到 camera-relative grid(可关)
//   4. Assign identity:按 LevelConfigSO.roomsPerFloor 拆楼层 + 独立编号
//   5. Invoke RoomConfigApplier.ApplyRulesToRooms()(读 LevelConfigSO 规则写属性)
//   6. Refresh visuals via Room2DController.ApplyStateVisual
//
// 触发方式:
//   - Play 时 Start() 自动跑(runOnStart=true 默认)— 玩家不需要手动点 ContextMenu
//   - Edit 时也可用 ContextMenu "Setup Scene Rooms Now"
public class Room2DSceneSetup : MonoBehaviour
{
    [Header("Source of Truth")]
    [Tooltip("唯一配置源 SO。必须设置;读取 roomsPerFloor / roomRules / defaults。")]
    public Room2DLevelConfigSO levelConfig;

    [Header("Run Strategy")]
    [Tooltip("Play 时 Start() 自动执行完整 setup pipeline。关掉则只能手动 ContextMenu 触发。")]
    public bool runOnStart = true;

    [Header("Layout — Camera Viewport Grid")]
    [Tooltip("用哪个 Camera 计算 viewport 内的房间摆放位置。空时自动用 Main Camera。")]
    public Camera targetCamera;

    [Tooltip("每行多少个房间。9 / 12 房常用值:3 列(4 行)或 4 列(3 行)。手机 9:16 portrait 推荐 3 列。")]
    public int gridColumns = 3;

    // Viewport 默认值:9:16 portrait 模式居中。
    //   - 水平 [0.1, 0.9] = 80% 居中(左右各 10% padding)
    //   - 垂直 [0.18, 0.78] = 60% 居中(顶 22% 留给 HUD,底 18% 留给 nav bar)
    // 旧默认 left=0.35 是为了"避开左上 debug UI",现已不需要。
    [Range(0f, 1f)] public float leftViewport = 0.1f;
    [Range(0f, 1f)] public float rightViewport = 0.9f;
    [Range(0f, 1f)] public float bottomViewport = 0.18f;
    [Range(0f, 1f)] public float topViewport = 0.78f;

    [Tooltip("保留 prefab 原本的 Z 坐标(避免破坏 2D 层级)。")]
    public bool preserveRoomZ = true;
    public float fallbackRoomZ;

    [Tooltip("关掉则只做 identity + properties,不动场景里房间的位置。")]
    public bool repositionRooms = true;

    [Tooltip("勾上后,自动测量第一间房的 Renderer.bounds 视觉宽/高,把 viewport 范围向内收缩 半个房间宽/高 —— 边缘房间的视觉边贴齐 viewport 边而不是越界。")]
    public bool respectRoomVisualSize = true;

    [Header("Internal references(autofind)")]
    public Room2DController[] roomControllers;
    public Room2DPrototypeRoomConfigApplier roomConfigApplier;
    public Room2DOverview roomOverview;
    // Story 3.7:demandLoop 也是 room list 的消费者(Prep panel / Front Desk 都读它的 rooms 数组)。
    // SceneSetup pipeline 末尾必须同步推一遍,否则 Prep panel 看到的房间数会落后于场景实际数量。
    public Room2DPrototypeDemandLoop demandLoop;

    [Header("Debug Result")]
    [Tooltip("最近一次 setup 的总结,用于 Inspector 快速验证。")]
    public string lastSetupResult = "None";
    public int lastRoomCount;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        // Story 3.6:UI 重构后,Room2DController 的 OnGUI 调试 label 永远关闭。
        // 这是 static flag,所有 Room2DController 实例都受影响。绕过 prefab 内 Inspector
        // 可能存在的 showPrototypeDebugLabel=true override —— 确保新 UI 永远不被 IMGUI 文字盖。
        Room2DController.hidePrototypeDebugLabelsGlobally = true;
    }

    private void Start()
    {
        if (!runOnStart)
        {
            return;
        }

        SetupSceneRooms();
    }

    [ContextMenu("Setup Scene Rooms Now")]
    public void SetupSceneRooms()
    {
        FindReferencesIfNeeded();

        if (roomControllers == null || roomControllers.Length == 0)
        {
            lastSetupResult = "FAILED: No Room2DController found in scene.";
            return;
        }

        if (levelConfig == null)
        {
            lastSetupResult = "FAILED: levelConfig is null. Drag a Room2DLevelConfigSO asset into the slot.";
            return;
        }

        // 0. Room 数量 vs LevelConfig.roomsPerFloor 总和 mismatch 检查
        // 单一配置源原则:场景里房间数必须等于 LevelConfig 配的总和;不一致即时报警。
        int sceneRoomCount = roomControllers.Length;
        int configTotal = 0;
        if (levelConfig.roomsPerFloor != null)
        {
            for (int i = 0; i < levelConfig.roomsPerFloor.Length; i++)
            {
                configTotal += Mathf.Max(0, levelConfig.roomsPerFloor[i]);
            }
        }
        if (configTotal > 0 && sceneRoomCount != configTotal)
        {
            // Warning,不 abort —— pipeline 继续跑,但 user 应该知道有 mismatch
            Debug.LogWarning("[Room2DSceneSetup] Scene has " + sceneRoomCount + " rooms but LevelConfig.roomsPerFloor sums to " + configTotal
                + ". 多余/少的房间会落到溢出处理或缺失楼层。请同步:要么场景调整为 " + configTotal + " 个 prefab,要么修改 LevelConfig.roomsPerFloor 数组。", this);
        }

        // 1. 排序(按 Hierarchy + roomNumber + name 综合)
        SortRoomsForDeterministicOrder();

        // 2. 摆放(可关)
        if (repositionRooms)
        {
            ArrangeRoomsInCameraGrid();
        }

        // 3. 按 LevelConfig.roomsPerFloor 分配 identity
        AssignFloorPlanIdentity();

        // 4. 应用规则(roomType / facing / roomCategory)
        ApplyRoomPropertyRules();

        // 5. 同步 demandLoop.rooms(单一房间数源 — Prep panel + Front Desk 都读这里)
        PropagateRoomListToDemandLoop();

        // 6. 刷新视觉
        RefreshAllVisuals();

        lastRoomCount = roomControllers.Length;
        int demandLoopCount = (demandLoop != null && demandLoop.rooms != null) ? demandLoop.rooms.Length : 0;
        bool mismatch = configTotal > 0 && sceneRoomCount != configTotal;
        string prefix = mismatch ? "WARNING — " : "Setup OK — ";
        lastSetupResult = prefix
            + "scene " + sceneRoomCount + " rooms"
            + " / LevelConfig configTotal " + configTotal
            + " / demandLoop.rooms " + demandLoopCount
            + " / floors " + (levelConfig.roomsPerFloor != null ? levelConfig.roomsPerFloor.Length : 0)
            + " (LevelConfig: " + levelConfig.levelName + ")";
    }

    // ── Pipeline steps ─────────────────────────────────────────────────────

    private void FindReferencesIfNeeded()
    {
        if (roomControllers == null || roomControllers.Length == 0)
        {
            roomControllers = FindObjectsByType<Room2DController>(FindObjectsSortMode.None);
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (roomConfigApplier == null)
        {
            roomConfigApplier = FindFirstObjectByType<Room2DPrototypeRoomConfigApplier>();
        }

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }

        if (demandLoop == null)
        {
            demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        }
    }

    private void SortRoomsForDeterministicOrder()
    {
        if (roomControllers == null) return;

        // 简单冒泡:Hierarchy index → roomNumber → name 三层 fallback。
        // 这样保证每次 setup 顺序一致,floor 分配确定。
        for (int i = 0; i < roomControllers.Length - 1; i++)
        {
            for (int j = i + 1; j < roomControllers.Length; j++)
            {
                if (ShouldComeBefore(roomControllers[j], roomControllers[i]))
                {
                    Room2DController tmp = roomControllers[i];
                    roomControllers[i] = roomControllers[j];
                    roomControllers[j] = tmp;
                }
            }
        }
    }

    private bool ShouldComeBefore(Room2DController candidate, Room2DController current)
    {
        if (candidate == null) return false;
        if (current == null) return true;

        // Hierarchy sibling index 优先(场景里上下顺序就是楼层顺序)。
        int candIdx = candidate.transform.GetSiblingIndex();
        int currIdx = current.transform.GetSiblingIndex();
        if (candIdx != currIdx) return candIdx < currIdx;

        // 同 Hierarchy 位置时用 name 字典序。
        return string.CompareOrdinal(candidate.name, current.name) < 0;
    }

    private void ArrangeRoomsInCameraGrid()
    {
        if (targetCamera == null) return;

        int safeCols = Mathf.Max(1, gridColumns);
        int rows = Mathf.CeilToInt(roomControllers.Length / (float)safeCols);

        // 测量第一间房的视觉尺寸,把 viewport 边界向内收缩 半宽/半高
        // —— 这样 leftmost/rightmost 房的视觉边缘贴齐 viewport 边,而不是中心(锚点)贴齐导致越界。
        float halfWidthVp = 0f;
        float halfHeightVp = 0f;
        if (respectRoomVisualSize)
        {
            ComputeRoomVisualHalfSizeInViewport(out halfWidthVp, out halfHeightVp);
        }

        float effectiveLeft = leftViewport + halfWidthVp;
        float effectiveRight = rightViewport - halfWidthVp;
        float effectiveBottom = bottomViewport + halfHeightVp;
        float effectiveTop = topViewport - halfHeightVp;

        // 防御性:若收缩后区间反向(房间视觉太大塞不下),回退到原始 viewport 范围
        if (effectiveRight <= effectiveLeft) { effectiveLeft = leftViewport; effectiveRight = rightViewport; }
        if (effectiveTop <= effectiveBottom) { effectiveBottom = bottomViewport; effectiveTop = topViewport; }

        for (int i = 0; i < roomControllers.Length; i++)
        {
            if (roomControllers[i] == null) continue;

            int row = i / safeCols;
            int col = i % safeCols;
            float xPct = GetGridPercent(col, safeCols);
            float yPct = GetGridPercent(row, rows);
            float vpX = Mathf.Lerp(effectiveLeft, effectiveRight, xPct);
            float vpY = Mathf.Lerp(effectiveTop, effectiveBottom, yPct);

            Transform t = roomControllers[i].transform;
            float z = preserveRoomZ ? t.position.z : fallbackRoomZ;
            t.position = ViewportToWorld(vpX, vpY, z);
        }
    }

    // Story 3.7:测量房间视觉尺寸(以第一间房的所有 Renderer 合并 bounds 为准),
    // 转换成 viewport 比例。后续 lerp 范围会减去这个半宽/半高,保证视觉边贴齐 viewport 边。
    private void ComputeRoomVisualHalfSizeInViewport(out float halfWidthVp, out float halfHeightVp)
    {
        halfWidthVp = 0f;
        halfHeightVp = 0f;

        if (roomControllers == null || roomControllers.Length == 0 || roomControllers[0] == null || targetCamera == null)
        {
            return;
        }

        Renderer[] renderers = roomControllers[0].GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        // 合并所有 Renderer.bounds 得到房间整体世界空间 AABB
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        // 世界 X 半宽 → viewport 半宽
        Vector3 worldCenter = bounds.center;
        Vector3 leftWorld = new Vector3(worldCenter.x - bounds.extents.x, worldCenter.y, worldCenter.z);
        Vector3 rightWorld = new Vector3(worldCenter.x + bounds.extents.x, worldCenter.y, worldCenter.z);
        Vector3 leftVp = targetCamera.WorldToViewportPoint(leftWorld);
        Vector3 rightVp = targetCamera.WorldToViewportPoint(rightWorld);
        halfWidthVp = Mathf.Abs(rightVp.x - leftVp.x) * 0.5f;

        // 同理 Y
        Vector3 bottomWorld = new Vector3(worldCenter.x, worldCenter.y - bounds.extents.y, worldCenter.z);
        Vector3 topWorld = new Vector3(worldCenter.x, worldCenter.y + bounds.extents.y, worldCenter.z);
        Vector3 bottomVp = targetCamera.WorldToViewportPoint(bottomWorld);
        Vector3 topVp = targetCamera.WorldToViewportPoint(topWorld);
        halfHeightVp = Mathf.Abs(topVp.y - bottomVp.y) * 0.5f;
    }

    private void AssignFloorPlanIdentity()
    {
        if (levelConfig.roomsPerFloor == null || levelConfig.roomsPerFloor.Length == 0)
        {
            // Fallback:全部 floor 1,连续编号。
            for (int i = 0; i < roomControllers.Length; i++)
            {
                if (roomControllers[i]?.roomEntity != null)
                {
                    roomControllers[i].roomEntity.SetIdentity(1, 101 + i);
                }
            }
            return;
        }

        int[] plan = levelConfig.roomsPerFloor;
        int globalIndex = 0;

        // 按 plan 顺序分配楼层 + 楼层内独立起号(楼层 N 起号 = N*100 + 1)
        for (int floor = 1; floor <= plan.Length && globalIndex < roomControllers.Length; floor++)
        {
            int floorCapacity = Mathf.Max(0, plan[floor - 1]);
            for (int slot = 0; slot < floorCapacity && globalIndex < roomControllers.Length; slot++)
            {
                int roomNum = floor * 100 + 1 + slot;
                if (roomControllers[globalIndex]?.roomEntity != null)
                {
                    roomControllers[globalIndex].roomEntity.SetIdentity(floor, roomNum);
                }
                globalIndex++;
            }
        }

        // 溢出 plan 总容量的房间 → 归到最后一层末尾(防御)
        int lastFloor = plan.Length;
        int lastFloorStart = lastFloor * 100 + plan[lastFloor - 1] + 1;
        int overflow = 0;
        while (globalIndex < roomControllers.Length)
        {
            if (roomControllers[globalIndex]?.roomEntity != null)
            {
                roomControllers[globalIndex].roomEntity.SetIdentity(lastFloor, lastFloorStart + overflow);
            }
            overflow++;
            globalIndex++;
        }
    }

    private void ApplyRoomPropertyRules()
    {
        if (roomConfigApplier == null) return;

        // 关键:把 Applier 的 levelConfig 引用同步到我们用的 SO(防止 Inspector 漏接线)
        SyncApplierLevelConfig();

        roomConfigApplier.ApplyRulesToRooms();
    }

    // Story 3.7:把"场景里实际存在的所有 Room2DEntity"推回 demandLoop.rooms 数组。
    // 这是单一房间数源的关键 — Prep panel(Story 3)、Front Desk view、Room2DOverview 都读
    // demandLoop.rooms / demandLoop.UpcomingQueueCount 等;如果 demandLoop.rooms 是 Inspector
    // 手动填的旧 9 房数组,Prep panel 就只渲染 9 个房间卡,与场景 12 房脱节。
    // SceneSetup pipeline 末尾强制 re-find,消灭这种 mismatch。
    private void PropagateRoomListToDemandLoop()
    {
        if (demandLoop == null) return;

        // demandLoop 有自己的 public FindRoomsInScene() — 它会重写 rooms 数组并按 roomNumber 排序。
        // 此时 identity(floor + roomNumber)已经被我们 AssignFloorPlanIdentity 写好,
        // 所以 demandLoop 排序得到的顺序就是正确的"按楼层 + 房号"顺序。
        demandLoop.FindRoomsInScene();
    }

    private void SyncApplierLevelConfig()
    {
        // Applier.levelConfig 是 [SerializeField] private,用反射同步,避免 Inspector 接线 mismatch
        var field = typeof(Room2DPrototypeRoomConfigApplier).GetField(
            "levelConfig",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field != null && field.GetValue(roomConfigApplier) == null)
        {
            field.SetValue(roomConfigApplier, levelConfig);
        }
    }

    private void RefreshAllVisuals()
    {
        for (int i = 0; i < roomControllers.Length; i++)
        {
            if (roomControllers[i] != null)
            {
                roomControllers[i].ApplyStateVisual();
            }
        }

        if (roomOverview != null)
        {
            roomOverview.FindRoomsInScene();
            roomOverview.RefreshSummary();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static float GetGridPercent(int index, int count)
    {
        if (count <= 1) return 0.5f;
        return (float)index / (count - 1);
    }

    private Vector3 ViewportToWorld(float viewportX, float viewportY, float worldZ)
    {
        float distanceFromCamera = Mathf.Abs(worldZ - targetCamera.transform.position.z);
        Vector3 wp = targetCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, distanceFromCamera));
        return new Vector3(wp.x, wp.y, worldZ);
    }
}
