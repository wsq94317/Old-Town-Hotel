using System.Collections.Generic;
using UnityEngine;

// Owns per-room renovation tiers + the active renovation queue. Renovation is a
// cash sink that raises hotel value (and thus credit limit) and drives the
// "renovate all rooms" goal. Ticks on the day-settled event. (Phase 5 Renovation.)
[DisallowMultipleComponent]
public sealed class RenovationSystem : MonoBehaviour
{
    [SerializeField] private RenovationConfigSO config;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private int totalRooms = 8;
    [SerializeField] private int startingRoomNumber = 101;

    private RenovationQueue _queue;
    private Dictionary<int, RoomTier> _tiers;
    private List<int> _roomOrder;

    public RenovationConfigSO Config => config;
    public int TotalRooms => totalRooms;
    public int RenovatedCount { get; private set; }   // rooms at Basic or better
    public int OpenCount => totalRooms;                // all rooms open in v1 (gating is a later step)
    public IReadOnlyList<int> RoomNumbers => _roomOrder;
    public RenovationQueue Queue => _queue;

    private void Awake()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        Initialize(config, economy, totalRooms, startingRoomNumber);
        if (dayController != null) dayController.OnDaySettled += HandleDaySettled;
    }

    private void OnDestroy()
    {
        if (dayController != null) dayController.OnDaySettled -= HandleDaySettled;
    }

    public void Initialize(RenovationConfigSO cfg, EconomySystem econ, int rooms, int firstRoom)
    {
        config = cfg;
        economy = econ;
        totalRooms = Mathf.Max(0, rooms);
        startingRoomNumber = firstRoom;
        _queue = new RenovationQueue();
        _tiers = new Dictionary<int, RoomTier>();
        _roomOrder = new List<int>();
        for (int i = 0; i < totalRooms; i++)
        {
            int n = firstRoom + i;
            _tiers[n] = RoomTier.Old;
            _roomOrder.Add(n);
        }
        RenovatedCount = 0;
    }

    public void InitializeForTest(RenovationConfigSO cfg, EconomySystem econ, int rooms, int firstRoom)
        => Initialize(cfg, econ, rooms, firstRoom);

    public RoomTier TierOf(int room)
        => _tiers != null && _tiers.TryGetValue(room, out var t) ? t : RoomTier.Old;

    public bool IsRenovating(int room)
    {
        if (_queue == null) return false;
        foreach (var j in _queue.Active) if (j.RoomNumber == room) return true;
        return false;
    }

    public int DaysRemaining(int room)
    {
        if (_queue == null) return 0;
        foreach (var j in _queue.Active) if (j.RoomNumber == room) return j.DaysRemaining;
        return 0;
    }

    // Cost to renovate the eligible subset of `rooms` to `target` right now.
    public int QuoteFor(IList<int> rooms, RoomTier target)
        => config != null ? config.BatchCost(target, Eligible(rooms, target).Count) : 0;

    // Pay + queue a batch renovation. Returns false if no eligible rooms or unaffordable.
    public bool StartRenovation(IList<int> rooms, RoomTier target)
    {
        if (config == null || economy == null || target == RoomTier.Old) return false;
        var eligible = Eligible(rooms, target);
        if (eligible.Count == 0) return false;
        int cost = config.BatchCost(target, eligible.Count);
        if (!economy.TrySpend(cost)) return false;
        int days = config.DaysFor(target);
        foreach (var n in eligible) _queue.Start(new RenovationJob(n, target, days));
        return true;
    }

    private List<int> Eligible(IList<int> rooms, RoomTier target)
    {
        var list = new List<int>();
        if (rooms == null || _tiers == null) return list;
        foreach (var n in rooms)
            if (_tiers.ContainsKey(n) && (int)_tiers[n] < (int)target && !IsRenovating(n))
                list.Add(n);
        return list;
    }

    // ── Save / load ──────────────────────────────────────────────────────────
    public RenovationState CaptureState()
    {
        var s = new RenovationState
        {
            totalRooms = totalRooms,
            startingRoomNumber = startingRoomNumber
        };
        if (_tiers != null && _roomOrder != null)
            foreach (var n in _roomOrder)
                s.rooms.Add(new RoomTierEntry { room = n, tier = (int)_tiers[n] });
        if (_queue != null)
            foreach (var j in _queue.Active)
                s.jobs.Add(new RenoJobEntry { room = j.RoomNumber, targetTier = (int)j.TargetTier, daysRemaining = j.DaysRemaining });
        return s;
    }

    public void RestoreState(RenovationState s)
    {
        if (s == null) return;
        totalRooms = s.totalRooms;
        startingRoomNumber = s.startingRoomNumber;
        _tiers = new Dictionary<int, RoomTier>();
        _roomOrder = new List<int>();
        foreach (var e in s.rooms)
        {
            if (!_tiers.ContainsKey(e.room)) _roomOrder.Add(e.room);
            _tiers[e.room] = (RoomTier)e.tier;
        }
        _queue = new RenovationQueue();
        foreach (var j in s.jobs)
            _queue.Start(new RenovationJob(j.room, (RoomTier)j.targetTier, j.daysRemaining));
        Recount();
    }

    private void HandleDaySettled(int day, int served, DayLedger ledger) => AdvanceDay();

    // Advance renovations one day; completed jobs flip their room's tier.
    public void AdvanceDay()
    {
        if (_queue == null) return;
        var done = _queue.TickDay();
        foreach (var job in done) _tiers[job.RoomNumber] = job.TargetTier;
        Recount();
    }

    private void Recount()
    {
        int c = 0;
        foreach (var kv in _tiers) if ((int)kv.Value >= (int)RoomTier.Basic) c++;
        RenovatedCount = c;
    }
}
