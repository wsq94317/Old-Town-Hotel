using UnityEngine;

// 房门系统：门禁刷卡 + 黑房亮灯 + 查错房触发。
//   经理走近门口 → 自动刷卡（停顿 0.55s + 卡片划过 + BEEP）→ 门滑开
//   员工/客人（有工卡/房卡）→ 快速开门无停顿
//   门开 → 房内黑幕缓慢变亮（1.2s）；门口/房内没人 2s → 门关、房间变黑
//   经理进入【入住中】房间内部 → 触发 WrongRoomDrama（受惊客人+枕头+撵人）
// 感应用轮询（无物理依赖）：每帧扫描经理/员工/客人与门/房内区的距离。
public class RoomDoor : MonoBehaviour
{
    [SerializeField] private Room2DEntity room;
    [SerializeField] private Vector3 interiorCenter;   // 房间内部中心（世界坐标）
    [SerializeField] private float doorSenseRadius = 0.85f;
    [SerializeField] private float autoCloseDelay = 2f;

    private GameObject _doorVisual;
    private Renderer _overlay;          // 黑幕（水平半透明quad）
    private Vector3 _doorClosedPos;
    private float _openness;            // 门 0..1（快）
    private float _brightness;          // 灯 0..1（慢，"缓慢亮灯"）
    private bool _targetOpen;
    private float _lastPresenceTime;
    private bool _swiping;
    private float _intrusionCooldownUntil;

    private ManagerController _manager;
    private StaffAgentSpawner _spawner;

    private static Material _doorMat, _overlayMat, _cardMat;

    public Room2DEntity Room => room;
    public Vector3 InteriorCenter => interiorCenter;
    public bool IsOpen => _openness > 0.7f;

