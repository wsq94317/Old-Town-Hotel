using UnityEngine;

// 世界空间飘字（JuiceKit）：上浮 + 渐隐 + billboard，1.1 秒自毁。
// 用内置 TextMesh（无 TMP 依赖），美术后期可整体替换。
public class FloatingTextFx : MonoBehaviour
{
    private const float Lifetime = 1.1f;
    private const float RiseSpeed = 0.9f;

    private TextMesh _text;
    private Camera _cam;
    private float _t;
    private Color _color;

    public static void Spawn(Vector3 worldPos, string text, Color color, float scale = 1f)
    {
        var go = new GameObject("FloatText");
        go.transform.position = worldPos + Vector3.up * 1.6f;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 48;
        tm.characterSize = 0.045f * scale;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        var fx = go.AddComponent<FloatingTextFx>();
        fx._text = tm;
        fx._color = color;
        go.AddComponent<AgentFloorVisibility>(); // 酒吧/赌场收入飘字发生在别的楼层时不该看见
    }

    private void Awake() => _cam = Camera.main;

    private void Update()
    {
        _t += Time.deltaTime;
        if (_t >= Lifetime) { Destroy(gameObject); return; }

        transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);
        if (_cam != null)
            transform.rotation = Quaternion.Euler(35f, _cam.transform.eulerAngles.y, 0f); // 面向 45° 相机

        float alpha = 1f - Mathf.SmoothStep(0.55f, 1f, _t / Lifetime);
        _text.color = new Color(_color.r, _color.g, _color.b, alpha);
    }
}
