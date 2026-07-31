using UnityEngine;
using Unity.AI.Navigation;

// 把**看得见的房间**和**账上的房间**缝在一起（场景基础架构第一块）。
//
// 场景原本是这样的：`World/Floor3/Room_301` 有墙、有床、有标签，是玩家看得见的东西；
// 而 `RoomData/Room_301_Anchor` 上挂着 Room2DEntity，是账上的东西。**两者互不相识**。
// 后果玩家已经反馈了——"场景里还没有玩家可以解锁和装修的房间"：
//   · 房间状态在世界里完全不可见（12 间房实测连 Renderer 都查不到，全在子物体上）
//   · 没有可点的目标，于是解锁/装修只能在抽屉页里做，和那栋楼没有关系
//   · 破败房（301-304）和"家具坏了封房"的 201/202/205 长得一模一样
//
// 这个组件挂在**可见的** Room_xxx 上，负责三件事：
//   ① 从物体名解析房号，向 Sim 要这间房的真实状态（Sim 是房态的唯一权威）
//   ② 按状态给墙和床上色 + 把状态写进头顶标签 —— 状态第一次变成看得见的东西
//   ③ 提供一个可点的碰撞体，点了就选中这间房（RoomSelection），
//      抽屉里的操作因此有了"对哪一间"的答案
//
// 刻意不做的事：这里**不改任何模拟状态**。上色和选中是纯表现层，
// 出 bug 也弄不坏存档（同 GuestLifeDirector 的规矩）。
[DisallowMultipleComponent]
public class RoomSceneBinder : MonoBehaviour
{
    /// <summary>房号。留空则从物体名 "Room_301" 里解析。</summary>
    [SerializeField] private int roomNumber;

    /// <summary>Doorway interaction area. The room floor remains available for movement taps.</summary>
    [SerializeField] private Vector3 tapBoxSize = new Vector3(3.2f, 2.4f, 1.1f);

    private TextMesh _label;
    private Renderer _stateChip;
    private Material _chipMaterial;
    private RoomSimState _shownState = (RoomSimState)(-1);
    private bool _shownSelected;
    private int _shownClearingPercent = -1;
    private float _refreshTimer;
    private float _pulseUntil;
    private Vector3 _chipBaseScale;

    public int RoomNumber => roomNumber;

    public void ConfigureRoomNumber(int number)
    {
        roomNumber = number;
        FindLabel();
        EnsureStateChip();
        EnsureTapCollider();
        _shownState = (RoomSimState)(-1);
    }

    private void OnEnable()
    {
        // 热重载自愈：这些引用全是运行时找的，域重载会清空（本项目定过的规矩）
        if (roomNumber <= 0) roomNumber = ParseRoomNumber(gameObject.name);
        FindLabel();
        EnsureStateChip();
        EnsureTapCollider();
        _shownState = (RoomSimState)(-1);   // 强制下一帧重刷
    }

    /// <summary>"Room_301" → 301。解析不出来就返回 0（组件安静地什么都不做）。</summary>
    public static int ParseRoomNumber(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return 0;
        int digits = 0, value = 0;
        for (int i = 0; i < objectName.Length; i++)
        {
            char c = objectName[i];
            if (c >= '0' && c <= '9') { value = value * 10 + (c - '0'); digits++; }
            else if (digits > 0) break;      // 只取第一段连续数字
        }
        return digits > 0 ? value : 0;
    }

    private void FindLabel()
    {
        _label = null;
        foreach (var text in GetComponentsInChildren<TextMesh>(includeInactive: true))
            if (text != null) { _label = text; break; }
    }

