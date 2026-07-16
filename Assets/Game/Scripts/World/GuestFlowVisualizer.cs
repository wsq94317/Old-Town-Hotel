using System.Collections.Generic;
using UnityEngine;

// 客人关键节点可视化（M2 简化版）：
//   ① 前台有活跃需求 → 大门生成客人走到前台排队点
//   ② 需求被办理（served 计数变化）→ 排队客人走向刚分配的房间 → 到房销毁
//   ③ 退房事件（OnDepartureCheckedOut）→ 房门口生成客人 → 走到大门销毁
// 纯表现层：轮询/订阅 DemandLoop，不改变任何模拟状态。
public class GuestFlowVisualizer : MonoBehaviour
{
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private Vector3 doorPoint = new Vector3(0f, 0f, -5.2f);
    [SerializeField] private Vector3 deskQueuePoint = new Vector3(0.9f, 0f, 2.2f);

    private GuestAgent _waitingGuest;      // 当前在前台排队的可视化客人
    private bool _waitingSpawned;
    private int _lastServedCount;
    private readonly Dictionary<int, Room2DEntity> _roomByNumber = new Dictionary<int, Room2DEntity>();

    private void Start()
    {
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (demandLoop != null)
        {
            demandLoop.OnDepartureCheckedOut += HandleDeparture;
            _lastServedCount = demandLoop.successfulDemandCount;
        }
    }

    private void OnDestroy()
    {
        if (demandLoop != null) demandLoop.OnDepartureCheckedOut -= HandleDeparture;
    }

    private void Update()
    {
        if (demandLoop == null) return;

        // ① 活跃需求出现 → 生成排队客人
        if (demandLoop.activeDemandWaitingForManualAssignment && !_waitingSpawned)
        {
            _waitingSpawned = true;
            _waitingGuest = GuestAgent.Spawn(doorPoint, "arriving");
            _waitingGuest.TravelTo(deskQueuePoint, null);
        }

        // ② 办理完成（served +1）→ 排队客人去房间
        if (demandLoop.successfulDemandCount != _lastServedCount)
        {
            _lastServedCount = demandLoop.successfulDemandCount;
            var room = FindRoom(demandLoop.lastChangedRoomName);
            if (_waitingGuest != null && room != null)
            {
                var guest = _waitingGuest;
                Vector3 target = room.transform.position;
                guest.TravelTo(target, () => Destroy(guest.gameObject)); // 到房"进屋"
            }
            else if (_waitingGuest != null)
            {
                Destroy(_waitingGuest.gameObject);
            }
            _waitingGuest = null;
            _waitingSpawned = false;
        }

        // 活跃需求消失但没办理成功（打烊清场/客人放弃）→ 排队客人离开
        if (!demandLoop.activeDemandWaitingForManualAssignment && _waitingSpawned
            && demandLoop.successfulDemandCount == _lastServedCount && _waitingGuest != null)
        {
            var guest = _waitingGuest;
            guest.TravelTo(doorPoint, () => Destroy(guest.gameObject));
            _waitingGuest = null;
            _waitingSpawned = false;
        }
    }

    // ③ 退房：房门口生成客人走向大门
    private void HandleDeparture(Room2DEntity room, int amount, bool byPlayer)
    {
        if (room == null) return;
        var guest = GuestAgent.Spawn(room.transform.position, "departing_" + room.roomNumber);
        guest.TravelTo(doorPoint, () => Destroy(guest.gameObject));
    }

    private Room2DEntity FindRoom(string roomName)
    {
        if (string.IsNullOrEmpty(roomName) || demandLoop.rooms == null) return null;
        foreach (var r in demandLoop.rooms)
        {
            if (r != null && r.roomName == roomName) return r;
        }
        return null;
    }
}
