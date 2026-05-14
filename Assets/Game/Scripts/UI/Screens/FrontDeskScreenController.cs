using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FrontDeskScreenController : MonoBehaviour
{
    [Header("Sub-views")]
    [SerializeField] private TopBarView topBar;
    [SerializeField] private HeroBannerView heroBanner;
    [SerializeField] private ActiveGuestCardView activeGuestCard;
    [SerializeField] private Transform queueListRoot;
    [SerializeField] private GuestQueueCardView queueCardPrefab;

    [Header("Gameplay sources")]
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DDayPhaseStateMachine phaseStateMachine;
    [SerializeField] private FrontDesk2D frontDesk;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.25f;

    public event Action OnViewAvailableRoomsRequested;
    public event Action OnSettingsRequested;
    public event Action<object> OnQueueCardTapped;

    private float lastRefreshTime;
    private readonly List<GuestQueueCardView> spawnedCards = new List<GuestQueueCardView>();

    private void Awake()
    {
        if (activeGuestCard != null) activeGuestCard.OnCtaClicked += HandleCtaClicked;
        if (topBar != null) topBar.OnSettingsClicked += HandleSettingsClicked;
    }

    private void OnDestroy()
    {
        if (activeGuestCard != null) activeGuestCard.OnCtaClicked -= HandleCtaClicked;
        if (topBar != null) topBar.OnSettingsClicked -= HandleSettingsClicked;
        DestroyAllQueueCards();
    }

    private void OnEnable()
    {
        if (heroBanner != null) heroBanner.SetTab(HotelTab.FrontDesk);
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
        RefreshActiveGuest();
        RefreshQueue();
    }

    private void RefreshTopBar()
    {
        if (topBar == null) return;
        if (dayController != null) topBar.SetDay(dayController.CurrentDay);
        if (dayController != null) topBar.SetMoney(dayController.PlayerCash);
        if (phaseStateMachine != null) topBar.SetTimeLabel(phaseStateMachine.CurrentTimeOfDayLabel);
        if (frontDesk != null) topBar.SetMoodPercent(frontDesk.SatisfactionScore);
    }

    private void RefreshActiveGuest()
    {
        if (activeGuestCard == null) return;

        // Active guest plumbing: presenter-side stubbed for now.
        // The demand loop owns the active guest concept; binding is filled by HotelUIFlow when guest data is available.
        // This method is a placeholder so the screen renders without crashing.
    }

    private void RefreshQueue()
    {
        // Queue plumbing: same as above — HotelUIFlow will push queue data via PushQueue() when wired.
    }

    public void PushQueue(IList<GuestQueueCardInfo> entries)
    {
        EnsureQueueCardCount(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var info = entries[i];
            var card = spawnedCards[i];
            card.gameObject.SetActive(true);
            card.Bind(info.guestRef, info.portrait, info.typeLabel, info.needLabel, info.waitMinutes, info.mood);
        }
        for (int i = entries.Count; i < spawnedCards.Count; i++)
        {
            spawnedCards[i].gameObject.SetActive(false);
        }
    }

    public void PushActiveGuest(ActiveGuestCardInfo info)
    {
        if (activeGuestCard == null) return;
        if (info == null) { activeGuestCard.BindEmpty(); return; }
        activeGuestCard.Bind(info.guestRef, info.portrait, info.typeLabel, info.requiredRoomLabel,
                             info.preferenceText, info.waitText, info.moodText, info.notesText, info.ctaLabel);
    }

    private void EnsureQueueCardCount(int count)
    {
        if (queueCardPrefab == null || queueListRoot == null) return;
        while (spawnedCards.Count < count)
        {
            var card = Instantiate(queueCardPrefab, queueListRoot);
            card.OnTapped += HandleQueueCardTapped;
            spawnedCards.Add(card);
        }
    }

    private void DestroyAllQueueCards()
    {
        foreach (var card in spawnedCards)
        {
            if (card == null) continue;
            card.OnTapped -= HandleQueueCardTapped;
            if (card.gameObject != null) Destroy(card.gameObject);
        }
        spawnedCards.Clear();
    }

    private void HandleQueueCardTapped(GuestQueueCardView card) => OnQueueCardTapped?.Invoke(card.BoundGuestRef);
    private void HandleCtaClicked() => OnViewAvailableRoomsRequested?.Invoke();
    private void HandleSettingsClicked() => OnSettingsRequested?.Invoke();
}

public sealed class GuestQueueCardInfo
{
    public object guestRef;
    public Sprite portrait;
    public string typeLabel;
    public string needLabel;
    public int waitMinutes;
    public GuestPatienceState mood;
}

public sealed class ActiveGuestCardInfo
{
    public object guestRef;
    public Sprite portrait;
    public string typeLabel;
    public string requiredRoomLabel;
    public string preferenceText;
    public string waitText;
    public string moodText;
    public string notesText;
    public string ctaLabel;
}
