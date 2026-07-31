using System.Collections.Generic;
using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class HotelSimFrontDeskCoverageTest
    {
        [Test]
        public void UnattendedFrontDesk_KeepsArrivalsQueuedUntilCoverageReturns()
        {
            HotelSim sim = BuildHotel();
            sim.BeginDay();
            sim.Pipeline.ManagerOnFloor = true;
            sim.FrontDeskCoverageAvailable = false;

            AdvanceTo(sim, 18 * 60);

            Assert.That(sim.DeskQueueLength, Is.GreaterThan(0));
            Assert.That(sim.ArrivalsCheckedInToday, Is.Zero);
            Assert.That(sim.Rooms.CountOf(RoomSimState.Occupied), Is.Zero);
            Assert.That(sim.CurrentCheckInsPerHour, Is.Zero);

            sim.FrontDeskCoverageAvailable = true;
            AdvanceTo(sim, 19 * 60);

            Assert.That(sim.CurrentCheckInsPerHour, Is.GreaterThan(0f));
            Assert.That(sim.ArrivalsCheckedInToday, Is.GreaterThan(0));
            Assert.That(sim.Rooms.CountOf(RoomSimState.Occupied),
                Is.EqualTo(sim.ArrivalsCheckedInToday));
        }

        [Test]
        public void InterruptedDeskService_DoesNotBankGhostProcessingCredit()
        {
            HotelSim sim = BuildHotel(receptionSpeed: 50);
            sim.BeginDay();
            sim.Pipeline.ManagerOnFloor = true;
            sim.FrontDeskCoverageAvailable = false;
            AdvanceTo(sim, 18 * 60);
            Assert.That(sim.DeskQueueLength, Is.GreaterThan(0));

            sim.FrontDeskCoverageAvailable = true;
            float rate = sim.CurrentCheckInsPerHour;
            int almostFinishedMinutes = System.Math.Max(
                1, (int)System.Math.Floor(60f / rate) - 1);

            AdvanceBy(sim, almostFinishedMinutes);
            Assert.That(sim.ArrivalsCheckedInToday, Is.Zero);

            sim.FrontDeskCoverageAvailable = false;
            AdvanceBy(sim, 1);
            sim.FrontDeskCoverageAvailable = true;
            AdvanceBy(sim, almostFinishedMinutes);

            Assert.That(sim.ArrivalsCheckedInToday, Is.Zero,
                "返岗后必须重新完成一整次办理，不能继承无人柜台前的部分进度");

            int fullServiceMinutes = (int)System.Math.Ceiling(
                60f / sim.CurrentCheckInsPerHour) + 3;
            AdvanceBy(sim, fullServiceMinutes);
            Assert.That(sim.ArrivalsCheckedInToday, Is.GreaterThan(0));
        }

        private static HotelSim BuildHotel(int receptionSpeed = 55)
        {
            var rooms = new List<RoomDefinition>();
            for (int i = 0; i < 12; i++)
            {
                rooms.Add(new RoomDefinition(
                    201 + i, 1, 1, Room2DRoomCategory.Single,
                    RoomTier.Old, RoomSimState.Ready));
            }

            var staff = new StaffRoster();
            staff.Register(new StaffMember(
                StaffRole.Reception, "Desk Test", 65,
                new StaffAttributes(receptionSpeed, 55, 55), 1, null));

            var sim = new HotelSim(
                new RoomLedger(rooms), staff, RoomRateTable.Default,
                DemandConfig.Default, 2000, 481516);
            sim.FurnishInheritedRooms();
            return sim;
        }

        private static void AdvanceTo(HotelSim sim, int minute)
        {
            sim.Clock.FastForwardTo(minute);
            while (sim.Clock.TryConsumeTick())
                sim.StepMinute();
        }

        private static void AdvanceBy(HotelSim sim, int minutes) =>
            AdvanceTo(sim, sim.Clock.CurrentMinute + minutes);
    }
}
