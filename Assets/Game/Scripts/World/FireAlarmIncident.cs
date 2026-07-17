using UnityEngine;

// 火警事件链（用户点名的完整无厘头闭环）：
//   每天 60% 概率在 11:00-17:00 随机时刻，某间【入住中】的房触发火警 🔥
//   → 手机红色通知 + 房间冒烟（灰色烟团）
//   → 经理赶到房内：发现有人抽烟 → 面板：打出去🥊 / 算了
//   → 无论如何消防局开罚单 -$1615（老楼的检查从不迟到）
//   → 追讨客人 $1615：70% 到账；30% CREDIT CARD DECLINED（钱打水漂+羞辱飘字）
//   → 无视 2 游戏小时：罚单照开 + 满意度 -3（客人们记得那个烟味）
public class FireAlarmIncident : MonoBehaviour
{
    public const int FineAmount = 1615;
    public const double DeclineChance = 0.30;

    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private ManagerController manager;
    [SerializeField] private int rngSeed = 8848;

    private System.Random _rng;
    private int _scheduledDay = -1;
    private float _triggerHour;
    private bool _scheduledToday;

    // 进行中的火警
    private Room2DEntity _room;
    private float _expireHour;
    private GameObject _smoke;
    private GuestAgent _smoker;
    private bool _panelOpen;
    private string _story = "";
    private float _storyUntil;

    public bool PanelOpen => _panelOpen;

    private void Awake()
    {
        _rng = new System.Random(rngSeed);
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
    }

    private void Update()
    {
        if (dayController == null) return;

        // 每日排程
        if (dayController.CurrentDay != _scheduledDay)
        {
            _scheduledDay = dayController.CurrentDay;
            _scheduledToday = _rng.NextDouble() < 0.6;
            _triggerHour = 11f + (float)_rng.NextDouble() * 6f;
            EndIncident(false);
        }

        float hour = dayController.Clock.CurrentHour;

        // 触发
        if (_scheduledToday && _room == null && hour >= _triggerHour)
        {
            _scheduledToday = false;
            TryStart();
        }

        if (_room == null) return;

        // 房态变了（客人退房了等）→ 静默收场
        if (_room.currentState != Room2DState.Occupied) { EndIncident(false); return; }

        // 超时无视 → 罚单照开 + 额外满意度惩罚
        if (hour >= _expireHour && !_panelOpen)
        {
            PayFine();
            if (demandLoop != null) demandLoop.prototypeSatisfactionScore -= 3;
            Say("You ignored the alarm. The fine arrived anyway. The smell stayed longer.");
            EndIncident(true);
            return;
        }

        // 经理进房 → 抓到烟民
        if (!_panelOpen && manager != null)
        {
            Vector3 p = manager.transform.position;
            if (FloorMath.FloorIndexForY(p.y) == FloorMath.FloorIndexForY(_room.transform.position.y)
                && Mathf.Abs(p.x - _room.transform.position.x) < 2.4f
                && Mathf.Abs(p.z - _room.transform.position.z) < 2.4f)
            {
                _panelOpen = true;
            }
        }
    }

    private void TryStart()
    {
        // 找一间入住房
        if (demandLoop == null || demandLoop.rooms == null) return;
        var occupied = new System.Collections.Generic.List<Room2DEntity>();
        foreach (var r in demandLoop.rooms)
            if (r != null && r.currentState == Room2DState.Occupied) occupied.Add(r);
        if (occupied.Count == 0) return; // 今天没得烧

        _room = occupied[_rng.Next(occupied.Count)];
        _expireHour = dayController.Clock.CurrentHour + 2f;

        // 烟雾（灰色小团上升循环）+ 烟民纸片人
        _smoke = new GameObject("SmokeFx");
        _smoke.transform.position = _room.transform.position;
        _smoke.AddComponent<SmokePuffs>();
        _smoke.AddComponent<AgentFloorVisibility>(); // 烟团在 Start 才生成，靠懒缓存收进来
        _smoker = GuestAgent.Spawn(_room.transform.position + new Vector3(-0.5f, 0f, 0.3f), "smoker");

        ManagerPhone.Push("fire", "FIRE ALARM — Room " + _room.roomNumber + "!!",
            _room.transform.position, new Color(1f, 0.25f, 0.2f));
        CameraShaker.Shake(0.12f, 0.4f);
    }

