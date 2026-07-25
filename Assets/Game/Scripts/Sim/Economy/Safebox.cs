// 保险箱 + 溢出账本（架构 B.3，修订版 3 定稿）。纯 C#，不引用 UnityEngine。
//
// 玩法定位：**正向召回，不是负向惩罚**。
//   保险箱装当日净利，玩家回来收走才变成可支配现金（装修/还款/招聘只能花现金）；
//   装满后的收入进溢出账本，只能捞回 65% 并有失窃风险——这是"忙一两天有损失但不白干"。
//   欠薪与保险箱无关：日结时当日收入先付固定成本（见 DaySettlement）。

/// <summary>固定等级容量表（修订版 3：拒绝按近七日收入动态缩放）。
/// 动态容量会让"升级保险箱"的可感知价值被自动增长吞掉，玩家也无法预测何时会满。
/// 数值按各进度期"约 2-3 天净利"离线调表。</summary>
public static class SafeboxLevels
{
    private static readonly int[] Capacities = { 600, 1200, 2400, 4800, 9600 };

    public static int MaxLevel => Capacities.Length;

    public static int CapacityFor(int level) =>
        Capacities[SimMath.Clamp(level, 1, Capacities.Length) - 1];
}

public sealed class Safebox
{
    public Safebox(int level = 1, int balance = 0)
    {
        Level = SimMath.Clamp(level, 1, SafeboxLevels.MaxLevel);
        Balance = balance < 0 ? 0 : balance;
    }

    public int Level { get; private set; }
    public int Balance { get; private set; }

    public int Capacity => SafeboxLevels.CapacityFor(Level);
    public int RoomLeft => Capacity - Balance;
    public bool IsFull => Balance >= Capacity;
    public float FillRatio => Capacity <= 0 ? 1f : SimMath.Clamp01(Balance / (float)Capacity);

    /// <summary>存钱。返回装不下的溢出额（交给 OverflowLedger）。</summary>
    public int Deposit(int amount)
    {
        if (amount <= 0) return 0;
        int room = RoomLeft;
        if (room <= 0) return amount;
        int stored = amount < room ? amount : room;
        Balance += stored;
        return amount - stored;
    }

    /// <summary>玩家收取：全额进现金，箱子清空。</summary>
    public int Collect()
    {
        int all = Balance;
        Balance = 0;
        return all;
    }

    /// <summary>升级：扩容，不动余额。</summary>
    public bool Upgrade()
    {
        if (Level >= SafeboxLevels.MaxLevel) return false;
        Level++;
        return true;
    }

    public void RestoreFromSave(int level, int balance)
    {
        Level = SimMath.Clamp(level, 1, SafeboxLevels.MaxLevel);
        Balance = balance < 0 ? 0 : balance;
    }
}

/// <summary>溢出临时账本：保险箱装满后的收入去处。只能捞回一部分，还可能被偷。</summary>
public sealed class OverflowLedger
{
    /// <summary>找回比例（修订版 2/3：60%-70% 区间取 65%）。</summary>
    public const float RecoveryRate = 0.65f;

    /// <summary>日结时的失窃概率与失窃比例。</summary>
    public const double TheftChance = 0.05d;
    public const float TheftPortion = 0.20f;

    public int Balance { get; private set; }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Balance += amount;
    }

    /// <summary>玩家找回：只拿回 RecoveryRate，其余凭空消失（"现金放在纸箱里"的代价）。</summary>
    public int Recover()
    {
        if (Balance <= 0) return 0;
        int recovered = SimMath.FloorToInt(Balance * RecoveryRate);
        Balance = 0;
        return recovered;
    }

    /// <summary>日结掷失窃（roll 外部注入）。返回被偷金额。</summary>
    public int RollTheft(double roll)
    {
        if (Balance <= 0) return 0;
        if (roll >= TheftChance) return 0;
        int stolen = SimMath.FloorToInt(Balance * TheftPortion);
        if (stolen <= 0) stolen = Balance < 1 ? 0 : 1;
        Balance -= stolen;
        return stolen;
    }

    public void RestoreFromSave(int balance) => Balance = balance < 0 ? 0 : balance;
}
