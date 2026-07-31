using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

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
    private RectTransform _toastPanel;
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
    private TextMeshProUGUI _toastLabel;

    private Image _safeboxFill;
    private Image _safeboxButtonImage;
    private Button _safeboxButton;
    private Button _roomPrimary;
    private Button _roomSecondary;

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
    private float _refreshTimer;
    private float _speed = 1f;
    private int _skipTargetMinute = -1;
    private int _skipDay = -1;
    private Rect _lastSafeArea;
    private Coroutine _toastRoutine;

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
        bool visible = Sim != null && (bridge == null || !bridge.AwaitingMorningReport);
        if (_safeRoot.gameObject.activeSelf != visible)
            _safeRoot.gameObject.SetActive(visible);
        if (!visible) return;

        TickSimulationSpeed();
        DetectMoneyChanges();

        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = 0.12f;

        RefreshTop();
        RefreshRoomCard();
        RefreshDrawerIfChanged();
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
        _cashLabel.text = "$" + Sim.Cash.ToString("N0");
        _earnedLabel.text = L("EARNED TODAY", "今日已赚")
                            + "  <color=#E8BB54>+$"
                            + Sim.GrossIncomeToday.ToString("N0") + "</color>";
        _occupancyLabel.text = L("TONIGHT", "今晚入住")
                               + "  " + tonight.Fraction;

        _safeboxLabel.text = Sim.Safebox.Balance > 0
            ? L("COLLECT ", "收取 ") + "$" + Sim.Safebox.Balance.ToString("N0")
            : L("SAFEBOX ", "保险箱 ") + "$0 / $" + Sim.Safebox.Capacity;
        _safeboxFill.rectTransform.anchorMax =
            new Vector2(Mathf.Clamp01(Sim.Safebox.FillRatio), 1f);
        _safeboxButton.interactable = Sim.Safebox.Balance > 0;
        _safeboxButtonImage.color = Sim.Safebox.Balance > 0 ? Gold : Slate;
        _safeboxLabel.color = ContrastText(_safeboxButtonImage.color);
    }

    private void RefreshRoomCard()
    {
        int number = RoomSelection.Selected;
        bool valid = !_drawerOpen && number > 0 && Sim.Rooms.Contains(number);
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
            _roomHeadline.text = L("BUILDING VALUE", "资产升级中");
            _roomDetail.text = L(
                days + " day(s) until handover. Cleaning is required before sale.",
                days + " 天后交付，完工后仍需清洁才能重新出售。");
            SetRoomButton(_roomPrimary, _roomPrimaryLabel, L("IN PROGRESS", "施工中"),
                null, false, Slate);
            SetRoomButton(_roomSecondary, _roomSecondaryLabel, L("VIEW PORTFOLIO", "查看资产"),
                OpenAssetsForSelectedRoom, true, Ink);
            return;
        }

        if (room.state == RoomSimState.Ruined)
        {
            var refit = ReclaimPlan.For(ReclaimPlanKind.Refit);
            int cash = Sim.QuoteReclaim(ReclaimPlanKind.Refit, 1);
            int total = RoomInvestmentMath.TotalInvestment(
                cash, refit.materialsPerRoom, Sim.Materials.UnitPrice);
            int nightly = Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, RoomTier.Basic);
            int payback = RoomInvestmentMath.SoldNightsToPayback(total, nightly);
            _roomHeadline.text = "<color=#C65B46>$0</color> / "
                                 + L("night", "晚") + "  →  <color=#297A6E>$"
                                 + nightly + "</color> / " + L("night", "晚");
            _roomDetail.text = Sim.Clearing.IsClearing(number)
                ? L("Free clearing ", "免费清理进行中  ")
                  + Sim.Clearing.ProgressOf(number).ToString("P0")
                : L(
                    "Free: housekeeping trips.  Fast: $" + cash + " + "
                    + refit.materialsPerRoom + " materials · " + refit.blockDays
                    + " days · pays back in " + payback + " sold nights.",
                    "免费路线：客房部往返清理。快速复原：$" + cash + " + "
                    + refit.materialsPerRoom + " 材料 · " + refit.blockDays
                    + " 天 · 约 " + payback + " 个售出夜回本。");

            SetRoomButton(
                _roomPrimary,
                _roomPrimaryLabel,
                Sim.Clearing.IsClearing(number) ? L("CLEARING", "清理中") : L("CLEAR FREE", "免费清理"),
                () => StartFreeClearing(number),
                !Sim.Clearing.IsClearing(number),
                Teal);
            SetRoomButton(_roomSecondary, _roomSecondaryLabel, L("COMPARE PLANS", "比较复原方案"),
                OpenAssetsForSelectedRoom, true, Ink);
            return;
        }

        if (room.state == RoomSimState.Blocked)
        {
            FurnitureInstance broken = FirstBrokenFurniture(number);
            int repairCost = broken != null ? FurnitureCatalog.Get(broken.kindId).repairCost : 0;
            int repairDays = broken != null ? FurnitureCatalog.Get(broken.kindId).repairDays : 0;
            _roomHeadline.text = "<color=#C65B46>$0</color> / "
                                 + L("night", "晚") + "  →  <color=#297A6E>$"
                                 + currentRate + "</color> / " + L("night", "晚");
            _roomDetail.text = broken == null
                ? L("Required furniture is blocking this room.", "必要家具故障，房间目前无法出售。")
                : L(
                    "Repair $" + repairCost + " · " + repairDays
                    + " day(s), or tape it free until tomorrow.",
                    "维修 $" + repairCost + " · " + repairDays
                    + " 天；也可免费胶带应急，但明天会再次故障。");
            SetRoomButton(_roomPrimary, _roomPrimaryLabel,
                L("REPAIR $" + repairCost, "维修 $" + repairCost),
                () => RepairBroken(number), broken != null && !broken.wrecked, Teal);
            SetRoomButton(_roomSecondary, _roomSecondaryLabel, L("TAPE FREE", "免费胶带应急"),
                () => TapeBroken(number), broken != null && !broken.taped, Ink);
            return;
        }

        var plan = RenovationPlan.For(RenovationPlanKind.Economy);
        int targetRate = Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, plan.targetTier);
        int gain = Mathf.Max(0, targetRate - currentRate);
        int cashCost = Sim.QuoteRenovation(RenovationPlanKind.Economy, 1);
        int investment = RoomInvestmentMath.TotalInvestment(
            cashCost, plan.materialsPerRoom, Sim.Materials.UnitPrice);
        int nights = RoomInvestmentMath.SoldNightsToPayback(investment, gain);

        _roomHeadline.text = "$" + currentRate + " / " + L("night", "晚")
                             + "  →  <color=#297A6E>$" + targetRate + "</color> / "
                             + L("night", "晚");
        _roomDetail.text = room.state == RoomSimState.Occupied
            ? L("Guest in house. Renovation can start after checkout.",
                "客人正在入住，退房后才能开始装修。")
            : L(
                "Economy upgrade $" + cashCost + " + " + plan.materialsPerRoom
                + " materials · closed " + plan.blockDays + " days · +$" + gain
                + "/night · payback " + nights + " sold nights.",
                "经济装修 $" + cashCost + " + " + plan.materialsPerRoom
                + " 材料 · 停业 " + plan.blockDays + " 天 · 每晚增收 $" + gain
                + " · 约 " + nights + " 个售出夜回本。");
        bool canRenovate = room.state != RoomSimState.Occupied && gain > 0;
        SetRoomButton(_roomPrimary, _roomPrimaryLabel,
            L("START $" + cashCost, "开始装修 $" + cashCost),
            () => StartRenovation(number, RenovationPlanKind.Economy),
            canRenovate,
            Teal);
        SetRoomButton(_roomSecondary, _roomSecondaryLabel, L("COMPARE PLANS", "比较方案"),
            OpenAssetsForSelectedRoom, true, Ink);
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
            + ServiceCapacityModel.CheckInsPerHour(Sim.Staff).ToString("0.0") + "/h"
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

        AddActionButton(L("SAVE / LOAD", "存档 / 读档"), SaveSlotPanel.Toggle, Slate, true);
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
        _roomCard.gameObject.SetActive(!open
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
        _refreshTimer = 0f;
    }

    private void ForceDrawerRefresh()
    {
        _drawerStateKey = "";
        _refreshTimer = 0f;
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
        BuildBottomNavigation();
        BuildDrawer();
        BuildRoomCard();
        BuildToast();
        UpdateSafeArea(force: true);
        SetDrawerOpen(false);
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

        _cashLabel = CreateText("Cash", _topPanel, 27f, Gold,
            TextAlignmentOptions.TopRight, true);
        SetAnchors(_cashLabel.rectTransform, new Vector2(0.62f, 0.48f), new Vector2(1f, 1f),
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
