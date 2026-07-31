using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public enum WorldHudMode
{
    Hidden,
    Operations,
    MorningReport
}

public static class WorldHudVisibilityPolicy
{
    public static WorldHudMode Resolve(bool simulationReady, bool awaitingMorningReport)
    {
        if (!simulationReady) return WorldHudMode.Hidden;
        return awaitingMorningReport ? WorldHudMode.MorningReport : WorldHudMode.Operations;
    }
}

/// <summary>
/// Scene-first management HUD for the v3 simulation. The hotel remains visible,
/// while economy decisions are grouped by the question the player is answering.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldManagementHud : MonoBehaviour
{
    private enum DeskTab
    {
        Overview,
        Pricing,
        Staff,
        Assets
    }

    // Old hotel front-desk language: smoked glass, walnut, oxidized brass and
    // muted ledger paper. Parchment is reserved for type, never large surfaces.
    private static readonly Color Ink = Hex("#1B1713");
    private static readonly Color InkSoft = Hex("#B9AB96");
    private static readonly Color Cream = Hex("#E9D9BC");
    private static readonly Color Paper = new Color(0.16f, 0.13f, 0.105f, 0.97f);
    private static readonly Color Gold = Hex("#C99A45");
    private static readonly Color GoldDark = Hex("#765426");
    private static readonly Color Teal = Hex("#2E6D61");
    private static readonly Color TealSoft = Hex("#86A99D");
    private static readonly Color Coral = Hex("#A64E40");
    private static readonly Color Slate = Hex("#6E685E");
    private static readonly Color White = Hex("#F5EAD4");
    private static readonly Color Glass = new Color(0.075f, 0.065f, 0.055f, 0.95f);
    private static readonly Color Ledger = new Color(0.105f, 0.087f, 0.07f, 0.98f);
    private static readonly Color Walnut = new Color(0.19f, 0.135f, 0.10f, 0.97f);
    private static readonly Color BrassLine = new Color(0.79f, 0.60f, 0.27f, 0.55f);

    private static WorldManagementHud _instance;
    private static Sprite _roundedSprite;
    private static TMP_FontAsset _runtimeFont;

    private Canvas _canvas;
    private RectTransform _safeRoot;
    private RectTransform _topPanel;
    private RectTransform _bottomNav;
    private RectTransform _drawer;
    private RectTransform _drawerContent;
    private RectTransform _roomCard;
    private RectTransform _alertStrip;
    private RectTransform _contextSheet;
    private RectTransform _contextContent;
    private RectTransform _toastPanel;
    private RectTransform _morningBackdrop;
    private RectTransform _morningReport;
    private RectTransform _morningContent;
    private CanvasGroup _toastGroup;

    private TextMeshProUGUI _dayTimeLabel;
    private TextMeshProUGUI _cashLabel;
    private TextMeshProUGUI _earnedLabel;
    private TextMeshProUGUI _occupancyLabel;
    private TextMeshProUGUI _safeboxLabel;
    private TextMeshProUGUI _drawerTitle;
    private TextMeshProUGUI _roomTitle;
    private TextMeshProUGUI _roomHeadline;
    private TextMeshProUGUI _roomDetail;
    private TextMeshProUGUI _roomPrimaryLabel;
    private TextMeshProUGUI _roomSecondaryLabel;
    private TextMeshProUGUI _alertLabel;
    private TextMeshProUGUI _alertGoLabel;
    private TextMeshProUGUI _toastLabel;
    private TextMeshProUGUI _morningTitle;
    private TextMeshProUGUI _morningSubtitle;
    private TextMeshProUGUI _openDoorsLabel;

    private Image _safeboxFill;
    private Image _safeboxButtonImage;
    private Button _safeboxButton;
    private Button _roomPrimary;
    private Button _roomSecondary;
    private Button _alertGoButton;
    private Button _openDoorsButton;

    private readonly List<Button> _navButtons = new List<Button>();
    private readonly List<TextMeshProUGUI> _navLabels = new List<TextMeshProUGUI>();

    private DeskTab _tab;
    private bool _drawerOpen;
    private bool _initializedValues;
    private int _lastCash;
    private int _lastGross;
    private int _lastSafebox;
    private int _lastSelectedRoom = -1;
    private string _roomStateKey = "";
    private string _drawerStateKey = "";
    private string _contextStateKey = "";
    private float _refreshTimer;
    private float _speed = 1f;
    private int _skipTargetMinute = -1;
    private int _skipDay = -1;
    private Rect _lastSafeArea;
    private Coroutine _toastRoutine;
    private ManagerPhone.Note _displayedNote;
    private WorldHudMode _hudMode = WorldHudMode.Hidden;
    private string _morningStateKey = "";

    private sealed class ContextAction
    {
        public string Label;
        public Action Invoke;
        public bool Enabled;
        public Color Color;
    }

    private sealed class ContextModel
    {
        public string Key;
        public string Title;
        public string Body;
        public readonly List<ContextAction> Actions = new List<ContextAction>();
    }

    private HotelSim Sim =>
        HotelSimSceneBridge.Instance != null ? HotelSimSceneBridge.Instance.Sim : null;

    private static WorldManagementHud ActiveInstance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<WorldManagementHud>();
            return _instance;
        }
    }

    public static bool IsActive
    {
        get
        {
            WorldManagementHud instance = ActiveInstance;
            return instance != null
                   && instance.isActiveAndEnabled
                   && instance._canvas != null;
        }
    }

    public static bool SuppressesWorldImGui
    {
        get
        {
            WorldManagementHud instance = ActiveInstance;
            return IsActive
                   && instance._safeRoot != null
                   && instance._safeRoot.gameObject.activeInHierarchy
                   && instance._drawerOpen;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForWorldScene()
    {
        var bridge = FindFirstObjectByType<HotelSimSceneBridge>();
        if (bridge == null) return;

        var existing = FindFirstObjectByType<WorldManagementHud>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        bridge.gameObject.AddComponent<WorldManagementHud>();
    }

    public static bool ContainsScreenPoint(Vector2 screenPoint)
    {
        WorldManagementHud instance = ActiveInstance;
        if (!IsActive || !instance._safeRoot.gameObject.activeInHierarchy) return false;
        if (Contains(instance._topPanel, screenPoint)) return true;
        if (Contains(instance._bottomNav, screenPoint)) return true;
        if (Contains(instance._alertStrip, screenPoint)) return true;
        if (Contains(instance._contextSheet, screenPoint)) return true;
        if (instance._drawerOpen && Contains(instance._drawer, screenPoint)) return true;
        return instance._roomCard.gameObject.activeInHierarchy
               && Contains(instance._roomCard, screenPoint);
    }

    private static bool Contains(RectTransform rect, Vector2 point) =>
        rect != null
        && rect.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(rect, point, null);

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        EnsureEventSystem();
        BuildUi();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnDisable()
    {
        if (Time.timeScale != 0f) Time.timeScale = 1f;
    }

    private void Update()
    {
        UpdateSafeArea();

        var bridge = HotelSimSceneBridge.Instance;
        WorldHudMode mode = WorldHudVisibilityPolicy.Resolve(
            Sim != null,
            bridge != null && bridge.AwaitingMorningReport);
        if (_safeRoot.gameObject.activeSelf != (mode != WorldHudMode.Hidden))
            _safeRoot.gameObject.SetActive(mode != WorldHudMode.Hidden);
        if (mode == WorldHudMode.Hidden) return;

        ApplyHudMode(mode);

        TickSimulationSpeed();
        if (mode == WorldHudMode.Operations)
            DetectMoneyChanges();

        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = 0.12f;

        if (mode == WorldHudMode.MorningReport)
        {
            RefreshMorningReport();
            return;
        }

        RefreshTop();
        RefreshAlerts();
        RefreshContextSheet();
        RefreshRoomCard();
        RefreshDrawerIfChanged();
    }

    private void ApplyHudMode(WorldHudMode mode)
    {
        if (_hudMode == mode) return;
        _hudMode = mode;

        bool operations = mode == WorldHudMode.Operations;
        _topPanel.gameObject.SetActive(operations);
        _bottomNav.gameObject.SetActive(operations);
        _drawer.gameObject.SetActive(operations && _drawerOpen);
        _roomCard.gameObject.SetActive(false);
        _alertStrip.gameObject.SetActive(false);
        _contextSheet.gameObject.SetActive(false);
        _morningBackdrop.gameObject.SetActive(mode == WorldHudMode.MorningReport);
        _morningReport.gameObject.SetActive(mode == WorldHudMode.MorningReport);

        if (mode == WorldHudMode.MorningReport)
            _morningStateKey = "";
        else if (operations)
            ForceAllRefresh();
    }

    private void TickSimulationSpeed()
    {
        if (_skipTargetMinute < 0)
        {
            if (Time.timeScale != _speed) Time.timeScale = _speed;
            return;
        }

        bool done = Sim == null
                    || Sim.Clock.CurrentDay != _skipDay
                    || Sim.Clock.CurrentMinute >= _skipTargetMinute;
        if (done)
        {
            _skipTargetMinute = -1;
            Time.timeScale = _speed;
            return;
        }

        Time.timeScale = 10f;
    }

    private void DetectMoneyChanges()
    {
        if (Sim == null) return;
        if (!_initializedValues)
        {
            _initializedValues = true;
            _lastCash = Sim.Cash;
            _lastGross = Sim.GrossIncomeToday;
            _lastSafebox = Sim.Safebox.Balance;
            return;
        }

        if (Sim.GrossIncomeToday > _lastGross)
            ShowMoneyBurst("退房入账  +$" + (Sim.GrossIncomeToday - _lastGross), Gold);

        if (Sim.Cash != _lastCash)
        {
            int delta = Sim.Cash - _lastCash;
            ShowMoneyBurst(
                delta > 0 ? "可用现金  +$" + delta : "经营支出  -$" + Mathf.Abs(delta),
                delta > 0 ? Teal : Coral);
            StartCoroutine(BumpCash());
        }

        if (Sim.Safebox.Balance > _lastSafebox)
            ShowMoneyBurst("利润进入保险箱  +$" + (Sim.Safebox.Balance - _lastSafebox), Gold);

        _lastCash = Sim.Cash;
        _lastGross = Sim.GrossIncomeToday;
        _lastSafebox = Sim.Safebox.Balance;
    }

    private void RefreshTop()
    {
        var clock = Sim.Clock;
        var tonight = Sim.OccupancyForNight(clock.CurrentDay);
        string speed = _skipTargetMinute >= 0
            ? "  >>>"
            : (_speed == 1f ? "" : "  x" + _speed.ToString("0.##"));

        _dayTimeLabel.text = L(
            "DAY " + clock.CurrentDay + "  " + clock.TimeFormatted,
            "第 " + clock.CurrentDay + " 天  " + clock.TimeFormatted)
            + "  " + GameText.T(PhaseScheduler.Label(
                PhaseScheduler.PhaseFor(clock.CurrentMinute)))
            + speed;
        _cashLabel.text = L("CASH  $", "现金  $") + Sim.Cash.ToString("N0");
        _earnedLabel.text = L("EARNED TODAY", "今日已赚")
                            + "  <color=#E8BB54>+$"
                            + Sim.GrossIncomeToday.ToString("N0") + "</color>";
        _occupancyLabel.text = L("TONIGHT", "今晚入住")
                               + "  " + tonight.Fraction;

        _safeboxLabel.text = Sim.Safebox.Balance > 0
            ? L("COLLECT PROFIT  +$", "收取利润  +$") + Sim.Safebox.Balance.ToString("N0")
            : L("SAFEBOX PROFIT  $0", "保险箱利润  $0");
        _safeboxFill.rectTransform.anchorMax =
            new Vector2(Mathf.Clamp01(Sim.Safebox.FillRatio), 1f);
        _safeboxButton.interactable = Sim.Safebox.Balance > 0;
        _safeboxButtonImage.color = Sim.Safebox.Balance > 0 ? Gold : Slate;
        _safeboxLabel.color = ContrastText(_safeboxButtonImage.color);
    }

    private void RefreshRoomCard()
    {
        int number = RoomSelection.Selected;
        bool contextOpen = _contextSheet != null && _contextSheet.gameObject.activeSelf;
        bool valid = !_drawerOpen && !contextOpen && number > 0 && Sim.Rooms.Contains(number);
        _roomCard.gameObject.SetActive(valid);
        if (!valid)
        {
            _lastSelectedRoom = number;
            _roomStateKey = "";
            return;
        }

        RoomRecord room = Sim.Rooms.At(number);
        string key = number + "|" + room.state + "|"
                     + Sim.Clearing.ProgressOf(number).ToString("0.00") + "|"
                     + Sim.Renovations.DaysRemainingFor(number) + "|"
                     + Sim.Cash + "|" + Sim.Materials.Stock;
        if (_lastSelectedRoom == number && _roomStateKey == key) return;

        _lastSelectedRoom = number;
        _roomStateKey = key;
        ConfigureRoomCard(room);
    }

    private void ConfigureRoomCard(RoomRecord room)
    {
        int number = room.number;
        int currentRate = room.state == RoomSimState.Ruined
            ? 0
            : Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, room.tier);
        _roomTitle.text = L("ROOM ", "房间 ") + number + "  ·  "
                          + GameText.T(RoomStatePalette.WordOf(room.state));

        if (Sim.Renovations.IsRenovating(number))
        {
            int days = Sim.Renovations.DaysRemainingFor(number);
            _roomHeadline.text = L("WORK IN PROGRESS", "施工进行中");
            _roomDetail.text = L(
                days + " day(s) until handover. Cleaning is required before sale.",
                days + " 天后交付，完工后仍需清洁才能重新出售。");
            SetRoomButton(_roomPrimary, _roomPrimaryLabel, L("GO TO ROOM", "前往房间"),
                () => NavigateToRoom(number), true, Teal);
            SetRoomButton(_roomSecondary, _roomSecondaryLabel, L("VIEW PROJECT", "查看项目"),
                OpenAssetsForSelectedRoom, true, Ink);
            return;
        }

        if (room.state == RoomSimState.Ruined)
        {
            int nightly = Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, RoomTier.Basic);
            _roomHeadline.text = L("OFF MARKET", "尚未营业")
                                 + "  ·  <color=#297A6E>$" + nightly
                                 + L("/night potential", "/晚潜力") + "</color>";
            _roomDetail.text = Sim.Clearing.IsClearing(number)
                ? L("Housekeeping recovery ", "客房部清理进度 ")
                  + Sim.Clearing.ProgressOf(number).ToString("P0") + "."
                : L(
                    "Inspect the room in the hotel. Investment plans stay in Assets.",
                    "先在场景中查看房间；复原成本、停业时间和回本统一放在“资产”。");

            SetRoomButton(_roomPrimary, _roomPrimaryLabel, L("GO TO ROOM", "前往房间"),
                () => NavigateToRoom(number), true, Teal);
            SetRoomButton(_roomSecondary, _roomSecondaryLabel, L("INVESTMENT PLANS", "投资方案"),
                OpenAssetsForSelectedRoom, true, Ink);
            return;
        }

        if (room.state == RoomSimState.Blocked)
        {
            FurnitureInstance broken = FirstBrokenFurniture(number);
            _roomHeadline.text = L("REVENUE STOPPED", "收入已中断")
                                 + "  ·  <color=#C65B46>-$" + currentRate
                                 + L("/sellable night", "/可售夜") + "</color>";
            _roomDetail.text = broken == null
                ? L("This room is blocked. Go to the door to inspect the live problem.",
                    "房间已封闭；前往门口查看现场问题。")
                : L(
                    "A required " + FurnitureCatalog.Get(broken.kindId).name
                    + " is broken. Repair decisions are available from Assets.",
                    "必要家具 " + FurnitureCatalog.Get(broken.kindId).name
                    + " 已故障；维修支出统一在“资产”中处理。");
            SetRoomButton(_roomPrimary, _roomPrimaryLabel, L("GO TO INCIDENT", "前往故障点"),
                () => NavigateToRoom(number), true, Teal);
            SetRoomButton(_roomSecondary, _roomSecondaryLabel, L("VIEW REPAIR", "查看维修"),
                OpenAssetsForSelectedRoom, true, Ink);
            return;
        }

        _roomHeadline.text = "<color=#297A6E>$" + currentRate + "</color> / "
                             + L("night", "晚") + "  ·  "
                             + OperationalRoomHeadline(room.state);
        _roomDetail.text = OperationalRoomDetail(room.state);
        SetRoomButton(_roomPrimary, _roomPrimaryLabel,
            room.state == RoomSimState.Occupied
                ? L("GO TO DOOR", "前往门口")
                : L("GO TO ROOM", "前往房间"),
            () => NavigateToRoom(number),
            true,
            Teal);
        SetRoomButton(_roomSecondary, _roomSecondaryLabel, L("ROOM VALUE", "房间价值"),
            OpenAssetsForSelectedRoom, true, Ink);
    }

    private void RefreshAlerts()
    {
        ManagerPhone phone = ManagerPhone.Instance;
        bool visible = phone != null && phone.Notes.Count > 0 && !_drawerOpen;
        _alertStrip.gameObject.SetActive(visible);
        if (!visible) return;

        _displayedNote = phone.Notes[phone.Notes.Count - 1];
        int count = phone.Notes.Count;
        _alertLabel.text = "<color=#D5A447>"
                           + (_displayedNote.Floor + 1) + "F</color>  "
                           + GameText.T(_displayedNote.Title)
                           + (count > 1 ? L("  ·  +", "  ·  另有 ") + (count - 1) : "");
        _alertLabel.color = Color.Lerp(Cream, _displayedNote.Tint, 0.35f);
        _alertGoLabel.text = L("GO", "前往");
    }

    private void NavigateToDisplayedAlert()
    {
        ManagerPhone.Instance?.NavigateTo(_displayedNote);
        ShowToast(L("Route set to the latest incident.", "已规划前往最新事件的路线。"));
    }

    private void RefreshContextSheet()
    {
        ContextModel model = BuildContextModel();
        bool visible = model != null && !_drawerOpen;
        _contextSheet.gameObject.SetActive(visible);
        if (!visible)
        {
            _contextStateKey = "";
            return;
        }
        if (_contextStateKey == model.Key) return;

        _contextStateKey = model.Key;
        for (int i = _contextContent.childCount - 1; i >= 0; i--)
            Destroy(_contextContent.GetChild(i).gameObject);

        float height = 86f + model.Actions.Count * 42f;
        SetAnchors(_contextSheet, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(8f, 72f), new Vector2(-8f, 72f + Mathf.Min(390f, height)));

        var title = CreateText("Title", _contextContent, 18f, Gold,
            TextAlignmentOptions.Left, true);
        title.text = model.Title;
        title.enableWordWrapping = true;
        title.overflowMode = TextOverflowModes.Overflow;
        var titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 28f;

        var body = CreateText("Body", _contextContent, 12.5f, InkSoft,
            TextAlignmentOptions.TopLeft, false);
        body.text = model.Body;
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Truncate;
        var bodyLayout = body.gameObject.AddComponent<LayoutElement>();
        bodyLayout.preferredHeight = 38f;

        foreach (ContextAction action in model.Actions)
        {
            ContextAction captured = action;
            RectTransform rect;
            TextMeshProUGUI label;
            Button button = CreateButton(
                "Action",
                _contextContent,
                out rect,
                out label,
                captured.Label,
                captured.Color,
                () =>
                {
                    captured.Invoke?.Invoke();
                    _contextStateKey = "";
                    ForceAllRefresh();
                });
            button.interactable = captured.Enabled;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 34f;
        }
    }

    private ContextModel BuildContextModel()
    {
        BreakdownSystem breakdown = FindFirstObjectByType<BreakdownSystem>();
        if (breakdown != null && breakdown.PanelOpen)
        {
            var model = NewContext(
                "breakdown|" + breakdown.PanelTitle + "|" + breakdown.PanelDetail,
                breakdown.PanelTitle,
                breakdown.PanelDetail);
            AddContext(model, L("FIX IT YOURSELF", "经理亲自处理"),
                () => breakdown.ResolvePanel(BreakdownFix.DIY), true, Gold);
            AddContext(model, L("SEND HOUSEKEEPING", "派客房部处理"),
                () => breakdown.ResolvePanel(BreakdownFix.SendStaff),
                breakdown.HasHousekeeper, Teal);
            AddContext(model, L("DUCT TAPE · TEMPORARY", "胶带应急 · 明天复发"),
                () => breakdown.ResolvePanel(BreakdownFix.DuctTape), true, Ink);
            if (breakdown.CanLockPanelRoom)
                AddContext(model, L("CLOSE THE ROOM", "封闭房间"),
                    () => breakdown.ResolvePanel(BreakdownFix.LockRoom), true, Coral);
            return model;
        }

        FireAlarmIncident fire = FindFirstObjectByType<FireAlarmIncident>();
        if (fire != null && fire.PanelOpen)
        {
            var model = NewContext(
                "fire|" + fire.ActiveRoomNumber,
                L("FIRE ALARM · ROOM ", "火警 · 房间 ") + fire.ActiveRoomNumber,
                L("A guest is smoking in bed. The fire department is already writing the $"
                  + FireAlarmIncident.FineAmount + " fine.",
                  "住客在床上吸烟；消防部门已经开出 $" + FireAlarmIncident.FineAmount + " 罚单。"));
            AddContext(model, L("REMOVE THE GUEST", "请住客离店"),
                () => fire.ResolvePanel(true), true, Coral);
            AddContext(model, L("LET IT SLIDE", "暂不追究"),
                () => fire.ResolvePanel(false), true, Ink);
            return model;
        }

        DailyEventInteraction daily = FindFirstObjectByType<DailyEventInteraction>();
        if (daily != null && daily.PanelOpen && daily.HasActiveEvent)
        {
            var model = NewContext(
                "event|" + daily.ActiveEventTitle,
                GameText.T(daily.ActiveEventTitle),
                GameText.T(daily.ActiveEventBlurb));
            for (int i = 0; i < daily.ActiveOptionCount; i++)
            {
                int index = i;
                AddContext(model, GameText.T(daily.ActiveOptionLabel(i)),
                    () => daily.ChooseActiveOption(index), true, i == 0 ? Gold : Ink);
            }
            return model;
        }

        ComplaintInteraction complaint = FindFirstObjectByType<ComplaintInteraction>();
        if (complaint != null && complaint.PanelOpen)
        {
            var model = NewContext(
                "complaint",
                L("ANGRY GUEST AT RECEPTION", "前台有愤怒住客"),
                L("Choose a response. This affects cash, rating and staff morale.",
                  "处理方式会影响现金、评分与员工士气。"));
            AddContext(model, L("COMPENSATE HALF A NIGHT", "赔偿半晚房费"),
                () => complaint.ResolvePanel(ComplaintChoice.Pay), true, Gold);
            AddContext(model, L("REFUSE COMPENSATION", "拒绝赔偿"),
                () => complaint.ResolvePanel(ComplaintChoice.ColdShoulder), true, Ink);
            AddContext(model, L("ESCALATE THE ARGUMENT", "升级争执"),
                () => complaint.ResolvePanel(ComplaintChoice.Fight), true, Coral);
            return model;
        }

        ManagerInteraction staffInteraction = FindFirstObjectByType<ManagerInteraction>();
        if (staffInteraction != null
            && staffInteraction.ActiveHudState != ManagerInteraction.HudState.None)
        {
            StaffAgent agent = staffInteraction.ActiveHudAgent;
            StaffMember member = agent != null ? agent.Member : null;
            switch (staffInteraction.ActiveHudState)
            {
                case ManagerInteraction.HudState.ToiletGuest:
                {
                    var model = NewContext(
                        "toilet_guest",
                        L("PRIVACY COMPLAINT", "厕所隐私投诉"),
                        L("You opened an occupied public-toilet stall. The guest demands an apology.",
                          "你打开了有人使用的公共厕所隔间，住客要求正式道歉。"));
                    AddContext(model, L("APOLOGIZE + $30", "道歉并赔偿 $30"),
                        staffInteraction.ResolveToiletGuestWithCompensation, true, Gold);
                    AddContext(model, L("APOLOGIZE ONLY", "只道歉"),
                        staffInteraction.ResolveToiletGuestWithApology, true, Ink);
                    AddContext(model, L("ARGUE WITH THE GUEST", "与住客争辩"),
                        staffInteraction.ResolveToiletGuestByArguing, true, Coral);
                    return model;
                }
                case ManagerInteraction.HudState.CaughtSlacking:
                {
                    var model = NewContext(
                        "caught|" + member?.DisplayName,
                        L("CAUGHT SLACKING · ", "抓到摸鱼 · ") + member?.DisplayName,
                        RoleName(member != null ? member.Role : StaffRole.Manager)
                        + L(" · morale ", " · 士气 ") + member?.Morale);
                    AddContext(model, L("URGE BACK TO WORK", "督促返岗"),
                        () => staffInteraction.ResolveCaughtStaff(CatchChoice.Urge), true, Teal);
                    AddContext(model, L("SCOLD HARD", "严厉训斥"),
                        () => staffInteraction.ResolveCaughtStaff(CatchChoice.Scold), true, Coral);
                    AddContext(model, L("LOOK AWAY", "装作没看见"),
                        () => staffInteraction.ResolveCaughtStaff(CatchChoice.Ignore), true, Ink);
                    return model;
                }
                case ManagerInteraction.HudState.Staff:
                {
                    var model = NewContext(
                        "staff|" + member?.DisplayName + "|" + agent?.ActivityState,
                        (member?.DisplayName ?? L("EMPLOYEE", "员工"))
                        + " · " + RoleName(member != null ? member.Role : StaffRole.Manager),
                        (agent != null ? agent.ShiftState + " · " + agent.ActivityState : "")
                        + L(" · morale ", " · 士气 ") + member?.Morale);
                    AddContext(model, L("HURRY UP", "催促加快"),
                        staffInteraction.HurryActiveStaff, true, Teal);
                    AddContext(model, L("INTERROGATE DELAY", "质询延误"),
                        staffInteraction.InterrogateActiveStaff,
                        staffInteraction.ActiveStaffCanBeInterrogated, Gold);
                    AddContext(model, L("ASSIGN A ROOM", "指定房间"),
                        staffInteraction.BeginAssignActiveStaff, true, Ink);
                    AddContext(model, L("FIRE EMPLOYEE", "解雇员工"),
                        staffInteraction.FireActiveStaff, true, Coral);
                    AddContext(model, L("CLOSE", "关闭"),
                        staffInteraction.CloseActiveStaff, true, Slate);
                    return model;
                }
                case ManagerInteraction.HudState.AssignRoom:
                {
                    IReadOnlyList<Room2DEntity> candidates = staffInteraction.CommandRoomCandidates;
                    var model = NewContext(
                        "assign|" + member?.DisplayName + "|" + candidates.Count,
                        L("ASSIGN ROOM · ", "指定房间 · ") + member?.DisplayName,
                        candidates.Count > 0
                            ? L("Choose from the live queue. You can also tap another valid room in the hotel.",
                                "从实时任务列表中选择；也可以直接点击场景中的其他有效房间。")
                            : L("No rooms currently need this role.", "当前没有房间需要该岗位。"));
                    int visibleCount = Mathf.Min(6, candidates.Count);
                    for (int i = 0; i < visibleCount; i++)
                    {
                        Room2DEntity room = candidates[i];
                        int floor = FloorMath.FloorIndexForY(room.transform.position.y) + 1;
                        AddContext(model,
                            L("ROOM ", "房间 ") + room.roomNumber + " · " + floor + "F · "
                            + room.GetStateDisplayName(),
                            () => staffInteraction.AssignCommandRoom(room), true, Teal);
                    }
                    AddContext(model, L("CANCEL", "取消"),
                        staffInteraction.CancelCommandRoom, true, Ink);
                    return model;
                }
                case ManagerInteraction.HudState.RoomFlaw:
                {
                    var model = NewContext(
                        "room_flaw",
                        L("INSPECTION FLAW FOUND", "验房发现问题"),
                        L("Return the nearby room to housekeeping before it reaches a guest.",
                          "在住客入住前，把附近房间退回客房部重新清洁。"));
                    AddContext(model, L("RETURN TO CLEANING", "退回清洁"),
                        staffInteraction.SendNearbyFlawedRoomBack, true, Coral);
                    return model;
                }
            }
        }

        ElevatorController elevator = ElevatorController.Instance;
        if (elevator != null && elevator.PanelOpen)
        {
            var model = NewContext(
                "elevator|" + elevator.CurrentFloor,
                L("ELEVATOR", "电梯"),
                L("Choose a floor. Locked facilities show their opening cost.",
                  "选择楼层；尚未开放的设施会显示开业成本。"));
            for (int f = FloorMath.FloorCount - 1; f >= 0; f--)
            {
                int floor = f;
                bool accessible = FacilitySystem.FloorAccessible(floor);
                string label = (floor + 1) + "F  " + FloorMath.FloorNames[floor];
                if (!accessible) label += L(" · OPEN $", " · 开放 $") + FacilityUnlockCost(floor);
                else if (floor == elevator.CurrentFloor) label += L(" · HERE", " · 当前");
                AddContext(model, label, () =>
                {
                    if (!elevator.SelectFloor(floor, out string message)
                        && !string.IsNullOrEmpty(message))
                        ShowToast(GameText.T(message));
                }, floor != elevator.CurrentFloor, accessible ? Teal : Gold);
            }
            return model;
        }

        RoomDoor door = RoomDoor.ActivePrompt;
        if (door != null && door.Room != null)
        {
            var model = NewContext(
                "door|" + door.Room.roomNumber + "|" + door.Room.currentState,
                L("ROOM ", "房间 ") + door.Room.roomNumber,
                GameText.T(door.StateHint)
                + (door.Room.currentState == Room2DState.Occupied
                    ? L(" Entry is optional; incidents can be handled from the corridor.",
                        " 无需为处理故障强行进入，门外即可维修。")
                    : ""));
            AddContext(model, L("SWIPE KEY & ENTER", "刷卡进入"),
                door.ConfirmEntry, true,
                door.Room.currentState == Room2DState.Occupied ? Coral : Teal);
            AddContext(model, L("CANCEL", "取消"),
                door.DeclineEntry, true, Ink);
            return model;
        }

        return null;
    }

    private static ContextModel NewContext(string key, string title, string body)
    {
        return new ContextModel { Key = key, Title = title, Body = body };
    }

    private static void AddContext(
        ContextModel model,
        string label,
        Action action,
        bool enabled,
        Color color)
    {
        model.Actions.Add(new ContextAction
        {
            Label = label,
            Invoke = action,
            Enabled = enabled,
            Color = color,
        });
    }

    private static int FacilityUnlockCost(int floor)
    {
        if (floor == FacilitySystem.GymFloor) return FacilitySystem.GymCost;
        if (floor == FacilitySystem.CasinoFloor) return FacilitySystem.CasinoCost;
        return FacilitySystem.PoolCost;
    }

    private void RefreshDrawerIfChanged()
    {
        if (!_drawerOpen) return;
        string key = BuildDrawerStateKey();
        if (_drawerStateKey == key) return;
        _drawerStateKey = key;
        RebuildDrawer();
    }

    private string BuildDrawerStateKey()
    {
        if (Sim == null) return "";
        string common = _tab + "|" + Sim.Clock.CurrentDay + "|" + Sim.Cash + "|"
                        + Sim.GrossIncomeToday + "|" + Sim.Safebox.Balance + "|"
                        + Sim.Materials.Stock + "|" + RoomSelection.Selected;
        switch (_tab)
        {
            case DeskTab.Pricing:
                return common + "|" + Sim.Pricing.DefaultTemplate;
            case DeskTab.Staff:
                return common + "|" + Sim.Staff.OnDutyCount + "|"
                       + Sim.Shifts.TierOf(StaffRole.Reception) + "|"
                       + Sim.Shifts.TierOf(StaffRole.Housekeeper) + "|"
                       + Sim.Shifts.TierOf(StaffRole.Inspector);
            case DeskTab.Assets:
                return common + "|" + Sim.Rooms.SellableCount + "|"
                       + Sim.Rooms.CountOf(RoomSimState.Ruined) + "|"
                       + Sim.Rooms.CountOf(RoomSimState.Blocked) + "|"
                       + Sim.Renovations.RoomsUnderRenovation;
            default:
                return common + "|" + Sim.Rooms.DirtyBacklog + "|"
                       + Sim.DeskQueueLength + "|"
                       + Sim.OccupancyForNight(Sim.Clock.CurrentDay).Fraction;
        }
    }

    private void RebuildDrawer()
    {
        for (int i = _drawerContent.childCount - 1; i >= 0; i--)
            Destroy(_drawerContent.GetChild(i).gameObject);

        switch (_tab)
        {
            case DeskTab.Pricing:
                _drawerTitle.text = L("ROOM RATES", "房价策略");
                BuildPricingDesk();
                break;
            case DeskTab.Staff:
                _drawerTitle.text = L("TEAM & CAPACITY", "员工与产能");
                BuildStaffDesk();
                break;
            case DeskTab.Assets:
                _drawerTitle.text = L("ASSETS & INVESTMENT", "资产与投资");
                BuildAssetsDesk();
                break;
            default:
                _drawerTitle.text = L("HOTEL PULSE", "经营驾驶舱");
                BuildOverviewDesk();
                break;
        }

        UpdateNavState();
        Canvas.ForceUpdateCanvases();
        var scroll = _drawer.GetComponentInChildren<ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    private void BuildOverviewDesk()
    {
        int gross = Sim.GrossIncomeToday;
        int commission = Sim.CommissionToday;
        int wages = Sim.Shifts.DailyWageCost(Sim.Staff, Sim.Clock.CurrentDay);
        int runningMargin = gross - commission - wages;
        AddSection(L("TODAY'S MONEY", "今天的钱在哪里"),
            L("Checkout income becomes profit only after operating costs.",
              "退房收入扣除佣金和运营成本后，才会成为可收取利润。"));
        AddInfoCard(
            L("CURRENT CASHFLOW", "当前现金流"),
            L("Room income  +$", "客房收入  +$") + gross
            + "\n" + L("Channel fees  -$", "平台佣金  -$") + commission
            + "\n" + L("Scheduled wages  -$", "预计工资  -$") + wages
            + "\n" + L("Running result  ", "当前结果  ")
            + (runningMargin >= 0 ? "+$" : "-$") + Mathf.Abs(runningMargin),
            runningMargin >= 0 ? Teal : Coral,
            122f);

        var tonight = Sim.OccupancyForNight(Sim.Clock.CurrentDay);
        AddInfoCard(
            L("TONIGHT " + tonight.Fraction, "今晚入住 " + tonight.Fraction),
            L("Still to sell ", "仍可出售 ") + tonight.Left
            + L(" · clean now ", " · 当前干净 ") + Sim.Rooms.SellableCount
            + L("\nDirty ", "\n待清洁 ") + Sim.Rooms.DirtyBacklog
            + L(" · broken ", " · 故障封房 ") + Sim.Rooms.CountOf(RoomSimState.Blocked)
            + L(" · derelict ", " · 破败待解锁 ") + Sim.Rooms.CountOf(RoomSimState.Ruined),
            Gold,
            90f);

        if (Sim.Safebox.Balance > 0)
            AddActionButton(
                L("COLLECT PROFIT  +$", "收取利润  +$") + Sim.Safebox.Balance,
                CollectSafebox,
                Gold,
                true);

        AddSection(L("TIME CONTROL", "时间控制"),
            L("Speed changes the whole hotel, including staff movement.",
              "倍速会同时推进经济、客人和员工行动。"));
        AddSpeedButtons();
        AddActionButton(
            _skipTargetMinute >= 0 ? L("FAST-FORWARDING...", "正在快进...")
                : L("SKIP TO NEXT PHASE", "跳到下一阶段"),
            SkipToNextPhase,
            Ink,
            _skipTargetMinute < 0);
    }

    private void BuildPricingDesk()
    {
        AddSection(L("PRICE IS A PROMISE", "房价也是承诺"),
            L("Higher rates increase margin and guest expectations. The room still needs to deliver.",
              "价格越高，利润与客人期待同时上升；房间品质必须兑现承诺。"));

        foreach (PriceTemplate template in Enum.GetValues(typeof(PriceTemplate)))
        {
            PriceTemplate captured = template;
            bool active = Sim.Pricing.DefaultTemplate == template;
            int oldRate = RateForTemplate(template, RoomTier.Old);
            int basicRate = RateForTemplate(template, RoomTier.Basic);
            int betterRate = RateForTemplate(template, RoomTier.Better);
            string label = PriceTemplateName(template)
                           + (active ? L("  · ACTIVE", "  · 当前") : "");
            string detail = PriceTemplateConsequence(template)
                            + "\nOLD $" + oldRate + "   BASIC $" + basicRate
                            + "   BETTER $" + betterRate;
            AddPlanButton(label, detail, () =>
            {
                Sim.Pricing.DefaultTemplate = captured;
                ShowToast(L("New rate strategy applied.", "新的房价策略已生效。"));
                ForceDrawerRefresh();
            }, active ? Gold : Ink, true);
        }

        AddSection(L("THE WEEK AHEAD", "未来七晚"),
            L("Sold rooms / available rooms", "已售房间 / 可售容量"));
        foreach (NightOccupancy night in Sim.OccupancyCalendar(7))
        {
            AddInfoCard(
                night.isTonight ? L("TONIGHT", "今晚") : L("DAY ", "第 ") + night.day,
                night.Fraction + "   "
                + L("Old room $", "旧房 $") + Sim.Pricing.PriceFor(night.day, RoomTier.Old),
                night.IsFull ? Coral : (night.sold > 0 ? Gold : Slate),
                64f);
        }
    }

    private void BuildStaffDesk()
    {
        int wages = Sim.Shifts.DailyWageCost(Sim.Staff, Sim.Clock.CurrentDay);
        AddSection(L("SERVICE CAPACITY", "服务产能"),
            L("Saving wages reduces throughput. Bottlenecks directly turn rooms and guests into lost money.",
              "省工资会降低产能；前台排队和脏房积压都会直接损失收入。"));
        AddInfoCard(
            L("TODAY'S TEAM", "今日团队"),
            L("On duty ", "在岗 ") + Sim.Staff.OnDutyCount + "/" + Sim.Staff.Count
            + L(" · wages $", " · 工资 $") + wages
            + "\n" + L("Check-ins ", "入住办理 ")
            + Sim.CurrentCheckInsPerHour.ToString("0.0") + "/h"
            + L(" · cleaning ", " · 清洁 ")
            + ServiceCapacityModel.CleanRoomsPerHour(Sim.Staff, 1f).ToString("0.0") + "/h",
            Teal,
            86f);

        var rosterLines = new List<string>();
        foreach (StaffSimEntry entry in Sim.Staff.Entries)
        {
            if (entry.member == null) continue;
            rosterLines.Add(
                entry.member.DisplayName + " · " + RoleName(entry.member.Role)
                + " · " + StaffStateName(entry.state)
                + L(" · $", " · $") + entry.member.DailyWage + L("/day", "/天"));
        }
        AddInfoCard(
            L("PEOPLE ON SHIFT", "人员明细"),
            rosterLines.Count > 0
                ? string.Join("\n", rosterLines)
                : L("No employees hired.", "尚未雇佣员工。"),
            Slate,
            Mathf.Max(70f, 36f + rosterLines.Count * 24f));

        foreach (StaffRole role in new[]
                 {
                     StaffRole.Reception,
                     StaffRole.Housekeeper,
                     StaffRole.Inspector
                 })
        {
            StaffRole captured = role;
            ShiftTier tier = Sim.Shifts.TierOf(role);
            int total = CountStaff(role);
            int productive = Sim.Staff.ProductiveCountOfRole(role);
            AddPlanButton(
                RoleName(role) + "  " + productive + "/" + total,
                L("Shift: ", "排班：") + GameText.T(ShiftPlan.LabelOf(tier))
                + "\n" + RoleConsequence(role),
                () =>
                {
                    ShiftTier current = Sim.Shifts.TierOf(captured);
                    ShiftTier next = current == ShiftTier.Full
                        ? ShiftTier.Skeleton
                        : (ShiftTier)((int)current + 1);
                    Sim.Shifts.SetTier(captured, next);
                    ShowToast(L("Shift plan updated for tomorrow.", "排班已更新，将在明天生效。"));
                    ForceDrawerRefresh();
                },
                Ink,
                true);
        }

        HiringInteraction hiring = FindFirstObjectByType<HiringInteraction>();
        if (hiring == null) return;

        AddSection(L("RECRUITMENT", "今日招聘"),
            L("Signing fee equals two days of wages. New hires join immediately.",
              "签约费为两天工资；成功招聘后员工会立即到岗。"));
        IReadOnlyList<StaffMember> candidates = hiring.Candidates;
        if (candidates.Count == 0)
        {
            AddInfoCard(
                L("NO CANDIDATES LEFT", "今日候选人已招完"),
                L("The board refreshes tomorrow.", "招聘栏会在明天刷新。"),
                Slate,
                62f);
            return;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            StaffMember captured = candidates[i];
            int fee = hiring.SigningCostFor(captured);
            string details = RoleName(captured.Role)
                             + L(" · wage $", " · 日薪 $") + captured.DailyWage
                             + L("/day", "/天")
                             + "\nSPD " + captured.Attributes.Speed
                             + "   QLT " + captured.Attributes.Quality
                             + "   STA " + captured.Attributes.Stamina;
            AddPlanButton(
                captured.DisplayName + L("  · HIRE -$", "  · 招聘 -$") + fee,
                details,
                () =>
                {
                    hiring.HireCandidate(captured);
                    ShowToast(string.IsNullOrEmpty(hiring.LatestStory)
                        ? L("Recruitment decision recorded.", "招聘结果已处理。")
                        : hiring.LatestStory);
                    ForceDrawerRefresh();
                },
                Gold,
                Sim.Cash >= fee);
        }
    }

    private void BuildAssetsDesk()
    {
        AddSection(L("CAPITAL ALLOCATION", "资金去向"),
            L("Cash is spendable now. Safebox profit must be collected before it can fund work.",
              "现金可立即投资；保险箱中的利润必须先收取，才能用于施工和还债。"));
        string debt = "";
        EconomySystem economy = FindFirstObjectByType<EconomySystem>();
        if (economy != null && economy.Loan != null)
            debt = L("\nDebt $", "\n贷款余额 $") + economy.Loan.Balance.ToString("N0");
        AddInfoCard(
            L("AVAILABLE CAPITAL", "可用资本"),
            L("Cash $", "现金 $") + Sim.Cash
            + L(" · safebox $", " · 保险箱 $") + Sim.Safebox.Balance
            + L("\nMaterials ", "\n材料 ") + Sim.Materials.Stock + "/"
            + (Sim.Warehouse.HasLimit ? Sim.Warehouse.Capacity.ToString() : "-")
            + debt,
            Gold,
            92f);

        if (Sim.Safebox.Balance > 0)
            AddActionButton(
                L("COLLECT SAFEBOX  +$", "收取保险箱  +$") + Sim.Safebox.Balance,
                CollectSafebox,
                Gold,
                true);

        int materialPrice = Sim.Materials.PriceFor(10);
        AddActionButton(
            L("BUY 10 MATERIALS  -$", "购买 10 材料  -$") + materialPrice,
            BuyMaterials,
            Ink,
            Sim.MaterialsThatFit(10) >= 10 && Sim.Cash >= materialPrice);

        AddInfoCard(
            L("ROOM PORTFOLIO", "客房资产"),
            L("Selling ", "营业中 ") + Sim.Rooms.SellableCount
            + L(" · building ", " · 施工中 ") + Sim.Renovations.RoomsUnderRenovation
            + L("\nDerelict ", "\n破败待解锁 ") + Sim.Rooms.CountOf(RoomSimState.Ruined)
            + L(" · broken ", " · 故障封房 ") + Sim.Rooms.CountOf(RoomSimState.Blocked),
            Teal,
            82f);

        int selected = RoomSelection.Selected;
        if (selected <= 0 || !Sim.Rooms.Contains(selected))
        {
            AddInfoCard(
                L("SELECT A ROOM IN THE HOTEL", "请先返回场景选择房间"),
                L("Its cost, downtime, nightly gain and payback will appear here.",
                  "选中房间后点“比较方案”，即可查看施工成本、停业时间、增收与回本夜数。"),
                Slate,
                80f);
            AddActionButton(
                L("BACK TO HOTEL", "返回场景选房"),
                () => SetDrawerOpen(false),
                Teal,
                true);
        }
        else
        {
            BuildSelectedRoomPlans(Sim.Rooms.At(selected));
        }

        if (economy != null && economy.Loan != null && economy.Loan.Balance > 0)
        {
            int suggested = DebtPolicy.ScheduledRepaymentFor(
                economy.Loan.Balance,
                Sim.LastSettlement.netToSafebox,
                Sim.Cash + Sim.Safebox.Balance,
                150);
            AddActionButton(
                L("PAY BANK  -$", "偿还贷款  -$") + suggested,
                () => PayBank(suggested),
                Coral,
                suggested > 0 && Sim.Cash > 0);
        }

        AddSection(L("SAVE SLOTS", "存档槽位"),
            L("Saving changes the active autosave slot. Loading rebuilds the scene cleanly.",
              "保存后自动存档会跟随该槽位；读取会完整重载场景，避免残留角色和任务。"));
        for (int slot = 1; slot <= SaveSlots.Count; slot++)
        {
            int capturedSlot = slot;
            SaveSlotSummary summary = SaveService.SummaryOf(slot);
            string slotName = L("SLOT ", "槽位 ") + slot;
            string summaryText = summary.exists
                ? L("Day ", "第 ") + summary.day + L(" · cash $", " 天 · 现金 $")
                  + summary.cash + L(" · rating ", " · 评分 ") + summary.stars.ToString("0.0")
                : L("Empty", "空槽位");
            AddPlanButton(
                slotName + (SaveSlots.ActiveSlot == slot ? L(" · ACTIVE", " · 当前") : ""),
                summaryText + L("\nTap to save or overwrite this slot.",
                                "\n点击保存或覆盖此槽位。"),
                () => SaveIntoSlot(capturedSlot),
                SaveSlots.ActiveSlot == slot ? Gold : Ink,
                true);
            if (summary.exists)
                AddActionButton(
                    L("LOAD SLOT ", "读取槽位 ") + slot,
                    () => LoadFromSlot(capturedSlot),
                    Slate,
                    true);
        }
    }

    private void BuildSelectedRoomPlans(RoomRecord room)
    {
        AddSection(
            L("ROOM ", "房间 ") + room.number + "  ·  "
            + GameText.T(RoomStatePalette.WordOf(room.state)),
            L("Compare total investment with revenue gained per sold night.",
              "以下按“现金 + 材料市价”计算投入，并用每个售出夜的增收计算回本。"));

        if (Sim.Renovations.IsRenovating(room.number))
        {
            AddInfoCard(
                L("WORK IN PROGRESS", "施工进行中"),
                Sim.Renovations.DaysRemainingFor(room.number)
                + L(" day(s) remaining.", " 天后完工。"),
                Gold,
                64f);
            return;
        }

        if (room.state == RoomSimState.Ruined)
        {
            AddPlanButton(
                L("FREE CLEARING", "免费清理"),
                L("No cash · housekeeping trips · opens as Old at $",
                  "不花现金 · 占用客房部往返 · 完成后旧房价 $")
                + Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, RoomTier.Old)
                + L("/night", "/晚"),
                () => StartFreeClearing(room.number),
                Teal,
                !Sim.Clearing.IsClearing(room.number));

            foreach (ReclaimPlanKind kind in Enum.GetValues(typeof(ReclaimPlanKind)))
            {
                ReclaimPlanKind captured = kind;
                ReclaimPlan plan = ReclaimPlan.For(kind);
                RoomTier target = ReclaimTarget(kind);
                int cash = Sim.QuoteReclaim(kind, 1);
                int total = RoomInvestmentMath.TotalInvestment(
                    cash, plan.materialsPerRoom, Sim.Materials.UnitPrice);
                int rate = Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, target);
                int payback = RoomInvestmentMath.SoldNightsToPayback(total, rate);
                AddPlanButton(
                    ReclaimName(kind) + "  $" + cash,
                    plan.materialsPerRoom + L(" materials · ", " 材料 · ")
                    + plan.blockDays + L(" days · $", " 天 · $") + rate
                    + L("/night · payback ", "/晚 · 回本 ") + payback
                    + L(" sold nights", " 个售出夜"),
                    () => StartReclaim(room.number, captured),
                    kind == ReclaimPlanKind.Refit ? Gold : Ink,
                    Sim.Cash >= cash && Sim.Materials.Stock >= plan.materialsPerRoom);
            }
            return;
        }

        int current = Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, room.tier);
        foreach (RenovationPlanKind kind in Enum.GetValues(typeof(RenovationPlanKind)))
        {
            RenovationPlanKind captured = kind;
            RenovationPlan plan = RenovationPlan.For(kind);
            int cash = Sim.QuoteRenovation(kind, 1);
            int target = Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, plan.targetTier);
            int gain = Mathf.Max(0, target - current);
            int total = RoomInvestmentMath.TotalInvestment(
                cash, plan.materialsPerRoom, Sim.Materials.UnitPrice);
            int payback = RoomInvestmentMath.SoldNightsToPayback(total, gain);
            AddPlanButton(
                RenovationName(kind) + "  $" + cash,
                plan.materialsPerRoom + L(" materials · ", " 材料 · ")
                + plan.blockDays + L(" days closed · +$", " 天停业 · 每晚 +$")
                + gain + L("/night · payback ", " · 回本 ") + payback
                + L(" sold nights", " 个售出夜"),
                () => StartRenovation(room.number, captured),
                kind == RenovationPlanKind.Economy ? Gold : Ink,
                room.state != RoomSimState.Occupied
                && gain > 0
                && Sim.Cash >= cash
                && Sim.Materials.Stock >= plan.materialsPerRoom);
        }
    }

    private void StartFreeClearing(int roomNumber)
    {
        bool ok = Sim.TryStartJunkClearing(roomNumber, out string reason);
        ShowToast(ok
            ? L("Housekeeping started clearing Room ", "客房部开始清理房间 ") + roomNumber
            : GameText.T(reason));
        ForceAllRefresh();
    }

    private void StartReclaim(int roomNumber, ReclaimPlanKind kind)
    {
        bool ok = Sim.TryStartReclaim(kind, new List<int> { roomNumber }, out string reason);
        ShowToast(ok
            ? L("Room restoration started.", "房间复原工程已开工。")
            : GameText.T(reason));
        ForceAllRefresh();
    }

    private void StartRenovation(int roomNumber, RenovationPlanKind kind)
    {
        bool ok = Sim.TryStartRenovation(kind, new List<int> { roomNumber }, out string reason);
        ShowToast(ok
            ? L("Renovation started. The room is now off sale.", "装修已开工，房间已停止销售。")
            : GameText.T(reason));
        ForceAllRefresh();
    }

    private void RepairBroken(int roomNumber)
    {
        FurnitureInstance broken = FirstBrokenFurniture(roomNumber);
        string reason = "";
        bool ok = broken != null && Sim.TryRepairFurniture(broken.instanceId, out reason);
        ShowToast(ok
            ? L("Repair ordered.", "维修已安排。")
            : broken == null ? L("No broken furniture found.", "没有找到故障家具。")
                : GameText.T(reason));
        ForceAllRefresh();
    }

    private void TapeBroken(int roomNumber)
    {
        FurnitureInstance broken = FirstBrokenFurniture(roomNumber);
        string reason = "";
        bool ok = broken != null && Sim.TryTapeFurniture(broken.instanceId, out reason);
        ShowToast(ok
            ? L("Room reopened with a temporary fix.", "应急修补完成，房间暂时恢复营业。")
            : broken == null ? L("No broken furniture found.", "没有找到故障家具。")
                : GameText.T(reason));
        ForceAllRefresh();
    }

    private FurnitureInstance FirstBrokenFurniture(int roomNumber)
    {
        foreach (FurnitureInstance item in Sim.Furniture.InRoom(roomNumber))
            if (item.IsFaulted) return item;
        return null;
    }

    private void CollectSafebox()
    {
        int amount = Sim.CollectSafebox();
        ShowToast(amount > 0
            ? L("Profit collected  +$", "利润已收取  +$") + amount
            : L("The safebox is empty.", "保险箱目前是空的。"));
        ForceAllRefresh();
    }

    private void BuyMaterials()
    {
        bool ok = Sim.TryBuyMaterials(10);
        ShowToast(ok
            ? L("10 materials delivered.", "10 份材料已入库。")
            : L("Not enough cash or warehouse space.", "现金不足或仓库空间不够。"));
        ForceAllRefresh();
    }

    private void PayBank(int amount)
    {
        EconomySystem economy = FindFirstObjectByType<EconomySystem>();
        if (economy == null || economy.Loan == null)
        {
            ShowToast(L("Loan account unavailable.", "贷款账户不可用。"));
            return;
        }

        int paid = Sim.PayLoanInstallment(amount);
        if (paid > 0) economy.RepayLoan(paid);
        ShowToast(paid > 0
            ? L("Paid the bank  -$", "已偿还贷款  -$") + paid
            : L("No cash available for repayment.", "当前没有可用于还款的现金。"));
        ForceAllRefresh();
    }

    private void NavigateToRoom(int roomNumber)
    {
        Room2DEntity entity = FindRoomEntity(roomNumber);
        ManagerController manager = FindFirstObjectByType<ManagerController>();
        if (entity == null || manager == null)
        {
            ShowToast(L("Room route is unavailable.", "暂时无法规划到该房间。"));
            return;
        }

        Vector3 target = RoomDoor.ExteriorPointFor(entity);
        manager.MoveTo(target);
        ShowToast(L("Walking to Room ", "正在前往房间 ") + roomNumber + ".");
        RoomSelection.Clear();
    }

    private static Room2DEntity FindRoomEntity(int roomNumber)
    {
        Room2DPrototypeDemandLoop loop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (loop == null || loop.rooms == null) return null;
        foreach (Room2DEntity room in loop.rooms)
            if (room != null && room.roomNumber == roomNumber) return room;
        return null;
    }

    private static string OperationalRoomHeadline(RoomSimState state)
    {
        switch (state)
        {
            case RoomSimState.Occupied: return L("GUEST IN HOUSE", "住客入住中");
            case RoomSimState.Dirty: return L("TURNOVER REQUIRED", "等待清洁");
            case RoomSimState.Cleaning: return L("HOUSEKEEPING ACTIVE", "清洁进行中");
            case RoomSimState.AwaitingInspection: return L("AWAITING INSPECTION", "等待验房");
            default: return L("READY TO SELL", "可立即出售");
        }
    }

    private static string OperationalRoomDetail(RoomSimState state)
    {
        switch (state)
        {
            case RoomSimState.Occupied:
                return L(
                    "The room is earning tonight. Entering may upset the guest.",
                    "该房今晚正在产生收入；擅自进入可能激怒住客。");
            case RoomSimState.Dirty:
                return L(
                    "Every minute dirty is lost selling time. Housekeeping assigns automatically.",
                    "脏房每多停留一分钟都在损失可售时间；客房部会自动接活。");
            case RoomSimState.Cleaning:
                return L(
                    "Housekeeping is turning this room over.",
                    "客房部正在完成退房清洁。");
            case RoomSimState.AwaitingInspection:
                return L(
                    "An inspector must release this room before it can be sold.",
                    "验房员确认后，房间才能重新出售。");
            default:
                return L(
                    "Clean, inspected and available for the next guest.",
                    "房间已清洁并通过验房，可接待下一位住客。");
        }
    }

    private void SaveIntoSlot(int slot)
    {
        SaveCoordinator coordinator = FindFirstObjectByType<SaveCoordinator>();
        if (coordinator == null)
        {
            ShowToast(L("Save system unavailable.", "存档系统不可用。"));
            return;
        }

        SaveService.SaveToSlot(slot, coordinator.Capture());
        SaveSlots.SetActiveSlot(slot);
        ShowToast(L("Saved into slot ", "已保存至槽位 ") + slot + ".");
        ForceDrawerRefresh();
    }

    private void LoadFromSlot(int slot)
    {
        if (!SaveSlots.Exists(slot))
        {
            ShowToast(L("That slot is empty.", "该槽位为空。"));
            return;
        }

        SaveSlots.RequestLoad(slot);
        Time.timeScale = 1f;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            scene.buildIndex >= 0 ? scene.name : scene.path);
    }

    private void SkipToNextPhase()
    {
        if (_skipTargetMinute >= 0 || Sim == null) return;
        if (!PhaseScheduler.CanSkip(Sim.Clock.CurrentMinute, 0, out string reason))
        {
            ShowToast(GameText.T(reason));
            return;
        }

        _skipTargetMinute = PhaseScheduler.NextKeyMinuteAfter(Sim.Clock.CurrentMinute);
        _skipDay = Sim.Clock.CurrentDay;
        ForceDrawerRefresh();
    }

    private void SetSpeed(float speed)
    {
        _speed = speed;
        if (_skipTargetMinute < 0) Time.timeScale = speed;
        ForceDrawerRefresh();
    }

    private void OpenAssetsForSelectedRoom()
    {
        _tab = DeskTab.Assets;
        SetDrawerOpen(true);
    }

    private void SetDrawerOpen(bool open)
    {
        _drawerOpen = open;
        _drawer.gameObject.SetActive(open);
        if (open)
        {
            _contextSheet.gameObject.SetActive(false);
            _alertStrip.gameObject.SetActive(false);
        }
        _roomCard.gameObject.SetActive(!open
                                       && !_contextSheet.gameObject.activeSelf
                                       && RoomSelection.Selected > 0
                                       && Sim != null
                                       && Sim.Rooms.Contains(RoomSelection.Selected));
        if (open)
        {
            _drawerStateKey = "";
            RefreshDrawerIfChanged();
        }
        UpdateNavState();
    }

    private void SelectTab(DeskTab tab)
    {
        if (_drawerOpen && _tab == tab)
        {
            SetDrawerOpen(false);
            return;
        }

        _tab = tab;
        SetDrawerOpen(true);
    }

    private void UpdateNavState()
    {
        for (int i = 0; i < _navButtons.Count; i++)
        {
            bool active = _drawerOpen && i == (int)_tab;
            _navButtons[i].GetComponent<Image>().color = active ? Gold : Paper;
            _navLabels[i].color = active ? Ink : Cream;
        }
    }

    private void ForceAllRefresh()
    {
        _roomStateKey = "";
        _drawerStateKey = "";
        _contextStateKey = "";
        _morningStateKey = "";
        _refreshTimer = 0f;
    }

    private void ForceDrawerRefresh()
    {
        _drawerStateKey = "";
        _refreshTimer = 0f;
    }

    private void RefreshMorningReport()
    {
        if (Sim == null) return;

        EconomySystem economy = FindFirstObjectByType<EconomySystem>();
        int loanBalance = economy != null && economy.Loan != null ? economy.Loan.Balance : 0;
        int firstRefund = Sim.PendingRefunds.Count > 0 ? Sim.PendingRefunds[0].requestId : 0;
        string key = string.Join("|",
            Sim.Clock.CurrentDay,
            Sim.Cash,
            Sim.Safebox.Balance,
            Sim.Overflow.Balance,
            Sim.Payroll.Owed,
            Sim.Payroll.Cycle,
            Sim.PendingRefunds.Count,
            firstRefund,
            loanBalance,
            Sim.Credit.PaidToday);
        if (_morningStateKey == key) return;
        _morningStateKey = key;

        for (int i = _morningContent.childCount - 1; i >= 0; i--)
            Destroy(_morningContent.GetChild(i).gameObject);

        int closedDay = Mathf.Max(1, Sim.Clock.CurrentDay - 1);
        _morningTitle.text = L("YESTERDAY'S LEDGER", "昨日账本");
        _morningSubtitle.text = L(
            "DAY " + closedDay + " CLOSED  ·  DAY " + Sim.Clock.CurrentDay + " READY",
            "第 " + closedDay + " 天已结算  ·  第 " + Sim.Clock.CurrentDay + " 天等待开门");

        DaySettlementResult last = Sim.LastSettlement;
        int net = last.NetProfit;
        AddMorningInfo(
            net >= 0
                ? L("THE HOTEL MADE +$", "酒店昨日赚了 +$") + net
                : L("THE HOTEL LOST -$", "酒店昨日亏了 -$") + Mathf.Abs(net),
            L("Room income  +$", "客房收入  +$") + Sim.GrossIncomeToday
            + "\n" + L("Channel fees  -$", "平台佣金  -$") + Sim.CommissionToday
            + "\n" + L("Profit into safebox  +$", "进入保险箱的利润  +$") + last.netToSafebox
            + (last.cashPaidFromReserve > 0
                ? "\n" + L("Cash used to cover loss  -$", "现金垫付亏损  -$")
                  + last.cashPaidFromReserve
                : ""),
            net >= 0 ? Teal : Coral,
            last.cashPaidFromReserve > 0 ? 118f : 96f);

        AddMorningInfo(
            L("OPERATIONS", "昨日经营"),
            L("Checked in ", "办理入住 ") + Sim.ArrivalsCheckedInToday
            + L("  ·  turned away ", "  ·  流失 ") + Sim.ArrivalsTurnedAwayToday
            + L("\nQueue waiting ", "\n排队等待 ") + Sim.TotalCheckInWaitToday
            + L(" min  ·  rating ", " 分钟  ·  评分 ") + Sim.Reputation.Stars.ToString("0.00") + "★",
            Gold,
            82f);

        if (Sim.Safebox.Balance > 0)
            AddMorningAction(
                L("COLLECT PROFIT  +$", "收取保险箱利润  +$") + Sim.Safebox.Balance,
                CollectSafebox,
                Gold,
                true);

        if (Sim.Overflow.Balance > 0)
            AddMorningAction(
                L("SALVAGE OVERFLOW CASH  +$", "抢救溢出现金  +$") + Sim.Overflow.Balance,
                RecoverOverflowForReport,
                Coral,
                true);

        int owed = Sim.Payroll.Owed;
        if (owed > 0)
        {
            bool weekly = Sim.Payroll.Cycle == PayrollCycle.Weekly;
            AddMorningInfo(
                L("PAYROLL DUE", "员工工资"),
                L("Owed $", "待付 $") + owed
                + (weekly
                    ? L("  ·  payday in ", "  ·  距离发薪日 ")
                      + Sim.Payroll.DaysUntilPayday(Sim.Clock.CurrentDay)
                      + L(" day(s)", " 天")
                    : L("  ·  paid daily", "  ·  日结工资")),
                owed <= Sim.Cash + Sim.Safebox.Balance ? Teal : Coral,
                68f);
            AddMorningAction(
                L("PAY WAGES  -$", "支付工资  -$") + owed,
                PayWagesForReport,
                Coral,
                Sim.Cash + Sim.Safebox.Balance > 0);
            AddMorningAction(
                weekly ? L("SWITCH TO DAILY PAY", "改为每日发薪")
                       : L("SWITCH TO WEEKLY PAY", "改为每周发薪"),
                TogglePayrollCycleForReport,
                Ink,
                true);
        }

        if (loanBalance > 0)
        {
            int suggested = DebtPolicy.ScheduledRepaymentFor(
                loanBalance,
                last.netToSafebox,
                Sim.Cash + Sim.Safebox.Balance,
                150);
            AddMorningInfo(
                L("BANK DEBT", "银行债务"),
                L("Balance $", "余额 $") + loanBalance
                + L("  ·  credit ", "  ·  信用 ") + Sim.Credit.Score + "/100"
                + "\n" + GameText.T(CreditPolicy.LabelOf(Sim.Credit.Rating)),
                Sim.Credit.PaidToday ? Teal : Gold,
                82f);
            if (!Sim.Credit.PaidToday && suggested > 0)
                AddMorningAction(
                    L("PAY THE BANK  -$", "偿还银行  -$") + suggested,
                    () => PayBank(suggested),
                    Coral,
                    Sim.Cash > 0);
        }

        IReadOnlyList<HotelSim.RefundRequest> refunds = Sim.PendingRefunds;
        if (refunds.Count > 0)
        {
            AddMorningSection(
                L("GUEST CLAIMS MUST BE CLOSED", "必须先处理客人退款"),
                L("The doors stay locked until every claim has a decision.",
                  "所有退款申请作出决定后，酒店才能重新开门。"));
            int shown = Mathf.Min(3, refunds.Count);
            for (int i = 0; i < shown; i++)
            {
                HotelSim.RefundRequest request = refunds[i];
                int requestId = request.requestId;
                AddMorningInfo(
                    L("ROOM ", "房间 ") + request.roomNumber
                    + L(" CLAIM  $", " 退款要求  $") + request.amount,
                    GameText.T(request.line),
                    Coral,
                    78f);
                AddMorningAction(
                    L("APPROVE REFUND  -$", "同意退款  -$") + request.amount,
                    () => ResolveMorningRefund(requestId, true),
                    Teal,
                    Sim.Cash >= request.amount);
                AddMorningAction(
                    L("REFUSE  ·  RATING WILL DROP", "拒绝  ·  评分将下降"),
                    () => ResolveMorningRefund(requestId, false),
                    Coral,
                    true);
            }
        }
        else
        {
            AddMorningInfo(
                L("READY FOR A NEW DAY", "新的一天可以开门"),
                L("Review the money, collect what you need, then reopen the hotel.",
                  "确认昨日收入与支出，收取需要的资金，然后重新开门营业。"),
                Teal,
                74f);
        }

        bool canOpen = refunds.Count == 0;
        _openDoorsButton.interactable = canOpen;
        _openDoorsButton.GetComponent<Image>().color = canOpen ? Gold : Slate;
        _openDoorsLabel.color = ContrastText(canOpen ? Gold : Slate);
        _openDoorsLabel.text = canOpen
            ? L("OPEN THE DOORS  ·  START DAY ", "开门营业  ·  开始第 ") + Sim.Clock.CurrentDay
            : L("RESOLVE ", "先处理 ") + refunds.Count + L(" CLAIM(S)", " 笔退款");

        Canvas.ForceUpdateCanvases();
        ScrollRect scroll = _morningReport.GetComponent<ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    private void OpenNextDay()
    {
        if (Sim == null || Sim.PendingRefunds.Count > 0)
        {
            ShowToast(L("Resolve every guest claim first.", "请先处理全部客人退款。"));
            return;
        }

        HotelSimSceneBridge bridge = HotelSimSceneBridge.Instance;
        if (bridge != null) bridge.ContinueToNextDay();
        ForceAllRefresh();
    }

    private void RecoverOverflowForReport()
    {
        int amount = Sim != null ? Sim.RecoverOverflow() : 0;
        ShowToast(amount > 0
            ? L("Recovered overflow cash  +$", "已抢救溢出现金  +$") + amount
            : L("No overflow cash remains.", "没有可抢救的溢出现金。"));
        ForceAllRefresh();
    }

    private void PayWagesForReport()
    {
        if (Sim == null) return;
        PayrollPayment payment = Sim.PayWages();
        ShowToast(payment.cleared
            ? L("Payroll cleared  -$", "工资已结清  -$") + payment.paid
            : L("Paid $", "已支付 $") + payment.paid
              + L(", still owed $", "，仍欠 $") + payment.stillOwed);
        ForceAllRefresh();
    }

    private void TogglePayrollCycleForReport()
    {
        if (Sim == null) return;
        PayrollCycle next = Sim.Payroll.Cycle == PayrollCycle.Weekly
            ? PayrollCycle.Daily
            : PayrollCycle.Weekly;
        bool changed = Sim.Payroll.TrySetCycle(next, out string reason);
        ShowToast(changed
            ? (next == PayrollCycle.Weekly
                ? L("Payroll changed to weekly.", "工资已改为每周发放。")
                : L("Payroll changed to daily.", "工资已改为每日发放。"))
            : GameText.T(reason));
        ForceAllRefresh();
    }

    private void ResolveMorningRefund(int requestId, bool approve)
    {
        if (Sim == null) return;
        bool resolved = approve
            ? Sim.ApproveRefund(requestId)
            : Sim.RejectRefund(requestId);
        ShowToast(resolved
            ? (approve
                ? L("Refund approved.", "退款已同意。")
                : L("Refund refused. The rating will take the hit.", "退款已拒绝，评分将受到影响。"))
            : L("Not enough cash for that refund.", "现金不足，无法完成退款。"));
        ForceAllRefresh();
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject(
            "WorldManagementHUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 80;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480f, 960f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.45f;

        _safeRoot = CreateRect("SafeArea", canvasObject.transform);
        Stretch(_safeRoot);
        BuildTop();
        BuildAlertStrip();
        BuildBottomNavigation();
        BuildDrawer();
        BuildRoomCard();
        BuildContextSheet();
        BuildMorningReport();
        BuildToast();
        UpdateSafeArea(force: true);
        SetDrawerOpen(false);
    }

    private void BuildMorningReport()
    {
        _morningBackdrop = CreatePanel("MorningReportBackdrop", _canvas.transform, Ink);
        Stretch(_morningBackdrop);
        Image backdropImage = _morningBackdrop.GetComponent<Image>();
        backdropImage.sprite = null;
        backdropImage.type = Image.Type.Simple;
        _morningBackdrop.SetAsFirstSibling();
        _morningBackdrop.gameObject.SetActive(false);

        _morningReport = CreatePanel("MorningReport", _safeRoot, Glass);
        Stretch(_morningReport);
        AddOutline(_morningReport, BrassLine, 1.2f);

        var topAccent = CreatePanel("TopAccent", _morningReport, Gold);
        SetAnchors(topAccent, new Vector2(0f, 1f), Vector2.one,
            Vector2.zero, new Vector2(0f, -5f));

        _morningTitle = CreateText("Title", _morningReport, 25f, Gold,
            TextAlignmentOptions.BottomLeft, true);
        SetAnchors(_morningTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(20f, -70f), new Vector2(-18f, -16f));

        _morningSubtitle = CreateText("Subtitle", _morningReport, 12.5f, InkSoft,
            TextAlignmentOptions.TopLeft, false);
        SetAnchors(_morningSubtitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(20f, -98f), new Vector2(-18f, -70f));

        var viewport = CreatePanel("Viewport", _morningReport, new Color(0f, 0f, 0f, 0f));
        SetAnchors(viewport, Vector2.zero, Vector2.one,
            new Vector2(14f, 90f), new Vector2(-14f, -106f));
        viewport.gameObject.AddComponent<RectMask2D>();

        _morningContent = CreateRect("Content", viewport);
        _morningContent.anchorMin = new Vector2(0f, 1f);
        _morningContent.anchorMax = new Vector2(1f, 1f);
        _morningContent.pivot = new Vector2(0.5f, 1f);
        _morningContent.anchoredPosition = Vector2.zero;
        _morningContent.sizeDelta = Vector2.zero;
        var layout = _morningContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(2, 2, 2, 20);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        var fitter = _morningContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = _morningReport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = _morningContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        RectTransform openRect;
        _openDoorsButton = CreateButton(
            "OpenDoors",
            _morningReport,
            out openRect,
            out _openDoorsLabel,
            "",
            Gold,
            OpenNextDay);
        SetAnchors(openRect, Vector2.zero, new Vector2(1f, 0f),
            new Vector2(16f, 18f), new Vector2(-16f, 72f));

        _morningReport.gameObject.SetActive(false);
    }

    private void BuildTop()
    {
        _topPanel = CreatePanel("EconomyHeader", _safeRoot, Glass);
        SetAnchors(_topPanel, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -114f), new Vector2(-8f, -8f));
        AddOutline(_topPanel, BrassLine, 1.2f);

        var accent = CreatePanel("Accent", _topPanel, Gold);
        SetAnchors(accent, new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(5f, 0f));

        _dayTimeLabel = CreateText("DayTime", _topPanel, 14f, Cream,
            TextAlignmentOptions.TopLeft, true);
        SetAnchors(_dayTimeLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.64f, 1f),
            new Vector2(16f, 2f), new Vector2(-2f, -8f));

        _cashLabel = CreateText("Cash", _topPanel, 22f, Gold,
            TextAlignmentOptions.TopRight, true);
        _cashLabel.enableAutoSizing = true;
        _cashLabel.fontSizeMin = 16f;
        _cashLabel.fontSizeMax = 22f;
        SetAnchors(_cashLabel.rectTransform, new Vector2(0.54f, 0.48f), new Vector2(1f, 1f),
            new Vector2(0f, 0f), new Vector2(-16f, -8f));

        _earnedLabel = CreateText("Earned", _topPanel, 13f, Cream,
            TextAlignmentOptions.MidlineLeft, true);
        SetAnchors(_earnedLabel.rectTransform, new Vector2(0f, 0.22f), new Vector2(0.58f, 0.58f),
            new Vector2(16f, 0f), Vector2.zero);

        _occupancyLabel = CreateText("Occupancy", _topPanel, 13f, Cream,
            TextAlignmentOptions.MidlineLeft, true);
        SetAnchors(_occupancyLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 0.30f),
            new Vector2(16f, 3f), Vector2.zero);

        RectTransform safeButtonRect;
        _safeboxButton = CreateButton("Safebox", _topPanel, out safeButtonRect,
            out _safeboxLabel, "", Gold, CollectSafebox);
        _safeboxButtonImage = _safeboxButton.GetComponent<Image>();
        SetAnchors(safeButtonRect, new Vector2(0.62f, 0.05f), new Vector2(1f, 0.45f),
            new Vector2(0f, 0f), new Vector2(-14f, 0f));

        var progress = CreatePanel("SafeboxProgress", safeButtonRect, new Color(0f, 0f, 0f, 0.25f));
        SetAnchors(progress, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(8f, 5f), new Vector2(-8f, 9f));
        _safeboxFill = CreatePanel("Fill", progress, Teal).GetComponent<Image>();
        Stretch(_safeboxFill.rectTransform);
        _safeboxFill.raycastTarget = false;
    }

    private void BuildBottomNavigation()
    {
        _bottomNav = CreatePanel("BottomNavigation", _safeRoot, Glass);
        SetAnchors(_bottomNav, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(8f, 8f), new Vector2(-8f, 66f));
        AddOutline(_bottomNav, BrassLine, 1.2f);
        var layout = _bottomNav.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 5, 5);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = true;

        AddNavButton(DeskTab.Overview, L("PULSE", "经营"));
        AddNavButton(DeskTab.Pricing, L("RATES", "房价"));
        AddNavButton(DeskTab.Staff, L("TEAM", "员工"));
        AddNavButton(DeskTab.Assets, L("ASSETS", "资产"));
    }

    private void BuildAlertStrip()
    {
        _alertStrip = CreatePanel("IncidentStrip", _safeRoot, Walnut);
        SetAnchors(_alertStrip, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -166f), new Vector2(-8f, -120f));
        AddOutline(_alertStrip, BrassLine, 1f);

        var accent = CreatePanel("Accent", _alertStrip, Coral);
        SetAnchors(accent, Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, new Vector2(5f, 0f));

        _alertLabel = CreateText("Alert", _alertStrip, 12.5f, Cream,
            TextAlignmentOptions.MidlineLeft, true);
        SetAnchors(_alertLabel.rectTransform, Vector2.zero, new Vector2(0.79f, 1f),
            new Vector2(14f, 4f), new Vector2(-4f, -4f));
        _alertLabel.enableWordWrapping = true;

        RectTransform goRect;
        _alertGoButton = CreateButton("Go", _alertStrip, out goRect, out _alertGoLabel,
            L("GO", "前往"), Gold, NavigateToDisplayedAlert);
        SetAnchors(goRect, new Vector2(0.80f, 0f), Vector2.one,
            new Vector2(0f, 6f), new Vector2(-8f, -6f));
        _alertStrip.gameObject.SetActive(false);
    }

    private void AddNavButton(DeskTab tab, string label)
    {
        RectTransform rect;
        TextMeshProUGUI text;
        Button button = CreateButton(tab.ToString(), _bottomNav, out rect, out text,
            label, Paper, () => SelectTab(tab));
        var element = button.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        _navButtons.Add(button);
        _navLabels.Add(text);
    }

    private void BuildDrawer()
    {
        _drawer = CreatePanel("ManagementDesk", _safeRoot, Ledger);
        SetAnchors(_drawer, new Vector2(0f, 0f), new Vector2(1f, 0.68f),
            new Vector2(8f, 72f), new Vector2(-8f, -8f));
        AddOutline(_drawer, BrassLine, 1.4f);

        var headerBand = CreatePanel("HeaderBand", _drawer, Walnut);
        SetAnchors(headerBand, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -58f), Vector2.zero);
        headerBand.SetAsFirstSibling();

        var handle = CreatePanel("Handle", _drawer, Gold);
        SetAnchors(handle, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -4f), Vector2.zero);

        _drawerTitle = CreateText("Title", _drawer, 22f, Cream,
            TextAlignmentOptions.MidlineLeft, true);
        SetAnchors(_drawerTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0.82f, 1f),
            new Vector2(18f, -55f), new Vector2(0f, -8f));

        RectTransform closeRect;
        TextMeshProUGUI closeText;
        CreateButton("Close", _drawer, out closeRect, out closeText,
            "", Walnut, () => SetDrawerOpen(false));
        SetAnchors(closeRect, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-54f, -51f), new Vector2(-12f, -10f));
        AddCloseGlyph(closeRect);

        var viewport = CreatePanel("Viewport", _drawer, new Color(0f, 0f, 0f, 0f));
        SetAnchors(viewport, Vector2.zero, Vector2.one,
            new Vector2(10f, 10f), new Vector2(-10f, -60f));
        var mask = viewport.gameObject.AddComponent<RectMask2D>();
        mask.padding = Vector4.zero;

        _drawerContent = CreateRect("Content", viewport);
        _drawerContent.anchorMin = new Vector2(0f, 1f);
        _drawerContent.anchorMax = new Vector2(1f, 1f);
        _drawerContent.pivot = new Vector2(0.5f, 1f);
        _drawerContent.anchoredPosition = Vector2.zero;
        _drawerContent.sizeDelta = Vector2.zero;
        var vertical = _drawerContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(2, 2, 2, 18);
        vertical.spacing = 8f;
        vertical.childControlHeight = true;
        vertical.childControlWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.childForceExpandWidth = true;
        var fitter = _drawerContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = _drawer.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = _drawerContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
    }

    private void BuildRoomCard()
    {
        _roomCard = CreatePanel("SelectedRoomInvestment", _safeRoot, Ledger);
        SetAnchors(_roomCard, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(8f, 72f), new Vector2(-8f, 262f));
        AddOutline(_roomCard, BrassLine, 1.4f);

        var accent = CreatePanel("Accent", _roomCard, Gold);
        SetAnchors(accent, new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(6f, 0f));

        _roomTitle = CreateText("RoomTitle", _roomCard, 18f, Gold,
            TextAlignmentOptions.TopLeft, true);
        SetAnchors(_roomTitle.rectTransform, new Vector2(0f, 0.72f), new Vector2(0.76f, 1f),
            new Vector2(16f, 0f), new Vector2(0f, -10f));

        _roomHeadline = CreateText("RoomHeadline", _roomCard, 18f, Cream,
            TextAlignmentOptions.TopLeft, true);
        SetAnchors(_roomHeadline.rectTransform, new Vector2(0f, 0.51f), new Vector2(1f, 0.78f),
            new Vector2(16f, 0f), new Vector2(-14f, 0f));

        _roomDetail = CreateText("RoomDetail", _roomCard, 12.5f, InkSoft,
            TextAlignmentOptions.TopLeft, false);
        _roomDetail.enableWordWrapping = true;
        SetAnchors(_roomDetail.rectTransform, new Vector2(0f, 0.27f), new Vector2(1f, 0.57f),
            new Vector2(16f, 0f), new Vector2(-14f, 0f));

        RectTransform closeRect;
        TextMeshProUGUI closeText;
        CreateButton("CloseRoom", _roomCard, out closeRect, out closeText,
            "×", Walnut, RoomSelection.Clear);
        SetAnchors(closeRect, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-49f, -43f), new Vector2(-10f, -8f));

        RectTransform primaryRect;
        _roomPrimary = CreateButton("Primary", _roomCard, out primaryRect,
            out _roomPrimaryLabel, "", Teal, null);
        SetAnchors(primaryRect, new Vector2(0f, 0f), new Vector2(0.55f, 0.25f),
            new Vector2(14f, 10f), new Vector2(-4f, 0f));

        RectTransform secondaryRect;
        _roomSecondary = CreateButton("Secondary", _roomCard, out secondaryRect,
            out _roomSecondaryLabel, "", Ink, null);
        SetAnchors(secondaryRect, new Vector2(0.55f, 0f), new Vector2(1f, 0.25f),
            new Vector2(4f, 10f), new Vector2(-12f, 0f));
    }

    private void BuildContextSheet()
    {
        _contextSheet = CreatePanel("WorldActionSheet", _safeRoot, Ledger);
        SetAnchors(_contextSheet, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(8f, 72f), new Vector2(-8f, 260f));
        AddOutline(_contextSheet, BrassLine, 1.4f);

        var accent = CreatePanel("Accent", _contextSheet, Gold);
        SetAnchors(accent, Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, new Vector2(6f, 0f));

        _contextContent = CreateRect("Content", _contextSheet);
        Stretch(_contextContent, 12f);
        _contextContent.offsetMin += new Vector2(6f, 2f);
        var layout = _contextContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        _contextSheet.gameObject.SetActive(false);
    }

    private void BuildToast()
    {
        _toastPanel = CreatePanel("Toast", _safeRoot, Walnut);
        SetAnchors(_toastPanel, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.73f),
            new Vector2(0f, -26f), new Vector2(0f, 26f));
        _toastGroup = _toastPanel.gameObject.AddComponent<CanvasGroup>();
        _toastGroup.alpha = 0f;
        _toastGroup.blocksRaycasts = false;
        _toastLabel = CreateText("ToastLabel", _toastPanel, 14f, Cream,
            TextAlignmentOptions.Center, true);
        Stretch(_toastLabel.rectTransform, 12f);
    }

    private void SetRoomButton(
        Button button,
        TextMeshProUGUI label,
        string text,
        Action action,
        bool interactable,
        Color color)
    {
        label.text = text;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(() => action());
        button.interactable = interactable;
        button.GetComponent<Image>().color = interactable ? color : Slate;
        label.color = ContrastText(interactable ? color : Slate);
    }

    private void AddSection(string title, string subtitle)
    {
        var item = CreateRect("Section", _drawerContent);
        var element = item.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 58f;
        var titleText = CreateText("Title", item, 17f, Cream,
            TextAlignmentOptions.BottomLeft, true);
        SetAnchors(titleText.rectTransform, new Vector2(0f, 0.42f), Vector2.one,
            new Vector2(8f, 0f), new Vector2(-8f, 0f));
        titleText.text = title;
        var subText = CreateText("Subtitle", item, 11.5f, InkSoft,
            TextAlignmentOptions.TopLeft, false);
        subText.enableWordWrapping = true;
        SetAnchors(subText.rectTransform, Vector2.zero, new Vector2(1f, 0.45f),
            new Vector2(8f, 0f), new Vector2(-8f, 0f));
        subText.text = subtitle;

        var rule = CreatePanel("LedgerRule", item, BrassLine);
        SetAnchors(rule, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(8f, 0f), new Vector2(-8f, 1f));
        rule.GetComponent<Image>().raycastTarget = false;
    }

    private void AddInfoCard(
        string title,
        string body,
        Color accent,
        float height)
    {
        var item = CreatePanel("InfoCard", _drawerContent, Paper);
        var element = item.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        AddOutline(item, new Color(BrassLine.r, BrassLine.g, BrassLine.b, 0.35f), 0.8f);
        var stripe = CreatePanel("Stripe", item, accent);
        SetAnchors(stripe, Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, new Vector2(5f, 0f));
        var titleText = CreateText("Title", item, 15f, Cream,
            TextAlignmentOptions.TopLeft, true);
        SetAnchors(titleText.rectTransform, new Vector2(0f, 0.62f), Vector2.one,
            new Vector2(14f, 2f), new Vector2(-10f, -8f));
        titleText.text = title;
        var bodyText = CreateText("Body", item, 12.5f, InkSoft,
            TextAlignmentOptions.TopLeft, false);
        bodyText.enableWordWrapping = true;
        SetAnchors(bodyText.rectTransform, Vector2.zero, new Vector2(1f, 0.7f),
            new Vector2(14f, 8f), new Vector2(-10f, 0f));
        bodyText.text = body;
    }

    private void AddActionButton(
        string label,
        Action action,
        Color color,
        bool interactable)
    {
        RectTransform rect;
        TextMeshProUGUI text;
        Button button = CreateButton("Action", _drawerContent, out rect, out text,
            label, interactable ? color : Slate, action);
        button.interactable = interactable;
        var element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 46f;
    }

    private void AddMorningSection(string title, string subtitle)
    {
        var item = CreateRect("ReportSection", _morningContent);
        item.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
        var titleText = CreateText("Title", item, 16f, Gold,
            TextAlignmentOptions.BottomLeft, true);
        SetAnchors(titleText.rectTransform, new Vector2(0f, 0.42f), Vector2.one,
            new Vector2(8f, 0f), new Vector2(-8f, 0f));
        titleText.text = title;
        var subtitleText = CreateText("Subtitle", item, 11.5f, InkSoft,
            TextAlignmentOptions.TopLeft, false);
        subtitleText.enableWordWrapping = true;
        SetAnchors(subtitleText.rectTransform, Vector2.zero, new Vector2(1f, 0.47f),
            new Vector2(8f, 0f), new Vector2(-8f, 0f));
        subtitleText.text = subtitle;

        var rule = CreatePanel("Rule", item, BrassLine);
        SetAnchors(rule, Vector2.zero, new Vector2(1f, 0f),
            new Vector2(8f, 0f), new Vector2(-8f, 1f));
        rule.GetComponent<Image>().raycastTarget = false;
    }

    private void AddMorningInfo(string title, string body, Color accent, float height)
    {
        var item = CreatePanel("ReportCard", _morningContent, Paper);
        item.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        AddOutline(item, new Color(BrassLine.r, BrassLine.g, BrassLine.b, 0.35f), 0.8f);

        var stripe = CreatePanel("Stripe", item, accent);
        SetAnchors(stripe, Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, new Vector2(5f, 0f));

        var titleText = CreateText("Title", item, 15.5f, Cream,
            TextAlignmentOptions.TopLeft, true);
        SetAnchors(titleText.rectTransform, new Vector2(0f, 0.66f), Vector2.one,
            new Vector2(14f, 2f), new Vector2(-10f, -8f));
        titleText.text = title;

        var bodyText = CreateText("Body", item, 12f, InkSoft,
            TextAlignmentOptions.TopLeft, false);
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Truncate;
        SetAnchors(bodyText.rectTransform, Vector2.zero, new Vector2(1f, 0.72f),
            new Vector2(14f, 8f), new Vector2(-10f, 0f));
        bodyText.text = body;
    }

    private void AddMorningAction(
        string label,
        Action action,
        Color color,
        bool interactable)
    {
        RectTransform rect;
        TextMeshProUGUI text;
        Button button = CreateButton(
            "ReportAction",
            _morningContent,
            out rect,
            out text,
            label,
            interactable ? color : Slate,
            action);
        button.interactable = interactable;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
    }

    private void AddPlanButton(
        string title,
        string detail,
        Action action,
        Color color,
        bool interactable)
    {
        RectTransform rect;
        TextMeshProUGUI ignored;
        Button button = CreateButton("Plan", _drawerContent, out rect, out ignored,
            "", interactable ? color : Slate, action);
        button.interactable = interactable;
        Destroy(ignored.gameObject);
        var element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 74f;

        Color planText = ContrastText(interactable ? color : Slate);
        var titleText = CreateText("Title", rect, 15f, planText,
            TextAlignmentOptions.TopLeft, true);
        SetAnchors(titleText.rectTransform, new Vector2(0f, 0.52f), Vector2.one,
            new Vector2(14f, 0f), new Vector2(-12f, -8f));
        titleText.text = title;
        var detailText = CreateText("Detail", rect, 11.5f, planText,
            TextAlignmentOptions.TopLeft, false);
        detailText.enableWordWrapping = true;
        SetAnchors(detailText.rectTransform, Vector2.zero, new Vector2(1f, 0.58f),
            new Vector2(14f, 7f), new Vector2(-12f, 0f));
        detailText.text = detail;
    }

    private void AddSpeedButtons()
    {
        var row = CreateRect("SpeedRow", _drawerContent);
        var element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 42f;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        foreach (float speed in new[] { 0.25f, 1f, 2f })
        {
            float captured = speed;
            RectTransform rect;
            TextMeshProUGUI text;
            Button button = CreateButton(
                "Speed" + speed,
                row,
                out rect,
                out text,
                (_speed == speed ? "● " : "") + speed.ToString("0.##") + "x",
                _speed == speed ? Gold : Ink,
                () => SetSpeed(captured));
            button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }
    }

    private void ShowMoneyBurst(string message, Color color)
    {
        if (_safeRoot == null || !_safeRoot.gameObject.activeInHierarchy) return;
        StartCoroutine(MoneyBurstRoutine(message, color));
    }

    private IEnumerator MoneyBurstRoutine(string message, Color color)
    {
        TextMeshProUGUI text = CreateText(
            "MoneyBurst",
            _safeRoot,
            21f,
            color,
            TextAlignmentOptions.Center,
            true);
        SetAnchors(text.rectTransform, new Vector2(0.2f, 0.82f), new Vector2(0.8f, 0.82f),
            new Vector2(0f, -24f), new Vector2(0f, 24f));
        text.text = message;
        var group = text.gameObject.AddComponent<CanvasGroup>();
        float t = 0f;
        Vector2 start = text.rectTransform.anchoredPosition;
        while (t < 1.1f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 1.1f);
            text.rectTransform.anchoredPosition = start + Vector2.up * (28f * p);
            group.alpha = 1f - Mathf.Clamp01((p - 0.55f) / 0.45f);
            yield return null;
        }
        Destroy(text.gameObject);
    }

    private IEnumerator BumpCash()
    {
        if (_cashLabel == null) yield break;
        RectTransform rect = _cashLabel.rectTransform;
        rect.localScale = Vector3.one * 1.14f;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            rect.localScale = Vector3.Lerp(Vector3.one * 1.14f, Vector3.one, t / 0.2f);
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    private void ShowToast(string message)
    {
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    private IEnumerator ToastRoutine(string message)
    {
        _toastLabel.text = message;
        _toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(2.2f);
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            _toastGroup.alpha = 1f - t / 0.25f;
            yield return null;
        }
        _toastGroup.alpha = 0f;
        _toastRoutine = null;
    }

    private void UpdateSafeArea(bool force = false)
    {
        if (_safeRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        if (!force && safe == _lastSafeArea) return;
        _lastSafeArea = safe;

        // Device Simulator reports safeArea in the emulated device resolution,
        // while Screen.width/height can be the scaled editor viewport.
        Resolution resolution = Screen.currentResolution;
        float coordinateWidth = safe.xMax > Screen.width + 1f
            ? Mathf.Max(1f, resolution.width)
            : Screen.width;
        float coordinateHeight = safe.yMax > Screen.height + 1f
            ? Mathf.Max(1f, resolution.height)
            : Screen.height;
        _safeRoot.anchorMin = new Vector2(
            Mathf.Clamp01(safe.xMin / coordinateWidth),
            Mathf.Clamp01(safe.yMin / coordinateHeight));
        _safeRoot.anchorMax = new Vector2(
            Mathf.Clamp01(safe.xMax / coordinateWidth),
            Mathf.Clamp01(safe.yMax / coordinateHeight));
        _safeRoot.offsetMin = Vector2.zero;
        _safeRoot.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return rect;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        out RectTransform rect,
        out TextMeshProUGUI label,
        string text,
        Color color,
        Action onClick)
    {
        rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        AddOutline(rect, new Color(BrassLine.r, BrassLine.g, BrassLine.b, 0.38f), 0.8f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, White, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.disabledColor = new Color(Slate.r, Slate.g, Slate.b, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        label = CreateText("Label", rect, 13.5f, ContrastText(color),
            TextAlignmentOptions.Center, true);
        label.text = text;
        Stretch(label.rectTransform, 6f);
        return button;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        float size,
        Color color,
        TextAlignmentOptions alignment,
        bool bold)
    {
        RectTransform rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = RuntimeFont;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.characterSpacing = 0.35f;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableWordWrapping = false;
        return text;
    }

    private static void AddCloseGlyph(RectTransform parent)
    {
        RectTransform forward = CreatePanel("SlashForward", parent, Cream);
        RectTransform back = CreatePanel("SlashBack", parent, Cream);
        SetAnchors(forward, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-8f, -1.5f), new Vector2(8f, 1.5f));
        SetAnchors(back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-8f, -1.5f), new Vector2(8f, 1.5f));
        forward.localRotation = Quaternion.Euler(0f, 0f, 45f);
        back.localRotation = Quaternion.Euler(0f, 0f, -45f);
        forward.GetComponent<Image>().raycastTarget = false;
        back.GetComponent<Image>().raycastTarget = false;
    }

    private static void AddOutline(RectTransform rect, Color color, float distance)
    {
        var outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    private static Color ContrastText(Color background)
    {
        float luminance =
            background.r * 0.2126f +
            background.g * 0.7152f +
            background.b * 0.0722f;
        return luminance > 0.43f ? Ink : Cream;
    }

    private static TMP_FontAsset RuntimeFont
    {
        get
        {
            if (_runtimeFont != null) return _runtimeFont;

            string[] installed = Font.GetOSInstalledFontNames();
            string[] preferredFamilies =
            {
                "Microsoft YaHei UI",
                "PingFang SC",
                "Noto Sans CJK SC",
                "Noto Sans SC",
                "Droid Sans Fallback",
                "Arial Unicode MS",
                "SimHei"
            };
            const string probe = "酒店经营现金房间员工收益回本";
            for (int i = 0; i < preferredFamilies.Length && _runtimeFont == null; i++)
            {
                string family = preferredFamilies[i];
                bool available = false;
                for (int j = 0; j < installed.Length; j++)
                {
                    if (!string.Equals(installed[j], family, StringComparison.OrdinalIgnoreCase))
                        continue;
                    family = installed[j];
                    available = true;
                    break;
                }
                if (!available) continue;

                TMP_FontAsset candidate = TMP_FontAsset.CreateFontAsset(family, "Regular", 48);
                if (candidate == null) continue;
                candidate.TryAddCharacters(probe, out _);
                if (candidate.HasCharacters(probe))
                {
                    candidate.name = "Runtime CJK UI";
                    _runtimeFont = candidate;
                }
                else
                {
                    Destroy(candidate);
                }
            }

            if (_runtimeFont == null)
                _runtimeFont = TMP_Settings.defaultFontAsset;
            return _runtimeFont;
        }
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetAnchors(
        RectTransform rect,
        Vector2 min,
        Vector2 max,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private int RateForTemplate(PriceTemplate template, RoomTier tier)
    {
        float multiplier = PricingPolicy.MultiplierOf(template);
        if (PricingPolicy.IsWeekend(Sim.Clock.CurrentDay))
            multiplier *= PricingPolicy.WeekendMultiplier;
        int market = RoomRateTable.Default.For(tier);
        return SimMath.RoundToInt(market * multiplier);
    }

    private int CountStaff(StaffRole role)
    {
        int count = 0;
        foreach (StaffSimEntry entry in Sim.Staff.Entries)
            if (entry.member != null && entry.member.Role == role) count++;
        return count;
    }

    private static RoomTier ReclaimTarget(ReclaimPlanKind kind)
    {
        switch (kind)
        {
            case ReclaimPlanKind.Refit: return RoomTier.Basic;
            case ReclaimPlanKind.FullFit: return RoomTier.Better;
            default: return RoomTier.Old;
        }
    }

    private static string RoleName(StaffRole role)
    {
        if (!GameText.UseChinese) return role.ToString().ToUpperInvariant();
        switch (role)
        {
            case StaffRole.Reception: return "前台";
            case StaffRole.Housekeeper: return "客房清洁";
            case StaffRole.Inspector: return "验房";
            default: return role.ToString();
        }
    }

    private static string RoleConsequence(StaffRole role)
    {
        if (!GameText.UseChinese)
        {
            switch (role)
            {
                case StaffRole.Reception: return "Faster check-in protects arrivals and ratings.";
                case StaffRole.Housekeeper: return "More clean rooms become sellable sooner.";
                default: return "Inspection releases cleaned rooms back to sale.";
            }
        }

        switch (role)
        {
            case StaffRole.Reception: return "提高入住办理速度，减少排队流失和差评。";
            case StaffRole.Housekeeper: return "更快清理退房，缩短房间停止销售的时间。";
            default: return "验房越快，清洁完成的房间越早恢复销售。";
        }
    }

    private static string StaffStateName(StaffOperationalState state)
    {
        if (!GameText.UseChinese) return state.ToString();
        switch (state)
        {
            case StaffOperationalState.Available: return "在班待命";
            case StaffOperationalState.Assigned: return "前往任务";
            case StaffOperationalState.Working: return "工作中";
            case StaffOperationalState.Break: return "休息中";
            case StaffOperationalState.Slacking: return "摸鱼中";
            case StaffOperationalState.Absent: return "缺勤";
            default: return "下班";
        }
    }

    private static string PriceTemplateName(PriceTemplate template)
    {
        if (!GameText.UseChinese) return template.ToString().ToUpperInvariant();
        switch (template)
        {
            case PriceTemplate.Clearance: return "清仓价  70%";
            case PriceTemplate.Conservative: return "稳健价  90%";
            case PriceTemplate.Squeeze: return "榨利价  125%";
            default: return "市场价  100%";
        }
    }

    private static string PriceTemplateConsequence(PriceTemplate template)
    {
        if (!GameText.UseChinese) return GameText.T(PricingPolicy.LabelOf(template));
        switch (template)
        {
            case PriceTemplate.Clearance: return "入住率更高，但客群更杂、清洁压力更大";
            case PriceTemplate.Conservative: return "较容易售出，利润和期待都偏低";
            case PriceTemplate.Squeeze: return "单房利润最高，但客人期待与差评风险上升";
            default: return "需求、利润和期待保持平衡";
        }
    }

    private static string ReclaimName(ReclaimPlanKind kind)
    {
        if (!GameText.UseChinese) return kind.ToString().ToUpperInvariant();
        switch (kind)
        {
            case ReclaimPlanKind.PatchUp: return "修旧复原";
            case ReclaimPlanKind.Refit: return "基础换新";
            default: return "全套翻新";
        }
    }

    private static string RenovationName(RenovationPlanKind kind)
    {
        if (!GameText.UseChinese) return kind.ToString().ToUpperInvariant();
        switch (kind)
        {
            case RenovationPlanKind.Economy: return "经济装修";
            case RenovationPlanKind.Standard: return "标准装修";
            default: return "豪华装修";
        }
    }

    private static string L(string english, string chinese) =>
        GameText.UseChinese ? chinese : english;

    private static Color Hex(string value)
    {
        return ColorUtility.TryParseHtmlString(value, out Color color)
            ? color
            : Color.magenta;
    }

    private static Sprite RoundedSprite
    {
        get
        {
            if (_roundedSprite != null) return _roundedSprite;
            const int size = 32;
            const float radius = 8f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeRoundedRect",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0f);
                    dx = Mathf.Max(dx, x - (size - 1 - radius));
                    float dy = Mathf.Max(radius - y, 0f);
                    dy = Mathf.Max(dy, y - (size - 1 - radius));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            _roundedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            _roundedSprite.name = "RuntimeRoundedRect";
            return _roundedSprite;
        }
    }
}

public static class RoomInvestmentMath
{
    public static int TotalInvestment(int cashCost, int materialUnits, int materialUnitPrice)
    {
        return Mathf.Max(0, cashCost)
               + Mathf.Max(0, materialUnits) * Mathf.Max(0, materialUnitPrice);
    }

    public static int SoldNightsToPayback(int totalInvestment, int nightlyGain)
    {
        if (totalInvestment <= 0) return 0;
        if (nightlyGain <= 0) return 999;
        return Mathf.CeilToInt(totalInvestment / (float)nightlyGain);
    }
}
