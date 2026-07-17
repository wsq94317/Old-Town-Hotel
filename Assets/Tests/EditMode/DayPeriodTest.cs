using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DayPeriodTest
    {
        [Test]
        public void Periods_MapHours()
        {
            Assert.AreEqual(DayPeriod.Morning, DayPeriodLogic.PeriodFor(6f));
            Assert.AreEqual(DayPeriod.Morning, DayPeriodLogic.PeriodFor(9.9f));
            Assert.AreEqual(DayPeriod.Midday, DayPeriodLogic.PeriodFor(10f));
            Assert.AreEqual(DayPeriod.Afternoon, DayPeriodLogic.PeriodFor(14f));
            Assert.AreEqual(DayPeriod.Night, DayPeriodLogic.PeriodFor(19f));
            Assert.AreEqual(DayPeriod.Night, DayPeriodLogic.PeriodFor(23.5f));
        }

        [Test]
        public void Activity_CasinoPeaksAtNight_GymClosedAtNight()
        {
            Assert.AreEqual(4, DayPeriodLogic.ActivityFor(5, DayPeriod.Night));
            Assert.AreEqual(0, DayPeriodLogic.ActivityFor(5, DayPeriod.Morning));
            Assert.AreEqual(0, DayPeriodLogic.ActivityFor(4, DayPeriod.Night));
            Assert.AreEqual(2, DayPeriodLogic.ActivityFor(4, DayPeriod.Morning));
        }
    }
}
