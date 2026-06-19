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
    }

    public void NewGame() => SaveService.Delete();

    private void HandleDaySettled(int day, int served, DayLedger ledger)
    {
        if (autoSaveOnDayEnd) SaveGame();
    }
}
