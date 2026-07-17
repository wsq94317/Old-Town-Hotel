using UnityEngine;
using UnityEngine.AI;

// 电梯（取代楼梯触发区）：走廊东端电梯间。
// 经理走进轿厢 → 弹楼层面板（明确点按钮才移动，无误触）→
// 滑门关 → 瞬移+切层+相机跳 → 滑门开 → 自动走出轿厢。
// 员工/客人不用面板：FloorNavigator 的传送点就设在轿厢位置（"货梯"）。
public class ElevatorController : MonoBehaviour
{
    public static ElevatorController Instance { get; private set; }

    [SerializeField] private FloorVisibilityController floors;
    [SerializeField] private Vector3 cabLocalCenter = new Vector3(9.3f, 0f, 0f); // 每层同位
    [SerializeField] private float cabRadius = 0.75f;

    private ManagerController _manager;
    private NavMeshAgent _managerAgent;
    private bool _panelOpen;
    private bool _traveling;
    private GameObject[] _doorPanels = new GameObject[FloorMath.FloorCount];
    private static Material _doorMat;

    public bool PanelOpen => _panelOpen;

    /// <summary>电梯轿厢在某层的世界坐标（手机 GO 引导用）。</summary>
    public Vector3 CabWorldPos(int floor) =>
        new Vector3(cabLocalCenter.x, FloorMath.BaseYFor(floor), cabLocalCenter.z);

    private void Awake()
    {
        Instance = this;
        if (floors == null) floors = FindFirstObjectByType<FloorVisibilityController>();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        _manager = FindFirstObjectByType<ManagerController>();
        _managerAgent = _manager != null ? _manager.GetComponent<NavMeshAgent>() : null;
        // 找回每层滑门（场景构建的子物体，private 引用不序列化）
        for (int f = 0; f < FloorMath.FloorCount; f++)
        {
            var t = transform.Find("ElevatorDoor_F" + (f + 1));
            if (t != null) _doorPanels[f] = t.gameObject;
        }
    }

    private void Update()
    {
        if (_manager == null || _traveling) return;

        Vector3 cab = CabWorldPos(CurrentManagerFloor());
        Vector3 p = _manager.transform.position;
        bool inside = Mathf.Abs(p.x - cab.x) < cabRadius && Mathf.Abs(p.z - cab.z) < cabRadius + 0.15f;

        if (inside && !_panelOpen)
        {
            // 意图判定：目的地也在轿厢内（点了电梯才开面板；路过不弹）
            Vector3 dest = _managerAgent != null && _managerAgent.hasPath ? _managerAgent.destination : p;
            if (Mathf.Abs(dest.x - cab.x) < cabRadius + 0.3f && Mathf.Abs(dest.z - cab.z) < cabRadius + 0.45f)
            {
                _panelOpen = true;
            }
        }
        else if (!inside && _panelOpen)
        {
            _panelOpen = false; // 走出轿厢自动收面板
        }

        // 滑门：经理/员工/客人靠近本层电梯就开（纯视觉）
        UpdateDoors();
    }

    private int CurrentManagerFloor() => FloorMath.FloorIndexForY(_manager.transform.position.y);

    private void UpdateDoors()
    {
        for (int f = 0; f < FloorMath.FloorCount; f++)
        {
            if (_doorPanels[f] == null) continue;
            bool near = false;
            Vector3 cab = CabWorldPos(f);
            if (_manager != null && FlatNear(_manager.transform.position, cab, 1.6f)) near = true;
            var spawner = FindFirstObjectByType<StaffAgentSpawner>();
            if (!near && spawner != null)
            {
                foreach (var a in spawner.Agents)
                    if (a != null && FlatNear(a.transform.position, cab, 1.6f)) { near = true; break; }
            }
            // 滑门横移：基准位=建造时的世界位 (8.55, y+0.8, 0)，只在 z 轴滑动
            var door = _doorPanels[f].transform;
            Vector3 closedPos = new Vector3(8.55f, FloorMath.BaseYFor(f) + 0.8f, 0f);
            float cur = door.localPosition.z - closedPos.z;
            float target = near ? 1.5f : 0f;
            float next = Mathf.MoveTowards(cur, target, Time.deltaTime * 4f);
            door.localPosition = closedPos + Vector3.forward * next;

            // 门挂在电梯根下不受楼层根显隐控制——手动按当前显示层开关渲染
            var r = _doorPanels[f].GetComponent<Renderer>();
            if (r != null && floors != null) r.enabled = (f == floors.CurrentFloor);
        }
    }

