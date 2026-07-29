using System.Collections.Generic;
using UnityEngine;

// v3 模拟内核的场景宿主（架构 A.2 的 HotelSimHost）。
//
// **绞杀者模式的接缝**：M-A 阶段这里是"影子模式"——新时钟、员工总账、tick 管线
// 在真实场景里跑起来并可被观察，但**房态权威仍在 v1 DemandLoop 手里**
// （ServiceEnabled=false），两边不抢方向盘。M-B 需求层迁过来后翻转权威。
//
// 日号与 v1 锁死：订阅 OnDaySettled 后 BeginNextDay，Sim 的 CurrentDay 永远
// 跟 Room2DDemoDayController 一致，绝不出现两套日历。
public class HotelSimSceneBridge : MonoBehaviour
{
    /// <summary>单例：调试 HUD / 战略层 UI 读它。</summary>
    public static HotelSimSceneBridge Instance { get; private set; }

    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private ManagerController manager;
    [SerializeField] private StaffAgentSpawner spawner;
    [SerializeField] private int rngSeed = 20260720;

    [Tooltip("每帧最多消化多少 tick（跳段/高倍速时分帧，防卡顿）")]
    [SerializeField] private int maxTicksPerFrame = 120;

    /// <summary>完整的 v3 内核（B1a 起：世界场景里真的托管一个 HotelSim，
    /// 预订簿/家具/挂牌档/声誉/债务全在里面）。UI 与战略层都读它。</summary>
    public HotelSim Sim { get; private set; }

    public SimClock Clock => Sim != null ? Sim.Clock : _bootClock;
    public RoomLedger Rooms => Sim != null ? Sim.Rooms : null;
    public StaffRoster Staff => Sim != null ? Sim.Staff : _bootStaff;
    public SimPipeline Pipeline => Sim != null ? Sim.Pipeline : null;

    private SimClock _bootClock;
    private StaffRoster _bootStaff;

    /// <summary>房态权威是否仍在 v1。
    ///
    /// **B1a 阶段刻意保持 true**：世界场景里的 StaffAgent 才是清洁模拟的本体
    /// （会走路、会摸鱼、会被抓、会留瑕疵），比 Sim 的信用额模型好，所以房态归它。
    /// Sim 负责经济那一半。B1b 起同步变成**双向**，裁判在 RoomAuthorityPolicy。</summary>
    public bool MirrorMode { get; private set; } = true;

    [Header("Derelict rooms at start")]
    [Tooltip("开局有几间破败房（满是蜘蛛网和垃圾，要派人一次次清出来）。" +
             "0 = 全部可用，那样'清理'这套操作就没有对象，玩家也没有解锁的进展感。")]
    [SerializeField] private int derelictRoomsAtStart = 4;

    /// <summary>B1b：客人与房费归 Sim。v1 需求循环退化为纯清洁演出——
    /// 不再自己发明客人、不再给房费入账、不再每早垫几位假过夜客。
    /// 这是"绞杀者迁移"里真正的权威翻转那一刀。</summary>
    public bool SimOwnsGuestFlow { get; private set; } = true;

    /// <summary>上一次同步里推给 v1 的房间数（调试/验收用）。</summary>
    public int LastPushedRoomCount { get; private set; }

    private readonly Dictionary<int, int> _staffIdByRoomlessMember = new Dictionary<int, int>();
    private readonly Dictionary<StaffMember, int> _staffIdByMember = new Dictionary<StaffMember, int>();
    private float _mirrorTimer;
    private bool _built;

    private void Awake()
    {
        Instance = this;
        // 注意：真正的自愈在 OnEnable——Play 中热重载会清空静态与非序列化字段，
        // 而 Awake 不重跑（本项目定过的规矩：静态注册表放 OnEnable）

        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
        if (spawner == null) spawner = FindFirstObjectByType<StaffAgentSpawner>();

        _bootClock = new SimClock();
        _bootStaff = new StaffRoster();
    }

    private void OnDestroy()
    {
        if (dayController != null) dayController.OnDaySettled -= HandleLegacyDaySettled;
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        // 域重载自愈：静态单例、纯 C# 对象、委托订阅全被热重载清空，
        // OnEnable 会重跑，所以这里把它们全部补齐（Awake 不会再来一次）。
        Instance = this;
        if (_bootClock == null) _bootClock = new SimClock();
        if (_bootStaff == null) _bootStaff = new StaffRoster();

        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (dayController != null)
        {
            dayController.OnDaySettled -= HandleLegacyDaySettled;   // 防双订阅
            dayController.OnDaySettled += HandleLegacyDaySettled;
        }
        // Sim 本体（纯 C#）被热重载清空，但 _built 是 bool（可序列化）**活了下来**——
        // 不归零的话桥以为账还在，永远不重建，Sim 恒为 null（实测踩到，NRE 刷屏）。
        // 中途状态（当日现金/预订）保不住，这是开发期热重载的固有代价，只保证不坏。
        if (Sim == null) _built = false;
    }

