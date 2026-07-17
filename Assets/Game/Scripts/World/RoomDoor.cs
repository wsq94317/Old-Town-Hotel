using UnityEngine;
using UnityEngine.AI;

// 房门系统 v2（面板确认制）：
//   经理点房内目标 → 被拦在门口 → 弹面板（像电梯）→ 点 [Swipe & enter] 刷卡开门
//   → 自动继续走向原目标；开门 2 秒后自动关闭（门口没人挡着就关）
//   房内亮度 = 门开着 或 屋里有人（关门不再把人闷进黑屋）
//   员工/客人持工卡/房卡：走近直接开（模拟不受面板打扰）
//   经理闯进【入住中】房间内部 → WrongRoomDrama（受惊客人+枕头+撵人）
public class RoomDoor : MonoBehaviour
{
    [SerializeField] private Room2DEntity room;
    [SerializeField] private Vector3 interiorCenter;
    [SerializeField] private float doorSenseRadius = 0.9f;
    [SerializeField] private float autoCloseAfterOpen = 2f;

    private GameObject _doorVisual;
    private Renderer _overlay;
    private Vector3 _doorClosedPos;
    private float _openness;
    private float _brightness;
    private bool _targetOpen;
    private float _openedAt;
    private bool _swiping;
    private bool _panelOpen;
    private Vector3 _pendingDest;
    private bool _hasPendingDest;
    private float _intrusionCooldownUntil;

    private ManagerController _manager;
    private NavMeshAgent _managerAgent;
    private StaffAgentSpawner _spawner;

    private static Material _doorMat, _overlayMat, _cardMat;

    /// <summary>任意房门面板打开（WorldInputController 拦截世界点击用）。</summary>
    public static bool AnyPanelOpen { get; private set; }
    private static int _openPanelCount;

    public Room2DEntity Room => room;
    public Vector3 InteriorCenter => interiorCenter;
    public bool IsOpen => _openness > 0.7f;

    public static RoomDoor Build(Transform floorParent, Room2DEntity roomEntity, Vector3 doorPos, Vector3 interior)
    {
        var go = new GameObject("Door_" + roomEntity.roomNumber);
        go.transform.SetParent(floorParent);
        go.transform.position = doorPos;
        var door = go.AddComponent<RoomDoor>();
        door.room = roomEntity;
        door.interiorCenter = interior;
        door.BuildVisuals();
        return door;
    }

