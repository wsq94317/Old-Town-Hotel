using UnityEngine;

// 打架尘云演出（占位）：一团灰色方片旋转乱飞 + 重震屏，1.4 秒散场。
public class FightFx : MonoBehaviour
{
    private const float Lifetime = 1.4f;
    private float _t;
    private Transform[] _bits;
    private Vector3[] _velocities;

    public static void Play(Vector3 center)
    {
        var go = new GameObject("FightCloud");
        go.transform.position = center + Vector3.up * 0.7f;
        var fx = go.AddComponent<FightFx>();
        fx.Build();
        CameraShaker.Shake(0.35f, 0.9f);
    }

    private void Build()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader) { color = new Color(0.75f, 0.72f, 0.68f) };
        int count = 9;
        _bits = new Transform[count];
        _velocities = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform);
            quad.transform.localPosition = Random.insideUnitSphere * 0.3f;
            quad.transform.localScale = Vector3.one * Random.Range(0.25f, 0.55f);
            quad.transform.localRotation = Random.rotation;
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            _bits[i] = quad.transform;
            _velocities[i] = Random.insideUnitSphere * 1.6f + Vector3.up * 0.5f;
        }
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_t >= Lifetime) { Destroy(gameObject); return; }
        float k = _t / Lifetime;
        for (int i = 0; i < _bits.Length; i++)
        {
            if (_bits[i] == null) continue;
            _bits[i].localPosition += _velocities[i] * Time.deltaTime;
            _bits[i].Rotate(360f * Time.deltaTime, 520f * Time.deltaTime, 0f);
            _bits[i].localScale = Vector3.one * Mathf.Lerp(0.45f, 0.05f, k);
        }
    }
}
