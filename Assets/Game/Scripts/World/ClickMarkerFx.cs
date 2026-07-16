using UnityEngine;

// 点击落点的绿色占位标记（LoL 式移动指令标记的简化版）：
// 地面绿方片，放大→收缩 + 轻微上浮，0.6 秒后自动销毁。
// 正式美术到位后换成箭头贴图/动画，只需要改这里。
public class ClickMarkerFx : MonoBehaviour
{
    private const float Lifetime = 0.6f;
    private static Material _sharedMat;

    private float _t;

    public static void Spawn(Vector3 worldPos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "ClickMarker";
        Destroy(go.GetComponent<Collider>()); // 不挡射线
        go.transform.position = worldPos + Vector3.up * 0.03f;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 平躺地面
        if (_sharedMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            _sharedMat = new Material(shader) { color = new Color(0.2f, 0.9f, 0.3f, 1f) };
        }
        go.GetComponent<Renderer>().sharedMaterial = _sharedMat;
        go.AddComponent<ClickMarkerFx>();
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float k = _t / Lifetime;
        if (k >= 1f) { Destroy(gameObject); return; }

        // 先大后小 + 轻微上浮。
        float scale = Mathf.Lerp(1.2f, 0.15f, k);
        transform.localScale = Vector3.one * 0.9f * scale;
        transform.position += Vector3.up * (Time.deltaTime * 0.3f);
    }
}
