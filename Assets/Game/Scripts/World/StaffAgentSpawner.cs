using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class StaffAgentSpawner : MonoBehaviour
{
    [SerializeField] private EconomySystem economy;
    [SerializeField] private Vector3 lobbySpawn = new Vector3(2f, 0f, 0f);
    [SerializeField] private Vector3 receptionPost = new Vector3(0f, 0f, 3.9f);

    private readonly List<StaffAgent> _agents = new List<StaffAgent>();
    private readonly Dictionary<StaffMember, GameObject> _byMember = new Dictionary<StaffMember, GameObject>();
    private static Material _matHsk;
    private static Material _matInsp;
    private static Material _matReception;
    private StaffBreakRoom _breakRoom;
    private StaffFacilitySystem _facilities;

    public IReadOnlyList<StaffAgent> Agents => _agents;

    private void OnEnable()
    {
        var pads = new Vector3[FloorMath.FloorCount];
        var exits = new Vector3[FloorMath.FloorCount];
        for (int i = 0; i < FloorMath.FloorCount; i++)
        {
            pads[i] = new Vector3(9.3f, FloorMath.BaseYFor(i), 0f);
            exits[i] = new Vector3(7.6f, FloorMath.BaseYFor(i), 0f);
        }

        FloorNavigator.RegisterStairs(pads, exits);
    }

    private void Start()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (economy == null || economy.Payroll == null) return;

        _breakRoom = StaffBreakRoom.EnsureInScene();
        _facilities = StaffFacilitySystem.EnsureInScene();
        GeneratedPlaceholderArt.EnsureLobbyDecor();

        bool hasInspector = false;
        foreach (var member in economy.Payroll.Roster)
        {
            if (member.Role == StaffRole.Inspector)
            {
                hasInspector = true;
                break;
            }
        }

        if (!hasInspector && economy.Config != null)
            economy.HireCandidate(new StaffMember(StaffRole.Inspector, "Inspector", economy.Config.WageFor(StaffRole.Inspector)));

        foreach (var member in economy.Payroll.Roster)
            SpawnFor(member);

        economy.Payroll.OnHired += SpawnFor;
        economy.Payroll.OnFired += DespawnFor;
    }

    private void OnDestroy()
    {
        if (economy != null && economy.Payroll != null)
        {
            economy.Payroll.OnHired -= SpawnFor;
            economy.Payroll.OnFired -= DespawnFor;
        }
    }

    private void SpawnFor(StaffMember member)
    {
        if (member == null || member.Role == StaffRole.Manager) return;
        if (_byMember.ContainsKey(member)) return;

        Vector3 spawnPos = member.Role == StaffRole.Reception ? receptionPost : lobbySpawn;
        Vector3 idleAnchor = IdleAnchorFor(member);

        var go = new GameObject("Staff_" + member.Role + "_" + member.DisplayName);
        go.transform.position = spawnPos;

        var nav = go.AddComponent<NavMeshAgent>();
        nav.speed = 3f;
        nav.radius = 0.3f;
        nav.height = 1.6f;
        nav.angularSpeed = 720f;
        nav.acceleration = 16f;

        var cap = go.AddComponent<CapsuleCollider>();
        cap.height = 1.6f;
        cap.radius = 0.35f;
        cap.center = new Vector3(0f, 0.8f, 0f);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Visual";
        quad.transform.SetParent(go.transform);
        quad.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var quadRenderer = quad.GetComponent<Renderer>();
        quad.transform.localScale = new Vector3(0.7f, 1.5f, 1f);
        if (!GeneratedPlaceholderArt.ApplyStaffSprite(quad.transform, quadRenderer, member.Role))
            quadRenderer.sharedMaterial = MaterialFor(member.Role);
        quad.AddComponent<BillboardSprite>();

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var worldLabel = label.AddComponent<StaffAgentWorldLabel>();
        worldLabel.SetLabel(BuildWorldLabel(member), Color.white);

        var agent = go.AddComponent<StaffAgent>();
        agent.Init(member, new System.Random(_agents.Count * 7919 + 12345));
        agent.SetIdleAnchor(idleAnchor);
        go.AddComponent<AgentFloorVisibility>();

        _agents.Add(agent);
        _byMember[member] = go;
    }

    private void DespawnFor(StaffMember member)
    {
        if (member == null || !_byMember.TryGetValue(member, out GameObject go)) return;

        _byMember.Remove(member);
        var agent = go.GetComponent<StaffAgent>();
        if (agent != null)
        {
            _agents.Remove(agent);
            agent.AbortTask();
            Destroy(agent);
        }

        var walker = go.GetComponent<GuestAgent>();
        if (walker == null) walker = go.AddComponent<GuestAgent>();
        var walkerRef = walker;
        walker.TravelTo(new Vector3(0f, 0f, -5.2f), () => Destroy(walkerRef.gameObject));
    }

    private Vector3 IdleAnchorFor(StaffMember member)
    {
        if (member == null) return lobbySpawn;
        if (member.Role == StaffRole.Reception) return receptionPost;

        if (member.Role == StaffRole.Housekeeper || member.Role == StaffRole.Inspector)
        {
            if (_breakRoom == null) _breakRoom = StaffBreakRoom.EnsureInScene();
            if (_breakRoom != null)
                return _breakRoom.GetIdleAnchorForRole(member.Role, ExistingRoleCount(member.Role));
        }

        return lobbySpawn;
    }

    private int ExistingRoleCount(StaffRole role)
    {
        int count = 0;
        for (int i = 0; i < _agents.Count; i++)
        {
            var agent = _agents[i];
            if (agent != null && agent.Member != null && agent.Member.Role == role)
                count++;
        }

        return count;
    }

    private static Material MaterialFor(StaffRole role)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        switch (role)
        {
            case StaffRole.Housekeeper:
                if (_matHsk == null) _matHsk = new Material(shader) { color = RoleColor(role) };
                return _matHsk;
            case StaffRole.Inspector:
                if (_matInsp == null) _matInsp = new Material(shader) { color = RoleColor(role) };
                return _matInsp;
            default:
                if (_matReception == null) _matReception = new Material(shader) { color = RoleColor(role) };
                return _matReception;
        }
    }

    public static Color RoleColor(StaffRole role)
    {
        switch (role)
        {
            case StaffRole.Housekeeper:
                return new Color(0.25f, 0.75f, 0.3f);
            case StaffRole.Inspector:
                return new Color(0.25f, 0.45f, 0.9f);
            case StaffRole.Reception:
                return new Color(0.7f, 0.4f, 0.85f);
            default:
                return new Color(0.75f, 0.65f, 0.3f);
        }
    }

    public static string RoleDisplayName(StaffRole role)
    {
        switch (role)
        {
            case StaffRole.Housekeeper:
                return "Housekeeper";
            case StaffRole.Inspector:
                return "Inspector";
            case StaffRole.Reception:
                return "Reception";
            case StaffRole.Manager:
                return "Manager";
            default:
                return role.ToString();
        }
    }

    public static string BuildWorldLabel(StaffMember member)
    {
        if (member == null) return "Unknown\nStaff";
        return $"{member.DisplayName}\n{RoleDisplayName(member.Role)}";
    }
}

