// 班次加成（用户设计：周末班和夜班要更高工资）。纯 C#，不引用 UnityEngine。
//
// **为什么用加法而不是乘法**：周末夜班 1.3 × 1.5 = 1.95 倍，玩家看到账单会觉得
// 被暗算了；1 + 0.3 + 0.5 = 1.8 倍则一眼能算出来。手机游戏里"能不能心算"
// 直接决定玩家敢不敢做这个决定。
//
// 数值的理由：
//   周末 +30%  周末是需求与房价的高峰，这笔溢价就是"抓住旺季"的门票钱
//   夜班 +50%  没人愿意上夜班；这是唯一能拿到"夜间到店客"的办法（见 NightDesk）
public enum ShiftSlot
{
    Day,    // 白班 08:00-22:00，就是玩家看得见的那一整天
    Night   // 夜班 22:00-08:00，幕后时段：只做前台，代价是加成
}

public static class ShiftPremium
{
    public const float WeekendPremium = 0.30f;
    public const float NightPremium = 0.50f;

    /// <summary>这个班次的工资倍率（加法叠加，周末夜班 = 1.8）。</summary>
    public static float MultiplierFor(ShiftSlot slot, bool weekend)
    {
        float premium = 0f;
        if (weekend) premium += WeekendPremium;
        if (slot == ShiftSlot.Night) premium += NightPremium;
        return 1f + premium;
    }

    /// <summary>一个人今天该拿多少（四舍五入到整元——工资单上不出现小数）。</summary>
    public static int WageFor(int baseDailyWage, ShiftSlot slot, bool weekend)
    {
        if (baseDailyWage <= 0) return 0;
        return SimMath.RoundToInt(baseDailyWage * MultiplierFor(slot, weekend));
    }

    public static string LabelOf(ShiftSlot slot, bool weekend)
    {
        if (slot == ShiftSlot.Night)
            return weekend ? "weekend night (+80%)" : "night (+50%)";
        return weekend ? "weekend day (+30%)" : "weekday day";
    }
}

/// <summary>夜间前台：22:00-08:00 的幕后时段。
///
/// 夜班不是装饰，它接住**打烊时还站在前台排队的客人**——以前这些人在
/// BeginDay 被静默清掉，玩家白丢一笔收入而且完全看不到。现在：
///   有人值夜 → 通宵办入住（一夜能办几个有上限，毕竟只有一个人）
///   没人值夜 → 客人对着锁着的门站到天亮，比"没来"更伤口碑（差评照记）
/// 于是"要不要排夜班"变成一道算得清的账：加成付出去，换回那几间夜的房费。</summary>
public static class NightDeskModel
{
    /// <summary>一名夜班前台一整夜能办多少入住。
    /// 白班一小时能办 6 个，夜里一个人守着、还要处理杂事，一夜 5 个是合理量级；
    /// 更重要的是它必须**小于**一天的到店量，否则夜班就成了万能补丁，
    /// 白班人手不足再也不疼了。</summary>
    public const int CheckInsPerNightPerReceptionist = 5;

    public static int CapacityFor(int nightReceptionists) =>
        nightReceptionists <= 0 ? 0 : nightReceptionists * CheckInsPerNightPerReceptionist;

    /// <summary>对着锁门站了一夜的客人扣多少满意度。
    /// 比"分到脏房"（0.22）更狠——人家是真的到了门口没人理。</summary>
    public const float LockedOutPenalty = 0.45f;
}
