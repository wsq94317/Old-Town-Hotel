using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class ReputationLedgerTest
    {
        [Test]
        public void FreshLedger_StartsAtNeutralThreeStars()
        {
            var rep = new ReputationLedger(windowSize: 20);
            Assert.That(rep.SampleCount, Is.EqualTo(0));
            Assert.That(rep.Stars, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void RecordGuest_PerfectWindow_ReachesFiveStars()
        {
            var rep = new ReputationLedger(windowSize: 5);
            for (int i = 0; i < 5; i++) rep.RecordGuest(1.5f);
            Assert.That(rep.Stars, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void RecordGuest_WorstWindow_FloorsAtOneStar()
        {
            var rep = new ReputationLedger(windowSize: 5);
            for (int i = 0; i < 5; i++) rep.RecordGuest(0.5f);
            Assert.That(rep.Stars, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void RecordGuest_EvictsOldestSamples_BeyondWindow()
        {
            var rep = new ReputationLedger(windowSize: 4);
            for (int i = 0; i < 4; i++) rep.RecordGuest(0.5f); // 1★ history...
            for (int i = 0; i < 4; i++) rep.RecordGuest(1.5f); // ...fully displaced
            Assert.That(rep.SampleCount, Is.EqualTo(4));
            Assert.That(rep.Stars, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void RecordGuest_ClampsOutOfRangeSatisfaction()
        {
            var rep = new ReputationLedger(windowSize: 2);
            rep.RecordGuest(99f);   // clamps to 1.5
            rep.RecordGuest(-3f);   // clamps to 0.5
            Assert.That(rep.AverageSatisfaction, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rep.Stars, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void ExportImport_RoundTripsSamples()
        {
            var rep = new ReputationLedger(windowSize: 10);
            rep.RecordGuest(1.4f);
            rep.RecordGuest(0.9f);
            rep.RecordGuest(1.1f);

            var restored = new ReputationLedger(windowSize: 10);
            restored.ImportSamples(rep.ExportSamples());

            Assert.That(restored.SampleCount, Is.EqualTo(3));
            Assert.That(restored.Stars, Is.EqualTo(rep.Stars).Within(0.001f));
        }

        [Test]
        public void ImportSamples_Null_ClearsToNeutral()
        {
            var rep = new ReputationLedger(windowSize: 10);
            rep.RecordGuest(0.5f);
            rep.ImportSamples(null);
            Assert.That(rep.SampleCount, Is.EqualTo(0));
            Assert.That(rep.Stars, Is.EqualTo(3f).Within(0.001f));
        }
    }
}
