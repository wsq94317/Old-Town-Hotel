using System.Collections.Generic;

// 装修系统（架构 A.2 / spec §4：**核心长线玩法**）。纯 C#，不引用 UnityEngine。
//
// 设计要点：不是"点一下就装修"，而是有计算性的取舍——
//   经济方案：便宜，但 Block 天数最长（房间关得久=损失的间夜多）
//   标准方案：多花钱买时间（Block 短）
//   豪华方案：直接跳到最高档位，房价与需求大涨，但投入与工期都最大
//   批量装修：单价折扣，但一次性现金/材料占用大、Block 期还会被拉长
// 真正的代价从来不是造价，是"这几间房这几天不能卖"。

public enum RenovationPlanKind
{
    Economy,   // 经济：省钱换工期
    Standard,  // 标准：花钱买时间
    Luxury     // 豪华：一步到顶档
}

/// <summary>一种装修方案的参数（纯数据）。</summary>
public readonly struct RenovationPlan
{
    public readonly RenovationPlanKind kind;
    public readonly RoomTier targetTier;
    public readonly int cashPerRoom;
    public readonly int materialsPerRoom;
    public readonly int blockDays;

    public RenovationPlan(RenovationPlanKind kind, RoomTier targetTier,
                          int cashPerRoom, int materialsPerRoom, int blockDays)
    {
        this.kind = kind;
        this.targetTier = targetTier;
        this.cashPerRoom = cashPerRoom;
        this.materialsPerRoom = materialsPerRoom;
        this.blockDays = blockDays;
    }

    public static RenovationPlan For(RenovationPlanKind kind)
    {
        switch (kind)
        {
            // M-C 调参：造价压到"约 10-15 天回本"，装修才值得当核心玩法
            case RenovationPlanKind.Economy:
                return new RenovationPlan(kind, RoomTier.Basic, cashPerRoom: 900,
                                          materialsPerRoom: 2, blockDays: 4);
            case RenovationPlanKind.Standard:
                return new RenovationPlan(kind, RoomTier.Basic, cashPerRoom: 1300,
                                          materialsPerRoom: 3, blockDays: 2);
            default:
                return new RenovationPlan(kind, RoomTier.Better, cashPerRoom: 3400,
                                          materialsPerRoom: 6, blockDays: 5);
        }
    }

    public static string LabelOf(RenovationPlanKind kind)
    {
        switch (kind)
        {
            case RenovationPlanKind.Economy: return "ECONOMY - cheap, and the room sits shut for a while";
            case RenovationPlanKind.Standard: return "STANDARD - pay extra to get it back on sale sooner";
            default: return "LUXURY - straight to the top tier, straight out of your pocket";
        }
    }
}

/// <summary>批量折扣与工期规则。</summary>
public static class RenovationPricing
{
    public const float DiscountPerExtraRoom = 0.06f;
    public const float MaxDiscount = 0.35f;

    /// <summary>每多 4 间，Block 期 +1 天（一支施工队干不完那么多房）。</summary>
    public const int RoomsPerExtraBlockDay = 4;

    public static float DiscountFor(int roomCount)
    {
        if (roomCount <= 1) return 0f;
        float d = DiscountPerExtraRoom * (roomCount - 1);
        return d > MaxDiscount ? MaxDiscount : d;
    }

    /// <summary>一批装修的现金总价（含批量折扣）。</summary>
    public static int CashCostFor(RenovationPlan plan, int roomCount)
    {
        if (roomCount <= 0) return 0;
        float gross = plan.cashPerRoom * roomCount;
        return SimMath.RoundToInt(gross * (1f - DiscountFor(roomCount)));
    }

    /// <summary>材料不打折（东西就是那么多）。</summary>
    public static int MaterialCostFor(RenovationPlan plan, int roomCount) =>
        roomCount <= 0 ? 0 : plan.materialsPerRoom * roomCount;

    /// <summary>一批装修的 Block 天数（批量越大工期越长）。</summary>
    public static int BlockDaysFor(RenovationPlan plan, int roomCount)
    {
        if (roomCount <= 0) return 0;
        int extra = (roomCount - 1) / RoomsPerExtraBlockDay;
        return plan.blockDays + extra;
    }

    /// <summary>单间均价（UI 展示批量折扣有多划算）。</summary>
    public static int CashPerRoomFor(RenovationPlan plan, int roomCount) =>
        roomCount <= 0 ? 0 : CashCostFor(plan, roomCount) / roomCount;
}

/// <summary>材料库存（M-B 期为单一资源；材料搭配组合表 M-G 展开）。</summary>
public sealed class MaterialStore
{
    public const int DefaultUnitPrice = 45;

    public int Stock { get; private set; }
    public int UnitPrice { get; set; } = DefaultUnitPrice;

    public MaterialStore(int stock = 0) { Stock = stock < 0 ? 0 : stock; }

    public int PriceFor(int units) => units <= 0 ? 0 : units * UnitPrice;

    public void Add(int units)
    {
        if (units <= 0) return;
        Stock += units;
    }

    public bool TryConsume(int units)
    {
        if (units <= 0) return true;
        if (Stock < units) return false;
        Stock -= units;
        return true;
    }

    public void RestoreFromSave(int stock) => Stock = stock < 0 ? 0 : stock;
}

/// <summary>一个在建的装修单。</summary>
public sealed class SimRenovationJob
{
    public int jobId;
    public RenovationPlanKind planKind;
    public RoomTier targetTier;
    public int daysRemaining;
    public readonly List<int> roomNumbers = new List<int>();
}

/// <summary>施工队列：按天推进，完工返回受影响的房间。</summary>
public sealed class SimRenovationQueue
{
    private readonly List<SimRenovationJob> _jobs = new List<SimRenovationJob>();
    private int _nextJobId;

    public IReadOnlyList<SimRenovationJob> Active => _jobs;

    public int RoomsUnderRenovation
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _jobs.Count; i++) n += _jobs[i].roomNumbers.Count;
            return n;
        }
    }

    public int Enqueue(RenovationPlan plan, IList<int> roomNumbers)
    {
        if (roomNumbers == null || roomNumbers.Count == 0) return 0;
        var job = new SimRenovationJob
        {
            jobId = ++_nextJobId,
            planKind = plan.kind,
            targetTier = plan.targetTier,
            daysRemaining = RenovationPricing.BlockDaysFor(plan, roomNumbers.Count),
        };
        job.roomNumbers.AddRange(roomNumbers);
        _jobs.Add(job);
        return job.jobId;
    }

    public bool IsRenovating(int roomNumber)
    {
        for (int i = 0; i < _jobs.Count; i++)
            if (_jobs[i].roomNumbers.Contains(roomNumber)) return true;
        return false;
    }

    public int DaysRemainingFor(int roomNumber)
    {
        for (int i = 0; i < _jobs.Count; i++)
            if (_jobs[i].roomNumbers.Contains(roomNumber)) return _jobs[i].daysRemaining;
        return 0;
    }

    /// <summary>推进一天。返回本日完工的工单。</summary>
    public List<SimRenovationJob> TickDay()
    {
        var finished = new List<SimRenovationJob>();
        for (int i = _jobs.Count - 1; i >= 0; i--)
        {
            _jobs[i].daysRemaining--;
            if (_jobs[i].daysRemaining > 0) continue;
            finished.Add(_jobs[i]);
            _jobs.RemoveAt(i);
        }
        finished.Reverse(); // 保持下单顺序，结果可复现
        return finished;
    }
}
