using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Story 3.6 Front Desk UI Controller。
//
// 挂在 UI_FrontDeskScreen.prefab 根 GameObject 上(prefab instance 在 Canvas 里之后用户 Add Component)。
// Awake 时按 Hierarchy 路径自动 Find 所有 UI 子元素,无需 Inspector 拖 30 个引用。
//
// 现阶段:
//   - HeaderBar:展示 Day + Score(从 demoDayController / scoring 读 —— scoring 是 Story 4 范围,先占位)
//   - ActiveGuestCard:展示当前 active demand(从 demandLoop.activeGuestType / activeGuestPreference / activeDemandRoomPreference)
//   - QueueContent:动态 Instantiate UI_GuestQueueCard,显示 demandLoop.UpcomingQueueCount 个 upcoming guests
//   - 3 个 ActionButton:Check In / Compensate / Skip 暂时输出 Debug.Log,等 Story 4-5-8 接上正式逻辑
//   - 3 个 NavTab:暂时不切 view(其他 view 还没写),输出 Debug.Log
//
// 数据绑定走 Refresh() 方法,Update 内 1Hz 调用即可(Front Desk 数据变化频率不高)。
public class Room2DFrontDeskScreenController : MonoBehaviour
{
    [Header("Wired Sources(autofind)")]
    public Room2DPrototypeDemandLoop demandLoop;
    public Room2DDemoDayController demoDayController;
    public Room2DDayPhaseStateMachine phaseStateMachine;

    [Header("Queue Card Prefab")]
    [Tooltip("UI_GuestQueueCard prefab — runtime 实例化到 QueueContent 下。")]
    public GameObject guestQueueCardPrefab;

    [Header("Refresh Cadence")]
    [Range(0.1f, 2.0f)] public float refreshIntervalSeconds = 0.5f;

    [Header("Debug Result")]
    public string lastRefreshResult = "None";

    // ── UI references(Awake 时按 Hierarchy 路径自动找)─────────────────────
    private TMP_Text _dayLabel;
    private TMP_Text _phaseLabel;
    private TMP_Text _scoreLabel;
    private TMP_Text _moneyLabel;
    private TMP_Text _guestTypeLabel;
    private TMP_Text _needsLabel;
    private RectTransform _patienceBarFill;
    private RectTransform _queueContent;
    private Button _checkInButton;
    private Button _compensateButton;
    private Button _skipButton;
    private Button _frontDeskTabButton;
    private Button _roomsTabButton;
    private Button _loungeTabButton;

    // RoomStatusPanel 计数 + badge
    private TMP_Text _readyCount;
    private TMP_Text _dirtyCount;
    private TMP_Text _cleaningCount;
    private TMP_Text _inspectCount;
    private TMP_Text _occupiedCount;
    private TMP_Text _blockedCount;
    private RectTransform _roomBadgeGrid;

