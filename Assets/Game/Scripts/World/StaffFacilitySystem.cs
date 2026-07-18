using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum StaffFacilityKind
{
    BreakRoom,
    PublicToilet,
    Kitchen,
    FloorSupport,
    Exit
}

public enum ToiletInspectionKind
{
    Empty,
    StaffHiding,
    StaffLegitimate,
    Guest
}

public readonly struct ToiletInspectionResult
{
    public readonly ToiletInspectionKind Kind;
    public readonly StaffAgent Staff;

    public ToiletInspectionResult(ToiletInspectionKind kind, StaffAgent staff = null)
    {
        Kind = kind;
        Staff = staff;
    }
}

[DisallowMultipleComponent]
public sealed class StaffFacilityNode : MonoBehaviour
{
    public StaffFacilityKind Kind { get; private set; }
    public Vector3 Anchor => transform.TransformPoint(new Vector3(0f, 0f, -0.75f));

    public void Configure(StaffFacilityKind kind, string displayName, Vector3 worldPosition, Vector3 size, Color color)
    {
        Kind = kind;
        name = "Facility_" + kind;
        transform.position = worldPosition;

        var collider = gameObject.GetComponent<BoxCollider>();
        if (collider == null) collider = gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.8f, 0f);
        collider.size = new Vector3(Mathf.Max(1.2f, size.x), 1.8f, Mathf.Max(1.2f, size.z));

        if (gameObject.GetComponent<AgentFloorVisibility>() == null)
            gameObject.AddComponent<AgentFloorVisibility>();

