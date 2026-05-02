using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 最小 Showcase 三视图控制器。
// 这个脚本只包装现有原型系统，让录屏时可以在 Front Desk / Rooms / Lounge 三个视图之间切换。
public class Room2DShowcaseViewController : MonoBehaviour
{
    public enum ShowcaseView
    {
        FrontDesk,
        Rooms,
        Lounge
    }

    [Header("References")]
    public bool autoFindReferences = true;
    public Room2DDemoDayController demoDayController;
    public Room2DPrototypeDebugHud debugHud;
    public Room2DSelectionManager selectionManager;
    public Room2DOverview roomOverview;
    public Room2DPrototypeDemandLoop demandLoop;
    public Room2DWorkerSelectionPanel workerSelectionPanel;
    public FrontDesk2D frontDesk;
    public Lounge2D lounge;

    [Header("Build")]
    // 开始运行时自动创建三个展示屏幕，减少手动搭 UI 的步骤。
    public bool autoBuildShellOnStart = true;
    // Showcase 模式下先隐藏旧 Debug HUD，避免两套 UI 叠在一起导致完全无法阅读。
    public bool hideLegacyDebugHudWhileShowcaseRuns = true;
    // Rooms View Phase 1：先隐藏旧调试按钮区，让主交互变成“点房间 -> 看详情卡”。
    public bool showLegacyRoomDebugPanels = false;
    // 使用专用 Overlay Canvas，避免误挂到房间自己的 World Space Canvas 上。
    public bool useDedicatedOverlayCanvas = true;
    public int showcaseCanvasSortingOrder = 5000;

    [Header("Rooms View Interaction")]
    // Rooms View 的屏幕点击选择。它按房间可见 Sprite 的屏幕范围判断，不强依赖 Collider。
    public bool enableRoomsViewClickSelection = true;
    public float roomClickPaddingPixels = 28f;
    public float maxRoomClickDistancePixels = 140f;

    public Canvas targetCanvas;
    public RectTransform showcaseRoot;
    public RectTransform navigationPanel;
    public RectTransform frontDeskViewPanel;
    public RectTransform roomViewPanel;
    public RectTransform loungeViewPanel;
    public RectTransform frontDeskStatusPanel;
    public RectTransform frontDeskDemandPanel;
    public RectTransform frontDeskActionsPanel;
    public RectTransform frontDeskResultPanel;
    public RectTransform frontDeskQueueIconPlaceholder;
    public RectTransform frontDeskGuestPortraitPlaceholder;
    public RectTransform frontDeskWarningBadgePlaceholder;
    public RectTransform roomSelectedPanel;
    public RectTransform roomWorkersPanel;
    public RectTransform roomActionsPanel;
    public RectTransform roomDemandPanel;
    public RectTransform roomWorkerPopupPanel;
    public RectTransform roomWorkerPopupButtonPanel;
    public RectTransform loungeStatusPanel;
    public RectTransform loungeActionsPanel;
    public RectTransform loungeResultPanel;

    [Header("Texts")]
    public TMP_Text activeViewLabelText;
    public TMP_Text frontDeskShellText;
    public TMP_Text roomShellText;
    public TMP_Text loungeShellText;
    public TMP_Text frontDeskStatusText;
    public TMP_Text frontDeskDemandText;
    public TMP_Text frontDeskResultText;
    public TMP_Text selectedRoomText;
    public TMP_Text roomWorkersText;
    public TMP_Text roomWorkerPopupText;
    public TMP_Text roomDemandText;
    public TMP_Text loungeStatusText;
    public TMP_Text loungeResultText;

    [Header("Buttons")]
    public Button frontDeskTabButton;
    public Button roomTabButton;
    public Button loungeTabButton;
    public Button roomAssignHousekeeperButton;
    public Button roomMarkCleanPriorityButton;
    public Button roomAssignInspectorButton;
    public Button roomMarkInspectionPriorityButton;
    public Button roomReserveButton;
    public Button roomAssignDemandButton;
    public Button roomSelectNextHousekeeperButton;
    public Button roomSelectHousekeeperButton;
    public Button roomSelectInspectorButton;
    public Button roomConfirmWorkerButton;
    public Button roomCancelWorkerPopupButton;

    [Header("Runtime")]
    public ShowcaseView currentView = ShowcaseView.Rooms;
    public string lastShellResult = "Not built";
    public bool roomWorkerPopupVisible;
    public string roomActionHint = "Tap a room.";

    private const string RootName = "Room2DShowcaseViews";

    private void Start()
    {
        FindReferencesIfNeeded();

        if (autoBuildShellOnStart)
        {
            BuildShowcaseViewShell();
        }

        SwitchView(currentView);
    }

    private void Update()
    {
        HandleRoomsViewClickSelection();
        RefreshShellText();
    }

    [ContextMenu("Build Showcase View Shell")]
    public void BuildShowcaseViewShell()
    {
        FindReferencesIfNeeded();

        if (!FindOrCreateCanvasIfNeeded())
        {
            lastShellResult = "No Canvas found. Create a Canvas first.";
            return;
        }

        showcaseRoot = FindOrCreateRectChild(targetCanvas.transform, RootName);
        ApplyStretch(showcaseRoot);
        showcaseRoot.SetAsLastSibling();
        HideDuplicateShowcaseRoots();
        HideLegacyDebugHudIfSafe();

        navigationPanel = FindOrCreatePanel(showcaseRoot, "Panel_ShowcaseBottomNav", new Color(0.03f, 0.04f, 0.06f, 0.88f));
        ApplyBottomPanel(navigationPanel, 88f);
        ApplyNavigationLayout(navigationPanel);

        frontDeskTabButton = FindOrCreateButton(navigationPanel, "Button_ShowFrontDeskView", "Front Desk");
        roomTabButton = FindOrCreateButton(navigationPanel, "Button_ShowRoomView", "Rooms");
        loungeTabButton = FindOrCreateButton(navigationPanel, "Button_ShowLoungeView", "Lounge");
        WireTabButtons();

        frontDeskViewPanel = FindOrCreatePanel(showcaseRoot, "Panel_FrontDeskView", new Color(0.02f, 0.03f, 0.04f, 1f));
        roomViewPanel = FindOrCreatePanel(showcaseRoot, "Panel_RoomView", new Color(0.02f, 0.03f, 0.04f, 0.0f));
        loungeViewPanel = FindOrCreatePanel(showcaseRoot, "Panel_LoungeView", new Color(0.02f, 0.03f, 0.04f, 1f));

        ApplyViewPanel(frontDeskViewPanel);
        ApplyViewPanel(roomViewPanel);
        ApplyViewPanel(loungeViewPanel);

        frontDeskShellText = FindOrCreateText(frontDeskViewPanel, "Text_FrontDeskViewShell", "Front Desk View");
        roomShellText = FindOrCreateText(roomViewPanel, "Text_RoomViewShell", "Room View");
        loungeShellText = FindOrCreateText(loungeViewPanel, "Text_LoungeViewShell", "Lounge View");
        activeViewLabelText = FindOrCreateText(showcaseRoot, "Text_ActiveShowcaseView", "View: Rooms");

        ApplyShellText(frontDeskShellText, TextAlignmentOptions.TopLeft);
        ApplyShellText(roomShellText, TextAlignmentOptions.TopLeft);
        ApplyShellText(loungeShellText, TextAlignmentOptions.TopLeft);
        ApplyNavLabelText(activeViewLabelText);
        HideOldNavLabelIfItExists();
        HideShellTextHeaders();

        BuildFrontDeskViewContent();
        BuildRoomViewContent();
        BuildLoungeViewContent();

        // Room View 需要尽量露出房间网格，所以只保留轻量透明提示，不挡住操作。
        SetPanelRaycast(frontDeskViewPanel, false);
        SetPanelRaycast(roomViewPanel, false);
        SetPanelRaycast(loungeViewPanel, false);

        SwitchView(currentView);
        lastShellResult = "Showcase shell ready";
    }

