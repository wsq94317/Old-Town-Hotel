using System.Collections.Generic;

// 自动派活决策（纯 C#，确定性，EditMode 可测）：
// Housekeeper → 等待最久的 Dirty；Inspector → 等待最久的 AwaitingInspection；
// 已被认领（claimed）的房跳过；其余角色无任务。
public static class TaskDispatchLogic
{
    public static StaffTask? NextTaskFor(
        StaffRole role,
        IReadOnlyList<Room2DEntity> rooms,
        ISet<Room2DEntity> claimed)
    {
        Room2DState wantedState;
        StaffTaskKind kind;
        switch (role)
        {
            case StaffRole.Housekeeper:
                wantedState = Room2DState.Dirty;
                kind = StaffTaskKind.Clean;
                break;
            case StaffRole.Inspector:
                wantedState = Room2DState.AwaitingInspection;
                kind = StaffTaskKind.Inspect;
                break;
            default:
                return null; // Reception/Manager 不干房务
        }

        Room2DEntity best = null;
        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            if (room == null || room.currentState != wantedState) continue;
            if (claimed != null && claimed.Contains(room)) continue;
            // 等待最久优先；同时长按房号稳定排序（确定性）。
            if (best == null
                || room.stateElapsedSeconds > best.stateElapsedSeconds
                || (UnityEngine.Mathf.Approximately(room.stateElapsedSeconds, best.stateElapsedSeconds)
                    && room.roomNumber < best.roomNumber))
            {
                best = room;
            }
        }

        return best != null ? new StaffTask(best, kind) : (StaffTask?)null;
    }
}
