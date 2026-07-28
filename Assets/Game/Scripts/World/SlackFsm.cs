using System;

// 偷懒状态机（纯 C#，随机注入，EditMode 全测）：
//   Working →(经理不在场, 概率)→ Slacking →(经理进层)→ Waking(惊醒延迟)
//   → PanicFaking(慌张装忙) → Working
//   Slacking →(懒够了, 自愈)→ Working
// Waking/PanicFaking 期间经理靠近（managerNear）→ 现场抓包（OnCaught）。
// Slacking 中 IsProductive=false（宿主冻结工作进度）。
//
// **自愈这条边是必需的**：只留"经理进层"一条出路的话，经理不去的楼层会永久
// 卡住（实测：验房员 3.78/4 秒的活干了 205 秒，两间房整天回不到可售）。
// 经理的价值不是"唯一的解药"而是"加速器 + 让偷懒有代价"。
public sealed class SlackFsm
{
    public enum State { Working, Slacking, Waking, PanicFaking }

    private readonly Random _rng;
    private readonly bool _lazy;
    private readonly Func<int> _moraleGetter;
    private float _windowTimer;

    public State Current { get; private set; } = State.Working;

    /// <summary>本班次内是否偷过懒（质询判定用；换班/新任务时由宿主 ResetShiftRecord）。</summary>
    public bool HasRecentSlackRecord { get; private set; }

    /// <summary>现场抓包事件（Waking/PanicFaking 窗口内经理靠近）。</summary>
    public event Action OnCaught;

    /// <summary>只有真正 Working 才推进工作进度（装忙=慌张擦同一块地方，进度冻结）。</summary>
    public bool IsProductive => Current == State.Working;

    public SlackFsm(Random rng, bool hasLazyTrait, Func<int> moraleGetter)
    {
        _rng = rng ?? new Random();
        _lazy = hasLazyTrait;
        _moraleGetter = moraleGetter ?? (() => StaffMember.DefaultMorale);
    }

    /// <summary>每帧推进。仅在宿主处于"工作中"的宏观状态下调用。</summary>
    public void Tick(float dt, bool managerOnFloor, bool managerNear)
    {
        switch (Current)
        {
            case State.Working:
                if (!managerOnFloor && RollSlackEntry(dt))
                {
                    Current = State.Slacking;
                    HasRecentSlackRecord = true;
                    _windowTimer = RollSlackDuration();
                }
                break;

            case State.Slacking:
                if (managerOnFloor)
                {
                    Current = State.Waking;
                    _windowTimer = _lazy
                        ? SupervisionTuning.WakeDelayLazySeconds
                        : SupervisionTuning.WakeDelaySeconds;
                    break;
                }
                // 没人管也会自己回去干活（懒够了/怕被发现），否则这一格是死锁
                _windowTimer -= dt;
                if (_windowTimer <= 0f) Current = State.Working;
                break;

            case State.Waking:
                if (managerNear) { Caught(); return; }
                _windowTimer -= dt;
                if (_windowTimer <= 0f)
                {
                    Current = State.PanicFaking;
                    _windowTimer = SupervisionTuning.PanicFakeSeconds;
                }
                break;

            case State.PanicFaking:
                if (managerNear) { Caught(); return; }
                _windowTimer -= dt;
                if (_windowTimer <= 0f) Current = State.Working;
                break;
        }
    }

    /// <summary>这一次偷懒打算持续多久。记录（HasRecentSlackRecord）不受影响——
    /// 自己回去干活了不等于没偷懒，质询照样问得出来。</summary>
    private float RollSlackDuration()
    {
        float span = SupervisionTuning.SlackMaxSeconds - SupervisionTuning.SlackMinSeconds;
        float seconds = SupervisionTuning.SlackMinSeconds + (float)_rng.NextDouble() * span;
        return _lazy ? seconds * SupervisionTuning.LazySlackMultiplier : seconds;
    }

    private bool RollSlackEntry(float dt)
    {
        float chance = SupervisionTuning.BaseSlackChancePerSecond * dt;
        if (_lazy) chance *= SupervisionTuning.LazyTraitMultiplier;
        if (_moraleGetter() < SupervisionTuning.LowMoraleThreshold)
            chance *= SupervisionTuning.LowMoraleMultiplier;
        return _rng.NextDouble() < chance;
    }

    private void Caught()
    {
        Current = State.Working; // 抓包后立即恢复工作；决策效果由宿主处理
        OnCaught?.Invoke();
    }

    /// <summary>新任务/新班次清除偷懒记录（质询窗口按任务算）。</summary>
    public void ResetShiftRecord() => HasRecentSlackRecord = false;
}
