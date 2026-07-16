using UnityEngine;

// 游戏内日时钟（纯 C#，不依赖 MonoBehaviour，EditMode 可测）。
// 一天 = dayStartHour → dayEndHour 的游戏时间，均匀映射到 dayLengthRealSeconds 真实秒。
// 到达 dayEndHour 后钟面停住（clamp），日结由 Room2DDemoDayController 负责触发。
public sealed class GameClock
{
    private readonly float _dayLengthRealSeconds;
    private readonly float _dayStartHour;
    private readonly float _dayEndHour;
    private float _currentHour;

    public GameClock(float dayLengthRealSeconds, float dayStartHour, float dayEndHour)
    {
        _dayLengthRealSeconds = Mathf.Max(1f, dayLengthRealSeconds);
        _dayStartHour = dayStartHour;
        _dayEndHour = Mathf.Max(dayStartHour + 0.01f, dayEndHour);
        _currentHour = _dayStartHour;
    }

    /// <summary>当前游戏时刻（小时，含小数）。</summary>
    public float CurrentHour => _currentHour;

    /// <summary>是否已到打烊时刻（钟面停在 dayEndHour）。</summary>
    public bool DayEndReached => _currentHour >= _dayEndHour;

    /// <summary>顶栏钟面字符串，24 小时制 "HH:MM"。</summary>
    public string CurrentTimeFormatted
    {
        get
        {
            int hour = Mathf.FloorToInt(_currentHour);
            int minute = Mathf.FloorToInt((_currentHour - hour) * 60f);
            return hour.ToString("00") + ":" + minute.ToString("00");
        }
    }

    /// <summary>按真实流逝秒数推进钟面；非正值忽略；到打烊即 clamp。</summary>
    public void Advance(float realDeltaSeconds)
    {
        if (realDeltaSeconds <= 0f) return;
        float hoursPerRealSecond = (_dayEndHour - _dayStartHour) / _dayLengthRealSeconds;
        _currentHour = Mathf.Min(_dayEndHour, _currentHour + realDeltaSeconds * hoursPerRealSecond);
    }

    /// <summary>直接推进 N 个游戏小时（事件用：如进警局跳过营业时间）。到打烊即 clamp。</summary>
    public void AdvanceGameHours(float gameHours)
    {
        if (gameHours <= 0f) return;
        _currentHour = Mathf.Min(_dayEndHour, _currentHour + gameHours);
    }

    /// <summary>回到当日开始时刻（新一天用）。</summary>
    public void ResetToDayStart() => _currentHour = _dayStartHour;

    /// <summary>是否已到达/越过某整点里程碑。</summary>
    public bool HasReachedHour(float hour) => _currentHour >= hour;
}
