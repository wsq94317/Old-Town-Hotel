using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LoungeScreenController : MonoBehaviour
{
    [Header("Sub-views")]
    [SerializeField] private TopBarView topBar;
    [SerializeField] private HeroBannerView heroBanner;

    [Header("Inventory cards (must be 6, in this order: cleanCups, dirtyCups, milk, tea, coffee, syrup)")]
    [SerializeField] private InventoryCardView cleanCupsCard;
    [SerializeField] private InventoryCardView dirtyCupsCard;
    [SerializeField] private InventoryCardView milkCard;
    [SerializeField] private InventoryCardView teaCard;
    [SerializeField] private InventoryCardView coffeeCard;
    [SerializeField] private InventoryCardView syrupCard;

    [Header("Inventory icons (drag matching sprites)")]
    [SerializeField] private Sprite iconCleanCup;
    [SerializeField] private Sprite iconDirtyCup;
    [SerializeField] private Sprite iconMilk;
    [SerializeField] private Sprite iconTea;
    [SerializeField] private Sprite iconCoffee;
    [SerializeField] private Sprite iconSyrup;

    [Header("Dishwasher")]
    [SerializeField] private DishwasherCardView dishwasherCard;

    [Header("Quick actions (welcomeGuest disabled-by-design; refill disabled-by-design)")]
    [SerializeField] private QuickActionButtonView welcomeGuestButton;
    [SerializeField] private QuickActionButtonView washCupsButton;
    [SerializeField] private QuickActionButtonView refillButton;

    [Header("Quick-action icons")]
    [SerializeField] private Sprite iconWelcomeGuest;
    [SerializeField] private Sprite iconWashCups;
    [SerializeField] private Sprite iconRefill;

    [Header("Gameplay sources")]
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DDayPhaseStateMachine phaseStateMachine;
    [SerializeField] private FrontDesk2D frontDesk;
    [SerializeField] private Lounge2D lounge;

    [Header("Inventory max values (per item)")]
    [SerializeField] private int cleanCupsMax = 20;
    [SerializeField] private int dirtyCupsMax = 20;
    [SerializeField] private int milkMax = 30;
    [SerializeField] private int teaMax = 30;
    [SerializeField] private int coffeeMax = 30;
    [SerializeField] private int syrupMax = 30;

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.25f;

    public event Action OnWashCupsRequested;
    public event Action OnWelcomeGuestRequested;
    public event Action OnRefillRequested;
    public event Action OnSettingsRequested;

    private float lastRefreshTime;

    private void Awake()
    {
        if (washCupsButton != null) washCupsButton.OnClicked += HandleWashCups;
        if (welcomeGuestButton != null) welcomeGuestButton.OnClicked += HandleWelcomeGuest;
        if (refillButton != null) refillButton.OnClicked += HandleRefill;
        if (topBar != null) topBar.OnSettingsClicked += HandleSettingsClicked;
    }

    private void OnDestroy()
    {
        if (washCupsButton != null) washCupsButton.OnClicked -= HandleWashCups;
        if (welcomeGuestButton != null) welcomeGuestButton.OnClicked -= HandleWelcomeGuest;
        if (refillButton != null) refillButton.OnClicked -= HandleRefill;
        if (topBar != null) topBar.OnSettingsClicked -= HandleSettingsClicked;
    }

    private void OnEnable()
    {
        if (heroBanner != null) heroBanner.SetTab(HotelTab.Lounge);
        // Mark designed-not-built actions as visibly disabled (per ui-spec.md §3.6.3)
        if (welcomeGuestButton != null) welcomeGuestButton.SetInteractable(false);
        if (refillButton != null) refillButton.SetInteractable(false);
        ForceRefresh();
    }

    private void Update()
    {
        if (Time.unscaledTime - lastRefreshTime >= refreshIntervalSeconds)
            ForceRefresh();
    }

    public void ForceRefresh()
    {
        lastRefreshTime = Time.unscaledTime;
        RefreshTopBar();
        RefreshInventory();
        RefreshDishwasher();
        RefreshQuickActions();
    }

    private void RefreshTopBar()
    {
        if (topBar == null) return;
        if (dayController != null) topBar.SetDay(dayController.CurrentDay);
        if (dayController != null) topBar.SetMoney(dayController.PlayerCash);
        if (phaseStateMachine != null) topBar.SetTimeLabel(phaseStateMachine.CurrentTimeOfDayLabel);
        if (frontDesk != null) topBar.SetMoodPercent(frontDesk.SatisfactionScore);
    }

    private void RefreshInventory()
    {
        if (lounge == null) return;
        int lowThreshold = lounge.lowStockThreshold;
        int lowCupThreshold = lounge.lowCleanCupThreshold;

        if (cleanCupsCard != null) cleanCupsCard.Bind(iconCleanCup, "干净杯子",  lounge.cleanCups, cleanCupsMax, lowCupThreshold);
        if (dirtyCupsCard != null) dirtyCupsCard.Bind(iconDirtyCup, "脏杯子",    lounge.dirtyCups, dirtyCupsMax, dirtyCupsMax + 1); // never low for dirty
        if (milkCard != null)      milkCard.Bind(iconMilk,           "牛奶",     lounge.milkStock, milkMax, lowThreshold);
        if (teaCard != null)       teaCard.Bind(iconTea,             "茶叶",     lounge.teaStock, teaMax, lowThreshold);
        if (coffeeCard != null)    coffeeCard.Bind(iconCoffee,       "咖啡豆",   lounge.coffeeStock, coffeeMax, lowThreshold);
        if (syrupCard != null)     syrupCard.Bind(iconSyrup,         "糖浆",     lounge.syrupStock, syrupMax, lowThreshold);
    }

    private void RefreshDishwasher()
    {
        if (dishwasherCard == null || lounge == null) return;
        dishwasherCard.Bind(lounge.washing, lounge.washTimerSeconds, lounge.washDurationSeconds);
    }

    private void RefreshQuickActions()
    {
        if (washCupsButton != null) washCupsButton.Bind(iconWashCups, "清洗杯子", true);
        if (welcomeGuestButton != null) welcomeGuestButton.Bind(iconWelcomeGuest, "招待客人", false);
        if (refillButton != null) refillButton.Bind(iconRefill, "补充库存", false);
    }

    private void HandleWashCups() => OnWashCupsRequested?.Invoke();
    private void HandleWelcomeGuest() => OnWelcomeGuestRequested?.Invoke();
    private void HandleRefill() => OnRefillRequested?.Invoke();
    private void HandleSettingsClicked() => OnSettingsRequested?.Invoke();
}