    [ContextMenu("Show Front Desk View")]
    public void ShowFrontDeskView()
    {
        SwitchView(ShowcaseView.FrontDesk);
    }

    [ContextMenu("Show Room View")]
    public void ShowRoomView()
    {
        SwitchView(ShowcaseView.Rooms);
    }

    [ContextMenu("Show Lounge View")]
    public void ShowLoungeView()
    {
        SwitchView(ShowcaseView.Lounge);
    }

    public void SwitchView(ShowcaseView newView)
    {
        currentView = newView;

        if (frontDeskViewPanel != null)
        {
            frontDeskViewPanel.gameObject.SetActive(currentView == ShowcaseView.FrontDesk);
        }

        if (roomViewPanel != null)
        {
            roomViewPanel.gameObject.SetActive(currentView == ShowcaseView.Rooms);
        }

        if (loungeViewPanel != null)
        {
            loungeViewPanel.gameObject.SetActive(currentView == ShowcaseView.Lounge);
        }

        if (showcaseRoot != null)
        {
            showcaseRoot.SetAsLastSibling();
        }

        ApplyPrototypeDebugLabelVisibility();
        RefreshShellText();
    }

    [ContextMenu("Show Legacy Debug HUD Again")]
    public void ShowLegacyDebugHudAgain()
    {
        if (debugHud != null)
        {
            debugHud.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        // 停用 Showcase 时恢复房间调试标签，避免影响普通测试。
        Room2DController.hidePrototypeDebugLabelsGlobally = false;
    }

    private void FindReferencesIfNeeded()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (demoDayController == null)
        {
            demoDayController = FindFirstObjectByType<Room2DDemoDayController>();
        }

        if (debugHud == null)
        {
            debugHud = FindFirstObjectByType<Room2DPrototypeDebugHud>();
        }

        if (selectionManager == null)
        {
            selectionManager = FindFirstObjectByType<Room2DSelectionManager>();
        }

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }

        if (demandLoop == null)
        {
            demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        }

        if (workerSelectionPanel == null)
        {
            workerSelectionPanel = FindFirstObjectByType<Room2DWorkerSelectionPanel>();
        }

        if (frontDesk == null)
        {
            frontDesk = FindFirstObjectByType<FrontDesk2D>();
        }

