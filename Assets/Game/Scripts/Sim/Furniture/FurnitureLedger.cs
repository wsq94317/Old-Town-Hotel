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

    /// <summary>用胶带糊上了：房间能重新开卖，但客人看得见，而且明天照坏。
    /// 这是**给破产玩家的唯一出路**——现金归零时故障会把房一间间永久封死
    /// （试玩实测：第 23 天 20 间房全在 Blocked，玩家看不见也修不动，局面已死）。
    /// v2 巡查层早有"胶带明日复发"的成语，这里是它在 Sim 侧的对应物。</summary>
    public bool taped;

    /// <summary>这件家具被**将就过**（修过或糊过胶带，而不是换新）。
    /// 塌陷只发生在被将就过的家具上——它记录的是玩家的一个决定，
    /// 不是一个倒计时。开局那批破家具在玩家做任何选择之前不会塌人。</summary>
    public bool patchedUp;

    /// <summary>塌了（不只是坏了）。**修不了**——只能换新或者先糊胶带顶着。
    /// 承重家具（床/卫浴/沙发）在健康度极低时会塌，而且会伤到住在里面的客人。</summary>
    public bool wrecked;

    public bool IsFaulted => faultLineIndex >= 0;

    /// <summary>还修得动吗（塌掉的修不动）。</summary>
    public bool IsRepairable => IsFaulted && !IsUnderRepair && !wrecked;
    public bool IsUnderRepair => repairDaysRemaining > 0;

    /// <summary>能不能用（糊上的也算能用——只是不体面）。</summary>
    public bool IsUsable => !IsFaulted || taped;
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
            // 糊了胶带的按"故障"算装饰度（能用，但一点不体面）——
            // 于是"糊上继续卖"会把交付水平压下去，虚报挂牌的人立刻吃退款
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
            if (!f.IsUsable) continue;      // 糊了胶带的算可用：房间能重新开卖
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

    /// <summary>一间房里最差那件家具的健康度（0-1）。
    /// **UI 必须能显示它**：整套"修好的床几天后还会塌"的机制都建立在健康度上，
    /// 而在加这套机制之前，三个面板里 grep "health" 是零命中——
    /// 后果全都会变成玩家眼里"莫名其妙发生的事"（设计审计点名的头号问题）。</summary>
    public float WorstHealthIn(int roomNumber)
    {
        var list = InRoom(roomNumber);
        if (list.Count == 0) return 1f;
        float worst = 1f;
        for (int i = 0; i < list.Count; i++)
            if (list[i].health < worst) worst = list[i].health;
        return worst;
    }

    /// <summary>把健康度说成人话。数字（0.28）对玩家没有意义，
    /// "快塌了"有意义——而且这四个词直接对应故障线与塌陷线两个阈值。</summary>
    public static string ConditionWord(float health, bool wrecked)
    {
        if (wrecked) return "WRECKED";
        if (health < FurnitureWearModel.CollapseHealthThreshold) return "ABOUT TO GIVE WAY";
        if (health < FurnitureWearModel.TroubleThreshold) return "BREAKS CONSTANTLY";
        if (health < 0.7f) return "worn but fine";
        return "solid";
    }

    // ── 维修 / 翻新 ───────────────────────────────────────────────────────────

    /// <summary>维修：故障清除，健康度回到**这件家具剩下的寿命**为止
    /// （FurnitureWearModel.RepairedHealthCeiling），**崭新度一动不动**。
    /// 修一张破床只能修成一张能用的破床——它几天后照样会坏，这是设计。
    /// 已经在上限之上的（健康家具偶发故障）不往下压，修完照旧健康。
    /// 立即生效的版本（测试/胶带式速修用）。</summary>
    public bool Repair(int instanceId)
    {
        if (!_byId.TryGetValue(instanceId, out FurnitureInstance f)) return false;
        float ceiling = FurnitureWearModel.RepairedHealthCeiling(f.newness);
        if (f.health < ceiling) f.health = ceiling;
        f.faultLineIndex = -1;
        f.repairDaysRemaining = 0;
        f.patchedUp = true;             // 将就过了：以后有塌的可能
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
            float ceiling = FurnitureWearModel.RepairedHealthCeiling(f.newness);
            if (f.health < ceiling) f.health = ceiling;   // 回到"剩下的寿命"为止，不回满
            f.faultLineIndex = -1;      // 崭新度**不动**
            f.taped = false;            // 真修好了，胶带撕掉
            f.patchedUp = true;         // 但将就过了：以后有塌的可能
            done.Add(f);
        }
        return done;
    }

    /// <summary>胶带过夜失效。返回失效的件数。
    /// 不撕掉的话胶带就成了永久免费维修，"修理要钱"整条经济链当场失去意义——
    /// v2 巡查层的胶带本来就是"明日复发"的语义，这里保持一致。
    /// 注意：只清 taped 标记，faultLineIndex 原样留着，所以次晨房间会重新被封。</summary>
    public int ExpireTape()
    {
        int n = 0;
        for (int i = 0; i < _all.Count; i++)
        {
            if (!_all[i].taped) continue;
            _all[i].taped = false;
            n++;
        }
        return n;
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

    /// <summary>给一间房装上**全新的必备家具**（床 + 卫浴，中间档）。破败房复原的 Refit 用。
    /// 房里原有的东西先清掉——复原是把废房重做，不是在旧家具上打补丁。</summary>
    public void FurnishWithNewRequired(int roomNumber)
    {
        var existing = new List<FurnitureInstance>(InRoom(roomNumber));
        for (int i = 0; i < existing.Count; i++) Remove(existing[i].instanceId);
        Place(roomNumber, FurnitureCatalog.ProperBed);
        Place(roomNumber, FurnitureCatalog.RenovatedBathroom);
    }

    /// <summary>把房内家具修到能用：**健康度回满、崭新度一点不动**。
    /// 这是"请维修工修旧家具"那条路的核心——`RefurbishRoom` 会把崭新度也重置，
    /// 那是翻新性装修的特权（崭新度只有翻新能重置，是定下的铁律）。</summary>
    public void ReviveRoomHealth(int roomNumber)
    {
        var list = InRoom(roomNumber);
        for (int i = 0; i < list.Count; i++)
        {
            list[i].health = 1f;
            list[i].faultLineIndex = -1;
            list[i].repairDaysRemaining = 0;
            list[i].taped = false;
        }
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
