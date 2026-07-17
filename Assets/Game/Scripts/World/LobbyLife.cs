using System.Collections.Generic;
using UnityEngine;

// 大堂生活系统：
//   ① 行李寄存处：前台旁行李架，行李箱数量 = 正在等房的客人数（排队+活跃等待）——
//      一眼看出寄存占有率（空/半满/爆满，满 6 件后堆到冒尖）
//   ② 闲逛的在住客：按入住房数量维持 0-3 个客人纸片人在大堂溜达（前台/沙发/吧台间晃）
public class LobbyLife : MonoBehaviour
{
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private Vector3 rackBase = new Vector3(2.4f, 0f, 3.3f); // 前台右侧
    private const int RackCapacity = 6;

    private readonly List<GameObject> _luggage = new List<GameObject>();
    private readonly List<GuestAgent> _loiterers = new List<GuestAgent>();
    private float _wanderTimer;
    private static Material _luggageMat;

    private static readonly Vector3[] LobbySpots =
    {
        new Vector3(-3f, 0f, 1f),   // 大堂中
        new Vector3(3.5f, 0f, -2f), // 大堂南
        new Vector3(-5f, 0f, 1.5f), // Lounge 沙发区
        new Vector3(-7f, 0f, -2f),  // 餐厅角
        new Vector3(1.5f, 0f, 0f),  // 前台前晃悠
    };

    private void Start()
    {
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (_luggageMat == null)
            _luggageMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.55f, 0.35f, 0.5f) };
    }

    private void Update()
    {
        UpdateLuggage();
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
        {
            _wanderTimer = Random.Range(4f, 8f);
            UpdateLoiterers();
        }
    }

    // ── 行李架 ──────────────────────────────────────────────────────────────
    private void UpdateLuggage()
    {
        if (demandLoop == null) return;
        int waiting = demandLoop.UpcomingQueueCount
                    + (demandLoop.activeDemandWaitingForManualAssignment ? 1 : 0);
        int want = Mathf.Min(waiting, RackCapacity + 2); // 爆满还能再堆 2 件冒尖

        while (_luggage.Count < want)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.name = "Luggage";
            box.transform.SetParent(transform);
            int i = _luggage.Count;
            // 两排 3 列，第 7 件起往上堆
            int row = (i % RackCapacity) / 3, col = (i % RackCapacity) % 3, layer = i / RackCapacity;
            box.transform.position = rackBase + new Vector3(col * 0.45f, 0.2f + layer * 0.42f, row * 0.5f);
            box.transform.localScale = new Vector3(0.35f, 0.38f, 0.42f);
            box.transform.localRotation = Quaternion.Euler(0f, Random.Range(-8f, 8f), 0f);
            box.GetComponent<Renderer>().sharedMaterial = _luggageMat;
            box.AddComponent<AgentFloorVisibility>(); // 挂在常驻 Systems 下，不在楼层树里——自管显隐
            _luggage.Add(box);
        }
        while (_luggage.Count > want)
        {
            var last = _luggage[_luggage.Count - 1];
            _luggage.RemoveAt(_luggage.Count - 1);
            if (last != null) Destroy(last);
        }
    }

    // ── 闲逛在住客 ──────────────────────────────────────────────────────────
    private void UpdateLoiterers()
    {
        if (demandLoop == null || demandLoop.rooms == null) return;
        int occupied = 0;
        foreach (var r in demandLoop.rooms)
            if (r != null && r.currentState == Room2DState.Occupied) occupied++;
        int want = Mathf.Min(3, occupied);

        _loiterers.RemoveAll(g => g == null);
        while (_loiterers.Count < want)
        {
            var g = GuestAgent.Spawn(LobbySpots[Random.Range(0, LobbySpots.Length)], "loiterer");
            _loiterers.Add(g);
        }
        while (_loiterers.Count > want)
        {
            var g = _loiterers[_loiterers.Count - 1];
            _loiterers.RemoveAt(_loiterers.Count - 1);
            if (g != null)
            {
                var gg = g;
                gg.TravelTo(new Vector3(0f, 0f, -5.2f), () => { if (gg != null) Destroy(gg.gameObject); });
            }
        }
        // 每拍随机让一位换个地方晃
        if (_loiterers.Count > 0)
        {
            var mover = _loiterers[Random.Range(0, _loiterers.Count)];
            if (mover != null) mover.TravelTo(LobbySpots[Random.Range(0, LobbySpots.Length)], null);
        }
    }
}
