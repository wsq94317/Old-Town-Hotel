using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class EconomySystemTest
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
            _config.managerDailyWage = 90;
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
        public void Initialize_SeedsCashAndStarterRoster()
        {
            Assert.That(_econ.Cash, Is.EqualTo(1000));
            // starter roster = 1 Reception + 1 HSK + 1 Manager => wages 185
            Assert.That(_econ.Payroll.Count, Is.EqualTo(3));
            Assert.That(_econ.Payroll.TotalDailyWages, Is.EqualTo(185));
        }

        [Test]
        public void CloseEconomicDay_AppliesIncomeMinusWages_ToCash()
        {
            // income = 5 guests * 80 = 400; wages = 185; net = +215
            DayLedger d = _econ.CloseEconomicDay(servedGuests: 5);
            Assert.That(d.Income, Is.EqualTo(400));
            Assert.That(d.Wages, Is.EqualTo(185));
            Assert.That(_econ.Cash, Is.EqualTo(1215));
        }
    }
}
