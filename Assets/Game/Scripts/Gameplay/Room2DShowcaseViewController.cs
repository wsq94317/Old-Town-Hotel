using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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
    public Canvas targetCanvas;
    public RectTransform showcaseRoot;
    public RectTransform navigationPanel;
    public RectTransform frontDeskViewPanel;
    public RectTransform roomViewPanel;
    public RectTransform loungeViewPanel;
    public RectTransform frontDeskStatusPanel;
    public RectTransform frontDeskDemandPanel;
    public RectTransform frontDeskActionsPanel;
    public RectTransform roomSelectedPanel;
    public RectTransform roomWorkersPanel;
    public RectTransform roomActionsPanel;
    public RectTransform loungeStatusPanel;
    public RectTransform loungeActionsPanel;

    [Header("Texts")]
    public TMP_Text activeViewLabelText;
    public TMP_Text frontDeskShellText;
    public TMP_Text roomShellText;
    public TMP_Text loungeShellText;
    public TMP_Text frontDeskStatusText;
    public TMP_Text frontDeskDemandText;
    public TMP_Text selectedRoomText;
    public TMP_Text roomWorkersText;
    public TMP_Text loungeStatusText;

    [Header("Buttons")]
    public Button frontDeskTabButton;
    public Button roomTabButton;
    public Button loungeTabButton;

    [Header("Runtime")]
    public ShowcaseView currentView = ShowcaseView.Rooms;
    public string lastShellResult = "Not built";

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
        RefreshShellText();
    }

    [ContextMenu("Build Showcase View Shell")]
    public void BuildShowcaseViewShell()
    {
        FindReferencesIfNeeded();

        if (!FindCanvasIfNeeded())
        {
            lastShellResult = "No Canvas found. Create a Canvas first.";
            return;
        }

        showcaseRoot = FindOrCreateRectChild(targetCanvas.transform, RootName);
        ApplyStretch(showcaseRoot);
        showcaseRoot.SetAsLastSibling();
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

    private bool FindCanvasIfNeeded()
    {
        if (targetCanvas != null)
        {
            return true;
        }

        targetCanvas = GetComponentInParent<Canvas>();

        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
        }

        return targetCanvas != null;
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
    }

    private void BuildFrontDeskViewContent()
    {
        frontDeskStatusPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_FrontDeskStatus", new Color(0.05f, 0.07f, 0.09f, 0.96f));
        frontDeskDemandPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_FrontDeskDemand", new Color(0.08f, 0.09f, 0.12f, 0.96f));
        frontDeskActionsPanel = FindOrCreatePanel(frontDeskViewPanel, "Card_FrontDeskActions", new Color(0.05f, 0.05f, 0.07f, 0.96f));

        ApplyAnchoredPanel(frontDeskStatusPanel, new Vector2(0.07f, 0.66f), new Vector2(0.93f, 0.91f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskDemandPanel, new Vector2(0.07f, 0.31f), new Vector2(0.93f, 0.63f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(frontDeskActionsPanel, new Vector2(0.07f, 0.12f), new Vector2(0.93f, 0.27f), Vector2.zero, Vector2.zero);

        frontDeskStatusText = FindOrCreateText(frontDeskStatusPanel, "Text_FrontDeskStatus", "Front Desk");
        frontDeskDemandText = FindOrCreateText(frontDeskDemandPanel, "Text_FrontDeskDemand", "Demand");
        ApplyCardText(frontDeskStatusText, 15f);
        ApplyCardText(frontDeskDemandText, 14f);

        ApplyActionGrid(frontDeskActionsPanel, 2, new Vector2(190f, 42f), 12f);
        FindOrCreateActionButton(frontDeskActionsPanel, "Button_StartOperating", "Start Day", StartDemoOperatingPeriod);
        FindOrCreateActionButton(frontDeskActionsPanel, "Button_ActivateDemand", "Call Guest", ActivateUpcomingDemandNow);
        FindOrCreateActionButton(frontDeskActionsPanel, "Button_AssignDemandShowcase", "Assign Room", AssignSelectedRoomToDemand);
        FindOrCreateActionButton(frontDeskActionsPanel, "Button_FrontDeskWait", "Wait", RecordWaitAction);
    }

    private void BuildRoomViewContent()
    {
        roomSelectedPanel = FindOrCreatePanel(roomViewPanel, "Card_SelectedRoom", new Color(0.03f, 0.04f, 0.06f, 0.88f));
        roomWorkersPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomWorkers", new Color(0.03f, 0.04f, 0.06f, 0.88f));
        roomActionsPanel = FindOrCreatePanel(roomViewPanel, "Card_RoomActions", new Color(0.03f, 0.04f, 0.06f, 0.92f));

        ApplyAnchoredPanel(roomSelectedPanel, new Vector2(0.04f, 0.10f), new Vector2(0.48f, 0.30f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomWorkersPanel, new Vector2(0.52f, 0.10f), new Vector2(0.96f, 0.30f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(roomActionsPanel, new Vector2(0.55f, 0.56f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);

        selectedRoomText = FindOrCreateText(roomSelectedPanel, "Text_SelectedRoomCard", "Selected Room");
        roomWorkersText = FindOrCreateText(roomWorkersPanel, "Text_WorkerCard", "Workers");
        ApplyCardText(selectedRoomText, 13f);
        ApplyCardText(roomWorkersText, 13f);

        ApplyActionGrid(roomActionsPanel, 2, new Vector2(150f, 36f), 8f);
        FindOrCreateActionButton(roomActionsPanel, "Button_PreviousRoomShowcase", "Prev Room", SelectPreviousRoom);
        FindOrCreateActionButton(roomActionsPanel, "Button_NextRoomShowcase", "Next Room", SelectNextRoom);
        FindOrCreateActionButton(roomActionsPanel, "Button_SelectHSKShowcase", "Select HSK", SelectHousekeeper);
        FindOrCreateActionButton(roomActionsPanel, "Button_SelectInspShowcase", "Select Insp", SelectInspector);
        FindOrCreateActionButton(roomActionsPanel, "Button_AssignWorkerShowcase", "Assign Worker", AssignSelectedWorker);
        FindOrCreateActionButton(roomActionsPanel, "Button_ReserveShowcase", "Reserve", ReserveSelectedRoom);
        FindOrCreateActionButton(roomActionsPanel, "Button_DirtyPrioShowcase", "Clean Prio", MarkDirtyPriority);
        FindOrCreateActionButton(roomActionsPanel, "Button_InspPrioShowcase", "Insp Prio", MarkInspectionPriority);
        FindOrCreateActionButton(roomActionsPanel, "Button_AssignDemandRoomView", "Assign Demand", AssignSelectedRoomToDemand);
    }

    private void BuildLoungeViewContent()
    {
        loungeStatusPanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeStatus", new Color(0.05f, 0.07f, 0.09f, 0.96f));
        loungeActionsPanel = FindOrCreatePanel(loungeViewPanel, "Card_LoungeActions", new Color(0.05f, 0.05f, 0.07f, 0.96f));

        ApplyAnchoredPanel(loungeStatusPanel, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
        ApplyAnchoredPanel(loungeActionsPanel, new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.30f), Vector2.zero, Vector2.zero);

        loungeStatusText = FindOrCreateText(loungeStatusPanel, "Text_LoungeStatus", "Lounge");
        ApplyCardText(loungeStatusText, 18f);

        ApplyActionGrid(loungeActionsPanel, 2, new Vector2(190f, 44f), 12f);
        FindOrCreateActionButton(loungeActionsPanel, "Button_ServeLoungeShowcase", "Serve Drink", ServeLoungeNow);
        FindOrCreateActionButton(loungeActionsPanel, "Button_WashCupsShowcase", "Wash Cups", StartLoungeWash);
        FindOrCreateActionButton(loungeActionsPanel, "Button_RestockLoungeShowcase", "Restock", RestockLounge);
        FindOrCreateActionButton(loungeActionsPanel, "Button_StartDayLoungeShowcase", "Start Day", StartDemoOperatingPeriod);
    }

    private string BuildFrontDeskStatusText()
    {
        string phase = demoDayController != null ? demoDayController.currentPhase.ToString() : "None";
        string time = demoDayController != null ? FormatSeconds(demoDayController.operatingTimerSeconds) : "0s";
        string duration = demoDayController != null ? FormatSeconds(demoDayController.operatingDurationSeconds) : "0s";
        string queue = frontDesk != null ? frontDesk.currentQueueCount.ToString() : "0";
        string wait = frontDesk != null ? FormatSeconds(frontDesk.waitingTimePressureSeconds) : "0s";
        string delayed = frontDesk != null ? frontDesk.totalDelayedCheckIns.ToString() : "0";
        string score = demandLoop != null
            ? demandLoop.prototypeSatisfactionScore + " (" + demandLoop.prototypeSatisfactionTrend + ")"
            : "0";

        return "Front Desk\n"
            + "Phase: " + phase + "   Time: " + time + " / " + duration + "\n"
            + "Queue: " + queue + "   Wait: " + wait + "\n"
            + "Delayed Check-ins: " + delayed + "\n"
            + "Satisfaction: " + score + "\n"
            + "Last: " + GetFrontDeskLastResultText();
    }

    private string BuildFrontDeskDemandText()
    {
        if (demandLoop == null)
        {
            return "Demand\nNone";
        }

        return "Demand\n"
            + "Upcoming: " + demandLoop.upcomingDemandType
            + " / " + demandLoop.upcomingDemandRoomPreference + "\n"
            + "ETA: " + FormatSeconds(demandLoop.upcomingDemandEtaSeconds)
            + "   Reserved: " + demandLoop.reservedRoomName + "\n"
            + "Active: " + GetActiveDemandShortLine() + "\n"
            + "Complaint: " + GetComplaintShortLine() + "\n"
            + "Latest: " + demandLoop.lastChangedRoomName
            + " / " + demandLoop.lastMatchQualityLabel
            + " / " + demandLoop.lastOutcomeLabel + "\n"
            + "Totals: " + demandLoop.successfulDemandCount
            + " ok, " + demandLoop.unmetDemandCount + " missed";
    }

    private string BuildSelectedRoomText()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        if (room == null)
        {
            return "Selected Room\nNone";
        }

        string reservedText = demandLoop != null && demandLoop.IsRoomReservedForPrototypeDemand(room) ? "Yes" : "No";
        string matchHint = demandLoop != null ? demandLoop.GetPrototypeMatchHintForRoom(room) : "Match Hint: None";

        return "Selected\n"
            + room.roomName + "   " + room.GetStateDisplayName() + "\n"
            + "Type: " + room.prototypeRoomType
            + "   Floor: " + room.floorNumber + "\n"
            + "Facing: " + room.prototypeFacing + "\n"
            + "Reserved: " + reservedText
            + "   Checked Out: " + GetYesNo(room.guestCheckedOut) + "\n"
            + "CLEAN: " + GetYesNo(room.markedCleaningPriority)
            + "   INSP: " + GetYesNo(room.markedInspectionPriority) + "\n"
            + "Wait: " + FormatSeconds(room.stateElapsedSeconds) + "\n"
            + ShortenMatchHint(matchHint);
    }

    private string BuildRoomWorkerText()
    {
        string summaryText = BuildRoomCountSummaryText();
        return summaryText + "\n\n" + BuildCompactWorkerText();
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
            + "Washing: " + washingText + "\n"
            + "Warning: " + lounge.loungeWarning + "\n"
            + "Served/Missed: " + lounge.servedDrinkCount + " / " + lounge.missedServiceCount + "\n"
            + "Last: " + lounge.lastLoungeResult;
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

    private void AssignSelectedRoomToDemand()
    {
        FindReferencesIfNeeded();
        if (demandLoop != null)
        {
            demandLoop.AssignSelectedRoomToActiveDemand();
        }
    }

    private void ReserveSelectedRoom()
    {
        FindReferencesIfNeeded();
        if (demandLoop != null)
        {
            demandLoop.ReserveSelectedRoomForUpcomingDemand();
        }
    }

    private void MarkDirtyPriority()
    {
        FindReferencesIfNeeded();
        if (demandLoop != null)
        {
            demandLoop.MarkSelectedDirtyRoomAsPriority();
        }
    }

    private void MarkInspectionPriority()
    {
        FindReferencesIfNeeded();
        if (demandLoop != null)
        {
            demandLoop.MarkSelectedInspectionRoomAsPriority();
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
        }
    }

    private void SelectInspector()
    {
        FindReferencesIfNeeded();
        if (workerSelectionPanel != null)
        {
            workerSelectionPanel.SelectInspector();
        }
    }

    private void AssignSelectedWorker()
    {
        FindReferencesIfNeeded();
        if (workerSelectionPanel != null)
        {
            workerSelectionPanel.AssignSelectedWorkerToSelectedRoom();
        }
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
