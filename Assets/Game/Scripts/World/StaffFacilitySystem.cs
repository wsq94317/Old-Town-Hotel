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

    // 站位锚点在"开口侧"的空地上（Configure 按贴墙方向算出）。
    // 教训：写死朝南时，贴南墙的厕所站位点被挤进设施与外墙的缝里——那是南墙的
    // 点击死区（0.8 墙在 35° 俯角下遮挡身后 ~1.1m 的地面射线），玩家点不出去。
    private Vector3 _anchorLocal = new Vector3(0f, 0f, -0.75f);
    public Vector3 Anchor => transform.TransformPoint(_anchorLocal);

    private string _signText;

    // 招牌延迟到楼层第一次显示时再建：节点现在挂在楼层树里，未激活层级上
    // 对 TMP 设 outline 会 NRE（材质在 Awake 前不存在，隔离复现过）。
    private void OnEnable() => BuildSignIfNeeded();

    public void Configure(StaffFacilityKind kind, string displayName, Vector3 worldPosition, Vector3 size, Color color)
    {
        Kind = kind;
        name = "Facility_" + kind;
        transform.position = worldPosition;

        // tap 碰撞体做小做矮：之前 1.8 高全尺寸大箱子把经理站位点包在里面，
        // 走到设施后屏幕中央点哪都命中它 → 每次都触发"走向设施"，玩家像被黏死
        var collider = gameObject.GetComponent<BoxCollider>();
        if (collider == null) collider = gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.45f, 0.15f);
        collider.size = new Vector3(Mathf.Max(1.0f, size.x * 0.7f), 0.9f, Mathf.Max(1.0f, size.z * 0.7f));

        // 节点挂在楼层树里（由 StaffFacilitySystem 指定 parent）：楼层 SetActive
        // 统一管显隐 + 碰撞体——不再需要 AgentFloorVisibility，也不会出现
        // "楼上设施的隐形碰撞体拦截楼下点击"（旧版根级节点的坑）。

        BuildPlaceholder(displayName, size, color);
    }

    private void BuildPlaceholder(string displayName, Vector3 size, Color color)
    {
        if (transform.Find("Floor") != null) return;

        Material material = NewMaterial(color);
        BuildBlock("Floor", new Vector3(0f, 0.03f, 0f), new Vector3(size.x, 0.06f, size.z), material);
        // 墙高对齐大堂灰盒（0.8）：之前 1.7 的高墙在 45° 视角里像悬空的"二楼盒子"。
        // 背墙贴向最近的外墙侧（南半场贴南墙），开口朝向房间开阔面；
        // 站位锚点放在开口侧、地台外 0.3m 的空地上——可见、可点击、不进墙缝。
        float backSign = transform.position.z >= 0f ? 1f : -1f;
        BuildBlock("BackWall", new Vector3(0f, 0.42f, backSign * size.z * 0.48f), new Vector3(size.x, 0.85f, 0.1f), material);
        _anchorLocal = new Vector3(0f, 0f, -backSign * (size.z * 0.5f + 0.3f));
        // 侧墙贴靠外墙一侧：相机从西南 45° 看，东半场的设施侧墙放东侧才不会挡住内部
        float sideSign = transform.position.x >= 0f ? 1f : -1f;
        BuildBlock("SideWall", new Vector3(sideSign * size.x * 0.48f, 0.42f, 0f), new Vector3(0.1f, 0.85f, size.z), material);

        _signText = displayName;
        BuildSignIfNeeded();
    }

    private void BuildSignIfNeeded()
    {
        if (string.IsNullOrEmpty(_signText) || !gameObject.activeInHierarchy) return;
        if (transform.Find("Sign") != null) return;

        var sign = new GameObject("Sign");
        sign.transform.SetParent(transform, false);
        sign.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        sign.transform.localScale = Vector3.one * 0.22f;
        var text = sign.AddComponent<TextMeshPro>();
        text.text = _signText;
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

    private readonly Dictionary<int, StaffFacilityNode> _supportByFloor = new Dictionary<int, StaffFacilityNode>();
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
        // 只有放了后勤锚点的客房层（2F/3F）有本层休息点；设施层(4-7F)不摆员工设施，
        // 员工下楼回一楼休息室（用户要求：员工设施不进客人活动区）。
        if (_supportByFloor.TryGetValue(floor, out var node) && node != null)
        {
            float offset = ((Mathf.Abs(index) % 3) - 1) * 0.55f;
            return node.Anchor + new Vector3(offset, 0f, 0f);
        }
        return BreakRoomAnchor;
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
        Vector3 delta = manager.transform.position - node.Anchor;
        delta.y = 0f;
        bool alreadyThere = delta.sqrMagnitude < 1.6f * 1.6f;

        if (node.Kind != StaffFacilityKind.PublicToilet)
        {
            if (!alreadyThere) manager.MoveTo(node.Anchor); // 已经站在旁边就别再下指令（防黏住）
            return;
        }

        _pendingInspector = manager;
        if (!alreadyThere) manager.MoveTo(node.Anchor);
        TickPendingInspection();
    }

    // 布局全部由场景锚点驱动（不再写死坐标）。锚点是 World/FloorN 下的空 Transform：
    //   Floor1: LobbyStaffBreakRoomAnchor / PublicToiletAnchor / KitchenAnchor
    //           (+ ManagerOfficeReservedAnchor 纯场景预留，不在这里消费)
    //   Floor2: Floor2HskSupportAnchor（走廊西端 Linen 隔间）
    //   Floor3: Floor3HskSupportAnchor（南侧空带 HSK 休息+布草）
    // 锚点缺失 = 响亮警告 + 不建该设施（绝不回退到拍脑袋坐标塞进客房/前台）。
    private void BuildFacilities()
    {
        var breakAnchor = FindSceneAnchor("LobbyStaffBreakRoomAnchor");
        var breakRoom = StaffBreakRoom.EnsureInScene();
        if (breakAnchor != null && breakRoom != null)
        {
            breakRoom.transform.SetParent(breakAnchor.parent, true); // 进楼层树，跟层显隐
            breakRoom.transform.position = breakAnchor.position;
        }
        else if (breakAnchor == null)
        {
            Debug.LogWarning("StaffFacilitySystem: 场景缺少 LobbyStaffBreakRoomAnchor——休息室停留在 StaffBreakRoom 自带位置。");
        }
        BreakRoomAnchor = breakRoom != null ? breakRoom.transform.position
                        : breakAnchor != null ? breakAnchor.position
                        : new Vector3(-7.4f, 0f, -3.7f);

        PublicToilet = CreateNodeAtAnchor(
            "PublicToiletAnchor", StaffFacilityKind.PublicToilet, "PUBLIC TOILET",
            new Vector3(2.6f, 0.1f, 2.2f), new Color(0.25f, 0.48f, 0.55f));

        Kitchen = CreateNodeAtAnchor(
            "KitchenAnchor", StaffFacilityKind.Kitchen, "KITCHEN",
            new Vector3(3.2f, 0.1f, 2.4f), new Color(0.52f, 0.36f, 0.24f));

        _supportByFloor.Clear();
        for (int floor = 1; floor < FloorMath.FloorCount; floor++)
        {
            string anchorName = "Floor" + (floor + 1) + "HskSupportAnchor";
            var anchor = FindSceneAnchor(anchorName);
            if (anchor == null) continue; // 没放锚点的楼层（设施层）不生成员工设施
            var node = CreateNode(
                StaffFacilityKind.FloorSupport,
                floor == 1 ? "LINEN" : "HSK LOUNGE / LINEN",
                anchor,
                floor == 1 ? new Vector3(1.3f, 0.1f, 1.6f) : new Vector3(2.6f, 0.1f, 2.0f),
                new Color(0.33f, 0.42f, 0.31f));
            _supportByFloor[floor] = node;
        }
    }

    private StaffFacilityNode CreateNodeAtAnchor(
        string anchorName, StaffFacilityKind kind, string displayName, Vector3 size, Color color)
    {
        var anchor = FindSceneAnchor(anchorName);
        if (anchor == null)
        {
            Debug.LogWarning("StaffFacilitySystem: 场景缺少锚点 " + anchorName + "——设施 " + kind + " 不生成。");
            return null;
        }
        return CreateNode(kind, displayName, anchor, size, color);
    }

    private static StaffFacilityNode CreateNode(
        StaffFacilityKind kind, string displayName, Transform anchor, Vector3 size, Color color)
    {
        var go = new GameObject("Facility_" + kind);
        go.transform.SetParent(anchor.parent, true); // 挂进楼层根：SetActive 连碰撞体一起管
        var node = go.AddComponent<StaffFacilityNode>();
        node.Configure(kind, displayName, anchor.position, size, color);
        return node;
    }

    /// <summary>在 World 楼层树里找功能区锚点（含未激活楼层）。</summary>
    private static Transform FindSceneAnchor(string anchorName)
    {
        var world = GameObject.Find("World");
        if (world == null) return null;
        foreach (var t in world.GetComponentsInChildren<Transform>(true))
            if (t.name == anchorName) return t;
        return null;
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
        if (PublicToilet == null) return; // 锚点缺失时厕所不存在，客人流程整体停摆（有警告）
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
