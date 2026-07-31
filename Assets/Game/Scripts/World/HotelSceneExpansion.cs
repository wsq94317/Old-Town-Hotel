using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds the derelict west guest wing before gameplay systems discover rooms.
/// The extension is runtime-only, so rebuilding navigation never overwrites the
/// checked-in NavMesh asset or hand-authored scene changes.
/// </summary>
public static class HotelSceneExpansion
{
    public const int ExpandedRoomCount = 24;
    public const int StartingDerelictRooms = 16;

    private const float WestEdge = -20f;
    private const float EastEdge = 10f;
    private const float ShellCenterX = -5f;
    private const float ShellWidth = 30f;
    private const float RoomRowZ = 4f;
    private const float DoorRowZ = 1.5f;
    private const float NorthSouthEdge = 6.5f;
    private const float ShellDepth = 13f;

    private readonly struct RoomSpec
    {
        public readonly int Number;
        public readonly int Floor;
        public readonly float X;
        public readonly float Z;

        public RoomSpec(int number, int floor, float x, float z)
        {
            Number = number;
            Floor = floor;
            X = x;
            Z = z;
        }
    }

    private static readonly RoomSpec[] AddedRooms =
    {
        new RoomSpec(209, 2, -12.5f, RoomRowZ),
        new RoomSpec(210, 2, -17.5f, RoomRowZ),
        new RoomSpec(211, 2, -12.5f, -RoomRowZ),
        new RoomSpec(212, 2, -17.5f, -RoomRowZ),
        new RoomSpec(305, 3, -12.5f, RoomRowZ),
        new RoomSpec(306, 3, -17.5f, RoomRowZ),
        new RoomSpec(307, 3, -17.5f, -RoomRowZ),
        new RoomSpec(308, 3, -12.5f, -RoomRowZ),
        new RoomSpec(309, 3, -7.5f, -RoomRowZ),
        new RoomSpec(310, 3, -2.5f, -RoomRowZ),
        new RoomSpec(311, 3, 2.5f, -RoomRowZ),
        new RoomSpec(312, 3, 7.5f, -RoomRowZ)
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        HotelSimSceneBridge bridge = UnityEngine.Object.FindFirstObjectByType<HotelSimSceneBridge>();
        if (bridge == null) return;

        Scene scene = SceneManager.GetActiveScene();
        Transform world = FindRoot(scene, "World");
        Transform roomData = FindRoot(scene, "RoomData");
        if (world == null || roomData == null) return;
        if (FindRoomEntity(312) != null) return;

        Transform floor2 = world.Find("Floor2");
        Transform floor3 = world.Find("Floor3");
        if (floor2 == null || floor3 == null) return;

        ExpandGuestFloorShell(floor2);
        ExpandGuestFloorShell(floor3);
        WidenExistingRoomData(roomData);

        for (int i = 0; i < AddedRooms.Length; i++)
            AddRoom(AddedRooms[i], floor2, floor3, roomData);

        RebindDoors(floor2);
        RebindDoors(floor3);
        bridge.SetDerelictRoomsAtStart(StartingDerelictRooms);

        ManagerCameraRig cameraRig = UnityEngine.Object.FindFirstObjectByType<ManagerCameraRig>();
        if (cameraRig != null)
            cameraRig.SetWorldBounds(new Vector2(-18.5f, -5.2f), new Vector2(8.5f, 5.2f));

        RefreshRoomConsumers();
        RebuildRuntimeNavigation(world);
        Debug.Log(
            $"[HotelExpansion] Guest wings expanded to {ExpandedRoomCount} rooms. " +
            $"Opening stock remains 8 rooms; {StartingDerelictRooms} require reclamation.");
    }

    private static void AddRoom(
        RoomSpec spec,
        Transform floor2,
        Transform floor3,
        Transform roomData)
    {
        Transform floor = spec.Floor == 2 ? floor2 : floor3;
        bool north = spec.Z > 0f;

        Transform visualSource = north
            ? floor.Find(spec.Floor == 2 ? "Room_201" : "Room_301")
            : floor2.Find("Room_205");
        Room2DEntity entitySource = FindRoomEntity(spec.Floor == 2 ? 201 : 301);
        Transform doorSource = north
            ? floor.Find(spec.Floor == 2 ? "Door_201" : "Door_301")
            : floor2.Find("Door_205");
        if (visualSource == null || entitySource == null || doorSource == null) return;

        GameObject entityObject = UnityEngine.Object.Instantiate(
            entitySource.gameObject, roomData, false);
        entityObject.name = "Room_" + spec.Number + "_Anchor";
        entityObject.transform.position = new Vector3(
            spec.X,
            FloorMath.BaseYFor(spec.Floor - 1),
            spec.Z);
        Room2DEntity entity = entityObject.GetComponent<Room2DEntity>();
        entity.SetIdentity(spec.Floor, spec.Number);
        entity.SetState(Room2DState.Ready);

        GameObject roomObject = UnityEngine.Object.Instantiate(
            visualSource.gameObject, floor, false);
        roomObject.name = "Room_" + spec.Number;
        roomObject.transform.localPosition = new Vector3(spec.X, 0f, spec.Z);
        RoomSceneBinder binder = roomObject.GetComponent<RoomSceneBinder>();
        if (binder != null) binder.ConfigureRoomNumber(spec.Number);
        RoomFurnitureView furniture = roomObject.GetComponent<RoomFurnitureView>();
        if (furniture != null) furniture.ConfigureRoomNumber(spec.Number);

        GameObject doorObject = UnityEngine.Object.Instantiate(
            doorSource.gameObject, floor, false);
        doorObject.name = "Door_" + spec.Number;
        doorObject.transform.localPosition = new Vector3(
            spec.X, 0f, north ? DoorRowZ : -DoorRowZ);
        RoomDoor door = doorObject.GetComponent<RoomDoor>();
        if (door != null) door.Configure(entity, roomObject.transform.position);

        AddRoomDecor(floor, spec.Number, spec.X, north);
    }

