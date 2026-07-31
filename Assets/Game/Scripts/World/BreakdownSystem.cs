using System.Collections.Generic;
using UnityEngine;

public class BreakdownSystem : MonoBehaviour
{
    private struct DeferredIncident
    {
        public Vector3 Pos;
        public int RoomNumber;
        public string Kind;
    }

    private class Incident
    {
        public string Id;
        public Vector3 Pos;
        public Room2DEntity Room;
        public BreakdownSeverity Severity;
        public string Kind;
        public float NextEscalateHour;
        public GameObject Marker;
        public GameObject Puddle;
        public Material Mat;
    }

    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private StaffAgentSpawner spawner;
    [SerializeField] private ManagerController manager;
    [SerializeField] private int rngSeed = 20261;

    private System.Random _rng;
    private readonly List<Incident> _active = new List<Incident>();
    private readonly List<DeferredIncident> _tapedForTomorrow = new List<DeferredIncident>();
    private readonly List<int> _lockedRoomNumbers = new List<int>();
    private int _scheduledDay = -1;
    private DayPeriod _lastPeriod = (DayPeriod)(-1);
    private DayPeriod _spawnPeriodToday;
    private bool _spawnedToday;
    private Incident _panelIncident;
    private string _story = "";
    private float _storyUntil;
    private int _idCounter;
    private bool _restoredRoomStateApplied = true;

    public bool PanelOpen => _panelIncident != null;
    public int ActiveCount => _active.Count;
    public string PanelTitle => _panelIncident == null
        ? ""
        : BreakdownLogic.SeverityLabel(_panelIncident.Severity) + " - " + _panelIncident.Kind;
    public string PanelDetail => _panelIncident == null
        ? ""
        : (_panelIncident.Room != null
            ? "Room " + _panelIncident.Room.roomNumber
              + (_panelIncident.Room.currentState == Room2DState.Occupied
                  ? " · guest occupied · repair from the corridor"
                  : "")
            : "Public area");
    public bool HasHousekeeper => FindHousekeeper() != null;
    public bool CanLockPanelRoom => _panelIncident != null
                                    && _panelIncident.Room != null
                                    && _panelIncident.Room.currentState != Room2DState.Occupied;

    public float EstimatedLossPerMinute(HotelSim sim)
    {
        float loss = 0f;
        for (int i = 0; i < _active.Count; i++)
            loss += EstimatedIncidentLossPerMinute(_active[i], sim);
        return loss;
    }

    public bool TryGetHighestLossIncident(
        HotelSim sim,
        out Vector3 anchor,
        out int roomNumber,
        out string title,
        out float lossPerMinute)
    {
        Incident best = null;
        float bestLoss = -1f;
        for (int i = 0; i < _active.Count; i++)
        {
            Incident candidate = _active[i];
            float candidateLoss = EstimatedIncidentLossPerMinute(candidate, sim);
            if (candidateLoss <= bestLoss) continue;
            best = candidate;
            bestLoss = candidateLoss;
        }

        if (best == null)
        {
            anchor = Vector3.zero;
            roomNumber = 0;
            title = "";
            lossPerMinute = 0f;
            return false;
        }

        anchor = InteractionPoint(best);
        roomNumber = best.Room != null ? best.Room.roomNumber : 0;
        title = best.Kind;
        lossPerMinute = Mathf.Max(0f, bestLoss);
        return true;
    }

    private static float EstimatedIncidentLossPerMinute(Incident incident, HotelSim sim)
    {
        if (incident == null) return 0f;

        float severityMultiplier = 0.45f + (int)incident.Severity * 0.35f;
        if (sim != null && incident.Room != null
            && sim.Rooms.Contains(incident.Room.roomNumber))
        {
            RoomRecord room = sim.Rooms.At(incident.Room.roomNumber);
            return HotelEconomyPresentation.RoomRevenuePerMinute(
                sim,
                room.tier,
                HotelEconomyPresentation.ExpectedOccupancy(sim))
                * severityMultiplier;
        }

        return (0.35f + (int)incident.Severity * 0.25f) * severityMultiplier;
    }