    private float _refreshTimer;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        FindUiElements();
        FindGameplayReferences();
        WireButtons();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer < refreshIntervalSeconds) return;
        _refreshTimer = 0f;
        Refresh();
    }

    // ── Find / wire ────────────────────────────────────────────────────────

    private void FindUiElements()
    {
        // 按 prefab Hierarchy 的固定路径找子元素;若 user 改动 prefab 结构,这里要同步改。
        // HeaderBar 4 段(新密集布局:DayCell/PhaseCell/ScoreCell/MoneyCell 下挂 *Label)
        _dayLabel = FindChildTmp("HeaderBar/DayCell/DayLabel");
        _phaseLabel = FindChildTmp("HeaderBar/PhaseCell/PhaseLabel");
        _scoreLabel = FindChildTmp("HeaderBar/ScoreCell/ScoreLabel");
        _moneyLabel = FindChildTmp("HeaderBar/MoneyCell/MoneyLabel");

        // ActiveGuestCard 现在在 MidSection 下
        _guestTypeLabel = FindChildTmp("MidSection/ActiveGuestCard/GuestTypeLabel");
        _needsLabel = FindChildTmp("MidSection/ActiveGuestCard/NeedsLabel");

        Transform bar = transform.Find("MidSection/ActiveGuestCard/PatienceBarTrack/PatienceBarFill");
        if (bar != null) _patienceBarFill = bar as RectTransform;

        Transform queue = transform.Find("QueueContent");
        if (queue != null) _queueContent = queue as RectTransform;

        // RoomStatusPanel 计数 + badge grid
        _readyCount = FindChildTmp("MidSection/RoomStatusPanel/StateCounts/ReadyRow/ReadyCount");
        _dirtyCount = FindChildTmp("MidSection/RoomStatusPanel/StateCounts/DirtyRow/DirtyCount");
        _cleaningCount = FindChildTmp("MidSection/RoomStatusPanel/StateCounts/CleaningRow/CleaningCount");
        _inspectCount = FindChildTmp("MidSection/RoomStatusPanel/StateCounts/InspectRow/InspectCount");
        _occupiedCount = FindChildTmp("MidSection/RoomStatusPanel/StateCounts/OccupiedRow/OccupiedCount");
        _blockedCount = FindChildTmp("MidSection/RoomStatusPanel/StateCounts/BlockedRow/BlockedCount");
        Transform grid = transform.Find("MidSection/RoomStatusPanel/RoomBadgeGrid");
        if (grid != null) _roomBadgeGrid = grid as RectTransform;

        _checkInButton = FindChildButton("ActionButtonRow/CheckInButton");
        _compensateButton = FindChildButton("ActionButtonRow/CompensateButton");
        _skipButton = FindChildButton("ActionButtonRow/SkipButton");

        _frontDeskTabButton = FindChildButton("BottomNav/FrontDeskTab");
        _roomsTabButton = FindChildButton("BottomNav/RoomsTab");
        _loungeTabButton = FindChildButton("BottomNav/LoungeTab");
    }

    private TMP_Text FindChildTmp(string path)
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private Button FindChildButton(string path)
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private void FindGameplayReferences()
    {
        if (demandLoop == null) demandLoop = Object.FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (demoDayController == null) demoDayController = Object.FindFirstObjectByType<Room2DDemoDayController>();
        if (phaseStateMachine == null) phaseStateMachine = Object.FindFirstObjectByType<Room2DDayPhaseStateMachine>();
    }

    private void WireButtons()
    {
        // Action buttons —— 暂时占位,等 Story 4-5-8 接正式逻辑
        if (_checkInButton != null)
        {
            _checkInButton.onClick.RemoveAllListeners();
            _checkInButton.onClick.AddListener(OnCheckInClicked);
        }
        if (_compensateButton != null)
        {
            _compensateButton.onClick.RemoveAllListeners();
            _compensateButton.onClick.AddListener(OnCompensateClicked);
        }
        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveAllListeners();
            _skipButton.onClick.AddListener(OnSkipClicked);
        }

        // Nav tabs —— Story 3.6 后续做 Rooms / Lounge view 时再接
        if (_frontDeskTabButton != null)
        {
            _frontDeskTabButton.onClick.RemoveAllListeners();
            _frontDeskTabButton.onClick.AddListener(() => Debug.Log("[FrontDeskScreen] FrontDesk tab clicked (already on this view)"));
        }
        if (_roomsTabButton != null)
        {
            _roomsTabButton.onClick.RemoveAllListeners();
            _roomsTabButton.onClick.AddListener(() => Debug.Log("[FrontDeskScreen] Rooms tab clicked — UI_RoomsScreen not yet built"));
        }
        if (_loungeTabButton != null)
        {
            _loungeTabButton.onClick.RemoveAllListeners();
            _loungeTabButton.onClick.AddListener(() => Debug.Log("[FrontDeskScreen] Lounge tab clicked — UI_LoungeScreen not yet built"));
        }
    }

    // ── Button handlers ────────────────────────────────────────────────────

    private void OnCheckInClicked()
    {
        Debug.Log("[FrontDeskScreen] Check In clicked. Active demand: "
            + (demandLoop != null ? demandLoop.activeGuestType.ToString() : "demandLoop=null"));
        // TODO Story 4:调用 demandLoop / FrontDesk2D 的 check-in API
    }

    private void OnCompensateClicked()
    {
        Debug.Log("[FrontDeskScreen] Compensate clicked.");
        // TODO Story 8:调用补偿逻辑(GuestTypeConfigSO.compensationCost 决定金额)
    }

    private void OnSkipClicked()
    {
        Debug.Log("[FrontDeskScreen] Skip clicked.");
        // TODO Story 4:Skip 当前 demand,标记为 unmet
    }

    // ── Refresh ────────────────────────────────────────────────────────────

    public void Refresh()
    {
        RefreshHeader();
        RefreshActiveGuest();
        RefreshQueue();
        RefreshRoomStatus();

        lastRefreshResult = "Refreshed @ " + Time.time.ToString("F1") + "s";
    }

    private void RefreshHeader()
    {
        // 密集 HUD:Day 只显示数字(caption "DAY" 已在 prefab 里)
        if (_dayLabel != null && demoDayController != null)
        {
            _dayLabel.text = Mathf.Max(1, demoDayController.demoDayIndex).ToString();
        }
        // Phase:从 state machine 读当前阶段
        if (_phaseLabel != null && phaseStateMachine != null)
        {
            _phaseLabel.text = phaseStateMachine.CurrentPhase.ToString();
        }
        // Score / Money 字段 Story 4 / 8 加 —— 暂时占位
        if (_scoreLabel != null) _scoreLabel.text = "--";
        if (_moneyLabel != null) _moneyLabel.text = "$--";
    }

    // RoomStatusPanel:统计 12 房各状态数量 + 更新 mini badge 颜色/房号
    private void RefreshRoomStatus()
    {
        if (demandLoop == null || demandLoop.rooms == null) return;

        int ready = 0, dirty = 0, cleaning = 0, inspect = 0, occupied = 0, blocked = 0;
        var rooms = demandLoop.rooms;
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == null) continue;
            switch (rooms[i].currentState)
            {
                case Room2DState.Ready: ready++; break;
                case Room2DState.Dirty: dirty++; break;
                case Room2DState.Cleaning: cleaning++; break;
                case Room2DState.AwaitingInspection: inspect++; break;
                case Room2DState.Occupied: occupied++; break;
                case Room2DState.Blocked: blocked++; break;
            }
        }

        if (_readyCount != null) _readyCount.text = ready.ToString();
        if (_dirtyCount != null) _dirtyCount.text = dirty.ToString();
        if (_cleaningCount != null) _cleaningCount.text = cleaning.ToString();
        if (_inspectCount != null) _inspectCount.text = inspect.ToString();
        if (_occupiedCount != null) _occupiedCount.text = occupied.ToString();
        if (_blockedCount != null) _blockedCount.text = blocked.ToString();

        // 更新 12 房 mini badge:房号 + 状态色
        if (_roomBadgeGrid != null)
        {
            for (int i = 0; i < _roomBadgeGrid.childCount && i < rooms.Length; i++)
            {
                var badge = _roomBadgeGrid.GetChild(i);
                var img = badge.GetComponent<Image>();
                var numTransform = badge.Find("Num");
                if (rooms[i] == null) continue;

                if (img != null) img.color = GetBadgeColor(rooms[i].currentState);
                if (numTransform != null)
                {
                    var tmp = numTransform.GetComponent<TMP_Text>();
                    if (tmp != null) tmp.text = rooms[i].roomNumber.ToString();
                }
            }
        }
    }

    private static Color GetBadgeColor(Room2DState state)
    {
        switch (state)
        {
            case Room2DState.Ready: return new Color(0.30f, 0.62f, 0.36f, 1f);          // 绿
            case Room2DState.Dirty: return new Color(0.62f, 0.40f, 0.28f, 1f);          // 棕
            case Room2DState.Cleaning: return new Color(0.30f, 0.46f, 0.62f, 1f);       // 蓝
            case Room2DState.AwaitingInspection: return new Color(0.62f, 0.58f, 0.28f, 1f); // 黄
            case Room2DState.Occupied: return new Color(0.45f, 0.32f, 0.55f, 1f);       // 紫
            case Room2DState.Blocked: return new Color(0.55f, 0.28f, 0.30f, 1f);        // 红
            default: return new Color(0.28f, 0.31f, 0.36f, 1f);                          // 灰
        }
    }

    private void RefreshActiveGuest()
    {
        if (demandLoop == null) return;

        // 当前 active demand 信息(Story 2 已写到 activeGuestType / activeGuestPreference,
        // active demand room preference 在 activeDemandRoomPreference 字段)
        if (_guestTypeLabel != null)
        {
            _guestTypeLabel.text = demandLoop.activeGuestType + " · " + demandLoop.activeGuestPreference;
        }
        if (_needsLabel != null)
        {
            _needsLabel.text = "Needs: " + demandLoop.activeDemandRoomPreference;
        }

        // Patience bar fill: Story 4 接 FrontDesk2D.patience 字段。
        // 现阶段不动 fill 宽度,prefab 默认 70% 占位。
    }

    private void RefreshQueue()
    {
        if (demandLoop == null || _queueContent == null || guestQueueCardPrefab == null) return;

        int desiredCount = demandLoop.UpcomingQueueCount;
        int currentCount = _queueContent.childCount;

        // 增删 children 直到匹配
        while (_queueContent.childCount > desiredCount)
        {
            Transform last = _queueContent.GetChild(_queueContent.childCount - 1);
            DestroyImmediate(last.gameObject);
        }
        while (_queueContent.childCount < desiredCount)
        {
            Instantiate(guestQueueCardPrefab, _queueContent);
        }

        // 更新每张卡的内容
        for (int i = 0; i < _queueContent.childCount && i < desiredCount; i++)
        {
            var card = _queueContent.GetChild(i);
            var typeLabel = card.Find("TypeLabel");
            if (typeLabel != null)
            {
                var tmp = typeLabel.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = demandLoop.GetUpcomingGuestType(i).ToString();
                }
            }
        }
    }
}