    /// <summary>房态灯：一块小方片，挂在**走廊那一侧的房门口外**。
    ///
    /// 第一版是直接给整间房的墙和床染色，玩家实测反馈"颜色有闪烁，根据玩家位置
    /// 不同颜色也有不同"——根因是 RoomDoor 把门口那块 Darkness 遮罩的透明度
    /// 按"门开或屋里有人"从 0.78 淡到 0：经理走近遮罩消失、染色的墙露出全彩，
    /// 他一走开同一面墙又变暗，淡入淡出的 0.8-1.2 秒就是那个闪烁。
    /// 而且染墙会把整栋楼变成彩虹（红绿黄一片）。
    ///
    /// 两条硬要求，这块小片同时满足：
    ///   ① **不能被遮罩覆盖**——所以它在房门外侧，不在房间内部；
    ///   ② **不能被光照改变**——所以用 Unlit。Lit 材质在阴影里会变色，
    ///      那又是一种"颜色随位置变"。</summary>
    private void EnsureStateChip()
    {
        Transform existing = transform.Find("StateChip");
        if (existing == null)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "StateChip";
            Collider chipCollider = quad.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(chipCollider);
            else DestroyImmediate(chipCollider);
            quad.transform.SetParent(transform, worldPositionStays: false);
            // 贴在走廊那一侧的地面上（房间 5×5，走廊边在本地 z=±2.5）。
            // 用房间自己的 z 符号决定朝哪边——205-208 的朝向是镜像的。
            float corridorSide = transform.localPosition.z >= 0f ? -1f : 1f;
            quad.transform.localPosition = new Vector3(0f, 0.06f, corridorSide * 2.15f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(2.6f, 0.55f, 1f);
            existing = quad.transform;
        }
        _stateChip = existing.GetComponent<Renderer>();
        _chipBaseScale = existing.localScale;

        if (_chipMaterial == null)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            _chipMaterial = unlit != null ? new Material(unlit) : null;
        }
        if (_stateChip != null && _chipMaterial != null) _stateChip.sharedMaterial = _chipMaterial;
    }

    /// <summary>
    /// Keeps room selection on a compact doorway strip instead of covering the
    /// full room floor, which must remain available for manager movement.
    /// </summary>
    private void EnsureTapCollider()
    {
        // Migrate the original room-sized click boxes without requiring a scene
        // reserialize. Only the doorway plaque is an interaction target now.
        tapBoxSize.x = Mathf.Min(tapBoxSize.x, 3.2f);
        tapBoxSize.y = Mathf.Max(tapBoxSize.y, 2.4f);
        tapBoxSize.z = Mathf.Min(tapBoxSize.z, 1.1f);

        var existing = GetComponent<BoxCollider>();
        if (existing == null) existing = gameObject.AddComponent<BoxCollider>();
        existing.size = tapBoxSize;
        float corridorSide = transform.localPosition.z >= 0f ? -1f : 1f;
        existing.center = new Vector3(0f, tapBoxSize.y * 0.5f, corridorSide * 2.05f);
        existing.isTrigger = false;

        var modifier = GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = gameObject.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
    }

    private void Update()
    {
        var bridge = HotelSimSceneBridge.Instance;
        if (bridge == null || bridge.Sim == null || roomNumber <= 0) return;
        TickPulse();
        if (bridge.AwaitingMorningReport) return;     // 晨报期间世界冻结

        // 每 0.25 秒够了：状态变化是分钟级的，逐帧刷是白烧手机电池
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = 0.25f;

        if (!bridge.Sim.Rooms.Contains(roomNumber)) return;
        RoomSimState state = bridge.Sim.Rooms.At(roomNumber).state;
        bool selected = RoomSelection.Selected == roomNumber;
        // 清理进度也要进变化检测，否则标签冻在 0%——指派了清理却看不到它在动，
        // 玩家会以为按钮没生效（实测抓到：进度已经 75%，头顶还写着 0%）
        int clearingPercent = bridge.Sim.Clearing.IsClearing(roomNumber)
            ? Mathf.RoundToInt(bridge.Sim.Clearing.ProgressOf(roomNumber) * 100f)
            : -1;
        if (state == _shownState && selected == _shownSelected
            && clearingPercent == _shownClearingPercent) return;

        RoomSimState previous = _shownState;
        _shownState = state;
        _shownSelected = selected;
        _shownClearingPercent = clearingPercent;
        Apply(state, selected, bridge.Sim);
        if (previous >= 0 && previous != state
            && (state == RoomSimState.Ready || state == RoomSimState.Occupied))
            Pulse();
    }

    public void Pulse(float duration = 1.1f)
    {
        _pulseUntil = Mathf.Max(_pulseUntil, Time.time + duration);
    }

    private void TickPulse()
    {
        if (_stateChip == null) return;
        float remaining = _pulseUntil - Time.time;
        if (remaining <= 0f)
        {
            _stateChip.transform.localScale = _chipBaseScale;
            return;
        }

        float scale = 1f + Mathf.Sin(Time.time * 16f) * 0.13f
                      + Mathf.Clamp01(remaining) * 0.08f;
        _stateChip.transform.localScale = _chipBaseScale * scale;
    }

    private void Apply(RoomSimState state, bool selected, HotelSim sim)
    {
        // **只染那块状态灯，绝不染墙**（见 EnsureStateChip 的注释：染墙会和
        // 门口遮罩的淡入淡出打架，表现为"颜色闪烁、随玩家位置变"）
        Color tint = RoomStatePalette.ColorOf(state);
        if (selected) tint = Color.Lerp(tint, Color.white, 0.5f);   // 选中的那间亮一档
        if (_chipMaterial != null) _chipMaterial.color = tint;

        if (_label != null)
            _label.text = roomNumber + "\n" + GameText.T(RoomStatePalette.WordOf(state))
                        + ClearingSuffix(sim);
    }

    /// <summary>正在清理的破败房要显示进度——玩家指派了清理之后必须看得见它在动。</summary>
    private string ClearingSuffix(HotelSim sim)
    {
        if (!sim.Clearing.IsClearing(roomNumber)) return "";
        return "\n" + sim.Clearing.ProgressOf(roomNumber).ToString("P0");
    }

    private void OnDestroy()
    {
        if (_chipMaterial == null) return;
        if (Application.isPlaying) Destroy(_chipMaterial);
        else DestroyImmediate(_chipMaterial);
    }
}

