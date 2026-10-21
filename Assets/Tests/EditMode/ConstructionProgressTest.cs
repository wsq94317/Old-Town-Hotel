using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class ConstructionProgressTest
    {
        [Test]
        public void ReclaimProgress_AdvancesAtDayClose_AndDisappearsOnCompletion()
        {
            var queue = new SimRenovationQueue();
            queue.EnqueueReclaim(ReclaimPlan.For(ReclaimPlanKind.Refit), new[] { 209 });
            Assert.That(queue.JobFor(209).Progress, Is.Zero);
            Assert.That(queue.JobFor(209).isReclaim, Is.True);
            Assert.That(queue.JobFor(210), Is.Null);
            queue.TickDay();
            Assert.That(queue.JobFor(209).Progress, Is.EqualTo(.25f));
            Assert.That(queue.DaysRemainingFor(209), Is.EqualTo(3));
            queue.TickDay(); queue.TickDay(); queue.TickDay();
            Assert.That(queue.JobFor(209), Is.Null);
        }

        [Test]
        public void OldSave_InfersBatchDuration_WithoutRestartingConstruction()
        {
            var queue = new SimRenovationQueue();
            queue.RestoreJob(7, RenovationPlanKind.Economy, RoomTier.Old, 3,
                new[] { 209, 210, 211, 212, 301 }, true, ReclaimPlanKind.Refit);
            Assert.That(queue.JobFor(209).totalDays, Is.EqualTo(5));
            Assert.That(queue.JobFor(301).Progress, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(queue.DaysRemainingFor(209), Is.EqualTo(3));
        }

        [Test]
        public void RestoredProgress_PreservesOriginalDuration_AndHandlesRenovation()
        {
            var queue = new SimRenovationQueue();
            queue.RestoreJob(2, RenovationPlanKind.Standard, RoomTier.Basic, 2,
                new[] { 201 }, false, ReclaimPlanKind.PatchUp, 8);
            Assert.That(queue.JobFor(201).Progress, Is.EqualTo(.75f));
            Assert.That(queue.JobFor(201).isReclaim, Is.False);
            queue.TickDay();
            Assert.That(queue.JobFor(201).totalDays, Is.EqualTo(8));
        }
    }
}
