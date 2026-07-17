using System.Collections.Generic;
using UnityEngine;

// 设施系统（楼层版）：4F 餐厅酒吧（免费）/ 5F 健身房 $3000 / 6F 赌场 $5000+威望3 /
// 7F 屋顶泳池 $8000+威望5。解锁在电梯面板里完成（ElevatorController 调 TryUnlock）。
// 时段人气：按 DayPeriodLogic.ActivityFor 维持各设施层的闲逛客数量（电梯口进出）。
// 营收/效果：
//   酒吧 20:00 每入住房 +$8；赌场 21:00 夜场 -$300~+$600；
//   泳池 22:00 派对：60% 满意度+3（全场狂欢），40% 满意度-2（失控）；
//   健身房：日结满意度 +1。
public class FacilitySystem : MonoBehaviour
{
    public const int GymCost = 3000, CasinoCost = 5000, CasinoPrestigeReq = 3;
    public const int PoolCost = 8000, PoolPrestigeReq = 5;

    public const int RestaurantFloor = 3, GymFloor = 4, CasinoFloor = 5, PoolFloor = 6;

    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private int rngSeed = 5150;

    // ★ 调试开关：接动画/场景期间全解锁；出正式版改回 false（用户 2026-07-17 要求）
    public const bool DebugUnlockAll = true;

    public static bool GymUnlocked { get; private set; } = DebugUnlockAll;
    public static bool CasinoUnlocked { get; private set; } = DebugUnlockAll;
    public static bool PoolUnlocked { get; private set; } = DebugUnlockAll;

    public static bool FloorAccessible(int floor)
    {
        switch (floor)
        {
            case GymFloor: return GymUnlocked;
            case CasinoFloor: return CasinoUnlocked;
            case PoolFloor: return PoolUnlocked;
            default: return true;
        }
    }

    /// <summary>电梯面板解锁入口。返回是否解锁成功，msg 给面板显示。</summary>
    public static bool TryUnlock(int floor, EconomySystem economy, out string msg)
    {
        switch (floor)
        {
            case GymFloor:
                if (economy == null || !economy.TrySpend(GymCost)) { msg = "No money, no muscles. ($" + GymCost + ")"; return false; }
                GymUnlocked = true;
                msg = "GYM OPEN. Guests can now feel guilty on vacation.";
                return true;
            case CasinoFloor:
                if (ManagerReputation.Prestige < CasinoPrestigeReq) { msg = "Casino crowd doesn't know you yet. (Prestige " + ManagerReputation.Prestige + "/" + CasinoPrestigeReq + ")"; return false; }
                if (economy == null || !economy.TrySpend(CasinoCost)) { msg = "You can't afford a casino. Ironic. ($" + CasinoCost + ")"; return false; }
                CasinoUnlocked = true;
                msg = "CASINO OPEN. You are the house now. Probably.";
                return true;
            case PoolFloor:
                if (ManagerReputation.Prestige < PoolPrestigeReq) { msg = "The roof demands respect. (Prestige " + ManagerReputation.Prestige + "/" + PoolPrestigeReq + ")"; return false; }
                if (economy == null || !economy.TrySpend(PoolCost)) { msg = "Water is expensive up here. ($" + PoolCost + ")"; return false; }
                PoolUnlocked = true;
                msg = "ROOF POOL OPEN. What could possibly go wrong at night.";
                return true;
            default:
                msg = "";
                return true;
        }
    }

    private System.Random _rng;
    private int _lastBarDay = -1, _lastCasinoDay = -1, _lastPoolDay = -1, _settledGymDay = -1;
    private float _ambienceTimer;
    private readonly Dictionary<int, List<GuestAgent>> _crowd = new Dictionary<int, List<GuestAgent>>();

    private static readonly Dictionary<int, Vector3[]> FloorSpots = new Dictionary<int, Vector3[]>
    {
        { RestaurantFloor, new[] { new Vector3(-6.5f, 12f, -2.5f), new Vector3(-5f, 12f, 2f), new Vector3(2f, 12f, 0f), new Vector3(5f, 12f, -2f) } },
        { GymFloor,        new[] { new Vector3(-5f, 16f, 0f), new Vector3(0f, 16f, 2f), new Vector3(3f, 16f, -2f) } },
        { CasinoFloor,     new[] { new Vector3(-4f, 20f, 1f), new Vector3(0f, 20f, -2f), new Vector3(4f, 20f, 1.5f), new Vector3(-6f, 20f, -3f) } },
        { PoolFloor,       new[] { new Vector3(-4f, 24f, 0f), new Vector3(2f, 24f, 2f), new Vector3(5f, 24f, -2f), new Vector3(-2f, 24f, -3.5f) } },
    };