    private static bool FlatNear(Vector3 a, Vector3 b, float r)
    {
        if (FloorMath.FloorIndexForY(a.y) != FloorMath.FloorIndexForY(b.y)) return false;
        a.y = 0; b.y = 0;
        return Vector3.Distance(a, b) < r;
    }

    private void GoToFloor(int targetFloor)
    {
        if (_traveling) return;
        _panelOpen = false;
        StartCoroutine(Travel(targetFloor));
    }

    private System.Collections.IEnumerator Travel(int targetFloor)
    {
        _traveling = true;
        if (_managerAgent != null) _managerAgent.isStopped = true;
        yield return new WaitForSeconds(0.45f); // 关门/运行的节奏感

        _managerAgent.Warp(CabWorldPos(targetFloor));
        if (floors != null) floors.ShowFloor(targetFloor);
        FloatingTextFx.Spawn(CabWorldPos(targetFloor), "DING", new Color(0.95f, 0.85f, 0.3f), 0.8f);
        yield return new WaitForSeconds(0.25f);

        if (_managerAgent != null) _managerAgent.isStopped = false;
        // 自动走出轿厢到走廊
        _manager.MoveTo(CabWorldPos(targetFloor) + new Vector3(-1.6f, 0f, 0f));
        _traveling = false;
    }

    private void OnGUI()
    {
        if (!_panelOpen) return;
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;
        int cur = CurrentManagerFloor();
        GUI.Box(new Rect(w * 0.5f - 110, h * 0.38f, 220, 40 + FloorMath.FloorCount * 30), "ELEVATOR — pick a floor");
        for (int f = 0; f < FloorMath.FloorCount; f++)
        {
            bool enabled = f != cur;
            bool prev = GUI.enabled;
            GUI.enabled = enabled;
            string label = (f + 1) + "F" + (f == cur ? " (here)" : "");
            if (GuiInput.Button(new Rect(w * 0.5f - 90, h * 0.38f + 32 + f * 30, 180, 26), label) && enabled)
            {
                GoToFloor(f);
            }
            GUI.enabled = prev;
        }
    }

    /// <summary>场景构建：每层建电梯间（井壁+轿厢地板+滑门）。</summary>
    public static ElevatorController BuildInScene(Transform worldRoot, GameObject[] floorRoots)
    {
        var go = new GameObject("Elevator");
        go.transform.SetParent(worldRoot);
        go.transform.position = Vector3.zero;
        var ctrl = go.AddComponent<ElevatorController>();

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (_doorMat == null) _doorMat = new Material(shader) { color = new Color(0.75f, 0.75f, 0.8f) };
        var shaftMat = new Material(shader) { color = new Color(0.35f, 0.35f, 0.42f) };
        var padMat = new Material(shader) { color = new Color(0.85f, 0.8f, 0.35f) };

        for (int f = 0; f < FloorMath.FloorCount; f++)
        {
            float y = FloorMath.BaseYFor(f);
            var parent = floorRoots[f].transform;

            // 轿厢地板（金色标识）
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            KillColliderStatic(pad);
            pad.name = "ElevatorPad";
            pad.transform.SetParent(parent);
            pad.transform.position = new Vector3(9.3f, y + 0.03f, 0f);
            pad.transform.localScale = new Vector3(1.5f, 0.06f, 1.6f);
            pad.GetComponent<Renderer>().sharedMaterial = padMat;

            // 井壁（北/南/东三面，西面是门）
            BuildWall(parent, new Vector3(9.3f, y + 0.9f, 0.85f), new Vector3(1.5f, 1.8f, 0.12f), shaftMat);
            BuildWall(parent, new Vector3(9.3f, y + 0.9f, -0.85f), new Vector3(1.5f, 1.8f, 0.12f), shaftMat);
            BuildWall(parent, new Vector3(10.02f, y + 0.9f, 0f), new Vector3(0.12f, 1.8f, 1.6f), shaftMat);

            // 滑门（西面，向北滑开）——挂电梯控制器下便于找回
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            KillColliderStatic(door);
            door.name = "ElevatorDoor_F" + (f + 1);
            door.transform.SetParent(go.transform);
            door.transform.position = new Vector3(8.55f, y + 0.8f, 0f);
            door.transform.localScale = new Vector3(0.1f, 1.6f, 1.6f);
            door.GetComponent<Renderer>().sharedMaterial = _doorMat;
        }
        return ctrl;
    }

    private static void BuildWall(Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        KillColliderStatic(wall);
        wall.name = "ElevatorShaft";
        wall.transform.SetParent(parent);
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static void KillColliderStatic(GameObject g)
    {
        var col = g.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
    }
}
