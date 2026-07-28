// B1b 权威切分：世界场景里 Sim 与 v1 各管一半房态，这里是唯一的裁判。
//
//   Sim 管**客人**——谁来、住哪间、住几晚、收多少钱（预订流 + 分房 + 房费）
//   v1  管**清洁演出**——脏房怎么一步步变回可售（管家走过去、打扫、验房）
//
// 两边都在写同一批房间，所以每个房间每帧都要判一次"这一格听谁的"。
// 判错的代价在 M-F 试玩里见过两次：读多了会把住着人的房当空房再卖一次
// （房费蒸发），写多了会把管家刚打扫好的进度抹掉（房永远清不完）。
public enum RoomSyncDirection
{
    /// <summary>把 Sim 的状态写进 v1（Sim 说了算）。</summary>
    PushToLegacy,

    /// <summary>把 v1 的状态读进 Sim（清洁链归 v1）。</summary>
    PullFromLegacy,
}

public static class RoomAuthorityPolicy
{
    /// <summary>这一间房该往哪个方向同步。
    ///
    /// <paramref name="hasStay"/> 是 Sim 台账里有没有住客——**它比两边的 state 都硬**，
    /// 因为台账是收房费的依据。state 只是台账的影子。</summary>
    public static RoomSyncDirection Decide(RoomSimState sim, Room2DState legacy, bool hasStay,
                                           out Room2DState pushTarget)
    {
        // ① 有住客：v1 必须显示 Occupied。管家不会去打扫住着人的房，
        //    投诉系统也靠 v1 的 Occupied 才知道哪间房里有人可以生气。
        if (hasStay)
        {
            pushTarget = Room2DState.Occupied;
            return RoomSyncDirection.PushToLegacy;
        }

        // ② 家具坏了或房间破败：封房是 Sim 的判断，v1 得跟着封，
        //    否则管家会排队去打扫一间根本卖不出去的房（白耗产能）。
        if (sim == RoomSimState.Blocked || sim == RoomSimState.Ruined)
        {
            pushTarget = Room2DState.Blocked;
            return RoomSyncDirection.PushToLegacy;
        }

        // ③ v1 还封着但 Sim 已经放行（家具修好了/破败房复原完）：必须推，
        //    不能读——读回来又变 Blocked，房间就永远解不开封（死锁）。
        if (legacy == Room2DState.Blocked)
        {
            pushTarget = RoomStateMapping.ToLegacy(sim);
            return RoomSyncDirection.PushToLegacy;
        }

        // ④ v1 觉得有人住着，可 Sim 台账里没这个人 = 幽灵客人
        //    （v1 自己发明的客人，或者打烊结账后 v1 还没收到退房通知）。
        //    客人的事 Sim 说了算，推。
        if (legacy == Room2DState.Occupied)
        {
            pushTarget = RoomStateMapping.ToLegacy(sim);
            return RoomSyncDirection.PushToLegacy;
        }

        // ⑤ 打烊结账刚把房弄脏，v1 还显示可售：推一次脏，清洁链才会启动。
        if (sim == RoomSimState.Dirty && legacy == Room2DState.Ready)
        {
            pushTarget = Room2DState.Dirty;
            return RoomSyncDirection.PushToLegacy;
        }

        // ⑥ 其余全是清洁链上的格子（Dirty→Cleaning→AwaitingInspection→Ready）：
        //    那是 v1 管家的实体劳动，Sim 只有资格旁观。
        pushTarget = legacy;
        return RoomSyncDirection.PullFromLegacy;
    }
}
