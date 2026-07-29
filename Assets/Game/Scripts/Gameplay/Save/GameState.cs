using System;
using System.Collections.Generic;

// Serializable save DTOs (JsonUtility-friendly: public fields, enums stored as int,
// no dictionaries). v2 persists economy progression + overnight room occupancy
// (day-cycle v2) — other transient in-day room states reset each day by design.
// See SaveService / SaveCoordinator.

[Serializable]
public sealed class StaffState
{
    public int role;       // StaffRole
    public string name;
    public int wage;
    public int speed, quality, stamina;
    public int education;
    public int morale;
    public List<int> traits = new List<int>(); // StaffTrait values
}

[Serializable]
public sealed class EconomyState
{
    public int cash;
    public int loanBalance;
    public float loanRate;
    public List<StaffState> staff = new List<StaffState>();
    public List<float> reputationSamples = new List<float>(); // Phase 6 ★ rating window
}

[Serializable]
public sealed class RoomTierEntry { public int room; public int tier; }      // tier = RoomTier

[Serializable]
public sealed class RenoJobEntry { public int room; public int targetTier; public int daysRemaining; }

[Serializable]
public sealed class RenovationState
{
    public int totalRooms;
    public int startingRoomNumber;
    public List<RoomTierEntry> rooms = new List<RoomTierEntry>();
    public List<RenoJobEntry> jobs = new List<RenoJobEntry>();
}

[Serializable]
public sealed class ProgressState
{
    public int day;
    public int satisfaction;
}

[Serializable]
public sealed class OccupiedRoomEntry
{
    public int room;
    public int stayQuality; // Room2DMatchQuality
    public int guestType;   // Room2DGuestType（退房卡片头像用；旧档缺省 0 = Business）
}

[Serializable]
public sealed class RoomsState
{
    // 过夜占用（day-cycle v2）：日结时 Occupied 的房间 + 该次入住的匹配质量，
    // 读档后次日晨间退房潮据此如实退房并结算收入。
    public List<OccupiedRoomEntry> occupied = new List<OccupiedRoomEntry>();
}

[Serializable]
public sealed class TapedBreakdownEntry
{
    public int room = -1;     // roomNumber；设施层损坏无房 = -1
    public float x, y, z;     // 复发位置
    public string kind;
}

[Serializable]
public sealed class WorldState
{
    // v2 世界层（经理模式）跨日状态：设施解锁 / 威望 / 胶带明日复发 / 昨日锁房。
    // 当日进行中的损坏不存——存档只发生在日结，损坏每天清场重掷。
    public bool gymUnlocked;
    public bool casinoUnlocked;
    public bool poolUnlocked;
    public int prestige;
    public List<TapedBreakdownEntry> tapedBreakdowns = new List<TapedBreakdownEntry>();
    public List<int> lockedRooms = new List<int>(); // 次晨自动转 Dirty
}

// ── v4：Sim 内核状态（增量演进第一批） ──────────────────────────────────────
// 存档随里程碑逐期加字段（JsonUtility 缺省值天然兼容旧档），拒绝到 M-E 一次性大爆炸
// 迁移——本项目在存档口径上踩过坑（day+1），越晚越大越危险。

[Serializable]
public sealed class PriceOverrideEntry { public int day; public int template; }

[Serializable]
public sealed class ShiftTierEntry { public int role; public int tier; }

[Serializable]
public sealed class StaffSimState
{
    public int staffId;
    public int rosterIndex;          // 对应 StaffRoster.Entries 的序（重建时顺序一致）
    public int operationalState;     // StaffOperationalState
    public float fatigue;
    public int slackMinutesRemaining;
}

[Serializable]
public sealed class FurnitureSaveEntry
{
    public int instanceId;
    public int kindId;
    public int roomNumber;
    public float posX, posY;
    public float newness = 1f;
    public float health = 1f;
    public int faultLineIndex = -1;
    public int repairDaysRemaining;
    public int anchorId;          // v8：摆在哪个锚点上（0 = 旧档，读档时补派）
}

/// <summary>每间房的挂牌档（RoomTier = "你声称它有多好"，玩家设定）。</summary>
[Serializable]
public sealed class RoomBandEntry { public int room; public int band; }

/// <summary>一张预订单（v6）。
/// **日历不进存档**：容量每晨按房态重算，需求由簿子全量重算——
/// 存一份派生数据只会多一个对不上账的地方。</summary>
[Serializable]
public sealed class ReservationSaveEntry
{
    public int id;
    public int channelId;
    public int bookedOnDay;
    public int arrivalDay;
    public int nights = 1;
    public int tier;            // RoomTier
    public int lockedPrice;
    public int segment;         // GuestSegment
    public int state;           // ReservationState
    public int assignedRoomNumber;
}

/// <summary>一张在建施工单（v7）。装修与破败房复原共用。</summary>
[Serializable]
public sealed class BuildJobEntry
{
    public int jobId;
    public int planKind;        // RenovationPlanKind
    public int targetTier;      // RoomTier
    public int daysRemaining;
    public bool isReclaim;
    public int reclaimKind;     // ReclaimPlanKind
    public List<int> rooms = new List<int>();
}

/// <summary>一间房的运行时状态（v7）。以前只存挂牌档，房态没存——
/// 于是装修中/破败/脏房读档后全变成默认值。</summary>
[Serializable]
public sealed class RoomStateEntry { public int room; public int state; }   // state = RoomSimState

[Serializable]
public sealed class SimState
{
    // 时钟（离线连续时间制需要它们全部）
    public int day = 1;
    public int minute = 8 * 60;
    public long totalMinutesElapsed;
    public long lastRealUtcTicks;
    public List<int> settledDayIds = new List<int>();   // 每 dayId 至多一行 DayLedger 的幂等守卫

