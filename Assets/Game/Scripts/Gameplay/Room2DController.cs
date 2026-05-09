using UnityEngine;
using UnityEngine.UI;

// 房间视觉和交互控制器。
// 它读取 Room2DEntity 的数据，然后更新颜色、状态物体、标签和总览。
public class Room2DController : MonoBehaviour
{
    // Showcase 页面切换时用的全局开关。
    // 只隐藏 OnGUI 调试房间标签，不会关闭房间物体、Sprite 或正式 UI。
    public static bool hidePrototypeDebugLabelsGlobally;

    // 房间数据源。正常情况下和这个组件挂在同一个 Room_A_2D / Room_B_2D 物体上。
    public Room2DEntity roomEntity;

    // 可选：房间自己的文字显示。
    public Room2DLabelView labelView;

    // 可选：场景里的总览统计。
    public Room2DOverview roomOverview;

    // 可选：场景里的单个 HSK，用来判断当前房间是否正在被保洁处理。
    public Housekeeper2D housekeeper;

    [Header("Prototype Click Selection")]
    // 原型阶段用：允许玩家在 Rooms View 里直接点击房间来选中。
    public bool enablePrototypeClickSelection = true;
    // 原型阶段用：没有 Collider 时自动补一个 BoxCollider2D，避免每个复制房间都要手动加碰撞。
    public bool autoAddPrototypeClickCollider = true;
    public Room2DSelectionManager selectionManager;

    [Header("Fallback Only - Use Room2DEntity")]
    // 没有绑定 Room2DEntity 时才使用这些备用数据。
    // 如果绑定了 Room2DEntity，这些字段会自动同步实体数据，只用于 Inspector 易读。
    [HideInInspector]
    public string roomName = "Room 101";
    [Tooltip("Only used when Room Entity is missing. If Room Entity exists, this field mirrors Room2DEntity.currentState.")]
    public Room2DState currentState = Room2DState.Dirty;
    [Tooltip("Only used when Room Entity is missing. If Room Entity exists, this field mirrors Room2DEntity.actionCount.")]
    public int actionCount;

    [Header("Optional State Visuals")]
    // 这些是可选的状态标记物体。绑定后会根据房态自动显示/隐藏。
    public GameObject dirtyVisual;
    public GameObject cleaningVisual;
    public GameObject awaitingInspectionVisual;
    public GameObject readyVisual;
    public GameObject occupiedVisual;
    public GameObject blockedVisual;
    public GameObject selectedVisual;

    [Header("Optional Tint Target")]
    // 房间主色目标。SpriteRenderer 用于场景 2D 精灵，Image 用于 UI 图片。
    public SpriteRenderer roomSpriteRenderer;
    public Image roomImage;

    // 原型阶段用颜色区分房态。之后可以换成真实美术资源。
    public Color dirtyColor = new Color(0.65f, 0.45f, 0.35f);
    public Color cleaningColor = new Color(0.35f, 0.65f, 0.9f);
    public Color awaitingInspectionColor = new Color(0.95f, 0.8f, 0.35f);
    public Color readyColor = new Color(0.45f, 0.8f, 0.45f);
    public Color occupiedColor = new Color(0.75f, 0.6f, 0.9f);
    public Color blockedColor = new Color(0.35f, 0.35f, 0.35f);

    // 原型阶段的默认 Block 时长，单位是游戏小时。
    public float prototypeMaintenanceBlockHours = 8f;
    public float prototypeRenovationBlockHours = 72f;

    [Header("Prototype Debug Room Label")]
    // 原型调试标签：直接显示在 Game 窗口里，不依赖 Canvas，方便快速看每个房间是谁。
    public bool showPrototypeDebugLabel = true;
    public Vector2 debugLabelScreenOffset = new Vector2(-60f, -62f);
    public Vector2 debugLabelSize = new Vector2(130f, 64f);
    public int debugLabelFontSize = 16;
    public Color normalDebugLabelColor = Color.white;
    public Color selectedDebugLabelColor = Color.yellow;
    public Color housekeeperDebugLabelColor = Color.cyan;

    private bool isSelected;
    private GUIStyle debugLabelStyle;

