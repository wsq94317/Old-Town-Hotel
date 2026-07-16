using UnityEngine;

// Gathers state from the economy + renovation systems into a GameState, autosaves
// on day-end, and loads on boot. v1: single slot, economy progression only.
[DisallowMultipleComponent]
public sealed class SaveCoordinator : MonoBehaviour
{
    [SerializeField] private EconomySystem economy;
    [SerializeField] private RenovationSystem renovation;
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private bool autoLoadOnStart = true;
    [SerializeField] private bool autoSaveOnDayEnd = true;

    private void Awake()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (renovation == null) renovation = FindFirstObjectByType<RenovationSystem>();
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
    }

    // Subscribe in Start so RenovationSystem (subscribes in Awake) ticks the day
    // BEFORE we capture — the autosave then reflects post-tick tiers.
    private void Start()
    {
        if (dayController != null) dayController.OnDaySettled += HandleDaySettled;
        if (autoLoadOnStart && SaveService.HasSave()) LoadGame();
    }

    private void OnDestroy()
    {
        if (dayController != null) dayController.OnDaySettled -= HandleDaySettled;
    }

    public GameState Capture()
    {
        var gs = new GameState();
        if (economy != null) gs.economy = economy.CaptureState();
        if (renovation != null) gs.renovation = renovation.CaptureState();
        gs.progress.day = dayController != null ? dayController.CurrentDay : 0;
        gs.progress.satisfaction = demandLoop != null ? demandLoop.prototypeSatisfactionScore : 0;
        gs.rooms = demandLoop != null ? demandLoop.CaptureOccupancy() : new RoomsState();
        return gs;
    }

    public void SaveGame() => SaveService.Save(Capture());

    public void LoadGame()
    {
        var gs = SaveService.Load();
        if (gs == null) return;
        if (economy != null) economy.RestoreState(gs.economy);
        if (renovation != null) renovation.RestoreState(gs.renovation);
        if (dayController != null) dayController.demoDayIndex = gs.progress.day;
        if (demandLoop != null) demandLoop.prototypeSatisfactionScore = gs.progress.satisfaction;
        // 过夜占用（save v2）：此时是 Start 阶段，早于控制器首帧 TickClock 触发的
        // 晨间退房潮——恢复必然先于退房潮（wave 因此从 EnterPreparationPhase 移出）。
        if (demandLoop != null) demandLoop.RestoreOccupancy(gs.rooms);
    }

    public void NewGame() => SaveService.Delete();

    private void HandleDaySettled(int day, int served, DayLedger ledger)
    {
        if (autoSaveOnDayEnd) SaveGame();
    }
}
