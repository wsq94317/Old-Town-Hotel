using NUnit.Framework;

// v3 SimClock：tick 单位=1 游戏分钟，一天 8:00-22:00（840 分钟）。
// 1x = 8 真实分钟/天（§C3 杠杆 A，原 30 分钟）；倍速只改推进速率，不改任何游戏时间语义。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class SimClockTest
    {
        private const float OneMinute = SimClock.BaseRealSecondsPerGameMinute;

        [Test]
        public void Constants_MatchDesignedDayLength()
        {
            Assert.That(SimClock.DayStartMinute, Is.EqualTo(8 * 60));
            Assert.That(SimClock.DayEndMinute, Is.EqualTo(22 * 60));
            Assert.That(SimClock.MinutesPerDay, Is.EqualTo(840));
            // 1x：RealMinutesPerGameDayAt1x 真实分钟跑完 840 游戏分钟
            // （断言对常量——日长是会调的设计旋钮，写死 30 会在调参时假失败）
            Assert.That(SimClock.MinutesPerDay * SimClock.BaseRealSecondsPerGameMinute,
                        Is.EqualTo(SimClock.RealMinutesPerGameDayAt1x * 60f).Within(0.01f));
        }

        [Test]
        public void NewClock_StartsAtDayOneMorning()
        {
            var clock = new SimClock();
            Assert.That(clock.CurrentDay, Is.EqualTo(1));
            Assert.That(clock.CurrentMinute, Is.EqualTo(SimClock.DayStartMinute));
            Assert.That(clock.TotalMinutesElapsed, Is.EqualTo(0));
            Assert.That(clock.PendingTicks, Is.EqualTo(0));
            Assert.That(clock.DayEndReached, Is.False);
        }

        [Test]
        public void Advance_AccumulatesFractionsIntoWholeTicks()
        {
            var clock = new SimClock();

            clock.Advance(OneMinute * 0.5f);
            Assert.That(clock.PendingTicks, Is.EqualTo(0), "半分钟不该产出 tick");

            clock.Advance(OneMinute * 0.6f);
            Assert.That(clock.PendingTicks, Is.EqualTo(1), "余量累积过 1 分钟就产出 tick");

            clock.Advance(OneMinute * 3f);
            Assert.That(clock.PendingTicks, Is.EqualTo(4));
        }

        [Test]
        public void Advance_IgnoresNonPositiveDelta()
        {
            var clock = new SimClock();
            clock.Advance(0f);
            clock.Advance(-5f);
            Assert.That(clock.PendingTicks, Is.EqualTo(0));
        }

        [Test]
        public void SpeedMultiplier_ScalesTickRate_ButNotGameTimeSemantics()
        {
            var fast = new SimClock { SpeedMultiplier = 2f };
            fast.Advance(OneMinute * 3f);
            Assert.That(fast.PendingTicks, Is.EqualTo(6), "2x：同样真实时间跑双倍游戏分钟");

            var stream = new SimClock { SpeedMultiplier = 0.25f }; // 直播/挂机档 = 2 小时/天
            stream.Advance(OneMinute * 4f);
            Assert.That(stream.PendingTicks, Is.EqualTo(1));
        }

        [Test]
        public void TryConsumeTick_AdvancesOneMinutePerCall()
        {
            var clock = new SimClock();
            clock.Advance(OneMinute * 3f);

            Assert.That(clock.TryConsumeTick(), Is.True);
            Assert.That(clock.CurrentMinute, Is.EqualTo(SimClock.DayStartMinute + 1));
            Assert.That(clock.TotalMinutesElapsed, Is.EqualTo(1));

            while (clock.TryConsumeTick()) { }
            Assert.That(clock.CurrentMinute, Is.EqualTo(SimClock.DayStartMinute + 3));
            Assert.That(clock.PendingTicks, Is.EqualTo(0));
            Assert.That(clock.TryConsumeTick(), Is.False, "没有 pending 时不该推进");
        }

        [Test]
        public void PendingTicks_ClampToRemainingMinutesToday()
        {
            var clock = new SimClock();
            clock.Advance(OneMinute * 5000f); // 远超一天

            Assert.That(clock.PendingTicks, Is.EqualTo(SimClock.MinutesPerDay),
                        "钟面不跨日：pending 最多到当日打烊");

            while (clock.TryConsumeTick()) { }
            Assert.That(clock.CurrentMinute, Is.EqualTo(SimClock.DayEndMinute));
            Assert.That(clock.DayEndReached, Is.True);
            Assert.That(clock.MinutesRemainingToday, Is.EqualTo(0));

            clock.Advance(OneMinute * 10f);
            Assert.That(clock.PendingTicks, Is.EqualTo(0), "打烊后不再产出 tick，等日结");
        }

        [Test]
        public void BeginNextDay_ResetsClockAndAccumulatesTotalMinutes()
        {
            var clock = new SimClock();
            clock.Advance(OneMinute * 5000f);
            while (clock.TryConsumeTick()) { }
            long afterDayOne = clock.TotalMinutesElapsed;

            clock.BeginNextDay();

            Assert.That(clock.CurrentDay, Is.EqualTo(2));
            Assert.That(clock.CurrentMinute, Is.EqualTo(SimClock.DayStartMinute));
            Assert.That(clock.DayEndReached, Is.False);
            Assert.That(clock.PendingTicks, Is.EqualTo(0), "跨日清空残留 pending");

            clock.Advance(OneMinute * 2f);
            while (clock.TryConsumeTick()) { }
            Assert.That(clock.TotalMinutesElapsed, Is.EqualTo(afterDayOne + 2),
                        "累计分钟跨日连续（事件排程用）");
        }

        [Test]
        public void FastForwardTo_QueuesExactTicks_AndRefusesBackwards()
        {
            var clock = new SimClock();

            int queued = clock.FastForwardTo(16 * 60); // 跳到入住高峰
            Assert.That(queued, Is.EqualTo(16 * 60 - SimClock.DayStartMinute));
            Assert.That(clock.PendingTicks, Is.EqualTo(queued));

            while (clock.TryConsumeTick()) { }
            Assert.That(clock.CurrentMinute, Is.EqualTo(16 * 60));

            Assert.That(clock.FastForwardTo(9 * 60), Is.EqualTo(0), "不能倒退");
            Assert.That(clock.FastForwardTo(99 * 60), Is.EqualTo(SimClock.DayEndMinute - 16 * 60),
                        "越界目标 clamp 到打烊");
        }

        [Test]
        public void JumpTo_SetsTimeWithoutQueueingTicks()
        {
            // 离线结算已用闭式算过账，钟面只需对齐，不能再跑一遍 tick
            var clock = new SimClock();
            clock.Advance(OneMinute * 10f);

            clock.JumpTo(day: 4, minute: 15 * 60);

            Assert.That(clock.CurrentDay, Is.EqualTo(4));
            Assert.That(clock.CurrentMinute, Is.EqualTo(15 * 60));
            Assert.That(clock.PendingTicks, Is.EqualTo(0));
        }

        [Test]
        public void TimeFormatted_IsTwentyFourHourClock()
        {
            var clock = new SimClock();
            Assert.That(clock.TimeFormatted, Is.EqualTo("08:00"));

            clock.FastForwardTo(9 * 60 + 5);
            while (clock.TryConsumeTick()) { }
            Assert.That(clock.TimeFormatted, Is.EqualTo("09:05"));
        }

        [Test]
        public void OfflineGameMinutesFor_UsesFixedOneXMapping_IgnoringSpeed()
        {
            // 离线换算必须锚 1x：否则玩家开着 2x 退出就能刷双倍离线收益
            double halfDayReal = SimClock.MinutesPerDay * SimClock.BaseRealSecondsPerGameMinute * 0.5;
            Assert.That(SimClock.OfflineGameMinutesFor(halfDayReal),
                        Is.EqualTo(SimClock.MinutesPerDay / 2).Within(1));

            Assert.That(SimClock.OfflineGameMinutesFor(-100d), Is.EqualTo(0),
                        "拨表回退 clamp 0（防作弊）");
        }

        [Test]
        public void OfflineGameMinutesFor_CapsAtSevenDays()
        {
            double thirtyDaysReal = 30 * SimClock.MinutesPerDay * SimClock.BaseRealSecondsPerGameMinute;
            Assert.That(SimClock.OfflineGameMinutesFor(thirtyDaysReal),
                        Is.EqualTo(SimClock.MaxOfflineDays * SimClock.MinutesPerDay));
        }
    }
}
