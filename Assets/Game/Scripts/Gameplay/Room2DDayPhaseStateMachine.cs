using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 白天阶段状态机：Preparation → CheckInPeak → Recovery → Ended（单向，不可跳级）。
// 同时负责驱动 HUD 上的阶段标签和"推进"按钮。
//
// 关于初始 OnPhaseEntered 的触发时机：
//   Awake() 仅静默设置内部状态字段，不广播事件。
//   真正的初始 OnPhaseEntered(Preparation) 在 Start() 里触发。
//   这样同场景其他组件在 Awake() 里挂载订阅，也能收到第一次事件。
//   在 EditMode 单元测试中，MonoBehaviour 生命周期不自动执行 Start，
//   因此暴露 InitialiseForTesting() 让测试在订阅后手动触发初始事件。
public class Room2DDayPhaseStateMachine : MonoBehaviour
{
    // ── 阶段枚举 ────────────────────────────────────────────────────────────
    public enum Room2DDayPhase
    {
        Preparation,
        CheckInPeak,
        Recovery,
        Ended
    }

    // ── Inspector 引用（HUD 控件） ───────────────────────────────────────────
    [Header("HUD References")]
    [Tooltip("点击后推进到下一阶段的按钮")]
    [SerializeField] private Button advanceButton;

    [Tooltip("显示当前阶段名称的文本标签")]
    [SerializeField] private TextMeshProUGUI phaseLabel;

    [Tooltip("推进按钮上的文字标签")]
    [SerializeField] private TextMeshProUGUI advanceButtonLabel;

    // ── 事件 ────────────────────────────────────────────────────────────────
    /// <summary>进入某阶段时触发，每次进入只触发一次。</summary>
    public event Action<Room2DDayPhase> OnPhaseEntered;

    /// <summary>退出某阶段时触发，每次退出只触发一次。Ended 是终态，永不触发退出事件。</summary>
    public event Action<Room2DDayPhase> OnPhaseExited;

    // ── 内部状态 ─────────────────────────────────────────────────────────────
    private Room2DDayPhase _currentPhase = Room2DDayPhase.Preparation;

    // ── 公开属性 ─────────────────────────────────────────────────────────────
    /// <summary>当前阶段（只读）。</summary>
    public Room2DDayPhase CurrentPhase => _currentPhase;

    // ── Unity 生命周期 ───────────────────────────────────────────────────────

    private void Awake()
    {
        // 静默初始化状态，不广播事件（事件在 Start 里广播）。
        _currentPhase = Room2DDayPhase.Preparation;

        // 代码驱动按钮监听，防止场景重建后连线丢失。
        if (advanceButton != null)
        {
            advanceButton.onClick.RemoveListener(RequestAdvancePhase);
            advanceButton.onClick.AddListener(RequestAdvancePhase);
        }
    }

    private void Start()
    {
        // 广播初始 Preparation 进入事件，此时其他组件的 Awake 已执行完毕，订阅均已就位。
        FirePhaseEntered(_currentPhase);
        RefreshHud(_currentPhase);
    }

    // ── 公开 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 请求推进到下一阶段。顺序固定：Prep → Peak → Recovery → Ended。
    /// 已在 Ended 时调用为无操作。每次调用恰好推进一步，不会因同帧多次调用而跳级。
    /// </summary>
    public void RequestAdvancePhase()
    {
        // 终态，无操作。
        if (_currentPhase == Room2DDayPhase.Ended)
        {
            return;
        }

        Room2DDayPhase exiting = _currentPhase;
        Room2DDayPhase entering = NextPhase(_currentPhase);

        // 退出当前阶段。
        OnPhaseExited?.Invoke(exiting);

        _currentPhase = entering;

        // 进入新阶段。
        FirePhaseEntered(entering);
        RefreshHud(entering);
    }

    /// <summary>
    /// 重置到 Preparation 阶段（新一天流程用）。
    /// 先对当前阶段（若非 Ended）触发退出事件，再触发 Preparation 进入事件。
    /// </summary>
    public void ResetToPreparation()
    {
        // Ended 是终态，不触发退出事件；其他阶段正常触发退出。
        if (_currentPhase != Room2DDayPhase.Ended)
        {
            OnPhaseExited?.Invoke(_currentPhase);
        }

        _currentPhase = Room2DDayPhase.Preparation;
        FirePhaseEntered(_currentPhase);
        RefreshHud(_currentPhase);
    }

    /// <summary>
    /// 直接跳转到 Ended，绕过中间阶段。
    /// 仅供 Room2DDemoDayController 的遗留 EndDemoDay() 和自动计时器使用，
    /// 不应连接到玩家可触发的 UI 按钮。
    /// </summary>
    public void ForceJumpToEnded()
    {
        if (_currentPhase == Room2DDayPhase.Ended)
        {
            return;
        }

        // 退出当前阶段（非 Ended 才触发退出事件）。
        OnPhaseExited?.Invoke(_currentPhase);

        _currentPhase = Room2DDayPhase.Ended;
        FirePhaseEntered(Room2DDayPhase.Ended);
        RefreshHud(Room2DDayPhase.Ended);
    }

    /// <summary>
    /// 供 EditMode 测试使用：在测试订阅事件之后手动触发初始 Preparation 进入事件。
    /// 在正式运行时由 Start() 自动调用，不应在游戏代码中再次调用。
    /// </summary>
    public void InitialiseForTesting()
    {
        FirePhaseEntered(_currentPhase);
        RefreshHud(_currentPhase);
    }

    // ── 私有辅助 ─────────────────────────────────────────────────────────────

    private static Room2DDayPhase NextPhase(Room2DDayPhase phase)
    {
        switch (phase)
        {
            case Room2DDayPhase.Preparation: return Room2DDayPhase.CheckInPeak;
            case Room2DDayPhase.CheckInPeak: return Room2DDayPhase.Recovery;
            case Room2DDayPhase.Recovery:    return Room2DDayPhase.Ended;
            default:                         return Room2DDayPhase.Ended;
        }
    }

    private void FirePhaseEntered(Room2DDayPhase phase)
    {
        OnPhaseEntered?.Invoke(phase);
    }

    // 更新 HUD 标签和按钮文本/可见性。
    private void RefreshHud(Room2DDayPhase phase)
    {
        if (phaseLabel != null)
        {
            phaseLabel.text = PhaseDisplayName(phase);
        }

        if (advanceButtonLabel != null)
        {
            advanceButtonLabel.text = ButtonLabel(phase);
        }

        // Ended 时隐藏推进按钮（玩家无处可进）。
        if (advanceButton != null)
        {
            advanceButton.gameObject.SetActive(phase != Room2DDayPhase.Ended);
        }
    }

    private static string PhaseDisplayName(Room2DDayPhase phase)
    {
        switch (phase)
        {
            case Room2DDayPhase.Preparation: return "Preparation";
            case Room2DDayPhase.CheckInPeak: return "Check-In Peak";
            case Room2DDayPhase.Recovery:    return "Recovery";
            case Room2DDayPhase.Ended:       return "Day Ended";
            default:                         return phase.ToString();
        }
    }

    // 返回推进按钮在当前阶段应显示的文字。Ended 时按钮已隐藏，此处仍返回空字串防御性保护。
    private static string ButtonLabel(Room2DDayPhase phase)
    {
        switch (phase)
        {
            case Room2DDayPhase.Preparation: return "Start Operating";
            case Room2DDayPhase.CheckInPeak: return "Begin Recovery";
            case Room2DDayPhase.Recovery:    return "End Day";
            default:                         return string.Empty;
        }
    }
}
