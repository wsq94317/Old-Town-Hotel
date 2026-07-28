using System.Collections.Generic;

// 破败房的清理进度（用户设计：白盒阶段先要逻辑，3D 阶段再配蜘蛛网/烟尘动画）。
//
// 玩家的构想：破败房一开始满是蜘蛛网和垃圾，指派清理之后**工作人员要一次次
// 进屋**，每次清掉一部分，房间上方有一根进度条。这和现有的"复原方案"
// （ReclaimPlan：花钱买家具、等 N 天完工）是两个不同的东西：
//
//   清理垃圾 = 只花**人工**。不要钱，但要占客房部的工时，和"今天的退房脏房"
//              抢同一批人手——这才是它的真代价，也是玩家真正要做的取舍。
//   复原家具 = 花**钱**（ReclaimPlan 三档）。清空之后想直接跳到好档位就买。
//
// 清理干净的房间沿用原有的破家具进入正常循环（Old 档），所以"只用人工"这条
// 路永远走得通——现金归零也不会把玩家锁死（同"胶带糊家具"一样的自救逻辑）。
//
// 纯 C#，不引用 UnityEngine。进度存在这里（可测、以后可存档），
// **谁去干、走多久、播什么动画由世界场景的 StaffAgent 负责**——
// 一个关注点只有一个权威（B1b 定的规矩）。
public static class JunkClearingModel
{
    /// <summary>清空一间破败房需要多少工作量。</summary>
    public const int WorkUnitsPerRoom = 100;

    /// <summary>一次标准进屋能清掉多少（约四趟清完一间——够玩家看见进度在动，
    /// 又不至于一趟搞定而失去"一次次进屋"的感觉）。</summary>
    public const int WorkUnitsPerVisit = 25;

    /// <summary>标准工作量下清完一间要几趟（UI 用它说"还差几趟"）。</summary>
    public static int VisitsForOneRoom =>
        (WorkUnitsPerRoom + WorkUnitsPerVisit - 1) / WorkUnitsPerVisit;

    /// <summary>还差几趟（向上取整；已清完返回 0）。</summary>
    public static int VisitsRemaining(int workDone)
    {
        int left = WorkUnitsPerRoom - workDone;
        if (left <= 0) return 0;
        return (left + WorkUnitsPerVisit - 1) / WorkUnitsPerVisit;
    }

    public static float ProgressOf(int workDone) =>
        SimMath.Clamp01((float)workDone / WorkUnitsPerRoom);
}

/// <summary>正在清理的破败房清单（每间房一条进度）。</summary>
public sealed class JunkClearingQueue
{
    private readonly Dictionary<int, int> _workByRoom = new Dictionary<int, int>();

    public int Count => _workByRoom.Count;

    public bool IsClearing(int roomNumber) => _workByRoom.ContainsKey(roomNumber);

    /// <summary>开工清理。已经在清的房返回 false（幂等，防重复下单）。</summary>
    public bool Begin(int roomNumber)
    {
        if (_workByRoom.ContainsKey(roomNumber)) return false;
        _workByRoom[roomNumber] = 0;
        return true;
    }

    /// <summary>一次进屋的成果。清满了返回 true（调用方负责把房间转成脏房）。
    /// 没在清理的房返回 false——不能凭空给一间没下单的房记工。</summary>
    public bool ApplyVisit(int roomNumber, int workUnits)
    {
        if (!_workByRoom.TryGetValue(roomNumber, out int done)) return false;
        if (workUnits < 0) workUnits = 0;

        done += workUnits;
        if (done >= JunkClearingModel.WorkUnitsPerRoom)
        {
            _workByRoom.Remove(roomNumber);
            return true;
        }

        _workByRoom[roomNumber] = done;
        return false;
    }

    public int WorkDoneOn(int roomNumber) =>
        _workByRoom.TryGetValue(roomNumber, out int done) ? done : 0;

    public float ProgressOf(int roomNumber) =>
        JunkClearingModel.ProgressOf(WorkDoneOn(roomNumber));

    /// <summary>放弃清理（玩家改主意/房间被别的流程接手）。进度一并丢掉。</summary>
    public bool Cancel(int roomNumber) => _workByRoom.Remove(roomNumber);

    /// <summary>正在清理的房号，**按进度从高到低**——工作人员先把快完的那间干完，
    /// 而不是雨露均沾。同时清十间半成品在玩家眼里等于"什么都没完成"。</summary>
    public List<int> RoomsByProgress()
    {
        var rooms = new List<int>(_workByRoom.Keys);
        rooms.Sort((a, b) =>
        {
            int byWork = _workByRoom[b].CompareTo(_workByRoom[a]);
            return byWork != 0 ? byWork : a.CompareTo(b);   // 同进度按房号，保证确定性
        });
        return rooms;
    }

    public void Clear() => _workByRoom.Clear();

    /// <summary>存档用：导出每间房的进度（世界场景接存档要到 M-F，先留好接口）。</summary>
    public List<KeyValuePair<int, int>> Export() => new List<KeyValuePair<int, int>>(_workByRoom);

    public void Restore(IEnumerable<KeyValuePair<int, int>> entries)
    {
        _workByRoom.Clear();
        if (entries == null) return;
        foreach (var entry in entries)
            if (entry.Value < JunkClearingModel.WorkUnitsPerRoom)
                _workByRoom[entry.Key] = entry.Value < 0 ? 0 : entry.Value;
    }
}
