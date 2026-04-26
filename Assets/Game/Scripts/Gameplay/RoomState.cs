// 旧版/通用房态枚举。
// 当前 2D 原型主要使用 Room2DState；保留这个文件时请和 Room2DState 的核心状态保持一致。
public enum RoomState
{
    // 客人离开后需要清洁。
    Dirty,

    // 正在清洁。
    Cleaning,

    // 清洁完成，等待检查。
    AwaitingInspection,

    // 空房且可入住。
    Ready,

    // 客人正在入住。
    Occupied,

    // 维修或装修等原因导致房间不可用。
    Blocked
}