    private static void ExpandGuestFloorShell(Transform floor)
    {
        SetLocalXAndWidth(floor.Find("Ground"), ShellCenterX, ShellWidth);
        SetLocalZAndDepth(floor.Find("Ground"), 0f, ShellDepth);
        SetLocalXAndWidth(floor.Find("Wall_N"), ShellCenterX, ShellWidth);
        SetLocalXAndWidth(floor.Find("Wall_S"), ShellCenterX, ShellWidth);
        SetLocalZ(floor.Find("Wall_N"), NorthSouthEdge);
        SetLocalZ(floor.Find("Wall_S"), -NorthSouthEdge);
        SetLocalX(floor.Find("Wall_W"), WestEdge);
        SetLocalX(floor.Find("Wall_E"), EastEdge);
        SetLocalZAndDepth(floor.Find("Wall_W"), 0f, ShellDepth);
        SetLocalZAndDepth(floor.Find("Wall_E"), 0f, ShellDepth);
        WidenExistingVisualRooms(floor);

        Transform support = floor.Find(
            floor.name == "Floor2" ? "Floor2HskSupportAnchor" : "Floor3HskSupportAnchor");
        if (support != null) SetLocalX(support, -18.3f);
        Transform facility = floor.Find("Facility_FloorSupport");
        if (facility != null) SetLocalX(facility, -18.3f);

        Transform art = floor.Find("ArtPass_LowPoly");
        if (art == null) return;

        SetLocalXAndWidth(art.Find("CutawayTrim_S"), ShellCenterX, 29.72f);
        SetLocalZ(art.Find("CutawayTrim_S"), -6.36f);
        SetLocalX(art.Find("CutawayTrim_W"), -19.86f);
        SetLocalZAndDepth(art.Find("CutawayTrim_W"), 0f, 12.7f);
        SetLocalXAndWidth(art.Find("CorridorRunner"), -5.35f, 27.2f);
        SetLocalZAndDepth(art.Find("CorridorRunner"), 0f, 2.22f);
        WidenExistingDecor(art);

        foreach (Transform child in art)
        {
            if (!child.name.StartsWith("BrassInlay", StringComparison.Ordinal)) continue;
            SetLocalZ(child, -6.43f);
        }

        Transform cart = art.Find("HousekeepingCart");
        if (cart != null) SetLocalX(cart, -18.65f);

        AddBrassInlay(art, -15.6f, "BrassInlay_W2");
        AddBrassInlay(art, -11.7f, "BrassInlay_W1");
    }

    private static void AddRoomDecor(Transform floor, int number, float x, bool north)
    {
        Transform art = floor.Find("ArtPass_LowPoly");
        if (art == null) return;

        Transform frameSource = FindFirstNamed(art, "Frame_");
        Transform lampSource = FindFirstNamed(art, "WallLamp");
        Transform plaqueSource = FindFirstNamed(art, "RoomPlaque");
        if (frameSource == null || lampSource == null || plaqueSource == null) return;

        Transform frame = UnityEngine.Object.Instantiate(frameSource, art, false);
        frame.name = "Frame_" + number;
        frame.localPosition = new Vector3(x, 0f, north ? DoorRowZ : -DoorRowZ);
        frame.localRotation = Quaternion.Euler(0f, north ? 0f : 180f, 0f);

        Transform lamp = UnityEngine.Object.Instantiate(lampSource, art, false);
        lamp.name = "WallLamp_" + number;
        lamp.localPosition = new Vector3(
            x + 1.65f, 0.82f, north ? DoorRowZ - 0.05f : -DoorRowZ + 0.05f);
        lamp.localRotation = Quaternion.Euler(0f, north ? 0f : 180f, 0f);

        Transform plaque = UnityEngine.Object.Instantiate(plaqueSource, art, false);
        plaque.name = "RoomPlaque_" + number;
        plaque.localPosition = new Vector3(
            x + 1.24f, 0f, north ? DoorRowZ - 0.06f : -DoorRowZ + 0.06f);
        plaque.localRotation = Quaternion.Euler(0f, north ? 0f : 180f, 0f);
    }

