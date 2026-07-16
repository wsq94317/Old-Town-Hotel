// 街区威望（长线系统占位计数）：打赢客人等无厘头行为累积。
// v3 的"街区霸王"线会消费它；当前只累计并广播。
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

    public static void ResetForNewGame() => Prestige = 0;
}
