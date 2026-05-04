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
    public RectTransform frontDeskWaitingGuestPanel;
    public RectTransform frontDeskWaitingGuestContent;
    public RectTransform frontDeskGuestDetailPopup;
    public RectTransform frontDeskReadyRoomListPopup;
    public RectTransform frontDeskReadyRoomListContent;
    public RectTransform frontDeskPageActionPanel;
    public RectTransform frontDeskQueueIconPlaceholder;
    public RectTransform frontDeskGuestPortraitPlaceholder;
    public RectTransform frontDeskWarningBadgePlaceholder;
    public RectTransform roomSelectedPanel;
    public RectTransform roomWorkersPanel;
    public RectTransform roomActionsPanel;
    public RectTransform roomDemandPanel;
    public RectTransform roomWorkerPopupPanel;
    public RectTransform roomWorkerPopupButtonPanel;
    public RectTransform roomRightLauncherPanel;
    public RectTransform roomWaitingGuestPeekPanel;
    public RectTransform roomWaitingGuestCard;
    public RectTransform roomWaitingGuestPortraitPlaceholder;
    public RectTransform roomHousekeeperPanel;
    public RectTransform roomInspectorPanel;
    public RectTransform roomActionPopupPanel;
    public RectTransform roomActionPopupButtonPanel;
    public RectTransform roomInfoPopupPanel;
    public RectTransform roomTileInfoButtonPanel;
    public RectTransform roomTypeIconPlaceholder;
    public RectTransform roomStateIconPlaceholder;
    public RectTransform roomWorkerPortraitPlaceholder;
    public RectTransform loungeStatusPanel;
    public RectTransform loungeStockPanel;
    public RectTransform loungeMachinePanel;
    public RectTransform loungeActionsPanel;
    public RectTransform loungeResultPanel;
    public RectTransform loungeStockIconPlaceholder;
    public RectTransform loungeCupIconPlaceholder;
    public RectTransform loungeMachineIconPlaceholder;
    public RectTransform loungeWarningBadgePlaceholder;

    [Header("Texts")]
    public TMP_Text activeViewLabelText;
    public TMP_Text frontDeskShellText;
    public TMP_Text roomShellText;
    public TMP_Text loungeShellText;
    public TMP_Text frontDeskStatusText;
    public TMP_Text frontDeskDemandText;
    public TMP_Text frontDeskResultText;
    public TMP_Text frontDeskGuestDetailText;
    public TMP_Text frontDeskReadyRoomListText;
    public TMP_Text selectedRoomText;
    public TMP_Text roomWorkersText;
    public TMP_Text roomWorkerPopupText;
    public TMP_Text roomDemandText;
    public TMP_Text roomWaitingGuestPeekText;
    public TMP_Text roomWaitingGuestCardText;
    public TMP_Text roomHousekeeperPanelText;
    public TMP_Text roomInspectorPanelText;
    public TMP_Text roomActionPopupText;
    public TMP_Text roomInfoPopupText;
    public TMP_Text loungeStatusText;
    public TMP_Text loungeStockText;
    public TMP_Text loungeMachineText;
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
    public Button roomOpenWaitingGuestsButton;
    public Button roomOpenHousekeeperPanelButton;
    public Button roomOpenInspectorPanelButton;
    public Button roomCloseWaitingGuestsButton;
    public Button roomPopupAssignHousekeeperButton;
    public Button roomPopupMarkCleanPriorityButton;
    public Button roomPopupAssignInspectorButton;
    public Button roomPopupMarkInspectionPriorityButton;
    public Button roomPopupReserveButton;
    public Button roomPopupInfoButton;
    public Button roomPopupCloseButton;
    public Button roomInfoPopupCloseButton;
    public Button[] roomTileInfoButtons = new Button[24];
    public Button frontDeskAssignRoomButton;
    public Button frontDeskCloseGuestPopupButton;
    public Button frontDeskCloseReadyRoomPopupButton;
    public Button frontDeskGuestActiveButton;
    public Button frontDeskGuestComplaintButton;
    public Button frontDeskGuestUpcomingButton;
    public Button[] frontDeskReadyRoomButtons = new Button[6];

    [Header("Runtime")]
    public ShowcaseView currentView = ShowcaseView.Rooms;
    public string lastShellResult = "Not built";
    public bool roomWorkerPopupVisible;
    public bool roomWaitingGuestPeekVisible;
    public bool roomHousekeeperPanelVisible;
    public bool roomInspectorPanelVisible;
    public bool roomActionPopupVisible;
    public bool roomInfoPopupVisible;
    public string roomActionHint = "Tap a room.";
    public bool frontDeskGuestPopupVisible;
    public bool frontDeskReadyRoomPopupVisible;
    public int selectedFrontDeskGuestKind;
    public string frontDeskGuestActionHint = "Select a guest card.";

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
            activeViewLabelText.text = GetViewTitleText()
                + "  |  "
                + (demoDayController != null ? demoDayController.GetShowcasePhaseLabel() : "No Phase");
        }

        if (frontDeskShellText != null)
        {
            frontDeskShellText.text = "Front Desk\n"
                + phaseText + "\n"
                + "Receive guests and assign rooms";
        }

        if (roomShellText != null)
        {
            roomShellText.text = "Rooms\n"
                + phaseText + "\n"
                + "Turn rooms over and support check-in";
        }

        if (loungeShellText != null)
        {
            loungeShellText.text = "Lounge\n"
                + phaseText + "\n"
                + "Keep service ready during the rush";
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
            selectedRoomText.text = BuildCompactSelectedRoomText();
        }

        if (roomWorkersText != null)
        {
            roomWorkersText.text = BuildRoomWorkerText();
        }

        if (loungeStatusText != null)
        {
            loungeStatusText.text = BuildLoungeStatusText();
        }

        if (loungeStockText != null)
        {
            loungeStockText.text = BuildLoungeStockText();
        }

        if (loungeMachineText != null)
        {
            loungeMachineText.text = BuildLoungeMachineText();
        }

        if (frontDeskResultText != null)
        {
            frontDeskResultText.text = BuildFrontDeskResultText();
        }

        RefreshFrontDeskGuestCards();
        RefreshFrontDeskPopups();

        if (roomDemandText != null)
        {
            roomDemandText.text = BuildRoomDemandText();
        }

        RefreshRoomsViewActionState();

        if (loungeResultText != null)
        {
            loungeResultText.text = BuildLoungeResultText();
        }

        RefreshLoungePlaceholderLabels();
    }

    private void BuildFrontDeskViewContent()
    {
        frontDeskStatusPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_FrontDeskHeader", new Color(0.04f, 0.10f, 0.09f, 0.96f));
        frontDeskWaitingGuestPanel = FindOrCreatePanel(frontDeskViewPanel, "Panel_WaitingGuests", new Color(0.07f, 0.06f, 0.13f, 0.94f));
        frontDeskResultPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_FrontDeskCurrentRequest", new Color(0.08f, 0.09f, 0.15f, 0.95f));
        frontDeskPageActionPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_FrontDeskQuickActions", new Color(0.10f, 0.08f, 0.05f, 0.94f));
        frontDeskGuestDetailPopup = FindOrCreatePanel(frontDeskViewPanel, "Popup_GuestDetail", new Color(0.06f, 0.06f, 0.11f, 0.98f));
        frontDeskReadyRoomListPopup = FindOrCreatePanel(frontDeskViewPanel, "Popup_ReadyRoomList", new Color(0.05f, 0.07f, 0.12f, 0.98f));

        ApplyAnchoredPanel(frontDeskStatusPanel, new Vector2(0.07f, 0.76f), new Vector2(0.93f, 0.91f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskWaitingGuestPanel, new Vector2(0.07f, 0.54f), new Vector2(0.93f, 0.73f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskResultPanel, new Vector2(0.07f, 0.28f), new Vector2(0.93f, 0.50f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskPageActionPanel, new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.18f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskGuestDetailPopup, new Vector2(0.11f, 0.34f), new Vector2(0.89f, 0.67f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskReadyRoomListPopup, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.74f), Vector2.zero, Vector2.zero);

        frontDeskStatusText = FindOrCreateText(frontDeskStatusPanel, "Text_FrontDeskHeader", "Front Desk");
        frontDeskDemandText = FindOrCreateText(frontDeskWaitingGuestPanel, "Text_WaitingGuestHeader", "Waiting Guests");
        frontDeskResultText = FindOrCreateText(frontDeskResultPanel, "Text_FrontDeskCurrentRequest", "Current Request");
        frontDeskGuestDetailText = FindOrCreateText(frontDeskGuestDetailPopup, "Text_GuestDetail", "Guest Detail");
        frontDeskReadyRoomListText = FindOrCreateText(frontDeskReadyRoomListPopup, "Text_ReadyRoomListTitle", "Ready Rooms");
        ApplyCardText(frontDeskStatusText, 20f);
        ApplyCardText(frontDeskDemandText, 18f);
        ApplyCardText(frontDeskResultText, 18f);
        ApplyCardText(frontDeskGuestDetailText, 18f);
        ApplyCardText(frontDeskReadyRoomListText, 18f);
        frontDeskStatusText.rectTransform.offsetMax = new Vector2(-120f, -16f);
        frontDeskDemandText.rectTransform.anchorMin = new Vector2(0f, 1f);
        frontDeskDemandText.rectTransform.anchorMax = new Vector2(1f, 1f);
        frontDeskDemandText.rectTransform.pivot = new Vector2(0.5f, 1f);
        frontDeskDemandText.rectTransform.offsetMin = new Vector2(18f, -56f);
        frontDeskDemandText.rectTransform.offsetMax = new Vector2(-18f, -14f);
        frontDeskResultText.rectTransform.offsetMax = new Vector2(-150f, -18f);
        frontDeskGuestDetailText.rectTransform.offsetMin = new Vector2(24f, 108f);
        frontDeskGuestDetailText.rectTransform.offsetMax = new Vector2(-24f, -24f);
        frontDeskReadyRoomListText.rectTransform.anchorMin = new Vector2(0f, 1f);
        frontDeskReadyRoomListText.rectTransform.anchorMax = new Vector2(1f, 1f);
        frontDeskReadyRoomListText.rectTransform.offsetMin = new Vector2(18f, -56f);
        frontDeskReadyRoomListText.rectTransform.offsetMax = new Vector2(-18f, -14f);

        frontDeskQueueIconPlaceholder = FindOrCreatePlaceholder(frontDeskStatusPanel, "IconPlaceholder_QueuePressure", "QUEUE");
        ApplyAnchoredPanel(frontDeskQueueIconPlaceholder, new Vector2(0.78f, 0.20f), new Vector2(0.95f, 0.80f), Vector2.zero, Vector2.zero);
        ApplyPlaceholderStyle(frontDeskQueueIconPlaceholder, new Color(0.28f, 0.50f, 0.60f, 0.94f), 13f);
        frontDeskGuestPortraitPlaceholder = FindOrCreatePlaceholder(frontDeskGuestDetailPopup, "PortraitPlaceholder_CurrentGuest", "G");
        ApplyAnchoredPanel(frontDeskGuestPortraitPlaceholder, new Vector2(0.72f, 0.54f), new Vector2(0.87f, 0.82f), Vector2.zero, Vector2.zero);
        frontDeskWarningBadgePlaceholder = FindOrCreatePlaceholder(frontDeskGuestDetailPopup, "BadgePlaceholder_Warning", "WAIT");
        ApplyAnchoredPanel(frontDeskWarningBadgePlaceholder, new Vector2(0.87f, 0.62f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero);
        ApplyPlaceholderStyle(frontDeskGuestPortraitPlaceholder, new Color(0.22f, 0.28f, 0.37f, 0.96f), 18f);
        ApplyPlaceholderStyle(frontDeskWarningBadgePlaceholder, new Color(0.48f, 0.22f, 0.20f, 0.96f), 10f);

        RectTransform frontDeskCurrentGuestPortrait = FindOrCreatePlaceholder(frontDeskResultPanel, "PortraitPlaceholder_CurrentDeskGuest", "G");
        ApplyAnchoredPanel(frontDeskCurrentGuestPortrait, new Vector2(0.72f, 0.18f), new Vector2(0.84f, 0.82f), Vector2.zero, Vector2.zero);
        ApplyPlaceholderStyle(frontDeskCurrentGuestPortrait, new Color(0.22f, 0.28f, 0.37f, 0.96f), 18f);
        RectTransform frontDeskCurrentGuestBadge = FindOrCreatePlaceholder(frontDeskResultPanel, "BadgePlaceholder_CurrentDeskStatus", "NOW");
        ApplyAnchoredPanel(frontDeskCurrentGuestBadge, new Vector2(0.86f, 0.54f), new Vector2(0.96f, 0.82f), Vector2.zero, Vector2.zero);
        ApplyPlaceholderStyle(frontDeskCurrentGuestBadge, new Color(0.28f, 0.42f, 0.28f, 0.96f), 11f);

        frontDeskWaitingGuestContent = FindOrCreateHorizontalScrollContent(frontDeskWaitingGuestPanel, "Scroll_WaitingGuestStrip", "Content_WaitingGuestCards", 74f, 28f);
        frontDeskGuestActiveButton = FindOrCreateFrontDeskGuestCard(frontDeskWaitingGuestContent, "Card_Guest_Active", "Active Guest", SelectActiveDemandGuest);
        frontDeskGuestComplaintButton = FindOrCreateFrontDeskGuestCard(frontDeskWaitingGuestContent, "Card_Guest_Complaint", "Complaint", SelectComplaintGuest);
        frontDeskGuestUpcomingButton = FindOrCreateFrontDeskGuestCard(frontDeskWaitingGuestContent, "Card_Guest_Upcoming", "Next Guest", SelectUpcomingGuest);
        if (frontDeskWaitingGuestContent.parent != null && frontDeskWaitingGuestContent.parent.parent != null)
        {
            frontDeskWaitingGuestContent.parent.parent.SetAsLastSibling();
        }

        frontDeskAssignRoomButton = FindOrCreateActionButton(frontDeskGuestDetailPopup, "Button_OpenReadyRoomList", "Assign Room", OpenReadyRoomListPopup);
        ApplyAnchoredPanel(frontDeskAssignRoomButton.transform as RectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.52f, 0.22f), Vector2.zero, Vector2.zero);
        frontDeskCloseGuestPopupButton = FindOrCreateActionButton(frontDeskGuestDetailPopup, "Button_CloseGuestPopup", "Close", CloseFrontDeskPopups);
        ApplyAnchoredPanel(frontDeskCloseGuestPopupButton.transform as RectTransform, new Vector2(0.56f, 0.08f), new Vector2(0.92f, 0.22f), Vector2.zero, Vector2.zero);

        frontDeskReadyRoomListContent = FindOrCreateReadyRoomScrollContent(frontDeskReadyRoomListPopup);
        if (frontDeskReadyRoomButtons == null || frontDeskReadyRoomButtons.Length != 12)
        {
            frontDeskReadyRoomButtons = new Button[12];
        }

        for (int i = 0; i < frontDeskReadyRoomButtons.Length; i++)
        {
            int roomIndex = i;
            frontDeskReadyRoomButtons[i] = FindOrCreateReadyRoomCard(frontDeskReadyRoomListContent, "Card_ReadyRoom_" + (i + 1), "Ready Room", () => CheckInReadyRoomByVisibleIndex(roomIndex));
        }

        frontDeskCloseReadyRoomPopupButton = FindOrCreateActionButton(frontDeskReadyRoomListPopup, "Button_CloseReadyRoomList", "Close", CloseReadyRoomListPopup);
        ApplyAnchoredPanel(frontDeskCloseReadyRoomPopupButton.transform as RectTransform, new Vector2(0.62f, 0.04f), new Vector2(0.92f, 0.13f), Vector2.zero, Vector2.zero);

        ApplyActionGrid(frontDeskPageActionPanel, 3, new Vector2(154f, 44f), 12f);
        FindOrCreateActionButtonWithIconPlaceholder(frontDeskPageActionPanel, "Button_StartOperating", "Start", StartDemoOperatingPeriod);
        FindOrCreateActionButtonWithIconPlaceholder(frontDeskPageActionPanel, "Button_ActivateDemand", "Call Guest", ActivateUpcomingDemandNow);
        FindOrCreateActionButtonWithIconPlaceholder(frontDeskPageActionPanel, "Button_FrontDeskWait", "Wait", RecordWaitAction);

        frontDeskGuestDetailPopup.gameObject.SetActive(false);
        frontDeskReadyRoomListPopup.gameObject.SetActive(false);
        HideLegacyFrontDeskCards();
    }

    private void HideLegacyFrontDeskCards()
    {
        if (frontDeskViewPanel == null)
        {
            return;
        }

        // 老版本的 raw debug 卡片/按钮如果还在场景里，保留对象但隐藏，避免和新 Front Desk 页面重叠。
        HideUnexpectedChildren(
            frontDeskViewPanel,
            "Text_FrontDeskViewShell",
            "Card_FrontDeskHeader",
            "Panel_WaitingGuests",
            "Card_FrontDeskCurrentRequest",
            "Card_FrontDeskQuickActions",
            "Popup_GuestDetail",
            "Popup_ReadyRoomList");

        HideUnexpectedChildren(frontDeskStatusPanel, "Text_FrontDeskHeader", "IconPlaceholder_QueuePressure");
        HideUnexpectedChildren(frontDeskWaitingGuestPanel, "Text_WaitingGuestHeader", "Scroll_WaitingGuestStrip");
        HideUnexpectedChildren(frontDeskResultPanel, "Text_FrontDeskCurrentRequest", "PortraitPlaceholder_CurrentDeskGuest", "BadgePlaceholder_CurrentDeskStatus");
        HideUnexpectedChildren(frontDeskPageActionPanel, "Button_StartOperating", "Button_ActivateDemand", "Button_FrontDeskWait");
        HideUnexpectedChildren(
            frontDeskGuestDetailPopup,
            "Text_GuestDetail",
            "PortraitPlaceholder_CurrentGuest",
            "BadgePlaceholder_Warning",
            "Button_OpenReadyRoomList",
            "Button_CloseGuestPopup");
        HideUnexpectedChildren(frontDeskReadyRoomListPopup, "Text_ReadyRoomListTitle", "Scroll_ReadyRoomList", "Button_CloseReadyRoomList");
    }

    private void HideUnexpectedChildren(RectTransform parent, params string[] namesToKeep)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null || ShouldKeepChild(child.name, namesToKeep))
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    private bool ShouldKeepChild(string childName, string[] namesToKeep)
    {
        if (namesToKeep == null)
        {
            return false;
        }

        for (int i = 0; i < namesToKeep.Length; i++)
        {
            if (childName == namesToKeep[i])
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshFrontDeskGuestCards()
    {
        bool hasActive = demandLoop != null && demandLoop.activeDemandWaitingForManualAssignment;
        bool hasComplaint = demandLoop != null && demandLoop.complaintWaitingForReassignment;
        bool hasUpcoming = demandLoop != null;

        SetButtonVisible(frontDeskGuestActiveButton, hasActive);
        SetButtonVisible(frontDeskGuestComplaintButton, hasComplaint);
        SetButtonVisible(frontDeskGuestUpcomingButton, hasUpcoming && !hasActive && !hasComplaint);

        SetButtonLabel(frontDeskGuestActiveButton, BuildGuestCardText(1));
        SetButtonLabel(frontDeskGuestComplaintButton, BuildGuestCardText(2));
        SetButtonLabel(frontDeskGuestUpcomingButton, BuildGuestCardText(3));
        UpdateGuestCardVisual(frontDeskGuestActiveButton, "IN", new Color(0.16f, 0.22f, 0.32f, 1f), new Color(0.72f, 0.78f, 0.88f, 1f));
        UpdateGuestCardVisual(frontDeskGuestComplaintButton, "FIX", new Color(0.32f, 0.17f, 0.16f, 1f), new Color(0.92f, 0.48f, 0.42f, 1f));
        UpdateGuestCardVisual(frontDeskGuestUpcomingButton, "ETA", new Color(0.14f, 0.18f, 0.22f, 1f), new Color(0.54f, 0.62f, 0.70f, 1f));

        if (frontDeskWaitingGuestContent != null)
        {
            frontDeskWaitingGuestContent.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(frontDeskWaitingGuestContent);
        }

        if (frontDeskDemandText != null)
        {
            int waitingCount = GetFrontDeskWaitingGuestCount();
            string requestLine = demandLoop != null
                ? demandLoop.GetShowcaseCurrentGuestHeadline()
                : "No demand loop linked.";
            string instruction = waitingCount > 0
                ? "Tap a guest card, then open room list."
                : "Use Call Guest when rooms are ready.";
            frontDeskDemandText.text = "<b>Waiting Guests</b>  " + waitingCount
                + "\n" + requestLine
                + "\n" + instruction;
        }

        int queueCount = GetFrontDeskWaitingGuestCount();
        if (frontDeskQueueIconPlaceholder != null)
        {
            frontDeskQueueIconPlaceholder.gameObject.SetActive(true);
        }
        UpdatePlaceholderLabel(frontDeskQueueIconPlaceholder, "WAIT\n" + queueCount);
        SetPlaceholderColor(frontDeskQueueIconPlaceholder, queueCount > 0
            ? new Color(0.55f, 0.30f, 0.18f, 0.95f)
            : new Color(0.28f, 0.44f, 0.34f, 0.95f));

        if (frontDeskGuestPopupVisible && !IsSelectedGuestStillValid())
        {
            CloseFrontDeskPopups();
        }

        // 没有手动选中时，优先把最重要的客卡当成当前办理对象，减少空页面感。
        if (!frontDeskGuestPopupVisible && !frontDeskReadyRoomPopupVisible)
        {
            if (hasComplaint)
            {
                selectedFrontDeskGuestKind = 2;
            }
            else if (hasActive)
            {
                selectedFrontDeskGuestKind = 1;
            }
            else if (hasUpcoming)
            {
                selectedFrontDeskGuestKind = 3;
            }
        }
    }

    private void UpdateGuestCardVisual(Button button, string portraitLabel, Color cardColor, Color portraitColor)
    {
        if (button == null)
        {
            return;
        }

        Image cardImage = button.GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = cardColor;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            bool selected = IsFrontDeskGuestButtonSelected(button);
            outline.effectColor = selected
                ? new Color(0.96f, 0.90f, 0.56f, 0.96f)
                : new Color(0.65f, 0.78f, 0.88f, 0.95f);
            outline.effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
        }

        Transform portraitTransform = button.transform.Find("PortraitPlaceholder_GuestCard");
        RectTransform portrait = portraitTransform as RectTransform;
        UpdatePlaceholderLabel(portrait, portraitLabel);
        SetPlaceholderColor(portrait, portraitColor);
    }

    private bool IsFrontDeskGuestButtonSelected(Button button)
    {
        if (button == null)
        {
            return false;
        }

        return (button == frontDeskGuestActiveButton && selectedFrontDeskGuestKind == 1)
            || (button == frontDeskGuestComplaintButton && selectedFrontDeskGuestKind == 2)
            || (button == frontDeskGuestUpcomingButton && selectedFrontDeskGuestKind == 3);
    }

    private void UpdateReadyRoomCardVisual(Button button, Room2DEntity room)
    {
        if (button == null)
        {
            return;
        }

        Image cardImage = button.GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = new Color(0.12f, 0.18f, 0.24f, 1f);
        }

        Transform iconTransform = button.transform.Find("IconPlaceholder_RoomCard");
        RectTransform icon = iconTransform as RectTransform;
        UpdatePlaceholderLabel(icon, "");
        SetPlaceholderColor(icon, GetReadyRoomTypeColor(room));
    }

    private void RefreshFrontDeskPopups()
    {
        if (frontDeskGuestDetailPopup != null)
        {
            frontDeskGuestDetailPopup.gameObject.SetActive(frontDeskGuestPopupVisible && currentView == ShowcaseView.FrontDesk);
            if (frontDeskGuestPopupVisible && currentView == ShowcaseView.FrontDesk)
            {
                frontDeskGuestDetailPopup.SetAsLastSibling();
            }
        }

        if (frontDeskReadyRoomListPopup != null)
        {
            frontDeskReadyRoomListPopup.gameObject.SetActive(frontDeskReadyRoomPopupVisible && currentView == ShowcaseView.FrontDesk);
            if (frontDeskReadyRoomPopupVisible && currentView == ShowcaseView.FrontDesk)
            {
                frontDeskReadyRoomListPopup.SetAsLastSibling();
            }
        }

        if (frontDeskGuestDetailText != null)
        {
            frontDeskGuestDetailText.text = BuildSelectedGuestDetailText();
        }

        if (frontDeskReadyRoomListText != null)
        {
            frontDeskReadyRoomListText.text = "<b>Ready Rooms</b>\nChoose a room and press Check In.";
        }

        RefreshReadyRoomCards();
        UpdatePlaceholderLabel(frontDeskGuestPortraitPlaceholder, GetSelectedGuestPortraitLabel());
        UpdatePlaceholderLabel(frontDeskWarningBadgePlaceholder, GetSelectedGuestWarningLabel());
        SetPlaceholderColor(frontDeskGuestPortraitPlaceholder, GetSelectedGuestPortraitColor());
        SetPlaceholderColor(frontDeskWarningBadgePlaceholder, GetSelectedGuestWarningColor());
        SetButtonLabel(frontDeskAssignRoomButton, selectedFrontDeskGuestKind == 3 ? "Call + Assign" : "Assign Room");

        RectTransform currentDeskGuestPortrait = frontDeskResultPanel != null
            ? frontDeskResultPanel.Find("PortraitPlaceholder_CurrentDeskGuest") as RectTransform
            : null;
        RectTransform currentDeskGuestBadge = frontDeskResultPanel != null
            ? frontDeskResultPanel.Find("BadgePlaceholder_CurrentDeskStatus") as RectTransform
            : null;
        UpdatePlaceholderLabel(currentDeskGuestPortrait, GetSelectedGuestPortraitLabel());
        UpdatePlaceholderLabel(currentDeskGuestBadge, GetSelectedGuestWarningLabel());
        SetPlaceholderColor(currentDeskGuestPortrait, GetSelectedGuestPortraitColor());
        SetPlaceholderColor(currentDeskGuestBadge, GetSelectedGuestWarningColor());
    }

    private void SelectActiveDemandGuest()
    {
        selectedFrontDeskGuestKind = 1;
        frontDeskGuestPopupVisible = true;
        frontDeskReadyRoomPopupVisible = false;
        frontDeskGuestActionHint = "Inspect active guest.";
    }

    private void SelectComplaintGuest()
    {
        selectedFrontDeskGuestKind = 2;
        frontDeskGuestPopupVisible = true;
        frontDeskReadyRoomPopupVisible = false;
        frontDeskGuestActionHint = "Complaint guest needs reassignment.";
    }

    private void SelectUpcomingGuest()
    {
        selectedFrontDeskGuestKind = 3;
        frontDeskGuestPopupVisible = true;
        frontDeskReadyRoomPopupVisible = false;
        frontDeskGuestActionHint = "Upcoming guest can be called early.";
    }

    private void OpenReadyRoomListPopup()
    {
        FindReferencesIfNeeded();

        if (demandLoop == null)
        {
            frontDeskGuestActionHint = "No demand loop linked.";
            return;
        }

        // Upcoming 卡片不是正式等待客人；点击 Assign 时先把它转成 active demand。
        if (selectedFrontDeskGuestKind == 3 && !demandLoop.activeDemandWaitingForManualAssignment)
        {
            demandLoop.ActivateUpcomingDemandNow();
            selectedFrontDeskGuestKind = 1;
        }

        if (!demandLoop.activeDemandWaitingForManualAssignment && !demandLoop.complaintWaitingForReassignment)
        {
            frontDeskGuestActionHint = "No waiting guest to assign.";
            return;
        }

        frontDeskGuestPopupVisible = false;
        frontDeskReadyRoomPopupVisible = true;
        frontDeskGuestActionHint = "Choose a Ready room.";
        ResetReadyRoomScrollPosition();
    }

    private void CloseFrontDeskPopups()
    {
        frontDeskGuestPopupVisible = false;
        frontDeskReadyRoomPopupVisible = false;
    }

    private void CloseReadyRoomListPopup()
    {
        frontDeskReadyRoomPopupVisible = false;
        frontDeskGuestPopupVisible = true;
    }

    private void ResetReadyRoomScrollPosition()
    {
        if (frontDeskReadyRoomListContent != null)
        {
            frontDeskReadyRoomListContent.anchoredPosition = Vector2.zero;
        }
    }

    private void CheckInReadyRoomByVisibleIndex(int visibleIndex)
    {
        FindReferencesIfNeeded();

        Room2DEntity room = GetReadyRoomByVisibleIndex(visibleIndex);
        if (room == null || demandLoop == null)
        {
            frontDeskGuestActionHint = "Check-in failed: no Ready room.";
            return;
        }

        bool assigned = demandLoop.AssignRoomToActiveDemand(room);
        frontDeskGuestActionHint = demandLoop.lastManualAssignmentResult;

        if (assigned)
        {
            CloseFrontDeskPopups();
        }
    }

    private void RefreshReadyRoomCards()
    {
        if (frontDeskReadyRoomButtons == null)
        {
            return;
        }

        int visibleCount = 0;
        for (int i = 0; i < frontDeskReadyRoomButtons.Length; i++)
        {
            Button button = frontDeskReadyRoomButtons[i];
            if (button == null)
            {
                continue;
            }

            Room2DEntity room = GetReadyRoomByVisibleIndex(i);
            button.gameObject.SetActive(room != null);
            if (room != null)
            {
                PositionReadyRoomCard(button, visibleCount);
                SetButtonLabel(button, BuildReadyRoomCardText(room));
                UpdateReadyRoomCardVisual(button, room);
                visibleCount++;
            }
        }

        if (frontDeskReadyRoomListContent != null)
        {
            UpdateReadyRoomScrollContentSize(visibleCount);
            LayoutRebuilder.ForceRebuildLayoutImmediate(frontDeskReadyRoomListContent);
        }
    }

    private void PositionReadyRoomCard(Button button, int visibleIndex)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        const float cardWidth = 660f;
        const float cardHeight = 96f;
        const float spacing = 12f;

        // Ready 房列表先使用固定位置，避免 ScrollRect/Mask 在原型阶段把卡片裁掉但仍然能点击。
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(cardWidth, cardHeight);
        rect.anchoredPosition = new Vector2(0f, -visibleIndex * (cardHeight + spacing));
        rect.localScale = Vector3.one;
    }

    private void UpdateReadyRoomScrollContentSize(int visibleCount)
    {
        if (frontDeskReadyRoomListContent == null)
        {
            return;
        }

        const float cardHeight = 96f;
        const float spacing = 12f;
        float contentHeight = Mathf.Max(420f, visibleCount * (cardHeight + spacing) + spacing);

        frontDeskReadyRoomListContent.anchorMin = new Vector2(0f, 1f);
        frontDeskReadyRoomListContent.anchorMax = new Vector2(0f, 1f);
        frontDeskReadyRoomListContent.pivot = new Vector2(0f, 1f);
        frontDeskReadyRoomListContent.sizeDelta = new Vector2(700f, contentHeight);
    }

    private string BuildGuestCardText(int guestKind)
    {
        if (demandLoop == null)
        {
            return "No Guest\nNo demand";
        }

        if (guestKind == 1)
        {
            return "<b>Check-In</b>\n"
                + demandLoop.activeDemandType + " guest\n"
                + BuildDemandPreferenceShortLine(
                    demandLoop.activeDemandRoomPreference,
                    demandLoop.activeDemandFloorPreference,
                    demandLoop.activeDemandFacingPreference)
                + "\nWait " + FormatSeconds(demandLoop.activeDemandWaitSeconds);
        }

        if (guestKind == 2)
        {
            return "<b>Complaint</b>\n"
                + demandLoop.complaintDemandType + " guest\n"
                + BuildDemandPreferenceShortLine(
                    demandLoop.complaintRoomPreference,
                    demandLoop.complaintFloorPreference,
                    demandLoop.complaintFacingPreference)
                + "\nPatience " + FormatSeconds(demandLoop.complaintPatienceRemainingSeconds);
        }

        if (guestKind == 3)
        {
            return "<b>Next Guest</b>\n"
                + demandLoop.upcomingDemandType + " guest\n"
                + BuildDemandPreferenceShortLine(
                    demandLoop.upcomingDemandRoomPreference,
                    demandLoop.upcomingDemandFloorPreference,
                    demandLoop.upcomingDemandFacingPreference)
                + "\nETA " + FormatSeconds(demandLoop.upcomingDemandEtaSeconds);
        }

        return "Guest\nNone";
    }

    private string BuildSelectedGuestDetailText()
    {
        if (demandLoop == null)
        {
            return "<b>Guest Request</b>\nNo demand loop linked.";
        }

        if (selectedFrontDeskGuestKind == 1)
        {
            return "<b>Current Guest</b>\n"
                + demandLoop.activeDemandType + " check-in guest\n"
                + "Request: " + BuildDemandPreferenceShortLine(
                    demandLoop.activeDemandRoomPreference,
                    demandLoop.activeDemandFloorPreference,
                    demandLoop.activeDemandFacingPreference) + "\n"
                + "Waiting: " + FormatSeconds(demandLoop.activeDemandWaitSeconds) + "\n"
                + "Ready Rooms: " + GetReadyRoomCount() + "\n"
                + "Action: " + GetFrontDeskSuggestionText();
        }

        if (selectedFrontDeskGuestKind == 2)
        {
            return "<b>Complaint Reassign</b>\n"
                + demandLoop.complaintDemandType + " guest\n"
                + "Request: " + BuildDemandPreferenceShortLine(
                    demandLoop.complaintRoomPreference,
                    demandLoop.complaintFloorPreference,
                    demandLoop.complaintFacingPreference) + "\n"
                + "Waiting: " + FormatSeconds(demandLoop.complaintReassignmentWaitSeconds) + "\n"
                + "Patience: " + FormatSeconds(demandLoop.complaintPatienceRemainingSeconds) + "\n"
                + "Action: Reassign this guest before patience runs out.";
        }

        if (selectedFrontDeskGuestKind == 3)
        {
            return "<b>Next Guest</b>\n"
                + demandLoop.upcomingDemandType + " incoming\n"
                + "Request: " + BuildDemandPreferenceShortLine(
                    demandLoop.upcomingDemandRoomPreference,
                    demandLoop.upcomingDemandFloorPreference,
                    demandLoop.upcomingDemandFacingPreference) + "\n"
                + "Arrival: " + FormatSeconds(demandLoop.upcomingDemandEtaSeconds) + "\n"
                + "Ready Rooms: " + GetReadyRoomCount() + "\n"
                + "Action: Call guest first, then assign.";
        }

        return "<b>Guest Request</b>\nSelect a guest card.";
    }

    private string BuildReadyRoomCardText(Room2DEntity room)
    {
        if (room == null)
        {
            return "No room";
        }

        string fitText = demandLoop != null
            ? demandLoop.GetShowcaseRoomFitText(room)
            : room.GetPrototypeRoomTypeDisplayName();

        return "<b>" + room.roomName + "</b>  |  Ready\n"
            + room.GetPrototypeRoomTypeDisplayName() + "  |  " + room.GetPrototypeFacingDisplayName() + "\n"
            + "Fit: " + fitText + "\n"
            + GetReadyRoomRecommendationLabel(room);
    }

    private string BuildPreferenceLine(
        Room2DPrototypeDemandLoop.Room2DFloorPreference floorPreference,
        Room2DPrototypeDemandLoop.Room2DFacingPreference facingPreference)
    {
        return floorPreference + " / " + facingPreference;
    }

    private string BuildDemandPreferenceShortLine(
        Room2DPrototypeDemandLoop.Room2DRoomPreference roomPreference,
        Room2DPrototypeDemandLoop.Room2DFloorPreference floorPreference,
        Room2DPrototypeDemandLoop.Room2DFacingPreference facingPreference)
    {
        return roomPreference + "  |  " + floorPreference + "  |  " + facingPreference;
    }

    private int GetFrontDeskWaitingGuestCount()
    {
        if (demandLoop == null)
        {
            return 0;
        }

        int count = 0;
        if (demandLoop.activeDemandWaitingForManualAssignment)
        {
            count++;
        }

        if (demandLoop.complaintWaitingForReassignment)
        {
            count++;
        }

        return count;
    }

    private bool IsSelectedGuestStillValid()
    {
        if (demandLoop == null)
        {
            return false;
        }

        if (selectedFrontDeskGuestKind == 1)
        {
            return demandLoop.activeDemandWaitingForManualAssignment;
        }

        if (selectedFrontDeskGuestKind == 2)
        {
            return demandLoop.complaintWaitingForReassignment;
        }

        if (selectedFrontDeskGuestKind == 3)
        {
            return false;
        }

        return false;
    }

    private string GetSelectedGuestPortraitLabel()
    {
        if (selectedFrontDeskGuestKind == 2)
        {
            return "!";
        }

        if (selectedFrontDeskGuestKind == 3)
        {
            return "N";
        }

        return "G";
    }

    private string GetSelectedGuestWarningLabel()
    {
        if (selectedFrontDeskGuestKind == 2)
        {
            return "COMPLAINT";
        }

        if (selectedFrontDeskGuestKind == 1 && demandLoop != null && demandLoop.activeDemandWaitSeconds > demandLoop.manualAssignmentFallbackDelaySeconds)
        {
            return "LATE";
        }

        return "OK";
    }

    private Color GetSelectedGuestPortraitColor()
    {
        if (selectedFrontDeskGuestKind == 2)
        {
            return new Color(0.92f, 0.48f, 0.42f, 1f);
        }

        if (selectedFrontDeskGuestKind == 3)
        {
            return new Color(0.54f, 0.62f, 0.70f, 1f);
        }

        return new Color(0.72f, 0.78f, 0.88f, 1f);
    }

    private Color GetSelectedGuestWarningColor()
    {
        if (selectedFrontDeskGuestKind == 2)
        {
            return new Color(0.50f, 0.16f, 0.13f, 1f);
        }

        if (selectedFrontDeskGuestKind == 1 && demandLoop != null && demandLoop.activeDemandWaitSeconds > demandLoop.manualAssignmentFallbackDelaySeconds)
        {
            return new Color(0.55f, 0.40f, 0.12f, 1f);
        }

        return new Color(0.16f, 0.28f, 0.20f, 1f);
    }

    private Color GetReadyRoomTypeColor(Room2DEntity room)
    {
        if (room == null)
        {
            return new Color(0.48f, 0.56f, 0.64f, 1f);
        }

        // 当前原型只有少量房型，用颜色帮助快速区分。后续可以替换为真实图标。
        string roomType = room.prototypeRoomType.ToString();
        if (roomType.Contains("Better"))
        {
            return new Color(0.84f, 0.68f, 0.34f, 1f);
        }

        return new Color(0.42f, 0.62f, 0.78f, 1f);
    }

    private int GetReadyRoomCount()
    {
        int count = 0;
        Room2DEntity[] rooms = GetRoomsForShowcase();
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] != null && rooms[i].CanSimulateCheckIn())
            {
                count++;
            }
        }

        return count;
    }

    private Room2DEntity GetReadyRoomByVisibleIndex(int visibleIndex)
    {
        int currentIndex = 0;
        Room2DEntity[] rooms = GetRoomsForShowcase();
        SortRoomsByNumber(rooms);

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null || !room.CanSimulateCheckIn())
            {
                continue;
            }

            if (currentIndex == visibleIndex)
            {
                return room;
            }

            currentIndex++;
        }

        return null;
    }

    private Room2DEntity[] GetRoomsForShowcase()
    {
        if (roomOverview != null && roomOverview.rooms != null && roomOverview.rooms.Length > 0)
        {
            return roomOverview.rooms;
        }

        return FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
    }

    private void SortRoomsByNumber(Room2DEntity[] rooms)
    {
        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Length - 1; i++)
        {
            for (int j = i + 1; j < rooms.Length; j++)
            {
                int left = rooms[i] != null ? rooms[i].roomNumber : int.MaxValue;
                int right = rooms[j] != null ? rooms[j].roomNumber : int.MaxValue;
                if (right < left)
                {
                    Room2DEntity temp = rooms[i];
                    rooms[i] = rooms[j];
                    rooms[j] = temp;
                }
            }
        }
    }

    private void BuildRoomViewContent()
    {
        roomDemandPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomTopSummary", new Color(0.05f, 0.08f, 0.11f, 0.90f));
        roomSelectedPanel = FindOrCreatePanel(roomViewPanel, "Card_SelectedRoom", new Color(0.06f, 0.06f, 0.10f, 0.92f));
        roomWorkersPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomWorkers", new Color(0.05f, 0.08f, 0.09f, 0.92f));
        roomActionsPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomActions", new Color(0.03f, 0.04f, 0.06f, 0.0f));
        roomRightLauncherPanel = FindOrCreatePanel(roomViewPanel, "Panel_RoomRightLaunchers", new Color(0.04f, 0.05f, 0.07f, 0.82f));
        roomWaitingGuestPeekPanel = FindOrCreatePanel(roomViewPanel, "Panel_RoomWaitingGuestsPeek", new Color(0.08f, 0.06f, 0.13f, 0.96f));
        roomHousekeeperPanel = FindOrCreatePanel(roomViewPanel, "Panel_HousekeeperCards", new Color(0.06f, 0.09f, 0.08f, 0.96f));
        roomInspectorPanel = FindOrCreatePanel(roomViewPanel, "Panel_InspectorCard", new Color(0.08f, 0.08f, 0.05f, 0.96f));
        roomActionPopupPanel = FindOrCreatePanel(roomViewPanel, "Popup_RoomActions", new Color(0.05f, 0.06f, 0.10f, 0.98f));
        roomActionPopupButtonPanel = FindOrCreatePanel(roomActionPopupPanel, "Panel_RoomActionPopupButtons", new Color(0f, 0f, 0f, 0f));
        roomInfoPopupPanel = FindOrCreatePanel(roomViewPanel, "Popup_RoomInfo", new Color(0.04f, 0.05f, 0.08f, 0.98f));
        roomTileInfoButtonPanel = FindOrCreatePanel(roomViewPanel, "Panel_RoomTileInfoButtons", new Color(0f, 0f, 0f, 0f));
        roomWorkerPopupPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomWorkerPopup", new Color(0.02f, 0.03f, 0.05f, 0.98f));
        roomWorkerPopupButtonPanel = FindOrCreatePanel(roomWorkerPopupPanel, "Panel_RoomWorkerPopupButtons", new Color(0f, 0f, 0f, 0f));

        // Rooms View：顶部只保留薄信息条，避免旧 Debug 卡片挡住房间点击。
        ApplyAnchoredPanel(roomDemandPanel, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.985f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomRightLauncherPanel, new Vector2(0.60f, 0.80f), new Vector2(0.96f, 0.91f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomSelectedPanel, new Vector2(0.04f, 0.80f), new Vector2(0.58f, 0.91f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomWorkersPanel, new Vector2(0.60f, 0.80f), new Vector2(0.96f, 0.91f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomWaitingGuestPeekPanel, new Vector2(0.10f, 0.38f), new Vector2(0.90f, 0.70f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomHousekeeperPanel, new Vector2(0.10f, 0.40f), new Vector2(0.90f, 0.68f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomInspectorPanel, new Vector2(0.10f, 0.42f), new Vector2(0.90f, 0.64f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomActionPopupPanel, new Vector2(0.12f, 0.31f), new Vector2(0.88f, 0.58f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomActionPopupButtonPanel, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.45f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomInfoPopupPanel, new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.70f), Vector2.zero, Vector2.zero);
        ApplyStretch(roomTileInfoButtonPanel);
        SetPanelRaycast(roomTileInfoButtonPanel, false);
        ApplyAnchoredPanel(roomWorkerPopupPanel, new Vector2(0.12f, 0.31f), new Vector2(0.88f, 0.58f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomWorkerPopupButtonPanel, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.45f), Vector2.zero, Vector2.zero);

        selectedRoomText = FindOrCreateText(roomSelectedPanel, "Text_SelectedRoomCard", "Selected Room");
        roomWorkersText = FindOrCreateText(roomWorkersPanel, "Text_WorkerCard", "Workers");
        roomDemandText = FindOrCreateText(roomDemandPanel, "Text_RoomDemandCard", "Demand");
        roomWaitingGuestPeekText = FindOrCreateText(roomWaitingGuestPeekPanel, "Text_RoomWaitingGuestsPeek", "Waiting Guests");
        roomHousekeeperPanelText = FindOrCreateText(roomHousekeeperPanel, "Text_HousekeeperPanel", "Housekeepers");
        roomInspectorPanelText = FindOrCreateText(roomInspectorPanel, "Text_InspectorPanel", "Inspector");
        roomActionPopupText = FindOrCreateText(roomActionPopupPanel, "Text_RoomActionPopup", "Room Actions");
        roomInfoPopupText = FindOrCreateText(roomInfoPopupPanel, "Text_RoomInfoPopup", "Room Info");
        roomWorkerPopupText = FindOrCreateText(roomWorkerPopupPanel, "Text_RoomWorkerPopup", "Assign Worker");

        ApplyCardText(selectedRoomText, 16f);
        ApplyCardText(roomWorkersText, 15f);
        ApplyCardText(roomDemandText, 15f);
        ApplyCardText(roomWaitingGuestPeekText, 15f);
        ApplyCardText(roomHousekeeperPanelText, 15f);
        ApplyCardText(roomInspectorPanelText, 15f);
        ApplyCardText(roomActionPopupText, 16f);
        ApplyCardText(roomInfoPopupText, 16f);
        ApplyCardText(roomWorkerPopupText, 15f);
        selectedRoomText.rectTransform.offsetMax = new Vector2(-14f, -10f);
        roomWorkersText.rectTransform.offsetMax = new Vector2(-14f, -10f);
        roomActionPopupText.rectTransform.offsetMax = new Vector2(-18f, -112f);
        roomInfoPopupText.rectTransform.offsetMax = new Vector2(-18f, -82f);
        roomWorkerPopupText.rectTransform.offsetMax = new Vector2(-18f, -112f);

        roomTypeIconPlaceholder = FindOrCreatePlaceholder(roomSelectedPanel, "IconPlaceholder_RoomType", "TYPE");
        ApplyAnchoredPanel(roomTypeIconPlaceholder, new Vector2(0.75f, 0.54f), new Vector2(0.94f, 0.86f), Vector2.zero, Vector2.zero);
        roomStateIconPlaceholder = FindOrCreatePlaceholder(roomSelectedPanel, "IconPlaceholder_State", "STATE");
        ApplyAnchoredPanel(roomStateIconPlaceholder, new Vector2(0.75f, 0.14f), new Vector2(0.94f, 0.46f), Vector2.zero, Vector2.zero);
        roomWorkerPortraitPlaceholder = FindOrCreatePlaceholder(roomWorkersPanel, "WorkerPortraitPlaceholder", "WORKER");
        ApplyAnchoredPanel(roomWorkerPortraitPlaceholder, new Vector2(0.78f, 0.18f), new Vector2(0.94f, 0.82f), Vector2.zero, Vector2.zero);
        ApplyPlaceholderStyle(roomTypeIconPlaceholder, new Color(0.30f, 0.43f, 0.58f, 0.96f), 14f);
        ApplyPlaceholderStyle(roomStateIconPlaceholder, new Color(0.30f, 0.42f, 0.34f, 0.96f), 13f);
        ApplyPlaceholderStyle(roomWorkerPortraitPlaceholder, new Color(0.28f, 0.32f, 0.40f, 0.96f), 12f);

        // 这些信息卡只负责显示，不应该挡住底下的房间点击。
        SetPanelRaycast(roomDemandPanel, false);
        SetPanelRaycast(roomSelectedPanel, false);
        SetPanelRaycast(roomWorkersPanel, false);

        roomWaitingGuestCard = FindOrCreatePanel(roomWaitingGuestPeekPanel, "Card_RoomWaitingGuestPreview", new Color(0.12f, 0.16f, 0.23f, 0.96f));
        ApplyAnchoredPanel(roomWaitingGuestCard, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.62f), Vector2.zero, Vector2.zero);
        roomWaitingGuestPortraitPlaceholder = FindOrCreatePlaceholder(roomWaitingGuestCard, "PortraitPlaceholder_RoomWaitingGuest", "G");
        ApplyAnchoredPanel(roomWaitingGuestPortraitPlaceholder, new Vector2(0.04f, 0.18f), new Vector2(0.18f, 0.82f), Vector2.zero, Vector2.zero);
        ApplyPlaceholderStyle(roomWaitingGuestPortraitPlaceholder, new Color(0.24f, 0.30f, 0.40f, 0.96f), 16f);
        roomWaitingGuestCardText = FindOrCreateText(roomWaitingGuestCard, "Text_RoomWaitingGuestCard", "Guest");
        ApplyCardText(roomWaitingGuestCardText, 15f);
        roomWaitingGuestCardText.rectTransform.offsetMin = new Vector2(104f, 12f);
        roomWaitingGuestCardText.rectTransform.offsetMax = new Vector2(-14f, -12f);

        roomActionsPanel.gameObject.SetActive(false);
        roomWorkersPanel.gameObject.SetActive(false);
        roomWaitingGuestPeekPanel.gameObject.SetActive(false);
        roomHousekeeperPanel.gameObject.SetActive(false);
        roomInspectorPanel.gameObject.SetActive(false);
        roomActionPopupPanel.gameObject.SetActive(false);
        roomInfoPopupPanel.gameObject.SetActive(false);
        roomWorkerPopupPanel.gameObject.SetActive(false);

        ApplyActionGrid(roomRightLauncherPanel, 3, new Vector2(100f, 34f), 8f);
        roomOpenWaitingGuestsButton = FindOrCreateActionButton(roomRightLauncherPanel, "Button_OpenRoomWaitingGuests", "Guests", ToggleRoomWaitingGuestPeek);
        roomOpenHousekeeperPanelButton = FindOrCreateActionButton(roomRightLauncherPanel, "Button_OpenHousekeepers", "HSK", ToggleRoomHousekeeperPanel);
        roomOpenInspectorPanelButton = FindOrCreateActionButton(roomRightLauncherPanel, "Button_OpenInspectors", "INSP", ToggleRoomInspectorPanel);

        roomCloseWaitingGuestsButton = FindOrCreateActionButton(roomWaitingGuestPeekPanel, "Button_CloseRoomWaitingGuests", "Close", CloseRoomWaitingGuestPeek);
        ApplyAnchoredPanel(roomCloseWaitingGuestsButton.transform as RectTransform, new Vector2(0.68f, 0.72f), new Vector2(0.94f, 0.90f), Vector2.zero, Vector2.zero);

        ApplyActionGrid(roomActionPopupButtonPanel, 2, new Vector2(190f, 38f), 8f);
        roomPopupAssignHousekeeperButton = FindOrCreateActionButton(roomActionPopupButtonPanel, "Button_RoomPopupAssignHSK", "Assign HSK", OpenHousekeeperPopup);
        roomPopupMarkCleanPriorityButton = FindOrCreateActionButton(roomActionPopupButtonPanel, "Button_RoomPopupCleanPrio", "Clean Prio", MarkDirtyPriority);
        roomPopupAssignInspectorButton = FindOrCreateActionButton(roomActionPopupButtonPanel, "Button_RoomPopupAssignINSP", "Assign INSP", OpenInspectorPopup);
        roomPopupMarkInspectionPriorityButton = FindOrCreateActionButton(roomActionPopupButtonPanel, "Button_RoomPopupInspPrio", "Insp Prio", MarkInspectionPriority);
        roomPopupReserveButton = FindOrCreateActionButton(roomActionPopupButtonPanel, "Button_RoomPopupReserve", "Reserve", ReserveSelectedRoom);
        roomPopupInfoButton = FindOrCreateActionButton(roomActionPopupButtonPanel, "Button_RoomPopupInfo", "Info", OpenRoomInfoPopup);
        roomPopupCloseButton = FindOrCreateActionButton(roomActionPopupButtonPanel, "Button_RoomPopupClose", "Close", CloseRoomActionPopup);

        roomInfoPopupCloseButton = FindOrCreateActionButton(roomInfoPopupPanel, "Button_CloseRoomInfoPopup", "Close", CloseRoomInfoPopup);
        ApplyAnchoredPanel(roomInfoPopupCloseButton.transform as RectTransform, new Vector2(0.58f, 0.05f), new Vector2(0.92f, 0.16f), Vector2.zero, Vector2.zero);

        ApplyActionGrid(roomWorkerPopupButtonPanel, 2, new Vector2(190f, 38f), 8f);
        roomSelectNextHousekeeperButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomSelectNextHSK", "Next HSK", SelectNextHousekeeper);
        roomSelectHousekeeperButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomSelectHSK", "Use HSK", SelectHousekeeper);
        roomSelectInspectorButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomSelectInsp", "Use Insp", SelectInspector);
        roomConfirmWorkerButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomConfirmWorker", "Confirm", ConfirmSelectedWorkerFromPopup);
        roomCancelWorkerPopupButton = FindOrCreateActionButton(roomWorkerPopupButtonPanel, "Button_RoomCancelWorkerPopup", "Cancel", CloseWorkerPopup);

        HideLegacyRoomActionButtons();
        HideLegacyRoomCards();
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

    private void HideLegacyRoomCards()
    {
        if (roomViewPanel == null)
        {
            return;
        }

        // 旧 Rooms 调试卡片不再作为主 UI，避免和新的点房间弹窗交互叠在一起。
        HideUnexpectedChildren(
            roomViewPanel,
            "Text_RoomViewShell",
            "Card_RoomTopSummary",
            "Panel_RoomRightLaunchers",
            "Card_SelectedRoom",
            "Card_RoomWorkers",
            "Card_RoomActions",
            "Panel_RoomWaitingGuestsPeek",
            "Panel_HousekeeperCards",
            "Panel_InspectorCard",
            "Popup_RoomActions",
            "Popup_RoomInfo",
            "Panel_RoomTileInfoButtons",
            "Card_RoomWorkerPopup");
    }

    private void BuildLoungeViewContent()
    {
        loungeStatusPanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeHeader", new Color(0.05f, 0.07f, 0.09f, 0.96f));
        loungeStockPanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeStock", new Color(0.06f, 0.08f, 0.10f, 0.96f));
        loungeMachinePanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeMachine", new Color(0.05f, 0.06f, 0.08f, 0.96f));
        loungeResultPanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeWarningResult", new Color(0.08f, 0.06f, 0.08f, 0.96f));
        loungeActionsPanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeActions", new Color(0.05f, 0.05f, 0.07f, 0.96f));

        ApplyAnchoredPanel(loungeStatusPanel, new Vector2(0.07f, 0.75f), new Vector2(0.93f, 0.91f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(loungeStockPanel, new Vector2(0.07f, 0.50f), new Vector2(0.93f, 0.72f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(loungeMachinePanel, new Vector2(0.07f, 0.31f), new Vector2(0.93f, 0.47f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(loungeResultPanel, new Vector2(0.07f, 0.18f), new Vector2(0.93f, 0.28f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(loungeActionsPanel, new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.15f), Vector2.zero, Vector2.zero);

        loungeStatusText = FindOrCreateText(loungeStatusPanel, "Text_LoungeStatus", "Lounge");
        loungeStockText = FindOrCreateText(loungeStockPanel, "Text_LoungeStock", "Stock");
        loungeMachineText = FindOrCreateText(loungeMachinePanel, "Text_LoungeMachine", "Washing");
        loungeResultText = FindOrCreateText(loungeResultPanel, "Text_LoungeResult", "Lounge Result");
        ApplyCardText(loungeStatusText, 20f);
        ApplyCardText(loungeStockText, 19f);
        ApplyCardText(loungeMachineText, 18f);
        ApplyCardText(loungeResultText, 17f);
        loungeStatusText.rectTransform.offsetMax = new Vector2(-120f, -16f);
        loungeStockText.rectTransform.offsetMax = new Vector2(-150f, -16f);
        loungeMachineText.rectTransform.offsetMax = new Vector2(-140f, -16f);
        loungeResultText.rectTransform.offsetMax = new Vector2(-110f, -16f);

        loungeStockIconPlaceholder = FindOrCreatePlaceholder(loungeStockPanel, "IconPlaceholder_LoungeStock", "STOCK");
        ApplyAnchoredPanel(loungeStockIconPlaceholder, new Vector2(0.72f, 0.54f), new Vector2(0.94f, 0.86f), Vector2.zero, Vector2.zero);
        loungeCupIconPlaceholder = FindOrCreatePlaceholder(loungeStockPanel, "IconPlaceholder_Cups", "CUPS");
        ApplyAnchoredPanel(loungeCupIconPlaceholder, new Vector2(0.72f, 0.14f), new Vector2(0.94f, 0.46f), Vector2.zero, Vector2.zero);
        loungeMachineIconPlaceholder = FindOrCreatePlaceholder(loungeMachinePanel, "IconPlaceholder_LoungeMachine", "WASH");
        ApplyAnchoredPanel(loungeMachineIconPlaceholder, new Vector2(0.74f, 0.18f), new Vector2(0.94f, 0.82f), Vector2.zero, Vector2.zero);
        loungeWarningBadgePlaceholder = FindOrCreatePlaceholder(loungeResultPanel, "BadgePlaceholder_LoungeWarning", "WARN");
        ApplyAnchoredPanel(loungeWarningBadgePlaceholder, new Vector2(0.78f, 0.18f), new Vector2(0.94f, 0.82f), Vector2.zero, Vector2.zero);
        ApplyPlaceholderStyle(loungeStockIconPlaceholder, new Color(0.28f, 0.42f, 0.54f, 0.96f), 12f);
        ApplyPlaceholderStyle(loungeCupIconPlaceholder, new Color(0.28f, 0.38f, 0.50f, 0.96f), 12f);
        ApplyPlaceholderStyle(loungeMachineIconPlaceholder, new Color(0.32f, 0.30f, 0.50f, 0.96f), 12f);
        ApplyPlaceholderStyle(loungeWarningBadgePlaceholder, new Color(0.52f, 0.24f, 0.22f, 0.96f), 12f);

        ApplyActionGrid(loungeActionsPanel, 2, new Vector2(220f, 44f), 10f);
        FindOrCreateActionButtonWithIconPlaceholder(loungeActionsPanel, "Button_ServeLoungeShowcase", "Serve", ServeLoungeNow);
        FindOrCreateActionButtonWithIconPlaceholder(loungeActionsPanel, "Button_WashCupsShowcase", "Wash", StartLoungeWash);
        FindOrCreateActionButtonWithIconPlaceholder(loungeActionsPanel, "Button_RestockLoungeShowcase", "Restock", RestockLounge);
        FindOrCreateActionButtonWithIconPlaceholder(loungeActionsPanel, "Button_StartDayLoungeShowcase", "Start", StartDemoOperatingPeriod);
        HideLegacyLoungeCards();
    }

    private void HideLegacyLoungeCards()
    {
        if (loungeViewPanel == null)
        {
            return;
        }

        HideUnexpectedChildren(
            loungeViewPanel,
            "Text_LoungeViewShell",
            "Card_LoungeHeader",
            "Card_LoungeStock",
            "Card_LoungeMachine",
            "Card_LoungeWarningResult",
            "Card_LoungeActions");

        // 旧 Lounge 卡片如果还在场景里，隐藏掉，避免新旧 UI 叠在一起。
        string[] legacyCardNames =
        {
            "Card_LoungeStatus",
            "Card_LoungeResult"
        };

        for (int i = 0; i < legacyCardNames.Length; i++)
        {
            Transform legacyCard = loungeViewPanel.Find(legacyCardNames[i]);
            if (legacyCard != null)
            {
                legacyCard.gameObject.SetActive(false);
            }
        }
    }

    private string BuildFrontDeskStatusText()
    {
        string phase = demoDayController != null ? demoDayController.GetShowcasePhaseLabel() : "None";
        string time = demoDayController != null ? FormatSeconds(demoDayController.operatingTimerSeconds) : "0s";
        string duration = demoDayController != null ? FormatSeconds(demoDayController.operatingDurationSeconds) : "0s";
        int queue = frontDesk != null ? frontDesk.currentQueueCount : 0;
        int readyRooms = GetReadyRoomCount();
        int delayed = frontDesk != null ? frontDesk.totalDelayedCheckIns : 0;
        string pressure = frontDesk != null ? frontDesk.GetShowcasePressureLabel() : GetFrontDeskPressureLabel(queue, delayed);
        string focus = demoDayController != null ? demoDayController.GetShowcaseFocusText() : "Link demo day controller.";

        return "<b>Front Desk</b>\n"
            + phase + "  |  " + time + " / " + duration + "\n"
            + "Queue " + queue
            + "  |  Ready " + readyRooms
            + "  |  Delay " + delayed + "\n"
            + "Pressure: " + pressure + "\n"
            + "Focus: " + focus;
    }

    private string BuildFrontDeskDemandText()
    {
        if (demandLoop == null)
        {
            return "<b>Current Desk Focus</b>\nNo demand loop linked.";
        }

        string guestHeadline = demandLoop.GetShowcaseCurrentGuestHeadline();
        string request = demandLoop.GetShowcaseCurrentGuestPreferenceLine();
        string roomReady = HasReadyRoomForFrontDesk() ? GetReadyRoomCount() + " ready rooms" : "No ready room";
        string suggestion = frontDesk != null ? frontDesk.GetShowcaseActionHint() : GetFrontDeskSuggestionText();

        return "<b>Current Desk Focus</b>\n"
            + guestHeadline + "\n"
            + "Request: " + request + "\n"
            + "Rooms: " + roomReady + "\n"
            + "Action: " + suggestion;
    }

    private string BuildFrontDeskResultText()
    {
        if (demandLoop == null)
        {
            return "<b>Desk Notes</b>\nNo demand loop";
        }

        return "<b>Desk Notes</b>\n"
            + "Upcoming: " + demandLoop.upcomingDemandType
            + " in " + FormatSeconds(demandLoop.upcomingDemandEtaSeconds) + "\n"
            + "Latest Room: " + demandLoop.lastChangedRoomName + "\n"
            + "Latest Result: " + demandLoop.lastOutcomeLabel + "\n"
            + "Served " + demandLoop.successfulDemandCount
            + "  |  Missed " + demandLoop.unmetDemandCount;
    }

    private string BuildSelectedRoomText()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        if (room == null)
        {
            return "<b>Selected Room</b>\nTap a room to inspect it.";
        }

        string reservedText = demandLoop != null && demandLoop.IsRoomReservedForPrototypeDemand(room) ? "Yes" : "No";
        string matchHint = demandLoop != null ? demandLoop.GetShowcaseRoomFitText(room) : "No demand";

        return "<b>Selected Room</b>\n"
            + "<b>" + room.roomName + "</b>  |  " + room.GetStateDisplayName() + "\n"
            + room.GetPrototypeRoomTypeDisplayName()
            + "  |  " + room.GetPrototypeFloorDisplayName()
            + "  |  " + room.GetPrototypeFacingDisplayName() + "\n"
            + "Wait: " + FormatSeconds(room.stateElapsedSeconds)
            + "  |  Reserved: " + reservedText + "\n"
            + "Priority: " + GetRoomPrioritySummary(room) + "\n"
            + "Best Action: " + GetRoomShowcaseActionHint(room) + "\n"
            + "Guest Fit: " + matchHint;
    }

    private string BuildCompactSelectedRoomText()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        if (room == null)
        {
            return "<b>Selected Room</b>\nTap a room.";
        }

        string reservedText = demandLoop != null && demandLoop.IsRoomReservedForPrototypeDemand(room) ? "Yes" : "No";
        string matchHint = demandLoop != null ? demandLoop.GetShowcaseRoomFitText(room) : "No demand";

        // 主 Rooms 页面只显示决策需要的摘要；完整信息放到房间弹窗里看。
        return "<b>Selected Room</b>\n"
            + room.roomName + "  |  " + room.GetStateDisplayName() + "\n"
            + room.GetPrototypeRoomTypeDisplayName() + "  |  F" + room.floorNumber + "  |  " + room.GetPrototypeFacingDisplayName() + "\n"
            + "Action: " + GetRoomShowcaseActionHint(room) + "\n"
            + "Reserved " + reservedText + "  |  " + GetRoomPrioritySummary(room) + "\n"
            + "Fit: " + matchHint;
    }

    private string BuildRoomWorkerText()
    {
        return "<b>Workers</b>\n"
            + "Selected: " + GetSelectedWorkerLine() + "\n"
            + "Target: " + GetSelectedRoomDisplayNameForHint() + "\n"
            + BuildWorkerRuntimeLine() + "\n"
            + "Next: " + roomActionHint;
    }

    private string BuildRoomDemandText()
    {
        if (demandLoop == null)
        {
            return "<b>Rooms</b>\n" + BuildCompactRoomStateLine() + "\nDemand: None";
        }

        return "<b>Rooms</b>\n"
            + BuildCompactRoomStateLine() + "\n"
            + "Focus: " + (demoDayController != null ? demoDayController.GetShowcaseFocusText() : "Link demo day controller.") + "\n"
            + "Guest: " + demandLoop.GetShowcaseCurrentGuestHeadline() + "\n"
            + "Reserved: " + demandLoop.reservedRoomName
            + "  |  Last: " + demandLoop.lastChangedRoomName
            + " / " + demandLoop.lastOutcomeLabel;
    }

    private void RefreshRoomsViewActionState()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        bool hasRoom = room != null;
        bool isDirty = hasRoom && room.currentState == Room2DState.Dirty;
        bool isAwaitingInspection = hasRoom && room.currentState == Room2DState.AwaitingInspection;
        bool isReady = hasRoom && room.currentState == Room2DState.Ready;

        if (roomActionsPanel != null)
        {
            roomActionsPanel.gameObject.SetActive(false);
        }

        if (roomWorkerPopupPanel != null)
        {
            roomWorkerPopupPanel.gameObject.SetActive(roomWorkerPopupVisible && currentView == ShowcaseView.Rooms);
        }

        if (roomWorkersPanel != null)
        {
            roomWorkersPanel.gameObject.SetActive(false);
        }

        if (roomWorkerPopupText != null)
        {
            roomWorkerPopupText.text = BuildWorkerPopupText();
        }

        if (roomWaitingGuestPeekPanel != null)
        {
            roomWaitingGuestPeekPanel.gameObject.SetActive(roomWaitingGuestPeekVisible && currentView == ShowcaseView.Rooms);
            if (roomWaitingGuestPeekPanel.gameObject.activeInHierarchy)
            {
                roomWaitingGuestPeekPanel.SetAsLastSibling();
            }
        }

        if (roomHousekeeperPanel != null)
        {
            roomHousekeeperPanel.gameObject.SetActive(roomHousekeeperPanelVisible && currentView == ShowcaseView.Rooms);
        }

        if (roomInspectorPanel != null)
        {
            roomInspectorPanel.gameObject.SetActive(roomInspectorPanelVisible && currentView == ShowcaseView.Rooms);
        }

        if (roomActionPopupPanel != null)
        {
            roomActionPopupPanel.gameObject.SetActive(roomActionPopupVisible && currentView == ShowcaseView.Rooms && hasRoom);
            if (roomActionPopupPanel.gameObject.activeInHierarchy)
            {
                roomActionPopupPanel.SetAsLastSibling();
            }
        }

        if (roomInfoPopupPanel != null)
        {
            roomInfoPopupPanel.gameObject.SetActive(roomInfoPopupVisible && currentView == ShowcaseView.Rooms && hasRoom);
            if (roomInfoPopupPanel.gameObject.activeInHierarchy)
            {
                roomInfoPopupPanel.SetAsLastSibling();
            }
        }

        if (roomWaitingGuestPeekText != null)
        {
            roomWaitingGuestPeekText.text = BuildRoomWaitingGuestPeekText();
        }

        if (roomWaitingGuestCardText != null)
        {
            roomWaitingGuestCardText.text = BuildRoomWaitingGuestCardText();
        }

        UpdatePlaceholderLabel(roomWaitingGuestPortraitPlaceholder, GetFrontDeskWaitingGuestCount() > 0 ? "G" : "-");
        SetPlaceholderColor(roomWaitingGuestPortraitPlaceholder, GetFrontDeskWaitingGuestCount() > 0
            ? new Color(0.30f, 0.42f, 0.56f, 0.96f)
            : new Color(0.20f, 0.24f, 0.30f, 0.96f));

        if (roomHousekeeperPanelText != null)
        {
            roomHousekeeperPanelText.text = BuildHousekeeperPanelText();
        }

        if (roomInspectorPanelText != null)
        {
            roomInspectorPanelText.text = BuildInspectorPanelText();
        }

        if (roomActionPopupText != null)
        {
            roomActionPopupText.text = BuildRoomActionPopupText(room);
        }

        if (roomInfoPopupText != null)
        {
            roomInfoPopupText.text = BuildSelectedRoomText();
        }

        UpdatePlaceholderLabel(roomTypeIconPlaceholder, hasRoom ? GetRoomTypeBadgeLabel(room) : "TYPE");
        UpdatePlaceholderLabel(roomStateIconPlaceholder, hasRoom ? GetRoomStateBadgeLabel(room) : "STATE");
        UpdatePlaceholderLabel(roomWorkerPortraitPlaceholder, GetSelectedWorkerBadgeLabel());
        SetPlaceholderColor(roomTypeIconPlaceholder, hasRoom ? GetRoomTypeBadgeColor(room) : new Color(0.30f, 0.43f, 0.58f, 0.96f));
        SetPlaceholderColor(roomStateIconPlaceholder, hasRoom ? GetRoomStateBadgeColor(room) : new Color(0.30f, 0.42f, 0.34f, 0.96f));
        SetPlaceholderColor(roomWorkerPortraitPlaceholder, GetSelectedWorkerBadgeColor());

        RefreshRoomActionPopupButtonState(isDirty, isAwaitingInspection, isReady);
        // 旧版漂浮 i 按钮和房间不是同一个整体，容易在层级上压到面板；现在先全部隐藏。
        SetAllRoomTileInfoButtonsVisible(false);
        RefreshWorkerPopupButtonState();
    }

    private void RefreshRoomActionPopupButtonState(bool isDirty, bool isAwaitingInspection, bool isReady)
    {
        // 房间弹窗只显示当前状态真正能做的事，避免玩家把测试按钮当成正常流程。
        SetButtonVisible(roomPopupAssignHousekeeperButton, isDirty);
        SetButtonVisible(roomPopupMarkCleanPriorityButton, isDirty);
        SetButtonVisible(roomPopupAssignInspectorButton, isAwaitingInspection);
        SetButtonVisible(roomPopupMarkInspectionPriorityButton, isAwaitingInspection);
        SetButtonVisible(roomPopupReserveButton, isReady);
        SetButtonVisible(roomPopupInfoButton, isDirty || isAwaitingInspection || isReady);
        SetButtonVisible(roomPopupCloseButton, true);
    }

    private void RefreshRoomTileInfoButtons()
    {
        if (roomTileInfoButtonPanel == null || currentView != ShowcaseView.Rooms)
        {
            SetAllRoomTileInfoButtonsVisible(false);
            return;
        }

        if (roomTileInfoButtons == null || roomTileInfoButtons.Length == 0)
        {
            roomTileInfoButtons = new Button[24];
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            SetAllRoomTileInfoButtonsVisible(false);
            return;
        }

        Room2DController[] rooms = GetSelectableRooms();
        int buttonCount = Mathf.Min(rooms.Length, roomTileInfoButtons.Length);

        for (int i = 0; i < buttonCount; i++)
        {
            int roomIndex = i;
            Button button = roomTileInfoButtons[i];
            if (button == null)
            {
                button = FindOrCreateActionButton(roomTileInfoButtonPanel, "Button_RoomTileInfo_" + i, "i", () => OpenRoomInfoByVisibleIndex(roomIndex));
                roomTileInfoButtons[i] = button;
                RectTransform rect = button.transform as RectTransform;
                if (rect != null)
                {
                    rect.sizeDelta = new Vector2(34f, 34f);
                }
            }

            Room2DController room = rooms[i];
            Rect screenRect = new Rect();
            Vector2 screenCenter = Vector2.zero;
            bool visible = room != null
                && room.gameObject.activeInHierarchy
                && TryGetRoomScreenRect(room, mainCamera, out screenRect, out screenCenter);

            button.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            RectTransform buttonRect = button.transform as RectTransform;
            if (buttonRect == null)
            {
                continue;
            }

            Vector2 localPoint;
            Vector2 screenPoint = new Vector2(screenRect.xMax - 18f, screenRect.yMax - 18f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(roomTileInfoButtonPanel, screenPoint, null, out localPoint);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = localPoint;
            buttonRect.sizeDelta = new Vector2(34f, 34f);
            SetButtonLabel(button, "i");
        }

        for (int i = buttonCount; i < roomTileInfoButtons.Length; i++)
        {
            SetButtonVisible(roomTileInfoButtons[i], false);
        }
    }

    private void SetAllRoomTileInfoButtonsVisible(bool visible)
    {
        if (roomTileInfoButtons == null)
        {
            return;
        }

        for (int i = 0; i < roomTileInfoButtons.Length; i++)
        {
            SetButtonVisible(roomTileInfoButtons[i], visible);
        }
    }

    private void OpenRoomInfoByVisibleIndex(int roomIndex)
    {
        Room2DController[] rooms = GetSelectableRooms();
        if (roomIndex < 0 || roomIndex >= rooms.Length || rooms[roomIndex] == null)
        {
            return;
        }

        FindReferencesIfNeeded();
        if (selectionManager != null)
        {
            selectionManager.SelectRoom(rooms[roomIndex]);
        }

        roomInfoPopupVisible = true;
        roomActionPopupVisible = false;
        roomWorkerPopupVisible = false;
        roomActionHint = "Opened info for " + GetSelectedRoomDisplayNameForHint() + ".";
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

        return "<b>Assign Worker</b>\n"
            + "Room: " + roomName + "\n"
            + "Selected Worker: " + workerName + "\n"
            + "Confirm to assign. Cancel to close.\n"
            + "Last: " + lastResult;
    }

    private string BuildRoomWaitingGuestPeekText()
    {
        if (demandLoop == null)
        {
            return "<b>Waiting Guests</b>\nNo demand loop.";
        }

        return "<b>Waiting Guests</b>\n"
            + "Queue: " + GetFrontDeskWaitingGuestCount() + "\n"
            + demandLoop.GetShowcaseCurrentGuestHeadline() + "\n"
            + "Use this only to check demand, then return to rooms.";
    }

    private string BuildRoomWaitingGuestCardText()
    {
        if (demandLoop == null)
        {
            return "No demand loop.";
        }

        if (demandLoop.complaintWaitingForReassignment)
        {
            return "<b>Complaint</b>\n"
                + demandLoop.complaintDemandType + " guest\n"
                + BuildDemandPreferenceShortLine(
                    demandLoop.complaintRoomPreference,
                    demandLoop.complaintFloorPreference,
                    demandLoop.complaintFacingPreference) + "\n"
                + "Patience " + FormatSeconds(demandLoop.complaintPatienceRemainingSeconds);
        }

        if (demandLoop.activeDemandWaitingForManualAssignment)
        {
            return "<b>Waiting Guest</b>\n"
                + demandLoop.activeDemandType + " guest\n"
                + BuildDemandPreferenceShortLine(
                    demandLoop.activeDemandRoomPreference,
                    demandLoop.activeDemandFloorPreference,
                    demandLoop.activeDemandFacingPreference) + "\n"
                + "Wait " + FormatSeconds(demandLoop.activeDemandWaitSeconds);
        }

        return "<b>No Waiting Guest</b>\n"
            + "Next: " + demandLoop.upcomingDemandType + " guest\n"
            + "ETA " + FormatSeconds(demandLoop.upcomingDemandEtaSeconds);
    }

    private string BuildHousekeeperPanelText()
    {
        if (workerSelectionPanel == null || workerSelectionPanel.housekeepers == null || workerSelectionPanel.housekeepers.Length == 0)
        {
            return "<b>Housekeepers</b>\nNo HSK linked.";
        }

        string text = "<b>Housekeepers</b>\n";
        for (int i = 0; i < workerSelectionPanel.housekeepers.Length; i++)
        {
            Housekeeper2D housekeeper = workerSelectionPanel.housekeepers[i];
            if (housekeeper == null)
            {
                continue;
            }

            string selected = i == workerSelectionPanel.selectedHousekeeperIndex
                && workerSelectionPanel.selectedWorkerType == Room2DWorkerSelectionPanel.PrototypeWorkerType.Housekeeper
                ? "> "
                : "- ";
            text += selected + housekeeper.name
                + ": " + housekeeper.currentState
                + " / " + housekeeper.assignedRoomName
                + " / " + FormatSeconds(housekeeper.cleaningTimerSeconds) + "\n";
        }

        return text;
    }

    private string BuildInspectorPanelText()
    {
        if (workerSelectionPanel == null || workerSelectionPanel.inspector == null)
        {
            return "<b>Inspector</b>\nNo inspector linked.";
        }

        Inspector2D inspectorWorker = workerSelectionPanel.inspector;
        string selected = workerSelectionPanel.selectedWorkerType == Room2DWorkerSelectionPanel.PrototypeWorkerType.Inspector
            ? "> "
            : "- ";

        return "<b>Inspector</b>\n"
            + selected + inspectorWorker.name
            + ": " + inspectorWorker.currentState
            + " / " + inspectorWorker.assignedRoomName
            + " / " + FormatSeconds(inspectorWorker.inspectionTimerSeconds)
            + "\nConfirm when this room is awaiting inspection.";
    }

    private string BuildRoomActionPopupText(Room2DEntity room)
    {
        if (room == null)
        {
            return "<b>Room Action</b>\nTap a room.";
        }

        string actionHint;
        switch (room.currentState)
        {
            case Room2DState.Dirty:
                actionHint = "Assign HSK or mark Clean Priority.";
                break;
            case Room2DState.AwaitingInspection:
                actionHint = "Assign Inspector or mark Insp Priority.";
                break;
            case Room2DState.Ready:
                actionHint = "Reserve for upcoming guest. Check-in is handled at Front Desk.";
                break;
            case Room2DState.Occupied:
                actionHint = "Occupied room. No room operation now.";
                break;
            case Room2DState.Blocked:
                actionHint = "Blocked: " + room.blockReason;
                break;
            default:
                actionHint = "No manual action for this state.";
                break;
        }

        return "<b>" + room.roomName + "</b>\n"
            + "State: " + room.GetStateDisplayName() + "\n"
            + "Action: " + actionHint + "\n"
            + "Wait: " + FormatSeconds(room.stateElapsedSeconds) + "\n"
            + "Last: " + roomActionHint;
    }

    private void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        Transform labelTransform = button.transform.Find("Text (TMP)");
        TMP_Text labelText = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        if (labelText != null)
        {
            labelText.text = label;
        }
    }

    private string BuildLoungeStatusText()
    {
        string phase = demoDayController != null ? demoDayController.GetShowcasePhaseLabel() : "None";
        string time = demoDayController != null ? FormatSeconds(demoDayController.operatingTimerSeconds) : "0s";
        string duration = demoDayController != null ? FormatSeconds(demoDayController.operatingDurationSeconds) : "0s";
        string warning = lounge != null ? lounge.GetShowcaseStockSummary() : "None";
        string focus = lounge != null ? lounge.GetShowcaseActionHint() : "Link lounge.";

        return "<b>Lounge</b>\n"
            + phase + "  |  " + time + " / " + duration + "\n"
            + "Service: " + GetLoungeServiceStateText() + "\n"
            + "Status: " + warning + "\n"
            + "Action: " + focus;
    }

    private string BuildLoungeStockText()
    {
        if (lounge == null)
        {
            return "<b>Stock</b>\nNo lounge linked.";
        }

        return "<b>Stock</b>\n"
            + "Clean Cups: " + lounge.cleanCups + "\n"
            + "Dirty Cups: " + lounge.dirtyCups + "\n"
            + "Milk: " + lounge.milkStock + "\n"
            + "Tea / Coffee: " + lounge.teaCoffeeStock + "\n"
            + "Summary: " + lounge.GetShowcaseStockSummary();
    }

    private string BuildLoungeMachineText()
    {
        if (lounge == null)
        {
            return "<b>Washing Machine</b>\nNo lounge linked.";
        }

        string washingState = lounge.washing ? "Running" : "Idle";
        string progress = lounge.washing
            ? FormatSeconds(lounge.washTimerSeconds) + " / " + FormatSeconds(lounge.washDurationSeconds)
            : "Ready";

        return "<b>Washing Machine</b>\n"
            + "State: " + washingState + "\n"
            + "Cups: " + lounge.cupsInWashing + "\n"
            + "Progress: " + progress + "\n"
            + "Summary: " + lounge.GetShowcaseWashSummary();
    }

    private string BuildLoungeResultText()
    {
        if (lounge == null)
        {
            return "<b>Warnings</b>\nNone";
        }

        return "<b>Warnings</b>\n"
            + "Now: " + lounge.loungeWarning + "\n"
            + "Served " + lounge.servedDrinkCount
            + "  |  Missed " + lounge.missedServiceCount + "\n"
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

    private string BuildCompactRoomStateLine()
    {
        int dirty = 0;
        int cleaning = 0;
        int inspection = 0;
        int ready = 0;
        int occupied = 0;
        int blocked = 0;

        Room2DEntity[] rooms = roomOverview != null && roomOverview.rooms != null && roomOverview.rooms.Length > 0
            ? roomOverview.rooms
            : FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null)
            {
                continue;
            }

            CountRoomState(room.currentState, ref dirty, ref cleaning, ref inspection, ref ready, ref occupied, ref blocked);
        }

        return "D " + dirty
            + "  C " + cleaning
            + "  I " + inspection
            + "  R " + ready
            + "  O " + occupied
            + "  B " + blocked;
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

    private string GetSelectedWorkerLine()
    {
        if (workerSelectionPanel == null)
        {
            return "None";
        }

        return workerSelectionPanel.selectedWorkerName
            + " (" + workerSelectionPanel.selectedWorkerType + ")";
    }

    private string BuildWorkerRuntimeLine()
    {
        if (workerSelectionPanel == null)
        {
            return "State: None";
        }

        if (workerSelectionPanel.selectedWorkerType == Room2DWorkerSelectionPanel.PrototypeWorkerType.Housekeeper)
        {
            Housekeeper2D housekeeper = GetSelectedHousekeeperForDisplay();
            if (housekeeper == null)
            {
                return "HSK: None";
            }

            return "HSK: " + housekeeper.currentState
                + " / " + housekeeper.assignedRoomName
                + " / " + FormatSeconds(housekeeper.cleaningTimerSeconds);
        }

        if (workerSelectionPanel.selectedWorkerType == Room2DWorkerSelectionPanel.PrototypeWorkerType.Inspector)
        {
            Inspector2D inspectorWorker = workerSelectionPanel.inspector;
            if (inspectorWorker == null)
            {
                return "Inspector: None";
            }

            return "Inspector: " + inspectorWorker.currentState
                + " / " + inspectorWorker.assignedRoomName
                + " / " + FormatSeconds(inspectorWorker.inspectionTimerSeconds);
        }

        return "State: No worker selected";
    }

    private Housekeeper2D GetSelectedHousekeeperForDisplay()
    {
        if (workerSelectionPanel == null
            || workerSelectionPanel.housekeepers == null
            || workerSelectionPanel.housekeepers.Length == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(workerSelectionPanel.selectedHousekeeperIndex, 0, workerSelectionPanel.housekeepers.Length - 1);
        return workerSelectionPanel.housekeepers[index];
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

    private string GetLoungeServiceStateText()
    {
        if (lounge == null)
        {
            return "None";
        }

        if (lounge.washing)
        {
            return "Washing cups";
        }

        if (lounge.cleanCups <= lounge.lowCleanCupThreshold)
        {
            return "Needs cups";
        }

        if (lounge.milkStock <= lounge.lowStockThreshold || lounge.teaCoffeeStock <= lounge.lowStockThreshold)
        {
            return "Needs stock";
        }

        return lounge.runDuringPlay ? "Serving" : "Paused";
    }

    private void RefreshLoungePlaceholderLabels()
    {
        if (lounge == null)
        {
            UpdatePlaceholderLabel(loungeStockIconPlaceholder, "STOCK");
            UpdatePlaceholderLabel(loungeCupIconPlaceholder, "CUPS");
            UpdatePlaceholderLabel(loungeMachineIconPlaceholder, "WASH");
            UpdatePlaceholderLabel(loungeWarningBadgePlaceholder, "WARN");
            return;
        }

        UpdatePlaceholderLabel(loungeStockIconPlaceholder, lounge.HasStockRisk() ? "LOW\nSTOCK" : "STOCK\nOK");
        UpdatePlaceholderLabel(loungeCupIconPlaceholder, lounge.HasCupRisk() ? "LOW\nCUPS" : "CUPS\nOK");
        UpdatePlaceholderLabel(loungeMachineIconPlaceholder, lounge.washing ? "WASH\nRUN" : "WASH\nIDLE");
        UpdatePlaceholderLabel(loungeWarningBadgePlaceholder, lounge.loungeWarning == "None" ? "OK" : "FIX");

        SetPlaceholderColor(loungeStockIconPlaceholder, new Color(0.45f, 0.62f, 0.78f, 0.95f));
        SetPlaceholderColor(loungeCupIconPlaceholder, new Color(0.62f, 0.76f, 0.82f, 0.95f));
        SetPlaceholderColor(loungeMachineIconPlaceholder, lounge.washing
            ? new Color(0.85f, 0.62f, 0.30f, 0.95f)
            : new Color(0.30f, 0.48f, 0.42f, 0.95f));
        SetPlaceholderColor(loungeWarningBadgePlaceholder, lounge.loungeWarning == "None"
            ? new Color(0.25f, 0.44f, 0.30f, 0.95f)
            : new Color(0.55f, 0.24f, 0.20f, 0.95f));
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

    private string GetReadyRoomRecommendationLabel(Room2DEntity room)
    {
        if (room == null)
        {
            return "Tap to Check In";
        }

        if (demandLoop == null)
        {
            return "Tap to Check In";
        }

        string fitText = demandLoop.GetShowcaseRoomFitText(room);
        if (fitText.Contains("Good"))
        {
            return "Recommended";
        }

        if (fitText.Contains("Poor"))
        {
            return "Usable but risky";
        }

        return "Stable choice";
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

        string firstLine = lines[0];
        int arrowIndex = firstLine.IndexOf("->");
        if (arrowIndex >= 0 && arrowIndex + 2 < firstLine.Length)
        {
            return "Match: " + firstLine.Substring(arrowIndex + 2).Trim();
        }

        return firstLine.Replace("Match Hint: ", "Match: ");
    }

    private string GetRoomPrioritySummary(Room2DEntity room)
    {
        if (room == null)
        {
            return "None";
        }

        if (room.markedCleaningPriority)
        {
            return "Clean Prio";
        }

        if (room.markedInspectionPriority)
        {
            return "Insp Prio";
        }

        if (room.currentState == Room2DState.Dirty)
        {
            return room.cleaningPriorityLabel;
        }

        return "Normal";
    }

    private string GetRoomShowcaseActionHint(Room2DEntity room)
    {
        if (room == null)
        {
            return "Tap a room.";
        }

        switch (room.currentState)
        {
            case Room2DState.Dirty:
                return "Assign HSK";
            case Room2DState.Cleaning:
                return "Wait for cleaning";
            case Room2DState.AwaitingInspection:
                return "Assign Inspector";
            case Room2DState.Ready:
                return demandLoop != null && demandLoop.IsRoomReservedForPrototypeDemand(room)
                    ? "Hold for guest"
                    : "Reserve or use at Front Desk";
            case Room2DState.Occupied:
                return "Guest is staying";
            case Room2DState.Blocked:
                return "Blocked: " + room.blockReason;
            default:
                return "Check room";
        }
    }

    private string GetRoomTypeBadgeLabel(Room2DEntity room)
    {
        if (room == null)
        {
            return "TYPE";
        }

        return room.prototypeRoomType == Room2DPrototypeRoomType.Better ? "BT" : "STD";
    }

    private Color GetRoomTypeBadgeColor(Room2DEntity room)
    {
        if (room == null)
        {
            return new Color(0.30f, 0.43f, 0.58f, 0.96f);
        }

        return room.prototypeRoomType == Room2DPrototypeRoomType.Better
            ? new Color(0.70f, 0.56f, 0.26f, 0.96f)
            : new Color(0.30f, 0.43f, 0.58f, 0.96f);
    }

    private string GetRoomStateBadgeLabel(Room2DEntity room)
    {
        if (room == null)
        {
            return "STATE";
        }

        switch (room.currentState)
        {
            case Room2DState.Dirty:
                return "DIRTY";
            case Room2DState.Cleaning:
                return "HSK";
            case Room2DState.AwaitingInspection:
                return "INSP";
            case Room2DState.Ready:
                return "READY";
            case Room2DState.Occupied:
                return "LIVE";
            case Room2DState.Blocked:
                return "BLOCK";
            default:
                return "ROOM";
        }
    }

    private Color GetRoomStateBadgeColor(Room2DEntity room)
    {
        if (room == null)
        {
            return new Color(0.30f, 0.42f, 0.34f, 0.96f);
        }

        switch (room.currentState)
        {
            case Room2DState.Dirty:
                return new Color(0.52f, 0.29f, 0.21f, 0.96f);
            case Room2DState.Cleaning:
                return new Color(0.26f, 0.48f, 0.62f, 0.96f);
            case Room2DState.AwaitingInspection:
                return new Color(0.62f, 0.52f, 0.20f, 0.96f);
            case Room2DState.Ready:
                return new Color(0.28f, 0.52f, 0.34f, 0.96f);
            case Room2DState.Occupied:
                return new Color(0.44f, 0.32f, 0.58f, 0.96f);
            case Room2DState.Blocked:
                return new Color(0.30f, 0.30f, 0.34f, 0.96f);
            default:
                return new Color(0.30f, 0.42f, 0.34f, 0.96f);
        }
    }

    private string GetSelectedWorkerBadgeLabel()
    {
        if (workerSelectionPanel == null)
        {
            return "WORK";
        }

        switch (workerSelectionPanel.selectedWorkerType)
        {
            case Room2DWorkerSelectionPanel.PrototypeWorkerType.Housekeeper:
                return "HSK";
            case Room2DWorkerSelectionPanel.PrototypeWorkerType.Inspector:
                return "INSP";
            default:
                return "WORK";
        }
    }

    private Color GetSelectedWorkerBadgeColor()
    {
        if (workerSelectionPanel == null)
        {
            return new Color(0.28f, 0.32f, 0.40f, 0.96f);
        }

        switch (workerSelectionPanel.selectedWorkerType)
        {
            case Room2DWorkerSelectionPanel.PrototypeWorkerType.Housekeeper:
                return new Color(0.22f, 0.44f, 0.48f, 0.96f);
            case Room2DWorkerSelectionPanel.PrototypeWorkerType.Inspector:
                return new Color(0.50f, 0.42f, 0.22f, 0.96f);
            default:
                return new Color(0.28f, 0.32f, 0.40f, 0.96f);
        }
    }

    private void UpdatePlaceholderLabel(RectTransform placeholder, string label)
    {
        if (placeholder == null)
        {
            return;
        }

        Transform labelTransform = placeholder.Find("Text_PlaceholderLabel");
        TMP_Text labelText = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        if (labelText != null)
        {
            labelText.text = label;
        }
    }

    private void SetPlaceholderColor(RectTransform placeholder, Color color)
    {
        if (placeholder == null)
        {
            return;
        }

        Image image = placeholder.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }
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

    private void ToggleRoomWaitingGuestPeek()
    {
        roomWaitingGuestPeekVisible = !roomWaitingGuestPeekVisible;
        roomHousekeeperPanelVisible = false;
        roomInspectorPanelVisible = false;
    }

    private void CloseRoomWaitingGuestPeek()
    {
        roomWaitingGuestPeekVisible = false;
    }

    private void ToggleRoomHousekeeperPanel()
    {
        roomHousekeeperPanelVisible = !roomHousekeeperPanelVisible;
        roomWaitingGuestPeekVisible = false;
        roomInspectorPanelVisible = false;
    }

    private void ToggleRoomInspectorPanel()
    {
        roomInspectorPanelVisible = !roomInspectorPanelVisible;
        roomWaitingGuestPeekVisible = false;
        roomHousekeeperPanelVisible = false;
    }

    private void OpenRoomInfoPopup()
    {
        roomInfoPopupVisible = true;
        roomActionPopupVisible = false;
        roomWorkerPopupVisible = false;
    }

    private void CloseRoomInfoPopup()
    {
        roomInfoPopupVisible = false;
    }

    private void CloseRoomActionPopup()
    {
        roomActionPopupVisible = false;
        roomActionHint = "Room popup closed.";
    }

    private void OpenHousekeeperPopup()
    {
        SelectHousekeeper();
        roomWorkerPopupVisible = true;
        roomActionPopupVisible = false;
        roomInfoPopupVisible = false;
        roomActionHint = "Choose HSK, then Confirm.";
    }

    private void OpenInspectorPopup()
    {
        SelectInspector();
        roomWorkerPopupVisible = true;
        roomActionPopupVisible = false;
        roomInfoPopupVisible = false;
        roomActionHint = "Choose Inspector, then Confirm.";
    }

    private void ConfirmSelectedWorkerFromPopup()
    {
        AssignSelectedWorker();
        roomWorkerPopupVisible = false;
        roomActionPopupVisible = true;
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

    private string FormatGameMinutes(float seconds)
    {
        // 录屏 UI 用分钟表达等待压力，避免直接暴露原型秒数。
        int minutes = Mathf.Max(0, Mathf.FloorToInt(seconds / 60f));
        return minutes + " min";
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
        // Showcase UI 已经负责显示房间信息；旧 OnGUI 标签会压在 UI 上方，录屏阶段全部关闭。
        Room2DController.hidePrototypeDebugLabelsGlobally = true;
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
            roomInfoPopupVisible = false;
            roomActionPopupVisible = true;
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
        return IsPointerInsideActiveRect(roomActionsPanel, screenPosition)
            || IsPointerInsideActiveRect(roomRightLauncherPanel, screenPosition)
            || IsPointerInsideActiveRect(roomWaitingGuestPeekPanel, screenPosition)
            || IsPointerInsideActiveRect(roomHousekeeperPanel, screenPosition)
            || IsPointerInsideActiveRect(roomInspectorPanel, screenPosition)
            || IsPointerInsideActiveRect(roomActionPopupPanel, screenPosition)
            || IsPointerInsideActiveRect(roomInfoPopupPanel, screenPosition)
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
            existing.gameObject.SetActive(true);
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
        ApplyPanelBorder(panel, color.a > 0.05f);
        return panel;
    }

    private void ApplyPanelBorder(RectTransform panel, bool visible)
    {
        Outline outline = panel.GetComponent<Outline>();
        if (outline == null)
        {
            outline = panel.gameObject.AddComponent<Outline>();
        }

        outline.enabled = visible;
        outline.effectColor = new Color(0.50f, 0.62f, 0.72f, 0.58f);
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow shadow = panel.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = panel.gameObject.AddComponent<Shadow>();
        }

        shadow.enabled = visible;
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(0f, -4f);
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

        image.color = new Color(0.17f, 0.24f, 0.30f, 0.98f);

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
        labelText.color = Color.white;
        labelText.fontSize = 15f;
        labelText.alignment = TextAlignmentOptions.Center;
        ApplyStretch(labelText.rectTransform);

        Outline outline = buttonRect.GetComponent<Outline>();
        if (outline == null)
        {
            outline = buttonRect.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0.54f, 0.70f, 0.82f, 0.94f);
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow shadow = buttonRect.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = buttonRect.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
        shadow.effectDistance = new Vector2(0f, -3f);

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
        Button button = FindOrCreateActionButton(parent, buttonName, label, action);
        RectTransform buttonRect = button.transform as RectTransform;

        RectTransform iconPlaceholder = FindOrCreatePlaceholder(buttonRect, "ButtonIconPlaceholder_" + label, "");
        ApplyAnchoredPanel(iconPlaceholder, new Vector2(0.06f, 0.24f), new Vector2(0.20f, 0.76f), Vector2.zero, Vector2.zero);
        ApplyPlaceholderStyle(iconPlaceholder, new Color(0.24f, 0.34f, 0.44f, 0.96f), 11f);
        UpdatePlaceholderLabel(iconPlaceholder, GetButtonIconLabel(label));

        Transform labelTransform = buttonRect.Find("Text (TMP)");
        TMP_Text labelText = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        if (labelText != null)
        {
            labelText.rectTransform.offsetMin = new Vector2(34f, 0f);
        }

        return button;
    }

    private Button FindOrCreateFrontDeskGuestCard(RectTransform parent, string buttonName, string label, UnityAction action)
    {
        Button button = FindOrCreateActionButton(parent, buttonName, label, action);
        RectTransform buttonRect = button.transform as RectTransform;
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.localScale = Vector3.one;

        LayoutElement layout = buttonRect.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = buttonRect.gameObject.AddComponent<LayoutElement>();
        }

        // 固定卡片尺寸，避免 ScrollRect 里文字和头像互相挤压。
        layout.preferredWidth = 430f;
        layout.minWidth = 430f;
        layout.minHeight = 150f;
        layout.preferredHeight = 150f;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
        buttonRect.sizeDelta = new Vector2(430f, 150f);
        buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 430f);
        buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 150f);

        Image image = buttonRect.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.16f, 0.20f, 0.26f, 1f);
        }

        Outline outline = buttonRect.GetComponent<Outline>();
        if (outline == null)
        {
            outline = buttonRect.gameObject.AddComponent<Outline>();
        }
        outline.effectColor = new Color(0.65f, 0.78f, 0.88f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform portrait = FindOrCreatePlaceholder(buttonRect, "PortraitPlaceholder_GuestCard", "");
        ApplyAnchoredPanel(portrait, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -38f), new Vector2(94f, 38f));
        ApplyPlaceholderStyle(portrait, new Color(0.24f, 0.30f, 0.40f, 0.96f), 13f);

        Transform labelTransform = buttonRect.Find("Text (TMP)");
        TMP_Text labelText = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        if (labelText != null)
        {
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.fontSize = 16f;
            labelText.color = Color.white;
            labelText.overflowMode = TextOverflowModes.Overflow;
            labelText.textWrappingMode = TextWrappingModes.Normal;
            ApplyAnchoredPanel(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(116f, 12f), new Vector2(414f, -12f));
        }

        return button;
    }

    private Button FindOrCreateReadyRoomCard(RectTransform parent, string buttonName, string label, UnityAction action)
    {
        Button button = FindOrCreateActionButton(parent, buttonName, label, action);
        RectTransform buttonRect = button.transform as RectTransform;
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.localScale = Vector3.one;

        LayoutElement layout = buttonRect.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = buttonRect.gameObject.AddComponent<LayoutElement>();
        }

        layout.minHeight = 96f;
        layout.minWidth = 660f;
        layout.preferredWidth = 660f;
        layout.preferredHeight = 96f;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
        buttonRect.sizeDelta = new Vector2(660f, 96f);
        buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 660f);
        buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 96f);

        Image image = buttonRect.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.13f, 0.17f, 0.22f, 1f);
        }

        Outline outline = buttonRect.GetComponent<Outline>();
        if (outline == null)
        {
            outline = buttonRect.gameObject.AddComponent<Outline>();
        }

        outline.enabled = true;
        outline.effectColor = new Color(0.62f, 0.78f, 0.90f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform icon = FindOrCreatePlaceholder(buttonRect, "IconPlaceholder_RoomCard", "ROOM");
        ApplyAnchoredPanel(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -30f), new Vector2(92f, 30f));
        ApplyPlaceholderStyle(icon, new Color(0.26f, 0.38f, 0.50f, 0.96f), 12f);

        Transform labelTransform = buttonRect.Find("Text (TMP)");
        TMP_Text labelText = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        if (labelText != null)
        {
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.fontSize = 16f;
            labelText.color = Color.white;
            labelText.overflowMode = TextOverflowModes.Overflow;
            labelText.textWrappingMode = TextWrappingModes.Normal;
            ApplyAnchoredPanel(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(112f, 8f), new Vector2(640f, -8f));
        }

        return button;
    }

    private RectTransform FindOrCreateReadyRoomScrollContent(RectTransform parent)
    {
        Transform oldFixedPanel = parent.Find("Panel_ReadyRoomFixedCards");
        if (oldFixedPanel != null)
        {
            oldFixedPanel.gameObject.SetActive(false);
        }

        RectTransform scrollRectTransform = FindOrCreatePanel(parent, "Scroll_ReadyRoomList", new Color(0f, 0f, 0f, 0f));
        ApplyAnchoredPanel(scrollRectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.82f), Vector2.zero, Vector2.zero);

        ScrollRect scrollRect = scrollRectTransform.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
        }

        RectTransform viewport = FindOrCreatePanel(scrollRectTransform, "Viewport", new Color(0f, 0f, 0f, 0f));
        ApplyStretch(viewport);

        Mask oldMask = viewport.GetComponent<Mask>();
        if (oldMask != null)
        {
            oldMask.enabled = false;
        }

        RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
        if (rectMask == null)
        {
            rectMask = viewport.gameObject.AddComponent<RectMask2D>();
        }

        RectTransform content = FindOrCreateRectChild(viewport, "Content_ReadyRoomCards");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(700f, 420f);

        // Ready 房卡片手动定位。这里禁用自动 Layout，避免文字和图标再次被挤到不可见。
        LayoutGroup oldLayout = content.GetComponent<LayoutGroup>();
        if (oldLayout != null)
        {
            oldLayout.enabled = false;
        }

        ContentSizeFitter oldFitter = content.GetComponent<ContentSizeFitter>();
        if (oldFitter != null)
        {
            oldFitter.enabled = false;
        }

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.inertia = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        return content;
    }

    private RectTransform FindOrCreateHorizontalScrollContent(RectTransform parent, string scrollName, string contentName, float topOffset, float bottomOffset)
    {
        RectTransform scrollRectTransform = FindOrCreatePanel(parent, scrollName, new Color(0f, 0f, 0f, 0f));
        ApplyAnchoredPanel(scrollRectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, bottomOffset), new Vector2(-14f, -topOffset - 54f));

        ScrollRect scrollRect = scrollRectTransform.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
        }

        RectTransform viewport = FindOrCreatePanel(scrollRectTransform, "Viewport", new Color(0f, 0f, 0f, 0f));
        ApplyStretch(viewport);
        Mask oldMask = viewport.GetComponent<Mask>();
        if (oldMask != null)
        {
            oldMask.enabled = false;
        }

        RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
        if (rectMask == null)
        {
            rectMask = viewport.gameObject.AddComponent<RectMask2D>();
        }

        RectTransform content = FindOrCreateRectChild(viewport, contentName);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = new Vector2(0f, 0f);
        content.sizeDelta = new Vector2(1440f, 172f);

        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        layout.spacing = 14f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.inertia = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.normalizedPosition = Vector2.zero;
        return content;
    }

    private RectTransform FindOrCreateVerticalScrollContent(RectTransform parent, string scrollName, string contentName, float sidePadding, float topOffset)
    {
        RectTransform scrollRectTransform = FindOrCreatePanel(parent, scrollName, new Color(0f, 0f, 0f, 0f));
        ApplyAnchoredPanel(scrollRectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(sidePadding, 86f), new Vector2(-sidePadding, -topOffset));

        ScrollRect scrollRect = scrollRectTransform.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
        }

        RectTransform viewport = FindOrCreatePanel(scrollRectTransform, "Viewport", new Color(0f, 0f, 0f, 0f));
        ApplyStretch(viewport);
        Mask mask = viewport.GetComponent<Mask>();
        if (mask == null)
        {
            mask = viewport.gameObject.AddComponent<Mask>();
        }
        mask.showMaskGraphic = false;

        RectTransform content = FindOrCreateRectChild(viewport, contentName);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(760f, 680f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.spacing = 12f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        return content;
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

    // 统一占位图标/头像/徽章的可见风格，至少让录屏时看起来像 UI 元件而不是空白块。
    private void ApplyPlaceholderStyle(RectTransform placeholder, Color backgroundColor, float fontSize)
    {
        if (placeholder == null)
        {
            return;
        }

        Image image = placeholder.GetComponent<Image>();
        if (image != null)
        {
            image.color = backgroundColor;
            image.raycastTarget = false;
        }

        TMP_Text labelText = placeholder.Find("Text_PlaceholderLabel")?.GetComponent<TMP_Text>();
        if (labelText != null)
        {
            labelText.color = new Color(1f, 1f, 1f, 0.88f);
            labelText.fontSize = fontSize;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
        }
    }

    private string GetButtonIconLabel(string label)
    {
        switch (label)
        {
            case "Start":
                return "GO";
            case "Call Guest":
                return "IN";
            case "Wait":
                return "HOLD";
            case "Serve":
                return "SRV";
            case "Wash":
                return "WASH";
            case "Restock":
                return "BOX";
            default:
                return label.Length <= 3 ? label.ToUpper() : label.Substring(0, 3).ToUpper();
        }
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
        text.richText = true;
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
