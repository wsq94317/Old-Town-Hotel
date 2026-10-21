using System.Collections.Generic;
using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class RuntimeCheckpointTest
    {
        private static HotelSim Build()
        {
            var rooms = new List<RoomDefinition>();
            for (int i = 0; i < 12; i++) rooms.Add(new RoomDefinition(201 + i, 1, 1,
                Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready));
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "Desk", 50));
            staff.Register(new StaffMember(StaffRole.Housekeeper, "Rooms", 45));
            var sim = new HotelSim(new RoomLedger(rooms), staff, RoomRateTable.Default,
                DemandConfig.Default, 2450, 424242);
            sim.FurnishInheritedRooms();
            return sim;
        }

        [Test]
        public void ReloadDuringBusiness_PreservesStaysQueuesAndSettledMoney()
        {
            var original = Build(); original.BeginDay();
            original.Clock.FastForwardTo(18 * 60);
            while (original.Clock.TryConsumeTick()) original.StepMinute();
            original.RecordMiscIncome(123);
            original.Breakdown.Add(ReputationCause.QueueWait, -0.2f);
            Assert.That(original.ActiveStayCount, Is.GreaterThan(0));
            var state = new SimState(); original.CaptureTo(state);
            var restored = Build(); restored.RestoreFrom(state);
            Assert.That(restored.Clock.CurrentMinute, Is.EqualTo(original.Clock.CurrentMinute));
            Assert.That(restored.ActiveStayCount, Is.EqualTo(original.ActiveStayCount));
            Assert.That(restored.UnsettledRoomRevenue, Is.EqualTo(original.UnsettledRoomRevenue));
            Assert.That(restored.DeskQueueLength, Is.EqualTo(original.DeskQueueLength));
            Assert.That(restored.GrossIncomeToday, Is.EqualTo(123));
            Assert.That(restored.ArrivalsCheckedInToday, Is.EqualTo(original.ArrivalsCheckedInToday));
            Assert.That(restored.Breakdown.TotalOf(ReputationCause.QueueWait), Is.EqualTo(-0.2f).Within(0.0001));
        }

        [Test]
        public void ReloadMorning_PreservesReportAndPayrollWithoutSettlingTwice()
        {
            var sim = Build(); sim.BeginDay(); sim.RunToEndOfDay();
            sim.SettleDay(); sim.Clock.BeginNextDay();
            var state = new SimState(); sim.CaptureTo(state);
            var restored = Build(); restored.RestoreFrom(state);
            Assert.That(restored.LastSettlement.NetProfit, Is.EqualTo(sim.LastSettlement.NetProfit));
            Assert.That(restored.Payroll.Owed, Is.EqualTo(sim.Payroll.Owed));
            Assert.That(restored.Safebox.Balance, Is.EqualTo(sim.Safebox.Balance));
            Assert.That(restored.WagesAccruedToday, Is.EqualTo(sim.WagesAccruedToday));
        }

        [Test]
        public void NightlyFatigue_RecoversOnceThroughThePipeline()
        {
            var sim = Build(); sim.BeginDay();
            foreach (var entry in sim.Staff.Entries) entry.fatigue = 0.9f;
            sim.SettleDay();
            foreach (var entry in sim.Staff.Entries)
                Assert.That(entry.fatigue, Is.EqualTo(SimMath.Clamp01(0.9f - StaffDayModel.FatigueRecovery())).Within(0.0001));
        }

        [Test]
        public void ReloadAfterBankPayment_DoesNotBecomeAnOverduePayment()
        {
            var sim = Build();
            sim.PayLoanInstallment(50);
            var state = new SimState(); sim.CaptureTo(state);
            var restored = Build(); restored.RestoreFrom(state);
            Assert.That(restored.Credit.PaidToday, Is.True);
            restored.BeginDay(loanPaymentWasDue: true);
            Assert.That(restored.Credit.MissedPayments, Is.Zero);
        }
    }
}
