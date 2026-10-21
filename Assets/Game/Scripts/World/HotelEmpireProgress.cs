using System.Collections.Generic;
using UnityEngine;

public enum PortfolioHotelKind { Legacy, Value, Business, Resort }

public static class HotelEmpireProgress
{
    private static readonly List<PortfolioHotelKind> Hotels = new List<PortfolioHotelKind>();
    public static int HotelCount => 1 + Hotels.Count;
    public static IReadOnlyList<PortfolioHotelKind> Properties => Hotels;
    public static int AcquisitionCost(PortfolioHotelKind kind) =>
        (kind == PortfolioHotelKind.Business ? 35000 : kind == PortfolioHotelKind.Resort ? 45000 : 25000) * HotelCount;
    public static int GrossOf(PortfolioHotelKind kind) => kind == PortfolioHotelKind.Business ? 1050
        : kind == PortfolioHotelKind.Resort ? 1600 : kind == PortfolioHotelKind.Legacy ? 960 : 720;
    public static int UpkeepOf(PortfolioHotelKind kind) => kind == PortfolioHotelKind.Business ? 350
        : kind == PortfolioHotelKind.Resort ? 750 : kind == PortfolioHotelKind.Legacy ? 0 : 240;
    public static int NextHotelCost => AcquisitionCost(PortfolioHotelKind.Value);
    public static int DailyIncome {
        get { int value = 0; foreach (var kind in Hotels) value += GrossOf(kind) - UpkeepOf(kind); return value; }
    }
    public static float PassiveIncomePerMinute => DailyIncome / HotelEconomyPresentation.RealMinutesPerGameDay;
    public static int IncomeForDay(int day)
    {
        int value = DailyIncome;
        for (int i = 0; i < Hotels.Count; i++) if (Hotels[i] == PortfolioHotelKind.Resort)
            value += ((day + i * 3) % 5 - 2) * 150;
        return value;
    }
    public static bool TryAcquireNextHotel(HotelSim sim, out string message) =>
        TryAcquire(sim, PortfolioHotelKind.Value, out message);
    public static bool TryAcquire(HotelSim sim, PortfolioHotelKind kind, out string message)
    {
        if (kind == PortfolioHotelKind.Legacy) kind = PortfolioHotelKind.Value;
        int cost = AcquisitionCost(kind);
        if (sim == null || !sim.TrySpendCash(cost)) {
            message = "Not enough available cash. ($" + cost + ")"; return false;
        }
        Hotels.Add(kind);
        message = "HOTEL NO. " + HotelCount + " ACQUIRED - " + kind + ", net $"
            + (GrossOf(kind) - UpkeepOf(kind)) + "/day before seasonal variation.";
        return true;
    }
    public static void CaptureTo(WorldState world)
    {
        if (world == null) return;
        world.hotelCount = HotelCount;
        world.portfolioKinds = new List<int>();
        foreach (var kind in Hotels) world.portfolioKinds.Add((int)kind);
    }
    public static void RestoreFrom(WorldState world)
    {
        Hotels.Clear();
        if (world == null) return;
        if (world.portfolioKinds != null && world.portfolioKinds.Count > 0)
            foreach (int kind in world.portfolioKinds) Hotels.Add((PortfolioHotelKind)Mathf.Clamp(kind, 0, 3));
        else for (int i = 1; i < Mathf.Max(1, world.hotelCount); i++) Hotels.Add(PortfolioHotelKind.Legacy);
    }
    public static void ResetForNewGame() => Hotels.Clear();
}
