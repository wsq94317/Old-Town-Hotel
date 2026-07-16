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

    [Header("Guest portraits by type")]
    [SerializeField] private Sprite portraitBusiness;
    [SerializeField] private Sprite portraitFamily;
    [SerializeField] private Sprite portraitVip;

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
        // Mockup-era placeholder cards are baked into the scene under the queue
        // root; clear them so only live-bound cards render.
        if (queueListRoot != null)
            for (int i = queueListRoot.childCount - 1; i >= 0; i--)
                Destroy(queueListRoot.GetChild(i).gameObject);
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
        if (activeGuestCard == null || demandLoop == null) return;

        bool complaint = demandLoop.complaintWaitingForReassignment;
        if (!demandLoop.activeDemandWaitingForManualAssignment && !complaint)
        {
            activeGuestCard.BindEmpty(NextArrivalText());
            return;
        }

        var type = demandLoop.activeGuestType;
        float waitSeconds = demandLoop.activeDemandWaitSeconds;
        activeGuestCard.Bind(
            demandLoop,
            PortraitFor(type),
            $"{type.ToString().ToUpperInvariant()} GUEST",
            demandLoop.activeDemandRoomPreference == Room2DPrototypeDemandLoop.Room2DRoomPreference.BetterRoomPreferred
                ? "BETTER ROOM" : "ANY ROOM",
            BuildPreferenceText(),
            $"Waiting: {Mathf.FloorToInt(waitSeconds)}s",
            $"Mood: {MoodName(waitSeconds)}",
            complaint ? "Note: Complaint — wants a different room" : string.Empty,
            "Check Available Rooms");
    }

    private readonly List<GuestQueueCardInfo> queueInfos = new List<GuestQueueCardInfo>();

    private void RefreshQueue()
    {
        if (demandLoop == null || queueCardPrefab == null || queueListRoot == null) return;
        queueInfos.Clear();

        // 晨间退房卡（day-cycle v2）：排在到店客人前面，点卡即办退房；
        // 不点的话客人到点自己离开（错峰兜底）。guestRef = Room2DEntity。
        int departures = demandLoop.DepartureCount;
        for (int i = 0; i < departures; i++)
        {
            var room = demandLoop.GetDepartureRoom(i);
            if (room == null) continue;
            queueInfos.Add(new GuestQueueCardInfo
            {
                guestRef = room,
                portrait = PortraitFor(demandLoop.GetDepartureGuestType(i)),
                typeLabel = "CHECKING OUT",
                needLabel = room.roomName.ToUpperInvariant(),
                waitText = "tap to check out",
                mood = GuestPatienceState.Calm,
            });
        }

        int count = demandLoop.UpcomingQueueCount;
        for (int i = 0; i < count; i++)
        {
            var type = demandLoop.GetUpcomingGuestType(i);
            queueInfos.Add(new GuestQueueCardInfo
            {
                guestRef = i,
                portrait = PortraitFor(type),
                typeLabel = type.ToString().ToUpperInvariant(),
                needLabel = BedNeedText(demandLoop.GetUpcomingBedTypePreference(i)),
                waitText = i == 0 && demandLoop.upcomingDemandEtaSeconds > 0f
                    ? $"in {Mathf.CeilToInt(demandLoop.upcomingDemandEtaSeconds)}s"
                    : "queued",
                mood = GuestPatienceState.Calm,
            });
        }
        PushQueue(queueInfos);
    }

    private Sprite PortraitFor(Room2DGuestType type)
    {
        switch (type)
        {
            case Room2DGuestType.Family: return portraitFamily;
            case Room2DGuestType.VIP:    return portraitVip;
            default:                     return portraitBusiness;
        }
    }

    private string BuildPreferenceText()
    {
        string floor = demandLoop.activeDemandFloorPreference switch
        {
            Room2DPrototypeDemandLoop.Room2DFloorPreference.HighFloorPreferred => "High floor",
            Room2DPrototypeDemandLoop.Room2DFloorPreference.LowFloorPreferred => "Low floor",
            _ => null,
        };
        string facing = demandLoop.activeDemandFacingPreference switch
        {
            Room2DPrototypeDemandLoop.Room2DFacingPreference.QuietPreferred => "Quiet side",
            Room2DPrototypeDemandLoop.Room2DFacingPreference.ViewPreferred => "View side",
            _ => null,
        };
        if (floor == null && facing == null) return "Prefers: Anything";
        if (floor != null && facing != null) return $"Prefers: {floor}, {facing}";
        return $"Prefers: {floor ?? facing}";
    }

    private static string MoodName(float waitSeconds)
        => waitSeconds < 20f ? "Normal" : waitSeconds < 45f ? "Impatient" : "Angry";

    private static string BedNeedText(Room2DBedTypePreference pref)
        => pref == Room2DBedTypePreference.Any ? "ANY ROOM" : $"{pref.ToString().ToUpperInvariant()} ROOM";

    private string NextArrivalText()
    {
        if (demandLoop != null && demandLoop.upcomingDemandEtaSeconds > 0f)
            return $"No guest at the desk — next arrives in {Mathf.CeilToInt(demandLoop.upcomingDemandEtaSeconds)}s";
        return "No guest at the desk";
    }

    public void PushQueue(IList<GuestQueueCardInfo> entries)
    {
        EnsureQueueCardCount(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var info = entries[i];
            var card = spawnedCards[i];
            card.gameObject.SetActive(true);
            card.Bind(info.guestRef, info.portrait, info.typeLabel, info.needLabel, info.waitText, info.mood);
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
    public string waitText;
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
