using System.Collections.Generic;
using UnityEngine;

// M4 事件池运行时：每天排 2-3 个事件 → 到点在锚点浮现紫色事件图标 →
// 经理走近开二选一面板 → 效果结算 + 文案。忽略 2 游戏小时后过期消失。
public class DailyEventInteraction : MonoBehaviour
{
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private StaffAgentSpawner spawner;
    [SerializeField] private ManagerController manager;
    [SerializeField] private int rngSeed = 4242;
    [SerializeField] private Vector3 frontDeskPoint = new Vector3(0.9f, 0f, 2.4f);
    [SerializeField] private Vector3 loungePoint = new Vector3(-6f, 0f, 2.2f);

    private const float ExpireGameHours = 2f;

    private System.Random _rng;
    private List<ScheduledEvent> _todaysSchedule = new List<ScheduledEvent>();
    private int _nextIndex;
    private int _scheduledForDay = -1;

    // 当前浮现的事件
    private HotelEventDef _activeDef;
    private Vector3 _activeAnchor;
    private float _expireAtHour;
    private GameObject _icon;
    private bool _panelOpen;
    private string _story = "";
    private float _storyUntil;

    public bool PanelOpen => _panelOpen;

    // 手机通知中心轮询用
    public bool HasActiveEvent => _activeDef != null;
    public string ActiveEventTitle => _activeDef != null ? _activeDef.Title : "";
    public Vector3 ActiveEventAnchor => _activeAnchor;

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

        // 新一天 → 重新排程
        if (dayController.CurrentDay != _scheduledForDay)
        {
            _scheduledForDay = dayController.CurrentDay;
            _todaysSchedule = EventScheduleLogic.ScheduleForDay(EventCatalog.All, _rng);
            _nextIndex = 0;
            ClearActive();
        }

        float hour = dayController.Clock.CurrentHour;

        // 到点浮现（一次只挂一个；锁着的设施事件直接跳过）
        if (_activeDef == null && _nextIndex < _todaysSchedule.Count
            && hour >= _todaysSchedule[_nextIndex].TriggerHour)
        {
            var def = _todaysSchedule[_nextIndex].Def;
            _nextIndex++;
            if (FacilityAnchorAccessible(def.Anchor)) Activate(def);
        }

        if (_activeDef == null) return;

        // 过期
        if (hour >= _expireAtHour && !_panelOpen)
        {
            _story = "(You ignored: " + _activeDef.Title + ". It resolved itself. Probably.)";
            _storyUntil = Time.time + 3f;
            ClearActive();
            return;
        }

