using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class RenovationSystemTest
    {
        private RenovationConfigSO _reno;
        private EconomyConfigSO _econCfg;
        private GameObject _go;
        private EconomySystem _econ;
        private RenovationSystem _sys;

        private void Build(int startingCash)
        {
            _reno = ScriptableObject.CreateInstance<RenovationConfigSO>();
            _econCfg = ScriptableObject.CreateInstance<EconomyConfigSO>();
            _econCfg.startingCash = startingCash;
            _econCfg.startingLoan = 0;
            _econCfg.dailyInterestRate = 0f;
            _go = new GameObject("RenoTest");
            _econ = _go.AddComponent<EconomySystem>();
            _econ.InitializeForTest(_econCfg);
            _sys = _go.AddComponent<RenovationSystem>();
            _sys.InitializeForTest(_reno, _econ, 4, 101); // rooms 101..104, all Old
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_reno != null) Object.DestroyImmediate(_reno);
            if (_econCfg != null) Object.DestroyImmediate(_econCfg);
        }

        [Test]
        public void StartRenovation_SpendsBatchCost_AndQueues()
        {
            Build(4000);
            Assert.That(_sys.StartRenovation(new[] { 101, 102 }, RoomTier.Basic), Is.True);
            Assert.That(_econ.Cash, Is.EqualTo(1300));          // 4000 - BatchCost(Basic,2)=2700
            Assert.That(_sys.Queue.ActiveCount, Is.EqualTo(2));
            Assert.That(_sys.IsRenovating(101), Is.True);
            Assert.That(_sys.RenovatedCount, Is.EqualTo(0));    // not finished yet
        }

        [Test]
        public void StartRenovation_FailsWhenBroke_NoSpendNoQueue()
        {
            Build(1000);
            Assert.That(_sys.StartRenovation(new[] { 101, 102 }, RoomTier.Basic), Is.False);
            Assert.That(_econ.Cash, Is.EqualTo(1000));
            Assert.That(_sys.Queue.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void AdvanceDay_CompletesJob_FlipsTier_CountsRenovated()
        {
            Build(4000);
            _sys.StartRenovation(new[] { 101 }, RoomTier.Basic); // basicDays = 3
            _sys.AdvanceDay();
            Assert.That(_sys.TierOf(101), Is.EqualTo(RoomTier.Old));   // 2 days left
            _sys.AdvanceDay();
            _sys.AdvanceDay();
            Assert.That(_sys.TierOf(101), Is.EqualTo(RoomTier.Basic));
            Assert.That(_sys.RenovatedCount, Is.EqualTo(1));
            Assert.That(_sys.Queue.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void StartRenovation_SkipsRoomsAlreadyAtOrAboveTarget()
        {
            Build(40000);
            // 101 -> Basic, finish it
            _sys.StartRenovation(new[] { 101 }, RoomTier.Basic);
            for (int i = 0; i < _reno.basicDays; i++) _sys.AdvanceDay();
            Assert.That(_sys.TierOf(101), Is.EqualTo(RoomTier.Basic));

            int cashBefore = _econ.Cash;
            // 101 already Basic -> not eligible for a Basic renovation
            Assert.That(_sys.StartRenovation(new[] { 101 }, RoomTier.Basic), Is.False);
            Assert.That(_econ.Cash, Is.EqualTo(cashBefore));
        }
    }
}