[DisallowMultipleComponent]
public sealed class StaffBreakRoom : MonoBehaviour
{
    [SerializeField] private bool autoBuildVisuals = true;

    private static readonly Vector3[] HousekeeperOffsets =
    {
        new Vector3(-0.95f, 0f, -0.45f),
        new Vector3(-0.35f, 0f, 0.1f),
        new Vector3(-1.1f, 0f, 0.75f),
        new Vector3(-0.2f, 0f, 0.85f),
    };

    private static readonly Vector3[] InspectorOffsets =
    {
        new Vector3(0.45f, 0f, -0.45f),
        new Vector3(1.05f, 0f, 0.1f),
        new Vector3(0.3f, 0f, 0.75f),
        new Vector3(1.2f, 0f, 0.85f),
    };

    private static Material _floorMat;
    private static Material _trimMat;
    private static Material _tableMat;
    private static Material _seatMat;

    public static StaffBreakRoom EnsureInScene()
    {
        // Include inactive：休息室挂在楼层树里，玩家在楼上时 Floor1 是隐藏的——
        // 不带这个参数会找不到然后重复造一间
        var existing = FindFirstObjectByType<StaffBreakRoom>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        var go = new GameObject("StaffBreakRoom");
        // 出生位置优先读场景锚点（布局由锚点驱动；StaffFacilitySystem 还会再对齐一次）
        var world = GameObject.Find("World");
        Transform anchor = null;
        if (world != null)
            foreach (var t in world.GetComponentsInChildren<Transform>(true))
                if (t.name == "LobbyStaffBreakRoomAnchor") { anchor = t; break; }
        go.transform.position = anchor != null ? anchor.position : new Vector3(-7.4f, FloorMath.BaseYFor(0), -3.7f);
        return go.AddComponent<StaffBreakRoom>();
    }

