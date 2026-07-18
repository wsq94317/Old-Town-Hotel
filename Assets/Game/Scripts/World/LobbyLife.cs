using System.Collections.Generic;
using UnityEngine;

public class LobbyLife : MonoBehaviour
{
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private Vector3 rackBase = new Vector3(2.4f, 0f, 3.3f);

    private const int RackCapacity = 6;

    private readonly List<GameObject> _luggage = new List<GameObject>();
    private readonly List<GuestAgent> _loiterers = new List<GuestAgent>();
    private float _wanderTimer;
    private static Material _luggageMat;

    private static readonly Vector3[] LobbySpots =
    {
        new Vector3(-3f, 0f, 1f),
        new Vector3(3.5f, 0f, -2f),
        new Vector3(-5f, 0f, 1.5f),
        new Vector3(-7f, 0f, -2f),
        new Vector3(1.5f, 0f, 0f),
    };

    private void Start()
    {
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (_luggageMat == null)
        {
            _luggageMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.55f, 0.35f, 0.5f)
            };
        }
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

    private void UpdateLuggage()
    {
        if (demandLoop == null) return;

        int waiting = demandLoop.UpcomingQueueCount
                    + (demandLoop.activeDemandWaitingForManualAssignment ? 1 : 0);
        int want = Mathf.Min(waiting, RackCapacity + 2);

        while (_luggage.Count < want)
        {
            var luggage = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(luggage.GetComponent<Collider>());
            luggage.name = "Luggage";
            luggage.transform.SetParent(transform);

            int index = _luggage.Count;
            int row = (index % RackCapacity) / 3;
            int col = (index % RackCapacity) % 3;
            int layer = index / RackCapacity;

            luggage.transform.position = rackBase + new Vector3(col * 0.5f, 0.36f + layer * 0.56f, row * 0.54f);

            var renderer = luggage.GetComponent<Renderer>();
            if (!GeneratedPlaceholderArt.ApplyLuggageSprite(luggage.transform, renderer, index))
            {
                luggage.transform.localScale = new Vector3(0.35f, 0.38f, 0.42f);
                renderer.sharedMaterial = _luggageMat;
            }

            luggage.AddComponent<BillboardSprite>();
            luggage.AddComponent<AgentFloorVisibility>();
            _luggage.Add(luggage);
        }

        while (_luggage.Count > want)
        {
            var last = _luggage[_luggage.Count - 1];
            _luggage.RemoveAt(_luggage.Count - 1);
            if (last != null) Destroy(last);
        }
    }

    private void UpdateLoiterers()
    {
        if (demandLoop == null || demandLoop.rooms == null) return;

        int occupied = 0;
        foreach (var room in demandLoop.rooms)
        {
            if (room != null && room.currentState == Room2DState.Occupied)
                occupied++;
        }

        int want = Mathf.Min(3, occupied);

        _loiterers.RemoveAll(g => g == null);
        while (_loiterers.Count < want)
        {
            var guest = GuestAgent.Spawn(LobbySpots[Random.Range(0, LobbySpots.Length)], "loiterer");
            _loiterers.Add(guest);
        }

        while (_loiterers.Count > want)
        {
            var guest = _loiterers[_loiterers.Count - 1];
            _loiterers.RemoveAt(_loiterers.Count - 1);
            if (guest != null)
            {
                var leavingGuest = guest;
                leavingGuest.TravelTo(new Vector3(0f, 0f, -5.2f), () =>
                {
                    if (leavingGuest != null) Destroy(leavingGuest.gameObject);
                });
            }
        }

        if (_loiterers.Count > 0)
        {
            var mover = _loiterers[Random.Range(0, _loiterers.Count)];
            if (mover != null)
                mover.TravelTo(LobbySpots[Random.Range(0, LobbySpots.Length)], null);
        }
    }
}
