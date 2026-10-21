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
    public GameState LastLoaded => _lastLoaded;
    public string StatusText { get; private set; } = "Auto-save: day close and app background. Manual save available.";
    private bool _saveAfterSettlement;

    private void LateUpdate()
    {
        if (!_saveAfterSettlement) return;
        _saveAfterSettlement = false;
        SaveGame(); // All day-end listeners, including the Sim host, have finished.
    }

    private long _pauseUtcTicks;
    private float _timeScaleBeforePause = 1f;
    private void OnApplicationPause(bool paused)
    {
        if (_simBridge == null || _simBridge.Sim == null) return;
        if (paused) {
            _pauseUtcTicks = System.DateTime.UtcNow.Ticks;
            _simBridge.IsSuspended = true;
            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            SaveGame();
        }
        else if (_pauseUtcTicks > 0) {
            double elapsed = (System.DateTime.UtcNow.Ticks - _pauseUtcTicks) / (double)System.TimeSpan.TicksPerSecond;
            _pauseUtcTicks = 0;
            _simBridge.ApplyElapsedDelegation(elapsed);
            _simBridge.IsSuspended = false;
            Time.timeScale = _timeScaleBeforePause;
            SaveGame();
        }
    }

    private void OnApplicationQuit()
    {
#if !UNITY_EDITOR
        if (_pauseUtcTicks == 0 && _simBridge != null && _simBridge.Sim != null) SaveGame();
#endif
    }

    private void Awake()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (renovation == null) renovation = FindFirstObjectByType<RenovationSystem>();
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        _breakdowns = FindFirstObjectByType<BreakdownSystem>();
        _simBridge = FindFirstObjectByType<HotelSimSceneBridge>();

        // 老的单槽存档搬进 1 号槽（只在 1 号槽空着时搬）——加槽位不能让老玩家丢进度
        SaveSlots.MigrateLegacySlotIfNeeded();
    }

    private HotelSimSceneBridge _simBridge;

    // Subscribe in Start so RenovationSystem (subscribes in Awake) ticks the day
    // BEFORE we capture — the autosave then reflects post-tick tiers.
    private void Start()
    {
        if (dayController != null) dayController.OnDaySettled += HandleDaySettled;

        // 玩家在存档界面点了"读取 2 号槽"→ 场景重载 → 现在把意向消费掉。
        // 读档必须走重载：第 12 天读第 3 天的档，场上有走到一半的客人、在途的
        // 施工单、正在打扫的管家——逐个系统改回去不可能改干净（见 SaveSlots 注释）。
        int requested = SaveSlots.ConsumePendingLoad();
        if (SaveSlots.IsValid(requested))
        {
            SaveSlots.SetActiveSlot(requested);
            LoadGame();
            return;
        }

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
        gs.presentation = HotelSessionState.Current;
        var manager = FindFirstObjectByType<ManagerController>();
        if (manager != null) {
            gs.presentation.hasManagerPosition = true;
            gs.presentation.managerX = manager.transform.position.x;
            gs.presentation.managerY = manager.transform.position.y;
            gs.presentation.managerZ = manager.transform.position.z;
        }
        if (economy != null) gs.economy = economy.CaptureState();
        if (renovation != null) gs.renovation = renovation.CaptureState();
        gs.progress.day = dayController != null ? dayController.CurrentDay : 0;
        gs.progress.satisfaction = demandLoop != null ? demandLoop.prototypeSatisfactionScore : 0;
        gs.rooms = demandLoop != null ? demandLoop.CaptureOccupancy() : new RoomsState();
        // v3 世界层：设施解锁 / 威望 / 胶带复发 / 锁房
        FacilitySystem.CaptureTo(gs.world);
        HotelEmpireProgress.CaptureTo(gs.world);
        gs.world.prestige = ManagerReputation.Prestige;

        // **模拟内核**（v4-v7 的 sim 段）。以前这一段从来没被写过——SimState 结构
        // 完整、HotelSim.CaptureTo 也写好了，但**没有人调用它**。于是家具、预订簿、
        // 在途施工单、仓库、工资账、信用……读档一律回到出厂状态。
        // 玩家自建酒店（改墙/摆家具）的前提就是它：改完一读档全没，功能等于假的。
        if (_simBridge != null && _simBridge.Sim != null)
        {
            _simBridge.Sim.CaptureTo(gs.sim);
            gs.delegationReport = _simBridge.LastDelegation;
            gs.resumeWorld = true;
            gs.awaitingMorningReport = _simBridge.AwaitingMorningReport;
            gs.needsBeginDay = _simBridge.NeedsBeginDay;
            gs.lastInterestAccrued = _simBridge.LastInterestAccrued;
            gs.lastFacilityUpkeep = _simBridge.LastFacilityUpkeep;
            gs.progress.day = gs.sim.day;
            gs.sim.lastRealUtcTicks = System.DateTime.UtcNow.Ticks;
        }
        else if (_lastLoaded != null && _lastLoaded.sim != null)
        {
            // 本场景没有桥（v1 原型场景）：把读进来的 sim 段原样带过，
            // 别用空表覆盖掉经理模式的进度（同 world 段的处理）
            gs.sim = _lastLoaded.sim;
        }
        if (_breakdowns != null) _breakdowns.CaptureTo(gs.world);
        else if (_lastLoaded != null && _lastLoaded.world != null)
        {
            // 本场景没有损坏系统（v1 原型场景）：把读进来的跨日数据原样带过，别用空表覆盖
            gs.world.tapedBreakdowns = _lastLoaded.world.tapedBreakdowns;
            gs.world.lockedRooms = _lastLoaded.world.lockedRooms;
        }
        return gs;
    }

    public void SaveGame()
    {
        try
        {
            SaveService.Save(Capture());
            StatusText = (GameText.UseChinese ? "已保存至槽位 " : "Saved to slot ")
                + SaveSlots.ActiveSlot + " · " + System.DateTime.Now.ToString("HH:mm:ss");
        }
        catch (System.Exception ex)
        {
            StatusText = GameText.UseChinese ? "保存失败：请检查存储空间并重试。" : "Save failed. Check storage and retry.";
            Debug.LogError("Save failed: " + ex.Message);
        }
    }

    public void LoadGame()
    {
        var gs = SaveService.Load();
        if (gs == null) return;
        _lastLoaded = gs;
        HotelSessionState.Current = gs.presentation ?? new HotelPresentationState();
        if (HotelSessionState.Current.hasManagerPosition) {
            var manager = FindFirstObjectByType<ManagerController>();
            manager?.TryRestorePosition(new Vector3(gs.presentation.managerX, gs.presentation.managerY, gs.presentation.managerZ));
        }
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
            HotelEmpireProgress.RestoreFrom(gs.world);
            ManagerReputation.Restore(gs.world.prestige);
            if (_breakdowns != null) _breakdowns.RestoreFrom(gs.world);
        }
        // **Sim 段延迟应用**：此刻是 Start 阶段，而 Sim 是在
        // HotelSimSceneBridge.Update() → TryBuild() 里惰性建的（那时 demandLoop.rooms
        // 才填好）。Unity 所有 Start 先于任何 Update ⇒ 这里 _simBridge.Sim 保证是 null，
        // 直接灌不是"没生效"而是**存档损坏**（并行审计抓到的）。
        // 挂到 SaveSlots 上，桥建完账第一件事就是取用。
        if (gs.sim != null) SaveSlots.QueueSimRestore(gs.sim);

        // 顶栏现金对齐（否则读档当天一直显示序列化默认值）
        if (dayController != null) dayController.SyncCashFromEconomy();
    }

    public void NewGame()
    {
        SaveService.Delete();
        HotelSessionState.Current = new HotelPresentationState();
        // 静态世界状态一并归零，否则上一局的威望/解锁渗进新档
        ManagerReputation.ResetForNewGame();
        FacilitySystem.ResetForNewGame();
        HotelEmpireProgress.ResetForNewGame();
        SaveSlots.ResetForNewGame();
        _lastLoaded = null;
    }

    private void HandleDaySettled(int day, int served, DayLedger ledger)
    {
        if (!autoSaveOnDayEnd) return;
        // 日结时 demoDayIndex 还没 ++（Continue 才推进）——存"明天"，读档即是次晨，
        // 否则读档重打第 N 天：收入重复赚、日历原地踏步。
        if (_simBridge != null) _saveAfterSettlement = true;
        else
        {
            var gs = Capture();
            gs.progress.day = day + 1;
            SaveService.Save(gs);
        }
    }
}
