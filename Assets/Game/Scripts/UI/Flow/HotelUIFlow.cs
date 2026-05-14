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
    [SerializeField] private Housekeeper2D housekeeper;
    [SerializeField] private Inspector2D inspector;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;

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
    }

    private void Start()
    {
        SwitchToTab(HotelTab.FrontDesk);
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
        // Soft-prefs not modelled yet — feeding an empty list as a placeholder so the empty banner renders.
        // When the active guest's bed-type preference is wired through HotelUIFlow, replace with:
        //   demandLoop.GetReadyRoomsForGuest(activeBedTypePreference)
        modal.Setup(new System.Collections.Generic.List<RoomSuitability>());
        modal.OnGotoRoomsRequested += () => SwitchToTab(HotelTab.Rooms);
        modal.OnRoomSelected += HandleRoomSelectedForActiveGuest;
    }

    private void HandleRoomSelectedForActiveGuest(Room2DEntity room)
    {
        if (toast != null) toast.Show($"已选择房间 {room.roomNumber}");
        // TODO: trigger check-in via FrontDesk2D when active-guest plumbing is wired.
    }

    private void HandleQueueCardTapped(object guestRef)
    {
        if (modalManager == null || guestDetailsModalPrefab == null) return;
        var modal = modalManager.Show(guestDetailsModalPrefab);
        modal.Setup(guestRef, null,
                    "—", "—", "偏好: —", "等待: —", "情绪: —",
                    "预算: —", "预计入住: —");
        modal.OnAssignRoomClicked += HandleViewAvailableRooms;
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
        modal.Setup(room, "楼层信息: —", "脏污时长: —", "优先级: 普通");
        modal.OnAssignHskClicked += () => HandleAssignHskRequested(room);
    }

    private void HandleAssignHskRequested(Room2DEntity room)
    {
        if (modalManager == null || room == null) return;

        if (housekeeper != null && housekeeper.IsBusy)
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
        modal.Setup(room.roomNumber, housekeeper != null ? housekeeper.CurrentActivityLabel : "空闲", 45f);
        modal.OnConfirmed += () =>
        {
            // TODO: trigger Housekeeper2D.Assign(room) when the public method exists; currently a gameplay-side gap.
            if (toast != null) toast.Show($"已安排 HSK 前往房间 {room.roomNumber}");
        };
    }

    private void HandleHskDetailsRequested()
    {
        if (toast != null) toast.Show("HSK 详情：" + (housekeeper != null ? housekeeper.CurrentActivityLabel : "空闲"));
    }

    private void HandleInspDetailsRequested()
    {
        if (toast != null) toast.Show("INSP 详情：" + (inspector != null ? inspector.CurrentActivityLabel : "空闲"));
    }
}
