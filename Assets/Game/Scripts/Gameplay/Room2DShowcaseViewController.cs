using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 最小 Showcase 三视图控制器。
// Phase 1 只负责创建 Front Desk / Rooms / Lounge 的切换外壳，不改动现有玩法逻辑。
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

    [Header("Build")]
    // 开始运行时自动创建三个展示屏幕，减少手动搭 UI 的步骤。
    public bool autoBuildShellOnStart = true;
    public Canvas targetCanvas;
    public RectTransform showcaseRoot;
    public RectTransform navigationPanel;
    public RectTransform frontDeskViewPanel;
    public RectTransform roomViewPanel;
    public RectTransform loungeViewPanel;

    [Header("Texts")]
    public TMP_Text activeViewLabelText;
    public TMP_Text frontDeskShellText;
    public TMP_Text roomShellText;
    public TMP_Text loungeShellText;

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

        navigationPanel = FindOrCreatePanel(showcaseRoot, "Panel_ShowcaseBottomNav", new Color(0.03f, 0.04f, 0.06f, 0.88f));
        ApplyBottomPanel(navigationPanel, 138f);
        ApplyNavigationLayout(navigationPanel);

        frontDeskTabButton = FindOrCreateButton(navigationPanel, "Button_ShowFrontDeskView", "Front Desk");
        roomTabButton = FindOrCreateButton(navigationPanel, "Button_ShowRoomView", "Rooms");
        loungeTabButton = FindOrCreateButton(navigationPanel, "Button_ShowLoungeView", "Lounge");
        WireTabButtons();

        frontDeskViewPanel = FindOrCreatePanel(showcaseRoot, "Panel_FrontDeskView", new Color(0.02f, 0.03f, 0.04f, 0.84f));
        roomViewPanel = FindOrCreatePanel(showcaseRoot, "Panel_RoomView", new Color(0.02f, 0.03f, 0.04f, 0.08f));
        loungeViewPanel = FindOrCreatePanel(showcaseRoot, "Panel_LoungeView", new Color(0.02f, 0.03f, 0.04f, 0.84f));

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

        RefreshShellText();
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
        string phaseText = demoDayController != null
            ? demoDayController.GetCompactDemoDayText()
            : "Demo controller not linked";

        if (activeViewLabelText != null)
        {
            activeViewLabelText.text = "View: " + currentView;
        }

        if (frontDeskShellText != null)
        {
            frontDeskShellText.text = "[Front Desk View]\n"
                + phaseText + "\n"
                + "Phase 1 shell: queue, active demand, and front desk actions will be connected in Phase 2.";
        }

        if (roomShellText != null)
        {
            roomShellText.text = "[Room View]\n"
                + phaseText + "\n"
                + "Phase 1 shell: room grid stays visible here.";
        }

        if (loungeShellText != null)
        {
            loungeShellText.text = "[Lounge View]\n"
                + phaseText + "\n"
                + "Phase 1 shell: cups, stock, washing, and restock actions will be connected in Phase 2.";
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

        layoutElement.preferredWidth = 220f;
        layoutElement.preferredHeight = 64f;

        TMP_Text labelText = FindOrCreateText(buttonRect, "Text (TMP)", label);
        labelText.text = label;
        ApplyDefaultFont(labelText);
        labelText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        labelText.fontSize = 24f;
        labelText.alignment = TextAlignmentOptions.Center;
        ApplyStretch(labelText.rectTransform);

        return button;
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

    private void ApplyNavigationLayout(RectTransform panel)
    {
        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 18f;
        layout.padding = new RectOffset(24, 24, 24, 24);
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
        rect.offsetMin = new Vector2(0f, 138f);
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void ApplyShellText(TMP_Text text, TextAlignmentOptions alignment)
    {
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(-48f, 160f);

        text.color = Color.white;
        ApplyDefaultFont(text);
        text.fontSize = 24f;
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
        rect.anchoredPosition = new Vector2(24f, 50f);
        rect.sizeDelta = new Vector2(260f, 44f);

        text.color = Color.white;
        ApplyDefaultFont(text);
        text.fontSize = 20f;
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

    private void ApplyDefaultFont(TMP_Text text)
    {
        if (text.font != null || TMP_Settings.defaultFontAsset == null)
        {
            return;
        }

        text.font = TMP_Settings.defaultFontAsset;
    }
}
