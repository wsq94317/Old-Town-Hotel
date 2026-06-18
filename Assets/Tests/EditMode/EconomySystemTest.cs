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
            _config.startingLoan = 0;        // wages-focused tests: no loan interest
            _config.dailyInterestRate = 0f;
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

        [Test]
        public void CloseEconomicDay_AccruesLoanInterest_AndGrowsBalance()
        {
            _config.startingCash = 1000;
            _config.startingLoan = 100000;
            _config.dailyInterestRate = 0.001f;
            _econ.InitializeForTest(_config); // re-init with a loan
            DayLedger d = _econ.CloseEconomicDay(servedGuests: 0); // income 0, wages 185, interest 100
            Assert.That(d.Interest, Is.EqualTo(100));
            Assert.That(_econ.Loan.Balance, Is.EqualTo(100100));
            Assert.That(_econ.Cash, Is.EqualTo(1000 - 185 - 100));
        }

        [Test]
        public void BorrowAndRepay_AdjustCashAndLoan()
        {
            _config.startingCash = 1000;
            _config.startingLoan = 0;
            _econ.InitializeForTest(_config);
            _econ.Borrow(5000);
            Assert.That(_econ.Cash, Is.EqualTo(6000));
            Assert.That(_econ.Loan.Balance, Is.EqualTo(5000));
            int repaid = _econ.RepayLoan(3000);
            Assert.That(repaid, Is.EqualTo(3000));
            Assert.That(_econ.Cash, Is.EqualTo(3000));
            Assert.That(_econ.Loan.Balance, Is.EqualTo(2000));
        }

        [Test]
        public void CreditLimit_UsesValueFactorMinusDebt()
        {
            _config.startingLoan = 0;
            _config.baseHotelValue = 0;
            _config.perRoomValue = 10000;
            _config.renovatedRoomBonus = 0;
            _config.creditLimitFactor = 0.5f;
            _econ.InitializeForTest(_config);
            // value = 10 * 10000 = 100000; limit = 50000 - 0 debt
            Assert.That(_econ.CreditLimit(10, 0), Is.EqualTo(50000));
        }

        [Test]
        public void HireCandidate_AddsToRoster_AndChargesSigningCost()
        {
            var bob = new StaffMember(StaffRole.Housekeeper, "Bob", 40);
            bool ok = _econ.HireCandidate(bob, 100);
            Assert.That(ok, Is.True);
            Assert.That(_econ.Payroll.Count, Is.EqualTo(4));
            Assert.That(_econ.Cash, Is.EqualTo(900));               // 1000 - 100
            Assert.That(_econ.Payroll.TotalDailyWages, Is.EqualTo(225)); // 185 + 40
        }

        [Test]
        public void HireCandidate_FailsWhenSigningCostUnaffordable()
        {
            var rich = new StaffMember(StaffRole.Manager, "Rich", 90);
            bool ok = _econ.HireCandidate(rich, 99999);
            Assert.That(ok, Is.False);
            Assert.That(_econ.Payroll.Count, Is.EqualTo(3));
            Assert.That(_econ.Cash, Is.EqualTo(1000));
        }

        [Test]
        public void FireStaff_RemovesFromRoster()
        {
            var bob = new StaffMember(StaffRole.Housekeeper, "Bob", 40);
            _econ.HireCandidate(bob);
            _econ.FireStaff(bob);
            Assert.That(_econ.Payroll.Count, Is.EqualTo(3));
        }

        [Test]
        public void GiveRaise_RaisesWage_ReflectedInTotalWages()
        {
            var liz = new StaffMember(StaffRole.Reception, "Liz", 50);
            _econ.HireCandidate(liz);
            int before = _econ.Payroll.TotalDailyWages;
            _econ.GiveRaise(liz, 70);
            Assert.That(liz.DailyWage, Is.EqualTo(70));
            Assert.That(_econ.Payroll.TotalDailyWages, Is.EqualTo(before + 20));
        }

        [Test]
        public void RefuseRaise_DropsMorale()
        {
            var liz = new StaffMember(StaffRole.Reception, "Liz", 50);
            _econ.HireCandidate(liz);
            int before = liz.Morale;
            _econ.RefuseRaise(liz, 20);
            Assert.That(liz.Morale, Is.EqualTo(before - 20));
        }
    }
}
