using System;

// v3 房间数据模型（架构 B.5）。纯数据，不引用 UnityEngine。
// 100 间房常驻一个数组；场景里的 Room2DEntity/RoomView 只是当前可视楼层的视图。

/// <summary>房间模拟状态 = v1 的 Room2DState + Ruined（开局大部分房破败未解锁）。</summary>
public enum RoomSimState
{
    Ruined,              // 破败未解锁：不可售、不计营业房量，需花钱解锁
    Dirty,
    Cleaning,
    AwaitingInspection,  // 现役状态：Inspector 岗位消化它（可选质量闸门）
    Ready,
    Occupied,
    Blocked              // 装修/锁房：算营业房量但当期不可售
}

[Flags]
public enum RoomFlags
{
    None = 0,
    Vip = 1 << 0,
    Problem = 1 << 1,     // 损坏/投诉/锁房等需要玩家单独处理
    Renovating = 1 << 2,
    Surfaced = 1 << 3     // 巡查层当前可视楼层（由视图层置位）
}

/// <summary>建账用的房间定义（场景/存档 → 总账）。</summary>
public readonly struct RoomDefinition
{
    public readonly int number;
    public readonly byte floor;
    public readonly byte zone;
    public readonly Room2DRoomCategory category;
    public readonly RoomTier tier;
    public readonly RoomSimState state;

    public RoomDefinition(int number, int floor, int zone, Room2DRoomCategory category,
                          RoomTier tier, RoomSimState state)
    {
        this.number = number;
        this.floor = (byte)SimMath.Clamp(floor, 0, 255);
        this.zone = (byte)SimMath.Clamp(zone, 0, 255);
        this.category = category;
        this.tier = tier;
        this.state = state;
    }
}

/// <summary>一间房的全部模拟状态。可变 struct——只能经 RoomLedger.At() 的 ref 访问器修改。</summary>
public struct RoomRecord
{
    public int number;
    public byte floor;
    public byte zone;
    public Room2DRoomCategory category;
    public RoomTier tier;
    public RoomSimState state;
    public float wear;            // 磨损 0..1，喂损坏概率
    public int occupantResvId;    // 当前住客的预订 id；0=无。int 而非 short（短整型约 460 天打穿）
    public RoomFlags flags;

    public bool IsOpen => state != RoomSimState.Ruined;
    public bool IsSellable => state == RoomSimState.Ready;
    public bool NeedsCleaning => state == RoomSimState.Dirty;
}

/// <summary>Sim 状态 ↔ v1 Room2DState 的无损互转（镜像期用；Ruined 对旧视图退化为 Blocked）。</summary>
public static class RoomStateMapping
{
    public static RoomSimState FromLegacy(Room2DState legacy)
    {
        switch (legacy)
        {
            case Room2DState.Dirty: return RoomSimState.Dirty;
            case Room2DState.Cleaning: return RoomSimState.Cleaning;
            case Room2DState.AwaitingInspection: return RoomSimState.AwaitingInspection;
            case Room2DState.Ready: return RoomSimState.Ready;
            case Room2DState.Occupied: return RoomSimState.Occupied;
            default: return RoomSimState.Blocked;
        }
    }

    public static Room2DState ToLegacy(RoomSimState sim)
    {
        switch (sim)
        {
            case RoomSimState.Dirty: return Room2DState.Dirty;
            case RoomSimState.Cleaning: return Room2DState.Cleaning;
            case RoomSimState.AwaitingInspection: return Room2DState.AwaitingInspection;
            case RoomSimState.Ready: return Room2DState.Ready;
            case RoomSimState.Occupied: return Room2DState.Occupied;
            default: return Room2DState.Blocked;  // Blocked 与 Ruined 都退化为 Blocked
        }
    }
}