    /// <summary>场景接线：建门板+黑幕并初始化。doorPos=门口中心（世界），interior=房内中心。</summary>
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
            _overlayMat.SetFloat("_Surface", 1f); // transparent
            _overlayMat.SetFloat("_Blend", 0f);
            _overlayMat.renderQueue = 3000;
            _overlayMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _overlayMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _overlayMat.SetInt("_ZWrite", 0);
        }
        return _overlayMat;
    }

    // 场景接线时（编辑模式）调用：只建层级+sharedMaterial；
    // 运行时引用（_doorVisual/_overlay 是非序列化 private）由 Start 按名字找回。
    private void BuildVisuals()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (_doorMat == null) _doorMat = new Material(shader) { color = new Color(0.5f, 0.33f, 0.18f) };

        // 门板：填满 1.8 门洞
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        KillCollider(panel);
        panel.name = "DoorPanel";
        panel.transform.SetParent(transform);
        panel.transform.localPosition = Vector3.up * 0.55f;
        panel.transform.localScale = new Vector3(1.8f, 1.1f, 0.12f);
        panel.GetComponent<Renderer>().sharedMaterial = _doorMat;

        // 黑幕：罩住房间格（材质实例化留到运行时 Start）
        var ov = GameObject.CreatePrimitive(PrimitiveType.Quad);
        KillCollider(ov);
        ov.name = "Darkness";
        ov.transform.SetParent(transform);
        ov.transform.position = interiorCenter + Vector3.up * 1.45f;
        ov.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ov.transform.localScale = new Vector3(4.6f, 4.9f, 1f);
        ov.GetComponent<Renderer>().sharedMaterial = OverlayBaseMat();
    }

    // 编辑模式 Destroy 无效（帧末才执行、编辑模式不走帧）——必须 DestroyImmediate。
    private static void KillCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }

    private void Start()
    {
        _manager = FindFirstObjectByType<ManagerController>();
        _spawner = FindFirstObjectByType<StaffAgentSpawner>();

        // 找回视觉引用（编辑模式赋的 private 引用不序列化，Play 里是 null）
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
            _overlay.material = new Material(OverlayBaseMat()); // 运行时独立实例做 alpha 动画
            // 黑幕贴地：悬在 1.45m 会被 45° 相机视差投影到走廊/墙上（位置漂移）。
            ovT.position = interiorCenter + Vector3.up * 0.07f;
            ovT.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    /// <summary>房间格矩形判定（半边长 2.4 覆盖 5x5 格，不外溢到走廊）。</summary>
    private bool InteriorContains(Vector3 pos)
    {
        return Mathf.Abs(pos.x - interiorCenter.x) < 2.4f
            && Mathf.Abs(pos.z - interiorCenter.z) < 2.4f;
    }

    private void Update()
    {
        SensePresence();

        // 门（快）：0.35s 开合
        float doorSpeed = Time.deltaTime / 0.35f;
        _openness = Mathf.MoveTowards(_openness, _targetOpen ? 1f : 0f, doorSpeed);
        // 灯（慢）：开门后 1.2s 缓亮；关门后 0.8s 变暗
        float lightSpeed = Time.deltaTime / (_targetOpen ? 1.2f : 0.8f);
        _brightness = Mathf.MoveTowards(_brightness, _targetOpen ? 1f : 0f, lightSpeed);

        if (_doorVisual != null)
        {
            // 门板横向滑进墙里
            _doorVisual.transform.localPosition = _doorClosedPos + Vector3.right * (1.72f * _openness);
        }
        if (_overlay != null)
        {
            var c = _overlay.material.color;
            c.a = Mathf.Lerp(0.78f, 0f, _brightness);
            _overlay.material.color = c;
        }
    }

    private void SensePresence()
    {
        bool anyone = false;

        // 经理
        if (_manager != null && SameFloor(_manager.transform.position))
        {
            float dDoor = FlatDist(_manager.transform.position, transform.position);
            bool inside = InteriorContains(_manager.transform.position);
            if (dDoor < doorSenseRadius && !_targetOpen && !_swiping)
            {
                StartCoroutine(SwipeAndOpen()); // 经理要刷卡
            }
            // 人在房间格内任何位置都算在场——不会被关进黑屋
            if (dDoor < doorSenseRadius + 0.6f || inside) anyone = true;

            // 查错房：闯进入住中的房间内部（整格判定）
            if (inside && room != null
                && room.currentState == Room2DState.Occupied
                && Time.time >= _intrusionCooldownUntil)
            {
                _intrusionCooldownUntil = Time.time + 4f;
                WrongRoomDrama.Trigger(this, _manager);
            }
        }

        // 员工/客人：工卡/房卡直接开
        if (_spawner != null)
        {
            foreach (var a in _spawner.Agents)
            {
                if (a == null || !SameFloor(a.transform.position)) continue;
                if (FlatDist(a.transform.position, transform.position) < doorSenseRadius
                    || InteriorContains(a.transform.position))
                {
                    _targetOpen = true;
                    anyone = true;
                }
            }
        }
        foreach (var g in FindObjectsByType<GuestAgent>(FindObjectsSortMode.None))
        {
            if (g == null || !SameFloor(g.transform.position)) continue;
            if (FlatDist(g.transform.position, transform.position) < doorSenseRadius
                || InteriorContains(g.transform.position))
            {
                _targetOpen = true;
                anyone = true;
            }
        }

        if (anyone) _lastPresenceTime = Time.time;
        else if (_targetOpen && Time.time - _lastPresenceTime > autoCloseDelay) _targetOpen = false;
    }

    private System.Collections.IEnumerator SwipeAndOpen()
    {
        _swiping = true;
        var agent = _manager != null ? _manager.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
        if (agent != null) agent.isStopped = true; // 站定刷卡

        // 卡片划过动画（白色小方片沿门边下滑）
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

        if (agent != null) agent.isStopped = false;
        _targetOpen = true;
        _lastPresenceTime = Time.time;
        _swiping = false;
    }

    private bool SameFloor(Vector3 pos) =>
        FloorMath.FloorIndexForY(pos.y) == FloorMath.FloorIndexForY(transform.position.y);

    private static float FlatDist(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