    public Vector3 GetIdleAnchorForRole(StaffRole role, int roleIndex)
    {
        switch (role)
        {
            case StaffRole.Housekeeper:
                return AnchorFromOffsets(HousekeeperOffsets, roleIndex);
            case StaffRole.Inspector:
                return AnchorFromOffsets(InspectorOffsets, roleIndex);
            default:
                return transform.position;
        }
    }

    private void Awake()
    {
        if (GetComponent<AgentFloorVisibility>() == null)
            gameObject.AddComponent<AgentFloorVisibility>();

        if (autoBuildVisuals)
            BuildVisualsIfNeeded();
    }

    private Vector3 AnchorFromOffsets(Vector3[] offsets, int roleIndex)
    {
        if (offsets == null || offsets.Length == 0) return transform.position;
        int safeIndex = Mathf.Abs(roleIndex) % offsets.Length;
        return transform.TransformPoint(offsets[safeIndex]);
    }

    private void BuildVisualsIfNeeded()
    {
        if (transform.Find("Floor") != null) return;
        if (GeneratedPlaceholderArt.TryDecorateBreakRoom(transform)) return;

        // 墙高/家具高对齐大堂灰盒（外墙 0.8）：1.9 的近黑高墙在 45° 视角里像悬空二层
        BuildBlock("Floor", new Vector3(0f, 0.03f, 0.2f), new Vector3(4.2f, 0.06f, 3f), FloorMaterial());
        BuildBlock("BackWall", new Vector3(0f, 0.45f, 1.55f), new Vector3(4.2f, 0.9f, 0.12f), TrimMaterial());
        BuildBlock("SideWall", new Vector3(-2.05f, 0.45f, 0.15f), new Vector3(0.12f, 0.9f, 2.75f), TrimMaterial());
        BuildBlock("Table", new Vector3(0.25f, 0.32f, 0.2f), new Vector3(1.25f, 0.64f, 0.7f), TableMaterial());
        BuildBlock("Bench_Left", new Vector3(-1.1f, 0.22f, 0.2f), new Vector3(0.75f, 0.44f, 1.35f), SeatMaterial());
        BuildBlock("Bench_Right", new Vector3(1.35f, 0.22f, 0.2f), new Vector3(0.75f, 0.44f, 1.35f), SeatMaterial());
        BuildBlock("Locker", new Vector3(1.65f, 0.45f, 1.05f), new Vector3(0.45f, 0.9f, 0.45f), TableMaterial());

        var sign = new GameObject("Sign");
        sign.transform.SetParent(transform, false);
        sign.transform.localPosition = new Vector3(0f, 1.5f, 0.65f);
        sign.transform.localScale = Vector3.one * 0.24f;

        var text = sign.AddComponent<TextMeshPro>();
        text.text = "STAFF\nBREAK ROOM";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 4.8f;
        text.fontStyle = FontStyles.Bold;
        text.lineSpacing = -8f;
        text.color = new Color(0.97f, 0.95f, 0.86f);
        text.outlineWidth = 0.28f;
        text.outlineColor = new Color(0.08f, 0.06f, 0.04f, 0.9f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        if (text.font == null && TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        sign.AddComponent<BillboardSprite>();
    }

    private void BuildBlock(string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var collider = block.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        block.name = name;
        block.transform.SetParent(transform, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;
        block.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Material FloorMaterial()
    {
        if (_floorMat != null) return _floorMat;
        _floorMat = NewMaterial(new Color(0.36f, 0.23f, 0.18f));
        return _floorMat;
    }

    private static Material TrimMaterial()
    {
        if (_trimMat != null) return _trimMat;
        // 近黑(0.19)读起来像黑洞/别的楼层，调亮到暖灰棕
        _trimMat = NewMaterial(new Color(0.42f, 0.36f, 0.32f));
        return _trimMat;
    }

    private static Material TableMaterial()
    {
        if (_tableMat != null) return _tableMat;
        _tableMat = NewMaterial(new Color(0.55f, 0.40f, 0.29f));
        return _tableMat;
    }

    private static Material SeatMaterial()
    {
        if (_seatMat != null) return _seatMat;
        _seatMat = NewMaterial(new Color(0.28f, 0.48f, 0.33f));
        return _seatMat;
    }

    private static Material NewMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { color = color };
    }
}

public static class GeneratedPlaceholderArt
{
    private const string UiRoot = "GeneratedPlaceholders/UI/";
    private const string WorldRoot = "GeneratedPlaceholders/World/";

    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

    public static Sprite LoadUiSprite(string name) => LoadSprite(UiRoot + name);
    public static Sprite LoadWorldSprite(string name) => LoadSprite(WorldRoot + name);

    public static Sprite GuestPortrait(Room2DGuestType type)
    {
        switch (type)
        {
            case Room2DGuestType.Family:
                return LoadUiSprite("guest_family");
            case Room2DGuestType.VIP:
                return LoadUiSprite("guest_vip");
            default:
                return LoadUiSprite("guest_business");
        }
    }

    public static Sprite WorkerPortrait(StaffRole role)
    {
        switch (role)
        {
            case StaffRole.Housekeeper:
                return LoadUiSprite("worker_housekeeper");
            case StaffRole.Inspector:
                return LoadUiSprite("worker_inspector");
            case StaffRole.Reception:
                return LoadUiSprite("worker_reception");
            case StaffRole.Manager:
                return LoadUiSprite("worker_manager");
            default:
                return null;
        }
    }

    public static Sprite RoomInterior(Room2DRoomCategory category)
    {
        switch (category)
        {
            case Room2DRoomCategory.Single:
                return LoadUiSprite("room_single");
            case Room2DRoomCategory.Twin:
                return LoadUiSprite("room_twin");
            case Room2DRoomCategory.Family:
                return LoadUiSprite("room_family");
            default:
                return LoadUiSprite("room_king") ?? LoadUiSprite("room_single");
        }
    }

    public static bool ApplyStaffSprite(Transform quadTransform, Renderer renderer, StaffRole role)
    {
        string spriteName;
        switch (role)
        {
            case StaffRole.Housekeeper:
                spriteName = "staff_housekeeper";
                break;
            case StaffRole.Inspector:
                spriteName = "staff_inspector";
                break;
            case StaffRole.Reception:
                spriteName = "staff_reception";
                break;
            default:
                return false;
        }

        return ApplySpriteToQuad(quadTransform, renderer, LoadWorldSprite(spriteName), 1.75f);
    }

    public static bool ApplyGuestSprite(Transform quadTransform, Renderer renderer, string label)
    {
        string spriteName = StableHash(label) % 2 == 0 ? "guest_male" : "guest_female";
        return ApplySpriteToQuad(quadTransform, renderer, LoadWorldSprite(spriteName), 1.7f);
    }

    public static bool ApplyLuggageSprite(Transform quadTransform, Renderer renderer, int index)
    {
        string spriteName = index % 2 == 0 ? "furniture_luggage_beige" : "furniture_luggage_blue";
        return ApplyNamedWorldSprite(quadTransform, renderer, spriteName, 0.72f);
    }

    public static bool ApplyNamedWorldSprite(Transform quadTransform, Renderer renderer, string spriteName, float targetHeight)
    {
        return ApplySpriteToQuad(quadTransform, renderer, LoadWorldSprite(spriteName), targetHeight);
    }

    public static bool TryDecorateBreakRoom(Transform root)
    {
        if (root == null) return false;
        var roomSprite = LoadWorldSprite("env_break_room");
        if (roomSprite == null) return false;

        CreateDecorQuad(root, "Backdrop", roomSprite, new Vector3(0f, 0.92f, 0.28f), 2.6f, false);
        CreateDecorQuad(root, "Sign", LoadWorldSprite("env_wall_short"), new Vector3(0f, 1.55f, 1.3f), 1.2f, true);
        return true;
    }

    public static void EnsureLobbyDecor()
    {
        if (Object.FindFirstObjectByType<GeneratedPlaceholderDecorTag>() != null) return;

        var root = new GameObject("GeneratedPlaceholderDecor");
        root.AddComponent<GeneratedPlaceholderDecorTag>();
        root.AddComponent<AgentFloorVisibility>();
        root.transform.position = Vector3.zero;

        CreateDecorQuad(root.transform, "FrontDeskCounter", LoadWorldSprite("furniture_reception_counter"), new Vector3(0.9f, 0.82f, 3.25f), 1.35f, true);
        CreateDecorQuad(root.transform, "QueueRope", LoadWorldSprite("prop_queue_rope"), new Vector3(-0.15f, 0.32f, 1.95f), 0.85f, true);
        CreateDecorQuad(root.transform, "LobbySofa", LoadWorldSprite("furniture_sofa_ornate"), new Vector3(-4.8f, 0.85f, 2.35f), 1.45f, true);
        CreateDecorQuad(root.transform, "CoffeeTable", LoadWorldSprite("furniture_coffee_table"), new Vector3(-3.85f, 0.42f, 2.1f), 0.7f, true);
        CreateDecorQuad(root.transform, "Armchair", LoadWorldSprite("furniture_armchair"), new Vector3(-2.8f, 0.72f, 2.55f), 1.15f, true);
        CreateDecorQuad(root.transform, "Plant", LoadWorldSprite("furniture_plant"), new Vector3(-1.45f, 0.7f, 2.95f), 1.15f, true);
        CreateDecorQuad(root.transform, "LuggageCart", LoadWorldSprite("furniture_luggage_cart"), new Vector3(1.95f, 0.78f, 1.25f), 1.3f, true);
        CreateDecorQuad(root.transform, "VendingMachine", LoadWorldSprite("furniture_vending_machine"), new Vector3(-7.55f, 0.88f, 0.95f), 1.4f, true);
    }

    private static void CreateDecorQuad(Transform parent, string name, Sprite sprite, Vector3 localPosition, float targetHeight, bool billboard)
    {
        if (parent == null || sprite == null) return;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var collider = quad.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPosition;
        if (billboard) quad.AddComponent<BillboardSprite>();

        var renderer = quad.GetComponent<Renderer>();
        ApplySpriteToQuad(quad.transform, renderer, sprite, targetHeight);
    }

    private static bool ApplySpriteToQuad(Transform quadTransform, Renderer renderer, Sprite sprite, float targetHeight)
    {
        if (quadTransform == null || renderer == null || sprite == null) return false;

        renderer.sharedMaterial = MaterialForSprite(sprite);
        float aspect = Mathf.Max(0.01f, sprite.rect.width / sprite.rect.height);
        quadTransform.localScale = new Vector3(targetHeight * aspect, targetHeight, 1f);
        return true;
    }

    private static Material MaterialForSprite(Sprite sprite)
    {
        string key = sprite.name;
        if (MaterialCache.TryGetValue(key, out Material cached) && cached != null) return cached;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Standard");

        var material = new Material(shader) { color = Color.white };
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", sprite.texture);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", sprite.texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        MaterialCache[key] = material;
        return material;
    }

    private static Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (SpriteCache.TryGetValue(path, out Sprite cached)) return cached;

        var sprite = Resources.Load<Sprite>(path);
        SpriteCache[path] = sprite;
        return sprite;
    }

    private static int StableHash(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return Mathf.Abs(hash);
        }
    }
}

public sealed class GeneratedPlaceholderDecorTag : MonoBehaviour
{
}
