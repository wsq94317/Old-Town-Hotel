using System.Collections.Generic;
using UnityEngine;

// 住客的一天（表现层，B1b 之后客人的唯一来源是 Sim 台账）。
//
// 以前客人视觉挂在 v1 需求循环上，B1b 把收客权交给 Sim 之后就没人演了——
// 玩家的原话是"怎么好像没看到客人下楼退房的动画"。这个导演补上那一段，
// 而且比原来多演一整天：
//
//   办入住：大门 → 前台（排队） → 房间（放行李，进屋消失）
//   白天：  按 GuestOutingPlan 出去吃饭/办事 → 房间 → 大门（离店一段时间）
//           回来：大门 → 前台（拿钥匙，短暂停留） → 房间
//   退房：  房间 → 前台（结账） → 大门 → 销毁
//
// 关键设计：**这里一行都不改模拟状态**。客人在不在房里只影响看得见的东西，
// 房费、评价、清洁全部照旧走 Sim/v1——表现层出 bug 也不会弄坏存档。
public class GuestLifeDirector : MonoBehaviour
{
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private Vector3 doorPoint = new Vector3(0f, 0f, -5.2f);
    [SerializeField] private Vector3 deskPoint = new Vector3(0.9f, 0f, 2.2f);

    [Tooltip("前台停留几秒（办入住/拿钥匙/结账都用它）")]
    [SerializeField] private float deskDwellSeconds = 1.6f;

    /// <summary>一位住客的表现状态。模拟层只知道"这间房有人"，
    /// 这一层知道他此刻是在房里、在前台，还是压根不在酒店。</summary>
    private enum Presence { WalkingToDesk, AtDesk, WalkingToRoom, InRoom, GoingOut, OutOfHotel, CheckingOut }

    private sealed class Resident
    {
        public int roomNumber;
        public GuestSegment segment;
        public GuestAgent agent;              // 不在场时为 null（省掉一堆看不见的 NavMeshAgent）
        public Presence presence;
        public float dwellUntil;              // 前台停留到几点（真实秒）
        public List<GuestOuting> outings;
        public int outingIndex;               // 下一趟还没演的行程
        public bool luggageDropped;           // 放过行李了（回来就不必再去前台排长队）
        public int planDay;                   // 这份行程是哪一天排的（连住客每天要重排）
    }

    private readonly Dictionary<int, Resident> _residents = new Dictionary<int, Resident>();
    private readonly List<int> _scratchGone = new List<int>();
    private System.Random _rng;

    private void OnEnable()
    {
        // 热重载自愈：纯 C# 对象会被域重载清空（本项目定过的规矩，坑踩过两次）
        if (_rng == null) _rng = new System.Random(20260728);
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
    }

    private void Update()
    {
        var bridge = HotelSimSceneBridge.Instance;
        if (bridge == null || bridge.Sim == null) return;
        // 晨报期间世界冻结，别让客人在报告后面继续走
        if (bridge.AwaitingMorningReport) return;
        if (_rng == null) _rng = new System.Random(20260728);

        SyncResidentsWithLedger(bridge);
        RefreshPlansForNewDay(bridge.Sim.Clock.CurrentDay);
        TickPresence(bridge.Sim.Clock.CurrentMinute);
    }

    /// <summary>台账是唯一的真相：新住客要有人演，退房的住客要送走。</summary>
    private void SyncResidentsWithLedger(HotelSimSceneBridge bridge)
    {
        var stays = bridge.Sim.ActiveStays();

        foreach (var stay in stays)
        {
            if (_residents.ContainsKey(stay.roomNumber)) continue;

            // 新客人：从大门出发去前台
            var resident = new Resident
            {
                roomNumber = stay.roomNumber,
                segment = stay.segment,
                presence = Presence.WalkingToDesk,
                outings = GuestOutingPlan.For(stay.segment, stay.checkInMinute,
                                              SimClock.DayEndMinute, () => _rng.NextDouble()),
                planDay = bridge.Sim.Clock.CurrentDay,
            };
            resident.agent = GuestAgent.Spawn(doorPoint, LabelFor(stay.segment));
            var captured = resident;
            resident.agent.TravelTo(deskPoint, () => ArriveAtDesk(captured));
            _residents[stay.roomNumber] = resident;
        }

        // 台账里没了 = 退房了：从房间走到前台结账，再出大门
        _scratchGone.Clear();
        foreach (var pair in _residents)
        {
            bool stillStaying = false;
            foreach (var stay in stays)
                if (stay.roomNumber == pair.Key) { stillStaying = true; break; }
            if (!stillStaying) _scratchGone.Add(pair.Key);
        }

        foreach (int roomNumber in _scratchGone)
        {
            Resident resident = _residents[roomNumber];
            _residents.Remove(roomNumber);
            SendHome(resident);
        }
    }

    /// <summary>连住客每天要重排行程。漏了这一步的话住三晚的客人只在第一天出门，
    /// 后面两天一直闷在房里——大堂第二天就冷清下来（表现层的静默退化：
    /// 不报错，只让人觉得"游戏越玩越死"）。</summary>
    private void RefreshPlansForNewDay(int currentDay)
    {
        foreach (var pair in _residents)
        {
            Resident resident = pair.Value;
            if (resident.planDay == currentDay) continue;   // 今天刚来的客人行程还新鲜

            // **跨天一律回到房里**：夜晚不模拟，时钟从 22:00 跳回 08:00。
            // 上一天出门在外的客人会拿着昨天的"22:00 回来"死等——新的分钟数
            // 永远小于它，于是整夜加半个白天都待在酒店外（实测抓到 202 房
            // 的客人 planDay=1、等着 1320 分钟回来，而现在才 761）。
            if (resident.agent != null)
            {
                Destroy(resident.agent.gameObject);
                resident.agent = null;
            }
            resident.presence = Presence.InRoom;

            resident.planDay = currentDay;
            resident.outingIndex = 0;
            // 新的一天从早上算起（行李早放好了）
            resident.outings = GuestOutingPlan.For(resident.segment, SimClock.DayStartMinute,
                                                   SimClock.DayEndMinute, () => _rng.NextDouble());
        }
    }

