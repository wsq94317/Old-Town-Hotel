using UnityEngine;

// 日结演出：赚钱=金币雨+小震屏，亏钱=头顶乌云。附带结算板与搞笑评语。
// 纯表现层；数据全部来自 OnDaySettled 的 DayLedger。
public class DayEndCelebration : MonoBehaviour
{
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private ManagerController manager;

    private string _board = "";
    private float _boardUntil;

    private void Start()
    {
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
        if (dayController != null) dayController.OnDaySettled += HandleSettled;
    }

    private void OnDestroy()
    {
        if (dayController != null) dayController.OnDaySettled -= HandleSettled;
    }

    private void HandleSettled(int day, int served, DayLedger ledger)
    {
        Vector3 stage = manager != null ? manager.transform.position : Vector3.zero;
        if (ledger.Net > 0)
        {
            CoinRainFx.Play(stage, Mathf.Clamp(ledger.Net / 15, 8, 40));
            CameraShaker.Shake(0.1f, 0.5f);
            FloatingTextFx.Spawn(stage, "+$" + ledger.Net, new Color(1f, 0.85f, 0.2f), 1.5f);
        }
        else
        {
            GloomCloudFx.Play(stage);
            FloatingTextFx.Spawn(stage, ledger.Net == 0 ? "$0" : "-$" + (-ledger.Net), new Color(0.6f, 0.6f, 0.7f), 1.3f);
        }

        _board = $"DAY {day} COMPLETE — guests {served}\n"
               + $"income ${ledger.Income}   wages ${ledger.Wages}   interest ${ledger.Interest}\n"
               + $"NET {(ledger.Net >= 0 ? "+" : "")}{ledger.Net}$\n"
               + DayVerdictLogic.Line(ledger.Net);
        _boardUntil = Time.time + 6f;
    }

    private void OnGUI()
    {
        if (Time.time >= _boardUntil) return;
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;
        GUI.Box(new Rect(w * 0.5f - 210, h * 0.55f, 420, 86), _board);
    }
}

// 金币雨（占位）：金色小方片从天而降旋转坠落。
public class CoinRainFx : MonoBehaviour
{
    private const float Lifetime = 2.6f;
    private float _t;
    private Transform[] _coins;
    private float[] _fallSpeeds;

    public static void Play(Vector3 center, int coinCount)
    {
        var go = new GameObject("CoinRain");
        go.transform.position = center;
        var fx = go.AddComponent<CoinRainFx>();
        fx.Build(coinCount);
    }

    private void Build(int count)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
        { color = new Color(1f, 0.84f, 0.15f) };
        _coins = new Transform[count];
        _fallSpeeds = new float[count];
        for (int i = 0; i < count; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform);
            quad.transform.localPosition = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(4f, 7f), Random.Range(-1.5f, 1.5f));
            quad.transform.localScale = Vector3.one * 0.22f;
            quad.transform.localRotation = Random.rotation;
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            _coins[i] = quad.transform;
            _fallSpeeds[i] = Random.Range(2.2f, 4.2f);
        }
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_t >= Lifetime) { Destroy(gameObject); return; }
        for (int i = 0; i < _coins.Length; i++)
        {
            if (_coins[i] == null) continue;
            _coins[i].localPosition += Vector3.down * (_fallSpeeds[i] * Time.deltaTime);
            _coins[i].Rotate(0f, 540f * Time.deltaTime, 120f * Time.deltaTime);
            if (_coins[i].localPosition.y < 0.05f) _coins[i].localPosition = new Vector3(_coins[i].localPosition.x, 0.05f, _coins[i].localPosition.z);
        }
    }
}

// 亏钱乌云（占位）：头顶一团深灰方片缓慢盘旋 3 秒。
public class GloomCloudFx : MonoBehaviour
{
    private const float Lifetime = 3f;
    private float _t;

    public static void Play(Vector3 center)
    {
        var go = new GameObject("GloomCloud");
        go.transform.position = center + Vector3.up * 2.6f;
        var fx = go.AddComponent<GloomCloudFx>();
        fx.Build();
    }

    private void Build()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
        { color = new Color(0.25f, 0.25f, 0.3f) };
        for (int i = 0; i < 7; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform);
            quad.transform.localPosition = new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(-0.15f, 0.15f), Random.Range(-0.3f, 0.3f));
            quad.transform.localScale = Vector3.one * Random.Range(0.4f, 0.8f);
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            quad.AddComponent<BillboardSprite>();
        }
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_t >= Lifetime) { Destroy(gameObject); return; }
        transform.Rotate(0f, 40f * Time.deltaTime, 0f);
        transform.position += Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.002f);
    }
}
