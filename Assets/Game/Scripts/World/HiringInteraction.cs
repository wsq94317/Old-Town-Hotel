using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Daily hiring board: top-right HIRE button opens today's candidates, each
// candidate costs a 2x-daily-wage signing fee, and successful hires spawn via
// StaffAgentSpawner immediately.
public class HiringInteraction : MonoBehaviour
{
    [SerializeField] private EconomySystem economy;
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private StaffArchetypeSO[] archetypes;

    private bool _panelOpen;
    private readonly List<StaffMember> _pool = new List<StaffMember>();
    private int _poolDay = -1;
    private string _story = "";
    private float _storyUntil;
    private Vector2 _poolScroll;
    private GUIStyle _candidateBodyStyle;
    private GUIStyle _candidateHeaderStyle;

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
        _pool.Clear();
        _pool.AddRange(CandidateGenerator.GeneratePool(archetypes, 3, 1000 + day));
        _poolScroll = Vector2.zero;
    }

    private int SigningCost(StaffMember member) => member.DailyWage * 2;

    private void Hire(StaffMember member)
    {
        if (economy == null || member == null) return;
        if (!_pool.Contains(member)) return;

        if (economy.HireCandidate(member, SigningCost(member)))
        {
            _pool.Remove(member);
            _story = member.DisplayName + " joins immediately. You're clearly desperate.";
            FloatingTextFx.Spawn(new Vector3(0f, 0f, 2f), "-$" + SigningCost(member), new Color(1f, 0.4f, 0.3f));
        }
        else
        {
            _story = "You can't afford " + member.DisplayName + ". Awkward for everyone.";
        }

        _storyUntil = Time.time + 4f;
    }

    private static string TraitsOf(StaffMember member)
    {
        if (member == null || member.Traits.Count == 0) return "no traits";

        var builder = new StringBuilder();
        for (int i = 0; i < member.Traits.Count; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(member.Traits[i]);
        }

        return builder.ToString();
    }

    private GUIStyle CandidateBodyStyle()
    {
        if (_candidateBodyStyle == null)
        {
            _candidateBodyStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                richText = true,
                fontSize = 15
            };
        }

        return _candidateBodyStyle;
    }

    private GUIStyle CandidateHeaderStyle()
    {
        if (_candidateHeaderStyle == null)
        {
            _candidateHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
        }

        return _candidateHeaderStyle;
    }

    private void OnGUI()
    {
        Vector2 view = GuiScale.Begin();
        float width = view.x;
        float height = view.y;

        if (Time.time < _storyUntil)
            GUI.Box(new Rect(width * 0.5f - 230f, height * 0.26f, 460f, 40f), _story);

        if (!_panelOpen)
        {
            var hireRect = new Rect(width - 90f, 40f, 80f, 28f);
            GuiInput.ReserveZone(hireRect);
            if (GuiInput.Button(hireRect, "HIRE"))
            {
                EnsurePool();
                _panelOpen = true;
            }
            return;
        }

        float margin = Mathf.Max(14f, width * 0.035f);
        float panelWidth = Mathf.Min(720f, width - margin * 2f);
        float panelX = (width - panelWidth) * 0.5f;
        float panelY = Mathf.Clamp(height * 0.12f, 20f, height * 0.22f);
        float panelHeight = Mathf.Min(460f, height - panelY - 20f);
        float headerHeight = 44f;
        float footerHeight = 38f;
        float viewportHeight = Mathf.Max(96f, panelHeight - headerHeight - footerHeight - 20f);
        float rowHeight = 76f;
        float rowGap = 8f;
        float contentHeight = _pool.Count == 0 ? viewportHeight : _pool.Count * (rowHeight + rowGap);

        var panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
        var viewportRect = new Rect(panelX + 12f, panelY + headerHeight + 6f, panelWidth - 24f, viewportHeight);
        var contentRect = new Rect(0f, 0f, viewportRect.width - 18f, contentHeight);

        GuiInput.ReserveZone(panelRect);
        GUI.Box(panelRect, GUIContent.none);
        GUI.Label(
            new Rect(panelX + 12f, panelY + 8f, panelWidth - 24f, 24f),
            "TODAY'S CANDIDATES (signing fee = 2x daily wage)",
            CandidateHeaderStyle());

        _poolScroll = GUI.BeginScrollView(viewportRect, _poolScroll, contentRect, false, contentHeight > viewportHeight);
        if (_pool.Count == 0)
        {
            GUI.Label(
                new Rect(8f, 10f, contentRect.width - 16f, 24f),
                "Pool's empty. Word got out about the fighting.",
                CandidateBodyStyle());
        }
        else
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                StaffMember member = _pool[i];
                float y = i * (rowHeight + rowGap);
                GUI.Box(new Rect(0f, y, contentRect.width, rowHeight), GUIContent.none);

                string body =
                    $"<b>{member.DisplayName}</b> - {member.Role}, ${member.DailyWage}/day\n" +
                    $"SPD {member.Attributes.Speed} / QLT {member.Attributes.Quality} / STA {member.Attributes.Stamina} - {TraitsOf(member)}";
                GUI.Label(
                    new Rect(12f, y + 10f, contentRect.width - 144f, rowHeight - 20f),
                    body,
                    CandidateBodyStyle());

                if (GUI.Button(new Rect(contentRect.width - 118f, y + 20f, 106f, 32f), $"Hire -${SigningCost(member)}"))
                {
                    Hire(member);
                    GUI.EndScrollView();
                    return;
                }
            }
        }
        GUI.EndScrollView();

        if (GuiInput.Button(new Rect(panelX + 12f, panelY + panelHeight - footerHeight, panelWidth - 24f, 28f), "Close"))
            _panelOpen = false;
    }
}
