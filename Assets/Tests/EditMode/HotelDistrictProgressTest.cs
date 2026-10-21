using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    public class HotelDistrictProgressTest
    {
        [SetUp] public void Reset() => HotelEmpireProgress.ResetForNewGame();
        [TearDown] public void Cleanup() => HotelEmpireProgress.ResetForNewGame();
        private static HotelSim Build(int cash) => new HotelSim(new RoomLedger(new List<RoomDefinition> {
            new RoomDefinition(201,1,1,Room2DRoomCategory.Single,RoomTier.Old,RoomSimState.Ready)
        }), new StaffRoster(), RoomRateTable.Default, DemandConfig.Default, cash, 5);

        [Test]
        public void NewEmpireStartsAtHomeWithOnlyTheStationUnlocked()
        {
            Assert.That(HotelEmpireProgress.DistrictOwnedCount,Is.EqualTo(1));
            Assert.That(HotelEmpireProgress.NextAvailableSite.Id,Is.EqualTo("station"));
            Assert.That(HotelDistrictCatalog.Sites.Where(x=>!x.IsHome && HotelEmpireProgress.IsUnlocked(x)).Select(x=>x.Id),Is.EqualTo(new[]{"station"}));
        }

        [Test]
        public void LockedAndUnaffordableSitesCannotSpendCash()
        {
            var sim=Build(1000000); int cash=sim.Cash;
            Assert.That(HotelEmpireProgress.TryAcquireSite(sim,"grand",out _),Is.False);
            Assert.That(HotelEmpireProgress.TryAcquire(sim,PortfolioHotelKind.Business,out _),Is.False);
            Assert.That(sim.Cash,Is.EqualTo(cash));
            var poor=Build(24999);
            Assert.That(HotelEmpireProgress.TryAcquireSite(poor,"station",out _),Is.False);
            Assert.That(poor.Cash,Is.EqualTo(24999));
            Assert.That(HotelEmpireProgress.HotelCount,Is.EqualTo(1));
        }

        [Test]
        public void StationPurchaseChargesOnceAndUnlocksBothBranches()
        {
            var sim=Build(30000);
            Assert.That(HotelEmpireProgress.TryAcquireSite(sim,"station",out _),Is.True);
            Assert.That(sim.Cash,Is.EqualTo(5000));
            Assert.That(HotelEmpireProgress.TryAcquireSite(sim,"station",out _),Is.False);
            Assert.That(sim.Cash,Is.EqualTo(5000));
            Assert.That(HotelEmpireProgress.DailyIncome,Is.EqualTo(480));
            Assert.That(HotelEmpireProgress.IsUnlocked(HotelDistrictCatalog.Find("market")),Is.True);
            Assert.That(HotelEmpireProgress.IsUnlocked(HotelDistrictCatalog.Find("canal")),Is.True);
            Assert.That(HotelEmpireProgress.IsUnlocked(HotelDistrictCatalog.Find("grand")),Is.False);
        }

        [Test]
        public void GrandHotelNeedsBothBranchesAndDistrictCanBeCompleted()
        {
            var sim=Build(1000000);
            foreach(string id in new[]{"station","market","theatre"})
                Assert.That(HotelEmpireProgress.TryAcquireSite(sim,id,out _),Is.True);
            Assert.That(HotelEmpireProgress.TryAcquireSite(sim,"grand",out _),Is.False);
            foreach(string id in new[]{"canal","garden","grand"})
                Assert.That(HotelEmpireProgress.TryAcquireSite(sim,id,out _),Is.True);
            Assert.That(HotelEmpireProgress.DistrictOwnedCount,Is.EqualTo(7));
            Assert.That(HotelEmpireProgress.NextAvailableSite,Is.Null);
            Assert.That(HotelEmpireProgress.TryAcquireNextHotel(sim,out _),Is.False);
            Assert.That(sim.Cash,Is.EqualTo(350000));
        }

        [Test]
        public void PropertyIdentitySeasonAndBranchStateSurviveJsonReload()
        {
            var sim=Build(1000000);
            foreach(string id in new[]{"station","canal","garden"}) HotelEmpireProgress.TryAcquireSite(sim,id,out _);
            int income=HotelEmpireProgress.IncomeForDay(4);
            var world=new WorldState(); HotelEmpireProgress.CaptureTo(world);
            var json=JsonUtility.ToJson(world);
            HotelEmpireProgress.ResetForNewGame(); HotelEmpireProgress.RestoreFrom(JsonUtility.FromJson<WorldState>(json));
            Assert.That(HotelEmpireProgress.IsOwned("garden"),Is.True);
            Assert.That(HotelEmpireProgress.IsOwned("theatre"),Is.False);
            Assert.That(HotelEmpireProgress.IncomeForDay(4),Is.EqualTo(income));
            Assert.That(HotelEmpireProgress.IncomeAt("garden",4),Is.InRange(550,1150));
        }

        [Test]
        public void OldTypedPortfoliosKeepIncomeAndReceiveUniqueMapSites()
        {
            HotelEmpireProgress.RestoreFrom(new WorldState { portfolioKinds=new List<int>{2,3,1,2} });
            Assert.That(HotelEmpireProgress.HotelCount,Is.EqualTo(5));
            Assert.That(HotelEmpireProgress.DailyIncome,Is.EqualTo(2730));
            Assert.That(HotelEmpireProgress.IsOwned("canal"),Is.True);
            var captured=new WorldState(); HotelEmpireProgress.CaptureTo(captured);
            Assert.That(captured.portfolioSiteIds.Distinct().Count(),Is.EqualTo(4));
        }

        [Test]
        public void OldLargeCountPortfoliosKeepEveryHotelAndItsIncome()
        {
            HotelEmpireProgress.RestoreFrom(new WorldState { hotelCount=10 });
            Assert.That(HotelEmpireProgress.HotelCount,Is.EqualTo(10));
            Assert.That(HotelEmpireProgress.DailyIncome,Is.EqualTo(9*960));
            var world=new WorldState(); HotelEmpireProgress.CaptureTo(world);
            Assert.That(world.portfolioSiteIds.Distinct().Count(),Is.EqualTo(9));
            HotelEmpireProgress.RestoreFrom(JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(world)));
            Assert.That(HotelEmpireProgress.DailyIncome,Is.EqualTo(8640));
        }

        [Test]
        public void DuplicateOrMissingSavedIdsAreRepairedWithoutLosingProperties()
        {
            HotelEmpireProgress.RestoreFrom(new WorldState {
                portfolioKinds=new List<int>{1,1,2}, portfolioSiteIds=new List<string>{"station","station","missing"}
            });
            var world=new WorldState(); HotelEmpireProgress.CaptureTo(world);
            Assert.That(world.portfolioSiteIds,Is.EqualTo(new[]{"station","market","canal"}));
            Assert.That(HotelEmpireProgress.HotelCount,Is.EqualTo(4));
        }
    }
}
