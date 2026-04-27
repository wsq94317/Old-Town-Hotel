using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 临时原型调试 HUD。
// 这不是最终 UI，只负责让当前房间周转循环更容易测试。
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
    public TMP_Text selectedRoomInfoText;
    public TMP_Text overviewInfoText;
    public TMP_Text workerStatusText;
    public TMP_Text demandStatusText;

    [Header("Panel Layout")]
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
    public bool assignDefaultFontWhenMissing = true;
    public TMP_FontAsset fallbackFontAsset;

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
        FindReferencesIfNeeded();
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

        ApplyPrototypeButtonLabels();

        if (assignDefaultFontWhenMissing)
        {
            AssignFallbackFontsToHudTexts();
        }

        RedirectLegacyOverviewText();

        // 手机竖屏调试布局：信息放在上方，按钮放在下方，中间尽量留给房间。
        ApplyFixedPanel(overviewPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(410f, 540f));
        ApplyFixedPanel(selectedRoomPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(360f, 260f));
        ApplyFixedPanel(workerPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -292f), new Vector2(360f, 150f));
        ApplyFixedPanel(actionPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(760f, 150f));

        ApplyTextPanelStyle(selectedRoomPanel);
        ApplyTextPanelStyle(overviewPanel);
        ApplyTextPanelStyle(workerPanel);
        ApplyActionPanelStyle(actionPanel);

        ApplyTextStyle(selectedRoomInfoText, 21f, 230f, TextAlignmentOptions.TopLeft);
        ApplyTextStyle(overviewInfoText, 20f, 130f, TextAlignmentOptions.TopLeft);
        ApplyTextStyle(workerStatusText, 20f, 120f, TextAlignmentOptions.TopLeft);
        ApplyTextStyle(demandStatusText, 17f, 390f, TextAlignmentOptions.TopLeft);

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
        RedirectLegacyOverviewText();

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
            return "Selected\nRoom: None";
        }

        string occupiedText = "";
        if (room.currentState == Room2DState.Occupied && demandLoop != null)
        {
            float remainingSeconds = Mathf.Max(0f, demandLoop.occupiedDurationSeconds - room.stateElapsedSeconds);
            occupiedText = "\nCheckout in: " + FormatSeconds(remainingSeconds);
        }

        return "Selected\n"
            + "Room: " + room.roomName + "\n"
            + "State: " + room.GetStateDisplayName() + "\n"
            + "Next: " + room.GetNextActionDisplayName() + "\n"
            + "Wait: " + FormatSeconds(room.stateElapsedSeconds)
            + occupiedText;
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
            if (rooms[i] == null)
            {
                continue;
            }

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
                urgentRoomText = urgentRoom.roomName + " " + FormatSeconds(urgentRoom.stateElapsedSeconds);
            }
        }

        return "Overview\n"
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
            + "HSK: " + GetHousekeeperText() + "\n"
            + "Insp: " + GetInspectorText();
    }

    private string BuildDemandText()
    {
        if (demandLoop == null)
        {
            return "Demand\nNone";
        }

        return demandLoop.GetUpcomingDemandPreviewText() + "\n\n"
            + demandLoop.GetManualAssignmentText() + "\n\n"
            + "Last Demand\n"
            + "Type: " + demandLoop.lastDemandType + "\n"
            + "Match: " + demandLoop.lastMatchQualityLabel + "\n"
            + "Clean/Wear: " + demandLoop.lastCleanlinessSuitability + " / " + demandLoop.lastWearSuitability + "\n"
            + "Outcome: " + demandLoop.lastOutcomeLabel + "\n"
            + "Result: " + demandLoop.lastOutcomeSummary + "\n"
            + BuildOccupiedRoomsText() + "\n\n"
            + demandLoop.GetPrototypeDaySummaryText();
    }

    private string BuildOccupiedRoomsText()
    {
        if (demandLoop == null)
        {
            return "Occupied: None";
        }

        Room2DEntity[] rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
        string occupiedText = "Occupied: None";
        int shownCount = 0;

        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null || room.currentState != Room2DState.Occupied)
            {
                continue;
            }

            float remainingSeconds = Mathf.Max(0f, demandLoop.occupiedDurationSeconds - room.stateElapsedSeconds);
            if (shownCount == 0)
            {
                occupiedText = "Occupied:";
            }

            // 原型 HUD 只显示前 3 间，避免文字把测试画面挤爆。
            if (shownCount < 3)
            {
                occupiedText += "\n- " + room.roomName + " out in " + FormatSeconds(remainingSeconds);
            }

            shownCount++;
        }

        if (shownCount > 3)
        {
            occupiedText += "\n- +" + (shownCount - 3) + " more";
        }

        return occupiedText;
    }

    private string GetHousekeeperText()
    {
        if (housekeeper == null)
        {
            return "None";
        }

        return housekeeper.currentState
            + " / " + housekeeper.assignedRoomName
            + " / " + FormatSeconds(housekeeper.cleaningTimerSeconds);
    }

    private string GetInspectorText()
    {
        if (inspector == null)
        {
            return "None";
        }

        return inspector.currentState
            + " / " + inspector.assignedRoomName
            + " / " + FormatSeconds(inspector.inspectionTimerSeconds);
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
        layout.cellSize = new Vector2(220f, 48f);
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

        image.color = new Color(0f, 0f, 0f, 0.62f);
        image.raycastTarget = false;
    }

    private void ApplyTextStyle(TMP_Text text, float maxFontSize, float preferredHeight, TextAlignmentOptions alignment)
    {
        if (text == null)
        {
            return;
        }

        AssignFallbackFontIfNeeded(text);

        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = maxFontSize;
        text.raycastTarget = false;

        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(text.gameObject);
        layoutElement.minHeight = 36f;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleWidth = 1f;
    }

    private void AssignFallbackFontsToHudTexts()
    {
        Transform root = GetHudSearchRoot();
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            AssignFallbackFontIfNeeded(texts[i]);
        }
    }

    private void AssignFallbackFontIfNeeded(TMP_Text text)
    {
        if (text == null || text.font != null)
        {
            return;
        }

        TMP_FontAsset fontAsset = GetFallbackFontAsset();
        if (fontAsset == null)
        {
            return;
        }

        // 新建 TMP 文字有时没有 Font Asset；这里给 HUD 文字补一个默认字体，避免 Generate Mesh warning。
        text.font = fontAsset;
    }

    private TMP_FontAsset GetFallbackFontAsset()
    {
        if (fallbackFontAsset != null)
        {
            return fallbackFontAsset;
        }

        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
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
        if (!IsValidPanel(actionPanel))
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

    private void ApplyPrototypeButtonLabels()
    {
        Transform root = GetHudSearchRoot();
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == "Button_NextState")
            {
                SetButtonText(buttons[i], "DEBUG State");
            }
            else if (buttons[i].name == "Button_AssignDemand" || buttons[i].name == "Button_AssignSelectedDemand")
            {
                SetButtonText(buttons[i], "Assign Demand");
            }
        }
    }

    private void SetButtonText(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
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

            rectTransform.gameObject.SetActive(false);
        }
    }

    private void RedirectLegacyOverviewText()
    {
        if (roomOverview != null && overviewInfoText != null)
        {
            // 旧版 Room2DOverview 可能还绑定着一个单行 Text_RoomSummary。
            // 这里让总览系统写到新的 HUD 文本，避免两个总览文本叠在一起。
            roomOverview.summaryLabelTextMeshPro = overviewInfoText;
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

    private string FormatSeconds(float seconds)
    {
        int wholeSeconds = Mathf.FloorToInt(seconds);
        int minutes = wholeSeconds / 60;
        int remainingSeconds = wholeSeconds % 60;

        if (minutes > 0)
        {
            return minutes + "m " + remainingSeconds + "s";
        }

        return remainingSeconds + "s";
    }
}