    private void Update()
    {
        // 场景里的房/员工在 Start 时可能还没建好，首帧起惰性建账（时序坑，踩过）
        if (!_built && !TryBuild()) return;

        SyncRooms();
        DriveClock();
    }

    // ── 建账 ─────────────────────────────────────────────────────────────────

    private bool TryBuild()
    {
        if (demandLoop == null || demandLoop.rooms == null) return false;

        var defs = new List<RoomDefinition>();
        foreach (var room in demandLoop.rooms)
        {
            if (room == null) continue;
            int floor = FloorMath.FloorIndexForY(room.transform.position.y);
            defs.Add(new RoomDefinition(
                room.roomNumber,
                floor: floor,
                zone: floor,                       // M-A：一层=一区；M-F 再按房型细分
                category: room.roomCategory,
                tier: RoomTier.Old,                // M-B 从 RenovationSystem 同步真实档位
                state: RoomStateMapping.FromLegacy(room.currentState)));
        }
        if (defs.Count == 0) return false;

        // **开局锁住几间房**：白盒场景本来 12 间全可用，于是"清理破败房"没有对象，
        // 玩家也没有"解锁一间"的进展感（玩家原话：破败房间现在是 0）。
        // 从房号最大的往前锁——顶楼先烂，符合"老楼上面没人管"的直觉。
        MarkTopRoomsDerelict(defs, derelictRoomsAtStart);

        // 用**场景里真实的房**建完整内核（不是另造一套 100 间的假房）
        int startingCash = economy != null ? economy.Cash : 4000;
        Sim = new HotelSim(new RoomLedger(defs), _bootStaff, RoomRateTable.Default,
                           DemandConfig.Default, startingCash, rngSeed);

        // 房态权威留在 v1 的 StaffAgent 手里（它是实体化的清洁模拟，比信用额模型好）。
        // Sim 只跑经济那一半——ServiceEnabled=false 让它不去动房态。
        Sim.Pipeline.ServiceEnabled = !MirrorMode;

        // 钟面与 v1 的日号对齐，绝不出现两套日历
        Sim.Clock.JumpTo(_bootClock.CurrentDay, _bootClock.CurrentMinute);

        // **日长对齐**：v1 一天 240 真实秒（BalanceConfig），Sim 1x 一天 480 秒。
        // 不对齐的话 v1 日结时 Sim 才走到 15:00，16:00 起的入住高峰整个被截掉，
        // 预订客一半永远到不了店（实测就是这么发现的）。2x 让两边同时收工。
        Sim.Clock.SpeedMultiplier = 2f;

        Sim.FurnishInheritedRooms();   // 继承的破家具：这家酒店本来就是这么破
        Sim.Materials.Add(6);

        RegisterStaffFromPayroll();
        _built = true;

        Debug.Log($"[HotelSim] 建账完成：{Rooms.Count} 间房（{Rooms.OpenRoomCount} 营业 / " +
                  $"{Rooms.CountOf(RoomSimState.Ruined)} 破败），{Staff.Count} 名员工，" +
                  $"家具 {Sim.Furniture.Count} 件。房态权威仍在 v1（MirrorMode={MirrorMode}），" +
                  $"Sim 负责经济。");
        return true;
    }

    /// <summary>把 v1 的花名册同步进 Sim 总账——**双向**：雇了要加，解雇要减。
    ///
    /// 以前只加不减，于是"解雇前台之后客人照常入住"（玩家实测）：v1 的名册里
    /// 人没了，Sim 总账里他还在上班，ServiceCapacityModel 照样算出每小时 6 个
    /// 入住能力。模型没错——前台无人时它返回 0——错在总账没跟着现实走。
    /// 一般化教训：**镜像同步必须处理"消失"**，只处理"出现"的同步迟早对不上账
    /// （同一个坑在房态上踩过：v1 覆写住着人的房导致房费蒸发）。</summary>
    /// <summary>把房号最大的几间标成破败（顶楼先烂）。
    /// 场景里那些房在 v1 眼里还是好房，桥的房态同步下一拍会把它们推成 Blocked，
    /// 于是玩家一进游戏就看到楼上几间是封着的。</summary>
    private static void MarkTopRoomsDerelict(List<RoomDefinition> defs, int count)
    {
        if (count <= 0) return;

        var byNumber = new List<RoomDefinition>(defs);
        byNumber.Sort((a, b) => b.number.CompareTo(a.number));

        int locked = 0;
        for (int i = 0; i < byNumber.Count && locked < count; i++)
        {
            // 至少留几间能卖的，否则开局无房可售直接死局
            if (defs.Count - locked <= 4) break;

            int index = defs.IndexOf(byNumber[i]);
            if (index < 0) continue;
            RoomDefinition d = defs[index];
            defs[index] = new RoomDefinition(d.number, d.floor, d.zone, d.category,
                                             d.tier, RoomSimState.Ruined);
            locked++;
        }
    }