    private void Awake()
    {
        FindRoomEntityIfNeeded();
        FindLabelViewIfNeeded();
        FindRoomOverviewIfNeeded();
        FindHousekeeperIfNeeded();
    }

    private void Start()
    {
        FindRoomOverviewIfNeeded();
        FindHousekeeperIfNeeded();
        FindSelectionManagerIfNeeded();
        EnsurePrototypeClickColliderIfNeeded();
        ApplyStateVisual();
    }

    private void LateUpdate()
    {
        SyncFallbackFieldsFromEntity();
    }

    private void OnValidate()
    {
        FindRoomEntityIfNeeded();
        FindLabelViewIfNeeded();
        SyncFallbackFieldsFromEntity();
        // 不在 OnValidate 里调用 ApplyStateVisual。
        // Unity 不允许在 OnValidate / Awake 校验阶段 SetActive，否则 Console 会出现 SendMessage 警告。
    }

    private void OnGUI()
    {
        DrawPrototypeDebugLabel();
    }

    private void OnMouseDown()
    {
        SelectThisRoomForPrototype();
    }

    // 给房间点击、按钮或临时测试脚本调用：把这个房间设为当前选中房间。
    public void SelectThisRoomForPrototype()
    {
        if (!enablePrototypeClickSelection)
        {
            return;
        }

        FindSelectionManagerIfNeeded();

        if (selectionManager != null)
        {
            selectionManager.SelectRoom(this);
        }
    }

    public void SetState(Room2DState newState)
    {
        if (roomEntity != null)
        {
            roomEntity.SetState(newState);
        }
        else
        {
            currentState = newState;
        }

        ApplyStateVisual();
        RefreshOverview();
    }

    // 以下方法方便 Unity Button 或右键菜单直接绑定。
    public void SetDirty()
    {
        SetState(Room2DState.Dirty);
    }

    public void SetCleaning()
    {
        SetState(Room2DState.Cleaning);
    }

    public void SetAwaitingInspection()
    {
        SetState(Room2DState.AwaitingInspection);
    }

    public void SetReady()
    {
        SetState(Room2DState.Ready);
    }

    public void SetOccupied()
    {
        SetState(Room2DState.Occupied);
    }

    public void SetBlocked()
    {
        SetState(Room2DState.Blocked);
    }

    public void CycleToNextState()
    {
        ForceDebugNextState();
    }

    // 旧按钮兼容入口：现在只作为 Debug Force 使用，不代表正常 HSK / Inspector 瓶颈流程。
    public void PerformNextAction()
    {
        ForceDebugNextState();
    }

    // Debug 强制推进房态。正常测试请优先使用 Assign HSK / Assign Inspector / Demand Loop。
    [ContextMenu("DEBUG Force Next State")]
    public void ForceDebugNextState()
    {
        if (roomEntity != null)
        {
            roomEntity.ForceDebugNextState();
            ApplyStateVisual();
            RefreshOverview();
            return;
        }

        actionCount++;

        switch (currentState)
        {
            case Room2DState.Dirty:
                SetState(Room2DState.Cleaning);
                break;
            case Room2DState.Cleaning:
                SetState(Room2DState.AwaitingInspection);
                break;
            case Room2DState.AwaitingInspection:
                SetState(Room2DState.Ready);
                break;
            case Room2DState.Ready:
                SetState(Room2DState.Occupied);
                break;
            default:
                SetState(Room2DState.Dirty);
                break;
        }
    }

    // 原型用：模拟客人入住。
    public void SimulateCheckIn()
    {
        PerformRoomAction(entity => entity.SimulateCheckIn(), Room2DState.Occupied);
    }

    // 原型用：模拟客人退房。
    public void SimulateCheckout()
    {
        PerformRoomAction(entity => entity.SimulateCheckout(), Room2DState.Dirty);
    }

    // 原型用：开始维修 Block。
    [ContextMenu("Start Maintenance Block")]
    public void StartMaintenanceBlock()
    {
        PerformRoomAction(entity => entity.StartBlock(Room2DBlockReason.Maintenance, prototypeMaintenanceBlockHours), Room2DState.Blocked);
    }

    // 原型用：开始装修 Block。
    [ContextMenu("Start Renovation Block")]
    public void StartRenovationBlock()
    {
        PerformRoomAction(entity => entity.StartBlock(Room2DBlockReason.Renovation, prototypeRenovationBlockHours), Room2DState.Blocked);
    }

