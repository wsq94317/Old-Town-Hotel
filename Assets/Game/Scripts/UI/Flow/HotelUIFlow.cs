using UnityEngine;

[DisallowMultipleComponent]
public sealed class HotelUIFlow : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] private FrontDeskScreenController frontDeskScreen;
    [SerializeField] private RoomsScreenController roomsScreen;
    [SerializeField] private LoungeScreenController loungeScreen;
    [SerializeField] private BottomNavView bottomNav;

    [Header("Modal manager")]
    [SerializeField] private ModalManager modalManager;

    [Header("Modal prefabs")]
    [SerializeField] private ChooseRoomModal chooseRoomModalPrefab;
    [SerializeField] private GuestDetailsModal guestDetailsModalPrefab;
    [SerializeField] private RoomActionsModal roomActionsModalPrefab;
    [SerializeField] private AssignHskModal assignHskModalPrefab;
    [SerializeField] private NoHskModal noHskModalPrefab;
    [SerializeField] private DayEndSummaryModal dayEndModalPrefab;
    [SerializeField] private AchievementsModal achievementsModalPrefab;

    [Header("Gameplay refs")]
    [SerializeField] private Housekeeper2D housekeeper; // legacy single-HSK fallback when no StaffCrew
    [SerializeField] private Inspector2D inspector;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private StaffCrew staffCrew;   // ADR 0008 multi-staff dispatch
    [SerializeField] private BossCover bossCover;   // "Do It Yourself"

    [Header("Manager Office")]
    [SerializeField] private GameObject managerOfficeScreen;
    [SerializeField] private ManagerOfficeScreenController managerOffice;
    [SerializeField] private UnityEngine.UI.Button officeEntryButton;

    [Header("Toast")]
    [SerializeField] private ToastView toast;

    private void Awake()
    {
        if (bottomNav != null) bottomNav.OnTabSelected += HandleTabSelected;

        if (frontDeskScreen != null)
        {
            frontDeskScreen.OnViewAvailableRoomsRequested += HandleViewAvailableRooms;
            frontDeskScreen.OnQueueCardTapped += HandleQueueCardTapped;
            frontDeskScreen.OnSettingsRequested += HandleSettingsRequested;
        }
        if (roomsScreen != null)
        {
            roomsScreen.OnRoomTileTapped += HandleRoomTileTapped;
            roomsScreen.OnHskDetailsRequested += HandleHskDetailsRequested;
            roomsScreen.OnInspDetailsRequested += HandleInspDetailsRequested;
            roomsScreen.OnSettingsRequested += HandleSettingsRequested;
        }
        if (loungeScreen != null)
        {
            loungeScreen.OnSettingsRequested += HandleSettingsRequested;
        }
        if (dayController != null)
        {
            dayController.OnDaySettled += HandleDaySettled;
        }
        if (demandLoop != null)
        {
            demandLoop.OnDepartureCheckedOut += HandleDepartureCheckedOut;
        }
        if (officeEntryButton != null) officeEntryButton.onClick.AddListener(OpenManagerOffice);
        if (managerOffice != null) managerOffice.OnCloseRequested += CloseManagerOffice;
    }

    private void OnDestroy()
    {
        if (bottomNav != null) bottomNav.OnTabSelected -= HandleTabSelected;
        if (frontDeskScreen != null)
        {
            frontDeskScreen.OnViewAvailableRoomsRequested -= HandleViewAvailableRooms;
            frontDeskScreen.OnQueueCardTapped -= HandleQueueCardTapped;
            frontDeskScreen.OnSettingsRequested -= HandleSettingsRequested;
        }
        if (roomsScreen != null)
        {
            roomsScreen.OnRoomTileTapped -= HandleRoomTileTapped;
            roomsScreen.OnHskDetailsRequested -= HandleHskDetailsRequested;
            roomsScreen.OnInspDetailsRequested -= HandleInspDetailsRequested;
            roomsScreen.OnSettingsRequested -= HandleSettingsRequested;
        }
        if (loungeScreen != null)
        {
            loungeScreen.OnSettingsRequested -= HandleSettingsRequested;
        }
        if (dayController != null)
        {
            dayController.OnDaySettled -= HandleDaySettled;
        }
        if (demandLoop != null)
        {
            demandLoop.OnDepartureCheckedOut -= HandleDepartureCheckedOut;
        }
        if (officeEntryButton != null) officeEntryButton.onClick.RemoveListener(OpenManagerOffice);
        if (managerOffice != null) managerOffice.OnCloseRequested -= CloseManagerOffice;
    }

    private void OpenManagerOffice()
    {
        if (managerOfficeScreen != null) managerOfficeScreen.SetActive(true);
    }

    private void CloseManagerOffice()
    {
        if (managerOfficeScreen != null) managerOfficeScreen.SetActive(false);
    }

    // 日结：弹出 Day-End 损益（收入/工资/净利）。
    private void HandleDaySettled(int day, int servedGuests, DayLedger ledger)
    {
        if (modalManager == null || dayEndModalPrefab == null) return;
        int sat = demandLoop != null ? demandLoop.prototypeSatisfactionScore : 0;
        var modal = modalManager.Show(dayEndModalPrefab);
        modal.Setup(day, servedGuests, ledger.Income, ledger.Wages, ledger.Interest, sat, sat, null);
        modal.OnContinueClicked += HandleContinueToNextDay;
    }

    // Day-end Continue: roll the calendar back to 8:00 Preparation — the morning
    // checkout wave and the 10:00 doors-open are both driven by the GameClock.
    private void HandleContinueToNextDay()
    {
        if (dayController == null) return;
        dayController.RestartDemoDay();
    }

    private void Start()
    {
        SwitchToTab(HotelTab.FrontDesk);
        // day-cycle v2: the day flows on the GameClock (8:00 Preparation starts
        // automatically) — the old auto-start coroutine is gone.
    }

    public void SwitchToTab(HotelTab tab)
    {
        if (frontDeskScreen != null) frontDeskScreen.gameObject.SetActive(tab == HotelTab.FrontDesk);
        if (roomsScreen != null)     roomsScreen.gameObject.SetActive(tab == HotelTab.Rooms);
        if (loungeScreen != null)    loungeScreen.gameObject.SetActive(tab == HotelTab.Lounge);
        if (bottomNav != null)       bottomNav.SetActiveTab(tab);
        if (modalManager != null)    modalManager.CloseAll();
    }

    // --- Front Desk handlers ---

    private void HandleTabSelected(HotelTab tab) => SwitchToTab(tab);

    private void HandleViewAvailableRooms()
    {
        if (modalManager == null || chooseRoomModalPrefab == null) return;
        var modal = modalManager.Show(chooseRoomModalPrefab);
        // Any = every Ready room is Suitable; bed-type soft prefs land with the
        // per-guest plumbing (the demand system doesn't carry a bed preference yet).
        var readyRooms = demandLoop != null
            ? new System.Collections.Generic.List<RoomSuitability>(demandLoop.GetReadyRoomsForGuest(Room2DBedTypePreference.Any))
            : new System.Collections.Generic.List<RoomSuitability>();
        modal.Setup(readyRooms);
        modal.OnGotoRoomsRequested += () => SwitchToTab(HotelTab.Rooms);
        modal.OnRoomSelected += HandleRoomSelectedForActiveGuest;
    }

    private void HandleRoomSelectedForActiveGuest(Room2DEntity room)
    {
        // Real check-in: occupies the room, records the match-quality outcome
        // (settled as revenue at checkout), and handles complaint reassignment.
        bool ok = demandLoop != null && room != null && demandLoop.AssignRoomToActiveDemand(room);
        if (toast != null)
        {
            toast.Show(ok ? $"Checked in — Room {room.roomNumber}"
                          : demandLoop != null ? demandLoop.lastManualAssignmentResult : "Check-in failed");
        }
    }

    private void HandleQueueCardTapped(object guestRef)
    {
        // 退房卡（guestRef 是 Room2DEntity）：点卡即办退房，toast 由
        // OnDepartureCheckedOut 事件统一弹（自动离开也走同一条）。
        if (guestRef is Room2DEntity departureRoom)
        {
            if (demandLoop != null) demandLoop.TryCheckOutDeparture(departureRoom);
            return;
        }

        if (modalManager == null || guestDetailsModalPrefab == null) return;
        var modal = modalManager.Show(guestDetailsModalPrefab);
        modal.Setup(guestRef, null,
                    "—", "—", "Prefers: —", "Waiting: —", "Mood: —",
                    "Budget: —", "Stay: —");
        modal.OnAssignRoomClicked += HandleViewAvailableRooms;
    }

    // 退房完成（点卡或客人自行离开）：飘一条入账 toast。
    private void HandleDepartureCheckedOut(Room2DEntity room, int amount, bool byPlayer)
    {
        if (toast == null || room == null) return;
        toast.Show(amount > 0
            ? $"Room {room.roomNumber} checked out · +${amount}"
            : $"Room {room.roomNumber} checked out");
    }

    private void HandleSettingsRequested()
    {
        if (modalManager == null || achievementsModalPrefab == null) return;
        var modal = modalManager.Show(achievementsModalPrefab);
        modal.Setup(null); // empty registry shows placeholder
    }

    // --- Rooms handlers ---

    private void HandleRoomTileTapped(Room2DEntity room)
    {
        if (modalManager == null || roomActionsModalPrefab == null || room == null) return;
        var modal = modalManager.Show(roomActionsModalPrefab);
        modal.Setup(room, "Floor: —", "Dirty since: —", "Priority: Normal");
        modal.OnAssignHskClicked += () => HandleAssignHskRequested(room);
        modal.OnDoItYourselfClicked += () => HandleBossCleanRequested(room);
    }

    // Route through StaffCrew when present (any idle worker); legacy single-HSK path otherwise.
    private Housekeeper2D PickIdleHousekeeper()
    {
        if (staffCrew != null) return staffCrew.FindIdleHousekeeper();
        return housekeeper != null && !housekeeper.IsBusy ? housekeeper : null;
    }

    private void HandleAssignHskRequested(Room2DEntity room)
    {
        if (modalManager == null || room == null) return;

        Housekeeper2D worker = PickIdleHousekeeper();
        if (worker == null)
        {
            if (noHskModalPrefab != null)
            {
                var noHsk = modalManager.Show(noHskModalPrefab);
                noHsk.Setup();
            }
            return;
        }

        if (assignHskModalPrefab == null) return;
        var modal = modalManager.Show(assignHskModalPrefab);
        modal.Setup(room.roomNumber, worker.CurrentActivityLabel, worker.cleaningDurationSeconds);
        modal.OnConfirmed += () =>
        {
            // Re-pick at confirm time — the worker chosen at modal-open may have been taken.
            Housekeeper2D confirmed = PickIdleHousekeeper();
            bool ok = confirmed != null && confirmed.AssignRoom(room);
            if (toast != null)
                toast.Show(ok ? $"HSK dispatched to Room {room.roomNumber}" : "No available HSK");
        };
    }

    // "Do It Yourself" — the boss covers the cleaning himself (free, slow, one at a time).
    private void HandleBossCleanRequested(Room2DEntity room)
    {
        if (room == null || bossCover == null) return;
        if (bossCover.IsBusy)
        {
            if (toast != null) toast.Show(bossCover.BusyLabel);
            return;
        }
        bool ok = bossCover.TryCleanRoom(room);
        if (toast != null)
            toast.Show(ok ? $"You start cleaning Room {room.roomNumber}…" : "Can't clean that room right now");
    }

    private void HandleHskDetailsRequested()
    {
        if (toast != null) toast.Show("HSK status: " + (housekeeper != null ? housekeeper.CurrentActivityLabel : "Idle"));
    }

    private void HandleInspDetailsRequested()
    {
        if (toast != null) toast.Show("INSP status: " + (inspector != null ? inspector.CurrentActivityLabel : "Idle"));
    }
}
