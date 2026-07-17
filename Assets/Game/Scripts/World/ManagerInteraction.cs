using UnityEngine;

// M3 经理交互（OnGUI 临时面板，正式 UI 统一在 M6）：
//   现场抓包（StaffAgent.OnAnyCaught）→ 弹 督促/训斥/无视 三选一
//   点击员工 → 近距离弹面板：催一催 / 质询(有🐌时) / 关闭
//   靠近带瑕疵的房间 → 底部出现"打回重扫"按钮
public class ManagerInteraction : MonoBehaviour
{
    [SerializeField] private ManagerController manager;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;

    private StaffAgent _caughtAgent;      // 现场抓包决策中
    private StaffAgent _panelAgent;       // 点击打开的员工面板
    private string _lastMessage = "";
    private float _messageUntil;

    /// <summary>有决策面板打开（OnGUI 不走 EventSystem，WorldInputController 靠它拦截穿透点击）。</summary>
    public bool PanelOpen => _caughtAgent != null || _panelAgent != null;

    // ── 指挥模式：面板选"指挥"后，下一次世界点击=指定房间插队 ────────────────
    private StaffAgent _commandAgent;
    private TaskDispatcher _dispatcher;

    public bool InCommandMode => _commandAgent != null;

    /// <summary>指挥模式下的世界点击：就近找房（3m 内）强制指派。</summary>
    public void CommandTarget(Vector3 worldPoint)
    {
        var agent = _commandAgent;
        _commandAgent = null;
        if (agent == null || demandLoop == null || demandLoop.rooms == null) return;
        if (_dispatcher == null) _dispatcher = FindFirstObjectByType<TaskDispatcher>();
        if (_dispatcher == null) return;

        Room2DEntity best = null;
        float bestDist = 3f;
        foreach (var r in demandLoop.rooms)
        {
            if (r == null) continue;
            Vector3 d = r.transform.position - worldPoint;
            d.y = 0f;
            if (d.magnitude < bestDist) { bestDist = d.magnitude; best = r; }
        }
        if (best == null) { Say("No room there."); return; }
        Say(_dispatcher.ForceAssign(agent, best)
            ? $"{agent.Member?.DisplayName} → Room {best.roomNumber}. Chop chop."
            : $"Room {best.roomNumber} doesn't need {agent.Member?.Role} right now.");
    }

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
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
        Vector2 v = GuiScale.Begin();
        float w = v.x;
        float h = v.y;

        // 提示消息
        if (Time.time < _messageUntil)
        {
            GUI.Label(new Rect(w * 0.5f - 200, h * 0.15f, 400, 30), _lastMessage, CenterStyle());
        }

        // ① 现场抓包三选一
        // 注意：按钮动作会把共享字段置空——全部先取局部变量 + else-if 链，
        // 否则同一 OnGUI pass 后续代码读到 null 直接把整个回调炸掉（按钮全灭）。
        if (_caughtAgent != null)
        {
            var caught = _caughtAgent;
            GUI.Box(new Rect(w * 0.5f - 170, h * 0.35f, 340, 130),
                $"CAUGHT SLACKING!  {caught.Member?.DisplayName} ({caught.Member?.Role})");
            if (GuiInput.Button(new Rect(w * 0.5f - 150, h * 0.35f + 40, 300, 24), "Urge back to work (morale -5, speed up)"))
                ResolveCatch(caught, CatchChoice.Urge, "Back to work!");
            else if (GuiInput.Button(new Rect(w * 0.5f - 150, h * 0.35f + 68, 300, 24), "Scold hard (morale -15, faster)"))
                ResolveCatch(caught, CatchChoice.Scold, "Scolded.");
            else if (GuiInput.Button(new Rect(w * 0.5f - 150, h * 0.35f + 96, 300, 24), "Look away (morale +3, slacking spreads)"))
                ResolveCatch(caught, CatchChoice.Ignore, "You saw nothing.");
            return;
        }

