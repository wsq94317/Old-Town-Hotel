// 客房部团队模型：**人数不是线性的**。纯函数，不引用 UnityEngine。
//
// 试玩反馈：「HSK 用人应该是最关键的部分，但是我没有体会到」。两个原因：
//   ① 12 间房的尺度上一个管家一天能清 17 间，而每天只有 7 位客人退房——
//      人手在数学上根本不稀缺，雇第二个人当然没感觉。
//   ② 吞吐是各人速率**直接相加**的，两个人恰好等于两倍，没有任何取舍可言。
//
// 用户给的手感基准：10 间房，一个人要 8 小时（耽误 check-in），两个人 3 小时。
// 反推出来的形状不是"两人有协作加成"，而是**独自干才是被罚的那个**：
// 一个人得自己搬布草、跑楼层、开门关门、找备品车；两个人是正常节奏；
// 人再多则走廊、货梯、备品车开始互相挤。
//   1 人 ×0.75 → 1.29 间/时 → 10 间 7.7 小时  ≈ 用户说的 8 小时
//   2 人 ×1.00 → 3.45 间/时 → 10 间 2.9 小时  ≈ 用户说的 3 小时
//   8 人 ×0.85 → 人均掉回来，规模不再免费
// 于是「再雇一个人」在 1→2 时是超线性的惊喜（2.67 倍吞吐），
// 而两份工资是恒定的对冲——这才构成一个真决策。
public static class HousekeepingTeamModel
{
    /// <summary>单人独干的效率折扣：什么都得自己来。</summary>
    public const float SoloFactor = 0.75f;

    /// <summary>正常班组的人均效率（不奖不罚）。</summary>
    public const float TeamFactor = 1.00f;

    /// <summary>规模上限之后的人均效率（走廊/货梯/备品车互相挤的稳态）。</summary>
    public const float CrowdedFactor = 0.85f;

    /// <summary>还算"正常班组"的最大人数，超过就开始往拥堵态滑。</summary>
    public const int ComfortableSize = 3;

    /// <summary>拥堵完全生效的人数。</summary>
    public const int CrowdedAtSize = 8;

    /// <summary>团队规模 → **人均**效率倍率，乘在"各人速率之和"上。</summary>
    public static float PerPersonFactor(int teamSize)
    {
        if (teamSize <= 0) return 0f;
        if (teamSize == 1) return SoloFactor;
        if (teamSize <= ComfortableSize) return TeamFactor;
        if (teamSize >= CrowdedAtSize) return CrowdedFactor;

        // 舒适区到拥堵区之间线性过渡，避免"多雇一人反而断崖式变差"
        float t = (teamSize - ComfortableSize) / (float)(CrowdedAtSize - ComfortableSize);
        return TeamFactor + (CrowdedFactor - TeamFactor) * t;
    }

    /// <summary>清完 rooms 间房要几小时（晨报/人员页展示"今天几点能清完"）。
    /// 吞吐为 0 时返回 -1，调用方自己决定怎么展示"永远清不完"。</summary>
    public static float HoursToClear(int rooms, float roomsPerHour)
    {
        if (rooms <= 0) return 0f;
        if (roomsPerHour <= 0f) return -1f;
        return rooms / roomsPerHour;
    }
}
