using System.Collections.Generic;

// 家具目录（设计文档 §9/§10）。纯 C#，不引用 UnityEngine。
//
// 采用**目录 + 实例**两层而非继承：所有家具行为完全相同（同一套衰减/故障/修理规则），
// 继承只会产出一堆无行为差异的子类。要加新家具只是加一行表。
// 若某类家具将来要特殊行为（电视故障连带电路），用 faultPool + 标记表达即可。

public enum FurnitureSlot
{
    Bed,            // 必备
    Bathroom,       // 必备
    Entertainment,
    Seating,
    Desk,
    Floor,
    Wall
}

/// <summary>一种家具的静态属性。</summary>
public readonly struct FurnitureKind
{
    public readonly int kindId;
    public readonly string name;              // 游戏内文案=英文
    public readonly FurnitureSlot slot;
    public readonly int cashCost;
    public readonly int decorPoints;
    public readonly int lifespanGuestNights;  // 标准寿命（客人晚）
    public readonly int repairCost;
    public readonly int repairDays;
    public readonly float appealBudget, appealBusiness, appealParty, appealVip;
    public readonly string[] faultLines;      // 无厘头故障文案池

    public FurnitureKind(int kindId, string name, FurnitureSlot slot, int cashCost, int decorPoints,
                         int lifespanGuestNights, int repairCost, int repairDays,
                         float appealBudget, float appealBusiness, float appealParty, float appealVip,
                         string[] faultLines)
    {
        this.kindId = kindId;
        this.name = name;
        this.slot = slot;
        this.cashCost = cashCost;
        this.decorPoints = decorPoints;
        this.lifespanGuestNights = lifespanGuestNights;
        this.repairCost = repairCost;
        this.repairDays = repairDays;
        this.appealBudget = appealBudget;
        this.appealBusiness = appealBusiness;
        this.appealParty = appealParty;
        this.appealVip = appealVip;
        this.faultLines = faultLines;
    }

    public bool IsRequired => slot == FurnitureSlot.Bed || slot == FurnitureSlot.Bathroom;

    public float AppealFor(GuestSegment segment)
    {
        switch (segment)
        {
            case GuestSegment.Business: return appealBusiness;
            case GuestSegment.Party: return appealParty;
            case GuestSegment.Vip: return appealVip;
            default: return appealBudget;
        }
    }
}

public static class FurnitureCatalog
{
    // kindId 是稳定身份：存档存的是 id，**永不重排、永不复用**
    public const int SaggingBed = 1;
    public const int ProperBed = 2;
    public const int MemoryFoamBed = 3;
    public const int BasicBathroom = 10;
    public const int RenovatedBathroom = 11;
    public const int BoxyTv = 20;
    public const int BigFlatTv = 21;
    public const int Sofa = 30;
    public const int WorkDesk = 40;
    public const int Rug = 50;
    public const int WallArt = 60;

    /// <summary>满配基准（deliveredQuality 的分母）。**固定常数**——若改成"当前最贵满配"，
    /// 以后加了更好的家具会让老房间凭空贬值，玩家会觉得东西被偷了。
    /// = 顶配床12 + 顶配卫浴9 + 最佳四件可选(8+5+5+4)22 = 43</summary>
    public const int FullyFurnishedDecor = 43;

    /// <summary>每间房的可选装饰位数量。</summary>
    public const int OptionalSlotCount = 4;

    private static readonly Dictionary<int, FurnitureKind> Kinds = BuildKinds();

