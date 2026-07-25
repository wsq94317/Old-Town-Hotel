using System.Collections.Generic;

// room-night 库存（架构 §B.4 修订版 3）。纯 C#，不引用 UnityEngine。
//
// **三元式 capacity / demand / remaining**，逐日逐档独立记账：
//   capacity[day][tier]  = 该档营业房量 − 该日被 Block 的房（装修/锁房）
//   demand[day][tier]    = 覆盖该日的已确认预订数（一张 n 晚单在 n 个日子各占 1）
//   remaining            = capacity − demand（可以是负数 = 超售）
//
// 修订版 2 原本按"stayNights 减一次"记账：一张 3 晚的单从某一天减掉 3 间，
// 既没占住后两晚、又把当天算重了三倍——重复计数会凭空判出超售。
// 这里的铁律是**一晚就是一个间夜，各日独立**。
//
// 容量是"设置"而不是"扣减"：装修 Block 掉房只需把那天的 capacity 调低，
// 已经卖掉的间夜不会被抹掉，于是 remaining 自然变负 = 那天超售。
// 这正是装修的真代价，也是超售事件的唯一来源之一。
public sealed class AvailabilityCalendar
{
    private const int TierCount = 3;   // RoomTier 成员数（Old/Basic/Better）

    // 按天存一小段数组，比 Dictionary<(day,tier)> 少一半分配，也让整天清理成为 O(1)
    private readonly Dictionary<int, int[]> _capacity = new Dictionary<int, int[]>();
    private readonly Dictionary<int, int[]> _demand = new Dictionary<int, int[]>();

    /// <summary>已登记容量的日子（UI 画未来 14 天的库存条用；顺序不保证）。</summary>
    public IEnumerable<int> KnownDays => _capacity.Keys;

    private static int Index(RoomTier tier) => (int)tier;

    private static int[] Row(Dictionary<int, int[]> map, int day, bool create)
    {
        if (map.TryGetValue(day, out int[] row)) return row;
        if (!create) return null;
        row = new int[TierCount];
        map[day] = row;
        return row;
    }

    /// <summary>设定某日某档的可售房量。**是设置不是累加**——每晨按房态重算即可。</summary>
    public void SetCapacity(int day, RoomTier tier, int rooms)
    {
        int[] row = Row(_capacity, day, create: true);
        row[Index(tier)] = rooms < 0 ? 0 : rooms;
    }

    public int CapacityOn(int day, RoomTier tier)
    {
        int[] row = Row(_capacity, day, create: false);
        return row == null ? 0 : row[Index(tier)];
    }

    public int DemandOn(int day, RoomTier tier)
    {
        int[] row = Row(_demand, day, create: false);
        return row == null ? 0 : row[Index(tier)];
    }

    public int RemainingOn(int day, RoomTier tier) => CapacityOn(day, tier) - DemandOn(day, tier);

    public bool IsOversold(int day, RoomTier tier) => RemainingOn(day, tier) < 0;

    /// <summary>某日超售了几间（≥0）。超售事件要按人数逐个处置。</summary>
    public int OversoldCountOn(int day, RoomTier tier)
    {
        int remaining = RemainingOn(day, tier);
        return remaining < 0 ? -remaining : 0;
    }

    /// <summary>0 或负的晚数按 1 晚算——一张不占库存的"白住单"是最难查的账目 bug。</summary>
    private static int NormaliseNights(int nights) => nights < 1 ? 1 : nights;

    /// <summary>占住 arrivalDay 起连续 nights 天，每天各一个间夜。</summary>
    public void Reserve(int arrivalDay, int nights, RoomTier tier)
    {
        int n = NormaliseNights(nights);
        int t = Index(tier);
        for (int d = arrivalDay; d < arrivalDay + n; d++) Row(_demand, d, create: true)[t]++;
    }

    /// <summary>取消/入住转实占时回补库存。绝不减成负数——那等于凭空多出容量。</summary>
    public void Release(int arrivalDay, int nights, RoomTier tier)
    {
        int n = NormaliseNights(nights);
        int t = Index(tier);
        for (int d = arrivalDay; d < arrivalDay + n; d++)
        {
            int[] row = Row(_demand, d, create: false);
            if (row == null) continue;
            if (row[t] > 0) row[t]--;
        }
    }

    /// <summary>这张单能不能接：**整段里每一晚都要有位**，一晚不够就整单拒绝。
    /// overbookingAllowance = 玩家设的"故意超售"档（赌渠道取消率）。</summary>
    public bool CanAccept(int arrivalDay, int nights, RoomTier tier, int overbookingAllowance = 0)
    {
        int n = NormaliseNights(nights);
        int allowance = overbookingAllowance < 0 ? 0 : overbookingAllowance;
        for (int d = arrivalDay; d < arrivalDay + n; d++)
            if (RemainingOn(d, tier) + allowance < 1) return false;
        return true;
    }

    /// <summary>丢掉 day 之前的日子（已成为历史）。未来的一条都不能碰。</summary>
    public void PruneBefore(int day)
    {
        PruneMap(_capacity, day);
        PruneMap(_demand, day);
    }

    private static void PruneMap(Dictionary<int, int[]> map, int day)
    {
        List<int> stale = null;
        foreach (int existing in map.Keys)
        {
            if (existing >= day) continue;
            (stale ?? (stale = new List<int>())).Add(existing);
        }
        if (stale == null) return;
        for (int i = 0; i < stale.Count; i++) map.Remove(stale[i]);
    }

    /// <summary>只清需求、留容量。BookingBook 每晨全量重算需求时用——
    /// 增量维护库存漏一处就永久对不上账，重算是覆盖而不是累加。</summary>
    public void ClearDemand() => _demand.Clear();

    public void Clear()
    {
        _capacity.Clear();
        _demand.Clear();
    }
}
