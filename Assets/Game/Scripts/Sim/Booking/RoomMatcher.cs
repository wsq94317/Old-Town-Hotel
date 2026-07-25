using System.Collections.Generic;

// 房间分配打分（架构 §B.4「绑定粒度」）。纯 C#，不引用 UnityEngine。
//
// **walk-in 与预订在 check-in 这一点合流**：预订只绑挂牌档，具体房号到店才定，
// 于是两条流问的是同一个问题——"手上这些空房，哪一间最该给这位客人"。
// 一个打分函数、一套测试，避免两条路各自漂移（v1 就是 walk-in 一套、没有预订）。
//
// 打分不只看"能不能住"，还看"该不该给"：把 Better 房贱卖给一位只订了 Old 的
// 预算客，当晚是满房了，但真正订 Better 的人来了就没房——库存保护是这里的核心。

public readonly struct RoomRequest
{
    public readonly RoomTier bookedBand;   // 客人订/要的是哪一档
    public readonly GuestSegment segment;

    public RoomRequest(RoomTier bookedBand, GuestSegment segment)
    {
        this.bookedBand = bookedBand;
        this.segment = segment;
    }
}

public readonly struct RoomCandidate
{
    public readonly int roomNumber;
    public readonly RoomTier band;       // 这间房现在挂的档
    public readonly float delivered;     // 实际交付水平（家具装饰度）
    public readonly float segmentAppeal; // 家具对该客群的偏好加成
    public readonly bool flawed;         // 没验房就上架
    public readonly bool sellable;       // Ready 且必备家具可用

    public RoomCandidate(int roomNumber, RoomTier band, float delivered,
                         float segmentAppeal, bool flawed, bool sellable)
    {
        this.roomNumber = roomNumber;
        this.band = band;
        this.delivered = delivered;
        this.segmentAppeal = segmentAppeal;
        this.flawed = flawed;
        this.sellable = sellable;
    }
}

public static class RoomMatcher
{
    /// <summary>压根不能给这位客人的房。</summary>
    public const float NoMatch = float.NegativeInfinity;

    /// <summary>档位完全对上的奖励。</summary>
    public const float ExactBandBonus = 0.5f;

    /// <summary>每高一档的浪费惩罚（库存保护：好房留给订好房的人）。</summary>
    public const float WastePerBandUp = 0.35f;

    /// <summary>每低一档的降级惩罚——能住但客人要生气，只在没别的选择时用。</summary>
    public const float PenaltyPerBandDown = 1.2f;

    public const float FlawPenalty = 0.4f;

    /// <summary>这间房给这位客人打几分。分数只用于互相比较，绝对值无意义。</summary>
    public static float ScoreFor(in RoomRequest request, in RoomCandidate candidate)
    {
        if (!candidate.sellable) return NoMatch;

        float score = 0f;

        int bandDelta = (int)candidate.band - (int)request.bookedBand;
        if (bandDelta == 0) score += ExactBandBonus;
        else if (bandDelta > 0) score -= WastePerBandUp * bandDelta;   // 别贱卖好房
        else score += PenaltyPerBandDown * bandDelta;                  // bandDelta 为负

        // 交付够不够这位客群的期待（同一把尺：DemandModel 的交付水平 0~1）
        GuestSegmentProfile profile = GuestSegmentProfile.For(request.segment);
        score += candidate.delivered - profile.tierExpectation;

        score += candidate.segmentAppeal;

        // 瑕疵房对差评权重高的客群更致命（VIP 的 satisfactionWeight = 2）
        if (candidate.flawed) score -= FlawPenalty * profile.satisfactionWeight;

        return score;
    }

    /// <summary>挑最合适的一间。全都不能住则返回 false。
    /// 同分取房号小的——**必须稳定**，否则同种子跑两次分房结果不一致，调参对照全废。</summary>
    public static bool TryPick(in RoomRequest request, IReadOnlyList<RoomCandidate> candidates,
                              out int roomNumber)
    {
        roomNumber = 0;
        if (candidates == null) return false;

        float best = NoMatch;
        bool found = false;
        for (int i = 0; i < candidates.Count; i++)
        {
            RoomCandidate c = candidates[i];
            float score = ScoreFor(request, c);
            if (float.IsNegativeInfinity(score)) continue;
            if (found && score <= best && !(score == best && c.roomNumber < roomNumber)) continue;
            best = score;
            roomNumber = c.roomNumber;
            found = true;
        }
        return found;
    }
}
