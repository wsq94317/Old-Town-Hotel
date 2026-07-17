using UnityEngine;

// 设施系统：
//   餐厅/酒吧（1F 西南角，开局就有）：每晚 20:00 按在住房数结算酒水收入（$8/房）
//   赌场（3F 南翼，解锁 $5000+威望3）：每晚 21:00 赌桌开张，收益随机 -$300~+$600
//   健身房（3F 南翼，解锁 $3000）：日结时满意度 +1（客人爱它）
//   屋顶泳池：v3 预告牌（不可解锁，纯挖坑）
// 解锁：经理走近上锁区域的牌子 → 面板 → 付钱解锁。解锁状态暂不进存档（开发期假设）。
public class FacilitySystem : MonoBehaviour
{
    public const int CasinoCost = 5000, CasinoPrestigeReq = 3, GymCost = 3000;

    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private ManagerController manager;
    [SerializeField] private int rngSeed = 5150;

    public static bool CasinoUnlocked { get; private set; }
    public static bool GymUnlocked { get; private set; }

    private System.Random _rng;
    private int _lastBarDay = -1, _lastCasinoDay = -1, _settledGymDay = -1;
    private string _panelFacility = null; // "casino"/"gym"/"pool" 或 null
    private string _story = "";
    private float _storyUntil;

    private static readonly Vector3 CasinoSign = new Vector3(-6f, 8f, -3.5f);
    private static readonly Vector3 GymSign = new Vector3(0f, 8f, -3.5f);
    private static readonly Vector3 PoolSign = new Vector3(6f, 8f, -3.5f);

    public bool PanelOpen => _panelFacility != null;

    private void Awake()
    {
        _rng = new System.Random(rngSeed);
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
    }

    private void Start()
    {
        if (dayController != null) dayController.OnDaySettled += HandleDaySettled;
    }

    private void OnDestroy()
    {
        if (dayController != null) dayController.OnDaySettled -= HandleDaySettled;
    }

    private int OccupiedCount()
    {
        int n = 0;
        if (demandLoop != null && demandLoop.rooms != null)
            foreach (var r in demandLoop.rooms)
                if (r != null && r.currentState == Room2DState.Occupied) n++;
        return n;
    }

    private void Update()
    {
        if (dayController == null) return;
        float hour = dayController.Clock.CurrentHour;
        int day = dayController.CurrentDay;

        // 酒吧晚市：20:00 一次性结算
        if (hour >= 20f && _lastBarDay != day)
        {
            _lastBarDay = day;
            int income = OccupiedCount() * 8;
            if (income > 0 && economy != null)
            {
                economy.RecordCheckout(income, 1f);
                FloatingTextFx.Spawn(new Vector3(-7f, 0f, -2.5f), "+$" + income + " BAR", new Color(0.95f, 0.7f, 0.3f));
            }
        }

        // 赌场夜场：21:00 随机浮动
        if (CasinoUnlocked && hour >= 21f && _lastCasinoDay != day)
        {
            _lastCasinoDay = day;
            int swing = -300 + (int)(_rng.NextDouble() * 900); // -300..+600
            if (swing >= 0 && economy != null) economy.RecordCheckout(swing, 1f);
            else if (economy != null) economy.TrySpend(-swing);
            FloatingTextFx.Spawn(CasinoSign, (swing >= 0 ? "+$" : "-$") + Mathf.Abs(swing) + " CASINO",
                swing >= 0 ? new Color(0.35f, 0.95f, 0.4f) : new Color(1f, 0.4f, 0.3f), 1.1f);
        }

        // 走近上锁牌子 → 面板
        if (_panelFacility == null && manager != null
            && FloorMath.FloorIndexForY(manager.transform.position.y) == 2)
        {
            Vector3 p = manager.transform.position;
            if (!CasinoUnlocked && Near(p, CasinoSign)) _panelFacility = "casino";
            else if (!GymUnlocked && Near(p, GymSign)) _panelFacility = "gym";
            else if (Near(p, PoolSign)) _panelFacility = "pool";
        }
        else if (_panelFacility != null && manager != null)
        {
            Vector3 anchor = _panelFacility == "casino" ? CasinoSign : _panelFacility == "gym" ? GymSign : PoolSign;
            if (!Near(manager.transform.position, anchor, 3.2f)) _panelFacility = null;
        }
    }