    private void Awake()
    {
        _rng = new System.Random(rngSeed);
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        foreach (var f in FloorSpots.Keys) _crowd[f] = new List<GuestAgent>();
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

        // 酒吧晚市 20:00
        if (hour >= 20f && _lastBarDay != day)
        {
            _lastBarDay = day;
            int income = OccupiedCount() * 8;
            if (income > 0 && economy != null)
            {
                economy.RecordCheckout(income, 1f);
                FloatingTextFx.Spawn(new Vector3(-6f, 12f, -2f), "+$" + income + " BAR", new Color(0.95f, 0.7f, 0.3f));
            }
        }

        // 赌场夜场 21:00
        if (CasinoUnlocked && hour >= 21f && _lastCasinoDay != day)
        {
            _lastCasinoDay = day;
            int swing = -300 + (int)(_rng.NextDouble() * 900);
            if (swing >= 0 && economy != null) economy.RecordCheckout(swing, 1f);
            else if (economy != null) economy.TrySpend(-swing);
            FloatingTextFx.Spawn(new Vector3(0f, 20f, 0f), (swing >= 0 ? "+$" : "-$") + Mathf.Abs(swing) + " CASINO",
                swing >= 0 ? new Color(0.35f, 0.95f, 0.4f) : new Color(1f, 0.4f, 0.3f), 1.1f);
        }

        // 泳池派对 22:00
        if (PoolUnlocked && hour >= 22f && _lastPoolDay != day)
        {
            _lastPoolDay = day;
            bool success = _rng.NextDouble() < 0.6;
            if (demandLoop != null) demandLoop.prototypeSatisfactionScore += success ? 3 : -2;
            FloatingTextFx.Spawn(new Vector3(0f, 24f, 0f),
                success ? "POOL PARTY!! +3😎" : "PARTY OUT OF CONTROL -2",
                success ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.4f, 0.3f), 1.3f);
            CameraShaker.Shake(success ? 0.1f : 0.25f, 0.5f);
        }

        // 时段人气（每 8 秒调整一次各设施层的闲逛客）
        _ambienceTimer -= Time.deltaTime;
        if (_ambienceTimer <= 0f)
        {
            _ambienceTimer = 8f;
            UpdateAmbience(DayPeriodLogic.PeriodFor(hour));
        }
    }

    private void UpdateAmbience(DayPeriod period)
    {
        foreach (var kv in FloorSpots)
        {
            int floor = kv.Key;
            if (!_crowd.ContainsKey(floor)) _crowd[floor] = new List<GuestAgent>(); // 热重载自愈
            var crowd = _crowd[floor];
            crowd.RemoveAll(g => g == null);
            int want = FloorAccessible(floor) ? DayPeriodLogic.ActivityFor(floor, period) : 0;

            while (crowd.Count < want)
            {
                // 从该层电梯口"到达"
                var g = GuestAgent.Spawn(new Vector3(7.6f, FloorMath.BaseYFor(floor), 0f), "visitor_f" + floor);
                g.TravelTo(kv.Value[_rng.Next(kv.Value.Length)], null);
                crowd.Add(g);
            }
            while (crowd.Count > want)
            {
                var g = crowd[crowd.Count - 1];
                crowd.RemoveAt(crowd.Count - 1);
                if (g != null)
                {
                    var gg = g;
                    gg.TravelTo(new Vector3(9.3f, FloorMath.BaseYFor(floor), 0f), () => { if (gg != null) Destroy(gg.gameObject); });
                }
            }
            // 随机换位（有活人感）
            if (crowd.Count > 0)
            {
                var mover = crowd[_rng.Next(crowd.Count)];
                if (mover != null) mover.TravelTo(kv.Value[_rng.Next(kv.Value.Length)], null);
            }
        }
    }

    private void HandleDaySettled(int day, int served, DayLedger ledger)
    {
        if (GymUnlocked && demandLoop != null && _settledGymDay != day)
        {
            _settledGymDay = day;
            demandLoop.prototypeSatisfactionScore += 1;
        }
    }
}
