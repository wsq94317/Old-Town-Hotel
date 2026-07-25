using System;
using System.Collections.Generic;

// 家具总账（设计文档 §9/§11/§12）。纯 C#，不引用 UnityEngine。
// 100 房 × 6 件 = 600 实例；衰减事件驱动（退房时只动该房 6 件），故障判定每日 O(600)。

/// <summary>一件家具的运行时状态。用 class（数量小，不值得承担可变 struct 的副本风险）。</summary>
public sealed class FurnitureInstance
{
    public int instanceId;
    public int kindId;
    public int roomNumber;
    public float posX, posY;      // 现在填槽位预设；将来的拖拽摆放直接用它
    public float newness = 1f;    // 只按衰减走，维修不能动
    public float health = 1f;     // 维修可回满
    public int faultLineIndex = -1; // ≥0 = 正在故障，指向文案池
    public int repairDaysRemaining; // >0 = 师傅还在修（"花时间"的那一半，期间仍不可用）

    public bool IsFaulted => faultLineIndex >= 0;
    public bool IsUnderRepair => repairDaysRemaining > 0;
}

public sealed class FurnitureLedger
{
    private readonly List<FurnitureInstance> _all = new List<FurnitureInstance>();
    private readonly Dictionary<int, FurnitureInstance> _byId = new Dictionary<int, FurnitureInstance>();
    private readonly Dictionary<int, List<FurnitureInstance>> _byRoom = new Dictionary<int, List<FurnitureInstance>>();
    private int _nextId;

    public int Count => _all.Count;
    public IReadOnlyList<FurnitureInstance> All => _all;

    /// <summary>下一个将分配的 id（存档要带，否则读档后新买家具会撞历史 id）。</summary>
    public int NextIdSeed => _nextId;

    public void RestoreIdSeed(int seed)
    {
        if (seed > _nextId) _nextId = seed;
    }

    public IReadOnlyList<FurnitureInstance> InRoom(int roomNumber) =>
        _byRoom.TryGetValue(roomNumber, out var list) ? list : EmptyList;

    private static readonly List<FurnitureInstance> EmptyList = new List<FurnitureInstance>();

    public bool TryGet(int instanceId, out FurnitureInstance instance) => _byId.TryGetValue(instanceId, out instance);

    /// <summary>放一件家具进房。newness/health 默认全新（新买/装修换新）。</summary>
    public FurnitureInstance Place(int roomNumber, int kindId, float newness = 1f, float health = 1f,
                                   float posX = 0f, float posY = 0f)
    {
        var instance = new FurnitureInstance
        {
            instanceId = ++_nextId,
            kindId = kindId,
            roomNumber = roomNumber,
            newness = SimMath.Clamp01(newness),
            health = SimMath.Clamp01(health),
            posX = posX,
            posY = posY,
        };
        _all.Add(instance);
        _byId[instance.instanceId] = instance;
        if (!_byRoom.TryGetValue(roomNumber, out var list))
        {
            list = new List<FurnitureInstance>();
            _byRoom[roomNumber] = list;
        }
        list.Add(instance);
        return instance;
    }

    /// <summary>移走一件家具（卖掉/替换）。</summary>
    public bool Remove(int instanceId)
    {
        if (!_byId.TryGetValue(instanceId, out FurnitureInstance instance)) return false;
        _byId.Remove(instanceId);
        _all.Remove(instance);
        if (_byRoom.TryGetValue(instance.roomNumber, out var list)) list.Remove(instance);
        return true;
    }

    // ── 房间层面的查询 ───────────────────────────────────────────────────────