    public void CaptureTo(WorldState w)
    {
        w.tapedBreakdowns.Clear();
        foreach (var taped in _tapedForTomorrow)
        {
            w.tapedBreakdowns.Add(new TapedBreakdownEntry
            {
                room = taped.RoomNumber,
                x = taped.Pos.x,
                y = taped.Pos.y,
                z = taped.Pos.z,
                kind = taped.Kind,
            });
        }

        w.lockedRooms.Clear();
        foreach (var roomNumber in _lockedRoomNumbers)
            w.lockedRooms.Add(roomNumber);
    }

    public void RestoreFrom(WorldState w)
    {
        _tapedForTomorrow.Clear();
        foreach (var entry in w.tapedBreakdowns)
        {
            _tapedForTomorrow.Add(new DeferredIncident
            {
                Pos = new Vector3(entry.x, entry.y, entry.z),
                RoomNumber = entry.room,
                Kind = entry.kind,
            });
        }

        _lockedRoomNumbers.Clear();
        foreach (var roomNumber in w.lockedRooms)
            _lockedRoomNumbers.Add(roomNumber);

        _restoredRoomStateApplied = false;
        ApplyRestoredWorldStateIfReady();
    }

    public bool ApplyRestoredWorldStateIfReady()
    {
        if (_restoredRoomStateApplied) return true;
        if (!HasPendingRoomBoundRestore())
        {
            _restoredRoomStateApplied = true;
            return true;
        }

        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (demandLoop == null || demandLoop.rooms == null || demandLoop.rooms.Length == 0)
            return false;

        foreach (var roomNumber in _lockedRoomNumbers)
        {
            var room = FindRoomByNumber(roomNumber);
            if (room != null)
                room.SetState(Room2DState.Blocked);
        }

        _restoredRoomStateApplied = true;
        return true;
    }

    private bool HasPendingRoomBoundRestore()
    {
        if (_lockedRoomNumbers.Count > 0) return true;
        foreach (var taped in _tapedForTomorrow)
            if (taped.RoomNumber >= 0) return true;
        return false;
    }

    private Room2DEntity FindRoomByNumber(int number)
    {
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (number < 0 || demandLoop == null || demandLoop.rooms == null) return null;
        foreach (var room in demandLoop.rooms)
            if (room != null && room.roomNumber == number) return room;
        return null;
    }

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
        if (_rng == null) _rng = new System.Random(rngSeed);
        if (!ApplyRestoredWorldStateIfReady()) return;

        float hour = dayController.Clock.CurrentHour;
        int day = dayController.CurrentDay;

        if (day != _scheduledDay)
        {
            _scheduledDay = day;

            foreach (var incident in new List<Incident>(_active))
                Remove(incident);

            foreach (var roomNumber in _lockedRoomNumbers)
            {
                var room = FindRoomByNumber(roomNumber);
                if (room != null && room.currentState == Room2DState.Blocked)
                    room.SetState(Room2DState.Dirty);
            }
            _lockedRoomNumbers.Clear();

            bool recurringIncident = _tapedForTomorrow.Count > 0
                                     && IncidentDailyBudget.TryClaim(day, "breakdown-recurring");
            if (recurringIncident)
            {
                DeferredIncident taped = _tapedForTomorrow[0];
                Spawn(
                    taped.Pos,
                    FindRoomByNumber(taped.RoomNumber),
                    taped.Kind,
                    BreakdownSeverity.Moderate);
            }
            _tapedForTomorrow.Clear();

            _lastPeriod = (DayPeriod)(-1);
            _spawnPeriodToday = (DayPeriod)_rng.Next(0, 4);
            // A normal day has zero or one breakdown. A recurring taped repair
            // consumes that day's incident budget instead of stacking another alert.
            _spawnedToday = recurringIncident || _rng.NextDouble() >= 0.65;
        }

        var period = DayPeriodLogic.PeriodFor(hour);
        if (period != _lastPeriod)
        {
            _lastPeriod = period;
            if (!_spawnedToday && (int)period >= (int)_spawnPeriodToday)
            {
                _spawnedToday = true;
                if (IncidentDailyBudget.TryClaim(day, "breakdown"))
                    SpawnForPeriod(period);
            }
        }

