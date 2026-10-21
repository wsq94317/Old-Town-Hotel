using UnityEngine;
using UnityEngine.AI;

// 纸片人视觉：quad 始终以 yaw 面向相机（保持直立，35° 俯角下观感正确），
// 按移动速度做上下 bob + 按移动方向水平翻转。
public class BillboardSprite : MonoBehaviour
{
    [SerializeField] private float bobAmplitude = 0.06f;
    [SerializeField] private float bobFrequency = 10f;

    private StaffAgent _staff;
    private Camera _cam;
    private NavMeshAgent _agent; // 在父级上；没有也能工作（静止纸片人）
    private Vector3 _baseLocalPos;
    private float _bobPhase;

    private void Awake()
    {
        _staff = GetComponentInParent<StaffAgent>();
        _cam = Camera.main;
        _agent = GetComponentInParent<NavMeshAgent>();
        _baseLocalPos = transform.localPosition;
    }

    private void Start() => _staff = GetComponentInParent<StaffAgent>();

    private void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            // 仅 yaw 对齐，保持直立。
            transform.rotation = Quaternion.Euler(0f, _cam.transform.eulerAngles.y, 0f);
        }

        float speed = _agent != null ? _agent.velocity.magnitude : 0f;
        if (speed > 0.1f)
        {
            _bobPhase += Time.deltaTime * bobFrequency;
            transform.localPosition = _baseLocalPos + Vector3.up * Mathf.Abs(Mathf.Sin(_bobPhase)) * bobAmplitude;

            // 按移动方向（相对相机右方向）水平翻转。
            if (_cam != null)
            {
                float side = Vector3.Dot(_agent.velocity, _cam.transform.right);
                if (Mathf.Abs(side) > 0.05f)
                {
                    Vector3 s = transform.localScale;
                    s.x = Mathf.Abs(s.x) * (side >= 0f ? 1f : -1f);
                    transform.localScale = s;
                }
            }
        }
        else
        {
            bool working = _staff != null && _staff.ActivityState == StaffActivityState.Working;
            float sway = working ? Mathf.Sin(Time.time * 9f) : 0f;
            transform.localPosition = _baseLocalPos + Vector3.up * (Mathf.Abs(sway) * .035f);
            if (working) transform.rotation *= Quaternion.Euler(0, 0, sway * 4f);
        }
    }
}
