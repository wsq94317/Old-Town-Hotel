using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 临时原型调试 HUD。
// 它不是最终玩家 UI，只负责把当前测试信息分区显示，方便调试房间周转循环。
public class Room2DPrototypeDebugHud : MonoBehaviour
{
    [Header("References")]
    // 自动寻找场景里的原型系统，减少手动拖引用。
    public bool autoFindReferences = true;
    public Room2DSelectionManager selectionManager;
    public Room2DOverview roomOverview;
    public Housekeeper2D housekeeper;
    public Inspector2D inspector;
    public Room2DPrototypeDemandLoop demandLoop;

    [Header("Text Targets")]
    // 顶部：当前选中房间信息。
    public TMP_Text selectedRoomInfoText;

    // 左侧：房态总览和最高优先级 Dirty 房。
    public TMP_Text overviewInfoText;

    // 右侧：保洁和查房主管状态。
    public TMP_Text workerStatusText;

    // 可选：外部需求循环状态，可以放在左侧或右侧面板里。
    public TMP_Text demandStatusText;

    [Header("Panel Layout")]
    // 这些 RectTransform 是 Canvas 下的四个分区面板。
    public RectTransform selectedRoomPanel;
    public RectTransform overviewPanel;
    public RectTransform workerPanel;
    public RectTransform actionPanel;

    [Header("Layout Helpers")]
    // 原型阶段先让脚本自动整理 HUD，避免手动拖拽 RectTransform 时把文字叠在一起。
    public bool applyLayoutOnStart = true;
    public bool moveBoundTextsToPanels = true;
    public bool moveButtonsToActionPanel = true;
    public bool hideUnboundDebugTexts = true;
    public bool hideUnboundDebugPanels = true;

    [Header("Refresh")]
    public bool refreshDuringPlay = true;
    public float refreshIntervalSeconds = 0.25f;

    private float refreshTimer;

    private void Start()
    {
        FindReferencesIfNeeded();
        FindHudObjectsIfNeeded();

        if (applyLayoutOnStart)
        {
            ApplyPrototypeHudLayout();
        }

        RefreshHud();
    }

    private void Update()
    {
        if (!refreshDuringPlay)
        {
            return;
        }

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshIntervalSeconds)
        {
            return;
        }