        foreach (var incident in _active)
        {
            if (hour >= incident.NextEscalateHour && incident.Severity < BreakdownSeverity.Severe)
            {
                incident.Severity++;
                incident.NextEscalateHour = hour + BreakdownLogic.EscalateGameHours;
                if (demandLoop != null) demandLoop.prototypeSatisfactionScore -= (int)incident.Severity;
                RefreshVisual(incident);
                PushPhone(incident);
                CameraShaker.Shake(0.08f, 0.25f);
            }
        }

        if (_panelIncident == null && manager != null)
        {
            Vector3 managerPos = manager.transform.position;
            foreach (var incident in _active)
            {
                Vector3 interactionPoint = InteractionPoint(incident);
                if (FloorMath.FloorIndexForY(managerPos.y) != FloorMath.FloorIndexForY(interactionPoint.y)) continue;
                if (Mathf.Abs(managerPos.x - interactionPoint.x) < 2.2f
                    && Mathf.Abs(managerPos.z - interactionPoint.z) < 2.2f)
                {
                    _panelIncident = incident;
                    break;
                }
            }
        }
        else if (_panelIncident != null && manager != null)
        {
            Vector3 managerPos = manager.transform.position;
            Vector3 interactionPoint = InteractionPoint(_panelIncident);
            if (Mathf.Abs(managerPos.x - interactionPoint.x) > 3.2f
                || Mathf.Abs(managerPos.z - interactionPoint.z) > 3.2f
                || FloorMath.FloorIndexForY(managerPos.y) != FloorMath.FloorIndexForY(interactionPoint.y))
            {
                _panelIncident = null;
                GuiModal.End(this);
            }
        }
    }

    private void SpawnForPeriod(DayPeriod period)
    {
        switch (period)
        {
            case DayPeriod.Morning:
                SpawnAtRandomRoom("CLOGGED TOILET", BreakdownSeverity.Minor);
                break;
            case DayPeriod.Midday:
                SpawnAtRandomRoom("LEAKY PIPE", BreakdownSeverity.Minor);
                break;
            case DayPeriod.Afternoon:
                if (FacilitySystem.PoolUnlocked && _rng.NextDouble() < 0.5)
                    Spawn(new Vector3(-2f, FloorMath.BaseYFor(FacilitySystem.PoolFloor), 0.5f), null, "POOL FILTER JAM", BreakdownSeverity.Minor);
                else if (FacilitySystem.GymUnlocked)
                    Spawn(new Vector3(0f, FloorMath.BaseYFor(FacilitySystem.GymFloor), 0f), null, "AC DRIPPING", BreakdownSeverity.Minor);
                else
                    SpawnAtRandomRoom("LEAKY FAUCET", BreakdownSeverity.Minor);
                break;
            default:
                Spawn(new Vector3(_rng.Next(-8, 8), FloorMath.BaseYFor(_rng.NextDouble() < 0.5 ? 0 : 3), _rng.Next(-4, 4)),
                    null, "SPARKING WIRES",
                    dayController.CurrentDay % 5 == 0
                        ? BreakdownSeverity.Moderate
                        : BreakdownSeverity.Minor);
                break;
        }
    }

    private void SpawnAtRandomRoom(string kind, BreakdownSeverity severity)
    {
        if (demandLoop == null || demandLoop.rooms == null) return;

        var candidates = new List<Room2DEntity>();
        foreach (var room in demandLoop.rooms)
        {
            if (room != null && room.currentState != Room2DState.Blocked)
                candidates.Add(room);
        }

        if (candidates.Count == 0) return;
        var chosen = candidates[_rng.Next(candidates.Count)];
        Spawn(chosen.transform.position, chosen, kind, severity);
    }

    private void Spawn(Vector3 pos, Room2DEntity room, string kind, BreakdownSeverity severity)
    {
        var incident = new Incident
        {
            Id = "bd_" + (_idCounter++),
            Pos = pos,
            Room = room,
            Severity = severity,
            Kind = kind,
            NextEscalateHour = dayController.Clock.CurrentHour + BreakdownLogic.EscalateGameHours,
        };

        incident.Marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(incident.Marker.GetComponent<Collider>());
        incident.Marker.name = "BdMarker_" + incident.Id;
        incident.Marker.transform.position = pos + Vector3.up * 1.9f;
        incident.Marker.transform.localScale = Vector3.one * 0.5f;
        incident.Marker.AddComponent<BillboardSprite>();
        incident.Marker.AddComponent<EventIconPulse>();
        incident.Marker.AddComponent<AgentFloorVisibility>();

        incident.Puddle = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(incident.Puddle.GetComponent<Collider>());
        incident.Puddle.name = "BdPuddle_" + incident.Id;
        incident.Puddle.transform.position = pos + Vector3.up * 0.09f;
        incident.Puddle.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        incident.Puddle.transform.localScale = Vector3.one * 0.9f;
        incident.Puddle.AddComponent<AgentFloorVisibility>();

        RefreshVisual(incident);
        _active.Add(incident);
        PushPhone(incident);
    }

    private void RefreshVisual(Incident incident)
    {
        if (incident.Mat == null)
            incident.Mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        incident.Mat.color = BreakdownLogic.SeverityColor(incident.Severity);
        if (incident.Marker != null) incident.Marker.GetComponent<Renderer>().sharedMaterial = incident.Mat;
        if (incident.Puddle != null)
        {
            incident.Puddle.GetComponent<Renderer>().sharedMaterial = incident.Mat;
            incident.Puddle.transform.localScale = Vector3.one * (0.9f + (int)incident.Severity * 0.5f);
        }
    }

    private void PushPhone(Incident incident)
    {
        Vector3 anchor = InteractionPoint(incident);
        ManagerPhone.Push(
            incident.Id,
            "ALERT " + BreakdownLogic.SeverityLabel(incident.Severity) + " " + incident.Kind
            + (incident.Room != null ? " @ Room " + incident.Room.roomNumber : ""),
            anchor,
            BreakdownLogic.SeverityColor(incident.Severity));
    }

    private static Vector3 InteractionPoint(Incident incident)
    {
        if (incident == null) return Vector3.zero;
        return incident.Room != null
            ? RoomDoor.ExteriorPointFor(incident.Room)
            : incident.Pos;
    }

    private void Remove(Incident incident)
    {
        if (incident.Marker != null) Destroy(incident.Marker);
        if (incident.Puddle != null) Destroy(incident.Puddle);
        if (incident.Mat != null) Destroy(incident.Mat);
        ManagerPhone.Resolve(incident.Id);
        _active.Remove(incident);
        if (_panelIncident == incident) { _panelIncident = null; GuiModal.End(this); }
    }

    private void Choose(BreakdownFix fix)
    {
        var incident = _panelIncident;
        _panelIncident = null;
        GuiModal.End(this);
        if (incident == null) return;

        bool clumsy = false;
        bool fast = false;
        if (fix == BreakdownFix.SendStaff && spawner != null)
        {
            foreach (var agent in spawner.Agents)
            {
                if (agent?.Member != null && agent.Member.Role == StaffRole.Housekeeper)
                {
                    clumsy = agent.Member.HasTrait(StaffTrait.Clumsy);
                    fast = agent.Member.HasTrait(StaffTrait.FastHands);
                    break;
                }
            }
        }

        var outcome = BreakdownLogic.Resolve(fix, _rng.NextDouble(), clumsy, fast);

        if (outcome.CashDelta > 0 && economy != null) economy.RecordMiscIncome(outcome.CashDelta);
        if (demandLoop != null) demandLoop.prototypeSatisfactionScore += outcome.SatisfactionDelta;
        if (outcome.ManagerSlapstick && manager != null)
        {
            CameraShaker.Shake(0.25f, 0.4f);
            FloatingTextFx.Spawn(manager.transform.position, "SPLOOSH!", new Color(0.4f, 0.7f, 1f), 1.2f);
        }
        if (outcome.CashDelta > 0 && manager != null)
            FloatingTextFx.Spawn(manager.transform.position, "+$" + outcome.CashDelta + " tip", new Color(0.35f, 0.95f, 0.4f));

        if (outcome.Fixed)
        {
            if (outcome.TapedRecurrence)
            {
                _tapedForTomorrow.Add(new DeferredIncident
                {
                    Pos = incident.Pos,
                    RoomNumber = incident.Room != null ? incident.Room.roomNumber : -1,
                    Kind = incident.Kind,
                });
            }
            if (outcome.LockedRoom && incident.Room != null)
            {
                incident.Room.SetState(Room2DState.Blocked);
                _lockedRoomNumbers.Add(incident.Room.roomNumber);
            }
            Remove(incident);
        }
        else if (outcome.SeverityDelta > 0 && incident.Severity < BreakdownSeverity.Severe)
        {
            incident.Severity += outcome.SeverityDelta;
            RefreshVisual(incident);
            PushPhone(incident);
        }

        _story = outcome.Story;
        _storyUntil = Time.time + 4.5f;
    }

    public void ResolvePanel(BreakdownFix fix) => Choose(fix);

    private StaffAgent FindHousekeeper()
    {
        if (spawner == null) return null;
        foreach (StaffAgent agent in spawner.Agents)
            if (agent?.Member != null && agent.Member.Role == StaffRole.Housekeeper)
                return agent;
        return null;
    }

    private void OnGUI()
    {
        if (WorldManagementHud.IsActive) return;
        // 全屏晨报期间全体让位：IMGUI 没有 z 序，谁画谁上；报告必须是唯一的画手，
        // 否则警报/HIRE/庆祝框会压在报告上，而且它们的按钮还会抢走转发的点击。
        if (HotelSimSceneBridge.Instance != null && HotelSimSceneBridge.Instance.AwaitingMorningReport) return;
        Vector2 view = GuiScale.Begin();
        float w = view.x;
        float h = view.y;

        if (Time.time < _storyUntil)
            GUI.Box(UiLayout.NextToast(w, h), _story);

        if (_panelIncident == null) return;

        var incident = _panelIncident;
        bool isRoom = incident.Room != null;
        float panelHeight = isRoom ? 158 : 132;
        if (!GuiModal.Begin(this, w, h, panelHeight, out Rect box)) return;
        GUI.Box(box,
            BreakdownLogic.SeverityLabel(incident.Severity) + " - " + incident.Kind
            + (isRoom ? " (Room " + incident.Room.roomNumber + ")" : ""));

        bool hasHousekeeper = false;
        if (spawner != null)
        {
            foreach (var agent in spawner.Agents)
            {
                if (agent?.Member != null && agent.Member.Role == StaffRole.Housekeeper)
                {
                    hasHousekeeper = true;
                    break;
                }
            }
        }

        bool canLock = isRoom && incident.Room.currentState != Room2DState.Occupied;
        bool prevEnabled = GUI.enabled;

        if (GuiInput.Button(GuiModal.Row(box, 0, top: 30f, rowHeight: 24f, gap: 4f), "Fix it yourself (55%, tips or a face full of water)"))
        {
            Choose(BreakdownFix.DIY);
        }
        else
        {
            GUI.enabled = hasHousekeeper;
            if (GuiInput.Button(GuiModal.Row(box, 1, top: 30f, rowHeight: 24f, gap: 4f),
                    hasHousekeeper ? "Send housekeeping (traits matter)" : "Send housekeeping (you have none)"))
            {
                Choose(BreakdownFix.SendStaff);
            }
            else
            {
                GUI.enabled = true;
                if (GuiInput.Button(GuiModal.Row(box, 2, top: 30f, rowHeight: 24f, gap: 4f), "DUCT TAPE (free, definitely permanent)"))
                {
                    Choose(BreakdownFix.DuctTape);
                }
                else if (isRoom)
                {
                    GUI.enabled = canLock;
                    if (GuiInput.Button(GuiModal.Row(box, 3, top: 30f, rowHeight: 24f, gap: 4f),
                            canLock ? "Lock the room (no problem if no witnesses)" : "Lock the room (there's a GUEST inside)"))
                    {
                        Choose(BreakdownFix.LockRoom);
                    }
                }
            }
        }

        GUI.enabled = prevEnabled;
    }
}