    /// <summary>房间交付水平 0~1（挂牌档 vs 交付的"交付"那一半）。</summary>
    public float DeliveredQuality(int roomNumber)
    {
        float sum = 0f;
        var list = InRoom(roomNumber);
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            FurnitureKind kind = FurnitureCatalog.Get(f.kindId);
            sum += FurnitureWearModel.EffectiveDecor(kind.decorPoints, f.newness, f.IsFaulted);
        }
        return SimMath.Clamp01(sum / FurnitureCatalog.FullyFurnishedDecor);
    }

    /// <summary>必备家具（床+卫浴）是否齐全且可用。缺失或故障 → 房间不可售。</summary>
    public bool RequiredFurnitureWorking(int roomNumber)
    {
        bool bed = false, bathroom = false;
        var list = InRoom(roomNumber);
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            if (f.IsFaulted) continue;
            FurnitureKind kind = FurnitureCatalog.Get(f.kindId);
            if (kind.slot == FurnitureSlot.Bed) bed = true;
            else if (kind.slot == FurnitureSlot.Bathroom) bathroom = true;
        }
        return bed && bathroom;
    }

    /// <summary>房间对某客群的家具吸引力加成。</summary>
    public float SegmentAppeal(int roomNumber, GuestSegment segment)
    {
        float sum = 0f;
        var list = InRoom(roomNumber);
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            if (f.IsFaulted) continue;
            sum += FurnitureCatalog.Get(f.kindId).AppealFor(segment);
        }
        return sum;
    }

    /// <summary>房内平均健康度（RoomRecord.wear 由它派生，避免两套衰减系统）。</summary>
    public float AverageHealth(int roomNumber)
    {
        var list = InRoom(roomNumber);
        if (list.Count == 0) return 1f;
        float sum = 0f;
        for (int i = 0; i < list.Count; i++) sum += list[i].health;
        return sum / list.Count;
    }

    public float AverageNewness(int roomNumber)
    {
        var list = InRoom(roomNumber);
        if (list.Count == 0) return 1f;
        float sum = 0f;
        for (int i = 0; i < list.Count; i++) sum += list[i].newness;
        return sum / list.Count;
    }

    public int FaultedCountInRoom(int roomNumber)
    {
        int n = 0;
        var list = InRoom(roomNumber);
        for (int i = 0; i < list.Count; i++) if (list[i].IsFaulted) n++;
        return n;
    }

    // ── 衰减 ─────────────────────────────────────────────────────────────────

    /// <summary>一位客人住了一晚：该房家具按客群磨损倍率衰减（退房时调一次）。</summary>
    public void ApplyGuestNight(int roomNumber, float segmentWear)
    {
        var list = InRoom(roomNumber);
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            FurnitureKind kind = FurnitureCatalog.Get(f.kindId);
            f.newness = SimMath.Clamp01(f.newness
                - FurnitureWearModel.NewnessLossPerNight(kind.lifespanGuestNights, segmentWear));
            f.health = SimMath.Clamp01(f.health
                - FurnitureWearModel.HealthLossPerNight(f.newness, segmentWear));
        }
    }

    /// <summary>过一天：所有家具（含空置房）都极慢老化。</summary>
    public void ApplyIdleDay()
    {
        for (int i = 0; i < _all.Count; i++)
        {
            var f = _all[i];
            FurnitureKind kind = FurnitureCatalog.Get(f.kindId);
            f.newness = SimMath.Clamp01(f.newness
                - FurnitureWearModel.NewnessLossPerIdleDay(kind.lifespanGuestNights));
            f.health = SimMath.Clamp01(f.health - FurnitureWearModel.HealthLossPerIdleDay(f.newness));
        }
    }

    /// <summary>每日故障判定。返回本日新出故障的家具（交给 BreakdownSystem 呈现）。</summary>
    public List<FurnitureInstance> RollDailyFaults(Func<double> roll)
    {
        var newFaults = new List<FurnitureInstance>();
        if (roll == null) return newFaults;
        for (int i = 0; i < _all.Count; i++)
        {
            var f = _all[i];
            if (f.IsFaulted) continue;
            if (roll() >= FurnitureWearModel.FaultChancePerDay(f.health)) continue;
            FurnitureKind kind = FurnitureCatalog.Get(f.kindId);
            int lines = kind.faultLines != null ? kind.faultLines.Length : 0;
            f.faultLineIndex = lines > 0 ? (int)(roll() * lines) % Math.Max(1, lines) : 0;
            newFaults.Add(f);
        }
        return newFaults;
    }

    /// <summary>故障文案（游戏内英文）。</summary>
    public static string FaultLineOf(FurnitureInstance instance)
    {
        if (instance == null || !instance.IsFaulted) return "";
        FurnitureKind kind = FurnitureCatalog.Get(instance.kindId);
        if (kind.faultLines == null || kind.faultLines.Length == 0) return kind.name + " IS BROKEN";
        return kind.faultLines[SimMath.Clamp(instance.faultLineIndex, 0, kind.faultLines.Length - 1)];
    }

    // ── 维修 / 翻新 ───────────────────────────────────────────────────────────

    /// <summary>维修：健康度回满、故障清除，**崭新度一动不动**（用户明确要求）。
    /// 立即生效的版本（测试/胶带式速修用）。</summary>
    public bool Repair(int instanceId)
    {
        if (!_byId.TryGetValue(instanceId, out FurnitureInstance f)) return false;
        f.health = 1f;
        f.faultLineIndex = -1;
        f.repairDaysRemaining = 0;
        return true;
    }

    /// <summary>安排维修：占用工期，期间家具仍不可用（"花钱+花时间"的时间那一半）。</summary>
    public bool BeginRepair(int instanceId, int days)
    {
        if (!_byId.TryGetValue(instanceId, out FurnitureInstance f)) return false;
        if (!f.IsFaulted || f.IsUnderRepair) return false;
        f.repairDaysRemaining = days < 1 ? 1 : days;
        return true;
    }

    /// <summary>推进维修工期（日结时调）。返回本日修好的家具。</summary>
    public List<FurnitureInstance> TickRepairs()
    {
        var done = new List<FurnitureInstance>();
        for (int i = 0; i < _all.Count; i++)
        {
            var f = _all[i];
            if (!f.IsUnderRepair) continue;
            if (--f.repairDaysRemaining > 0) continue;
            f.repairDaysRemaining = 0;
            f.health = 1f;              // 健康度回满
            f.faultLineIndex = -1;      // 崭新度**不动**
            done.Add(f);
        }
        return done;
    }

    /// <summary>所有正在故障（含在修）的家具——手机通知/战术层列表用。</summary>
    public List<FurnitureInstance> FaultedItems()
    {
        var list = new List<FurnitureInstance>();
        for (int i = 0; i < _all.Count; i++) if (_all[i].IsFaulted) list.Add(_all[i]);
        return list;
    }

    /// <summary>翻新（Economy 装修）：该房家具崭新度与健康度都回满，种类不变。
    /// 这是**唯一**能重置崭新度的途径。</summary>
    public void RefurbishRoom(int roomNumber)
    {
        var list = InRoom(roomNumber);
        for (int i = 0; i < list.Count; i++)
        {
            list[i].newness = 1f;
            list[i].health = 1f;
            list[i].faultLineIndex = -1;
        }
    }

    /// <summary>标准装修：翻新 + 必备家具升一档。</summary>
    public void RefurbishAndUpgradeRequired(int roomNumber)
    {
        var list = InRoom(roomNumber);
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            FurnitureKind kind = FurnitureCatalog.Get(f.kindId);
            if (kind.IsRequired) f.kindId = FurnitureCatalog.UpgradedRequiredKind(f.kindId);
            f.newness = 1f;
            f.health = 1f;
            f.faultLineIndex = -1;
        }
    }

    /// <summary>豪华装修：整间换顶配配置。</summary>
    public void ReplaceRoomWithTopTier(int roomNumber)
    {
        var existing = new List<FurnitureInstance>(InRoom(roomNumber));
        for (int i = 0; i < existing.Count; i++) Remove(existing[i].instanceId);
        int[] loadout = FurnitureCatalog.TopTierLoadout();
        for (int i = 0; i < loadout.Length; i++) Place(roomNumber, loadout[i]);
    }

    /// <summary>开局给一间房配上继承来的破家具（崭新度与健康度都很低）。</summary>
    public void FurnishDerelictRoom(int roomNumber, float newness, float health)
    {
        Place(roomNumber, FurnitureCatalog.DerelictKindFor(FurnitureSlot.Bed), newness, health);
        Place(roomNumber, FurnitureCatalog.DerelictKindFor(FurnitureSlot.Bathroom), newness, health);
    }

    // ── 存档 ─────────────────────────────────────────────────────────────────

    public void Clear()
    {
        _all.Clear();
        _byId.Clear();
        _byRoom.Clear();
    }

    /// <summary>读档：按存下来的 id 原样恢复一件家具。</summary>
    public FurnitureInstance RestoreInstance(int instanceId, int kindId, int roomNumber,
                                            float posX, float posY, float newness, float health, int faultLineIndex)
    {
        var instance = new FurnitureInstance
        {
            instanceId = instanceId,
            kindId = kindId,
            roomNumber = roomNumber,
            posX = posX,
            posY = posY,
            newness = SimMath.Clamp01(newness),
            health = SimMath.Clamp01(health),
            faultLineIndex = faultLineIndex,
        };
        _all.Add(instance);
        _byId[instanceId] = instance;
        if (!_byRoom.TryGetValue(roomNumber, out var list))
        {
            list = new List<FurnitureInstance>();
            _byRoom[roomNumber] = list;
        }
        list.Add(instance);
        RestoreIdSeed(instanceId);
        return instance;
    }
}