        // ② 员工面板
        // 按钮动作会把 _panelAgent 置空——先取局部变量 + else-if 链，动作内绝不再读共享字段；
        // 否则同一 OnGUI pass 后续行读到 null 抛异常，整个回调中止 → 所有按钮失效。
        if (_panelAgent != null)
        {
            var agent = _panelAgent;
            var m = agent.Member;
            GUI.Box(new Rect(w * 0.5f - 150, h * 0.4f, 300, 150),
                $"{m?.DisplayName} ({m?.Role})  morale:{m?.Morale}" + (agent.IsGrudging ? "  💢 GRUDGING" : ""));

            bool canInterrogate = agent.HasDelayMark;
            if (GuiInput.Button(new Rect(w * 0.5f - 130, h * 0.4f + 34, 260, 24), "Hurry up! (speed up, morale -3)"))
            {
                agent.Hurry();
                Say("Hurried.");
                _panelAgent = null;
            }
            else if (InterrogateButton(new Rect(w * 0.5f - 130, h * 0.4f + 62, 260, 24), canInterrogate))
            {
                _panelAgent = null;
                if (agent.Interrogate() == InterrogationVerdict.Caught) _caughtAgent = agent; // 坐实→三选一
                else Say($"WRONG ACCUSATION! {agent.Member?.DisplayName} is furious (morale {SupervisionTuning.WrongAccusationMoraleDelta}).");
            }
            else if (GuiInput.Button(new Rect(w * 0.5f - 130, h * 0.4f + 90, 260, 24), "Command → tap a room"))
            {
                _commandAgent = agent;
                _panelAgent = null;
                Say("Now tap the room you want them on.");
            }
            else if (GuiInput.Button(new Rect(w * 0.5f - 130, h * 0.4f + 118, 125, 24), "FIRE THEM"))
            {
                _panelAgent = null;
                if (economy != null && agent.Member != null)
                {
                    FloatingTextFx.Spawn(agent.transform.position, "FIRED!", new Color(1f, 0.25f, 0.2f), 1.2f);
                    economy.FireStaff(agent.Member); // Spawner 经 OnFired 收走纸片人（含走人演出）
                    Say($"{agent.Member.DisplayName} is packing. The stapler goes with them.");
                }
            }
            else if (GuiInput.Button(new Rect(w * 0.5f + 5, h * 0.4f + 118, 125, 24), "Close"))
            {
                _panelAgent = null;
            }
            return;
        }

        // ③ 附近瑕疵房 → 打回重扫（常驻按钮：登记热区吃触屏点击）
        var flawed = FlawedRoomNearby();
        if (flawed != null)
        {
            var flawRect = new Rect(w * 0.5f - 150, h - 90, 300, 30);
            GuiInput.ReserveZone(flawRect);
            if (GuiInput.Button(flawRect,
                $"Room {flawed.roomNumber}: flaw found — send back to cleaning"))
            {
                RoomFlaw.Clear(flawed);
                flawed.SetState(Room2DState.Dirty);
                Say($"Room {flawed.roomNumber} sent back to cleaning.");
            }
        }
    }

    // 抓包决策落地（幂等：同一 agent 只结算一次，防双通道同帧双触发）。
    private void ResolveCatch(StaffAgent agent, CatchChoice choice, string message)
    {
        if (agent == null || _caughtAgent != agent) return;
        _caughtAgent = null;
        agent.ApplyCatchChoice(choice);
        Say(message);
    }

    // 质询按钮：禁用态只画不响应（GuiInput.Button 已尊重 GUI.enabled）。
    private static bool InterrogateButton(Rect r, bool enabled)
    {
        bool prev = GUI.enabled;
        GUI.enabled = enabled;
        bool clicked = GuiInput.Button(r, enabled ? "Interrogate the delay (🐌)" : "Interrogate (no delay mark)");
        GUI.enabled = prev;
        return clicked;
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