    private static bool Near(Vector3 p, Vector3 anchor, float r = 2.2f)
    {
        p.y = 0; anchor.y = 0;
        return Vector3.Distance(p, anchor) < r;
    }

    private void HandleDaySettled(int day, int served, DayLedger ledger)
    {
        // 健身房：日结满意度 +1
        if (GymUnlocked && demandLoop != null && _settledGymDay != day)
        {
            _settledGymDay = day;
            demandLoop.prototypeSatisfactionScore += 1;
        }
    }

    private void Unlock(string facility)
    {
        if (facility == "casino")
        {
            if (ManagerReputation.Prestige < CasinoPrestigeReq) { Say("The casino crowd doesn't know you yet. (Prestige " + ManagerReputation.Prestige + "/" + CasinoPrestigeReq + ")"); return; }
            if (economy == null || !economy.TrySpend(CasinoCost)) { Say("You can't afford a casino. Ironic."); return; }
            CasinoUnlocked = true;
            FacilityZoneVisual.Unlock("Casino");
            Say("CASINO OPEN. The house always wins. You are the house. Probably.");
        }
        else if (facility == "gym")
        {
            if (economy == null || !economy.TrySpend(GymCost)) { Say("No money, no muscles."); return; }
            GymUnlocked = true;
            FacilityZoneVisual.Unlock("Gym");
            Say("GYM OPEN. Guests can now feel guilty on vacation.");
        }
        _panelFacility = null;
    }

    private void Say(string msg) { _story = msg; _storyUntil = Time.time + 4f; }

    private void OnGUI()
    {
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        if (Time.time < _storyUntil)
            GUI.Box(new Rect(w * 0.5f - 230, h * 0.16f, 460, 40), _story);

        if (_panelFacility == null) return;

        if (_panelFacility == "pool")
        {
            GUI.Box(new Rect(w * 0.5f - 160, h * 0.4f, 320, 70),
                "ROOF POOL\nComing in a future update. The roof isn't ready.\nNeither are you.");
            if (GuiInput.Button(new Rect(w * 0.5f - 60, h * 0.4f + 42, 120, 22), "Fine.")) _panelFacility = null;
            return;
        }

        bool casino = _panelFacility == "casino";
        string title = casino
            ? "CASINO — unlock for $" + CasinoCost + " (needs Prestige " + CasinoPrestigeReq + ")\nNightly take: -$300 ~ +$600. Gambling!"
            : "GYM — unlock for $" + GymCost + "\n+1 satisfaction every day. Sweat sells.";
        GUI.Box(new Rect(w * 0.5f - 170, h * 0.38f, 340, 96), title);
        if (GuiInput.Button(new Rect(w * 0.5f - 150, h * 0.38f + 44, 300, 24), casino ? "Open the casino" : "Open the gym"))
            Unlock(_panelFacility);
        if (GuiInput.Button(new Rect(w * 0.5f - 150, h * 0.38f + 70, 300, 22), "Not today"))
            _panelFacility = null;
    }
}

// 上锁区视觉：暗色地台+牌子；解锁后变亮色（静态注册表按名字找）。
public class FacilityZoneVisual : MonoBehaviour
{
    private static readonly System.Collections.Generic.Dictionary<string, FacilityZoneVisual> _byName =
        new System.Collections.Generic.Dictionary<string, FacilityZoneVisual>();

    [SerializeField] private string facilityName;
    private Renderer _pad;

    public static void Register(string name, FacilityZoneVisual v) => _byName[name] = v;

    public static void Unlock(string name)
    {
        if (_byName.TryGetValue(name, out var v) && v != null && v._pad != null)
            v._pad.material.color = name == "Casino" ? new Color(0.6f, 0.2f, 0.5f) : new Color(0.3f, 0.7f, 0.5f);
    }

    public void Init(string name, Renderer pad)
    {
        facilityName = name;
        _pad = pad;
        Register(name, this);
    }

    private void Start()
    {
        if (_pad == null)
        {
            var t = transform.Find("Pad");
            if (t != null) _pad = t.GetComponent<Renderer>();
        }
        Register(facilityName, this);
    }
}
