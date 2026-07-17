// 一天四时段（用户设计）：时段驱动各设施活跃度与事件类型。
//   Morning   6:00-10:00 开张准备（巡查员工/退房潮/早餐）
//   Midday   10:00-14:00 入住高峰（前台/餐厅事件密集）
//   Afternoon 14:00-19:00 休闲高峰（Gym/Casino 热起来）
//   Night    19:00-24:00 派对与混乱（全设施火爆，最无厘头）
public enum DayPeriod { Morning, Midday, Afternoon, Night }

public static class DayPeriodLogic
{
    public static DayPeriod PeriodFor(float hour)
    {
        if (hour < 10f) return DayPeriod.Morning;
        if (hour < 14f) return DayPeriod.Midday;
        if (hour < 19f) return DayPeriod.Afternoon;
        return DayPeriod.Night;
    }

    public static string Label(DayPeriod p)
    {
        switch (p)
        {
            case DayPeriod.Morning: return "MORNING — opening prep";
            case DayPeriod.Midday: return "MIDDAY — check-in rush";
            case DayPeriod.Afternoon: return "AFTERNOON — leisure hours";
            default: return "NIGHT — party & chaos";
        }
    }

    /// <summary>设施在某时段的目标人气（闲逛客数量）。楼层：3=餐厅酒吧 4=健身房 5=赌场 6=泳池。</summary>
    public static int ActivityFor(int floorIndex, DayPeriod p)
    {
        switch (floorIndex)
        {
            case 3: // Restaurant & Bar：早餐/午餐高峰/晚上酒吧模式
                return p == DayPeriod.Morning ? 2 : p == DayPeriod.Midday ? 3 : p == DayPeriod.Afternoon ? 1 : 3;
            case 4: // Gym：晨练 + 下午高峰
                return p == DayPeriod.Morning ? 2 : p == DayPeriod.Afternoon ? 3 : 0;
            case 5: // Casino：下午小赌，晚上火爆
                return p == DayPeriod.Afternoon ? 2 : p == DayPeriod.Night ? 4 : 0;
            case 6: // Pool：下午游泳，晚上派对
                return p == DayPeriod.Afternoon ? 2 : p == DayPeriod.Night ? 4 : 0;
            default:
                return 0;
        }
    }
}
