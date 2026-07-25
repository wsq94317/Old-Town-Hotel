using System;

// v3 模拟时钟（取代 GameClock）。纯 C#，不引用 UnityEngine。
//
// 设计要点（架构文档 B.2）：
//   · tick 单位 = 1 游戏分钟。一天 8:00→22:00 = 840 tick。
//     所有子系统按分钟推进，倍速与"跳到下一阶段"都只是"多跑几个 tick"——
//     **不存在两套时间语义**，这是防加速 bug 的关键。
//   · 1x = 30 真实分钟/天（默认速度就该是默认值）；0.25x 为直播/挂机档，
//     刚好还原"2 小时 = 1 天"的宣传口径；另有 2x。
//   · 钟面不自动跨日：到 22:00 clamp 并置 DayEndReached，由宿主完成日结后
//     调 BeginNextDay()——沿用 v1 GameClock 的语义，六个 OnDaySettled 订阅者零改动。
//   · 离线换算锚死 1x（OfflineGameMinutesFor），与玩家在线选的倍速无关，
//     否则开着 2x 退出就能刷双倍离线收益。
public sealed class SimClock
{
    public const int DayStartMinute = 8 * 60;   // 08:00
    public const int DayEndMinute = 22 * 60;    // 22:00
    public const int MinutesPerDay = DayEndMinute - DayStartMinute; // 840

    /// <summary>1x 映射：30 真实分钟跑完一天。</summary>
    public const float BaseRealSecondsPerGameMinute = 30f * 60f / MinutesPerDay; // ≈2.1429

    /// <summary>离线最多补 7 天（防拨表刷收益，配合保险箱容量双重封顶）。</summary>
    public const int MaxOfflineDays = 7;

    // 余量以"游戏分钟"为单位用 double 累加：float 累加在 2x 下会让 6.0 变成 5.9999
    // 而 floor 掉一个 tick（本项目有 float 吸收前科，这里从一开始就按 double 算）。
    private double _carryMinutes;
    private int _pendingTicks;
    private float _speed = 1f;

    /// <summary>浮点噪声容差（游戏分钟）：0.0001 分钟 ≈ 0.0002 真实秒，远小于一个 tick。</summary>
    private const double TickEpsilon = 1e-4;

    public int CurrentDay { get; private set; } = 1;
    public int CurrentMinute { get; private set; } = DayStartMinute;

    /// <summary>开局以来累计游戏分钟（跨日连续，事件排程/胶带复发等用）。</summary>
    public long TotalMinutesElapsed { get; private set; }

    /// <summary>倍速：0.25（直播档）/ 1 / 2。只影响推进速率，不改任何游戏时间语义。</summary>
    public float SpeedMultiplier
    {
        get => _speed;
        set => _speed = SimMath.Clamp(value, 0.05f, 16f);
    }

    /// <summary>待消化的 tick 数（宿主可按帧预算分批消化，防跳段卡顿）。</summary>
    public int PendingTicks => _pendingTicks;

    public bool DayEndReached => CurrentMinute >= DayEndMinute;

    public int MinutesRemainingToday => DayEndMinute - CurrentMinute;

    /// <summary>顶栏钟面 "HH:MM"（24 小时制）。</summary>
    public string TimeFormatted
    {
        get
        {
            int hour = CurrentMinute / 60;
            int minute = CurrentMinute % 60;
            return hour.ToString("00") + ":" + minute.ToString("00");
        }
    }

    /// <summary>按真实流逝秒数累积 tick（不立即推进钟面——逐 tick 消化时子系统才能看到正确的分钟）。</summary>
    public void Advance(float realDeltaSeconds)
    {
        if (realDeltaSeconds <= 0f) return;
        _carryMinutes += (double)realDeltaSeconds * _speed / BaseRealSecondsPerGameMinute;

        int whole = SimMath.FloorToInt(_carryMinutes + TickEpsilon);
        if (whole <= 0) return;

        _carryMinutes -= whole;
        if (_carryMinutes < 0d) _carryMinutes = 0d;
        QueueTicks(whole);
    }

    /// <summary>跳到当日某分钟（"跳到下一关键阶段"）。返回实际排入的 tick 数；不能倒退。</summary>
    public int FastForwardTo(int targetMinute)
    {
        int clamped = SimMath.Clamp(targetMinute, DayStartMinute, DayEndMinute);
        int alreadyQueuedMinute = CurrentMinute + _pendingTicks;
        int delta = clamped - alreadyQueuedMinute;
        if (delta <= 0) return 0;
        QueueTicks(delta);
        return delta;
    }

    /// <summary>消化一个 tick（推进 1 游戏分钟）。返回 false 表示没有待消化的 tick。</summary>
    public bool TryConsumeTick()
    {
        if (_pendingTicks <= 0) return false;
        _pendingTicks--;
        CurrentMinute++;
        TotalMinutesElapsed++;
        return true;
    }

    /// <summary>日结之后开启新一天（钟面回到 8:00，清空残留 tick）。</summary>
    public void BeginNextDay()
    {
        CurrentDay++;
        CurrentMinute = DayStartMinute;
        _pendingTicks = 0;
        _carryMinutes = 0d;
    }

    /// <summary>直接对齐钟面（离线结算已用闭式算过账，绝不能再跑一遍 tick）。</summary>
    public void JumpTo(int day, int minute)
    {
        CurrentDay = Math.Max(1, day);
        CurrentMinute = SimMath.Clamp(minute, DayStartMinute, DayEndMinute);
        _pendingTicks = 0;
        _carryMinutes = 0d;
    }

    /// <summary>存档恢复：连累计分钟一起还原（事件排程依赖它的连续性）。</summary>
    public void RestoreFromSave(int day, int minute, long totalMinutesElapsed)
    {
        JumpTo(day, minute);
        TotalMinutesElapsed = Math.Max(0L, totalMinutesElapsed);
    }

    /// <summary>离线时长 → 应推进的游戏分钟数。锚死 1x，负值 clamp 0，7 天封顶。</summary>
    public static long OfflineGameMinutesFor(double elapsedRealSeconds)
    {
        if (elapsedRealSeconds <= 0d) return 0L;
        double minutes = elapsedRealSeconds / BaseRealSecondsPerGameMinute;
        long cap = (long)MaxOfflineDays * MinutesPerDay;
        return (long)SimMath.Clamp(minutes, 0d, cap);
    }

    private void QueueTicks(int count)
    {
        // 钟面不跨日：pending 最多到当日打烊，日结由宿主驱动
        int room = MinutesRemainingToday - _pendingTicks;
        if (room <= 0) return;
        _pendingTicks += Math.Min(count, room);
    }
}
