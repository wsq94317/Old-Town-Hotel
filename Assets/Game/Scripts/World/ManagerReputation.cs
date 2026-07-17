public static class ManagerReputation
{
    public static int Prestige { get; private set; }
    public static event System.Action<int> OnChanged;

    public static void Add(int delta)
    {
        if (delta == 0) return;
        Prestige = System.Math.Max(0, Prestige + delta);
        OnChanged?.Invoke(Prestige);
    }

    public static void ResetForNewGame()
    {
        Prestige = 0;
        OnChanged?.Invoke(Prestige);
    }

    public static void Restore(int value)
    {
        Prestige = System.Math.Max(0, value);
        OnChanged?.Invoke(Prestige);
    }
}
