using System;
using UnityEngine;

// Tunable renovation economics. (Phase 5 Renovation.)
[CreateAssetMenu(fileName = "RenovationConfig", menuName = "Old Town Hotel/Renovation Config", order = 104)]
public sealed class RenovationConfigSO : ScriptableObject
{
    [Header("Nightly revenue by tier")]
    public int oldNightlyRevenue = 80;
    public int basicNightlyRevenue = 110;
    public int betterNightlyRevenue = 190;

    [Header("Upgrade cost (to target tier)")]
    public int basicCost = 1500;
    public int betterCost = 3500;

    [Header("Duration in days (to target tier)")]
    public int basicDays = 3;
    public int betterDays = 4;

    [Header("Batch renovation discount")]
    [Range(0f, 1f)] public float batchDiscountPerExtraRoom = 0.1f; // each extra room in a batch
    [Range(0f, 1f)] public float maxBatchDiscount = 0.4f;

    public int NightlyRevenueFor(RoomTier tier)
    {
        switch (tier)
        {
            case RoomTier.Better: return betterNightlyRevenue;
            case RoomTier.Basic: return basicNightlyRevenue;
            default: return oldNightlyRevenue;
        }
    }

    public int CostFor(RoomTier target)
    {
        switch (target)
        {
            case RoomTier.Better: return betterCost;
            case RoomTier.Basic: return basicCost;
            default: return 0;
        }
    }

    public int DaysFor(RoomTier target)
    {
        switch (target)
        {
            case RoomTier.Better: return betterDays;
            case RoomTier.Basic: return basicDays;
            default: return 0;
        }
    }

    // Total cost to renovate `roomCount` rooms to `target` at once, with a batch discount.
    public int BatchCost(RoomTier target, int roomCount)
    {
        if (roomCount <= 0) return 0;
        float discount = Math.Min(maxBatchDiscount, (roomCount - 1) * batchDiscountPerExtraRoom);
        long total = (long)CostFor(target) * roomCount;
        return (int)Math.Round(total * (1.0 - discount), MidpointRounding.AwayFromZero);
    }
}
