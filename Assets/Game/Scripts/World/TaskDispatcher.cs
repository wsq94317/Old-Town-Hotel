using System.Collections.Generic;
using UnityEngine;

// 自动派活宿主：节拍扫描房态，把任务派给空闲的 HSK/INSP 纸片人。
// 决策在 TaskDispatchLogic（纯逻辑，有测试）；本组件只管节拍、claim 表和回收。
public class TaskDispatcher : MonoBehaviour
{
    [SerializeField] private StaffAgentSpawner spawner;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private float scanIntervalSeconds = 0.5f;

    private readonly HashSet<Room2DEntity> _claimed = new HashSet<Room2DEntity>();
    private readonly HashSet<StaffAgent> _subscribed = new HashSet<StaffAgent>();
    private float _timer;

    private void Awake()
    {
        if (spawner == null) spawner = FindFirstObjectByType<StaffAgentSpawner>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < scanIntervalSeconds) return;
        _timer = 0f;
        Dispatch();
    }

    private void Dispatch()
    {
        if (spawner == null || demandLoop == null || demandLoop.rooms == null) return;

        foreach (var agent in spawner.Agents)
        {
            if (agent == null || !agent.IsIdle || agent.Member == null) continue;
            EnsureSubscribed(agent);

            StaffTask? task = TaskDispatchLogic.NextTaskFor(agent.Member.Role, demandLoop.rooms, _claimed);
            // **脏房优先，清垃圾垫底**：客人在等今天的房，破败房不急一时。
            // 这就是"什么时候清破败房"的真代价——不是钱，是被挪走的工时。
            if (!task.HasValue) task = NextJunkClearingTask(agent.Member.Role);
            if (!task.HasValue) continue;
            if (agent.AssignTask(task.Value))
            {
                _claimed.Add(task.Value.Room);
            }
        }
    }

    /// <summary>没有脏房要打扫时，客房部去清破败房的垃圾。
    ///
    /// 目标房从 **Sim 的清理清单**取（进度高的在前，先把快完的那间干完），
    /// 而不是看 v1 房态——破败房在 v1 里是 Blocked，房态里没有"玩家指派了清理"
    /// 这条信息，那是 Sim 的账。</summary>
    private StaffTask? NextJunkClearingTask(StaffRole role)
    {
        if (role != StaffRole.Housekeeper) return null;
        var bridge = HotelSimSceneBridge.Instance;
        if (bridge == null || bridge.Sim == null) return null;

        var queued = bridge.Sim.RoomsBeingCleared();
        for (int i = 0; i < queued.Count; i++)
        {
            Room2DEntity room = FindRoom(queued[i]);
            if (room == null || _claimed.Contains(room)) continue;
            return new StaffTask(room, StaffTaskKind.ClearJunk);
        }
        return null;
    }

    private Room2DEntity FindRoom(int roomNumber)
    {
        var rooms = demandLoop.rooms;
        if (rooms == null) return null;
        for (int i = 0; i < rooms.Length; i++)
            if (rooms[i] != null && rooms[i].roomNumber == roomNumber) return rooms[i];
        return null;
    }

    private void EnsureSubscribed(StaffAgent agent)
    {
        if (_subscribed.Contains(agent)) return;
        agent.OnTaskFinished += HandleTaskFinished;
        _subscribed.Add(agent);
    }

    private void HandleTaskFinished(StaffAgent agent, Room2DEntity room)
    {
        // 无条件释放：Unity 假 null（房被销毁）时更要把 claim 摘掉，否则永久占坑
        _claimed.Remove(room);
    }

    /// <summary>经理指挥插队：把某间房强塞给指定员工（抢占双方的当前任务/认领）。</summary>
    public bool ForceAssign(StaffAgent agent, Room2DEntity room)
    {
        if (agent == null || room == null || agent.Member == null) return false;

        // 记仇中的员工反正会拒单——先问再拆：别把双方手头的活都撂了才发现指挥失败
        if (agent.IsGrudging) return false;

        StaffTaskKind kind;
        if (room.currentState == Room2DState.Dirty && agent.Member.Role == StaffRole.Housekeeper)
            kind = StaffTaskKind.Clean;
        else if (room.currentState == Room2DState.AwaitingInspection && agent.Member.Role == StaffRole.Inspector)
            kind = StaffTaskKind.Inspect;
        else
            return false; // 房态与角色不匹配

        // 目标房已被别人认领 → 只让占用该房的那位撂下
        if (_claimed.Contains(room) && spawner != null)
        {
            foreach (var other in spawner.Agents)
            {
                if (other != null && other != agent && other.CurrentTaskRoom == room)
                {
                    other.AbortTask();
                    break;
                }
            }
        }
        // 该员工手头有活 → 撂下（AbortTask 触发 OnTaskFinished 释放旧 claim）
        if (!agent.IsIdle) agent.AbortTask();

        EnsureSubscribed(agent);
        if (agent.AssignTask(new StaffTask(room, kind)))
        {
            _claimed.Add(room);
            return true;
        }
        return false;
    }
}
