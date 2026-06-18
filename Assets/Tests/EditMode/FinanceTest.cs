using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FinanceTest
    {
        // ── LoanAccount ──────────────────────────────────────────────
        [Test]
        public void Loan_AccrueDailyInterest_AddsRoundedInterest()
        {
            var loan = new LoanAccount(100000, 0.001f);
            int interest = loan.AccrueDailyInterest();
            Assert.That(interest, Is.EqualTo(100));
            Assert.That(loan.Balance, Is.EqualTo(100100));
        }

        [Test]
        public void Loan_Repay_IsCappedAtBalance_AndClears()
        {
            var loan = new LoanAccount(50000, 0f);
            Assert.That(loan.Repay(20000), Is.EqualTo(20000));
            Assert.That(loan.Balance, Is.EqualTo(30000));
            Assert.That(loan.Repay(999999), Is.EqualTo(30000)); // capped
            Assert.That(loan.Balance, Is.EqualTo(0));
            Assert.That(loan.IsCleared, Is.True);
        }

        [Test]
        public void Loan_Borrow_IncreasesBalance_NegativeOpeningClampedToZero()
        {
            var loan = new LoanAccount(-5, -1f);
            Assert.That(loan.Balance, Is.EqualTo(0));
            Assert.That(loan.DailyInterestRate, Is.EqualTo(0f));
            loan.Borrow(2000);
            Assert.That(loan.Balance, Is.EqualTo(2000));
        }

        // ── HotelValuation ───────────────────────────────────────────
        [Test]
        public void Valuation_Compute_SumsBaseRoomsAndRenovations()
        {
            int v = HotelValuation.Compute(openRooms: 10, renovatedRooms: 3,
                                           baseValue: 50000, perRoomValue: 8000, renovatedRoomBonus: 12000);
            Assert.That(v, Is.EqualTo(50000 + 80000 + 36000));
        }

        [Test]
        public void Valuation_Compute_ClampsRenovatedToOpenRooms()
        {
            int v = HotelValuation.Compute(openRooms: 5, renovatedRooms: 99,
                                           baseValue: 0, perRoomValue: 1000, renovatedRoomBonus: 500);
            Assert.That(v, Is.EqualTo(5 * 1000 + 5 * 500));
        }

        [Test]
        public void Valuation_CreditLimit_ValueTimesFactorMinusDebt_NeverNegative()
        {
            Assert.That(HotelValuation.CreditLimit(200000, 0.5f, 60000), Is.EqualTo(40000));
            Assert.That(HotelValuation.CreditLimit(100000, 0.5f, 80000), Is.EqualTo(0));
        }

        // ── DayLedger interest/maintenance ───────────────────────────
        [Test]
        public void DayLedger_WithInterestAndMaintenance_NetSubtractsAll()
        {
            var d = new DayLedger(income: 400, wages: 185, interest: 30, maintenance: 20, openingBalance: 1000);
            Assert.That(d.Net, Is.EqualTo(165));
            Assert.That(d.ClosingBalance, Is.EqualTo(1165));
        }

        [Test]
        public void DayLedger_LegacyCtor_HasZeroInterestAndMaintenance()
        {
            var d = new DayLedger(income: 300, wages: 95, openingBalance: 1000);
            Assert.That(d.Interest, Is.EqualTo(0));
            Assert.That(d.Maintenance, Is.EqualTo(0));
            Assert.That(d.Net, Is.EqualTo(205));
        }

        // ── EconomyConfig finance defaults ───────────────────────────
        [Test]
        public void Config_FinanceDefaults_AreSane()
        {
            var so = ScriptableObject.CreateInstance<EconomyConfigSO>();
            try
            {
                Assert.That(so.startingLoan, Is.GreaterThan(0));
                Assert.That(so.dailyInterestRate, Is.GreaterThan(0f));
                Assert.That(so.creditLimitFactor, Is.GreaterThan(0f));
                Assert.That(so.perRoomValue, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(so); }
        }
    }
}
