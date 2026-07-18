using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomsScreenController : MonoBehaviour
{
    [Header("Sub-views")]
    [SerializeField] private TopBarView topBar;
    [SerializeField] private HeroBannerView heroBanner;
    [SerializeField] private Transform tileGridRoot;
    [SerializeField] private RoomTileView tilePrefab;
    [SerializeField] private WorkerStatusCardView hskCard;
    [SerializeField] private WorkerStatusCardView inspCard;

    [Header("Worker portraits")]
    [SerializeField] private Sprite hskPortrait;
    [SerializeField] private Sprite inspPortrait;

    [Header("Gameplay sources")]
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DDayPhaseStateMachine phaseStateMachine;
    [SerializeField] private FrontDesk2D frontDesk;
    [SerializeField] private Housekeeper2D housekeeper;
    [SerializeField] private Inspector2D inspector;
    [SerializeField] private Room2DEntity[] rooms;

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.25f;

    public event Action<Room2DEntity> OnRoomTileTapped;
    public event Action OnHskDetailsRequested;
    public event Action OnInspDetailsRequested;
    public event Action OnSettingsRequested;

    private float lastRefreshTime;
    private readonly List<RoomTileView> spawnedTiles = new List<RoomTileView>();

    private void Awake()
    {
        ApplyGeneratedPortraits();
        if (hskCard != null) hskCard.OnDetailsClicked += HandleHskDetails;
        if (inspCard != null) inspCard.OnDetailsClicked += HandleInspDetails;
        if (topBar != null) topBar.OnSettingsClicked += HandleSettingsClicked;
    }

    private void OnDestroy()
    {
        if (hskCard != null) hskCard.OnDetailsClicked -= HandleHskDetails;
        if (inspCard != null) inspCard.OnDetailsClicked -= HandleInspDetails;
        if (topBar != null) topBar.OnSettingsClicked -= HandleSettingsClicked;
        foreach (var tile in spawnedTiles)
        {
            if (tile != null) tile.OnTapped -= HandleTileTapped;
        }
    }

    private void OnEnable()
    {
        if (heroBanner != null) heroBanner.SetTab(HotelTab.Rooms);
        EnsureTiles();
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
        RefreshTiles();
        RefreshWorkerCards();
    }

    private void EnsureTiles()
    {
        if (tilePrefab == null || tileGridRoot == null || rooms == null) return;
        while (spawnedTiles.Count < rooms.Length)
        {
            var tile = Instantiate(tilePrefab, tileGridRoot);
            tile.OnTapped += HandleTileTapped;
            spawnedTiles.Add(tile);
        }
        for (int i = 0; i < rooms.Length; i++)
        {
            spawnedTiles[i].Bind(rooms[i]);
        }
    }

    private void RefreshTopBar()
    {
        if (topBar == null) return;
        if (dayController != null) topBar.SetDay(dayController.CurrentDay);
        if (dayController != null) topBar.SetMoney(dayController.PlayerCash);
        if (phaseStateMachine != null) topBar.SetTimeLabel(phaseStateMachine.CurrentTimeOfDayLabel);
        if (frontDesk != null) topBar.SetMoodPercent(frontDesk.SatisfactionScore);
    }

    private void RefreshTiles()
    {
        for (int i = 0; i < spawnedTiles.Count; i++)
        {
            var tile = spawnedTiles[i];
            if (tile == null) continue;
            tile.Refresh();
            var room = tile.BoundRoom;
            if (room == null) { tile.SetTimerText(string.Empty); continue; }

            string timerText = ComputeTimerForRoom(room);
            tile.SetTimerText(timerText);
        }
    }

    private string ComputeTimerForRoom(Room2DEntity room)
    {
        if (room == null) return string.Empty;
        int roomNumber = room.roomNumber;

        if (housekeeper != null && housekeeper.IsBusy && housekeeper.AssignedRoomNumber == roomNumber)
            return FormatRemaining(housekeeper.RemainingSeconds);
        if (inspector != null && inspector.IsBusy && inspector.AssignedRoomNumber == roomNumber)
            return FormatRemaining(inspector.RemainingSeconds);
        return string.Empty;
    }

    private static string FormatRemaining(float seconds)
    {
        if (seconds <= 0f) return string.Empty;
        if (seconds < 60f) return $"{Mathf.CeilToInt(seconds)}s";
        int minutes = Mathf.CeilToInt(seconds / 60f);
        return $"{minutes}m";
    }

    private void RefreshWorkerCards()
    {
        if (hskCard != null && housekeeper != null)
        {
            hskCard.Bind(hskPortrait, "HOUSEKEEPER (HSK)",
                         housekeeper.CurrentActivityLabel,
                         housekeeper.AssignedRoomNumber,
                         housekeeper.RemainingSeconds,
                         housekeeper.IsBusy);
        }
        if (inspCard != null && inspector != null)
        {
            inspCard.Bind(inspPortrait, "INSPECTOR (INSP)",
                          inspector.CurrentActivityLabel,
                          inspector.AssignedRoomNumber,
                          inspector.RemainingSeconds,
                          inspector.IsBusy);
        }
    }

    private void ApplyGeneratedPortraits()
    {
        hskPortrait = GeneratedPlaceholderArt.WorkerPortrait(StaffRole.Housekeeper) ?? hskPortrait;
        inspPortrait = GeneratedPlaceholderArt.WorkerPortrait(StaffRole.Inspector) ?? inspPortrait;
    }

    private void HandleTileTapped(RoomTileView tile) => OnRoomTileTapped?.Invoke(tile.BoundRoom);
    private void HandleHskDetails() => OnHskDetailsRequested?.Invoke();
    private void HandleInspDetails() => OnInspDetailsRequested?.Invoke();
    private void HandleSettingsClicked() => OnSettingsRequested?.Invoke();
}