    private static Material OverlayBaseMat()
    {
        if (_overlayMat == null)
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            _overlayMat = new Material(unlit) { color = new Color(0f, 0f, 0f, 0.78f) };
            _overlayMat.SetFloat("_Surface", 1f);
            _overlayMat.SetFloat("_Blend", 0f);
            _overlayMat.renderQueue = 3000;
            _overlayMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _overlayMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _overlayMat.SetInt("_ZWrite", 0);
        }
        return _overlayMat;
    }

    private void BuildVisuals()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (_doorMat == null) _doorMat = new Material(shader) { color = new Color(0.5f, 0.33f, 0.18f) };

        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        KillCollider(panel);
        panel.name = "DoorPanel";
        panel.transform.SetParent(transform);
        panel.transform.localPosition = Vector3.up * 0.55f;
        panel.transform.localScale = new Vector3(1.8f, 1.1f, 0.12f);
        panel.GetComponent<Renderer>().sharedMaterial = _doorMat;

        var ov = GameObject.CreatePrimitive(PrimitiveType.Quad);
        KillCollider(ov);
        ov.name = "Darkness";
        ov.transform.SetParent(transform);
        ov.transform.position = interiorCenter + Vector3.up * 0.07f;
        ov.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ov.transform.localScale = new Vector3(4.6f, 4.9f, 1f);
        ov.GetComponent<Renderer>().sharedMaterial = OverlayBaseMat();
    }

    private static void KillCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
    }

    private void Start()
    {
        _manager = FindFirstObjectByType<ManagerController>();
        _managerAgent = _manager != null ? _manager.GetComponent<NavMeshAgent>() : null;
        _spawner = FindFirstObjectByType<StaffAgentSpawner>();

        var panelT = transform.Find("DoorPanel");
        if (panelT != null)
        {
            _doorVisual = panelT.gameObject;
            _doorClosedPos = _doorVisual.transform.localPosition;
        }
        var ovT = transform.Find("Darkness");
        if (ovT != null)
        {
            _overlay = ovT.GetComponent<Renderer>();
            _overlay.material = new Material(OverlayBaseMat());
            ovT.position = interiorCenter + Vector3.up * 0.07f;
            ovT.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    private void OnDestroy()
    {
        if (_panelOpen) { _openPanelCount--; AnyPanelOpen = _openPanelCount > 0; }
    }

    private bool InteriorContains(Vector3 pos) =>
        Mathf.Abs(pos.x - interiorCenter.x) < 2.4f && Mathf.Abs(pos.z - interiorCenter.z) < 2.4f;

    private bool SameFloor(Vector3 pos) =>
        FloorMath.FloorIndexForY(pos.y) == FloorMath.FloorIndexForY(transform.position.y);

    private static float FlatDist(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Vector3 DoorFrontPoint()
    {
        Vector3 outward = transform.position - interiorCenter;
        outward.y = 0f;
        return transform.position + outward.normalized * 0.7f;
    }

    private void SetPanel(bool open)
    {
        if (_panelOpen == open) return;
        _panelOpen = open;
        _openPanelCount += open ? 1 : -1;
        AnyPanelOpen = _openPanelCount > 0;
    }

    private void Update()
    {
        bool managerInside = false;
        bool anyoneInside = false;
        bool anyoneAtDoor = false;

        // ── 经理：拦在门外 + 面板 + 闯房判定 ─────────────────────────────
        if (_manager != null && SameFloor(_manager.transform.position))
        {
            Vector3 mp = _manager.transform.position;
            managerInside = InteriorContains(mp);
            float dDoor = FlatDist(mp, transform.position);
            if (dDoor < doorSenseRadius) anyoneAtDoor = true;

            // 拦截：门关着 && 经理目的地在房内 && 人还在外面 → 改道门口
            if (!IsOpen && !_swiping && !managerInside && _managerAgent != null && _managerAgent.hasPath
                && InteriorContains(_managerAgent.destination))
            {
                _pendingDest = _managerAgent.destination;
                _hasPendingDest = true;
                _manager.MoveTo(DoorFrontPoint());
            }

            // 门被别人（员工/客人）打开了 → 拦截中的原目标自动续走
            if (IsOpen && _hasPendingDest && !managerInside)
            {
                _hasPendingDest = false;
                _manager.MoveTo(_pendingDest);
            }

            // 到门口 && 门关着 → 弹面板
            if (!IsOpen && !_swiping && !managerInside && dDoor < doorSenseRadius && !_panelOpen)
            {
                SetPanel(true);
            }
            // 走远/门开了 → 收面板
            if (_panelOpen && (dDoor > doorSenseRadius + 0.8f || IsOpen))
            {
                SetPanel(false);
            }

            // 查错房
            if (managerInside && room != null && room.currentState == Room2DState.Occupied
                && Time.time >= _intrusionCooldownUntil)
            {
                _intrusionCooldownUntil = Time.time + 4f;
                WrongRoomDrama.Trigger(this, _manager);
            }
        }
        else if (_panelOpen)
        {
            SetPanel(false);
        }

        // ── 员工/客人：直接开门 ───────────────────────────────────────────
        if (_spawner != null)
        {
            foreach (var a in _spawner.Agents)
            {
                if (a == null || !SameFloor(a.transform.position)) continue;
                if (FlatDist(a.transform.position, transform.position) < doorSenseRadius)
                {
                    anyoneAtDoor = true;
                    OpenNow();
                }
                if (InteriorContains(a.transform.position)) anyoneInside = true;
            }
        }
        foreach (var g in FindObjectsByType<GuestAgent>(FindObjectsSortMode.None))
        {
            if (g == null || !SameFloor(g.transform.position)) continue;
            if (FlatDist(g.transform.position, transform.position) < doorSenseRadius)
            {
                anyoneAtDoor = true;
                OpenNow();
            }
            if (InteriorContains(g.transform.position)) anyoneInside = true;
        }
        if (managerInside) anyoneInside = true;

        // ── 自动关门：开门 2 秒后关（门口有人挡着就等） ─────────────────────
        if (_targetOpen && Time.time - _openedAt > autoCloseAfterOpen && !anyoneAtDoor)
        {
            _targetOpen = false;
        }

        // ── 动画：门（快）+ 灯（慢；门开或屋里有人都算亮） ────────────────
        _openness = Mathf.MoveTowards(_openness, _targetOpen ? 1f : 0f, Time.deltaTime / 0.35f);
        bool lit = _targetOpen || anyoneInside;
        _brightness = Mathf.MoveTowards(_brightness, lit ? 1f : 0f, Time.deltaTime / (lit ? 1.2f : 0.8f));

        if (_doorVisual != null)
            _doorVisual.transform.localPosition = _doorClosedPos + Vector3.right * (1.72f * _openness);
        if (_overlay != null)
        {
            var c = _overlay.material.color;
            c.a = Mathf.Lerp(0.78f, 0f, _brightness);
            _overlay.material.color = c;
        }
    }

    private void OpenNow()
    {
        if (!_targetOpen)
        {
            _targetOpen = true;
            _openedAt = Time.time;
        }
        else
        {
            _openedAt = Time.time; // 有人持续通过就重置关门计时
        }
    }

    private void OnGUI()
    {
        if (!_panelOpen || room == null) return;
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;
        GUI.Box(new Rect(w * 0.5f - 130, h * 0.42f, 260, 96),
            "ROOM " + room.roomNumber + "\n" + RoomStateHint());
        if (GuiInput.Button(new Rect(w * 0.5f - 110, h * 0.42f + 40, 220, 24), "Swipe & enter 🔑"))
        {
            SetPanel(false);
            StartCoroutine(SwipeAndOpen());
        }
        if (GuiInput.Button(new Rect(w * 0.5f - 110, h * 0.42f + 68, 220, 24), "Never mind"))
        {
            SetPanel(false);
            _hasPendingDest = false;
        }
    }

    private string RoomStateHint()
    {
        switch (room.currentState)
        {
            case Room2DState.Occupied: return "Guest inside. Enter at your own risk.";
            case Room2DState.Dirty: return "Needs cleaning.";
            case Room2DState.AwaitingInspection: return "Awaiting inspection.";
            case Room2DState.Ready: return "Ready for guests.";
            default: return room.currentState.ToString();
        }
    }

    private System.Collections.IEnumerator SwipeAndOpen()
    {
        _swiping = true;
        if (_managerAgent != null) _managerAgent.isStopped = true;

        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(card.GetComponent<Collider>());
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (_cardMat == null) _cardMat = new Material(unlit) { color = Color.white };
        card.GetComponent<Renderer>().sharedMaterial = _cardMat;
        card.transform.localScale = new Vector3(0.18f, 0.26f, 1f);
        Vector3 slotTop = transform.position + Vector3.up * 1.25f + Vector3.right * 0.95f;
        card.AddComponent<BillboardSprite>();

        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            card.transform.position = slotTop + Vector3.down * (t / 0.45f * 0.35f);
            yield return null;
        }
        Destroy(card);
        FloatingTextFx.Spawn(transform.position, "BEEP", new Color(0.3f, 1f, 0.4f), 0.7f);

        if (_managerAgent != null) _managerAgent.isStopped = false;
        OpenNow();
        _swiping = false;

        // 继续走向被拦截前的原目标
        if (_hasPendingDest && _manager != null)
        {
            _hasPendingDest = false;
            _manager.MoveTo(_pendingDest);
        }
    }
}
