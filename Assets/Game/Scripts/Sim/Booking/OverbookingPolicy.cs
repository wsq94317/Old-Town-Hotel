// 超售处置（架构 §B.4「超售落地日触发 Overbooking 事件，处置复用 ComplaintDecision 链」）。
// 纯 C#，不引用 UnityEngine。
//
// 超售有两个来源，都是玩家自己招来的：
//   ① 把已经卖掉的间夜送去装修（容量下调 ⇒ remaining 变负）
//   ② 开了"故意超售"档赌取消率，结果没那么多人取消
// 客人站在前台、你没房——这时候的三选一才是这个机制的玩法所在：
//
//   升级换房 Upgrade    ：等有房就塞进去（哪怕是更贵的档）。客人反而高兴，
//                        但你按原价收——赚得少，口碑最好。**要有房才行。**
//   赔钱送走 Compensate ：掏现金给他安排到别处。钱最疼，口碑中等。
//   硬赶 WalkAway       ：一分不花，声誉重罚。现金流断了的时候的最后手段。
//
// 无视到日结 = 按硬赶处理，且额外再掉一记声誉（比主动选还亏——拖着不处理
// 从来不该是划算的选项，退款链已经立过这个规矩）。
public enum OverbookingResolution
{
    Upgrade,      // 等到有房，安排进去（可能是更高档的房，按原价）
    Compensate,   // 赔钱送去别家
    WalkAway      // 直接赶走
}

public static class OverbookingPolicy
{
    /// <summary>赔钱送走的现金代价 = 锁定房价的倍数（要替他付别家的房费，还要赔个不是）。</summary>
    public const float CompensationMultiplier = 1.5f;

    /// <summary>无视到日结时额外记几条最低满意度样本。</summary>
    public const int IgnoredExtraReputationSamples = 2;

    public static int CompensationFor(int lockedPrice)
    {
        if (lockedPrice <= 0) return 0;
        return SimMath.RoundToInt(lockedPrice * CompensationMultiplier);
    }

    /// <summary>各处置留给客人的满意度（进声誉样本）。
    ///
    /// **数值必须落在 ReputationLedger 的 [Min, Max] 区间里，1.0 是中性**——
    /// 这个区间是 0.5~1.5 而不是 0~1，第一版按 0 起算，结果"赔钱"给到 0.45，
    /// 比"硬赶"的 0.5 还低：花了钱反而更掉星，赔钱成了纯亏的选项（测试当场抓到）。
    ///   升级 1.2 ：订了便宜档却住进好房，是**正面**体验（酒店业真实的救场手法）
    ///   赔钱 0.8 ：被折腾了一趟但拿到补偿，略低于中性
    ///   硬赶 Min ：最低分</summary>
    public static float SatisfactionFor(OverbookingResolution resolution)
    {
        switch (resolution)
        {
            case OverbookingResolution.Upgrade: return 1.2f;
            case OverbookingResolution.Compensate: return 0.8f;
            default: return ReputationLedger.MinSatisfaction;   // 硬赶
        }
    }

    /// <summary>这次处置要不要占用一间房。</summary>
    public static bool NeedsARoom(OverbookingResolution resolution) =>
        resolution == OverbookingResolution.Upgrade;

    /// <summary>这次处置要不要掏现金。</summary>
    public static bool CostsCash(OverbookingResolution resolution) =>
        resolution == OverbookingResolution.Compensate;
}
