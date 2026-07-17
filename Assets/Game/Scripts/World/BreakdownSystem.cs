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
    private Incident _panelIncident;
    private string _story = "";
    private float _storyUntil;
    private int _idCounter;
    private bool _restoredRoomStateApplied = true;

    public bool PanelOpen => _panelIncident != null;

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

            foreach (var taped in _tapedForTomorrow)
                Spawn(taped.Pos, FindRoomByNumber(taped.RoomNumber), taped.Kind, BreakdownSeverity.Moderate);
            _tapedForTomorrow.Clear();

            _lastPeriod = (DayPeriod)(-1);
        }

        var period = DayPeriodLogic.PeriodFor(hour);
        if (period != _lastPeriod)
        {
            _lastPeriod = period;
            int count = 1 + (_rng.NextDouble() < 0.4 ? 1 : 0);
            for (int i = 0; i < count; i++) SpawnForPeriod(period);
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
                if (FloorMath.FloorIndexForY(managerPos.y) != FloorMath.FloorIndexForY(incident.Pos.y)) continue;
                if (Mathf.Abs(managerPos.x - incident.Pos.x) < 2.2f && Mathf.Abs(managerPos.z - incident.Pos.z) < 2.2f)
                {
                    _panelIncident = incident;
                    break;
                }
            }
        }
        else if (_panelIncident != null && manager != null)
        {
            Vector3 managerPos = manager.transform.position;
            if (Mathf.Abs(managerPos.x - _panelIncident.Pos.x) > 3.2f
                || Mathf.Abs(managerPos.z - _panelIncident.Pos.z) > 3.2f
                || FloorMath.FloorIndexForY(managerPos.y) != FloorMath.FloorIndexForY(_panelIncident.Pos.y))
            {
                _panelIncident = null;
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
                    null, "SPARKING WIRES", BreakdownSeverity.Moderate);
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
        ManagerPhone.Push(
            incident.Id,
            "ALERT " + BreakdownLogic.SeverityLabel(incident.Severity) + " " + incident.Kind
            + (incident.Room != null ? " @ Room " + incident.Room.roomNumber : ""),
            incident.Pos,
            BreakdownLogic.SeverityColor(incident.Severity));
    }

    private void Remove(Incident incident)
    {
        if (incident.Marker != null) Destroy(incident.Marker);
        if (incident.Puddle != null) Destroy(incident.Puddle);
        if (incident.Mat != null) Destroy(incident.Mat);
        ManagerPhone.Resolve(incident.Id);
        _active.Remove(incident);
        if (_panelIncident == incident) _panelIncident = null;
    }

    private void Choose(BreakdownFix fix)
    {
        var incident = _panelIncident;
        _panelIncident = null;
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

    private void OnGUI()
    {
        Vector2 view = GuiScale.Begin();
        float w = view.x;
        float h = view.y;

        if (Time.time < _storyUntil)
            GUI.Box(new Rect(w * 0.5f - 230, h * 0.13f, 460, 40), _story);

        if (_panelIncident == null) return;

        var incident = _panelIncident;
        bool isRoom = incident.Room != null;
        float panelHeight = isRoom ? 158 : 132;
        GUI.Box(new Rect(w * 0.5f - 180, h * 0.32f, 360, panelHeight),
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

        if (GuiInput.Button(new Rect(w * 0.5f - 160, h * 0.32f + 30, 320, 24), "Fix it yourself (55%, tips or a face full of water)"))
        {
            Choose(BreakdownFix.DIY);
        }
        else
        {
            GUI.enabled = hasHousekeeper;
            if (GuiInput.Button(new Rect(w * 0.5f - 160, h * 0.32f + 58, 320, 24),
                    hasHousekeeper ? "Send housekeeping (traits matter)" : "Send housekeeping (you have none)"))
            {
                Choose(BreakdownFix.SendStaff);
            }
            else
            {
                GUI.enabled = true;
                if (GuiInput.Button(new Rect(w * 0.5f - 160, h * 0.32f + 86, 320, 24), "DUCT TAPE (free, definitely permanent)"))
                {
                    Choose(BreakdownFix.DuctTape);
                }
                else if (isRoom)
                {
                    GUI.enabled = canLock;
                    if (GuiInput.Button(new Rect(w * 0.5f - 160, h * 0.32f + 114, 320, 24),
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
