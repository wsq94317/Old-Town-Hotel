using UnityEngine;

public static class HotelEmpireProgress
{
    private const int BaseAcquisitionCost = 25000;
    private const float PassiveIncomePerHotelPerMinute = 8f;
    private static int _hotelCount = 1;

    public static int HotelCount => Mathf.Max(1, _hotelCount);
    public static int NextHotelCost => BaseAcquisitionCost * HotelCount;
    public static float PassiveIncomePerMinute =>
        Mathf.Max(0, HotelCount - 1) * PassiveIncomePerHotelPerMinute;
    public static int DailyIncome => Mathf.RoundToInt(
        PassiveIncomePerMinute * HotelEconomyPresentation.RealMinutesPerGameDay);

    public static bool TryAcquireNextHotel(HotelSim sim, out string message)
    {
        int cost = NextHotelCost;
        if (sim == null || !sim.TrySpendCash(cost))
        {
            message = "Not enough available cash. ($" + cost + ")";
            return false;
        }

        _hotelCount++;
        message = "HOTEL NO. " + HotelCount + " ACQUIRED - portfolio income +$"
                  + PassiveIncomePerHotelPerMinute.ToString("0")
                  + "/min.";
        return true;
    }

    public static void CaptureTo(WorldState world)
    {
        if (world != null) world.hotelCount = HotelCount;
    }

    public static void RestoreFrom(WorldState world)
    {
        _hotelCount = world != null ? Mathf.Max(1, world.hotelCount) : 1;
    }

    public static void ResetForNewGame()
    {
        _hotelCount = 1;
    }
}
