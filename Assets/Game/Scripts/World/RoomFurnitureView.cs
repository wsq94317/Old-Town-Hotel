using System.Collections.Generic;
using UnityEngine;

// 把账上的家具**摆到房间里**（玩家选的方向："家具实体化"）。
//
// 之前每间房只有一个手摆的 `Bed` 方块，而账上其实有 2-6 件家具（床、卫浴、电视、
// 沙发、书桌、地毯、挂画）——玩家花钱买的东西**一件都看不见**，装修完只有数字变化。
// 这个组件按 FurnitureAnchors 的锚点生成灰盒，一件家具一个盒子。
//
// 三条硬规矩：
//   ① **不改任何模拟状态**。这里只读 FurnitureLedger（同 GuestLifeDirector 的规矩）。
//   ② **不加碰撞体**。房间已经有一个覆盖整间的点击盒（RoomSceneBinder），
//      家具再加碰撞体会把点击抢走，玩家点房间反而选不中。
//   ③ **南北两排房是镜像的**：走廊在北排的 -z、南排的 +z，锚点布局要跟着翻。
//      单一布局对三分之一的房间是错的（并行审计点名，做状态灯时也独立踩到）。
[DisallowMultipleComponent]
public class RoomFurnitureView : MonoBehaviour
{
    [SerializeField] private int roomNumber;

    private readonly Dictionary<int, GameObject> _boxesByInstance = new Dictionary<int, GameObject>();
    private readonly List<int> _scratchGone = new List<int>();
    private Transform _root;
    private float _refreshTimer;
    private bool _doorOnPositiveZ;

    private static readonly Dictionary<int, Material> _materialsByKind = new Dictionary<int, Material>();

    private void OnEnable()
    {
        if (roomNumber <= 0) roomNumber = RoomSceneBinder.ParseRoomNumber(gameObject.name);
        // 门在哪一侧：北排房（本地 z>0）的走廊在 -z，南排相反
        _doorOnPositiveZ = transform.localPosition.z < 0f;
        EnsureRoot();
        _refreshTimer = 0f;
    }

    private void EnsureRoot()
    {
        Transform existing = transform.Find("Furniture");
        if (existing == null)
        {
            var go = new GameObject("Furniture");
            go.transform.SetParent(transform, worldPositionStays: false);
            existing = go.transform;
        }
        _root = existing;
    }

    private void Update()
    {
        var bridge = HotelSimSceneBridge.Instance;
        if (bridge == null || bridge.Sim == null || roomNumber <= 0) return;
        if (bridge.AwaitingMorningReport) return;

        // 家具的变化是分钟级的（买/坏/修好/装修换新），0.5 秒一次够了
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = 0.5f;

        Sync(bridge.Sim);
    }

