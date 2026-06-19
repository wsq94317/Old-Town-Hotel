using System;
using System.Collections.Generic;

// Serializable save DTOs (JsonUtility-friendly: public fields, enums stored as int,
// no dictionaries). v1 persists economy progression only — not transient in-day
// room occupancy. See SaveService / SaveCoordinator.

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
public sealed class GameState
{
    public int version = 1;
    public EconomyState economy = new EconomyState();
    public RenovationState renovation = new RenovationState();
    public ProgressState progress = new ProgressState();
}