    private static Dictionary<int, FurnitureKind> BuildKinds()
    {
        var map = new Dictionary<int, FurnitureKind>();
        void Add(FurnitureKind k) => map[k.kindId] = k;

        Add(new FurnitureKind(SaggingBed, "Sagging Bed", FurnitureSlot.Bed, 300, 2, 900, 120, 1,
            0f, 0f, 0f, 0f, new[]
            {
                "THE BED HAS OPINIONS. It voiced one at 3am.",
                "BED FRAME SNAPPED. The guest describes it as 'a controlled descent'.",
            }));
        Add(new FurnitureKind(ProperBed, "Proper Bed", FurnitureSlot.Bed, 700, 6, 1200, 200, 1,
            0f, 0f, 0f, 0.5f, new[]
            {
                "ONE BED LEG IS SHORTER NOW. Nobody knows which one.",
                "MATTRESS HAS DEVELOPED A CRATER.",
            }));
        Add(new FurnitureKind(MemoryFoamBed, "Memory Foam Bed", FurnitureSlot.Bed, 1500, 12, 1500, 350, 2,
            0f, 0.5f, 0f, 1f, new[]
            {
                "THE FOAM REMEMBERS THE LAST GUEST. Vividly.",
                "BED REFUSES TO RETURN TO SHAPE. It has given up.",
            }));

        Add(new FurnitureKind(BasicBathroom, "Basic Bathroom", FurnitureSlot.Bathroom, 400, 3, 1000, 150, 1,
            0f, 0f, 0f, 0f, new[]
            {
                "SHOWER RUNS LAVA OR ICE, NOTHING BETWEEN.",
                "THE TAP WHISTLES. It knows one song.",
            }));
        Add(new FurnitureKind(RenovatedBathroom, "Renovated Bathroom", FurnitureSlot.Bathroom, 1100, 9, 1400, 300, 2,
            0f, 0.5f, 0f, 0.5f, new[]
            {
                "RAIN SHOWER NOW ONLY DRIZZLES SIDEWAYS.",
                "THE HEATED FLOOR PICKED A FAVOURITE TILE.",
            }));

        Add(new FurnitureKind(BoxyTv, "Boxy TV", FurnitureSlot.Entertainment, 250, 3, 700, 90, 1,
            0f, 0f, 0.5f, 0f, new[]
            {
                "TV ONLY GETS ONE CHANNEL. It's a shopping channel.",
                "THE TV TURNS ITSELF ON AT DAWN. Cheerfully.",
            }));
        Add(new FurnitureKind(BigFlatTv, "Big Flat TV", FurnitureSlot.Entertainment, 900, 8, 900, 220, 1,
            0f, 0f, 1f, 0f, new[]
            {
                "SCREEN HAS A DEAD PIXEL SHAPED LIKE A MAN.",
                "REMOTE WORKS FOR THE ROOM NEXT DOOR INSTEAD.",
            }));

        Add(new FurnitureKind(Sofa, "Sofa", FurnitureSlot.Seating, 500, 5, 800, 160, 1,
            0f, 0f, 0.5f, 0.5f, new[]
            {
                "SOFA ATE A GUEST'S PHONE. It is not giving it back.",
                "ONE CUSHION IS NOW STRUCTURAL. Do not remove it.",
            }));
        Add(new FurnitureKind(WorkDesk, "Work Desk", FurnitureSlot.Desk, 400, 4, 1100, 130, 1,
            0f, 1f, 0f, 0f, new[]
            {
                "DESK DRAWER WON'T OPEN. Something inside rattles.",
                "THE DESK WOBBLES IN ONE DIRECTION ONLY.",
            }));
        Add(new FurnitureKind(Rug, "Rug", FurnitureSlot.Floor, 200, 3, 600, 70, 1,
            0f, 0f, 0f, 0.5f, new[]
            {
                "THE RUG HAS A SMELL WITH A PERSONALITY.",
                "RUG EDGE HAS BECOME A TRIP HAZARD WITH AMBITION.",
            }));
        Add(new FurnitureKind(WallArt, "Wall Art", FurnitureSlot.Wall, 350, 5, 2000, 100, 1,
            0f, 0f, 0f, 0.5f, new[]
            {
                "THE PAINTING'S EYES FOLLOW YOU.",
                "THE FRAME WON'T HANG STRAIGHT. It has chosen an angle.",
            }));

        return map;
    }

    /// <summary>这个位置的家具**承重**吗（人躺上去、坐上去、站上去的）。
    /// 从 slot 推导而不是给 11 行目录各加一列——加列要改构造函数签名，
    /// 而 decorPoints 的校准被好几个测试钉着（"每个档位都有一个投资阶段能诚实交付它"），
    /// 碰它风险远大于收益。承重的东西塌下来会伤人，不承重的坏了只是难看。</summary>
    public static bool BearsWeight(FurnitureSlot slot) =>
        slot == FurnitureSlot.Bed || slot == FurnitureSlot.Bathroom || slot == FurnitureSlot.Seating;

    public static bool TryGet(int kindId, out FurnitureKind kind) => Kinds.TryGetValue(kindId, out kind);

    public static FurnitureKind Get(int kindId) =>
        Kinds.TryGetValue(kindId, out FurnitureKind k) ? k : Kinds[SaggingBed];

    public static IEnumerable<FurnitureKind> All => Kinds.Values;

    /// <summary>开局继承的破家具（最便宜那套）。</summary>
    public static int DerelictKindFor(FurnitureSlot slot)
    {
        switch (slot)
        {
            case FurnitureSlot.Bathroom: return BasicBathroom;
            default: return SaggingBed;
        }
    }

    /// <summary>某必备位升一档（标准装修方案用）。已是顶配则返回原样。</summary>
    public static int UpgradedRequiredKind(int kindId)
    {
        switch (kindId)
        {
            case SaggingBed: return ProperBed;
            case ProperBed: return MemoryFoamBed;
            case BasicBathroom: return RenovatedBathroom;
            default: return kindId;
        }
    }

    /// <summary>顶配配置（豪华装修方案用）：必备顶配 + 最佳四件可选。</summary>
    public static int[] TopTierLoadout() =>
        new[] { MemoryFoamBed, RenovatedBathroom, BigFlatTv, Sofa, WallArt, WorkDesk };
}