    private void Sync(HotelSim sim)
    {
        var items = sim.Furniture.InRoom(roomNumber);

        // 账上有的、场上没有 → 生成
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!_boxesByInstance.TryGetValue(item.instanceId, out GameObject box) || box == null)
            {
                box = BuildBox(item);
                if (box == null) continue;
                _boxesByInstance[item.instanceId] = box;
            }
            UpdateBox(box, item);
        }

        // 场上有的、账上没有 → 删掉（卖了/装修换新了）
        _scratchGone.Clear();
        foreach (var pair in _boxesByInstance)
        {
            bool stillThere = false;
            for (int i = 0; i < items.Count; i++)
                if (items[i].instanceId == pair.Key) { stillThere = true; break; }
            if (!stillThere) _scratchGone.Add(pair.Key);
        }
        for (int i = 0; i < _scratchGone.Count; i++)
        {
            if (_boxesByInstance.TryGetValue(_scratchGone[i], out GameObject box) && box != null)
                Destroy(box);
            _boxesByInstance.Remove(_scratchGone[i]);
        }
    }

    private GameObject BuildBox(FurnitureInstance item)
    {
        if (!FurnitureCatalog.TryGet(item.kindId, out FurnitureKind kind)) return null;

        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = kind.name;
        Destroy(box.GetComponent<Collider>());      // 规矩 ②：别抢房间的点击
        box.transform.SetParent(_root, worldPositionStays: false);
        box.GetComponent<Renderer>().sharedMaterial = MaterialFor(kind);
        return box;
    }

    private void UpdateBox(GameObject box, FurnitureInstance item)
    {
        if (!FurnitureCatalog.TryGet(item.kindId, out FurnitureKind kind)) return;

        Vector3 size = SizeOf(kind.slot);
        float x = item.posX, z = item.posY;

        // 锚点是权威；posX/posY 只是它的缓存。锚点查不到（旧档补派失败）就
        // 退到房间中心而不是原点——原点在墙里，看着像穿模
        if (FurnitureAnchors.TryGet(item.anchorId, _doorOnPositiveZ, out FurnitureAnchor anchor))
        {
            x = anchor.localX;
            z = anchor.localZ;
        }

        box.transform.localPosition = new Vector3(x, size.y * 0.5f, z);
        box.transform.localScale = size;

        // 坏了的家具要看得出来：塌了压扁，坏了歪一下，糊了胶带的也歪着
        if (item.wrecked)
        {
            box.transform.localScale = new Vector3(size.x, size.y * 0.25f, size.z);
            box.transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
        }
        else if (item.IsFaulted)
        {
            box.transform.localRotation = Quaternion.Euler(0f, 0f, item.taped ? 3f : 8f);
        }
        else
        {
            box.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>各位置的体量。**只是灰盒**，但比例要对得上——
    /// 床该占掉一格的大半，挂画该薄得像挂在墙上。</summary>
    private static Vector3 SizeOf(FurnitureSlot slot)
    {
        switch (slot)
        {
            case FurnitureSlot.Bed: return new Vector3(1.30f, 0.45f, 1.00f);
            case FurnitureSlot.Bathroom: return new Vector3(1.10f, 1.05f, 1.10f);
            case FurnitureSlot.Entertainment: return new Vector3(1.05f, 0.62f, 0.14f);
            case FurnitureSlot.Seating: return new Vector3(1.10f, 0.42f, 0.62f);
            case FurnitureSlot.Desk: return new Vector3(1.00f, 0.62f, 0.46f);
            case FurnitureSlot.Floor: return new Vector3(1.40f, 0.04f, 1.40f);
            default: return new Vector3(0.75f, 0.55f, 0.08f);   // 挂画：薄片
        }
    }

    /// <summary>一种家具一份材质（静态共享，12 间房不会各建一份）。
    /// 配色跟着美化那一版的暖旧调子走，不要再造一套。</summary>
    private static Material MaterialFor(FurnitureKind kind)
    {
        if (_materialsByKind.TryGetValue(kind.kindId, out Material cached) && cached != null)
            return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        var material = shader != null ? new Material(shader) : null;
        if (material != null)
        {
            material.color = ColorFor(kind.slot);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
        }
        _materialsByKind[kind.kindId] = material;
        return material;
    }

    private static Color ColorFor(FurnitureSlot slot)
    {
        switch (slot)
        {
            case FurnitureSlot.Bed: return new Color(0.72f, 0.68f, 0.60f);        // 床品：米白
            case FurnitureSlot.Bathroom: return new Color(0.78f, 0.80f, 0.80f);   // 瓷白
            case FurnitureSlot.Entertainment: return new Color(0.16f, 0.16f, 0.18f); // 黑屏
            case FurnitureSlot.Seating: return new Color(0.42f, 0.34f, 0.40f);    // 暗紫沙发
            case FurnitureSlot.Desk: return new Color(0.46f, 0.34f, 0.24f);       // 木色
            case FurnitureSlot.Floor: return new Color(0.48f, 0.28f, 0.26f);      // 暗红地毯
            default: return new Color(0.55f, 0.50f, 0.38f);                       // 画框
        }
    }
}
