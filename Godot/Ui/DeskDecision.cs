using System.Collections.Generic;
using Godot;

// 前台待决事项的统一视图：把「超售」和「退款」两种不同来源的待办，
// 抹平成一种卡片能渲染的形状。
//
// 为什么需要这层：Sim 里这两样是**唯一**既公开、又逐项、又有身份、又有提交动作的
// 前台数据。到店排队拿不到（`_deskQueue` 私有，而且 walk-in 的身份在
// AdmitOneGuest 跑之前根本还没被掷出来——是"不存在"而不是"取不到"）。
//
// ⚠️ 两个必须遵守的约束，都是从 Sim 源码里读出来的：
//
// 1. **必须拷贝再遍历。** HotelSim.PendingRefunds / PendingOverbookings 是
//    `=> _refunds` / `=> _overbookings`，是活视图不是快照；而 TryResolveOverbooking
//    会 `_overbookings.Remove(...)`。边遍历边处理会当场抛集合修改异常。
//    Collect() 每次返回新 List 就是为这个。
//
// 2. **不能同时渲染 Bookings.ArrivalsFor(today)。** AdmitOneGuest 在登记超售事件后
//    直接 return，**没有**改 reservation.state，所以同一个客人会同时出现在
//    PendingOverbookings 和当日到店预订里——两边都画就是同一个人显示两次。
public enum DeskDecisionKind { Overbooking, Refund }

public sealed class DeskDecision
{
    public DeskDecisionKind Kind;
    public int Id;                  // incidentId 或 requestId
    public GuestSegment Segment;
    public string Headline;         // 卡片主标题
    public string Detail;           // 一行事实（金额 / 房号 / 等待时长）
    public string Quote;            // 客人原话，退款才有
    public int Money;               // 这个决定牵涉的钱，用于排序
    public bool CanUpgrade;         // 仅超售：有没有房可以安排

    /// <summary>
    /// 取当前全部待决事项的**快照**，按「金额大的排前面」排序。
    ///
    /// 排序是刻意的：Sim 那两个列表本身没有稳定顺序保证，不排的话卡片会在
    /// 每次 0.25 秒刷新时乱跳。用金额排也顺带把最贵的决定顶到拇指热区。
    /// </summary>
    public static List<DeskDecision> Collect(HotelSim sim)
    {
        var list = new List<DeskDecision>();

        var overbookings = sim.PendingOverbookings;
        for (int i = 0; i < overbookings.Count; i++)
        {
            var o = overbookings[i];
            list.Add(new DeskDecision
            {
                Kind = DeskDecisionKind.Overbooking,
                Id = o.incidentId,
                Segment = o.segment,
                Headline = $"OVERBOOKED · {GuestSegmentUi.Label(o.segment)}",
                Detail = $"Paid ${o.lockedPrice:N0} · waiting {o.waitMinutes} min · payout ${o.CompensationCost:N0}",
                Quote = null,
                Money = o.CompensationCost,
                // CanUpgradeOverbooking 内部调用的是和 TryResolveOverbooking 同一个
                // 私有 TryPickRoomFor，所以按钮的可用态和实际结果不会漂移。
                CanUpgrade = sim.CanUpgradeOverbooking(o.incidentId),
            });
        }

        var refunds = sim.PendingRefunds;
        for (int i = 0; i < refunds.Count; i++)
        {
            var r = refunds[i];
            list.Add(new DeskDecision
            {
                Kind = DeskDecisionKind.Refund,
                Id = r.requestId,
                Segment = r.segment,
                Headline = $"REFUND · ROOM {r.roomNumber}",
                Detail = $"Wants ${r.amount:N0} back · delivery {r.gap:+0.00;-0.00;0.00} vs listed",
                Quote = r.line,
                Money = r.amount,
                CanUpgrade = false,
            });
        }

        list.Sort((a, b) => b.Money.CompareTo(a.Money));
        return list;
    }
}
