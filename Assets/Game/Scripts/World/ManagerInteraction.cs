using UnityEngine;

// M3 经理交互（OnGUI 临时面板，正式 UI 统一在 M6）：
//   现场抓包（StaffAgent.OnAnyCaught）→ 弹 督促/训斥/无视 三选一
//   点击员工 → 近距离弹面板：催一催 / 质询(有🐌时) / 关闭
//   靠近带瑕疵的房间 → 底部出现"打回重扫"按钮
public class ManagerInteraction : MonoBehaviour
{
    [SerializeField] private ManagerController manager;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;

    private StaffAgent _caughtAgent;      // 现场抓包决策中
    private StaffAgent _panelAgent;       // 点击打开的员工面板
    private string _lastMessage = "";
    private float _messageUntil;

    /// <summary>有决策面板打开（OnGUI 不走 EventSystem，WorldInputController 靠它拦截穿透点击）。</summary>
    public bool PanelOpen => _caughtAgent != null || _panelAgent != null;

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        StaffAgent.OnAnyCaught += HandleCaught;
    }

    private void OnDestroy() => StaffAgent.OnAnyCaught -= HandleCaught;

    private void HandleCaught(StaffAgent agent) => _caughtAgent = agent;

    /// <summary>WorldInputController 点到员工时调用。</summary>
    public void OnStaffTapped(StaffAgent agent)
    {
        if (agent == null || manager == null) return;
        Vector3 d = agent.transform.position - manager.transform.position;
        d.y = 0f;
        if (FloorMath.FloorIndexForY(agent.transform.position.y) == FloorMath.FloorIndexForY(manager.transform.position.y)
            && d.magnitude <= SupervisionTuning.CatchRadius + 1f)
        {
            _panelAgent = agent; // 够近：直接开面板
        }
        else
        {
            manager.MoveTo(agent.transform.position); // 太远：先走过去
        }
    }

    private void Say(string msg)
    {
        _lastMessage = msg;
        _messageUntil = Time.time + 3f;
    }

    private Room2DEntity FlawedRoomNearby()
    {
        if (manager == null || demandLoop == null || demandLoop.rooms == null) return null;
        int mgrFloor = FloorMath.FloorIndexForY(manager.transform.position.y);
        foreach (var r in demandLoop.rooms)
        {
            if (r == null || RoomFlaw.Get(r) == null) continue;
            if (FloorMath.FloorIndexForY(r.transform.position.y) != mgrFloor) continue;
            Vector3 d = r.transform.position - manager.transform.position;
            d.y = 0f;
            if (d.magnitude < 2.2f) return r;
        }
        return null;
    }

    private void OnGUI()
    {
        float w = Screen.width;
        float h = Screen.height;

        // 提示消息
        if (Time.time < _messageUntil)
        {
            GUI.Label(new Rect(w * 0.5f - 200, h * 0.15f, 400, 30), _lastMessage, CenterStyle());
        }

        // ① 现场抓包三选一
        if (_caughtAgent != null)
        {
            GUI.Box(new Rect(w * 0.5f - 170, h * 0.35f, 340, 130),
                $"CAUGHT SLACKING!  {_caughtAgent.Member?.DisplayName} ({_caughtAgent.Member?.Role})");
            if (GUI.Button(new Rect(w * 0.5f - 150, h * 0.35f + 40, 300, 24), "Urge back to work (morale -5, speed up)"))
            { _caughtAgent.ApplyCatchChoice(CatchChoice.Urge); Say("Back to work!"); _caughtAgent = null; }
            if (GUI.Button(new Rect(w * 0.5f - 150, h * 0.35f + 68, 300, 24), "Scold hard (morale -15, faster)"))
            { _caughtAgent.ApplyCatchChoice(CatchChoice.Scold); Say("Scolded."); _caughtAgent = null; }
            if (GUI.Button(new Rect(w * 0.5f - 150, h * 0.35f + 96, 300, 24), "Look away (morale +3, slacking spreads)"))
            { _caughtAgent.ApplyCatchChoice(CatchChoice.Ignore); Say("You saw nothing."); _caughtAgent = null; }
            return;
        }

        // ② 员工面板
        if (_panelAgent != null)
        {
            var m = _panelAgent.Member;
            GUI.Box(new Rect(w * 0.5f - 150, h * 0.4f, 300, 122),
                $"{m?.DisplayName} ({m?.Role})  morale:{m?.Morale}");
            if (GUI.Button(new Rect(w * 0.5f - 130, h * 0.4f + 34, 260, 24), "Hurry up! (speed up, morale -3)"))
            { _panelAgent.Hurry(); Say("Hurried."); _panelAgent = null; }
            bool canInterrogate = _panelAgent.HasDelayMark;
            GUI.enabled = canInterrogate;
            if (GUI.Button(new Rect(w * 0.5f - 130, h * 0.4f + 62, 260, 24),
                canInterrogate ? "Interrogate the delay (🐌)" : "Interrogate (no delay mark)"))
            {
                var agent = _panelAgent;
                _panelAgent = null;
                if (agent.Interrogate() == InterrogationVerdict.Caught) _caughtAgent = agent; // 坐实→三选一
                else Say($"WRONG ACCUSATION! {agent.Member?.DisplayName} is furious (morale {SupervisionTuning.WrongAccusationMoraleDelta}).");
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(w * 0.5f - 130, h * 0.4f + 90, 260, 24), "Close"))
                _panelAgent = null;
            return;
        }

        // ③ 附近瑕疵房 → 打回重扫
        var flawed = FlawedRoomNearby();
        if (flawed != null)
        {
            if (GUI.Button(new Rect(w * 0.5f - 150, h - 90, 300, 30),
                $"Room {flawed.roomNumber}: flaw found — send back to cleaning"))
            {
                RoomFlaw.Clear(flawed);
                flawed.SetState(Room2DState.Dirty);
                Say($"Room {flawed.roomNumber} sent back to cleaning.");
            }
        }
    }

    private GUIStyle _center;
    private GUIStyle CenterStyle()
    {
        if (_center == null)
        {
            _center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
        }
        return _center;
    }
}
