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
    private readonly List<int> _priorityRooms=new List<int>();
    public int PriorityRevision { get; private set; }
    public bool IsPriority(int roomNumber)=>_priorityRooms.Contains(roomNumber);
    public void CaptureTo(WorldState world) { if(world!=null)world.servicePriorityRooms=new List<int>(_priorityRooms); }
    public void RestoreFrom(WorldState world)
    {
        _priorityRooms.Clear();
        if(world?.servicePriorityRooms!=null)foreach(int room in world.servicePriorityRooms)if(!_priorityRooms.Contains(room))_priorityRooms.Add(room);
        PriorityRevision++;
    }
    public bool PrioritizeRoom(Room2DEntity room,out string message)
    {
        if(room==null || (room.currentState!=Room2DState.Dirty && room.currentState!=Room2DState.Cleaning && room.currentState!=Room2DState.AwaitingInspection)) {
            message=GameText.UseChinese ? "这间房目前不需要清洁或验房。" : "This room does not need service.";return false;
        }
        StaffRole role=room.currentState==Room2DState.AwaitingInspection ? StaffRole.Inspector : StaffRole.Housekeeper;
        bool hasStaff=false;
        if(spawner!=null)foreach(var agent in spawner.Agents) if(agent!=null&&agent.Member!=null&&agent.Member.Role==role) { hasStaff=true;break; }
        if(!hasStaff) { message=GameText.UseChinese ? (role==StaffRole.Housekeeper ? "请先招聘一名管家。" : "请先招聘一名验房员。") : "Hire the required staff role first.";return false; }
        if(!_priorityRooms.Contains(room.roomNumber)){_priorityRooms.Add(room.roomNumber);PriorityRevision++;}
        Dispatch();
        message=GameText.UseChinese ? room.roomNumber+" 已设为优先服务，员工空闲后优先处理。" : "Room "+room.roomNumber+" has priority for the next available worker.";
        return true;
    }

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

            var sim = HotelSimSceneBridge.Instance != null ? HotelSimSceneBridge.Instance.Sim : null;
            StaffTask? task=NextPriorityTask(agent.Member.Role);
            if(!task.HasValue && sim != null && sim.ClearRuinsFirst) task=NextJunkClearingTask(agent.Member.Role);
            if (!task.HasValue) task = TaskDispatchLogic.NextTaskFor(agent.Member.Role, demandLoop.rooms, _claimed);
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

    private StaffTask? NextPriorityTask(StaffRole role)
    {
        for(int i=0;i<_priorityRooms.Count;i++) {
            var room=FindRoom(_priorityRooms[i]);
            if(room==null || room.currentState==Room2DState.Ready || room.currentState==Room2DState.Occupied || room.currentState==Room2DState.Blocked) {
                _priorityRooms.RemoveAt(i--);PriorityRevision++;continue;
            }
            if(_claimed.Contains(room))continue;
            if(role==StaffRole.Housekeeper && room.currentState==Room2DState.Dirty)return new StaffTask(room,StaffTaskKind.Clean);
            if(role==StaffRole.Inspector && room.currentState==Room2DState.AwaitingInspection)return new StaffTask(room,StaffTaskKind.Inspect);
        }
        return null;
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
