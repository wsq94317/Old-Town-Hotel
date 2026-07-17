using System.Collections.Generic;
using UnityEngine;

// 老旧酒店损坏系统（运行时）：
//   生成：每时段 1-2 个，类型随时段——早晨马桶堵 / 中午水管 / 下午泳池健身房 / 晚上电路
//   呈现：现场彩色脉冲标记 + 水渍/火花色块 + 手机通知（黄/橙/红随严重度）
//   恶化：不处理每 3 游戏小时升一级（红色顶格），每次升级掉满意度
//   处理（走近弹面板）：亲自上手(55%成功+小费/失败喷一脸水) / 派员工(笨手笨脚30%越修越坏) /
//                       胶带(免费但明天同址复发更严重) / 锁房(仅客房，房间封锁到次日)
// 视觉走位从简（派员工即时结算），走位版留给动画阶段——已记录假设。
public class BreakdownSystem : MonoBehaviour
{
    private class Incident
    {
        public string Id;
        public Vector3 Pos;
        public Room2DEntity Room;            // 可为 null（设施层）
        public BreakdownSeverity Severity;
        public string Kind;
        public float NextEscalateHour;
        public GameObject Marker;
        public GameObject Puddle;
    }

    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private StaffAgentSpawner spawner;
    [SerializeField] private ManagerController manager;
    [SerializeField] private int rngSeed = 20261;

    private System.Random _rng;
    private readonly List<Incident> _active = new List<Incident>();
    private readonly List<(Vector3 pos, Room2DEntity room, string kind)> _tapedForTomorrow = new List<(Vector3, Room2DEntity, string)>();
    private readonly List<Room2DEntity> _lockedRooms = new List<Room2DEntity>();
    private int _scheduledDay = -1;
    private DayPeriod _lastPeriod = (DayPeriod)(-1);
    private Incident _panelIncident;
    private string _story = "";
    private float _storyUntil;
    private int _idCounter;

    public bool PanelOpen => _panelIncident != null;

    private void Awake()
    {
        _rng = new System.Random(rngSeed);
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (spawner == null) spawner = FindFirstObjectByType<StaffAgentSpawner>();
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
    }

    private void Update()
    {
        if (dayController == null) return;
        float hour = dayController.Clock.CurrentHour;
        int day = dayController.CurrentDay;

        // 新一天：清场 + 胶带复发 + 解锁昨日封的房
        if (day != _scheduledDay)
        {
            _scheduledDay = day;
            foreach (var i in new List<Incident>(_active)) Remove(i);
            foreach (var r in _lockedRooms)
                if (r != null && r.currentState == Room2DState.Blocked) r.SetState(Room2DState.Dirty);
            _lockedRooms.Clear();
            foreach (var t in _tapedForTomorrow)
                Spawn(t.pos, t.room, t.kind, BreakdownSeverity.Moderate); // 复发直接橙色
            _tapedForTomorrow.Clear();
            _lastPeriod = (DayPeriod)(-1);
        }

        // 时段切换：掷 1-2 个新损坏
        var period = DayPeriodLogic.PeriodFor(hour);
        if (period != _lastPeriod)
        {
            _lastPeriod = period;
            int count = 1 + (_rng.NextDouble() < 0.4 ? 1 : 0);
            for (int i = 0; i < count; i++) SpawnForPeriod(period);
        }

        // 恶化 + 经理走近弹面板
        foreach (var inc in _active)
        {
            if (hour >= inc.NextEscalateHour && inc.Severity < BreakdownSeverity.Severe)
            {
                inc.Severity++;
                inc.NextEscalateHour = hour + BreakdownLogic.EscalateGameHours;
                if (demandLoop != null) demandLoop.prototypeSatisfactionScore -= (int)inc.Severity;
                RefreshVisual(inc);
                PushPhone(inc);
                CameraShaker.Shake(0.08f, 0.25f);
            }
        }
        if (_panelIncident == null && manager != null)
        {
            Vector3 p = manager.transform.position;
            foreach (var inc in _active)
            {
                if (FloorMath.FloorIndexForY(p.y) != FloorMath.FloorIndexForY(inc.Pos.y)) continue;
                if (Mathf.Abs(p.x - inc.Pos.x) < 2.2f && Mathf.Abs(p.z - inc.Pos.z) < 2.2f)
                {
                    _panelIncident = inc;
                    break;
                }
            }
        }
        else if (_panelIncident != null && manager != null)
        {
            Vector3 p = manager.transform.position;
            if (Mathf.Abs(p.x - _panelIncident.Pos.x) > 3.2f || Mathf.Abs(p.z - _panelIncident.Pos.z) > 3.2f)
                _panelIncident = null;
        }
    }

