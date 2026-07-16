// v2 世界层：员工任务描述（TaskDispatcher 产出，StaffAgent 消费）。
public enum StaffTaskKind
{
    Clean,   // Dirty → (StartCleaning) → Cleaning → (FinishCleaning) → AwaitingInspection
    Inspect  // AwaitingInspection → (ApproveInspection) → Ready
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