/// <summary>房态 → 颜色与词。**同一套配色在世界和 UI 里通用**，
/// 否则玩家要在脑子里维护两张对照表。</summary>
public static class RoomStatePalette
{
    public static Color ColorOf(RoomSimState state)
    {
        switch (state)
        {
            case RoomSimState.Ready: return new Color(0.62f, 0.78f, 0.60f);       // 干净可售：绿
            case RoomSimState.Occupied: return new Color(0.55f, 0.66f, 0.86f);    // 有人住：蓝
            case RoomSimState.Dirty: return new Color(0.80f, 0.68f, 0.42f);       // 待打扫：土黄
            case RoomSimState.Cleaning: return new Color(0.88f, 0.82f, 0.50f);    // 正在打扫：亮黄
            case RoomSimState.AwaitingInspection: return new Color(0.72f, 0.78f, 0.86f);
            case RoomSimState.Blocked: return new Color(0.78f, 0.42f, 0.38f);     // 家具坏了：红
            default: return new Color(0.34f, 0.31f, 0.30f);                       // 破败：几乎黑
        }
    }

    /// <summary>状态的人话（英文原文，走 GameText 翻译）。</summary>
    public static string WordOf(RoomSimState state)
    {
        switch (state)
        {
            case RoomSimState.Ready: return "READY";
            case RoomSimState.Occupied: return "IN USE";
            case RoomSimState.Dirty: return "DIRTY";
            case RoomSimState.Cleaning: return "CLEANING";
            case RoomSimState.AwaitingInspection: return "TO INSPECT";
            case RoomSimState.Blocked: return "BROKEN";
            default: return "DERELICT";
        }
    }
}

/// <summary>当前选中的房间。**一个静态房号**就够了——
/// 世界里点一下、抽屉里的操作就知道对哪一间下手，不需要事件总线。</summary>
public static class RoomSelection
{
    public static int Selected { get; private set; }

    public static void Select(int roomNumber) =>
        Selected = Selected == roomNumber ? 0 : roomNumber;   // 再点一下取消选中

    public static void Clear() => Selected = 0;
}
