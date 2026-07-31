using UnityEngine;
using UnityEngine.SceneManagement;

public enum HotelBusinessKind
{
    Reception,
    Restaurant,
    Gym,
    Casino,
    Pool
}

[DisallowMultipleComponent]
public sealed class HotelBusinessHotspot : MonoBehaviour
{
    [SerializeField] private HotelBusinessKind kind;
    [SerializeField] private string displayName;

    public HotelBusinessKind Kind => kind;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? kind.ToString() : displayName;
    public Vector3 Anchor => transform.position;

    public void Configure(HotelBusinessKind targetKind, string targetName)
    {
        kind = targetKind;
        displayName = targetName;
    }

    public void Pulse()
    {
        FloatingTextFx.Spawn(
            Anchor + Vector3.up * 0.8f,
            DisplayName.ToUpperInvariant(),
            new Color(0.90f, 0.69f, 0.28f),
            1.15f);
    }
}

public static class HotelBusinessHotspotInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Object.FindFirstObjectByType<HotelSimSceneBridge>() == null) return;

        InstallOn("FrontDesk", HotelBusinessKind.Reception, "Reception");
        InstallOn("RestaurantBar", HotelBusinessKind.Restaurant, "Restaurant");
        InstallOn("WeightBench", HotelBusinessKind.Gym, "Gym");
        InstallOn("CasinoTable", HotelBusinessKind.Casino, "Casino");
        InstallOn("Pool", HotelBusinessKind.Pool, "Rooftop Pool");
    }

    private static void InstallOn(string objectName, HotelBusinessKind kind, string displayName)
    {
        Transform target = FindTransform(SceneManager.GetActiveScene(), objectName);
        if (target == null) return;

        HotelBusinessHotspot hotspot = target.GetComponent<HotelBusinessHotspot>();
        if (hotspot == null) hotspot = target.gameObject.AddComponent<HotelBusinessHotspot>();
        hotspot.Configure(kind, displayName);

        if (target.GetComponentInChildren<Collider>(true) != null) return;
        var collider = target.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.65f, 0f);
        collider.size = new Vector3(2.2f, 1.3f, 1.8f);
    }

    private static Transform FindTransform(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform result = FindTransform(roots[i].transform, objectName);
            if (result != null) return result;
        }

        return null;
    }

    private static Transform FindTransform(Transform root, string objectName)
    {
        if (root.name == objectName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindTransform(root.GetChild(i), objectName);
            if (result != null) return result;
        }

        return null;
    }
}
