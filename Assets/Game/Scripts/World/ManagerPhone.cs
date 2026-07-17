using System.Collections.Generic;
using UnityEngine;

// 经理手机（通知中心）：右下角手机按钮+未读角标 → 通知列表。
// 每条通知带 [GO]：同层直接走过去；跨层引导进电梯（到电梯口自动弹楼层面板）。
// 通知源（轮询+静态推送双通道）：前台客诉、每日事件、火警链（FireAlarmIncident 推送）。
public class ManagerPhone : MonoBehaviour
{
    public static ManagerPhone Instance { get; private set; }

    public struct Note
    {
        public string Id;
        public string Title;
        public Vector3 Anchor;
        public int Floor;
        public Color Tint;
    }

    private readonly List<Note> _notes = new List<Note>();

    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private ManagerController manager;
    [SerializeField] private DailyEventInteraction events;

    private string _hint = "";
    private float _hintUntil;

    public bool PanelOpen => false; // 通知常驻不折叠，无全屏面板（GO 按钮走独立热区）

    private void Awake()
    {
        Instance = this;
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
        if (events == null) events = FindFirstObjectByType<DailyEventInteraction>();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>外部推送通知（火警链等）。同 id 覆盖。</summary>
    public static void Push(string id, string title, Vector3 anchor, Color tint)
    {
        if (Instance == null) return;
        Instance._notes.RemoveAll(n => n.Id == id);
        Instance._notes.Add(new Note
        {
            Id = id,
            Title = title,
            Anchor = anchor,
            Floor = FloorMath.FloorIndexForY(anchor.y),
            Tint = tint
        });
    }

    /// <summary>外部撤销通知。</summary>
    public static void Resolve(string id)
    {
        if (Instance != null) Instance._notes.RemoveAll(n => n.Id == id);
    }

    private void Update()
    {
        // 轮询源①：前台客诉
        bool complaint = demandLoop != null && demandLoop.complaintWaitingForReassignment;
        bool hasComplaintNote = _notes.Exists(n => n.Id == "complaint");
        if (complaint && !hasComplaintNote)
            Push("complaint", "Trouble at reception — guest is FURIOUS", new Vector3(-0.9f, 0f, 2.2f), new Color(1f, 0.45f, 0.3f));
        else if (!complaint && hasComplaintNote)
            Resolve("complaint");

        // 轮询源②：每日事件
        bool hasEvent = events != null && events.HasActiveEvent;
        bool hasEventNote = _notes.Exists(n => n.Id == "event");
        if (hasEvent && !hasEventNote)
            Push("event", events.ActiveEventTitle, events.ActiveEventAnchor, new Color(0.8f, 0.4f, 0.95f));
        else if (!hasEvent && hasEventNote)
            Resolve("event");
        else if (hasEvent && hasEventNote)
        {
            // 事件切换时刷新标题
            var idx = _notes.FindIndex(n => n.Id == "event");
            var note = _notes[idx];
            if (note.Title != events.ActiveEventTitle)
            {
                Push("event", events.ActiveEventTitle, events.ActiveEventAnchor, new Color(0.8f, 0.4f, 0.95f));
            }
        }
    }

    private void Go(Note n)
    {
        if (manager == null) return;
        int mgrFloor = FloorMath.FloorIndexForY(manager.transform.position.y);
        if (mgrFloor == n.Floor)
        {
            manager.MoveTo(n.Anchor);
            Say("On the way.");
        }
        else if (ElevatorController.Instance != null)
        {
            manager.MoveTo(ElevatorController.Instance.CabWorldPos(mgrFloor));
            Say("Take the elevator to " + (n.Floor + 1) + "F.");
        }
    }

    private void Say(string msg) { _hint = msg; _hintUntil = Time.time + 3f; }

    private void OnGUI()
    {
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        if (Time.time < _hintUntil)
            GUI.Label(new Rect(w * 0.5f - 150, h - 132, 300, 22), _hint);

        // 通知不折叠：来了就自动挂在右侧（HIRE 下方），逐条带 GO。
        // GO 按钮各自登记热区（触屏通道）；行本身不吃点击。
        for (int i = 0; i < _notes.Count && i < 5; i++)
        {
            var n = _notes[i];
            float y = 76 + i * 40;
            GUI.Box(new Rect(w - 316, y, 306, 36), "");
            var old = GUI.color;
            GUI.color = n.Tint;
            GUI.Label(new Rect(w - 308, y + 2, 210, 32), (n.Floor + 1) + "F  " + n.Title);
            GUI.color = old;
            var goRect = new Rect(w - 92, y + 5, 76, 26);
            GuiInput.ReserveZone(goRect);
            if (GuiInput.Button(goRect, "GO"))
            {
                Go(n);
                break;
            }
        }
    }
}
