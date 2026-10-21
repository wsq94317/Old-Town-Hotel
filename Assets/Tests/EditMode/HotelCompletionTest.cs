using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class HotelCompletionTest
    {
        [Test]
        public void WarehouseExpansion_IsAtomicCappedAndSaved()
        {
            var rooms = new[]{new RoomDefinition(201,1,1,Room2DRoomCategory.Single,RoomTier.Old,RoomSimState.Ready)};
            var sim = new HotelSim(new RoomLedger(rooms),new StaffRoster(),RoomRateTable.Default,DemandConfig.Default,1000,1);
            sim.Warehouse.SetCapacity(40);sim.Materials.Add(12);
            Assert.That(sim.TryExpandWarehouse(out _),Is.False);
            Assert.That(sim.Cash,Is.EqualTo(1000));Assert.That(sim.Warehouse.Capacity,Is.EqualTo(40));
            sim.RecordCashCredit(500);Assert.That(sim.TryExpandWarehouse(out _),Is.True);
            Assert.That(sim.Cash,Is.Zero);Assert.That(sim.Warehouse.Capacity,Is.EqualTo(80));
            sim.RecordCashCredit(4000);Assert.That(sim.TryExpandWarehouse(out _),Is.True);
            sim.RecordCashCredit(9000);Assert.That(sim.TryExpandWarehouse(out _),Is.True);
            Assert.That(sim.Warehouse.Capacity,Is.EqualTo(400));Assert.That(sim.TryExpandWarehouse(out _),Is.False);
            var state=new SimState();sim.CaptureTo(state);sim.Warehouse.SetCapacity(40);sim.RestoreFrom(state);
            Assert.That(sim.Warehouse.Capacity,Is.EqualTo(400));Assert.That(sim.Materials.Stock,Is.EqualTo(12));
        }

        [Test]
        public void OneHundredRoomCampaign_SustainsTwelveDaysAndDeliversABatch()
        {
            var rooms = new System.Collections.Generic.List<RoomDefinition>();
            for (int floor=1;floor<=2;floor++) for(int n=1;n<=50;n++)
                rooms.Add(new RoomDefinition((floor+1)*100+n,floor,floor,Room2DRoomCategory.Single,
                    RoomTier.Old,floor==1 && n<=8 ? RoomSimState.Ready : RoomSimState.Ruined));
            var staff=new StaffRoster();
            foreach(var role in new[]{StaffRole.Reception,StaffRole.Housekeeper,StaffRole.Inspector})
                staff.Register(new StaffMember(role,role.ToString(),50,new StaffAttributes(60,60,60),1,null));
            var sim=new HotelSim(new RoomLedger(rooms),staff,RoomRateTable.Default,DemandConfig.Default,20000,101);
            sim.FurnishInheritedRooms(); sim.Materials.Add(20);
            Assert.That(sim.TryStartReclaim(ReclaimPlanKind.Refit,new[]{209,210,211,212},out var reason),Is.True,reason);
            int checkedIn=0,gross=0;
            for(int day=0;day<12;day++) {
                sim.BeginDay();sim.RunToEndOfDay();checkedIn+=sim.ArrivalsCheckedInToday;
                sim.SettleDay();gross+=sim.GrossIncomeToday;
                sim.CollectSafebox();sim.PayWages();sim.Clock.BeginNextDay();
            }
            Assert.That(sim.Rooms.Count,Is.EqualTo(100));
            Assert.That(sim.Rooms.OpenRoomCount,Is.EqualTo(12));
            Assert.That(checkedIn,Is.GreaterThan(0));Assert.That(gross,Is.GreaterThan(0));
            Assert.That(sim.Renovations.RoomsUnderRenovation,Is.Zero);
            Assert.That(sim.Cash,Is.GreaterThan(0));
        }

        [Test]
        public void ReopeningRequiresTheWholeHotelAndSoundFinances()
        {
            Assert.That(HotelCampaign.CanReopen(0,100,0,0,0,100,0,3), Is.True);
            Assert.That(HotelCampaign.CanReopen(1,100,0,0,0,100,0,3), Is.False);
            Assert.That(HotelCampaign.CanReopen(0,100,1,0,0,100,0,3), Is.False);
            Assert.That(HotelCampaign.CanReopen(0,100,0,1,0,100,0,3), Is.False);
            Assert.That(HotelCampaign.CanReopen(0,100,0,0,1,100,0,3), Is.False);
            Assert.That(HotelCampaign.CanReopen(0,100,0,0,0,99,0,3), Is.False);
            Assert.That(HotelCampaign.CanReopen(0,100,0,0,0,100,1,3), Is.False);
            Assert.That(HotelCampaign.CanReopen(0,100,0,0,0,100,0,2.9f), Is.False);
            Assert.That(HotelCampaign.CanReopen(0,24,0,0,0,24,0,4), Is.False);
        }

#if UNITY_5_3_OR_NEWER
        [Test]
        public void StaleSimulatorSafeAreaCannotShrinkTheGameView()
        {
            var stale = new UnityEngine.Rect(0,102,1170,2289);
            Assert.That(WorldManagementHud.ValidatedSafeArea(stale,540,960), Is.EqualTo(new UnityEngine.Rect(0,0,540,960)));
            var valid = new UnityEngine.Rect(0,30,540,900);
            Assert.That(WorldManagementHud.ValidatedSafeArea(valid,540,960), Is.EqualTo(valid));
        }
#endif
    }
}
