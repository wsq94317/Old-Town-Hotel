using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 花名册 → 场景纸片人：按 Payroll.Roster 生成员工 agent（Manager=玩家自己，跳过；
// Reception 站前台固定岗）。订阅 OnHired/OnFired 同步增删。
// v1 初始 roster 没有 Inspector——没有验房员整个房态循环会卡死，boot 时补雇一名。
public class StaffAgentSpawner : MonoBehaviour
{
    [SerializeField] private EconomySystem economy;
    [SerializeField] private Vector3 lobbySpawn = new Vector3(2f, 0f, 0f);
    [SerializeField] private Vector3 receptionPost = new Vector3(0f, 0f, 3.9f);

    private readonly List<StaffAgent> _agents = new List<StaffAgent>();
    private readonly Dictionary<StaffMember, GameObject> _byMember = new Dictionary<StaffMember, GameObject>();
    private static Material _matHsk, _matInsp, _matReception;

    public IReadOnlyList<StaffAgent> Agents => _agents;

    private void Awake()
    {
        // 楼梯注册表（场景约定坐标：pad x8.5 / exit x6.5，每层 z=0）。
        // 员工跨层不走 StairZone 触发器（那是经理专用），走到点位后代码瞬移。
        var pads = new Vector3[FloorMath.FloorCount];
        var exits = new Vector3[FloorMath.FloorCount];
        for (int i = 0; i < FloorMath.FloorCount; i++)
        {
            pads[i] = new Vector3(8.5f, FloorMath.BaseYFor(i), 0f);
            exits[i] = new Vector3(6.5f, FloorMath.BaseYFor(i), 0f);
        }
        FloorNavigator.RegisterStairs(pads, exits);
    }

    private void Start()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (economy == null || economy.Payroll == null) return;

        // 补雇 Inspector（v1 星标阵容缺口）
        bool hasInspector = false;
        foreach (var m in economy.Payroll.Roster)
        {
            if (m.Role == StaffRole.Inspector) { hasInspector = true; break; }
        }
        if (!hasInspector && economy.Config != null)
        {
            economy.HireCandidate(new StaffMember(StaffRole.Inspector, "Inspector", economy.Config.WageFor(StaffRole.Inspector)));
        }

        foreach (var m in economy.Payroll.Roster) SpawnFor(m);
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
        if (member == null || member.Role == StaffRole.Manager) return; // 经理=玩家
        if (_byMember.ContainsKey(member)) return;

        Vector3 spawnPos = member.Role == StaffRole.Reception ? receptionPost : lobbySpawn;
        var go = new GameObject("Staff_" + member.Role + "_" + member.DisplayName);
        go.transform.position = spawnPos;

        var nav = go.AddComponent<NavMeshAgent>();
        nav.speed = 3f;
        nav.radius = 0.3f;
        nav.height = 1.6f;
        nav.angularSpeed = 720f;
        nav.acceleration = 16f;

        // 点击选中用（M3 监督交互）
        var cap = go.AddComponent<CapsuleCollider>();
        cap.height = 1.6f;
        cap.radius = 0.35f;
        cap.center = new Vector3(0, 0.8f, 0);

        // 纸片视觉（颜色按角色）
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Visual";
        quad.transform.SetParent(go.transform);
        quad.transform.localPosition = new Vector3(0, 0.8f, 0);
        quad.transform.localScale = new Vector3(0.7f, 1.5f, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = MaterialFor(member.Role);
        quad.AddComponent<BillboardSprite>();

        var agent = go.AddComponent<StaffAgent>();
        // 每人独立种子（可复现），避免全员同帧同结果
        agent.Init(member, new System.Random(_agents.Count * 7919 + 12345));
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
            agent.AbortTask();          // 撂下手头活（半途房回 Dirty）
            Destroy(agent);             // 不再接单
        }
        // 走人小演出：收拾好心情走向大门，出门即消失
        var walker = go.GetComponent<GuestAgent>();
        if (walker == null) walker = go.AddComponent<GuestAgent>();
        var walkerRef = walker;
        walker.TravelTo(new Vector3(0f, 0f, -5.2f), () => Destroy(walkerRef.gameObject));
    }

    private static Material MaterialFor(StaffRole role)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        switch (role)
        {
            case StaffRole.Housekeeper:
                if (_matHsk == null) _matHsk = new Material(shader) { color = new Color(0.25f, 0.75f, 0.3f) };
                return _matHsk;
            case StaffRole.Inspector:
                if (_matInsp == null) _matInsp = new Material(shader) { color = new Color(0.25f, 0.45f, 0.9f) };
                return _matInsp;
            default:
                if (_matReception == null) _matReception = new Material(shader) { color = new Color(0.7f, 0.4f, 0.85f) };
                return _matReception;
        }
    }
}
