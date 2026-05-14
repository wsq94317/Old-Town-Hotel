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
    private TMP_Text _scoreLabel;
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
        _dayLabel = FindChildTmp("HeaderBar/DayLabel");
        _scoreLabel = FindChildTmp("HeaderBar/ScoreLabel");
        _guestTypeLabel = FindChildTmp("ActiveGuestCard/GuestTypeLabel");
        _needsLabel = FindChildTmp("ActiveGuestCard/NeedsLabel");

        Transform bar = transform.Find("ActiveGuestCard/PatienceBarTrack/PatienceBarFill");
        if (bar != null) _patienceBarFill = bar as RectTransform;

        Transform queue = transform.Find("QueueContent");
        if (queue != null) _queueContent = queue as RectTransform;

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

        lastRefreshResult = "Refreshed @ " + Time.time.ToString("F1") + "s";
    }

    private void RefreshHeader()
    {
        if (_dayLabel != null && demoDayController != null)
        {
            _dayLabel.text = "Day " + Mathf.Max(1, demoDayController.demoDayIndex);
        }
        // Score 字段尚未实现(Story 4 加)—— 暂时占位
        if (_scoreLabel != null)
        {
            _scoreLabel.text = "Score: --";
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