    private void SpawnForPeriod(DayPeriod period)
    {
        // 时段决定类型与地点（用户设计表）
        switch (period)
        {
            case DayPeriod.Morning: // 昨晚遗留：马桶堵，客房
                SpawnAtRandomRoom("CLOGGED TOILET", BreakdownSeverity.Minor);
                break;
            case DayPeriod.Midday: // 用水高峰：水管
                SpawnAtRandomRoom("LEAKY PIPE", BreakdownSeverity.Minor);
                break;
            case DayPeriod.Afternoon: // 泳池/健身房设施
                if (FacilitySystem.PoolUnlocked && _rng.NextDouble() < 0.5)
                    Spawn(new Vector3(-2f, FloorMath.BaseYFor(FacilitySystem.PoolFloor), 0.5f), null, "POOL FILTER JAM", BreakdownSeverity.Minor);
                else if (FacilitySystem.GymUnlocked)
                    Spawn(new Vector3(0f, FloorMath.BaseYFor(FacilitySystem.GymFloor), 0f), null, "AC DRIPPING", BreakdownSeverity.Minor);
                else
                    SpawnAtRandomRoom("LEAKY FAUCET", BreakdownSeverity.Minor);
                break;
            default: // 晚上：电路最危险，直接橙色起步
                Spawn(new Vector3(_rng.Next(-8, 8), FloorMath.BaseYFor(_rng.NextDouble() < 0.5 ? 0 : 3), _rng.Next(-4, 4)),
                    null, "SPARKING WIRES", BreakdownSeverity.Moderate);
                break;
        }
    }

    private void SpawnAtRandomRoom(string kind, BreakdownSeverity sev)
    {
        if (demandLoop == null || demandLoop.rooms == null) return;
        var candidates = new List<Room2DEntity>();
        foreach (var r in demandLoop.rooms)
            if (r != null && r.currentState != Room2DState.Blocked) candidates.Add(r);
        if (candidates.Count == 0) return;
        var room = candidates[_rng.Next(candidates.Count)];
        Spawn(room.transform.position, room, kind, sev);
    }

    private void Spawn(Vector3 pos, Room2DEntity room, string kind, BreakdownSeverity sev)
    {
        var inc = new Incident
        {
            Id = "bd_" + (_idCounter++),
            Pos = pos,
            Room = room,
            Severity = sev,
            Kind = kind,
            NextEscalateHour = dayController.Clock.CurrentHour + BreakdownLogic.EscalateGameHours,
        };
        // 现场标记（脉冲色块）+ 地面水渍/火花
        inc.Marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(inc.Marker.GetComponent<Collider>());
        inc.Marker.name = "BdMarker_" + inc.Id;
        inc.Marker.transform.position = pos + Vector3.up * 1.9f;
        inc.Marker.transform.localScale = Vector3.one * 0.5f;
        inc.Marker.AddComponent<BillboardSprite>();
        inc.Marker.AddComponent<EventIconPulse>();

        inc.Puddle = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(inc.Puddle.GetComponent<Collider>());
        inc.Puddle.name = "BdPuddle_" + inc.Id;
        inc.Puddle.transform.position = pos + Vector3.up * 0.09f;
        inc.Puddle.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        inc.Puddle.transform.localScale = Vector3.one * 0.9f;

        RefreshVisual(inc);
        _active.Add(inc);
        PushPhone(inc);
    }

    private void RefreshVisual(Incident inc)
    {
        var color = BreakdownLogic.SeverityColor(inc.Severity);
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(unlit) { color = color };
        if (inc.Marker != null) inc.Marker.GetComponent<Renderer>().material = m;
        if (inc.Puddle != null)
        {
            inc.Puddle.GetComponent<Renderer>().material = m;
            inc.Puddle.transform.localScale = Vector3.one * (0.9f + (int)inc.Severity * 0.5f); // 越严重摊得越大
        }
    }

