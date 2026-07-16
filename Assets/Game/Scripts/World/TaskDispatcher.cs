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
            if (!task.HasValue) continue;
            if (agent.AssignTask(task.Value))
            {
                _claimed.Add(task.Value.Room);
            }
        }
    }

    private void EnsureSubscribed(StaffAgent agent)
    {
        if (_subscribed.Contains(agent)) return;
        agent.OnTaskFinished += HandleTaskFinished;
        _subscribed.Add(agent);
    }

    private void HandleTaskFinished(StaffAgent agent, Room2DEntity room)
    {
        if (room != null) _claimed.Remove(room);
    }
}