    // 以下是清洁链条的显式动作。
    public void StartCleaning()
    {
        PerformRoomAction(entity => entity.StartCleaning(), Room2DState.Cleaning);
    }

    public void FinishCleaning()
    {
        PerformRoomAction(entity => entity.FinishCleaning(), Room2DState.AwaitingInspection);
    }

    public void ApproveInspection()
    {
        PerformRoomAction(entity => entity.ApproveInspection(), Room2DState.Ready);
    }

    // 根据当前房态刷新颜色、状态标记和文字。
    public void ApplyStateVisual()
    {
        SyncFallbackFieldsFromEntity();
        Room2DState visualState = GetCurrentState();

        SetVisualActive(dirtyVisual, visualState == Room2DState.Dirty);
        SetVisualActive(cleaningVisual, visualState == Room2DState.Cleaning);
        SetVisualActive(awaitingInspectionVisual, visualState == Room2DState.AwaitingInspection);
        SetVisualActive(readyVisual, visualState == Room2DState.Ready);
        SetVisualActive(occupiedVisual, visualState == Room2DState.Occupied);
        SetVisualActive(blockedVisual, visualState == Room2DState.Blocked);

        Color stateColor = GetStateColor();

        if (roomSpriteRenderer != null)
        {
            roomSpriteRenderer.color = stateColor;
        }

        if (roomImage != null)
        {
            roomImage.color = stateColor;
        }

        RefreshLabelView();
    }

    // 当前选中房间的视觉反馈。
    public void SetSelected(bool isSelected)
    {
        this.isSelected = isSelected;
        SetVisualActive(selectedVisual, isSelected);
        RefreshLabelView();
    }

    private void SetVisualActive(GameObject visual, bool isActive)
    {
        if (visual != null)
        {
            visual.SetActive(isActive);
        }
    }

    // 执行 Room2DEntity 上的动作，并在成功后刷新显示。
    private void PerformRoomAction(System.Func<Room2DEntity, bool> entityAction, Room2DState fallbackState)
    {
        if (roomEntity != null)
        {
            if (!entityAction(roomEntity))
            {
                return;
            }
        }
        else
        {
            currentState = fallbackState;
            actionCount++;
        }

        ApplyStateVisual();
        RefreshOverview();
    }

    // 把房态映射成原型颜色。
    private Color GetStateColor()
    {
        switch (GetCurrentState())
        {
            case Room2DState.Cleaning:
                return cleaningColor;
            case Room2DState.AwaitingInspection:
                return awaitingInspectionColor;
            case Room2DState.Ready:
                return readyColor;
            case Room2DState.Occupied:
                return occupiedColor;
            case Room2DState.Blocked:
                return blockedColor;
            default:
                return dirtyColor;
        }
    }

    // 优先读取 Room2DEntity；没有数据实体时才用本组件备用字段。
    private Room2DState GetCurrentState()
    {
        if (roomEntity != null)
        {
            return roomEntity.currentState;
        }

        return currentState;
    }

    private void SyncFallbackFieldsFromEntity()
    {
        if (roomEntity == null)
        {
            return;
        }

        roomName = roomEntity.roomName;
        currentState = roomEntity.currentState;
        actionCount = roomEntity.actionCount;
    }

    // 以下 Find... 方法让复制房间时少拖几个引用，降低 Unity 初学阶段的绑定成本。
    private void FindRoomEntityIfNeeded()
    {
        if (roomEntity == null)
        {
            roomEntity = GetComponent<Room2DEntity>();
        }
    }

    private void FindLabelViewIfNeeded()
    {
        if (labelView == null)
        {
            labelView = GetComponentInChildren<Room2DLabelView>(true);
        }
    }