        refreshTimer = 0f;
        RefreshHud();
    }

    [ContextMenu("Apply Prototype HUD Layout")]
    public void ApplyPrototypeHudLayout()
    {
        FindHudObjectsIfNeeded();
        ApplyCanvasSettings();
        ApplyHudRootLayout();

        if (moveBoundTextsToPanels)
        {
            MoveBoundTextsToCorrectPanels();
        }

        if (moveButtonsToActionPanel)
        {
            MoveHudButtonsToActionPanel();
        }

        // 四块区域都用固定像素尺寸，手机竖屏下更容易读，也不会把房间区域整片盖住。
        ApplyFixedPanel(overviewPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(330f, 370f));
        ApplyFixedPanel(selectedRoomPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(360f, 230f));
        ApplyFixedPanel(workerPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -262f), new Vector2(360f, 190f));
        ApplyFixedPanel(actionPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(720f, 150f));

        ApplyTextPanelStyle(selectedRoomPanel);
        ApplyTextPanelStyle(overviewPanel);
        ApplyTextPanelStyle(workerPanel);
        ApplyActionPanelStyle(actionPanel);

        ApplyTextStyle(selectedRoomInfoText, 24f, TextAlignmentOptions.TopLeft);
        ApplyTextStyle(overviewInfoText, 23f, TextAlignmentOptions.TopLeft);
        ApplyTextStyle(workerStatusText, 23f, TextAlignmentOptions.TopLeft);
        ApplyTextStyle(demandStatusText, 21f, TextAlignmentOptions.TopLeft);

        if (hideUnboundDebugTexts)
        {
            HideUnboundTextsInHudCanvas();
        }

        if (hideUnboundDebugPanels)
        {
            HideUnboundPanelsInHudCanvas();
        }

        RefreshHud();
    }

    [ContextMenu("Refresh HUD Now")]
    public void RefreshHud()
    {
        FindReferencesIfNeeded();

        if (selectedRoomInfoText != null)
        {
            selectedRoomInfoText.text = BuildSelectedRoomText();
        }

        if (overviewInfoText != null)
        {
            overviewInfoText.text = BuildOverviewText();
        }

        if (workerStatusText != null)
        {
            workerStatusText.text = BuildWorkerText();
        }

        if (demandStatusText != null)
        {
            demandStatusText.text = BuildDemandText();
        }
    }

    private void FindReferencesIfNeeded()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (selectionManager == null)
        {
            selectionManager = FindFirstObjectByType<Room2DSelectionManager>();
        }

        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }

        if (housekeeper == null)
        {
            housekeeper = FindFirstObjectByType<Housekeeper2D>();
        }

        if (inspector == null)
        {
            inspector = FindFirstObjectByType<Inspector2D>();
        }

        if (demandLoop == null)
        {
            demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        }
    }

    private void FindHudObjectsIfNeeded()
    {
        // 兼容之前手动创建过的不同命名，减少重新搭 UI 的成本。
        if (!IsValidPanel(selectedRoomPanel))
        {
            selectedRoomPanel = FindRectTransformInHud("Panel_SelectedRoom");
        }

        if (!IsValidPanel(overviewPanel))
        {
            overviewPanel = FindRectTransformInHud("Panel_RoomOverview", "Panel_Overview");
        }

        if (!IsValidPanel(workerPanel))
        {
            workerPanel = FindRectTransformInHud("Panel_Workers", "Panel_WorkerStatus");
        }

        if (!IsValidPanel(actionPanel))
        {
            actionPanel = FindRectTransformInHud("Panel_Actions", "Panel_ActionButtons");
        }

        if (selectedRoomInfoText == null)
        {
            selectedRoomInfoText = FindTextInHud("Text_SelectedRoomInfo", "Text_Selected_Room", "Text_SelectedRoom");
        }

        if (overviewInfoText == null)
        {
            overviewInfoText = FindTextInHud("Text_OverviewInfo", "Text_RoomSummary");
        }

        if (workerStatusText == null)
        {
            workerStatusText = FindTextInHud("Text_WorkerStatus");
        }

        if (demandStatusText == null)
        {
            demandStatusText = FindTextInHud("Text_DemandInfo", "Text_DemandStatus");
        }
    }

    private string BuildSelectedRoomText()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        if (room == null)
        {
            return "Selected Room\nNone";
        }

        return "Selected Room\n"
            + room.roomName + "\n"
            + "State: " + room.GetStateDisplayName() + "\n"
            + room.GetNextActionDisplayName() + "\n"
            + room.GetCleaningPriorityDisplayName() + "\n"
            + room.GetStateTimeDisplayName();
    }

    private string BuildOverviewText()
    {
        Room2DEntity[] rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
        int dirtyCount = 0;
        int cleaningCount = 0;
        int awaitingInspectionCount = 0;
        int readyCount = 0;
        int occupiedCount = 0;
        int blockedCount = 0;

        for (int i = 0; i < rooms.Length; i++)
        {
            switch (rooms[i].currentState)
            {
                case Room2DState.Cleaning:
                    cleaningCount++;
                    break;
                case Room2DState.AwaitingInspection:
                    awaitingInspectionCount++;
                    break;
                case Room2DState.Ready:
                    readyCount++;
                    break;
                case Room2DState.Occupied:
                    occupiedCount++;
                    break;
                case Room2DState.Blocked:
                    blockedCount++;
                    break;
                default:
                    dirtyCount++;
                    break;
            }
        }

        string urgentRoomText = "None";
        if (roomOverview != null)
        {
            Room2DEntity urgentRoom = roomOverview.GetHighestPriorityDirtyRoom();
            if (urgentRoom != null)
            {
                urgentRoomText = urgentRoom.roomName + " " + Mathf.FloorToInt(urgentRoom.stateElapsedSeconds) + "s";
            }
        }

        return "Room Overview\n"
            + "Dirty: " + dirtyCount + "\n"
            + "Cleaning: " + cleaningCount + "\n"
            + "Inspect: " + awaitingInspectionCount + "\n"
            + "Ready: " + readyCount + "\n"
            + "Occupied: " + occupiedCount + "\n"
            + "Blocked: " + blockedCount + "\n"
            + "Urgent: " + urgentRoomText;
    }

    private string BuildWorkerText()
    {
        return "Workers\n"
            + "Housekeeper: " + GetHousekeeperText() + "\n"
            + "Inspector: " + GetInspectorText();
    }

    private string BuildDemandText()
    {
        if (demandLoop == null)
        {
            return "Demand\nNone";
        }

        return "Demand\n"
            + "Generated: " + demandLoop.generatedDemandCount + "\n"
            + "Success: " + demandLoop.successfulDemandCount + "\n"
            + "Unmet: " + demandLoop.unmetDemandCount + "\n"
            + "Checkouts: " + demandLoop.simulatedCheckoutCount + "\n"
            + demandLoop.lastDemandResult;
    }

    private string GetHousekeeperText()
    {
        if (housekeeper == null)
        {
            return "None";
        }

        return housekeeper.currentState
            + " / " + housekeeper.assignedRoomName
            + " / " + Mathf.FloorToInt(housekeeper.cleaningTimerSeconds) + "s";
    }

    private string GetInspectorText()
    {
        if (inspector == null)
        {
            return "None";
        }

        return inspector.currentState
            + " / " + inspector.assignedRoomName
            + " / " + Mathf.FloorToInt(inspector.inspectionTimerSeconds) + "s";
    }

    private Room2DEntity GetSelectedRoomEntity()
    {
        if (selectionManager == null || selectionManager.selectedRoom == null)
        {
            return null;
        }

        return selectionManager.selectedRoom.roomEntity;
    }

    private void ApplyCanvasSettings()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            return;
        }

        // 统一用手机竖屏参考尺寸，避免不同 Game 窗口比例下文字忽大忽小。
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void ApplyHudRootLayout()
    {
        RectTransform root = GetComponent<RectTransform>();
        if (root != null)
        {
            // HUD 根物体必须铺满 Canvas；否则所有子 Panel 都会相对错误区域排版。
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.anchoredPosition = Vector2.zero;
            root.localScale = Vector3.one;
        }

        Image rootImage = GetComponent<Image>();
        if (rootImage != null)
        {
            // 根物体不应该有背景，否则会出现一整块灰色遮罩。
            rootImage.enabled = false;
            rootImage.raycastTarget = false;
        }

        LayoutGroup[] rootLayoutGroups = GetComponents<LayoutGroup>();
        for (int i = 0; i < rootLayoutGroups.Length; i++)
        {
            // 根物体不参与自动布局，只让四个 Panel 自己布局。
            rootLayoutGroups[i].enabled = false;
        }
    }

    private void ApplyFixedPanel(RectTransform panel, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        if (!IsValidPanel(panel))
        {
            return;
        }

        panel.anchorMin = anchorMin;
        panel.anchorMax = anchorMax;
        panel.pivot = new Vector2(anchorMin.x, anchorMin.y);
        panel.anchoredPosition = anchoredPosition;
        panel.sizeDelta = size;
    }

    private void ApplyTextPanelStyle(RectTransform panel)
    {
        if (!IsValidPanel(panel))
        {
            return;
        }

        ApplyPanelBackground(panel);

        VerticalLayoutGroup layout = GetOrAddComponent<VerticalLayoutGroup>(panel.gameObject);
        DisableOtherLayoutGroups(panel, layout);

        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private void ApplyActionPanelStyle(RectTransform panel)
    {
        if (!IsValidPanel(panel))
        {
            return;
        }

        ApplyPanelBackground(panel);

        GridLayoutGroup layout = GetOrAddComponent<GridLayoutGroup>(panel.gameObject);
        DisableOtherLayoutGroups(panel, layout);

        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = new Vector2(12f, 10f);
        layout.cellSize = new Vector2(210f, 48f);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;
    }

    private void ApplyPanelBackground(RectTransform panel)
    {
        if (!IsValidPanel(panel))
        {
            return;
        }

        Image image = GetOrAddComponent<Image>(panel.gameObject);
        if (image == null)
        {
            return;
        }

        // 深色半透明背景比默认白色 Panel 更适合调试，不会整片发灰。
        image.color = new Color(0f, 0f, 0f, 0.55f);
        image.raycastTarget = false;
    }

    private void ApplyTextStyle(TMP_Text text, float maxFontSize, TextAlignmentOptions alignment)
    {
        if (text == null)
        {
            return;
        }

        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = maxFontSize;
        text.raycastTarget = false;

        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(text.gameObject);
        layoutElement.minHeight = 36f;
        layoutElement.preferredHeight = text == demandStatusText ? 130f : 180f;
        layoutElement.flexibleWidth = 1f;
    }

    private void MoveBoundTextsToCorrectPanels()
    {
        MoveTextToPanel(selectedRoomInfoText, selectedRoomPanel);
        MoveTextToPanel(overviewInfoText, overviewPanel);
        MoveTextToPanel(demandStatusText, overviewPanel);
        MoveTextToPanel(workerStatusText, workerPanel);
    }

    private void MoveTextToPanel(TMP_Text text, RectTransform panel)
    {
        if (text == null || !IsValidPanel(panel) || text.transform.parent == panel)
        {
            return;
        }

        text.transform.SetParent(panel, false);
    }

    private void MoveHudButtonsToActionPanel()
    {
        if (actionPanel == null)
        {
            return;
        }

        Transform root = GetHudSearchRoot();
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!buttons[i].name.StartsWith("Button_"))
            {
                continue;
            }

            buttons[i].transform.SetParent(actionPanel, false);
        }
    }

    private void HideUnboundTextsInHudCanvas()
    {
        Transform root = GetHudSearchRoot();
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == selectedRoomInfoText || text == overviewInfoText || text == workerStatusText || text == demandStatusText)
            {
                text.gameObject.SetActive(true);
                continue;
            }

            if (text.GetComponentInParent<Button>() != null)
            {
                text.gameObject.SetActive(true);
                continue;
            }

            // 旧版散落的调试 Text 会造成重叠，先隐藏，之后需要时再手动删除。
            text.gameObject.SetActive(false);
        }
    }

    private void HideUnboundPanelsInHudCanvas()
    {
        Transform root = GetHudSearchRoot();
        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            RectTransform rectTransform = images[i].GetComponent<RectTransform>();
            if (rectTransform == null || !rectTransform.name.StartsWith("Panel_"))
            {
                continue;
            }

            if (rectTransform == selectedRoomPanel || rectTransform == overviewPanel || rectTransform == workerPanel || rectTransform == actionPanel)
            {
                rectTransform.gameObject.SetActive(true);
                continue;
            }

            // 旧版没再使用的 Panel 会遮住画面，原型阶段先隐藏。
            rectTransform.gameObject.SetActive(false);
        }
    }

    private bool IsValidPanel(RectTransform panel)
    {
        if (panel == null)
        {
            return false;
        }

        if (!panel.name.StartsWith("Panel_"))
        {
            return false;
        }

        // Text 和 Button 不能当 Panel 用，否则 Unity UI Graphic 组件会冲突。
        if (panel.GetComponent<TMP_Text>() != null || panel.GetComponent<Button>() != null)
        {
            return false;
        }

        return true;
    }

    private RectTransform FindRectTransformInHud(params string[] names)
    {
        Transform root = GetHudSearchRoot();
        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(true);

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            for (int j = 0; j < names.Length; j++)
            {
                if (rectTransforms[i].name == names[j] && IsValidPanel(rectTransforms[i]))
                {
                    return rectTransforms[i];
                }
            }
        }

        return null;
    }

    private TMP_Text FindTextInHud(params string[] names)
    {
        Transform root = GetHudSearchRoot();
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            for (int j = 0; j < names.Length; j++)
            {
                if (texts[i].name == names[j])
                {
                    return texts[i];
                }
            }
        }

        return null;
    }

    private Transform GetHudSearchRoot()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            return canvas.transform;
        }

        return transform;
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    private void DisableOtherLayoutGroups(RectTransform panel, LayoutGroup layoutToKeep)
    {
        LayoutGroup[] layoutGroups = panel.GetComponents<LayoutGroup>();
        for (int i = 0; i < layoutGroups.Length; i++)
        {
            layoutGroups[i].enabled = layoutGroups[i] == layoutToKeep;
        }
    }
}