    private void RegisterStaffFromPayroll()
    {
        if (economy == null || economy.Payroll == null) return;

        foreach (var member in economy.Payroll.Roster)
        {
            if (member == null || member.Role == StaffRole.Manager) continue; // 经理=玩家
            if (_staffIdByMember.ContainsKey(member)) continue;
            int id = Staff.Register(member);
            _staffIdByMember[member] = id;
            Staff.StartShift(id); // M-B 由 ShiftPlan 决定谁上班
        }

        UnregisterFiredStaff();
    }

    /// <summary>名册里已经没有的人要从 Sim 总账里摘掉（解雇/辞职）。</summary>
    private void UnregisterFiredStaff()
    {
        _scratchFired.Clear();
        foreach (var pair in _staffIdByMember)
        {
            bool stillEmployed = false;
            foreach (var member in economy.Payroll.Roster)
                if (ReferenceEquals(member, pair.Key)) { stillEmployed = true; break; }
            if (!stillEmployed) _scratchFired.Add(pair.Key);
        }

        foreach (var member in _scratchFired)
        {
            // 先下班再摘：Pipeline 每分钟都会把"人手缩了却还占着的房"退回脏房池
            // （ReleaseIfCrewShrank），所以只要状态先落到 OffShift 就自愈；
            // 直接 Remove 而不下班的话那一分钟的清洁额度会算在幽灵身上。
            if (_staffIdByMember.TryGetValue(member, out int staffId))
            {
                Staff.EndShift(staffId);
                Staff.Remove(staffId);
            }
            _staffIdByMember.Remove(member);
        }
    }

    private readonly List<StaffMember> _scratchFired = new List<StaffMember>();

    // ── 与 v1 对齐 ───────────────────────────────────────────────────────────

    /// <summary>房态双向同步（每 0.5 秒一次，够 HUD 用且不浪费）。
    ///
    /// B1b：Sim 管客人（入住/退房/封房要**写进** v1，否则管家看不到活、
    /// 投诉系统找不到住客），v1 管清洁链（Dirty→Cleaning→验房→Ready 只能**读**，
    /// 写进去会抹掉管家刚干完的进度）。逐格裁决在 RoomAuthorityPolicy。</summary>
    private void SyncRooms()
    {
        if (!MirrorMode || Rooms == null || demandLoop == null || demandLoop.rooms == null) return;

        // v1 需求循环每帧都可能被别人（日控制器/调试面板）改回默认，所以每次都表态
        demandLoop.guestFlowOwnedBySim = SimOwnsGuestFlow;

        _mirrorTimer -= Time.deltaTime;
        if (_mirrorTimer > 0f) return;
        _mirrorTimer = 0.5f;

        int pushed = 0;
        foreach (var room in demandLoop.rooms)
        {
            if (room == null || Sim == null || !Rooms.Contains(room.roomNumber)) continue;

            RoomSimState simState = Rooms.At(room.roomNumber).state;
            var direction = RoomAuthorityPolicy.Decide(
                simState, room.currentState, Sim.HasActiveStay(room.roomNumber),
                out Room2DState pushTarget);

            if (direction == RoomSyncDirection.PushToLegacy)
            {
                if (room.currentState != pushTarget)
                {
                    // SetState 是强制入口（绕过 guard）——这里就是要绕：Sim 的裁决
                    // 不必迁就 v1 的状态机顺序，比如打烊结账可以直接 Occupied→Dirty。
                    room.SetState(pushTarget);
                    // 房牌颜色不会自己跟上：EnterState 只改数据，刷视觉是控制器的活。
                    // 漏了这一句的话玩家看到的是"房态没变但管家跑过去了"。
                    var controller = room.GetComponent<Room2DController>();
                    if (controller != null) controller.ApplyStateVisual();
                    pushed++;
                }
            }
            else
            {
                Rooms.SetState(room.roomNumber, RoomStateMapping.FromLegacy(room.currentState));
            }
        }
        LastPushedRoomCount = pushed;

        // 新雇的人也要进总账（HiringInteraction 随时可能加人）
        RegisterStaffFromPayroll();
    }

    private void DriveClock()
    {
        if (AwaitingMorningReport) return;   // 晨报没看完，时间不走

        if (_needsBeginDay)
        {
            // 有债务在身 = 昨天有一笔该还的钱。玩家在晨报上没按还款键就记一次逾期
            // （涨利率、掉信用评级）——BeginDay 里收口，因为支付发生在晨报上。
            bool loanWasDue = economy != null && economy.Loan != null && economy.Loan.Balance > 0;
            Sim.BeginDay(loanWasDue);
            _needsBeginDay = false;
        }

        Pipeline.ManagerOnFloor = ComputeManagerOnFloor();

        Clock.Advance(Time.deltaTime);
        int budget = Mathf.Max(1, maxTicksPerFrame);
        while (budget-- > 0 && Clock.TryConsumeTick())
        {
            Sim.StepMinute();
        }
    }

