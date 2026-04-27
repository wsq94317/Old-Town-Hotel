using UnityEngine;
using UnityEngine.UI;

// 房间视觉和交互控制器。
// 它读取 Room2DEntity 的数据，然后更新颜色、状态物体、标签和总览。
public class Room2DController : MonoBehaviour
{
    // 房间数据源。正常情况下和这个组件挂在同一个 Room_A_2D / Room_B_2D 物体上。
    public Room2DEntity roomEntity;

    // 可选：房间自己的文字显示。
    public Room2DLabelView labelView;

    // 可选：场景里的总览统计。
    public Room2DOverview roomOverview;

    // 可选：场景里的单个 HSK，用来判断当前房间是否正在被保洁处理。
    public Housekeeper2D housekeeper;

    // 没有绑定 Room2DEntity 时的备用原型数据。当前房间身份请改 Room2DEntity。
    [HideInInspector]
    public string roomName = "Room 101";
    public Room2DState currentState = Room2DState.Dirty;
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
        ApplyStateVisual();
    }

    private void OnValidate()
    {
        FindRoomEntityIfNeeded();
        FindLabelViewIfNeeded();
        // 不在 OnValidate 里调用 ApplyStateVisual。
        // Unity 不允许在 OnValidate / Awake 校验阶段 SetActive，否则 Console 会出现 SendMessage 警告。
    }

    private void OnGUI()
    {
        DrawPrototypeDebugLabel();
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
            && housekeeper.currentState == Housekeeper2D.HousekeeperState.Busy;
    }

    private void DrawPrototypeDebugLabel()
    {
        if (!showPrototypeDebugLabel || roomEntity == null)
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
