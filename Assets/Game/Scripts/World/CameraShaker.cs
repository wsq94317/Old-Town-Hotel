using UnityEngine;

// 震屏（JuiceKit）：挂在 Main Camera 上，执行序排在 ManagerCameraRig 之后，
// 在 rig 定位完相机后叠加随机偏移——两者互不知晓，零耦合。
[DefaultExecutionOrder(200)]
public class CameraShaker : MonoBehaviour
{
    private static CameraShaker _instance;
    private float _amplitude;
    private float _until;

    /// <summary>触发震屏（幅度=世界单位，持续秒）。可叠加：取更强的一次。</summary>
    public static void Shake(float amplitude, float duration)
    {
        if (_instance == null) return;
        _instance._amplitude = Mathf.Max(_instance._amplitude, amplitude);
        _instance._until = Mathf.Max(_instance._until, Time.time + duration);
    }

    private void Awake() => _instance = this;
    private void OnDestroy() { if (_instance == this) _instance = null; }

    private void LateUpdate()
    {
        if (Time.time >= _until) { _amplitude = 0f; return; }
        // 衰减的随机抖动（rig 每帧重设位置，这里做纯叠加即可）
        float falloff = Mathf.Clamp01((_until - Time.time) / 0.4f);
        Vector2 r = Random.insideUnitCircle * _amplitude * falloff;
        transform.position += new Vector3(r.x, r.y * 0.6f, 0f);
    }
}