    /// <summary>经理是否和某个在班员工同层——巡查层向 Sim 上报的"被盯着"事实。</summary>
    private bool ComputeManagerOnFloor()
    {
        if (manager == null || spawner == null) return false;
        int managerFloor = FloorMath.FloorIndexForY(manager.transform.position.y);
        foreach (var agent in spawner.Agents)
        {
            if (agent == null || agent.Member == null) continue;
            if (agent.CurrentFloor == managerFloor) return true;
        }
        return false;
    }

    private void HandleLegacyDaySettled(int day, int served, DayLedger ledger)
    {
        if (Sim == null) return;

        // 走 Sim 的完整日结（固定成本前置、家具老化、故障判定、装修推进、声誉收口），
        // 而不只是推一下管线——世界场景里现在跑的是同一套经济。
        Sim.SettleDay();
        Sim.Clock.BeginNextDay();
        // 日号与 v1 对齐（v1 的 demoDayIndex 在 Continue 时才 ++，这里跟着它走）
        Sim.Clock.JumpTo(day + 1, SimClock.DayStartMinute);

        // **不立刻开新一天**：先亮全屏晨报，玩家看完点"开门营业"才继续。
        // BeginDay 会清零昨日明细（Breakdown.Reset），必须等报告被看过之后再跑。
        AwaitingMorningReport = true;
    }

    /// <summary>日结完成、等玩家看晨报（WorldOperationsPanel 据此画全屏报告）。
    /// 为 true 期间 Sim 时钟暂停，v1 世界也停在日结后等 Restart。</summary>
    public bool AwaitingMorningReport { get; private set; }

    /// <summary>玩家点了"开门营业"：关报告，v1 开新的一天，Sim 跑晨间流程。</summary>
    public void ContinueToNextDay()
    {
        if (!AwaitingMorningReport) return;
        AwaitingMorningReport = false;
        _needsBeginDay = true;                       // 下一帧跑 Sim.BeginDay（退房潮+预订晨间流程）
        if (dayController != null) dayController.RestartDemoDay();
    }

    /// <summary>次晨要不要跑 Sim 的 BeginDay（退房潮 + 预订晨间流程）。
    /// 放到下一帧做，避免在 v1 的日结回调里嵌套一整套晨间逻辑。</summary>
    private bool _needsBeginDay = true;

    // ── 玩家操作入口（战略层顶栏将调用它们） ──────────────────────────────────

    public void SetSpeed(float multiplier) => Clock.SpeedMultiplier = multiplier;

    /// <summary>跳到下一关键阶段。有阻塞事件时拒绝并给出英文原因。</summary>
    public bool TrySkipToNextPhase(out string reason)
    {
        int blocking = CountBlockingIncidents();
        if (!PhaseScheduler.CanSkip(Clock.CurrentMinute, blocking, out reason)) return false;
        Clock.FastForwardTo(PhaseScheduler.NextKeyMinuteAfter(Clock.CurrentMinute));
        return true;
    }

    /// <summary>当前有多少"必须先处理"的事件（M-G 事件权威上收后改读 IncidentScheduler）。</summary>
    private int CountBlockingIncidents()
    {
        int n = 0;
        var fire = FindFirstObjectByType<FireAlarmIncident>();
        if (fire != null && fire.PanelOpen) n++;
        var complaint = FindFirstObjectByType<ComplaintInteraction>();
        if (complaint != null && complaint.PanelOpen) n++;
        return n;
    }

    /// <summary>调试 HUD 用的一行摘要。</summary>
    public string DebugSummary()
    {
        if (!_built || Sim == null) return "[Sim] building...";
        return $"[Sim] D{Clock.CurrentDay} {Clock.TimeFormatted} {PhaseScheduler.Label(PhaseScheduler.PhaseFor(Clock.CurrentMinute))}" +
               $" x{Clock.SpeedMultiplier:0.##}" +
               $" | rooms rdy{Rooms.SellableCount} drt{Rooms.DirtyBacklog} occ{Rooms.CountOf(RoomSimState.Occupied)}" +
               $" | staff on{Staff.OnDutyCount} work{Staff.ProductiveCountOfRole(StaffRole.Housekeeper)}" +
               $" slack{Staff.MaxSlackMinutesRemaining}m morale{Staff.AverageMorale:0}" +
               (Pipeline.ManagerOnFloor ? " [WATCHED]" : "");
    }
}
