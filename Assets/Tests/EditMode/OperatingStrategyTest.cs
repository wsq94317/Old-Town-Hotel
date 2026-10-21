using System.Collections.Generic;
using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class OperatingStrategyTest
    {
        private static HotelSim Build(int cash)
        {
            var sim = new HotelSim(new RoomLedger(new List<RoomDefinition> {
                new RoomDefinition(201, 1, 1, Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready)
            }), new StaffRoster(), RoomRateTable.Default, DemandConfig.Default, cash, 19);
            sim.FurnishInheritedRooms();
            return sim;
        }
        [Test]
        public void UnaffordablePackage_DoesNotPartiallyReplaceFurniture()
        {
            var sim = Build(5);
            int count = sim.Furniture.Count;
            Assert.That(sim.TryApplyRoomPackage(201, HotelOperatingStyle.Boutique, out _), Is.False);
            Assert.That(sim.Cash, Is.EqualTo(5));
            Assert.That(sim.Furniture.Count, Is.EqualTo(count));
        }
        [Test]
        public void BusinessPackage_ActuallyPlacesDeskAndImprovesDelivery()
        {
            var sim = Build(10000);
            float before = sim.DeliveredQualityOf(201);
            Assert.That(sim.TryApplyRoomPackage(201, HotelOperatingStyle.Business, out string reason), Is.True, reason);
            bool hasDesk = false;
            foreach (var item in sim.Furniture.InRoom(201)) if (item.kindId == FurnitureCatalog.WorkDesk) hasDesk = true;
            Assert.That(hasDesk, Is.True);
            Assert.That(sim.DeliveredQualityOf(201), Is.GreaterThan(before));
            Assert.That(sim.QuoteRoomPackage(201, HotelOperatingStyle.Business), Is.Zero);
        }
        [Test]
        public void StrategyAndWorkPrioritySurviveSave()
        {
            var sim = Build(10000);
            sim.ApplyOperatingStyle(HotelOperatingStyle.Boutique);
            sim.ClearRuinsFirst = true;
            var state = new SimState(); sim.CaptureTo(state);
            var copy = Build(10000); copy.RestoreFrom(state);
            Assert.That(copy.Pricing.DefaultTemplate, Is.EqualTo(PriceTemplate.Squeeze));
            Assert.That(copy.OperatingStyle, Is.EqualTo(HotelOperatingStyle.Boutique));
            Assert.That(copy.ClearRuinsFirst, Is.True);
        }
    }
}
