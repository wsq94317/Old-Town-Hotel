using UnityEngine;

// 三个存档槽位的界面（玩家要求："可以选择保存不同的存档槽位，读取另一个存档"）。
//
// 走 UiLayout 的模态位与令牌（GuiModal），所以它和其它决策面板不会叠在一起抢点击
// ——IMGUI 没有 z 序，重叠就是误触，这条在 B1c 里已经吃过一次教训。
//
// **读档一律重载场景**。第 12 天读一个第 3 天的档，场上有走到一半的客人、排到一半
// 的队、在途的施工单、正在打扫的管家——逐个系统"改回去"不可能改干净，漏一个就是
// 一个幽灵。所以：记下意向（SaveSlots.RequestLoad）→ 重载场景 → 一切从零 → 灌存档。
public class SaveSlotPanel : MonoBehaviour
{
    private static SaveSlotPanel _instance;

    /// <summary>面板开着吗（WorldInputController 据此把点击转给 GUI 而不是世界）。</summary>
    public static bool AnyPanelOpen => _instance != null && _instance._open;

    private bool _open;
    private string _toast = "";
    private float _toastUntil;
    private readonly SaveSlotSummary[] _summaries = new SaveSlotSummary[SaveSlots.Count + 1];
    private bool _summariesFresh;

    private void OnEnable()
    {
        _instance = this;          // 热重载自愈：静态在域重载后被清空
        _summariesFresh = false;
    }

    private void OnDisable()
    {
        if (_instance == this) _instance = null;
        GuiModal.End(this);        // 别把令牌带进坟墓，否则其它面板再也弹不出来
    }

    /// <summary>没有这个组件时自动装一个（场景文件不必改）。</summary>
    public static SaveSlotPanel EnsureInScene()
    {
        if (_instance != null) return _instance;
        var existing = FindFirstObjectByType<SaveSlotPanel>();
        if (existing != null) { _instance = existing; return existing; }
        var go = new GameObject("SaveSlotPanel");
        return go.AddComponent<SaveSlotPanel>();
    }

    public static void Toggle()
    {
        var panel = EnsureInScene();
        panel._open = !panel._open;
        panel._summariesFresh = false;
        if (!panel._open) GuiModal.End(panel);
    }

    public static void CloseIfOpen()
    {
        if (_instance == null || !_instance._open) return;
        _instance._open = false;
        GuiModal.End(_instance);
    }

    private void RefreshSummaries()
    {
        for (int slot = 1; slot <= SaveSlots.Count; slot++)
            _summaries[slot] = SaveService.SummaryOf(slot);
        _summariesFresh = true;
    }

    private void OnGUI()
    {
        if (!_open) return;
        // 全屏晨报期间全体让位（IMGUI 没有 z 序，报告必须是唯一的画手）
        if (HotelSimSceneBridge.Instance != null && HotelSimSceneBridge.Instance.AwaitingMorningReport) return;

        Vector2 v = GuiScale.Begin();
        float w = v.x, h = v.y;
        if (!_summariesFresh) RefreshSummaries();

        float height = 96f + SaveSlots.Count * 74f;
        if (!GuiModal.Begin(this, w, h, height, out Rect box)) return;

        GUI.Box(box, GameText.T("SAVES"));
        float y = box.y + 28f;
        float left = box.x + 12f;
        float width = box.width - 24f;

        for (int slot = 1; slot <= SaveSlots.Count; slot++)
        {
            SaveSlotSummary summary = _summaries[slot];
            bool active = SaveSlots.ActiveSlot == slot;

            GUI.Label(new Rect(left, y, width, 20),
                      (active ? "> " : "  ") + GameText.F("SLOT {0}", slot) + "   " + DescribeSlot(summary));
            y += 22f;

            float half = (width - 8f) / 2f;
            if (GuiInput.Button(new Rect(left, y, half, 26),
                                GameText.T(summary.exists ? "OVERWRITE" : "SAVE HERE")))
                SaveInto(slot);

            // 空槽不给"读取"按钮：一个永远失败的按钮比没有按钮更让人困惑
            if (summary.exists)
            {
                if (GuiInput.Button(new Rect(left + half + 8f, y, half, 26), GameText.T("LOAD")))
                    LoadFrom(slot);
            }
            else
            {
                GUI.Label(new Rect(left + half + 8f, y, half, 26), GameText.T("(empty)"));
            }
            y += 30f;
            y += 6f;
        }

        if (GuiInput.Button(new Rect(left, y, width, 26), GameText.T("CLOSE")))
            CloseIfOpen();

        if (Time.time < _toastUntil)
            GUI.Box(new Rect(box.x, box.yMax + 4f, box.width, 24f), _toast);
    }

    private static string DescribeSlot(SaveSlotSummary s)
    {
        if (!s.exists) return GameText.T("empty");
        // 三个槽长得一样玩家没法选：天数+现金+星级足够区分两局游戏
        return GameText.F("day {0}   ${1}   {2}*", s.day, s.cash, s.stars.ToString("0.0"));
    }

    private void SaveInto(int slot)
    {
        var coordinator = FindFirstObjectByType<SaveCoordinator>();
        if (coordinator == null) { Say("No save system in this scene."); return; }

        SaveService.SaveToSlot(slot, coordinator.Capture());
        SaveSlots.SetActiveSlot(slot);     // 之后的自动存盘跟着走，否则玩家以为存了其实没存
        _summariesFresh = false;
        Say(GameText.F("Saved into slot {0}.", slot));
    }

    private void LoadFrom(int slot)
    {
        if (!SaveSlots.Exists(slot)) { Say("That slot is empty."); return; }

        // 记下意向 → 重载场景 → SaveCoordinator 在新场景的 Start 里消费它。
        // timeScale 要还原：玩家可能停在 0.25x 或跳段的 10x 上，那个值会活过重载。
        SaveSlots.RequestLoad(slot);
        Time.timeScale = 1f;
        CloseIfOpen();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.name : scene.path);
    }

    private void Say(string message)
    {
        _toast = GameText.T(message);
        _toastUntil = Time.time + 3f;
    }
}
