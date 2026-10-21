public static class WarehouseExpansion
{
    public static int NextCapacity(int capacity) => capacity <= 0 || capacity >= 400 ? 0
        : capacity < 80 ? 80 : capacity < 160 ? 160 : 400;
    public static int Cost(int capacity) => NextCapacity(capacity) == 0 ? 0
        : capacity < 80 ? 1500 : capacity < 160 ? 4000 : 9000;
}

public sealed partial class HotelSim
{
    public bool TryExpandWarehouse(out string reason)
    {
        int capacity = WarehouseExpansion.NextCapacity(Warehouse.Capacity);
        if (capacity == 0) { reason = "No further warehouse expansion is needed."; return false; }
        int cost = WarehouseExpansion.Cost(Warehouse.Capacity);
        if (!TrySpendCash(cost)) { reason = "Not enough cash for warehouse expansion. ($" + cost + ")"; return false; }
        Warehouse.SetCapacity(capacity);
        reason = ""; return true;
    }
}