        if (lounge == null)
        {
            lounge = FindFirstObjectByType<Lounge2D>();
        }
    }

    private bool FindOrCreateCanvasIfNeeded()
    {
        if (useDedicatedOverlayCanvas)
        {
            targetCanvas = FindOrCreateDedicatedOverlayCanvas();
            return targetCanvas != null;
        }

        if (targetCanvas != null)
        {
            ConfigureCanvasForShowcase(targetCanvas);
            return true;
        }

        targetCanvas = GetComponentInParent<Canvas>();

        if (targetCanvas == null)
        {
            targetCanvas = FindBestExistingScreenCanvas();
        }

        ConfigureCanvasForShowcase(targetCanvas);
        return targetCanvas != null;
    }

    private Canvas FindOrCreateDedicatedOverlayCanvas()
    {
        GameObject existingCanvasObject = GameObject.Find("Canvas_ShowcaseUI");
        Canvas canvas = existingCanvasObject != null ? existingCanvasObject.GetComponent<Canvas>() : null;

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "Canvas_ShowcaseUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvas = canvasObject.GetComponent<Canvas>();
        }

        ConfigureCanvasForShowcase(canvas);
        return canvas;
    }

    private Canvas FindBestExistingScreenCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas bestCanvas = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (bestCanvas == null || canvas.sortingOrder > bestCanvas.sortingOrder)
            {
                bestCanvas = canvas;
            }
        }

        if (bestCanvas != null)
        {
            return bestCanvas;
        }

        return FindFirstObjectByType<Canvas>();
    }

    private void ConfigureCanvasForShowcase(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = showcaseCanvasSortingOrder;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void RefreshShellText()
    {
        FindReferencesIfNeeded();

        string phaseText = demoDayController != null
            ? demoDayController.GetCompactDemoDayText()
            : "Demo controller not linked";

        if (activeViewLabelText != null)
        {
            activeViewLabelText.text = GetViewTitleText();
        }

        if (frontDeskShellText != null)
        {
            frontDeskShellText.text = "Front Desk\n"
                + phaseText + "\n"
                + "Guests waiting and demand pressure";
        }

        if (roomShellText != null)
        {
            roomShellText.text = "Rooms\n"
                + phaseText + "\n"
                + "Select rooms and assign workers";
        }

        if (loungeShellText != null)
        {
            loungeShellText.text = "Lounge\n"
                + phaseText + "\n"
                + "Cups, stock, washing, and restock";
        }

        if (frontDeskStatusText != null)
        {
            frontDeskStatusText.text = BuildFrontDeskStatusText();
        }

        if (frontDeskDemandText != null)
        {
            frontDeskDemandText.text = BuildFrontDeskDemandText();
        }

        if (selectedRoomText != null)
        {
            selectedRoomText.text = BuildSelectedRoomText();
        }

        if (roomWorkersText != null)
        {
            roomWorkersText.text = BuildRoomWorkerText();
        }

        if (loungeStatusText != null)
        {
            loungeStatusText.text = BuildLoungeStatusText();
        }

        if (frontDeskResultText != null)
        {
            frontDeskResultText.text = BuildFrontDeskResultText();
        }

        if (roomDemandText != null)
        {
            roomDemandText.text = BuildRoomDemandText();
        }

        RefreshRoomsViewActionState();

        if (loungeResultText != null)
        {
            loungeResultText.text = BuildLoungeResultText();
        }
    }

    private void BuildFrontDeskViewContent()
    {
        frontDeskStatusPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_FrontDeskHeaderAndQueue", new Color(0.05f, 0.07f, 0.09f, 0.96f));
        frontDeskDemandPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_CurrentGuestRequest", new Color(0.08f, 0.09f, 0.12f, 0.96f));
        frontDeskActionsPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_FrontDeskActionBar", new Color(0.05f, 0.05f, 0.07f, 0.96f));
        frontDeskResultPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_DemandSummary", new Color(0.05f, 0.06f, 0.08f, 0.96f));

        ApplyAnchoredPanel(frontDeskStatusPanel, new Vector2(0.07f, 0.70f), new Vector2(0.93f, 0.91f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskDemandPanel, new Vector2(0.07f, 0.48f), new Vector2(0.93f, 0.67f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskResultPanel, new Vector2(0.07f, 0.27f), new Vector2(0.93f, 0.45f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskActionsPanel, new Vector2(0.07f, 0.09f), new Vector2(0.93f, 0.24f), Vector2.zero, Vector2.zero);

        frontDeskStatusText = FindOrCreateText(frontDeskStatusPanel, "Text_HeaderAndQueue", "Front Desk");
        frontDeskDemandText = FindOrCreateText(frontDeskDemandPanel, "Text_CurrentGuestRequest", "Current Guest");
        frontDeskResultText = FindOrCreateText(frontDeskResultPanel, "Text_DemandSummary", "Demand Summary");
        ApplyCardText(frontDeskStatusText, 20f);
        ApplyCardText(frontDeskDemandText, 18f);
        ApplyCardText(frontDeskResultText, 17f);
        frontDeskStatusText.rectTransform.offsetMax = new Vector2(-120f, -16f);
        frontDeskDemandText.rectTransform.offsetMax = new Vector2(-150f, -16f);
        frontDeskResultText.rectTransform.offsetMax = new Vector2(-120f, -16f);

        frontDeskQueueIconPlaceholder = FindOrCreatePlaceholder(frontDeskStatusPanel, "IconPlaceholder_QueuePressure", "QUEUE");
        ApplyAnchoredPanel(frontDeskQueueIconPlaceholder, new Vector2(0.76f, 0.48f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero);
        frontDeskGuestPortraitPlaceholder = FindOrCreatePlaceholder(frontDeskDemandPanel, "PortraitPlaceholder_CurrentGuest", "GUEST");
        ApplyAnchoredPanel(frontDeskGuestPortraitPlaceholder, new Vector2(0.74f, 0.18f), new Vector2(0.96f, 0.82f), Vector2.zero, Vector2.zero);
        frontDeskWarningBadgePlaceholder = FindOrCreatePlaceholder(frontDeskResultPanel, "BadgePlaceholder_Warning", "RISK");
        ApplyAnchoredPanel(frontDeskWarningBadgePlaceholder, new Vector2(0.78f, 0.22f), new Vector2(0.96f, 0.78f), Vector2.zero, Vector2.zero);

        ApplyActionGrid(frontDeskActionsPanel, 3, new Vector2(168f, 44f), 12f);
        FindOrCreateActionButtonWithIconPlaceholder(frontDeskActionsPanel, "Button_StartOperating", "Start", StartDemoOperatingPeriod);
        FindOrCreateActionButtonWithIconPlaceholder(frontDeskActionsPanel, "Button_ActivateDemand", "Call", ActivateUpcomingDemandNow);
        FindOrCreateActionButtonWithIconPlaceholder(frontDeskActionsPanel, "Button_AssignDemandShowcase", "Assign", AssignSelectedRoomToDemand);
        FindOrCreateActionButtonWithIconPlaceholder(frontDeskActionsPanel, "Button_FrontDeskWait", "Wait", RecordWaitAction);
        FindOrCreateActionButtonWithIconPlaceholder(frontDeskActionsPanel, "Button_EndDemoDayShowcase", "End", EndDemoDay);
        FindOrCreateActionButtonWithIconPlaceholder(frontDeskActionsPanel, "Button_RestartDemoDayShowcase", "Reset", RestartDemoDay);
        HideLegacyFrontDeskCards();
    }

    private void HideLegacyFrontDeskCards()
    {
        if (frontDeskViewPanel == null)
        {
            return;
        }

        // 老版本的 raw debug 卡片如果还在场景里，保留但隐藏，避免和新 UI foundation 重叠。
        string[] legacyCardNames =
        {
            "Card_FrontDeskStatus",
            "Card_FrontDeskDemand",
            "Card_FrontDeskActions",
            "Card_FrontDeskResult"
        };

        for (int i = 0; i < legacyCardNames.Length; i++)
        {
            Transform legacyCard = frontDeskViewPanel.Find(legacyCardNames[i]);
            if (legacyCard != null)
            {
                legacyCard.gameObject.SetActive(false);
            }
        }
    }

    private void BuildRoomViewContent()
    {
        roomSelectedPanel = FindOrCreatePanel(roomViewPanel, "Card_SelectedRoom", new Color(0.03f, 0.04f, 0.06f, 0.88f));
        roomWorkersPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomWorkers", new Color(0.03f, 0.04f, 0.06f, 0.88f));
        roomActionsPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomActions", new Color(0.03f, 0.04f, 0.06f, 0.92f));
        roomDemandPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomDemand", new Color(0.03f, 0.04f, 0.06f, 0.88f));
        roomWorkerPopupPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomWorkerPopup", new Color(0.02f, 0.03f, 0.05f, 0.96f));
        roomWorkerPopupButtonPanel = FindOrCreatePanel(roomWorkerPopupPanel, "Panel_RoomWorkerPopupButtons", new Color(0f, 0f, 0f, 0f));

        // Rooms View MVP：底部是房间详情，上方是根据房态变化的动作按钮。
        ApplyAnchoredPanel(roomSelectedPanel, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.29f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomDemandPanel, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.91f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomWorkersPanel, new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.39f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomActionsPanel, new Vector2(0.06f, 0.40f), new Vector2(0.94f, 0.54f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomWorkerPopupPanel, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.60f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomWorkerPopupButtonPanel, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.46f), Vector2.zero, Vector2.zero);

        selectedRoomText = FindOrCreateText(roomSelectedPanel, "Text_SelectedRoomCard", "Selected Room");
        roomWorkersText = FindOrCreateText(roomWorkersPanel, "Text_WorkerCard", "Workers");
        roomDemandText = FindOrCreateText(roomDemandPanel, "Text_RoomDemandCard", "Demand");
        roomWorkerPopupText = FindOrCreateText(roomWorkerPopupPanel, "Text_RoomWorkerPopup", "Assign Worker");
        ApplyCardText(selectedRoomText, 15f);
        ApplyCardText(roomWorkersText, 13f);
        ApplyCardText(roomDemandText, 13f);
        ApplyCardText(roomWorkerPopupText, 14f);
        roomWorkerPopupText.rectTransform.offsetMax = new Vector2(-18f, -100f);

        roomWorkersPanel.gameObject.SetActive(true);
        roomActionsPanel.gameObject.SetActive(true);
        roomWorkerPopupPanel.gameObject.SetActive(false);

        ApplyActionGrid(roomActionsPanel, 2, new Vector2(210f, 42f), 10f);
        roomAssignHousekeeperButton = FindOrCreateActionButton(roomActionsPanel, "Button_RoomAssignHSK", "Assign HSK", OpenHousekeeperPopup);
        roomMarkCleanPriorityButton = FindOrCreateActionButton(roomActionsPanel, "Button_RoomMarkCleanPriority", "Clean Prio", MarkDirtyPriority);
        roomAssignInspectorButton = FindOrCreateActionButton(roomActionsPanel, "Button_RoomAssignInspector", "Assign Insp", OpenInspectorPopup);
        roomMarkInspectionPriorityButton = FindOrCreateActionButton(roomActionsPanel, "Button_RoomMarkInspectionPriority", "Insp Prio", MarkInspectionPriority);
        roomReserveButton = FindOrCreateActionButton(roomActionsPanel, "Button_RoomReserve", "Reserve", ReserveSelectedRoom);
        roomAssignDemandButton = FindOrCreateActionButton(roomActionsPanel, "Button_RoomAssignDemand", "Assign Guest", AssignSelectedRoomToDemand);
        HideLegacyRoomActionButtons();

        ApplyActionGrid(roomWorkerPopupButtonPanel, 2, new Vector2(190f, 38f), 8f);
        roomSelectNextHousekeeperButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomSelectNextHSK", "Next HSK", SelectNextHousekeeper);
        roomSelectHousekeeperButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomSelectHSK", "Use HSK", SelectHousekeeper);
        roomSelectInspectorButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomSelectInsp", "Use Insp", SelectInspector);
        roomConfirmWorkerButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomConfirmWorker", "Confirm", ConfirmSelectedWorkerFromPopup);
        roomCancelWorkerPopupButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomCancelWorkerPopup", "Cancel", CloseWorkerPopup);
    }

    private void HideLegacyRoomActionButtons()
    {
        if (roomActionsPanel == null)
        {
            return;
        }

        // 旧调试按钮仍可能留在已有场景层级里；这里隐藏它们，避免和新的状态动作按钮混在一起。
        string[] legacyButtonNames =
        {
            "Button_PreviousRoomShowcase",
            "Button_NextRoomShowcase",
            "Button_SelectHSKShowcase",
            "Button_SelectInspShowcase",
            "Button_AssignWorkerShowcase",
            "Button_ReserveShowcase",
            "Button_DirtyPrioShowcase",
            "Button_InspPrioShowcase",
            "Button_AssignDemandRoomView"
        };

        for (int i = 0; i < legacyButtonNames.Length; i++)
        {
            Transform legacyButton = roomActionsPanel.Find(legacyButtonNames[i]);
            if (legacyButton != null)
            {
                legacyButton.gameObject.SetActive(false);
            }
        }
    }

    private void BuildLoungeViewContent()
    {
        loungeStatusPanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeStatus", new Color(0.05f, 0.07f, 0.09f, 0.96f));
        loungeActionsPanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeActions", new Color(0.05f, 0.05f, 0.07f, 0.96f));
        loungeResultPanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeResult", new Color(0.05f, 0.06f, 0.08f, 0.96f));

        ApplyAnchoredPanel(loungeStatusPanel, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(loungeResultPanel, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.44f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(loungeActionsPanel, new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.28f), Vector2.zero, Vector2.zero);

        loungeStatusText = FindOrCreateText(loungeStatusPanel, "Text_LoungeStatus", "Lounge");
        loungeResultText = FindOrCreateText(loungeResultPanel, "Text_LoungeResult", "Lounge Result");
        ApplyCardText(loungeStatusText, 18f);
        ApplyCardText(loungeResultText, 15f);

        ApplyActionGrid(loungeActionsPanel, 2, new Vector2(190f, 42f), 12f);
        FindOrCreateActionButton(loungeActionsPanel, "Button_ServeLoungeShowcase", "Serve", ServeLoungeNow);
        FindOrCreateActionButton(loungeActionsPanel, "Button_WashCupsShowcase", "Wash", StartLoungeWash);
        FindOrCreateActionButton(loungeActionsPanel, "Button_RestockLoungeShowcase", "Restock", RestockLounge);
        FindOrCreateActionButton(loungeActionsPanel, "Button_StartDayLoungeShowcase", "Start", StartDemoOperatingPeriod);
    }

    private string BuildFrontDeskStatusText()
    {
        string phase = demoDayController != null ? demoDayController.currentPhase.ToString() : "None";
        string time = demoDayController != null ? FormatSeconds(demoDayController.operatingTimerSeconds) : "0s";
        string duration = demoDayController != null ? FormatSeconds(demoDayController.operatingDurationSeconds) : "0s";
        int queue = frontDesk != null ? frontDesk.currentQueueCount : 0;
        string wait = frontDesk != null ? FormatSeconds(frontDesk.waitingTimePressureSeconds) : "0s";
        int delayed = frontDesk != null ? frontDesk.totalDelayedCheckIns : 0;
        string score = demandLoop != null
            ? demandLoop.prototypeSatisfactionScore + " (" + demandLoop.prototypeSatisfactionTrend + ")"
            : "0";
        string pressure = GetFrontDeskPressureLabel(queue, delayed);

        return "Old Town Hotel\n"
            + phase + "  |  " + time + " / " + duration + "\n\n"
            + "Queue Pressure\n"
            + "Queue: " + queue + "    Wait: " + wait + "\n"
            + "Delayed: " + delayed + "    Pressure: " + pressure + "\n"
            + "Satisfaction: " + score;
    }

    private string BuildFrontDeskDemandText()
    {
        if (demandLoop == null)
        {
            return "Current Guest\nNo demand loop linked.";
        }

        string guestState = demandLoop.activeDemandWaitingForManualAssignment ? "Waiting" : "No active guest";
        string roomReady = HasReadyRoomForFrontDesk() ? "Ready room available" : "No ready room";
        string suggestion = GetFrontDeskSuggestionText();

        return "Current Request\n"
            + "Guest: " + GetCurrentFrontDeskGuestTypeText() + "\n"
            + "State: " + guestState + "\n"
            + "Wait: " + FormatSeconds(demandLoop.activeDemandWaitSeconds) + "\n"
            + "Room: " + roomReady + "\n"
            + "Suggestion: " + suggestion;
    }

    private string BuildFrontDeskResultText()
    {
        if (demandLoop == null)
        {
            return "Demand Summary\nNo demand loop";
        }

        return "Demand Summary\n"
            + "Upcoming: " + demandLoop.upcomingDemandType
            + " in " + FormatSeconds(demandLoop.upcomingDemandEtaSeconds) + "\n"
            + "Active: " + GetActiveDemandShortLine() + "\n"
            + "Latest: " + demandLoop.lastChangedRoomName
            + " / " + demandLoop.lastOutcomeLabel + "\n"
            + "Result: " + demandLoop.successfulDemandCount
            + " served, " + demandLoop.unmetDemandCount + " missed";
    }

    private string BuildSelectedRoomText()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        if (room == null)
        {
            return "Selected Room\nTap a room to inspect it.";
        }

        string reservedText = demandLoop != null && demandLoop.IsRoomReservedForPrototypeDemand(room) ? "Yes" : "No";
        string matchHint = demandLoop != null ? demandLoop.GetPrototypeMatchHintForRoom(room) : "Match Hint: None";
        string cleanSuitability = demandLoop != null ? demandLoop.GetPrototypeCleanlinessSuitability(room).ToString() : "N/A";
        string wearSuitability = demandLoop != null ? demandLoop.GetPrototypeWearSuitability(room).ToString() : "N/A";

        return "Selected Room\n"
            + room.roomName + "  |  State: " + room.GetStateDisplayName() + "\n"
            + "Type: " + room.prototypeRoomType
            + "  |  Floor: " + room.floorNumber
            + "  |  Facing: " + room.prototypeFacing + "\n"
            + "Checked Out: " + GetYesNo(room.guestCheckedOut)
            + "  |  Reserved: " + reservedText + "\n"
            + "Clean Priority: " + GetYesNo(room.markedCleaningPriority)
            + "  |  Insp Priority: " + GetYesNo(room.markedInspectionPriority) + "\n"
            + "Clean Suitability: " + cleanSuitability
            + "  |  Wear Suitability: " + wearSuitability
            + "  |  Wait: " + FormatSeconds(room.stateElapsedSeconds) + "\n"
            + ShortenMatchHint(matchHint);
    }

    private string BuildRoomWorkerText()
    {
        return "Room Actions\n"
            + roomActionHint + "\n"
            + BuildCompactWorkerText();
    }

    private string BuildRoomDemandText()
    {
        if (demandLoop == null)
        {
            return "Demand\nNone";
        }

        return "Demand\n"
            + "Upcoming: " + demandLoop.upcomingDemandType
            + " in " + FormatSeconds(demandLoop.upcomingDemandEtaSeconds) + "\n"
            + "Active: " + GetActiveDemandShortLine() + "\n"
            + "Reserved: " + demandLoop.reservedRoomName + "\n"
            + "Last: " + demandLoop.lastChangedRoomName
            + " / " + demandLoop.lastOutcomeLabel;
    }

    private void RefreshRoomsViewActionState()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        bool hasRoom = room != null;
        bool isDirty = hasRoom && room.currentState == Room2DState.Dirty;
        bool isAwaitingInspection = hasRoom && room.currentState == Room2DState.AwaitingInspection;
        bool isReady = hasRoom && room.currentState == Room2DState.Ready;

        SetButtonVisible(roomAssignHousekeeperButton, isDirty);
        SetButtonVisible(roomMarkCleanPriorityButton, isDirty);
        SetButtonVisible(roomAssignInspectorButton, isAwaitingInspection);
        SetButtonVisible(roomMarkInspectionPriorityButton, isAwaitingInspection);
        SetButtonVisible(roomReserveButton, isReady);
        SetButtonVisible(roomAssignDemandButton, isReady);

        if (roomActionsPanel != null)
        {
            roomActionsPanel.gameObject.SetActive(isDirty || isAwaitingInspection || isReady);
        }

        if (roomWorkerPopupPanel != null)
        {
            roomWorkerPopupPanel.gameObject.SetActive(roomWorkerPopupVisible && currentView == ShowcaseView.Rooms);
        }

        if (roomWorkerPopupText != null)
        {
            roomWorkerPopupText.text = BuildWorkerPopupText();
        }

        RefreshWorkerPopupButtonState();
    }

    private void RefreshWorkerPopupButtonState()
    {
        bool popupVisible = roomWorkerPopupVisible && workerSelectionPanel != null;
        bool selectingHousekeeper = popupVisible
            && workerSelectionPanel.selectedWorkerType == Room2DWorkerSelectionPanel.PrototypeWorkerType.Housekeeper;
        bool selectingInspector = popupVisible
            && workerSelectionPanel.selectedWorkerType == Room2DWorkerSelectionPanel.PrototypeWorkerType.Inspector;

        SetButtonVisible(roomSelectNextHousekeeperButton, selectingHousekeeper);
        SetButtonVisible(roomSelectHousekeeperButton, selectingHousekeeper);
        SetButtonVisible(roomSelectInspectorButton, selectingInspector);
        SetButtonVisible(roomConfirmWorkerButton, popupVisible);
        SetButtonVisible(roomCancelWorkerPopupButton, popupVisible);
    }

    private string BuildWorkerPopupText()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        string roomName = room != null ? room.roomName : "None";
        string workerName = workerSelectionPanel != null ? workerSelectionPanel.selectedWorkerName : "None";
        string lastResult = workerSelectionPanel != null ? workerSelectionPanel.lastManualAssignmentResult : "None";

        return "Assign Worker\n"
            + "Room: " + roomName + "\n"
            + "Selected Worker: " + workerName + "\n"
            + "Confirm to assign. Cancel to close.\n"
            + "Last: " + lastResult;
    }

    private void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    private string BuildLoungeStatusText()
    {
        string demoText = demoDayController != null
            ? demoDayController.GetCompactDemoDayText()
            : "Demo: None";

        return "Lounge View\n"
            + demoText + "\n\n"
            + BuildCompactLoungeText();
    }

    private string BuildLoungeResultText()
    {
        if (lounge == null)
        {
            return "Lounge Result\nNone";
        }

        return "Lounge Result\n"
            + "Warning: " + lounge.loungeWarning + "\n"
            + "Served/Missed: " + lounge.servedDrinkCount + " / " + lounge.missedServiceCount + "\n"
            + "Last: " + lounge.lastLoungeResult;
    }

    private string BuildRoomCountSummaryText()
    {
        if (roomOverview == null)
        {
            return "Rooms\nNo overview";
        }

        roomOverview.RefreshSummary();

        int dirty = 0;
        int cleaning = 0;
        int inspection = 0;
        int ready = 0;
        int occupied = 0;
        int blocked = 0;

        if (roomOverview.rooms != null)
        {
            for (int i = 0; i < roomOverview.rooms.Length; i++)
            {
                Room2DEntity room = roomOverview.rooms[i];
                if (room == null)
                {
                    continue;
                }

                CountRoomState(room.currentState, ref dirty, ref cleaning, ref inspection, ref ready, ref occupied, ref blocked);
            }
        }

        return "Rooms\n"
            + "Dirty: " + dirty + "  Cleaning: " + cleaning + "\n"
            + "Inspect: " + inspection + "  Ready: " + ready + "\n"
            + "Occupied: " + occupied + "  Blocked: " + blocked + "\n"
            + "Urgent Dirty: " + roomOverview.highestPriorityDirtyRoomName
            + " " + FormatSeconds(roomOverview.highestPriorityDirtySeconds);
    }

    private string BuildCompactWorkerText()
    {
        if (workerSelectionPanel == null)
        {
            return "Workers\nNone";
        }

        return "Workers\n"
            + "Selected: " + workerSelectionPanel.selectedWorkerName + "\n"
            + "Last: " + workerSelectionPanel.lastManualAssignmentResult;
    }

    private string BuildCompactLoungeText()
    {
        if (lounge == null)
        {
            return "Lounge\nNone";
        }

        string washingText = lounge.washing
            ? "Yes " + FormatSeconds(lounge.washTimerSeconds) + " / " + FormatSeconds(lounge.washDurationSeconds)
            : "No";

        return "Stock\n"
            + "Clean Cups: " + lounge.cleanCups + "\n"
            + "Dirty Cups: " + lounge.dirtyCups + "\n"
            + "Milk: " + lounge.milkStock + "\n"
            + "Tea/Coffee: " + lounge.teaCoffeeStock + "\n"
            + "Washing: " + washingText;
    }

    private string GetActiveDemandShortLine()
    {
        if (!demandLoop.activeDemandWaitingForManualAssignment)
        {
            return "None";
        }

        return demandLoop.activeDemandType
            + " waiting " + FormatSeconds(demandLoop.activeDemandWaitSeconds)
            + " / " + FormatSeconds(demandLoop.manualAssignmentFallbackDelaySeconds);
    }

    private string GetComplaintShortLine()
    {
        if (demandLoop.complaintWaitingForReassignment)
        {
            return "Waiting " + FormatSeconds(demandLoop.complaintReassignmentWaitSeconds)
                + " / patience " + FormatSeconds(demandLoop.complaintPatienceRemainingSeconds);
        }

        if (demandLoop.pendingComplaintRoom != null)
        {
            return "Pending from " + demandLoop.pendingComplaintRoomName;
        }

        return "None";
    }

    private string GetFrontDeskLastResultText()
    {
        if (frontDesk == null || string.IsNullOrEmpty(frontDesk.lastFrontDeskResult))
        {
            return "None";
        }

        return frontDesk.lastFrontDeskResult;
    }

    private string GetFrontDeskPressureLabel(int queue, int delayed)
    {
        if (delayed > 0 || queue >= 3)
        {
            return "High";
        }

        if (queue > 0)
        {
            return "Medium";
        }

        return "Low";
    }

    private string GetCurrentFrontDeskGuestTypeText()
    {
        if (demandLoop == null)
        {
            return "None";
        }

        if (demandLoop.activeDemandWaitingForManualAssignment)
        {
            return demandLoop.activeDemandType.ToString();
        }

        return demandLoop.upcomingDemandType + " incoming";
    }

    private string GetFrontDeskSuggestionText()
    {
        if (demandLoop == null)
        {
            return "Link demand loop.";
        }

        if (demandLoop.activeDemandWaitingForManualAssignment)
        {
            return HasReadyRoomForFrontDesk() ? "Assign a ready room." : "Wait or prepare a room.";
        }

        if (demandLoop.upcomingDemandEtaSeconds > 0f)
        {
            return "Prepare before arrival.";
        }

        return "Call next guest.";
    }

    private bool HasReadyRoomForFrontDesk()
    {
        Room2DEntity[] rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room != null && room.currentState == Room2DState.Ready)
            {
                return true;
            }
        }

        return false;
    }

    private string ShortenMatchHint(string matchHint)
    {
        if (string.IsNullOrEmpty(matchHint))
        {
            return "Match: None";
        }

        string[] lines = matchHint.Split('\n');
        if (lines.Length == 0)
        {
            return matchHint;
        }

        return lines[0];
    }

    private string GetPhaseActionHint()
    {
        if (demoDayController == null)
        {
            return "Start the demo.";
        }

        switch (demoDayController.currentPhase)
        {
            case Room2DDemoDayController.DemoDayPhase.Preparation:
                return "Prepare rooms, then press Start.";
            case Room2DDemoDayController.DemoDayPhase.Operating:
                if (demandLoop != null && demandLoop.activeDemandWaitingForManualAssignment)
                {
                    return "Select a Ready room, then Assign.";
                }

                return "Watch ETA and keep rooms Ready.";
            case Room2DDemoDayController.DemoDayPhase.Ended:
                return "Review result, then Reset.";
            default:
                return "Continue.";
        }
    }

    private void CountRoomState(
        Room2DState state,
        ref int dirty,
        ref int cleaning,
        ref int inspection,
        ref int ready,
        ref int occupied,
        ref int blocked)
    {
        switch (state)
        {
            case Room2DState.Dirty:
                dirty++;
                break;
            case Room2DState.Cleaning:
                cleaning++;
                break;
            case Room2DState.AwaitingInspection:
                inspection++;
                break;
            case Room2DState.Ready:
                ready++;
                break;
            case Room2DState.Occupied:
                occupied++;
                break;
            case Room2DState.Blocked:
                blocked++;
                break;
        }
    }

    private void StartDemoOperatingPeriod()
    {
        FindReferencesIfNeeded();
        if (demoDayController != null)
        {
            demoDayController.StartOperatingPeriod();
        }
    }

    private void ActivateUpcomingDemandNow()
    {
        FindReferencesIfNeeded();
        if (demandLoop != null)
        {
            demandLoop.ActivateUpcomingDemandNow();
        }
    }

    private void EndDemoDay()
    {
        FindReferencesIfNeeded();
        if (demoDayController != null)
        {
            demoDayController.EndDemoDay();
        }
    }

    private void RestartDemoDay()
    {
        FindReferencesIfNeeded();
        if (demoDayController != null)
        {
            demoDayController.RestartDemoDay();
        }
    }

    private void AssignSelectedRoomToDemand()
    {
        FindReferencesIfNeeded();
        if (demandLoop != null)
        {
            demandLoop.AssignSelectedRoomToActiveDemand();
            roomActionHint = demandLoop.lastManualAssignmentResult;
        }
    }

    private void ReserveSelectedRoom()
    {
        FindReferencesIfNeeded();
        if (demandLoop != null)
        {
            demandLoop.ReserveSelectedRoomForUpcomingDemand();
            roomActionHint = demandLoop.lastPreparationAction;
        }
    }

    private void MarkDirtyPriority()
    {
        FindReferencesIfNeeded();
        if (demandLoop != null)
        {
            demandLoop.MarkSelectedDirtyRoomAsPriority();
            roomActionHint = demandLoop.lastPreparationAction;
        }
    }

    private void MarkInspectionPriority()
    {
        FindReferencesIfNeeded();
        if (demandLoop != null)
        {
            demandLoop.MarkSelectedInspectionRoomAsPriority();
            roomActionHint = demandLoop.lastPreparationAction;
        }
    }

    private void SelectPreviousRoom()
    {
        FindReferencesIfNeeded();
        if (selectionManager != null)
        {
            selectionManager.SelectPreviousRoom();
        }
    }

    private void SelectNextRoom()
    {
        FindReferencesIfNeeded();
        if (selectionManager != null)
        {
            selectionManager.SelectNextRoom();
        }
    }

    private void SelectHousekeeper()
    {
        FindReferencesIfNeeded();
        if (workerSelectionPanel != null)
        {
            workerSelectionPanel.SelectHousekeeper();
            roomActionHint = "Selected worker: " + workerSelectionPanel.selectedWorkerName;
        }
    }

    private void SelectNextHousekeeper()
    {
        FindReferencesIfNeeded();
        if (workerSelectionPanel != null)
        {
            workerSelectionPanel.SelectNextHousekeeper();
            roomActionHint = "Selected worker: " + workerSelectionPanel.selectedWorkerName;
        }
    }

    private void SelectInspector()
    {
        FindReferencesIfNeeded();
        if (workerSelectionPanel != null)
        {
            workerSelectionPanel.SelectInspector();
            roomActionHint = "Selected worker: " + workerSelectionPanel.selectedWorkerName;
        }
    }

    private void AssignSelectedWorker()
    {
        FindReferencesIfNeeded();
        if (workerSelectionPanel != null)
        {
            workerSelectionPanel.AssignSelectedWorkerToSelectedRoom();
            roomActionHint = workerSelectionPanel.lastManualAssignmentResult;
        }
    }

    private void OpenHousekeeperPopup()
    {
        SelectHousekeeper();
        roomWorkerPopupVisible = true;
        roomActionHint = "Choose HSK, then Confirm.";
    }

    private void OpenInspectorPopup()
    {
        SelectInspector();
        roomWorkerPopupVisible = true;
        roomActionHint = "Choose Inspector, then Confirm.";
    }

    private void ConfirmSelectedWorkerFromPopup()
    {
        AssignSelectedWorker();
        roomWorkerPopupVisible = false;
    }

    private void CloseWorkerPopup()
    {
        roomWorkerPopupVisible = false;
        roomActionHint = "Worker assignment cancelled.";
    }

    private void ServeLoungeNow()
    {
        FindReferencesIfNeeded();
        if (lounge != null)
        {
            lounge.ServeOneLoungeDemand();
        }
    }

    private void StartLoungeWash()
    {
        FindReferencesIfNeeded();
        if (lounge != null)
        {
            lounge.StartWashingCups();
        }
    }

    private void RestockLounge()
    {
        FindReferencesIfNeeded();
        if (lounge != null)
        {
            lounge.RestockPrototypeLounge();
        }
    }

    private void RecordWaitAction()
    {
        lastShellResult = "Front desk waits. Existing demand timers keep running.";
    }

    private Room2DEntity GetSelectedRoomEntity()
    {
        if (selectionManager == null || selectionManager.selectedRoom == null)
        {
            return null;
        }

        return selectionManager.selectedRoom.roomEntity;
    }

    private string GetSelectedRoomDisplayNameForHint()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        return room != null ? room.roomName : "None";
    }

    private string GetYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    private string FormatSeconds(float seconds)
    {
        int wholeSeconds = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        int minutes = wholeSeconds / 60;
        int remainingSeconds = wholeSeconds % 60;

        if (minutes > 0)
        {
            return minutes + "m " + remainingSeconds + "s";
        }

        return remainingSeconds + "s";
    }

    private string GetViewTitleText()
    {
        switch (currentView)
        {
            case ShowcaseView.FrontDesk:
                return "Front Desk";
            case ShowcaseView.Rooms:
                return "Rooms";
            case ShowcaseView.Lounge:
                return "Lounge";
            default:
                return "Showcase";
        }
    }

    private void ApplyPrototypeDebugLabelVisibility()
    {
        // OnGUI 房间标签会盖在 Canvas 之上；录屏时只在 Rooms 页面显示。
        Room2DController.hidePrototypeDebugLabelsGlobally = currentView != ShowcaseView.Rooms;
    }

    private void HandleRoomsViewClickSelection()
    {
        if (!enableRoomsViewClickSelection || currentView != ShowcaseView.Rooms)
        {
            return;
        }

        Vector2 screenPosition;
        if (!TryGetPointerDownScreenPosition(out screenPosition))
        {
            return;
        }

        if (IsPointerInsideBottomNavigation(screenPosition))
        {
            return;
        }

        if (IsPointerInsideRoomsViewUi(screenPosition))
        {
            return;
        }

        Room2DController clickedRoom = FindRoomAtScreenPosition(screenPosition);
        if (clickedRoom == null)
        {
            return;
        }

        FindReferencesIfNeeded();
        if (selectionManager != null)
        {
            selectionManager.SelectRoom(clickedRoom);
            roomWorkerPopupVisible = false;
            roomActionHint = "Selected " + GetSelectedRoomDisplayNameForHint() + ".";
        }
    }

    private bool TryGetPointerDownScreenPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        // 项目使用 Unity 新 Input System 时，不能读取 UnityEngine.Input。
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null)
        {
            var primaryTouch = Touchscreen.current.primaryTouch;
            if (primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = primaryTouch.position.ReadValue();
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                screenPosition = touch.position;
                return true;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = Vector2.zero;
        return false;
    }

    private bool IsPointerInsideBottomNavigation(Vector2 screenPosition)
    {
        if (navigationPanel == null || !navigationPanel.gameObject.activeInHierarchy)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(navigationPanel, screenPosition, null);
    }

    private bool IsPointerInsideRoomsViewUi(Vector2 screenPosition)
    {
        return IsPointerInsideActiveRect(roomSelectedPanel, screenPosition)
            || IsPointerInsideActiveRect(roomDemandPanel, screenPosition)
            || IsPointerInsideActiveRect(roomWorkersPanel, screenPosition)
            || IsPointerInsideActiveRect(roomActionsPanel, screenPosition)
            || IsPointerInsideActiveRect(roomWorkerPopupPanel, screenPosition);
    }

    private bool IsPointerInsideActiveRect(RectTransform rect, Vector2 screenPosition)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null);
    }

    private Room2DController FindRoomAtScreenPosition(Vector2 screenPosition)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return null;
        }

        Room2DController[] rooms = GetSelectableRooms();
        Room2DController closestRoom = null;
        float closestDistance = maxRoomClickDistancePixels;

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DController room = rooms[i];
            Rect roomRect;
            Vector2 roomCenter;

            if (room == null || !room.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!TryGetRoomScreenRect(room, mainCamera, out roomRect, out roomCenter))
            {
                continue;
            }

            if (roomRect.Contains(screenPosition))
            {
                return room;
            }

            float distance = Vector2.Distance(screenPosition, roomCenter);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestRoom = room;
            }
        }

        return closestRoom;
    }

    private Room2DController[] GetSelectableRooms()
    {
        FindReferencesIfNeeded();

        if (selectionManager != null && selectionManager.rooms != null && selectionManager.rooms.Length > 0)
        {
            return selectionManager.rooms;
        }

        return FindObjectsByType<Room2DController>(FindObjectsSortMode.None);
    }

    private bool TryGetRoomScreenRect(
        Room2DController room,
        Camera mainCamera,
        out Rect screenRect,
        out Vector2 screenCenter)
    {
        Bounds bounds;
        screenRect = new Rect();
        screenCenter = Vector2.zero;

        if (!TryGetVisibleSpriteBounds(room, out bounds))
        {
            return false;
        }

        Vector3 center = mainCamera.WorldToScreenPoint(bounds.center);
        if (center.z < 0f)
        {
            return false;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };

        float left = float.MaxValue;
        float right = float.MinValue;
        float bottom = float.MaxValue;
        float top = float.MinValue;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(corners[i]);
            left = Mathf.Min(left, screenPoint.x);
            right = Mathf.Max(right, screenPoint.x);
            bottom = Mathf.Min(bottom, screenPoint.y);
            top = Mathf.Max(top, screenPoint.y);
        }

        left -= roomClickPaddingPixels;
        right += roomClickPaddingPixels;
        bottom -= roomClickPaddingPixels;
        top += roomClickPaddingPixels;

        screenRect = Rect.MinMaxRect(left, bottom, right, top);
        screenCenter = new Vector2(center.x, center.y);
        return true;
    }

    private bool TryGetVisibleSpriteBounds(Room2DController room, out Bounds combinedBounds)
    {
        SpriteRenderer[] renderers = room.GetComponentsInChildren<SpriteRenderer>(true);
        combinedBounds = new Bounds(room.transform.position, Vector3.zero);
        bool foundRenderer = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
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

    private RectTransform FindOrCreateRectChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject child = new GameObject(childName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private RectTransform FindOrCreatePanel(RectTransform parent, string panelName, Color color)
    {
        RectTransform panel = FindOrCreateRectChild(parent, panelName);
        Image image = panel.GetComponent<Image>();

        if (image == null)
        {
            image = panel.gameObject.AddComponent<Image>();
        }

        image.color = color;
        return panel;
    }

    private TMP_Text FindOrCreateText(RectTransform parent, string textName, string defaultText)
    {
        Transform existing = parent.Find(textName);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (text != null)
        {
            return text;
        }

        GameObject textObject = new GameObject(textName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = defaultText;
        text.raycastTarget = false;
        return text;
    }

    private Button FindOrCreateButton(RectTransform parent, string buttonName, string label)
    {
        RectTransform buttonRect = FindOrCreateRectChild(parent, buttonName);

        Image image = buttonRect.GetComponent<Image>();
        if (image == null)
        {
            image = buttonRect.gameObject.AddComponent<Image>();
        }

        image.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        Button button = buttonRect.GetComponent<Button>();
        if (button == null)
        {
            button = buttonRect.gameObject.AddComponent<Button>();
        }

        LayoutElement layoutElement = buttonRect.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = buttonRect.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.preferredWidth = 152f;
        layoutElement.preferredHeight = 48f;

        TMP_Text labelText = FindOrCreateText(buttonRect, "Text (TMP)", label);
        labelText.text = label;
        ApplyDefaultFont(labelText);
        labelText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        labelText.fontSize = 17f;
        labelText.alignment = TextAlignmentOptions.Center;
        ApplyStretch(labelText.rectTransform);

        return button;
    }

    private Button FindOrCreateActionButton(RectTransform parent, string buttonName, string label, UnityAction action)
    {
        Button button = FindOrCreateButton(parent, buttonName, label);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        return button;
    }

    private Button FindOrCreateActionButtonWithIconPlaceholder(RectTransform parent, string buttonName, string label, UnityAction action)
    {
        Button button = FindOrCreateActionButton(parent, buttonName, label);
        RectTransform buttonRect = button.transform as RectTransform;

        RectTransform iconPlaceholder = FindOrCreatePlaceholder(buttonRect, "ButtonIconPlaceholder_" + label, "");
        ApplyAnchoredPanel(iconPlaceholder, new Vector2(0.06f, 0.24f), new Vector2(0.20f, 0.76f), Vector2.zero, Vector2.zero);

        Transform labelTransform = buttonRect.Find("Text (TMP)");
        TMP_Text labelText = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        if (labelText != null)
        {
            labelText.rectTransform.offsetMin = new Vector2(34f, 0f);
        }

        return button;
    }

    private RectTransform FindOrCreatePlaceholder(RectTransform parent, string placeholderName, string label)
    {
        RectTransform placeholder = FindOrCreatePanel(parent, placeholderName, new Color(1f, 1f, 1f, 0.10f));
        Image image = placeholder.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = false;
        }

        TMP_Text labelText = FindOrCreateText(placeholder, "Text_PlaceholderLabel", label);
        labelText.text = label;
        labelText.color = new Color(1f, 1f, 1f, 0.72f);
        labelText.fontSize = 12f;
        labelText.alignment = TextAlignmentOptions.Center;
        ApplyDefaultFont(labelText);
        ApplyStretch(labelText.rectTransform);

        return placeholder;
    }

    private void ApplyAnchoredPanel(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private void ApplyCardText(TMP_Text text, float fontSize)
    {
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18f, 16f);
        rect.offsetMax = new Vector2(-18f, -16f);
        rect.localScale = Vector3.one;

        text.color = Color.white;
        ApplyDefaultFont(text);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    private void ApplyActionGrid(RectTransform panel, int columns, Vector2 cellSize, float spacing)
    {
        GridLayoutGroup grid = panel.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = panel.gameObject.AddComponent<GridLayoutGroup>();
        }

        grid.cellSize = cellSize;
        grid.spacing = new Vector2(spacing, spacing);
        grid.padding = new RectOffset(18, 18, 18, 18);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.childAlignment = TextAnchor.MiddleCenter;
    }

    private void WireTabButtons()
    {
        if (frontDeskTabButton != null)
        {
            frontDeskTabButton.onClick.RemoveListener(ShowFrontDeskView);
            frontDeskTabButton.onClick.AddListener(ShowFrontDeskView);
        }

        if (roomTabButton != null)
        {
            roomTabButton.onClick.RemoveListener(ShowRoomView);
            roomTabButton.onClick.AddListener(ShowRoomView);
        }

        if (loungeTabButton != null)
        {
            loungeTabButton.onClick.RemoveListener(ShowLoungeView);
            loungeTabButton.onClick.AddListener(ShowLoungeView);
        }
    }

    private void HideOldNavLabelIfItExists()
    {
        if (navigationPanel == null)
        {
            return;
        }

        Transform oldLabel = navigationPanel.Find("Text_ActiveShowcaseView");
        if (oldLabel != null)
        {
            oldLabel.gameObject.SetActive(false);
        }
    }

    private void HideDuplicateShowcaseRoots()
    {
        RectTransform[] roots = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < roots.Length; i++)
        {
            RectTransform root = roots[i];
            if (root == null || root == showcaseRoot || root.name != RootName)
            {
                continue;
            }

            if (root.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            root.gameObject.SetActive(false);
        }
    }

    private void HideShellTextHeaders()
    {
        if (frontDeskShellText != null)
        {
            frontDeskShellText.gameObject.SetActive(false);
        }

        if (roomShellText != null)
        {
            roomShellText.gameObject.SetActive(false);
        }

        if (loungeShellText != null)
        {
            loungeShellText.gameObject.SetActive(false);
        }
    }

    private void ApplyNavigationLayout(RectTransform panel)
    {
        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 18f;
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private void ApplyStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void ApplyBottomPanel(RectTransform rect, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, height);
    }

    private void ApplyViewPanel(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(0f, 88f);
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void ApplyShellText(TMP_Text text, TextAlignmentOptions alignment)
    {
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -18f);
        rect.sizeDelta = new Vector2(-48f, 110f);

        text.color = Color.white;
        ApplyDefaultFont(text);
        text.fontSize = 18f;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    private void ApplyNavLabelText(TMP_Text text)
    {
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -18f);
        rect.sizeDelta = new Vector2(260f, 40f);

        text.color = Color.white;
        ApplyDefaultFont(text);
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
    }

    private void SetPanelRaycast(RectTransform panel, bool enabled)
    {
        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = enabled;
        }
    }

    private void HideLegacyDebugHudIfSafe()
    {
        if (!hideLegacyDebugHudWhileShowcaseRuns || debugHud == null)
        {
            return;
        }

        // 如果这个脚本正好挂在旧 HUD 自己或它的子物体上，就不能隐藏旧 HUD，否则会把自己也关掉。
        if (debugHud.gameObject == gameObject || transform.IsChildOf(debugHud.transform))
        {
            return;
        }

        debugHud.gameObject.SetActive(false);
    }

    private void ApplyDefaultFont(TMP_Text text)
    {
        if (text.font != null || TMP_Settings.defaultFontAsset == null)
        {
            return;
        }

        text.font = TMP_Settings.defaultFontAsset;
    }
}
