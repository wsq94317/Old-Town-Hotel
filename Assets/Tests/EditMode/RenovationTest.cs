using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class RenovationTest
    {
        private RenovationConfigSO _cfg;

        [SetUp]
        public void SetUp() { _cfg = ScriptableObject.CreateInstance<RenovationConfigSO>(); }

        [TearDown]
        public void TearDown() { if (_cfg != null) Object.DestroyImmediate(_cfg); }

        [Test]
        public void Config_RevenueCostDays_ByTier()
        {
            Assert.That(_cfg.NightlyRevenueFor(RoomTier.Old), Is.LessThan(_cfg.NightlyRevenueFor(RoomTier.Basic)));
            Assert.That(_cfg.NightlyRevenueFor(RoomTier.Basic), Is.LessThan(_cfg.NightlyRevenueFor(RoomTier.Better)));
            Assert.That(_cfg.CostFor(RoomTier.Old), Is.EqualTo(0));
            Assert.That(_cfg.CostFor(RoomTier.Basic), Is.EqualTo(1500));
            Assert.That(_cfg.DaysFor(RoomTier.Better), Is.EqualTo(4));
        }

        [Test]
        public void BatchCost_AppliesDiscount_CappedAtMax()
        {
            Assert.That(_cfg.BatchCost(RoomTier.Basic, 1), Is.EqualTo(1500));          // no discount
            Assert.That(_cfg.BatchCost(RoomTier.Basic, 3), Is.EqualTo(3600));          // 1500*3*0.8
            Assert.That(_cfg.BatchCost(RoomTier.Basic, 6), Is.EqualTo(5400));          // discount capped 0.4 -> *0.6
            Assert.That(_cfg.BatchCost(RoomTier.Old, 5), Is.EqualTo(0));
        }

        [Test]
        public void Job_CountsDownAndCompletes()
        {
            var job = new RenovationJob(203, RoomTier.Basic, 3);
            Assert.That(job.IsComplete, Is.False);
            job.TickDay(); job.TickDay();
            Assert.That(job.DaysRemaining, Is.EqualTo(1));
            job.TickDay();
            Assert.That(job.IsComplete, Is.True);
        }

        [Test]
        public void Queue_TickDay_ReturnsCompletedJobs()
        {
            var q = new RenovationQueue();
            q.Start(new RenovationJob(201, RoomTier.Basic, 1));
            q.Start(new RenovationJob(202, RoomTier.Better, 2));
            Assert.That(q.ActiveCount, Is.EqualTo(2));

            var done1 = q.TickDay();
            Assert.That(done1.Count, Is.EqualTo(1));
            Assert.That(done1[0].RoomNumber, Is.EqualTo(201));
            Assert.That(q.ActiveCount, Is.EqualTo(1));

            var done2 = q.TickDay();
            Assert.That(done2.Count, Is.EqualTo(1));
            Assert.That(done2[0].RoomNumber, Is.EqualTo(202));
            Assert.That(q.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void EconomySystem_TrySpend_PaysBatchCost_OrRefusesWhenBroke()
        {
            var econCfg = ScriptableObject.CreateInstance<EconomyConfigSO>();
            econCfg.startingCash = 4000;
            econCfg.startingLoan = 0;
            var go = new GameObject("Econ");
            var econ = go.AddComponent<EconomySystem>();
            econ.InitializeForTest(econCfg);
            try
            {
                int cost = _cfg.BatchCost(RoomTier.Basic, 2); // 1500*2*0.9 = 2700
                Assert.That(cost, Is.EqualTo(2700));
                Assert.That(econ.TrySpend(cost), Is.True);
                Assert.That(econ.Cash, Is.EqualTo(1300));
                Assert.That(econ.TrySpend(9999), Is.False); // can't afford
                Assert.That(econ.Cash, Is.EqualTo(1300));   // unchanged
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(econCfg);
            }
        }
    }
}
