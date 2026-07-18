using TMPro;
using UnityEngine;

// World-space two-line label above each paper-doll staff block.
[DisallowMultipleComponent]
public sealed class StaffAgentWorldLabel : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.45f, 0f);
    [SerializeField] private float worldScale = 0.28f;
    [SerializeField] private float referenceDistance = 18f;
    [SerializeField] private Vector2 distanceScaleRange = new Vector2(1.15f, 2.6f);

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
        _text.fontSize = 7.8f;
        _text.fontStyle = FontStyles.Bold;
        _text.lineSpacing = -6f;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.outlineWidth = 0.38f;
        _text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        _text.text = string.Empty;

        if (_text.font == null && TMP_Settings.defaultFontAsset != null)
            _text.font = TMP_Settings.defaultFontAsset;
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
