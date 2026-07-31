using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Materializes the furniture ledger inside a visible room.
/// The simulation remains authoritative; this component only creates and updates art.
/// </summary>
[DisallowMultipleComponent]
public class RoomFurnitureView : MonoBehaviour
{
    [SerializeField] private int roomNumber;

    private readonly Dictionary<int, GameObject> _modelsByInstance =
        new Dictionary<int, GameObject>();
    private readonly Dictionary<int, int> _kindByInstance =
        new Dictionary<int, int>();
    private readonly List<int> _scratchGone = new List<int>();

    private Transform _root;
    private float _refreshTimer;
    private bool _doorOnPositiveZ;

    public void ConfigureRoomNumber(int number)
    {
        roomNumber = number;
        _doorOnPositiveZ = transform.localPosition.z < 0f;
    }

    private void OnEnable()
    {
        if (roomNumber <= 0) roomNumber = RoomSceneBinder.ParseRoomNumber(gameObject.name);
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
            go.layer = LowPolyHotelKit.ArtLayer;
            go.transform.SetParent(transform, false);
            existing = go.transform;
        }
        _root = existing;
    }

    private void Update()
    {
        HotelSimSceneBridge bridge = HotelSimSceneBridge.Instance;
        if (bridge == null || bridge.Sim == null || roomNumber <= 0) return;
        if (bridge.AwaitingMorningReport) return;

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = 0.5f;
        Sync(bridge.Sim);
    }

    private void Sync(HotelSim sim)
    {
        IReadOnlyList<FurnitureInstance> items = sim.Furniture.InRoom(roomNumber);
        for (int i = 0; i < items.Count; i++)
        {
            FurnitureInstance item = items[i];
            bool missing = !_modelsByInstance.TryGetValue(item.instanceId, out GameObject model)
                           || model == null;
            bool changedKind = _kindByInstance.TryGetValue(item.instanceId, out int shownKind)
                               && shownKind != item.kindId;
            if (missing || changedKind)
            {
                if (model != null) Destroy(model);
                model = BuildModel(item);
                if (model == null) continue;
                _modelsByInstance[item.instanceId] = model;
                _kindByInstance[item.instanceId] = item.kindId;
            }
            UpdateModel(model, item);
        }

        _scratchGone.Clear();
        foreach (KeyValuePair<int, GameObject> pair in _modelsByInstance)
        {
            bool stillThere = false;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].instanceId != pair.Key) continue;
                stillThere = true;
                break;
            }
            if (!stillThere) _scratchGone.Add(pair.Key);
        }

        for (int i = 0; i < _scratchGone.Count; i++)
        {
            int instanceId = _scratchGone[i];
            if (_modelsByInstance.TryGetValue(instanceId, out GameObject model) && model != null)
                Destroy(model);
            _modelsByInstance.Remove(instanceId);
            _kindByInstance.Remove(instanceId);
        }
    }

    private GameObject BuildModel(FurnitureInstance item)
    {
        if (!FurnitureCatalog.TryGet(item.kindId, out FurnitureKind kind)) return null;
        GameObject model = LowPolyHotelKit.CreateFurniture(kind);
        model.name = kind.name + "_" + item.instanceId;
        model.transform.SetParent(_root, false);
        LowPolyHotelKit.SetLayerRecursively(model, LowPolyHotelKit.ArtLayer);
        LowPolyHotelKit.RemoveColliders(model);
        return model;
    }

    private void UpdateModel(GameObject model, FurnitureInstance item)
    {
        if (!FurnitureCatalog.TryGet(item.kindId, out FurnitureKind kind)) return;

        float x = item.posX;
        float z = item.posY;
        if (FurnitureAnchors.TryGet(item.anchorId, _doorOnPositiveZ, out FurnitureAnchor anchor))
        {
            x = anchor.localX;
            z = anchor.localZ;
        }

        model.transform.localPosition = new Vector3(x, 0f, z);

        float roomFacing = _doorOnPositiveZ ? 180f : 0f;
        float faultTilt = item.IsFaulted ? (item.taped ? 2.5f : 7f) : 0f;
        if (item.wrecked)
        {
            model.transform.localRotation = Quaternion.Euler(0f, roomFacing, 7f);
            model.transform.localScale = new Vector3(1f, 0.28f, 1f);
        }
        else
        {
            model.transform.localRotation = Quaternion.Euler(0f, roomFacing, faultTilt);
            model.transform.localScale = Vector3.one;
        }

        // Wall-mounted pieces sit above the floor; all other procedural models use floor-level origins.
        if (kind.slot == FurnitureSlot.Wall)
            model.transform.localPosition += Vector3.up * 0.10f;

        LowPolyHotelKit.ApplyFurnitureCondition(model, item);
    }
}
