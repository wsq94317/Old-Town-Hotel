using System.Collections.Generic;

// 家具摆放锚点（玩家设计："家具摆放主要是预定义一些格子内的锚点，可以摆放在锚点上"）。
// 纯 C#，不引用 UnityEngine。
//
// 尺寸全部**量自真实场景**，不是拍脑袋：房间 5×5，墙厚 0.2，走廊那一侧的门洞在
// 本地 x∈[-0.9,0.9]，所以净内空 4.8×4.8。切成 3×3 个 1.6 的格子刚好铺满，
// **门口那一格刻意留空**（不然客人一进门就撞在柜子上），于是有 8 个锚点。
//
// **南北两排房是镜像的**（并行审计抓到，我做状态灯时也独立踩到）：
// 所有房 euler 都是 0、Wall_Div 都在本地 x=-2.5，但走廊在北排的 -z、南排的 +z。
// 于是"门口那一格"在两排里位置不同——单一布局对三分之一的房间是错的。
// `For(doorOnPositiveZ)` 按房间自己的朝向返回镜像后的布局。
public enum AnchorKind
{
    BedNook,        // 床位（最大的一格，靠里墙）
    Bathroom,       // 卫浴（角落，靠承重墙）
    WallOpposite,   // 床对面的墙：电视挂这儿
    SeatingCorner,  // 沙发角
    DeskWall,       // 书桌靠窗那面
    FloorCentre,    // 地毯（地面中央）
    WallArtLeft,    // 挂画位
    WallArtRight
}

/// <summary>一个锚点：房间本地坐标 + 它收哪些位置的家具。</summary>
public readonly struct FurnitureAnchor
{
    public readonly int anchorId;
    public readonly AnchorKind kind;
    public readonly float localX, localZ;

    public FurnitureAnchor(int anchorId, AnchorKind kind, float localX, float localZ)
    {
        this.anchorId = anchorId;
        this.kind = kind;
        this.localX = localX;
        this.localZ = localZ;
    }

    /// <summary>这个锚点收不收这种位置的家具。
    /// **必备位（床/卫浴）各有专属锚点**——否则玩家可能把地毯摆在唯一能放床的
    /// 格子上，然后这间房永远furnish 不完（那是能把自己锁死的操作）。</summary>
    public bool Accepts(FurnitureSlot slot)
    {
        switch (kind)
        {
            case AnchorKind.BedNook: return slot == FurnitureSlot.Bed;
            case AnchorKind.Bathroom: return slot == FurnitureSlot.Bathroom;
            case AnchorKind.WallOpposite: return slot == FurnitureSlot.Entertainment;
            case AnchorKind.SeatingCorner: return slot == FurnitureSlot.Seating;
            case AnchorKind.DeskWall: return slot == FurnitureSlot.Desk;
            case AnchorKind.FloorCentre: return slot == FurnitureSlot.Floor;
            default: return slot == FurnitureSlot.Wall;   // 两个挂画位
        }
    }
}

public static class FurnitureAnchors
{
    /// <summary>格子边长。房间净内空 4.8，3 格刚好铺满。</summary>
    public const float CellSize = 1.6f;

    /// <summary>每间房的锚点数（3×3 去掉门口那一格）。</summary>
    public const int PerRoom = 8;

    /// <summary>"没有锚点"。存档里旧条目就是这个值，读档时补派。</summary>
    public const int Unassigned = 0;

    // 锚点 id 从 1 开始（0 留给"未指派"），顺序固定——**存档存的是 id，永不重排**
    private static readonly FurnitureAnchor[] NorthDoor =
    {
        // 门在 -z：所以门口那一格是 (0, -1.6)，留空
        new FurnitureAnchor(1, AnchorKind.BedNook,       -1.6f,  1.6f),
        new FurnitureAnchor(2, AnchorKind.Bathroom,       1.6f,  1.6f),
        new FurnitureAnchor(3, AnchorKind.WallOpposite,   0f,    1.6f),
        new FurnitureAnchor(4, AnchorKind.SeatingCorner, -1.6f,  0f),
        new FurnitureAnchor(5, AnchorKind.DeskWall,       1.6f,  0f),
        new FurnitureAnchor(6, AnchorKind.FloorCentre,    0f,    0f),
        new FurnitureAnchor(7, AnchorKind.WallArtLeft,   -1.6f, -1.6f),
        new FurnitureAnchor(8, AnchorKind.WallArtRight,   1.6f, -1.6f),
    };

    /// <summary>这间房的锚点布局。doorOnPositiveZ = 走廊/门在本地 +z 那一侧
    /// （场景里南排的 205-208 就是这样）。镜像只翻 z，**不改 anchorId**——
    /// 翻 id 的话同一个存档在两排房之间会错位。</summary>
    public static IReadOnlyList<FurnitureAnchor> For(bool doorOnPositiveZ)
    {
        if (!doorOnPositiveZ) return NorthDoor;
        var mirrored = new FurnitureAnchor[NorthDoor.Length];
        for (int i = 0; i < NorthDoor.Length; i++)
        {
            var a = NorthDoor[i];
            mirrored[i] = new FurnitureAnchor(a.anchorId, a.kind, a.localX, -a.localZ);
        }
        return mirrored;
    }

    /// <summary>按 id 找锚点（布局与镜像无关的那部分信息）。</summary>
    public static bool TryGet(int anchorId, bool doorOnPositiveZ, out FurnitureAnchor anchor)
    {
        var list = For(doorOnPositiveZ);
        for (int i = 0; i < list.Count; i++)
            if (list[i].anchorId == anchorId) { anchor = list[i]; return true; }
        anchor = default;
        return false;
    }

    /// <summary>这种位置的家具能放在哪些锚点上（按 id 升序，确定性）。</summary>
    public static List<int> AnchorsAccepting(FurnitureSlot slot)
    {
        var result = new List<int>();
        for (int i = 0; i < NorthDoor.Length; i++)
            if (NorthDoor[i].Accepts(slot)) result.Add(NorthDoor[i].anchorId);
        return result;
    }

    /// <summary>给这件家具挑一个空着的合法锚点。挑不到返回 Unassigned。
    /// occupied 是这间房已经被占用的锚点 id。</summary>
    public static int FirstFreeAnchorFor(FurnitureSlot slot, ICollection<int> occupied)
    {
        var candidates = AnchorsAccepting(slot);
        for (int i = 0; i < candidates.Count; i++)
            if (occupied == null || !occupied.Contains(candidates[i])) return candidates[i];
        return Unassigned;
    }

    /// <summary>每个必备位都得有地方放——否则某间房会永远无法配齐而不可售，
    /// 玩家还找不出原因。这条由测试守着。</summary>
    public static bool EveryRequiredSlotHasAnAnchor()
    {
        return AnchorsAccepting(FurnitureSlot.Bed).Count > 0
            && AnchorsAccepting(FurnitureSlot.Bathroom).Count > 0;
    }
}