    /// <summary>退房演出：人从房里出来，去前台结账，然后出大门。
    /// 不在酒店的客人（正好出门在外）直接从大门离开——不能从空房里冒人。</summary>
    private void SendHome(Resident resident)
    {
        Vector3 start = resident.agent != null
            ? resident.agent.transform.position
            : (resident.presence == Presence.OutOfHotel ? doorPoint : RoomPositionOf(resident.roomNumber));

        GuestAgent agent = resident.agent != null
            ? resident.agent
            : GuestAgent.Spawn(start, LabelFor(resident.segment));

        // 先到前台结个账，再走——玩家要看得见"钱是从客人身上来的"
        agent.TravelTo(deskPoint, () =>
        {
            if (agent == null) return;
            agent.ExitVia(doorPoint, () => { if (agent != null) Destroy(agent.gameObject); });
        });
    }

    private void ArriveAtDesk(Resident resident)
    {
        resident.presence = Presence.AtDesk;
        resident.dwellUntil = Time.time + deskDwellSeconds;
    }

    private void TickPresence(int currentMinute)
    {
        foreach (var pair in _residents)
        {
            Resident resident = pair.Value;

            switch (resident.presence)
            {
                case Presence.AtDesk:
                    // 办完手续上楼
                    if (Time.time < resident.dwellUntil) break;
                    resident.presence = Presence.WalkingToRoom;
                    if (resident.agent != null)
                    {
                        var agent = resident.agent;
                        var captured = resident;
                        agent.TravelTo(RoomPositionOf(resident.roomNumber), () => EnterRoom(captured));
                    }
                    break;

                case Presence.InRoom:
                    // 该出门了吗
                    if (resident.outingIndex >= resident.outings.Count) break;
                    if (currentMinute < resident.outings[resident.outingIndex].leaveMinute) break;
                    LeaveForOuting(resident);
                    break;

                case Presence.OutOfHotel:
                    // 该回来了吗
                    if (resident.outingIndex >= resident.outings.Count) break;
                    if (currentMinute < resident.outings[resident.outingIndex].returnMinute) break;
                    ComeBack(resident);
                    break;
            }
        }
    }

    /// <summary>进屋：纸片人收起来（房里的人看不见），省掉常驻的 NavMeshAgent。</summary>
    private void EnterRoom(Resident resident)
    {
        resident.presence = Presence.InRoom;
        resident.luggageDropped = true;
        if (resident.agent != null)
        {
            Destroy(resident.agent.gameObject);
            resident.agent = null;
        }
    }

    private void LeaveForOuting(Resident resident)
    {
        resident.presence = Presence.GoingOut;
        var agent = GuestAgent.Spawn(RoomPositionOf(resident.roomNumber), LabelFor(resident.segment));
        resident.agent = agent;
        var captured = resident;
        agent.ExitVia(doorPoint, () =>
        {
            captured.presence = Presence.OutOfHotel;
            if (agent != null) Destroy(agent.gameObject);
            captured.agent = null;
        });
    }

    private void ComeBack(Resident resident)
    {
        resident.outingIndex++;
        var agent = GuestAgent.Spawn(doorPoint, LabelFor(resident.segment));
        resident.agent = agent;
        var captured = resident;

        // 回来先去前台拿钥匙（大堂因此一整天都有人），再上楼
        resident.presence = Presence.WalkingToDesk;
        agent.TravelTo(deskPoint, () => ArriveAtDesk(captured));
    }

    /// <summary>房间的世界坐标。找不到那间房（场景房号不匹配）时退回大门，
    /// 让客人安静地走掉而不是站在原点发呆。</summary>
    private Vector3 RoomPositionOf(int roomNumber)
    {
        if (demandLoop != null && demandLoop.rooms != null)
            foreach (var room in demandLoop.rooms)
                if (room != null && room.roomNumber == roomNumber) return room.transform.position;
        return doorPoint;
    }

    /// <summary>纸片人贴图标签：四类客人长得不一样，玩家能看出今晚住的是谁。</summary>
    private static string LabelFor(GuestSegment segment)
    {
        switch (segment)
        {
            case GuestSegment.Business: return "business";
            case GuestSegment.Party: return "party";
            case GuestSegment.Vip: return "vip";
            default: return "budget";
        }
    }

    /// <summary>此刻不在酒店里的住客数（调试/验收用：白天该有人出门）。</summary>
    public int GuestsOutOfHotel
    {
        get
        {
            int count = 0;
            foreach (var pair in _residents)
                if (pair.Value.presence == Presence.OutOfHotel) count++;
            return count;
        }
    }

    /// <summary>此刻在大堂/走廊上走着的住客数（"热闹感"的量化指标）。</summary>
    public int GuestsOnTheMove
    {
        get
        {
            int count = 0;
            foreach (var pair in _residents)
            {
                var p = pair.Value.presence;
                if (p == Presence.WalkingToDesk || p == Presence.AtDesk
                    || p == Presence.WalkingToRoom || p == Presence.GoingOut) count++;
            }
            return count;
        }
    }

    public int TrackedResidents => _residents.Count;
}
