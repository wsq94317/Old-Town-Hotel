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

[Serializable]
public sealed class GameState
{
    public int version = 3;   // v2: + rooms（过夜占用）；v3: + world（经理模式世界层）
    public EconomyState economy = new EconomyState();
    public RenovationState renovation = new RenovationState();
    public ProgressState progress = new ProgressState();
    public RoomsState rooms = new RoomsState();
    public WorldState world = new WorldState();
}