    private void PushPhone(Incident inc)
    {
        ManagerPhone.Push(inc.Id,
            "🔧 " + BreakdownLogic.SeverityLabel(inc.Severity) + " " + inc.Kind
            + (inc.Room != null ? " @ Room " + inc.Room.roomNumber : ""),
            inc.Pos, BreakdownLogic.SeverityColor(inc.Severity));
    }

    private void Remove(Incident inc)
    {
        if (inc.Marker != null) Destroy(inc.Marker);
        if (inc.Puddle != null) Destroy(inc.Puddle);
        ManagerPhone.Resolve(inc.Id);
        _active.Remove(inc);
        if (_panelIncident == inc) _panelIncident = null;
    }

    private void Choose(BreakdownFix fix)
    {
        var inc = _panelIncident;
        _panelIncident = null;
        if (inc == null) return;

        // 派员工：取一个 HSK 的特质参与结算
        bool clumsy = false, fast = false;
        if (fix == BreakdownFix.SendStaff && spawner != null)
        {
            foreach (var a in spawner.Agents)
            {
                if (a?.Member != null && a.Member.Role == StaffRole.Housekeeper)
                {
                    clumsy = a.Member.HasTrait(StaffTrait.Clumsy);
                    fast = a.Member.HasTrait(StaffTrait.FastHands);
                    break;
                }
            }
        }

        var o = BreakdownLogic.Resolve(fix, _rng.NextDouble(), clumsy, fast);

        if (o.CashDelta > 0 && economy != null) economy.RecordCheckout(o.CashDelta, 1f);
        if (demandLoop != null) demandLoop.prototypeSatisfactionScore += o.SatisfactionDelta;
        if (o.ManagerSlapstick && manager != null)
        {
            CameraShaker.Shake(0.25f, 0.4f);
            FloatingTextFx.Spawn(manager.transform.position, "SPLOOSH!", new Color(0.4f, 0.7f, 1f), 1.2f);
        }
        if (o.CashDelta > 0 && manager != null)
            FloatingTextFx.Spawn(manager.transform.position, "+$" + o.CashDelta + " tip", new Color(0.35f, 0.95f, 0.4f));

        if (o.Fixed)
        {
            if (o.TapedRecurrence) _tapedForTomorrow.Add((inc.Pos, inc.Room, inc.Kind));
            if (o.LockedRoom && inc.Room != null)
            {
                inc.Room.SetState(Room2DState.Blocked);
                _lockedRooms.Add(inc.Room);
            }
            Remove(inc);
        }
        else if (o.SeverityDelta > 0 && inc.Severity < BreakdownSeverity.Severe)
        {
            inc.Severity += o.SeverityDelta;
            RefreshVisual(inc);
            PushPhone(inc);
        }

        _story = o.Story;
        _storyUntil = Time.time + 4.5f;
    }

    private void OnGUI()
    {
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        if (Time.time < _storyUntil)
            GUI.Box(new Rect(w * 0.5f - 230, h * 0.13f, 460, 40), _story);

        if (_panelIncident == null) return;
        var inc = _panelIncident;
        bool isRoom = inc.Room != null;
        float ph = isRoom ? 158 : 132;
        GUI.Box(new Rect(w * 0.5f - 180, h * 0.32f, 360, ph),
            BreakdownLogic.SeverityLabel(inc.Severity) + " — " + inc.Kind
            + (isRoom ? " (Room " + inc.Room.roomNumber + ")" : ""));
        if (GuiInput.Button(new Rect(w * 0.5f - 160, h * 0.32f + 30, 320, 24), "Fix it yourself (55%, tips or a face full of water)"))
            Choose(BreakdownFix.DIY);
        else if (GuiInput.Button(new Rect(w * 0.5f - 160, h * 0.32f + 58, 320, 24), "Send housekeeping (traits matter)"))
            Choose(BreakdownFix.SendStaff);
        else if (GuiInput.Button(new Rect(w * 0.5f - 160, h * 0.32f + 86, 320, 24), "DUCT TAPE (free, definitely permanent)"))
            Choose(BreakdownFix.DuctTape);
        else if (isRoom && GuiInput.Button(new Rect(w * 0.5f - 160, h * 0.32f + 114, 320, 24), "Lock the room (no problem if no witnesses)"))
            Choose(BreakdownFix.LockRoom);
    }
}
