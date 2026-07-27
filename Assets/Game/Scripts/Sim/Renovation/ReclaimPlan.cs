using System.Collections.Generic;

// 破败房复原（用户设计）。纯 C#，不引用 UnityEngine。
//
// **破败房不是脏，是废。** 以前 $800 一键"清理"就把一间百年破房变成"只是有点脏"，
// 语义上说不过去，而且 RenovatableRooms 还明确跳过 Ruined——破败房压根不能装修，
// 必须先花钱"打扫"出来。现在反过来：破败房只能开工复原，而复原是装修的一种。
//
// 三档共用现有装修的语言（便宜=关得久，贵=队伍大反而快完工），
// 但**产出的差别不在档位而在家具**——这正好接上 M-C2 的"挂牌档 vs 交付"：
//
//   PatchUp  修旧家具：请外包维修工把原来的破家具修到能用。最省钱、工期最长。
//            崭新度**一点不回**（维修只回健康度，这是铁律），交付约 0.05，
//            只配挂 Old。想提档得以后再单独花钱换家具。
//   Refit    换必备家具：新床 + 新卫浴。中等价、中等工期。交付约 0.35，可以诚实挂 Basic。
//   FullFit  换全套：必备 + 电视/沙发/书桌/地毯。最贵、队伍最大所以最快。
//            交付约 0.81，够挂 Better。
//
// 完工后房间进 Dirty 而不是直接可售——施工完总得打扫。于是复原也会**占用客房部工时**，
// 和退房脏房抢同一批人手，"什么时候开工"因此多了一层考虑。
public enum ReclaimPlanKind
{
    PatchUp,   // 修旧家具：最省，最慢，只配 Old
    Refit,     // 换必备：中等
    FullFit    // 换全套：最贵，最快
}

/// <summary>一种复原方案的参数（纯数据）。</summary>
public readonly struct ReclaimPlan
{
    public readonly ReclaimPlanKind kind;
    public readonly int cashPerRoom;
    public readonly int materialsPerRoom;
    public readonly int blockDays;

    /// <summary>是否沿用原有家具（true = 请维修工修旧的，false = 装新的）。</summary>
    public readonly bool keepsOldFurniture;

    /// <summary>换新时装到哪一档：只装必备，还是连可选装饰位一起装满。</summary>
    public readonly bool fitsOptionalSlots;

    public ReclaimPlan(ReclaimPlanKind kind, int cashPerRoom, int materialsPerRoom, int blockDays,
                       bool keepsOldFurniture, bool fitsOptionalSlots)
    {
        this.kind = kind;
        this.cashPerRoom = cashPerRoom;
        this.materialsPerRoom = materialsPerRoom;
        this.blockDays = blockDays;
        this.keepsOldFurniture = keepsOldFurniture;
        this.fitsOptionalSlots = fitsOptionalSlots;
    }

    public static ReclaimPlan For(ReclaimPlanKind kind)
    {
        switch (kind)
        {
            // 修旧家具明显比换新省（用户要求"相比于装修要更省"），代价是慢 + 交付垫底
            case ReclaimPlanKind.PatchUp:
                return new ReclaimPlan(kind, cashPerRoom: 600, materialsPerRoom: 2, blockDays: 7,
                                       keepsOldFurniture: true, fitsOptionalSlots: false);
            case ReclaimPlanKind.Refit:
                return new ReclaimPlan(kind, cashPerRoom: 1400, materialsPerRoom: 4, blockDays: 4,
                                       keepsOldFurniture: false, fitsOptionalSlots: false);
            default:
                return new ReclaimPlan(kind, cashPerRoom: 3200, materialsPerRoom: 8, blockDays: 2,
                                       keepsOldFurniture: false, fitsOptionalSlots: true);
        }
    }

    public static string LabelOf(ReclaimPlanKind kind)
    {
        switch (kind)
        {
            case ReclaimPlanKind.PatchUp:
                return "PATCH UP - a handyman revives the old furniture. Cheap, slow, still shabby";
            case ReclaimPlanKind.Refit:
                return "REFIT - new bed and bathroom. Sellable as Basic";
            default:
                return "FULL FIT - everything new. Costs a fortune, opens fastest";
        }
    }
}

/// <summary>复原报价（与装修共用批量折扣曲线：整层一起开工才划算）。</summary>
public static class ReclaimPricing
{
    public static int CashCostFor(ReclaimPlan plan, int roomCount)
    {
        if (roomCount <= 0) return 0;
        float gross = plan.cashPerRoom * roomCount;
        return SimMath.RoundToInt(gross * (1f - RenovationPricing.DiscountFor(roomCount)));
    }

    /// <summary>材料不打折（东西就是那么多）。</summary>
    public static int MaterialCostFor(ReclaimPlan plan, int roomCount) =>
        roomCount <= 0 ? 0 : plan.materialsPerRoom * roomCount;

    /// <summary>工期：与装修同一条规则，每多 4 间 +1 天（一支施工队干不完那么多房）。</summary>
    public static int BlockDaysFor(ReclaimPlan plan, int roomCount)
    {
        if (roomCount <= 0) return 0;
        int extra = (roomCount - 1) / RenovationPricing.RoomsPerExtraBlockDay;
        return plan.blockDays + extra;
    }

    public static int CashPerRoomFor(ReclaimPlan plan, int roomCount) =>
        roomCount <= 0 ? 0 : CashCostFor(plan, roomCount) / roomCount;
}
