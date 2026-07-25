using NUnit.Framework;

// 日内关键阶段：退房高峰 → 白天 → 入住高峰 → 夜间收尾 → 结算。
// "跳到下一关键阶段"按钮读 NextKeyMinuteAfter。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class PhaseSchedulerTest
    {
        [Test]
        public void PhaseFor_MapsSpansAndBoundaries()
        {
            Assert.That(PhaseScheduler.PhaseFor(8 * 60), Is.EqualTo(SimDayPhase.CheckoutPeak), "8:00 开门=退房高峰");
            Assert.That(PhaseScheduler.PhaseFor(10 * 60 + 59), Is.EqualTo(SimDayPhase.CheckoutPeak));
            Assert.That(PhaseScheduler.PhaseFor(11 * 60), Is.EqualTo(SimDayPhase.Midday), "边界归下一阶段");
            Assert.That(PhaseScheduler.PhaseFor(15 * 60), Is.EqualTo(SimDayPhase.Midday));
            Assert.That(PhaseScheduler.PhaseFor(16 * 60), Is.EqualTo(SimDayPhase.CheckInPeak));
            Assert.That(PhaseScheduler.PhaseFor(20 * 60), Is.EqualTo(SimDayPhase.CheckInPeak));
            Assert.That(PhaseScheduler.PhaseFor(21 * 60), Is.EqualTo(SimDayPhase.Evening));
            Assert.That(PhaseScheduler.PhaseFor(22 * 60), Is.EqualTo(SimDayPhase.Settle), "22:00 打烊=结算");
        }

        [Test]
        public void PhaseFor_ClampsOutOfRangeMinutes()
        {
            Assert.That(PhaseScheduler.PhaseFor(0), Is.EqualTo(SimDayPhase.CheckoutPeak));
            Assert.That(PhaseScheduler.PhaseFor(99 * 60), Is.EqualTo(SimDayPhase.Settle));
        }

        [Test]
        public void NextKeyMinuteAfter_WalksPhaseBoundaries()
        {
            Assert.That(PhaseScheduler.NextKeyMinuteAfter(SimClock.DayStartMinute), Is.EqualTo(11 * 60));
            Assert.That(PhaseScheduler.NextKeyMinuteAfter(9 * 60), Is.EqualTo(11 * 60));
            Assert.That(PhaseScheduler.NextKeyMinuteAfter(11 * 60), Is.EqualTo(16 * 60));
            Assert.That(PhaseScheduler.NextKeyMinuteAfter(17 * 60), Is.EqualTo(21 * 60));
            Assert.That(PhaseScheduler.NextKeyMinuteAfter(21 * 60 + 30), Is.EqualTo(SimClock.DayEndMinute));
            Assert.That(PhaseScheduler.NextKeyMinuteAfter(SimClock.DayEndMinute), Is.EqualTo(SimClock.DayEndMinute),
                        "已在结算点：无处可跳");
        }

        [Test]
        public void StartMinuteOf_RoundTripsWithPhaseFor()
        {
            foreach (SimDayPhase phase in System.Enum.GetValues(typeof(SimDayPhase)))
            {
                int start = PhaseScheduler.StartMinuteOf(phase);
                Assert.That(PhaseScheduler.PhaseFor(start), Is.EqualTo(phase),
                            "阶段起点分钟必须落回该阶段：" + phase);
            }
        }

        [Test]
        public void Labels_AreEnglishGameText()
        {
            foreach (SimDayPhase phase in System.Enum.GetValues(typeof(SimDayPhase)))
            {
                string label = PhaseScheduler.Label(phase);
                Assert.That(label, Is.Not.Null.And.Not.Empty);
                foreach (char c in label)
                    Assert.That(c, Is.LessThan(128), "游戏内文案必须是英文：" + label);
            }
        }

        [Test]
        public void SkipTarget_IsBlockedWhileUrgentIncidentPending()
        {
            // 有待处置的阻塞事件时禁止跳段（架构 B.2）：返回 false + 原因给 UI
            Assert.That(PhaseScheduler.CanSkip(10 * 60, blockingIncidents: 0, out string reason), Is.True);
            Assert.That(reason, Is.Empty);

            Assert.That(PhaseScheduler.CanSkip(10 * 60, blockingIncidents: 2, out reason), Is.False);
            Assert.That(reason, Is.Not.Empty);

            Assert.That(PhaseScheduler.CanSkip(SimClock.DayEndMinute, blockingIncidents: 0, out reason), Is.False,
                        "已到结算点没有下一阶段");
        }
    }
}