    private void PayFine()
    {
        if (economy != null) economy.TrySpend(FineAmount);
        if (manager != null)
            FloatingTextFx.Spawn(manager.transform.position, "-$" + FineAmount + " FIRE DEPT", new Color(1f, 0.35f, 0.25f), 1.1f);
    }

    private void ChargeGuest()
    {
        bool declined = _rng.NextDouble() < DeclineChance;
        if (declined)
        {
            Say("CREDIT CARD DECLINED. The guest shrugged. Your accountant wept.");
            if (manager != null)
                FloatingTextFx.Spawn(manager.transform.position, "DECLINED", new Color(1f, 0.2f, 0.2f), 1.4f);
        }
        else
        {
            if (economy != null) economy.RecordCheckout(FineAmount, 1f); // 追回计入当日收入
            Say("Charged the guest $" + FineAmount + ". Justice, itemized.");
            if (manager != null)
                FloatingTextFx.Spawn(manager.transform.position, "+$" + FineAmount, new Color(0.35f, 0.95f, 0.4f), 1.2f);
        }
    }

    private void Choose(bool beatThemOut)
    {
        if (!_panelOpen) return; // 幂等
        _panelOpen = false;

        if (beatThemOut)
        {
            FightFx.Play(_room.transform.position);
            if (_smoker != null)
            {
                var s = _smoker;
                _smoker = null;
                s.TravelTo(new Vector3(0f, 0f, -5.2f), () => Destroy(s.gameObject)); // 打出大门
            }
            _room.SetState(Room2DState.Dirty); // 人走房脏（烟味）
            ManagerReputation.Add(1);
            if (demandLoop != null) demandLoop.prototypeSatisfactionScore -= 1;
        }
        else
        {
            if (demandLoop != null) demandLoop.prototypeSatisfactionScore += 1; // 客人感激，其他客人闻着烟味扣在后面
        }

        PayFine();
        ChargeGuest();
        EndIncident(true);
    }

    private void EndIncident(bool resolved)
    {
        if (_smoke != null) Destroy(_smoke);
        if (_smoker != null) { Destroy(_smoker.gameObject); _smoker = null; }
        _room = null;
        _panelOpen = false;
        ManagerPhone.Resolve("fire");
    }

    private void Say(string msg) { _story = msg; _storyUntil = Time.time + 4.5f; }

    private void OnGUI()
    {
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        if (Time.time < _storyUntil)
            GUI.Box(new Rect(w * 0.5f - 230, h * 0.22f, 460, 40), _story);

        if (!_panelOpen || _room == null) return;
        GUI.Box(new Rect(w * 0.5f - 190, h * 0.32f, 380, 118),
            "ROOM " + _room.roomNumber + " — he's SMOKING in bed.\nThe fire dept is already writing the $" + FineAmount + " fine.");
        if (GuiInput.Button(new Rect(w * 0.5f - 170, h * 0.32f + 56, 340, 26), "Beat him out 🥊 (+prestige, room needs cleaning)"))
            Choose(true);
        if (GuiInput.Button(new Rect(w * 0.5f - 170, h * 0.32f + 86, 340, 26), "Let it slide (he'll tell his friends you're cool)"))
            Choose(false);
    }
}

// 烟团占位：灰色小方片循环上升渐隐。
public class SmokePuffs : MonoBehaviour
{
    private Transform[] _puffs = new Transform[5];
    private float[] _phase = new float[5];
    private static Material _mat;

    private void Start()
    {
        if (_mat == null)
            _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.6f, 0.6f, 0.6f) };
        for (int i = 0; i < _puffs.Length; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform);
            q.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);
            q.GetComponent<Renderer>().sharedMaterial = _mat;
            q.AddComponent<BillboardSprite>();
            _puffs[i] = q.transform;
            _phase[i] = Random.value * 2f;
        }
    }

    private void Update()
    {
        for (int i = 0; i < _puffs.Length; i++)
        {
            if (_puffs[i] == null) continue;
            _phase[i] += Time.deltaTime * 0.6f;
            float k = _phase[i] % 2f / 2f; // 0..1 循环
            _puffs[i].localPosition = new Vector3(Mathf.Sin(_phase[i]) * 0.2f, 0.8f + k * 1.6f, 0f);
            _puffs[i].localScale = Vector3.one * Mathf.Lerp(0.35f, 0.1f, k);
        }
    }
}
