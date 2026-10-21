using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class HotelJourneyTest
    {
        [Test]
        public void CompletedOutOfOrder_StillCountsAfterReload()
        {
            var j = new HotelJourney();
            j.RestoreRoom(209); j.RenovateRoom(201);
            j.CloseDay(1, 20); j.CloseDay(2, 40); j.CloseDay(3, 10);
            Assert.That(j.Stage, Is.Zero);
            var restored = new HotelJourney(); restored.Restore(j.Capture());
            restored.CheckIn(); Assert.That(restored.Stage, Is.EqualTo(1));
            restored.Collect(40); Assert.That(restored.Stage, Is.EqualTo(5));
        }
        [Test]
        public void ReloadingSameClosingDay_DoesNotDuplicateProfitableDays()
        {
            var j = new HotelJourney();
            j.CloseDay(1, 20); j.CloseDay(1, 20);
            Assert.That(j.State.profitableStreak, Is.EqualTo(1));
            j.CloseDay(2, -1);
            Assert.That(j.State.profitableStreak, Is.Zero);
            Assert.That(j.State.longestStreak, Is.EqualTo(1));
        }
    }
}
