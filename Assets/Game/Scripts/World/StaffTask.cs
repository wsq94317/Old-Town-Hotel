// v2 世界层：员工任务描述（TaskDispatcher 产出，StaffAgent 消费）。
public enum StaffTaskKind
{
    Clean,      // Dirty → (StartCleaning) → Cleaning → (FinishCleaning) → AwaitingInspection
    Inspect,    // AwaitingInspection → (ApproveInspection) → Ready
    ClearJunk   // 破败房清垃圾：**一趟清不完**，进度记在 Sim 的 JunkClearingQueue 里，
                // 干满了房间才转成脏房。玩家看到的是工作人员一次次进屋（用户设计）
}

public readonly struct StaffTask
{
    public readonly Room2DEntity Room;
    public readonly StaffTaskKind Kind;

    public StaffTask(Room2DEntity room, StaffTaskKind kind)
    {
        Room = room;
        Kind = kind;
    }
}
