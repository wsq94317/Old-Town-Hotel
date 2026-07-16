using NUnit.Framework;

public class GameClockTest
{
    // 一天 = 8:00→22:00（14 小时）映射 140 真实秒 → 1 真实秒 = 0.1 游戏小时，数字好算。
    private static GameClock MakeClock() => new GameClock(140f, 8f, 22f);

    [Test]
    public void Clock_StartsAtDayStart_AndFormats24h()
    {
        var clock = MakeClock();
        Assert.AreEqual(8f, clock.CurrentHour, 0.0001f);
        Assert.AreEqual("08:00", clock.CurrentTimeFormatted);
        Assert.IsFalse(clock.DayEndReached);
    }

    [Test]
    public void Advance_MapsRealSecondsToGameHours()
    {
        var clock = MakeClock();
        clock.Advance(70f); // 一半 → 8 + 7 = 15:00
        Assert.AreEqual(15f, clock.CurrentHour, 0.0001f);
        Assert.AreEqual("15:00", clock.CurrentTimeFormatted);
    }

    [Test]
    public void Advance_FormatsMinutes()
    {
        var clock = MakeClock();
        clock.Advance(5f); // 0.5 游戏小时 → 08:30
        Assert.AreEqual("08:30", clock.CurrentTimeFormatted);
    }

    [Test]
    public void Advance_ClampsAtDayEnd()
    {
        var clock = MakeClock();
        clock.Advance(9999f);
        Assert.AreEqual(22f, clock.CurrentHour, 0.0001f);
        Assert.IsTrue(clock.DayEndReached);
        Assert.AreEqual("22:00", clock.CurrentTimeFormatted);
    }

    [Test]
    public void HasReachedHour_MilestoneChecks()
    {
        var clock = MakeClock();
        Assert.IsTrue(clock.HasReachedHour(8f));
        Assert.IsFalse(clock.HasReachedHour(10f));
        clock.Advance(20f); // → 10:00
        Assert.IsTrue(clock.HasReachedHour(10f));
        Assert.IsFalse(clock.HasReachedHour(18f));
    }

    [Test]
    public void AdvanceGameHours_JumpsAndClamps()
    {
        var clock = new GameClock(180f, 8f, 22f);
        clock.AdvanceGameHours(3f);
        Assert.AreEqual(11f, clock.CurrentHour, 0.001f);
        clock.AdvanceGameHours(999f);
        Assert.AreEqual(22f, clock.CurrentHour, 0.001f); // clamp 到打烊
    }

    [Test]
    public void ResetToDayStart_RewindsClock()
    {
        var clock = MakeClock();
        clock.Advance(9999f);
        clock.ResetToDayStart();
        Assert.AreEqual(8f, clock.CurrentHour, 0.0001f);
        Assert.IsFalse(clock.DayEndReached);
    }

    [Test]
    public void Advance_IgnoresNonPositiveDelta()
    {
        var clock = MakeClock();
        clock.Advance(-5f);
        clock.Advance(0f);
        Assert.AreEqual(8f, clock.CurrentHour, 0.0001f);
    }
}
