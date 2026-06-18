using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class PayrollLedgerTest
    {
        [Test]
        public void StaffMember_StoresRoleNameAndWage()
        {
            var s = new StaffMember(StaffRole.Housekeeper, "Bob", 45);
            Assert.That(s.Role, Is.EqualTo(StaffRole.Housekeeper));
            Assert.That(s.DisplayName, Is.EqualTo("Bob"));
            Assert.That(s.DailyWage, Is.EqualTo(45));
        }

        [Test]
        public void Hire_AddsToRoster_AndTotalsWages()
        {
            var ledger = new PayrollLedger();
            ledger.Hire(new StaffMember(StaffRole.Reception, "Ann", 50));
            ledger.Hire(new StaffMember(StaffRole.Housekeeper, "Bob", 45));
            Assert.That(ledger.Count, Is.EqualTo(2));
            Assert.That(ledger.TotalDailyWages, Is.EqualTo(95));
        }

        [Test]
        public void Fire_RemovesMember_AndLowersWages()
        {
            var ledger = new PayrollLedger();
            var bob = new StaffMember(StaffRole.Housekeeper, "Bob", 45);
            ledger.Hire(new StaffMember(StaffRole.Reception, "Ann", 50));
            ledger.Hire(bob);
            ledger.Fire(bob);
            Assert.That(ledger.Count, Is.EqualTo(1));
            Assert.That(ledger.TotalDailyWages, Is.EqualTo(50));
        }

        [Test]
        public void CloseDay_ComputesNetAndNewBalance()
        {
            var ledger = new PayrollLedger();
            ledger.Hire(new StaffMember(StaffRole.Reception, "Ann", 50));
            ledger.Hire(new StaffMember(StaffRole.Housekeeper, "Bob", 45)); // wages 95
            DayLedger d = ledger.CloseDay(income: 300, startingBalance: 1000);
            Assert.That(d.Income, Is.EqualTo(300));
            Assert.That(d.Wages, Is.EqualTo(95));
            Assert.That(d.Net, Is.EqualTo(205));
            Assert.That(d.ClosingBalance, Is.EqualTo(1205));
        }

        [Test]
        public void CloseDay_AllowsNegativeBalance_WhenDeficit()
        {
            var ledger = new PayrollLedger();
            ledger.Hire(new StaffMember(StaffRole.Manager, "Mae", 90));
            DayLedger d = ledger.CloseDay(income: 20, startingBalance: 50);
            Assert.That(d.Net, Is.EqualTo(-70));
            Assert.That(d.ClosingBalance, Is.EqualTo(-20)); // deficit allowed; fail-state handled elsewhere
        }
    }
}
