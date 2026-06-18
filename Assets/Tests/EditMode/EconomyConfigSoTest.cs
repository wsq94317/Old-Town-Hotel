using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class EconomyConfigSoTest
    {
        private EconomyConfigSO _so;

        [SetUp]
        public void SetUp() { _so = ScriptableObject.CreateInstance<EconomyConfigSO>(); }

        [TearDown]
        public void TearDown() { if (_so != null) Object.DestroyImmediate(_so); }

        [Test]
        public void Defaults_AreSane()
        {
            Assert.That(_so.startingCash, Is.EqualTo(2450)); // matches current PlayerCash default
            Assert.That(_so.roomRevenuePerGuest, Is.GreaterThan(0));
            Assert.That(_so.WageFor(StaffRole.Manager), Is.GreaterThan(_so.WageFor(StaffRole.Housekeeper)));
        }

        [Test]
        public void WageFor_ReturnsConfiguredRoleWage()
        {
            _so.receptionDailyWage = 50;
            _so.housekeeperDailyWage = 45;
            _so.managerDailyWage = 90;
            _so.inspectorDailyWage = 55;
            Assert.That(_so.WageFor(StaffRole.Reception), Is.EqualTo(50));
            Assert.That(_so.WageFor(StaffRole.Housekeeper), Is.EqualTo(45));
            Assert.That(_so.WageFor(StaffRole.Manager), Is.EqualTo(90));
            Assert.That(_so.WageFor(StaffRole.Inspector), Is.EqualTo(55));
        }
    }
}
