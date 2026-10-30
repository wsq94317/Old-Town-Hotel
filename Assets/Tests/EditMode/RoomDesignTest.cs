using System.Collections.Generic;
using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class RoomDesignTest
    {
        private static HotelSim Hotel(int cash=20000)
        {
            var rooms=new RoomLedger(new[]{new RoomDefinition(201,1,1,Room2DRoomCategory.Single,RoomTier.Old,RoomSimState.Ready),new RoomDefinition(202,1,1,Room2DRoomCategory.Single,RoomTier.Old,RoomSimState.Ready)});
            var sim=new HotelSim(rooms,new StaffRoster(),RoomRateTable.Default,DemandConfig.Default,cash,77);sim.FurnishInheritedRooms();return sim;
        }
        [Test] public void InheritedRoomOpensWithACompleteAccessibleDraftWithoutSpending()
        {var s=Hotel();int money=s.Cash;var d=s.CreateRoomDesignDraft(201);Assert.That(RoomDesignGeometry.Validate(d.plan),Is.Empty);Assert.That(s.QuoteRoomDesign(d).NetCost,Is.Zero);Assert.That(s.Cash,Is.EqualTo(money));Assert.That(s.RoomDesigns.For(201),Is.Null);}
        [TestCase(1001)] [TestCase(1002)] [TestCase(1003)] [TestCase(1004)] [TestCase(1005)] [TestCase(1006)] [TestCase(1007)] [TestCase(1008)] [TestCase(1009)] [TestCase(1010)] [TestCase(1011)] [TestCase(1012)] [TestCase(1013)] [TestCase(1014)]
        public void EveryDecorativeItemHasAValidInitialPlacement(int kind)
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);Assert.That(s.TryAddRoomDecoration(d,kind,out _,out var why),Is.True,why);Assert.That(s.QuoteRoomDesign(d).valid,Is.True,s.QuoteRoomDesign(d).reason);}
        [Test] public void CancelledDraftHasNoEconomicOrFurnitureSideEffects()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);int cash=s.Cash,count=s.Furniture.Count;d.plan.wallStyle=5;s.TryAddRoomDecoration(d,1001,out _,out _);s.TryAddDesignFurniture(d,3,0,out _);Assert.That(s.Cash,Is.EqualTo(cash));Assert.That(s.Furniture.Count,Is.EqualTo(count));Assert.That(s.RoomDesigns.For(201),Is.Null);}
        [Test] public void ApplyingDesignChargesTheExactQuoteAndMarksInstallationForService()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=1;d.plan.floorStyle=1;d.plan.lighting=1;Assert.That(s.TryAddRoomDecoration(d,1001,out _,out _),Is.True);var q=s.QuoteRoomDesign(d);int before=s.Cash;Assert.That(q.NetCost,Is.EqualTo(310));Assert.That(s.TryApplyRoomDesign(d,out var why),Is.True,why);Assert.That(before-s.Cash,Is.EqualTo(q.NetCost));Assert.That(s.Rooms.At(201).state,Is.EqualTo(RoomSimState.Dirty));Assert.That(s.RoomDesigns.For(201).revision,Is.EqualTo(1));}
        [Test] public void InsufficientCashIsAtomic()
        {var s=Hotel(1);var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=2;int count=s.Furniture.Count;Assert.That(s.TryApplyRoomDesign(d,out _),Is.False);Assert.That(s.Cash,Is.EqualTo(1));Assert.That(s.Furniture.Count,Is.EqualTo(count));Assert.That(s.RoomDesigns.For(201),Is.Null);}
        [Test] public void ApplyingTheSameStaleDraftTwiceCannotChargeAgain()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=1;Assert.That(s.TryApplyRoomDesign(d,out _),Is.True);int cash=s.Cash;Assert.That(s.TryApplyRoomDesign(d,out _),Is.False);Assert.That(s.Cash,Is.EqualTo(cash));}
        [Test] public void ChangesToTheFurnitureLedgerInvalidateAnOpenDraft()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);Assert.That(s.TryBuyFurniture(201,2,out _),Is.True);int cash=s.Cash;Assert.That(s.TryApplyRoomDesign(d,out _),Is.False);Assert.That(s.Cash,Is.EqualTo(cash));}
        [Test] public void OccupiedRoomsCanBePreviewedAndSavedButCannotBeModified()
        {var s=Hotel();s.Rooms.SetState(201,RoomSimState.Occupied);var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=3;var q=s.QuoteRoomDesign(d);Assert.That(q.valid,Is.True);Assert.That(q.roomAvailable,Is.False);s.RoomDesigns.SaveBlueprint(0,"Warm",d.plan);Assert.That(s.RoomDesigns.Blueprints.Count,Is.EqualTo(1));Assert.That(s.TryApplyRoomDesign(d,out _),Is.False);}
        [Test] public void RearrangingWornFurnitureDoesNotRepairIt()
        {var s=Hotel();var bed=s.Furniture.InRoom(201)[0];bed.newness=.12f;bed.health=.18f;bed.faultLineIndex=0;var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=1;Assert.That(s.TryApplyRoomDesign(d,out _),Is.True);Assert.That(bed.health,Is.EqualTo(.18f));Assert.That(bed.newness,Is.EqualTo(.12f));Assert.That(bed.IsFaulted,Is.True);Assert.That(s.Rooms.At(201).state,Is.EqualTo(RoomSimState.Blocked));}
        [Test] public void ReplacedFurnitureMovesToStorageWithItsIdentityAndWear()
        {var s=Hotel();var old=s.Furniture.InRoom(201)[0];float health=old.health;var d=s.CreateRoomDesignDraft(201);Assert.That(s.TryAddDesignFurniture(d,2,0,out var why),Is.True,why);Assert.That(s.TryApplyRoomDesign(d,out why),Is.True,why);Assert.That(old.roomNumber,Is.Zero);Assert.That(old.health,Is.EqualTo(health));Assert.That(s.Furniture.InRoom(0),Does.Contain(old));}
        [Test] public void StoredFurnitureCanBeReusedWithoutBuyingItAgain()
        {var s=Hotel();var owned=s.Furniture.Place(0,40,newness:.23f,health:.3f);var d=s.CreateRoomDesignDraft(201);Assert.That(s.TryAddDesignFurniture(d,40,owned.instanceId,out var why),Is.True,why);Assert.That(s.QuoteRoomDesign(d).purchases,Is.Zero);Assert.That(s.TryApplyRoomDesign(d,out why),Is.True,why);Assert.That(owned.roomNumber,Is.EqualTo(201));Assert.That(owned.health,Is.EqualTo(.3f));}
        [Test] public void AnotherRoomsFurnitureCannotBeBorrowedBySpoofingAnId()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);d.plan.pieces[0].instanceId=s.Furniture.InRoom(202)[0].instanceId;Assert.That(s.TryApplyRoomDesign(d,out _),Is.False);}
        [Test] public void RepeatedIdenticalDecorDoesNotFarmTheDesignScore()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);s.TryAddRoomDecoration(d,1005,out _,out _);int score=RoomDesignLedger.Score(d.plan);s.TryAddRoomDecoration(d,1005,out _,out _);Assert.That(RoomDesignLedger.Score(d.plan),Is.EqualTo(score));}
        [Test] public void DecorationQualityBonusIsBoundedAndAbsentInLegacyRooms()
        {var s=Hotel();float before=s.DeliveredQualityOf(201);var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=5;d.plan.floorStyle=5;d.plan.lighting=3;s.TryAddRoomDecoration(d,1005,out _,out _);Assert.That(s.TryApplyRoomDesign(d,out _),Is.True);Assert.That(s.DeliveredQualityOf(201)-before,Is.InRange(0.001f,.08001f));Assert.That(s.RoomDesigns.QualityBonus(202),Is.Zero);}
        [Test] public void RoomDesignAndBlueprintsSurviveTheExistingSimSaveRoundTrip()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=2;d.plan.floorStyle=3;d.plan.lighting=2;s.TryAddRoomDecoration(d,1003,out _,out _);Assert.That(s.TryApplyRoomDesign(d,out _),Is.True);s.RoomDesigns.SaveBlueprint(2,"Business",s.RoomDesigns.For(201));var state=new SimState();s.CaptureTo(state);var loaded=Hotel();loaded.RestoreFrom(state);var p=loaded.RoomDesigns.For(201);Assert.That(p.wallStyle,Is.EqualTo(2));Assert.That(p.floorStyle,Is.EqualTo(3));Assert.That(p.pieces.Exists(x=>x.kindId==1003),Is.True);Assert.That(loaded.RoomDesigns.Blueprints[0].name,Is.EqualTo("Business"));Assert.That(loaded.Cash,Is.EqualTo(s.Cash));}
        [Test] public void ApplyingABlueprintReusesMatchingFurnitureWithoutDuplicatingOrRepairingIt()
        {var s=Hotel();var source=s.CreateRoomDesignDraft(201);source.plan.wallStyle=3;s.TryAddRoomDecoration(source,1005,out _,out _);s.RoomDesigns.SaveBlueprint(0,"Warm",source.plan);var target=s.CreateRoomDesignDraft(202);var bed=s.Furniture.InRoom(202)[0];float health=bed.health;s.ApplyDesignBlueprint(target,s.RoomDesigns.Blueprints[0].plan);Assert.That(s.QuoteRoomDesign(target).purchases,Is.EqualTo(65));Assert.That(s.TryApplyRoomDesign(target,out var why),Is.True,why);Assert.That(s.Furniture.InRoom(202).Count,Is.EqualTo(2));Assert.That(bed.health,Is.EqualTo(health));}
        [Test] public void WallPiecesSnapToFacesAndKeepTheDoorClear()
        {var p=new RoomDesignPiece{kindId=1005};RoomDesignGeometry.Place(p,0,-2.3f,0);Assert.That(p.z,Is.EqualTo(-2.25f));Assert.That(System.Math.Abs(p.x),Is.GreaterThan(1.2f));Assert.That(p.yaw,Is.EqualTo(180));}
        [Test] public void InvalidOverlapDoorAndOutOfBoundsPlansCannotCommit()
        {var s=Hotel();foreach(var xy in new[]{new[]{0f,0f},new[]{0f,-2.25f},new[]{3f,0f}}){var d=s.CreateRoomDesignDraft(201);d.plan.pieces[0].x=xy[0];d.plan.pieces[0].z=xy[1];Assert.That(s.TryApplyRoomDesign(d,out _),Is.False);}}
        [Test] public void NonFinitePoseIsRejectedWithoutSpending()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);d.plan.pieces[0].x=float.NaN;int cash=s.Cash;Assert.That(s.TryApplyRoomDesign(d,out _),Is.False);Assert.That(s.Cash,Is.EqualTo(cash));}
        [Test] public void SavingMoreThanFourBlueprintSlotsIsRejected()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);for(int i=0;i<7;i++)s.RoomDesigns.SaveBlueprint(i,"Plan",d.plan);Assert.That(s.RoomDesigns.Blueprints.Count,Is.EqualTo(4));}
        [Test] public void PreviewCannotClaimADesignCommission()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=1;d.plan.floorStyle=1;s.TryAddRoomDecoration(d,1001,out _,out _);s.TryAddRoomDecoration(d,1005,out _,out _);int cash=s.Cash;Assert.That(s.TryClaimDesignCommission(1,out _),Is.False);Assert.That(s.Cash,Is.EqualTo(cash));}
        [Test] public void CommissionRequiresCleanInspectedWorkingRoomAndPaysOnlyOnce()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=1;d.plan.floorStyle=1;s.TryAddRoomDecoration(d,1001,out _,out _);s.TryAddRoomDecoration(d,1005,out _,out _);Assert.That(s.TryApplyRoomDesign(d,out _),Is.True);Assert.That(s.TryClaimDesignCommission(2,out _),Is.False);s.Rooms.SetState(201,RoomSimState.Ready);int cash=s.Cash;Assert.That(s.TryClaimDesignCommission(2,out _),Is.True);Assert.That(s.Cash,Is.EqualTo(cash+180));Assert.That(s.TryClaimDesignCommission(2,out _),Is.False);Assert.That(s.Cash,Is.EqualTo(cash+180));}
        [Test] public void CommissionClaimSurvivesSaveAndCannotBeClaimedAgain()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);d.plan.wallStyle=5;s.TryAddRoomDecoration(d,1006,out _,out _);s.TryAddRoomDecoration(d,1004,out _,out _);Assert.That(s.TryApplyRoomDesign(d,out _),Is.True);s.Rooms.SetState(201,RoomSimState.Ready);Assert.That(s.TryClaimDesignCommission(4,out _),Is.True);var state=new SimState();s.CaptureTo(state);var loaded=Hotel();loaded.RestoreFrom(state);Assert.That(loaded.DesignCommissionClaimed(4),Is.True);Assert.That(loaded.TryClaimDesignCommission(4,out _),Is.False);}
        [Test] public void ReapplyingBlueprintWithSeveralOwnedDecorationsDoesNotDuplicateIdsOrRebuy()
        {var s=Hotel();var d=s.CreateRoomDesignDraft(201);foreach(int kind in new[]{1001,1005,1004,1002})Assert.That(s.TryAddRoomDecoration(d,kind,out _,out _),Is.True);Assert.That(s.TryApplyRoomDesign(d,out _),Is.True);var template=s.RoomDesigns.For(201).Clone();template.pieces.Reverse();var again=s.CreateRoomDesignDraft(201);s.ApplyDesignBlueprint(again,template);var q=s.QuoteRoomDesign(again);Assert.That(q.valid,Is.True,q.reason);Assert.That(q.purchases,Is.Zero);}
        [Test] public void CorruptSavedPosesAreDiscardedWithoutAffectingOtherRoomDesigns()
        {var s=Hotel();var good=s.CreateRoomDesignDraft(201).plan;var bad=s.CreateRoomDesignDraft(202).plan;bad.pieces[0].x=float.NaN;var ledger=new RoomDesignLedger();ledger.Restore(new System.Collections.Generic.List<RoomDesignPlan>{bad,good},null);Assert.That(ledger.For(201),Is.Not.Null);Assert.That(ledger.For(202),Is.Null);}
    }
}
