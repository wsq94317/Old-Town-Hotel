// 房间当前处于什么业务状态。
// 这个枚举会被 Room2DEntity、Room2DController、Room2DOverview 同时使用。
public enum Room2DState
{
    // 客人退房后，房间需要清洁。
    Dirty,

    // 保洁正在清理房间。
    Cleaning,

    // 清洁完成，等待检查。
    AwaitingInspection,

    // 空房、干净，可以安排客人入住。
    Ready,

    // 客人正在房间中。
    Occupied,

    // 房间被维修/装修等原因锁定，暂时不能入住。
    Blocked
}