        BuildPlaceholder(displayName, size, color);
    }

    private void BuildPlaceholder(string displayName, Vector3 size, Color color)
    {
        if (transform.Find("Floor") != null) return;

        Material material = NewMaterial(color);
        BuildBlock("Floor", new Vector3(0f, 0.03f, 0f), new Vector3(size.x, 0.06f, size.z), material);
        BuildBlock("BackWall", new Vector3(0f, 0.85f, size.z * 0.48f), new Vector3(size.x, 1.7f, 0.1f), material);
        BuildBlock("SideWall", new Vector3(-size.x * 0.48f, 0.85f, 0f), new Vector3(0.1f, 1.7f, size.z), material);

        var sign = new GameObject("Sign");
        sign.transform.SetParent(transform, false);
        sign.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        sign.transform.localScale = Vector3.one * 0.22f;
        var text = sign.AddComponent<TextMeshPro>();
        text.text = displayName;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 4.5f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        if (text.font == null && TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        sign.AddComponent<BillboardSprite>();
    }

    private void BuildBlock(string blockName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var blockCollider = block.GetComponent<Collider>();
        if (blockCollider != null) Destroy(blockCollider);
        block.name = blockName;
        block.transform.SetParent(transform, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;
        block.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Material NewMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { color = color };
    }
}

[DisallowMultipleComponent]
public sealed class StaffFacilitySystem : MonoBehaviour
{
    private static readonly Vector3 MapExit = new Vector3(0f, 0f, -5.2f);
    private static StaffFacilitySystem _instance;

    private readonly List<StaffFacilityNode> _supportNodes = new List<StaffFacilityNode>();
    private StaffAgentSpawner _spawner;
    private Room2DDemoDayController _dayController;
    private Room2DPrototypeDemandLoop _demandLoop;
    private ManagerController _pendingInspector;
    private StaffAgent _reservedStaff;
    private StaffAgent _toiletStaff;
    private GuestAgent _toiletGuest;
    private bool _staffHiding;
    private float _guestUseUntil;
    private float _nextGuestVisit;
    private float _frontDeskVacancySeconds;
    private float _nextVacancyPenaltyAt = StaffRoutineLogic.FrontDeskVacancyGraceSeconds;
    private int _observedDay = -1;

    public StaffFacilityNode PublicToilet { get; private set; }
    public StaffFacilityNode Kitchen { get; private set; }
    public Vector3 BreakRoomAnchor { get; private set; }
    public Vector3 ExitPoint => MapExit;
    public float FrontDeskVacancySeconds => _frontDeskVacancySeconds;
    public bool ToiletOccupied => _toiletStaff != null || _toiletGuest != null;

    public static StaffFacilitySystem EnsureInScene()
    {
        if (_instance != null) return _instance;
        _instance = FindFirstObjectByType<StaffFacilitySystem>();
        if (_instance != null) return _instance;
        return new GameObject("StaffFacilitySystem").AddComponent<StaffFacilitySystem>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        BuildFacilities();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Start()
    {
        ResolveReferences();
        _nextGuestVisit = Time.time + Random.Range(12f, 22f);
    }

    private void Update()
    {
        ResolveReferences();
        TickDayReset();
        TickPendingInspection();
        TickGuestUse();
        TickFrontDeskCoverage();
    }

    public Vector3 SupportAnchorForFloor(int floor, int index)
    {
        if (floor <= 0 || _supportNodes.Count == 0) return BreakRoomAnchor;
        int safeFloor = Mathf.Clamp(floor - 1, 0, _supportNodes.Count - 1);
        Vector3 baseAnchor = _supportNodes[safeFloor].Anchor;
        float offset = ((Mathf.Abs(index) % 3) - 1) * 0.55f;
        return baseAnchor + new Vector3(offset, 0f, 0f);
    }

    public Vector3 PatrolAnchor(int floor, int index)
    {
        int safeFloor = Mathf.Clamp(floor, 0, FloorMath.FloorCount - 1);
        float x = index % 2 == 0 ? -2.8f : 3.2f;
        float z = index % 3 == 0 ? -1.7f : 1.3f;
        return new Vector3(x, FloorMath.BaseYFor(safeFloor), z);
    }

    public bool TryReserveToilet(StaffAgent staff)
    {
        if (staff == null || PublicToilet == null) return false;
        if (_reservedStaff != null || _toiletStaff != null || _toiletGuest != null) return false;
        _reservedStaff = staff;
        return true;
    }

    public bool BeginStaffToiletUse(StaffAgent staff, bool hiding)
    {
        if (staff == null || _reservedStaff != staff || _toiletGuest != null) return false;
        _reservedStaff = null;
        _toiletStaff = staff;
        _staffHiding = hiding;
        return true;
    }

    public void ReleaseToilet(StaffAgent staff)
    {
        if (_reservedStaff == staff) _reservedStaff = null;
        if (_toiletStaff != staff) return;
        _toiletStaff = null;
        _staffHiding = false;
    }

    public void OnFacilityTapped(StaffFacilityNode node, ManagerController manager)
    {
        if (node == null || manager == null) return;
        if (node.Kind != StaffFacilityKind.PublicToilet)
        {
            manager.MoveTo(node.Anchor);
            return;
        }

        _pendingInspector = manager;
        manager.MoveTo(node.Anchor);
        TickPendingInspection();
    }

    private void BuildFacilities()
    {
        var breakRoom = StaffBreakRoom.EnsureInScene();
        BreakRoomAnchor = breakRoom != null ? breakRoom.transform.position : new Vector3(-6f, 0f, 1.25f);

        PublicToilet = CreateNode(
            StaffFacilityKind.PublicToilet,
            "PUBLIC TOILET",
            new Vector3(-7.1f, FloorMath.BaseYFor(0), -2.25f),
            new Vector3(2.4f, 0.1f, 2.1f),
            new Color(0.25f, 0.48f, 0.55f));

        Kitchen = CreateNode(
            StaffFacilityKind.Kitchen,
            "KITCHEN",
            new Vector3(-3.8f, FloorMath.BaseYFor(0), -2.45f),
            new Vector3(3.2f, 0.1f, 2.2f),
            new Color(0.52f, 0.36f, 0.24f));

        for (int floor = 1; floor < FloorMath.FloorCount; floor++)
        {
            var node = CreateNode(
                StaffFacilityKind.FloorSupport,
                floor == 1 ? "HSK LOUNGE / STORAGE" : "LINEN / STAFF SUPPORT",
                new Vector3(-5.7f, FloorMath.BaseYFor(floor), -2.15f),
                new Vector3(2.5f, 0.1f, 1.8f),
                new Color(0.33f, 0.42f, 0.31f));
            _supportNodes.Add(node);
        }
    }

    private StaffFacilityNode CreateNode(
        StaffFacilityKind kind,
        string displayName,
        Vector3 position,
        Vector3 size,
        Color color)
    {
        var go = new GameObject("Facility_" + kind);
        go.transform.SetParent(transform, true);
        var node = go.AddComponent<StaffFacilityNode>();
        node.Configure(kind, displayName, position, size, color);
        return node;
    }

    private void ResolveReferences()
    {
        if (_spawner == null) _spawner = FindFirstObjectByType<StaffAgentSpawner>();
        if (_dayController == null) _dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (_demandLoop == null) _demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
    }

    private void TickDayReset()
    {
        if (_dayController == null || _observedDay == _dayController.CurrentDay) return;
        _observedDay = _dayController.CurrentDay;
        _frontDeskVacancySeconds = 0f;
        _nextVacancyPenaltyAt = StaffRoutineLogic.FrontDeskVacancyGraceSeconds;
    }

    private void TickPendingInspection()
    {
        if (_pendingInspector == null || PublicToilet == null) return;
        Vector3 delta = _pendingInspector.transform.position - PublicToilet.Anchor;
        delta.y = 0f;
        if (delta.sqrMagnitude > 2.4f * 2.4f) return;

        _pendingInspector = null;
        InspectToilet();
    }

    private void InspectToilet()
    {
        var interaction = FindFirstObjectByType<ManagerInteraction>();
        ToiletInspectionResult result;

        if (_toiletStaff != null)
        {
            StaffAgent staff = _toiletStaff;
            bool hiding = _staffHiding;
            _toiletStaff = null;
            _staffHiding = false;
            staff.HandleToiletInspection(hiding);
            result = new ToiletInspectionResult(
                hiding ? ToiletInspectionKind.StaffHiding : ToiletInspectionKind.StaffLegitimate,
                staff);
        }
        else if (_toiletGuest != null)
        {
            GuestAgent leavingGuest = _toiletGuest;
            _toiletGuest = null;
            SendGuestOffMap(leavingGuest);
            result = new ToiletInspectionResult(ToiletInspectionKind.Guest);
        }
        else
        {
            result = new ToiletInspectionResult(ToiletInspectionKind.Empty);
        }

        interaction?.HandleToiletInspection(result);
    }

    private void TickGuestUse()
    {
        if (_toiletGuest != null)
        {
            if (Time.time < _guestUseUntil) return;
            GuestAgent departingGuest = _toiletGuest;
            _toiletGuest = null;
            SendGuestOffMap(departingGuest);
            _nextGuestVisit = Time.time + Random.Range(14f, 28f);
            return;
        }

        if (Time.time < _nextGuestVisit || _reservedStaff != null || _toiletStaff != null) return;
        if (_dayController == null || _dayController.Clock.CurrentHour < 10f || _dayController.Clock.DayEndReached) return;

        _nextGuestVisit = Time.time + Random.Range(18f, 32f);
        var guest = GuestAgent.Spawn(new Vector3(0f, 0f, -4.8f), "toilet guest");
        guest.TravelTo(PublicToilet.Anchor, () => BeginGuestToiletUse(guest));
    }

    private void BeginGuestToiletUse(GuestAgent guest)
    {
        if (guest == null) return;
        if (_reservedStaff != null || _toiletStaff != null || _toiletGuest != null)
        {
            SendGuestOffMap(guest);
            return;
        }

        _toiletGuest = guest;
        _guestUseUntil = Time.time + Random.Range(5f, 9f);
    }

    private static void SendGuestOffMap(GuestAgent guest)
    {
        if (guest == null) return;
        guest.TravelTo(MapExit, () =>
        {
            if (guest != null) Destroy(guest.gameObject);
        });
    }

    private void TickFrontDeskCoverage()
    {
        if (_dayController == null || _spawner == null || _demandLoop == null) return;
        float hour = _dayController.Clock.CurrentHour;
        if (hour < 10f || hour >= 22f) return;

        bool covered = false;
        foreach (var agent in _spawner.Agents)
        {
            if (agent != null && agent.ProvidesFrontDeskCoverage)
            {
                covered = true;
                break;
            }
        }

        if (covered)
        {
            _frontDeskVacancySeconds = 0f;
            _nextVacancyPenaltyAt = StaffRoutineLogic.FrontDeskVacancyGraceSeconds;
            return;
        }

        _frontDeskVacancySeconds += Time.deltaTime;
        if (_frontDeskVacancySeconds < _nextVacancyPenaltyAt) return;
        _nextVacancyPenaltyAt += 30f;
        _demandLoop.ApplyExternalSatisfaction(
            StaffRoutineLogic.FrontDeskVacancySatisfactionPenalty,
            "Front desk unattended for over one minute");
    }
}
