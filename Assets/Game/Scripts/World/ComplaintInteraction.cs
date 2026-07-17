using UnityEngine;

// M4 客诉决策交互：客诉出现 → 前台生成愤怒客人（❗）→ 经理走近 → 三选一面板
// （赔钱/冷处理/打架🥊）→ 演出 + 效果结算 + 结果文案。
// 效果只走公开 API：TrySpend / prototypeSatisfactionScore / AdjustMorale /
// Clock.AdvanceGameHours / 复用打烊清场同款字段收口客诉状态。
public class ComplaintInteraction : MonoBehaviour
{
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private StaffAgentSpawner spawner;
    [SerializeField] private ManagerController manager;
    [SerializeField] private Vector3 complaintPoint = new Vector3(-0.9f, 0f, 2.2f); // 前台另一侧
    [SerializeField] private Vector3 doorPoint = new Vector3(0f, 0f, -5.2f);
    [SerializeField] private int rngSeed = 777;

    private System.Random _rng;
    private GuestAgent _angryGuest;
    private EmoteBubble _angryEmote;
    private bool _panelOpen;
    private string _story = "";
    private float _storyUntil;
    private GameObject _managerVisual;

    /// <summary>面板打开时 WorldInputController 吞掉世界点击。</summary>
    public bool PanelOpen => _panelOpen;

    private void Awake()
    {
        _rng = new System.Random(rngSeed);
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (spawner == null) spawner = FindFirstObjectByType<StaffAgentSpawner>();
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
    }

    private void Update()
    {
        if (demandLoop == null || manager == null) return;

        bool complaintActive = demandLoop.complaintWaitingForReassignment;

        // 客诉出现 → 生成愤怒客人
        if (complaintActive && _angryGuest == null)
        {
            _angryGuest = GuestAgent.Spawn(doorPoint, "angry");
            _angryGuest.TravelTo(complaintPoint, null);
            _angryEmote = EmoteBubble.Attach(_angryGuest.transform);
            _angryEmote.Show(EmoteBubble.Emote.Alert);
        }

        // 客诉被其他路径解决（如打烊清场）→ 客人离场
        if (!complaintActive && _angryGuest != null && !_panelOpen)
        {
            SendGuestHome();
        }

        // 经理走近愤怒客人 → 开面板
        if (complaintActive && _angryGuest != null && !_panelOpen)
        {
            Vector3 d = manager.transform.position - _angryGuest.transform.position;
            d.y = 0f;
            if (FloorMath.FloorIndexForY(manager.transform.position.y) == 0 && d.magnitude < 2.5f)
            {
                _panelOpen = true;
            }
        }
    }

    private void SendGuestHome()
    {
        var g = _angryGuest;
        _angryGuest = null;
        if (g != null) g.TravelTo(doorPoint, () => Destroy(g.gameObject));
    }

    private void Choose(ComplaintChoice choice)
    {
        _panelOpen = false;
        int rate = economy != null && economy.Config != null ? economy.Config.roomRevenuePerGuest : 80;
        var outcome = ComplaintDecisionLogic.Resolve(choice, rate, _rng.NextDouble());

        // 演出
        if (choice == ComplaintChoice.Fight && _angryGuest != null)
        {
            FightFx.Play(_angryGuest.transform.position);
            if (outcome.ManagerKnockedDown) StartCoroutine(KnockDownManager());
        }

        // 结算
        if (outcome.CashDelta < 0 && economy != null) economy.TrySpend(-outcome.CashDelta); // 破产付不起=白嫖，很合理
        if (demandLoop != null) demandLoop.prototypeSatisfactionScore += outcome.SatisfactionDelta;
        if (outcome.StaffMoraleDelta != 0 && spawner != null)
        {
            foreach (var a in spawner.Agents) a?.Member?.AdjustMorale(outcome.StaffMoraleDelta);
        }
        ManagerReputation.Add(outcome.PrestigeDelta);
        if (outcome.SkipGameHours > 0f && dayController != null)
        {
            dayController.Clock.AdvanceGameHours(outcome.SkipGameHours);
        }
        if (outcome.CashDelta != 0)
        {
            FloatingTextFx.Spawn(manager.transform.position, outcome.CashDelta.ToString("+#;-#") + "$",
                outcome.CashDelta < 0 ? new Color(1f, 0.4f, 0.3f) : new Color(0.35f, 0.95f, 0.4f));
        }

        // 收口客诉模拟状态（同打烊清场的字段语义）
        if (demandLoop != null)
        {
            demandLoop.complaintWaitingForReassignment = false;
            demandLoop.complaintReassignmentWaitSeconds = 0f;
            demandLoop.complaintStatus = "Handled by manager: " + choice;
        }

        _story = outcome.Story;
        _storyUntil = Time.time + 4.5f;
        SendGuestHome();
    }

    private System.Collections.IEnumerator KnockDownManager()
    {
        if (_managerVisual == null && manager != null)
            _managerVisual = manager.transform.Find("Visual") != null ? manager.transform.Find("Visual").gameObject : null;
        if (_managerVisual == null) yield break;
        _managerVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 躺平
        yield return new WaitForSeconds(2.5f);
        _managerVisual.transform.localRotation = Quaternion.identity;
    }

    private void OnGUI()
    {
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        if (Time.time < _storyUntil)
        {
            GUI.Box(new Rect(w * 0.5f - 230, h * 0.12f, 460, 44), _story);
        }

        if (!_panelOpen) return;
        GUI.Box(new Rect(w * 0.5f - 170, h * 0.32f, 340, 136), "ANGRY GUEST — your call, boss");
        if (GUI.Button(new Rect(w * 0.5f - 150, h * 0.32f + 36, 300, 26), "Pay them off (half the rate)"))
            Choose(ComplaintChoice.Pay);
        if (GUI.Button(new Rect(w * 0.5f - 150, h * 0.32f + 66, 300, 26), "Cold shoulder (free, risky)"))
            Choose(ComplaintChoice.ColdShoulder);
        if (GUI.Button(new Rect(w * 0.5f - 150, h * 0.32f + 96, 300, 26), "FIGHT 🥊 (what could go wrong)"))
            Choose(ComplaintChoice.Fight);
    }
}
