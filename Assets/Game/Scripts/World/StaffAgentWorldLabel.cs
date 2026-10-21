using TMPro;
using UnityEngine;

// World-space two-line label above each paper-doll staff block.
[DisallowMultipleComponent]
public sealed class StaffAgentWorldLabel : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.30f, 0f);
    [SerializeField] private float worldScale = 0.23f;
    [SerializeField] private float referenceDistance = 18f;
    [SerializeField] private Vector2 distanceScaleRange = new Vector2(1.0f, 1.6f);

    private Camera _camera;
    private TextMeshPro _text;

    public void SetLabel(string text, Color color)
    {
        EnsureText();
        _text.text = text ?? string.Empty;
        _text.color = color;
    }

    private void Awake()
    {
        EnsureText();
    }

    private void LateUpdate()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        var agent = GetComponentInParent<StaffAgent>();
        if (agent != null && agent.Member != null)
        {
            string status = agent.ShiftState == StaffShiftState.LeavingMap ? "LEAVING"
                : agent.ShiftState == StaffShiftState.OffShift ? "OFF DUTY"
                : agent.ShiftState == StaffShiftState.Arriving ? "ARRIVING"
                : agent.IsSlacking ? "SLOW SERVICE"
                : agent.State == StaffAgent.AgentState.Working ? "WORKING" : "ON DUTY";
            string role = agent.Member.Role.ToString();
            if(GameText.UseChinese) {
                status=status=="LEAVING"?"离岗中":status=="OFF DUTY"?"休班":status=="ARRIVING"?"到岗中":status=="SLOW SERVICE"?"摸鱼中":status=="WORKING"?"工作中":"在岗";
                role=agent.Member.Role==StaffRole.Reception?"前台":agent.Member.Role==StaffRole.Housekeeper?"客房部":agent.Member.Role==StaffRole.Inspector?"验房主管":"经理";
            }
            string name = agent.Member.DisplayName;
            _text.text = (name == agent.Member.Role.ToString() ? role : name + " · " + role) + "\n" + status;
            _text.color = agent.ShiftState == StaffShiftState.LeavingMap ? Color.gray : Color.white;
        }
        transform.localPosition = localOffset;
        transform.localScale = Vector3.one * EffectiveScale();
        transform.rotation = Quaternion.Euler(0f, _camera.transform.eulerAngles.y, 0f);
    }

    private void EnsureText()
    {
        if (_text != null) return;

        _text = GetComponent<TextMeshPro>();
        if (_text == null) _text = gameObject.AddComponent<TextMeshPro>();

        transform.localPosition = localOffset;
        transform.localScale = Vector3.one * EffectiveScale();

        _text.alignment = TextAlignmentOptions.Center;
        _text.fontSize = 6.2f;
        _text.fontStyle = FontStyles.Bold;
        _text.lineSpacing = -6f;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.outlineWidth = 0.38f;
        _text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        _text.text = string.Empty;

        _text.font = WorldManagementHud.PresentationFont;
    }

    private float EffectiveScale()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return worldScale * distanceScaleRange.x;

        float distance = Vector3.Distance(_camera.transform.position, transform.position);
        float distanceFactor = Mathf.Max(0.01f, distance / Mathf.Max(0.01f, referenceDistance));
        float scaleFactor = Mathf.Clamp(distanceFactor, distanceScaleRange.x, distanceScaleRange.y);
        return worldScale * scaleFactor;
    }
}