    // 资金
    public int safeboxLevel = 1;
    public int safeboxBalance;
    public int overflowBalance;
    public int cash;

    // 定价 / 排班
    public int defaultPriceTemplate;                    // PriceTemplate
    public List<PriceOverrideEntry> priceOverrides = new List<PriceOverrideEntry>();
    public List<ShiftTierEntry> shiftTiers = new List<ShiftTierEntry>();

    // 员工模拟状态（士气/工资在 EconomyState.staff，这里只放 Sim 侧运营状态）
    public int nextStaffId;
    public List<StaffSimState> staff = new List<StaffSimState>();

    // v5：家具（崭新度/健康度/故障/维修工期）+ 材料库存 + 每房挂牌档
    public int materialStock;
    public int nextFurnitureId;
    public List<FurnitureSaveEntry> furniture = new List<FurnitureSaveEntry>();
    public List<RoomBandEntry> roomBands = new List<RoomBandEntry>();

    // v6：预订簿（逐单）+ 故意超售档 + 视野是否已铺开
    public int nextReservationId;
    public int overbookingAllowance;
    public bool bookingHorizonSeeded;
    public List<ReservationSaveEntry> reservations = new List<ReservationSaveEntry>();

    // v7：在建施工单 + 房态。
    // **既有缺陷**：施工队列从来没进存档——玩家花了钱和材料，读档后工单凭空消失。
    // 破败房复原工期最长 7 天（全游戏最长），不存等于直接吞钱。
    public int nextBuildJobId;
    public List<BuildJobEntry> buildJobs = new List<BuildJobEntry>();

    // v8：**Sim 自己的声誉样本**。把存档接通之后才暴露出来的缺口——
    // 世界场景的星级走 Sim 的 ReputationLedger，而这里以前只有 v1 经济体那份
    // （EconomyState.reputationSamples，经理模式压根不填它）。
    // 结果：每次读档星级归零，玩了二十天的口碑一瞬间白给。
    public List<float> reputationSamples = new List<float>();

    // v8：本次会话新加的三套账，同样不存就同样读档归零
    public int warehouseCapacity;                       // 仓库容量（0 = 不设上限）
    public int payrollCycle;                            // PayrollCycle
    public int payrollOwed;                             // 累计未付工资
    public int payrollDaysAccrued;
    public int payrollMissedPaydays;
    public int creditScore = CreditPolicy.StartingScore; // 信用 0-100
    public int creditMissedPayments;
    public List<JunkClearEntry> junkClearing = new List<JunkClearEntry>();   // 破败房清理进度
    public List<RoomStateEntry> roomStates = new List<RoomStateEntry>();
}

[Serializable]
public sealed class JunkClearEntry { public int room; public int workDone; }

[Serializable]
public sealed class GameState
{
    // v2: + rooms（过夜占用）；v3: + world（经理模式世界层）；v4: + sim（模拟内核）
    // v5: + 家具（崭新度/健康度）、材料库存、每房挂牌档
    // v6: + 预订簿（逐单 Reservation）、故意超售档、预订视野已铺开标记
    // v7: + 在建施工单（装修/复原）与房态——以前工单和房态都会在读档时丢
    // v8: + Sim 的声誉样本（星级以前每次读档归零）；仓库容量；工资账与信用
    public const int CurrentVersion = 8;

    public int version = CurrentVersion;
    public EconomyState economy = new EconomyState();
    public RenovationState renovation = new RenovationState();
    public ProgressState progress = new ProgressState();
    public RoomsState rooms = new RoomsState();
    public WorldState world = new WorldState();
    public SimState sim = new SimState();

    /// <summary>旧档补齐：JsonUtility 对缺失字段给 null，逐段兜底后升到当前版本。
    /// 只做"补默认值"，绝不丢已有数据（v3 的 world / v2 的 rooms 原样留着）。</summary>
    public void MigrateToCurrentVersion()
    {
        if (economy == null) economy = new EconomyState();
        if (renovation == null) renovation = new RenovationState();
        if (progress == null) progress = new ProgressState();
        if (rooms == null) rooms = new RoomsState();
        if (world == null) world = new WorldState();
        if (sim == null) sim = new SimState();
        if (sim.settledDayIds == null) sim.settledDayIds = new List<int>();
        if (sim.priceOverrides == null) sim.priceOverrides = new List<PriceOverrideEntry>();
        if (sim.shiftTiers == null) sim.shiftTiers = new List<ShiftTierEntry>();
        if (sim.staff == null) sim.staff = new List<StaffSimState>();
        if (sim.furniture == null) sim.furniture = new List<FurnitureSaveEntry>();
        if (sim.roomBands == null) sim.roomBands = new List<RoomBandEntry>();
        if (sim.reservations == null) sim.reservations = new List<ReservationSaveEntry>();
        if (sim.buildJobs == null) sim.buildJobs = new List<BuildJobEntry>();
        if (sim.roomStates == null) sim.roomStates = new List<RoomStateEntry>();
        if (sim.reputationSamples == null) sim.reputationSamples = new List<float>();
        if (sim.junkClearing == null) sim.junkClearing = new List<JunkClearEntry>();
        // 旧档没有信用分：给满分而不是 0，否则老玩家一读档就变"已拉黑"
        if (version < 8 && sim.creditScore == 0) sim.creditScore = CreditPolicy.StartingScore;

        // v3 及更早：Sim 尚未存在，用进度里的日号对齐钟面（读档即是那天早上）
        if (version < 4 && sim.day <= 1 && progress.day > 0) sim.day = progress.day;

        version = CurrentVersion;
    }
}
