using UnityEngine;

// Short-lived world marker for accepted and rejected movement commands.
public class ClickMarkerFx : MonoBehaviour
{
    private const float Lifetime = 0.6f;
    private static Material _acceptedMaterial;
    private static Material _rejectedMaterial;

    private float _t;

    public static void Spawn(Vector3 worldPos)
    {
        Spawn(worldPos, rejected: false);
    }

    public static void SpawnRejected(Vector3 worldPos)
    {
        Spawn(worldPos, rejected: true);
    }

    private static void Spawn(Vector3 worldPos, bool rejected)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = rejected ? "RejectedClickMarker" : "ClickMarker";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = worldPos + Vector3.up * 0.03f;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.GetComponent<Renderer>().sharedMaterial = MarkerMaterial(rejected);
        go.AddComponent<ClickMarkerFx>();
    }

    private static Material MarkerMaterial(bool rejected)
    {
        if (rejected)
        {
            if (_rejectedMaterial == null)
            {
                _rejectedMaterial = BuildMaterial(new Color(1f, 0.24f, 0.12f, 1f));
            }

            return _rejectedMaterial;
        }

        if (_acceptedMaterial == null)
        {
            _acceptedMaterial = BuildMaterial(new Color(0.2f, 0.9f, 0.3f, 1f));
        }

        return _acceptedMaterial;
    }

    private static Material BuildMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        return new Material(shader) { color = color };
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float progress = _t / Lifetime;
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float scale = Mathf.Lerp(1.2f, 0.15f, progress);
        transform.localScale = Vector3.one * 0.9f * scale;
        transform.position += Vector3.up * (Time.deltaTime * 0.3f);
    }
}