    private void FindRoomOverviewIfNeeded()
    {
        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }
    }

    private void FindHousekeeperIfNeeded()
    {
        if (housekeeper == null)
        {
            housekeeper = FindFirstObjectByType<Housekeeper2D>();
        }
    }

    private void FindSelectionManagerIfNeeded()
    {
        if (selectionManager == null)
        {
            selectionManager = FindFirstObjectByType<Room2DSelectionManager>();
        }
    }

    private void EnsurePrototypeClickColliderIfNeeded()
    {
        if (!enablePrototypeClickSelection || !autoAddPrototypeClickCollider)
        {
            return;
        }

        if (GetComponent<Collider2D>() != null || GetComponent<Collider>() != null)
        {
            return;
        }

        BoxCollider2D clickCollider = gameObject.AddComponent<BoxCollider2D>();
        ApplyRendererBoundsToClickCollider(clickCollider);
    }

    private void ApplyRendererBoundsToClickCollider(BoxCollider2D clickCollider)
    {
        Bounds rendererBounds;
        if (!TryGetRendererBounds(out rendererBounds))
        {
            clickCollider.size = new Vector2(3f, 2f);
            return;
        }

        Vector3 localCenter = transform.InverseTransformPoint(rendererBounds.center);
        Vector3 localSize = transform.InverseTransformVector(rendererBounds.size);

        clickCollider.offset = new Vector2(localCenter.x, localCenter.y);
        clickCollider.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
    }

    private bool TryGetRendererBounds(out Bounds combinedBounds)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        combinedBounds = new Bounds(transform.position, Vector3.zero);
        bool foundRenderer = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!foundRenderer)
            {
                combinedBounds = renderer.bounds;
                foundRenderer = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return foundRenderer;
    }

    private void RefreshOverview()
    {
        FindRoomOverviewIfNeeded();

        if (roomOverview != null)
        {
            roomOverview.RefreshSummary();
        }
    }

    private void RefreshLabelView()
    {
        FindLabelViewIfNeeded();
        FindHousekeeperIfNeeded();

        if (labelView != null && roomEntity != null)
        {
            labelView.Refresh(roomEntity, isSelected, IsAssignedToHousekeeper());
        }
    }

    private bool IsAssignedToHousekeeper()
    {
        return housekeeper != null
            && housekeeper.assignedRoom == roomEntity
            && housekeeper.currentState == Housekeeper2D.HousekeeperState.Working;
    }

    private void DrawPrototypeDebugLabel()
    {
        if (hidePrototypeDebugLabelsGlobally || !showPrototypeDebugLabel || roomEntity == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(transform.position);
        if (screenPosition.z < 0f)
        {
            return;
        }

        Rect labelRect = new Rect(
            screenPosition.x + debugLabelScreenOffset.x,
            Screen.height - screenPosition.y + debugLabelScreenOffset.y,
            debugLabelSize.x,
            debugLabelSize.y);

        bool isHousekeeperCleaning = IsAssignedToHousekeeper();
        GUIStyle labelStyle = GetDebugLabelStyle(GetDebugLabelColor(isHousekeeperCleaning));
        GUI.Label(labelRect, BuildPrototypeDebugLabelText(isHousekeeperCleaning), labelStyle);
    }

    private GUIStyle GetDebugLabelStyle(Color textColor)
    {
        if (debugLabelStyle == null)
        {
            debugLabelStyle = new GUIStyle(GUI.skin.label);
            debugLabelStyle.alignment = TextAnchor.MiddleCenter;
            debugLabelStyle.wordWrap = false;
        }

        debugLabelStyle.fontSize = debugLabelFontSize;
        debugLabelStyle.normal.textColor = textColor;
        return debugLabelStyle;
    }

    private Color GetDebugLabelColor(bool isHousekeeperCleaning)
    {
        if (isSelected)
        {
            return selectedDebugLabelColor;
        }

        if (isHousekeeperCleaning)
        {
            return housekeeperDebugLabelColor;
        }

        return normalDebugLabelColor;
    }

    private string BuildPrototypeDebugLabelText(bool isHousekeeperCleaning)
    {
        string labelText = roomEntity.roomName + "\n" + roomEntity.GetStateDisplayName();

        if (isSelected)
        {
            labelText += "\nSELECTED";
        }

        if (isHousekeeperCleaning)
        {
            labelText += "\nHSK Cleaning";
        }

        if (roomEntity.markedCleaningPriority)
        {
            labelText += "\nCLEAN PRIO";
        }
        else if (roomEntity.markedInspectionPriority)
        {
            labelText += "\nINSP PRIO";
        }

        return labelText;
    }
}