    private static void AddBrassInlay(Transform art, float x, string name)
    {
        if (art.Find(name) != null) return;
        Transform source = art.Find("BrassInlay_0");
        if (source == null) return;
        Transform clone = UnityEngine.Object.Instantiate(source, art, false);
        clone.name = name;
        SetLocalX(clone, x);
    }

    private static void WidenExistingVisualRooms(Transform floor)
    {
        foreach (Transform child in floor)
        {
            if (child.GetComponent<RoomSceneBinder>() != null)
            {
                SetLocalZ(child, child.localPosition.z >= 0f ? RoomRowZ : -RoomRowZ);
                continue;
            }

            if (child.GetComponent<RoomDoor>() != null)
                SetLocalZ(child, child.localPosition.z >= 0f ? DoorRowZ : -DoorRowZ);
        }
    }

    private static void WidenExistingRoomData(Transform roomData)
    {
        Room2DEntity[] rooms = roomData.GetComponentsInChildren<Room2DEntity>(true);
        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null || (room.floorNumber != 2 && room.floorNumber != 3)) continue;
            Vector3 position = room.transform.position;
            position.z = position.z >= 0f ? RoomRowZ : -RoomRowZ;
            room.transform.position = position;
        }
    }

    private static void RebindDoors(Transform floor)
    {
        RoomDoor[] doors = floor.GetComponentsInChildren<RoomDoor>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            RoomDoor door = doors[i];
            int number = RoomSceneBinder.ParseRoomNumber(door.name);
            Room2DEntity entity = FindRoomEntity(number);
            Transform room = floor.Find("Room_" + number);
            if (entity != null && room != null)
                door.Configure(entity, room.position);
        }
    }

    private static void WidenExistingDecor(Transform art)
    {
        foreach (Transform child in art)
        {
            if (child.name.StartsWith("Frame_", StringComparison.Ordinal))
                SetLocalZ(child, child.localPosition.z >= 0f ? DoorRowZ : -DoorRowZ);
            else if (child.name.StartsWith("WallLamp", StringComparison.Ordinal))
                SetLocalZ(child, child.localPosition.z >= 0f
                    ? DoorRowZ - 0.05f
                    : -DoorRowZ + 0.05f);
            else if (child.name.StartsWith("RoomPlaque", StringComparison.Ordinal))
                SetLocalZ(child, child.localPosition.z >= 0f
                    ? DoorRowZ - 0.06f
                    : -DoorRowZ + 0.06f);
        }
    }

    private static void RefreshRoomConsumers()
    {
        Room2DPrototypeDemandLoop demand =
            UnityEngine.Object.FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (demand != null) demand.FindRoomsInScene();

        Room2DOverview overview = UnityEngine.Object.FindFirstObjectByType<Room2DOverview>();
        if (overview != null) overview.FindRoomsInScene();
    }

    private static void RebuildRuntimeNavigation(Transform world)
    {
        NavMeshSurface surface = world.GetComponentInChildren<NavMeshSurface>(true);
        if (surface == null) return;

        var activeStates = new List<KeyValuePair<GameObject, bool>>();
        foreach (Transform child in world)
        {
            if (!child.name.StartsWith("Floor", StringComparison.Ordinal)) continue;
            activeStates.Add(new KeyValuePair<GameObject, bool>(
                child.gameObject, child.gameObject.activeSelf));
            child.gameObject.SetActive(true);
        }

        surface.BuildNavMesh();

        for (int i = 0; i < activeStates.Count; i++)
            activeStates[i].Key.SetActive(activeStates[i].Value);
    }

    private static Room2DEntity FindRoomEntity(int roomNumber)
    {
        Room2DEntity[] rooms = UnityEngine.Object.FindObjectsByType<Room2DEntity>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < rooms.Length; i++)
            if (rooms[i] != null && rooms[i].roomNumber == roomNumber)
                return rooms[i];
        return null;
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == name) return roots[i].transform;
        return null;
    }

    private static Transform FindFirstNamed(Transform parent, string prefix)
    {
        foreach (Transform child in parent)
            if (child.name.StartsWith(prefix, StringComparison.Ordinal))
                return child;
        return null;
    }

    private static void SetLocalX(Transform target, float x)
    {
        if (target == null) return;
        Vector3 position = target.localPosition;
        position.x = x;
        target.localPosition = position;
    }

    private static void SetLocalXAndWidth(Transform target, float x, float width)
    {
        if (target == null) return;
        SetLocalX(target, x);
        Vector3 scale = target.localScale;
        scale.x = width;
        target.localScale = scale;
    }

    private static void SetLocalZ(Transform target, float z)
    {
        if (target == null) return;
        Vector3 position = target.localPosition;
        position.z = z;
        target.localPosition = position;
    }

    private static void SetLocalZAndDepth(Transform target, float z, float depth)
    {
        if (target == null) return;
        SetLocalZ(target, z);
        Vector3 scale = target.localScale;
        scale.z = depth;
        target.localScale = scale;
    }
}
