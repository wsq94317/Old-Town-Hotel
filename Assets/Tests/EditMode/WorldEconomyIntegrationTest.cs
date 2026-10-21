#if UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    public class WorldEconomyIntegrationTest
    {
        private GameObject _go;
        private EconomyConfigSO _config;
        [TearDown]
        public void Cleanup()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_config != null) Object.DestroyImmediate(_config);
            HotelEmpireProgress.ResetForNewGame();
        }
        private HotelSim Build(int cash)
        {
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "Desk", 50));
            var sim = new HotelSim(new RoomLedger(new List<RoomDefinition> {
                new RoomDefinition(201, 1, 1, Room2DRoomCategory.Single, RoomTier.Old, RoomSimState.Ready)
            }), staff, RoomRateTable.Default, DemandConfig.Default, cash, 5);
            sim.FurnishInheritedRooms();
            return sim;
        }
        private EconomySystem Bind(HotelSim sim)
        {
            _go = new GameObject("Economy integration test");
            var economy = _go.AddComponent<EconomySystem>();
            _config = ScriptableObject.CreateInstance<EconomyConfigSO>();
            economy.InitializeForTest(_config);
            economy.BindWorldSim(sim);
            return economy;
        }
        [Test]
        public void FacadeSpendingHiringAndIncomeShareOneLedger()
        {
            var sim = Build(1000); var economy = Bind(sim);
            Assert.That(economy.TrySpend(30), Is.True);
            Assert.That(economy.HireCandidate(new StaffMember(StaffRole.Housekeeper, "New", 50), 100), Is.True);
            economy.RecordMiscIncome(40);
            Assert.That(economy.Cash, Is.EqualTo(870));
            Assert.That(sim.Cash, Is.EqualTo(870));
            Assert.That(sim.MiscIncomeToday, Is.EqualTo(40));
            int debt = economy.Loan.Balance;
            economy.CloseEconomicDay(0);
            Assert.That(economy.Cash, Is.EqualTo(870));
            Assert.That(economy.Loan.Balance, Is.EqualTo(debt), "Only the world close capitalises interest.");
        }
        [Test]
        public void RepaymentUsesCashAndSafeExactlyOnce()
        {
            var sim = Build(50); sim.Safebox.Deposit(100);
            var economy = Bind(sim); economy.Loan.Borrow(1000);
            int debt = economy.Loan.Balance;
            Assert.That(economy.RepayLoan(80), Is.EqualTo(80));
            Assert.That(sim.Cash, Is.Zero);
            Assert.That(sim.Safebox.Balance, Is.EqualTo(70));
            Assert.That(economy.Loan.Balance, Is.EqualTo(debt - 80));
        }
        [Test]
        public void SnapshotSurvivesUnityJsonSerialization()
        {
            var sim = Build(1000); sim.BeginDay(); sim.RunToEndOfDay();
            var data = new SimState(); sim.CaptureTo(data);
            string json = JsonUtility.ToJson(data);
            var restored = Build(1000); restored.RestoreFrom(JsonUtility.FromJson<SimState>(json));
            Assert.That(restored.ActiveStayCount, Is.EqualTo(sim.ActiveStayCount));
            Assert.That(restored.UnsettledRoomRevenue, Is.EqualTo(sim.UnsettledRoomRevenue));
        }
        [Test]
        public void AcquiredPropertyKeepsItsTypeAndOperatingCostsAfterReload()
        {
            HotelEmpireProgress.ResetForNewGame();
            var sim = Build(100000);
            Assert.That(HotelEmpireProgress.TryAcquire(sim, PortfolioHotelKind.Business, out _), Is.True);
            Assert.That(sim.Cash, Is.EqualTo(65000));
            var world = new WorldState(); HotelEmpireProgress.CaptureTo(world);
            HotelEmpireProgress.ResetForNewGame(); HotelEmpireProgress.RestoreFrom(world);
            Assert.That(HotelEmpireProgress.HotelCount, Is.EqualTo(2));
            Assert.That(HotelEmpireProgress.DailyIncome, Is.EqualTo(700));
        }
        [Test]
        public void ForecastDeductsWagesCapitalisedInterestAndUpkeep()
        {
            var sim = Build(1000); sim.BeginDay();
            var forecast = HotelEconomyPresentation.Build(sim, dailyInterest: 92, dailyOperatingCosts: 25);
            int fees = Mathf.RoundToInt(forecast.ExpectedDailyGross * sim.CommissionRate);
            Assert.That(forecast.ExpectedDailyNet, Is.EqualTo(forecast.ExpectedDailyGross - fees
                - sim.Shifts.DailyWageCost(sim.Staff, 1) - 92 - 25));
        }
    }
}
#endif
