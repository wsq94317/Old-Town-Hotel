using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    // Phase 6 income v2: per-checkout tiered revenue × satisfaction + ★ reputation.
    [TestFixture]
    public class EconomyCheckoutTest
    {
        private GameObject _go;
        private EconomySystem _econ;
        private EconomyConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EconomyConfigSO>();
            _config.startingCash = 1000;
            _config.roomRevenuePerGuest = 80;
            _config.receptionDailyWage = 50;
            _config.housekeeperDailyWage = 45;
            _config.managerDailyWage = 90;   // starter roster wages = 185
            _config.startingLoan = 0;
            _config.dailyInterestRate = 0f;
            _config.reputationWindowSize = 20;
            _go = new GameObject("Econ");
            _econ = _go.AddComponent<EconomySystem>();
            _econ.InitializeForTest(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [Test]
        public void RecordCheckout_CreditsNightlyRateTimesSatisfaction()
        {
            int credited = _econ.RecordCheckout(nightlyRate: 110, satisfactionMult: 1.2f);
            Assert.That(credited, Is.EqualTo(132)); // 110 * 1.2
        }

        [Test]
        public void RecordCheckout_ClampsSatisfactionToLedgerRange()
        {
            Assert.That(_econ.RecordCheckout(100, 99f), Is.EqualTo(150));  // clamp to 1.5
            Assert.That(_econ.RecordCheckout(100, -1f), Is.EqualTo(50));   // clamp to 0.5
        }

        [Test]
        public void CloseEconomicDay_PrefersCheckoutIncome_OverFlatRate()
        {
            _econ.RecordCheckout(110, 1.0f); // 110
            _econ.RecordCheckout(80, 0.5f);  // 40
            // legacy path would be 5 * 80 = 400; checkout path wins: 150
            DayLedger d = _econ.CloseEconomicDay(servedGuests: 5);
            Assert.That(d.Income, Is.EqualTo(150));
        }

        [Test]
        public void CloseEconomicDay_FallsBackToFlatRate_WhenNoCheckouts()
        {
            DayLedger d = _econ.CloseEconomicDay(servedGuests: 3);
            Assert.That(d.Income, Is.EqualTo(240)); // 3 * 80
        }

        [Test]
        public void CloseEconomicDay_ResetsPendingCheckouts_ForNextDay()
        {
            _econ.RecordCheckout(190, 1.5f);
            _econ.CloseEconomicDay(servedGuests: 0);
            // day 2: nothing recorded -> falls back to flat rate again
            DayLedger d2 = _econ.CloseEconomicDay(servedGuests: 2);
            Assert.That(d2.Income, Is.EqualTo(160));
        }

        [Test]
        public void RecordCheckout_FeedsReputation()
        {
            Assert.That(_econ.Reputation.Stars, Is.EqualTo(3f).Within(0.001f)); // fresh hotel
            _econ.RecordCheckout(110, 1.5f);
            Assert.That(_econ.Reputation.Stars, Is.EqualTo(5f).Within(0.001f)); // single perfect stay
        }

        [Test]
        public void DailyGuestTarget_FollowsStarBrackets()
        {
            // fresh hotel = 3.0★ -> "<4★" bracket
            Assert.That(_econ.DailyGuestTarget, Is.EqualTo(_config.guestsBelow4Stars));
            // drive rating down to 1★
            for (int i = 0; i < 20; i++) _econ.RecordCheckout(80, 0.5f);
            Assert.That(_econ.DailyGuestTarget, Is.EqualTo(_config.guestsBelow2Stars));
        }

        [Test]
        public void CaptureRestore_RoundTripsReputation()
        {
            _econ.RecordCheckout(110, 1.4f);
            _econ.RecordCheckout(110, 0.6f);
            EconomyState saved = _econ.CaptureState();

            _econ.InitializeForTest(_config); // wipe
            Assert.That(_econ.Reputation.SampleCount, Is.EqualTo(0));
            _econ.RestoreState(saved);

            Assert.That(_econ.Reputation.SampleCount, Is.EqualTo(2));
            Assert.That(_econ.Reputation.AverageSatisfaction, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void GuestsPerDayFor_CoversAllBrackets()
        {
            Assert.That(_config.GuestsPerDayFor(1.5f), Is.EqualTo(_config.guestsBelow2Stars));
            Assert.That(_config.GuestsPerDayFor(2.5f), Is.EqualTo(_config.guestsBelow3Stars));
            Assert.That(_config.GuestsPerDayFor(3.5f), Is.EqualTo(_config.guestsBelow4Stars));
            Assert.That(_config.GuestsPerDayFor(4.2f), Is.EqualTo(_config.guestsBelow45Stars));
            Assert.That(_config.GuestsPerDayFor(5f), Is.EqualTo(_config.guestsTopStars));
        }
    }
}
