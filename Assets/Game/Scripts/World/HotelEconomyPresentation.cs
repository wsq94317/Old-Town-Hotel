using UnityEngine;

public readonly struct HotelEconomySnapshot
{
    public readonly int ExpectedDailyGross;
    public readonly int ExpectedDailyNet;
    public readonly int DailyRevenueGoal;
    public readonly float GrossPerMinute;
    public readonly float NetPerMinute;
    public readonly float RevenueLossPerMinute;
    public readonly float ExpectedOccupancy;

    public HotelEconomySnapshot(
        int expectedDailyGross,
        int expectedDailyNet,
        int dailyRevenueGoal,
        float grossPerMinute,
        float netPerMinute,
        float revenueLossPerMinute,
        float expectedOccupancy)
    {
        ExpectedDailyGross = expectedDailyGross;
        ExpectedDailyNet = expectedDailyNet;
        DailyRevenueGoal = dailyRevenueGoal;
        GrossPerMinute = grossPerMinute;
        NetPerMinute = netPerMinute;
        RevenueLossPerMinute = revenueLossPerMinute;
        ExpectedOccupancy = expectedOccupancy;
    }
}

public static class HotelEconomyPresentation
{
    public const float RealMinutesPerGameDay = 120f;
    public const float DefaultOccupancy = 0.72f;

    public static HotelEconomySnapshot Build(
        HotelSim sim,
        float incidentLossPerMinute = 0f,
        float portfolioIncomePerMinute = 0f)
    {
        if (sim == null)
            return new HotelEconomySnapshot(0, 0, 100, 0f, 0f, 0f, DefaultOccupancy);

        float occupancy = ExpectedOccupancy(sim);
        float gross = 0f;
        float roomLoss = 0f;
        for (int i = 0; i < sim.Rooms.Count; i++)
        {
            RoomRecord room = sim.Rooms.Peek(i);
            if (room.state == RoomSimState.Ruined) continue;

            int rate = sim.Pricing.PriceFor(sim.Clock.CurrentDay, room.tier);
            float contribution = rate * occupancy;
            switch (room.state)
            {
                case RoomSimState.Occupied:
                    gross += rate;
                    break;
                case RoomSimState.Ready:
                    gross += contribution;
                    break;
                case RoomSimState.Dirty:
                case RoomSimState.Cleaning:
                case RoomSimState.AwaitingInspection:
                    gross += contribution * 0.55f;
                    roomLoss += contribution * 0.45f;
                    break;
                case RoomSimState.Blocked:
                    roomLoss += contribution;
                    break;
            }
        }

        int wages = sim.Shifts.DailyWageCost(sim.Staff, sim.Clock.CurrentDay);
        int expectedRoomGross = Mathf.Max(0, Mathf.RoundToInt(gross));
        int expectedPortfolioGross = Mathf.RoundToInt(
            Mathf.Max(0f, portfolioIncomePerMinute) * RealMinutesPerGameDay);
        int expectedGross = expectedRoomGross + expectedPortfolioGross;
        int expectedCommission = Mathf.RoundToInt(expectedRoomGross * sim.CommissionRate);
        int expectedNet = expectedGross - expectedCommission - wages;
        float grossPerMinute = expectedGross / RealMinutesPerGameDay;
        float lossPerMinute = roomLoss / RealMinutesPerGameDay
                              + Mathf.Max(0f, incidentLossPerMinute);
        float netPerMinute = expectedNet / RealMinutesPerGameDay - incidentLossPerMinute;
        int dailyGoal = Mathf.Max(100, Mathf.CeilToInt(expectedGross / 100f) * 100);

        return new HotelEconomySnapshot(
            expectedGross,
            expectedNet,
            dailyGoal,
            grossPerMinute,
            netPerMinute,
            lossPerMinute,
            occupancy);
    }

    public static float ExpectedOccupancy(HotelSim sim)
    {
        if (sim == null || sim.Rooms.OpenRoomCount <= 0) return DefaultOccupancy;

        NightOccupancy tonight = sim.OccupancyForNight(sim.Clock.CurrentDay);
        float soldRatio = tonight.capacity > 0
            ? tonight.sold / (float)tonight.capacity
            : sim.ActiveStayCount / (float)sim.Rooms.OpenRoomCount;
        if (soldRatio <= 0.01f) return DefaultOccupancy;
        return Mathf.Clamp(soldRatio, 0.55f, 0.95f);
    }

    public static float RoomRevenuePerMinute(
        HotelSim sim,
        RoomTier tier,
        float occupancy = DefaultOccupancy)
    {
        if (sim == null) return 0f;
        int rate = sim.Pricing.PriceFor(sim.Clock.CurrentDay, tier);
        return Mathf.Max(0f, rate * Mathf.Clamp01(occupancy) / RealMinutesPerGameDay);
    }

    public static int PaybackMinutes(int investment, float gainPerMinute)
    {
        if (investment <= 0) return 0;
        if (gainPerMinute <= 0.001f) return 99999;
        return Mathf.CeilToInt(investment / gainPerMinute);
    }

    public static float OpportunityScore(
        int cost,
        int availableCash,
        float gainPerMinute,
        float lossAvoidedPerMinute,
        bool strategicMilestone,
        bool urgent)
    {
        float returnRate = (Mathf.Max(0f, gainPerMinute)
                            + Mathf.Max(0f, lossAvoidedPerMinute) * 1.2f)
                           / Mathf.Max(1, cost);
        float affordability = cost <= availableCash
            ? 2f
            : Mathf.Clamp01(availableCash / (float)Mathf.Max(1, cost));
        return returnRate * 10000f
               + affordability
               + (strategicMilestone ? 1.5f : 0f)
               + (urgent ? 5f : 0f);
    }
}
