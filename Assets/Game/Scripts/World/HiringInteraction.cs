using System.Collections.Generic;
using UnityEngine;

// M5 雇佣：右上角 HIRE 按钮 → 3 张候选卡（CandidateGenerator 生成，按天换池）→
// 点雇佣付签约费（2×日薪）→ 入册（Spawner 经 OnHired 自动生成纸片人上岗）。
public class HiringInteraction : MonoBehaviour
{
    [SerializeField] private EconomySystem economy;
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private StaffArchetypeSO[] archetypes;

    private bool _panelOpen;
    private List<StaffMember> _pool = new List<StaffMember>();
    private int _poolDay = -1;
    private string _story = "";
    private float _storyUntil;

    public bool PanelOpen => _panelOpen;

    private void Awake()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
    }

    private void EnsurePool()
    {
        int day = dayController != null ? dayController.CurrentDay : 1;
        if (day == _poolDay && _pool.Count > 0) return;
        _poolDay = day;
        // 每日刷新候选池（种子=天数，可复现）
        _pool = CandidateGenerator.GeneratePool(archetypes, 3, 1000 + day);
    }

    private int SigningCost(StaffMember m) => m.DailyWage * 2;

    private void Hire(StaffMember m)
    {
        if (economy == null) return;
        if (economy.HireCandidate(m, SigningCost(m)))
        {
            _pool.Remove(m);
            _story = m.DisplayName + " joins tomorrow— no wait, immediately. We're short-staffed.";
            FloatingTextFx.Spawn(new Vector3(0f, 0f, 2f), "-$" + SigningCost(m), new Color(1f, 0.4f, 0.3f));
        }
        else
        {
            _story = "You can't afford " + m.DisplayName + ". Awkward for everyone.";
        }
        _storyUntil = Time.time + 4f;
    }

    private static string TraitsOf(StaffMember m)
    {
        if (m.Traits.Count == 0) return "no traits";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < m.Traits.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(m.Traits[i]);
        }
        return sb.ToString();
    }

    private void OnGUI()
    {
        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;

        if (Time.time < _storyUntil)
            GUI.Box(new Rect(w * 0.5f - 230, h * 0.26f, 460, 40), _story);

        if (!_panelOpen)
        {
            var hireRect = new Rect(w - 90, 40, 80, 28);
            GuiInput.ReserveZone(hireRect); // 面板未开时也要吃点击（触屏通道）
            if (GuiInput.Button(hireRect, "HIRE")) { EnsurePool(); _panelOpen = true; }
            return;
        }

        GUI.Box(new Rect(w * 0.5f - 260, h * 0.28f, 520, 40 + Mathf.Max(1, _pool.Count) * 62), "TODAY'S CANDIDATES (signing fee = 2x daily wage)");
        if (_pool.Count == 0)
            GUI.Label(new Rect(w * 0.5f - 240, h * 0.28f + 34, 480, 24), "Pool's empty. Word got out about the fighting.");

        for (int i = 0; i < _pool.Count; i++)
        {
            var m = _pool[i];
            float y = h * 0.28f + 34 + i * 62;
            GUI.Label(new Rect(w * 0.5f - 240, y, 380, 44),
                $"{m.DisplayName} — {m.Role}, ${m.DailyWage}/day\n" +
                $"SPD {m.Attributes.Speed} / QLT {m.Attributes.Quality} / STA {m.Attributes.Stamina} · {TraitsOf(m)}");
            if (GuiInput.Button(new Rect(w * 0.5f + 150, y + 6, 100, 30), $"Hire -${SigningCost(m)}"))
            {
                Hire(m);
                break;
            }
        }
        if (GuiInput.Button(new Rect(w * 0.5f - 260, h * 0.28f + 40 + Mathf.Max(1, _pool.Count) * 62 + 6, 520, 26), "Close"))
            _panelOpen = false;
    }
}