        // 经理走近 → 开面板
        if (!_panelOpen && manager != null)
        {
            Vector3 d = manager.transform.position - _activeAnchor;
            d.y = 0f;
            if (FloorMath.FloorIndexForY(manager.transform.position.y) == FloorMath.FloorIndexForY(_activeAnchor.y)
                && d.magnitude < 2.5f)
            {
                _panelOpen = true;
            }
        }
    }

    private void Activate(HotelEventDef def)
    {
        _activeDef = def;
        _activeAnchor = ResolveAnchor(def.Anchor);
        _expireAtHour = dayController.Clock.CurrentHour + ExpireGameHours;

        // 紫色事件图标（旋转菱形占位）
        _icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(_icon.GetComponent<Collider>());
        _icon.name = "EventIcon_" + def.Id;
        _icon.transform.position = _activeAnchor + Vector3.up * 2.2f;
        _icon.transform.localScale = Vector3.one * 0.55f;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        _icon.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = new Color(0.8f, 0.35f, 0.95f) };
        _icon.AddComponent<BillboardSprite>();
        _icon.AddComponent<EventIconPulse>();
    }

    private static bool FacilityAnchorAccessible(EventAnchor anchor)
    {
        switch (anchor)
        {
            case EventAnchor.Gym: return FacilitySystem.GymUnlocked;
            case EventAnchor.Casino: return FacilitySystem.CasinoUnlocked;
            case EventAnchor.Pool: return FacilitySystem.PoolUnlocked;
            default: return true;
        }
    }

    private Vector3 ResolveAnchor(EventAnchor anchor)
    {
        switch (anchor)
        {
            case EventAnchor.Lounge: return loungePoint;
            case EventAnchor.Restaurant: return new Vector3(-5f, FloorMath.BaseYFor(FacilitySystem.RestaurantFloor), -1f);
            case EventAnchor.Gym: return new Vector3(0f, FloorMath.BaseYFor(FacilitySystem.GymFloor), 0f);
            case EventAnchor.Casino: return new Vector3(0f, FloorMath.BaseYFor(FacilitySystem.CasinoFloor), 0f);
            case EventAnchor.Pool: return new Vector3(0f, FloorMath.BaseYFor(FacilitySystem.PoolFloor), 0f);
            case EventAnchor.RandomOccupiedRoom:
                if (demandLoop != null && demandLoop.rooms != null)
                {
                    var occupied = new List<Room2DEntity>();
                    foreach (var r in demandLoop.rooms)
                        if (r != null && r.currentState == Room2DState.Occupied) occupied.Add(r);
                    if (occupied.Count > 0)
                        return occupied[_rng.Next(occupied.Count)].transform.position;
                }
                return frontDeskPoint; // 没有入住房就退回前台
            default: return frontDeskPoint;
        }
    }

    private void ClearActive()
    {
        _activeDef = null;
        _panelOpen = false;
        if (_icon != null) Destroy(_icon);
    }

    private void Choose(EventOption option)
    {
        if (!_panelOpen || _activeDef == null) return; // 幂等：双通道双触发只结算一次
        // 效果结算
        if (option.Effect.Cash < 0 && economy != null) economy.TrySpend(-option.Effect.Cash);
        else if (option.Effect.Cash > 0 && economy != null) economy.RecordCheckout(option.Effect.Cash, 1f); // 计入当日收入
        if (demandLoop != null) demandLoop.prototypeSatisfactionScore += option.Effect.Satisfaction;
        if (option.Effect.StaffMorale != 0 && spawner != null)
            foreach (var a in spawner.Agents) a?.Member?.AdjustMorale(option.Effect.StaffMorale);
        ManagerReputation.Add(option.Effect.Prestige);
        ApplySpecial(option.Special);

        if (option.Effect.Cash != 0 && manager != null)
            FloatingTextFx.Spawn(manager.transform.position, option.Effect.Cash.ToString("+#;-#") + "$",
                option.Effect.Cash < 0 ? new Color(1f, 0.4f, 0.3f) : new Color(0.35f, 0.95f, 0.4f));

        _story = option.Story;
        _storyUntil = Time.time + 4.5f;
        ClearActive();
    }

    private void ApplySpecial(EventSpecial special)
    {
        if (special == EventSpecial.None || economy == null || economy.Payroll == null) return;
        // 随机挑一个非 Manager 员工
        var candidates = new List<StaffMember>();
        foreach (var m in economy.Payroll.Roster)
            if (m != null && m.Role != StaffRole.Manager) candidates.Add(m);
        if (candidates.Count == 0) return;
        var target = candidates[_rng.Next(candidates.Count)];
        if (special == EventSpecial.RaiseRandomStaff) economy.GiveRaise(target, target.DailyWage + 5);
        else economy.RefuseRaise(target, 10);
    }

    private void OnGUI()
    {
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        if (Time.time < _storyUntil)
            GUI.Box(new Rect(w * 0.5f - 230, h * 0.19f, 460, 44), _story);

        // 事件提醒（未走近时右上角提示）
        if (_activeDef != null && !_panelOpen)
            GUI.Label(new Rect(w - 330, 10, 320, 22), "! " + _activeDef.Title + " — go take a look");

        if (!_panelOpen || _activeDef == null) return;
        GUI.Box(new Rect(w * 0.5f - 190, h * 0.3f, 380, 128), _activeDef.Title + "\n" + _activeDef.Blurb);
        for (int i = 0; i < _activeDef.Options.Length; i++)
        {
            if (GuiInput.Button(new Rect(w * 0.5f - 170, h * 0.3f + 62 + i * 30, 340, 26), _activeDef.Options[i].Label))
            {
                Choose(_activeDef.Options[i]);
                break;
            }
        }
    }
}

// 事件图标呼吸动画（占位）。
public class EventIconPulse : MonoBehaviour
{
    private Vector3 _base;
    private void Awake() => _base = transform.localScale;
    private void Update()
    {
        float k = 1f + Mathf.Sin(Time.time * 4f) * 0.15f;
        transform.localScale = _base * k;
        transform.position += Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.002f);
    }
}
