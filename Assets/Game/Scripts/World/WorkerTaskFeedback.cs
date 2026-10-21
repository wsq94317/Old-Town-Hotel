using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Room work must be visible as work, rather than an employee standing by a bed.
public sealed class WorkerTaskFeedback : MonoBehaviour
{
    private StaffAgent _staff;
    private Transform _board;
    private Image _fill;
    private TMP_Text _text;
    private FloorVisibilityController _floors;

    private void Start()
    {
        _staff = GetComponent<StaffAgent>();
        _floors = FindFirstObjectByType<FloorVisibilityController>();
        var go = new GameObject("LiveRoomWork", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false); go.transform.localPosition = Vector3.up * 2.4f;
        go.transform.localScale = Vector3.one * .009f;
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(200, 48);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        _board = go.transform;
        var text = new GameObject("Task", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(_board, false);
        _text = text.GetComponent<TextMeshProUGUI>();
        _text.font = WorldManagementHud.PresentationFont; _text.fontSize = 22;
        _text.alignment = TextAlignmentOptions.Center; _text.color = Color.white; _text.raycastTarget = false;
        Fit(_text.rectTransform, new Vector2(0,.25f), Vector2.one);
        var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(_board, false); track.GetComponent<Image>().color = new Color(.15f,.16f,.18f);
        track.GetComponent<Image>().raycastTarget = false;
        Fit((RectTransform)track.transform, Vector2.zero, new Vector2(1,.18f));
        var fill = new GameObject("WorkDone", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(track.transform,false);
        _fill = fill.GetComponent<Image>(); _fill.color = new Color(.85f,.68f,.3f); _fill.raycastTarget = false;
        Fit(_fill.rectTransform, Vector2.zero, Vector2.one);
    }

    private void LateUpdate()
    {
        if (_board == null || _staff == null) return;
        bool visible = _staff.AssignedRoomNumber > 0
            && (_floors == null || _staff.CurrentFloor == _floors.CurrentFloor);
        _board.gameObject.SetActive(visible);
        if (!visible) return;
        var cam = Camera.main; if (cam != null) _board.rotation = cam.transform.rotation;
        bool working = _staff.ActivityState == StaffActivityState.Working;
        string action = _staff.IsSlacking ? (GameText.UseChinese ? "偷懒中" : "SLACKING")
            : working ? (GameText.UseChinese ? "工作中" : "WORKING") : (GameText.UseChinese ? "前往" : "TO ROOM");
        _text.text = action + " " + _staff.AssignedRoomNumber + (working ? " · " + Mathf.RoundToInt(_staff.WorkProgress*100) + "%" : "");
        _fill.rectTransform.anchorMax = new Vector2(working ? _staff.WorkProgress : 0,1);
    }

    private static void Fit(RectTransform r, Vector2 min, Vector2 max)
    { r.anchorMin=min; r.anchorMax=max; r.offsetMin=r.offsetMax=Vector2.zero; }
}
