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
    private BreakdownSystem _breakdowns; // v2 经理场景才有；v1 场景为 null
    private GameState _lastLoaded;       // 本场景缺席的系统，其存档段落原样带过（防 v1 场景日结清掉 v2 数据）
    [SerializeField] private bool autoLoadOnStart = true;
    [SerializeField] private bool autoSaveOnDayEnd = true;

    private void Awake()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (renovation == null) renovation = FindFirstObjectByType<RenovationSystem>();
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        _breakdowns = FindFirstObjectByType<BreakdownSystem>();
    }

    // Subscribe in Start so RenovationSystem (subscribes in Awake) ticks the day
    // BEFORE we capture — the autosave then reflects post-tick tiers.
    private void Start()
    {
        if (dayController != null) dayController.OnDaySettled += HandleDaySettled;
        if (autoLoadOnStart && SaveService.HasSave()) LoadGame();
    }

    private bool _roomsSynced;

    // 装修表对齐场景真实房号（v2 房号 2xx/3xx 而序列化默认 101-108；读档也可能带来
    // 别的场景的号）。放首帧 Update：Start 时 demandLoop.rooms 可能还没填（时序坑）。
    private void Update()
    {
        if (_roomsSynced || renovation == null || demandLoop == null || demandLoop.rooms == null) return;
        var numbers = new System.Collections.Generic.List<int>();
        foreach (var r in demandLoop.rooms) if (r != null) numbers.Add(r.roomNumber);
        if (numbers.Count == 0) return; // 还没填好，下帧再试
        numbers.Sort();
        renovation.SyncRooms(numbers);
        _roomsSynced = true;
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
        // v3 世界层：设施解锁 / 威望 / 胶带复发 / 锁房
        FacilitySystem.CaptureTo(gs.world);
        gs.world.prestige = ManagerReputation.Prestige;
        if (_breakdowns != null) _breakdowns.CaptureTo(gs.world);
        else if (_lastLoaded != null && _lastLoaded.world != null)
        {
            // 本场景没有损坏系统（v1 原型场景）：把读进来的跨日数据原样带过，别用空表覆盖
            gs.world.tapedBreakdowns = _lastLoaded.world.tapedBreakdowns;
            gs.world.lockedRooms = _lastLoaded.world.lockedRooms;
        }
        return gs;
    }

    public void SaveGame() => SaveService.Save(Capture());

    public void LoadGame()
    {
        var gs = SaveService.Load();
        if (gs == null) return;
        _lastLoaded = gs;
        if (economy != null) economy.RestoreState(gs.economy);
        if (renovation != null) renovation.RestoreState(gs.renovation);
        if (dayController != null) dayController.demoDayIndex = gs.progress.day;
        if (demandLoop != null) demandLoop.prototypeSatisfactionScore = gs.progress.satisfaction;
        // 过夜占用（save v2）：此时是 Start 阶段，早于控制器首帧 TickClock 触发的
        // 晨间退房潮——恢复必然先于退房潮（wave 因此从 EnterPreparationPhase 移出）。
        if (demandLoop != null) demandLoop.RestoreOccupancy(gs.rooms);
        // v3 世界层（旧档缺省 = 默认 WorldState，安全）
        if (gs.world != null)
        {
            FacilitySystem.RestoreFrom(gs.world);
            ManagerReputation.Restore(gs.world.prestige);
            if (_breakdowns != null) _breakdowns.RestoreFrom(gs.world);
        }
        // 顶栏现金对齐（否则读档当天一直显示序列化默认值）
        if (dayController != null) dayController.SyncCashFromEconomy();
    }

    public void NewGame()
    {
        SaveService.Delete();
        // 静态世界状态一并归零，否则上一局的威望/解锁渗进新档
        ManagerReputation.ResetForNewGame();
        FacilitySystem.ResetForNewGame();
        _lastLoaded = null;
    }

    private void HandleDaySettled(int day, int served, DayLedger ledger)
    {
        if (!autoSaveOnDayEnd) return;
        // 日结时 demoDayIndex 还没 ++（Continue 才推进）——存"明天"，读档即是次晨，
        // 否则读档重打第 N 天：收入重复赚、日历原地踏步。
        var gs = Capture();
        gs.progress.day = day + 1;
        SaveService.Save(gs);
    }
}
