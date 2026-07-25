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
}

/// <summary>每间房的挂牌档（RoomTier = "你声称它有多好"，玩家设定）。</summary>
[Serializable]
public sealed class RoomBandEntry { public int room; public int band; }

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
}

[Serializable]
public sealed class GameState
{
    // v2: + rooms（过夜占用）；v3: + world（经理模式世界层）；v4: + sim（模拟内核）
    // v5: + 家具（崭新度/健康度）、材料库存、每房挂牌档
    public const int CurrentVersion = 5;

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

        // v3 及更早：Sim 尚未存在，用进度里的日号对齐钟面（读档即是那天早上）
        if (version < 4 && sim.day <= 1 && progress.day > 0) sim.day = progress.day;

        version = CurrentVersion;
    }
}
