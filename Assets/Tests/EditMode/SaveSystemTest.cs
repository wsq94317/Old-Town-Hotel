using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class SaveSystemTest
    {
        [Test]
        public void GameState_JsonRoundTrip_PreservesFields()
        {
            var gs = new GameState { version = 1 };
            gs.economy.cash = 1234;
            gs.economy.loanBalance = 90000;
            gs.economy.loanRate = 0.0015f;
            gs.economy.staff.Add(new StaffState
            {
                role = (int)StaffRole.Reception, name = "Liz", wage = 70,
                speed = 70, quality = 80, stamina = 60, education = 2, morale = 85,
                traits = { (int)StaffTrait.Charmer, (int)StaffTrait.Ambitious }
            });
            gs.renovation.totalRooms = 12;
            gs.renovation.startingRoomNumber = 101;
            gs.renovation.rooms.Add(new RoomTierEntry { room = 101, tier = (int)RoomTier.Basic });
            gs.renovation.jobs.Add(new RenoJobEntry { room = 102, targetTier = (int)RoomTier.Better, daysRemaining = 3 });
            gs.progress.day = 7;
            gs.progress.satisfaction = 42;

            string json = JsonUtility.ToJson(gs);
            var back = JsonUtility.FromJson<GameState>(json);

            Assert.That(back.economy.cash, Is.EqualTo(1234));
            Assert.That(back.economy.loanBalance, Is.EqualTo(90000));
            Assert.That(back.economy.loanRate, Is.EqualTo(0.0015f).Within(1e-6f));
            Assert.That(back.economy.staff.Count, Is.EqualTo(1));
            Assert.That(back.economy.staff[0].name, Is.EqualTo("Liz"));
            Assert.That(back.economy.staff[0].morale, Is.EqualTo(85));
            Assert.That(back.economy.staff[0].traits, Is.EquivalentTo(new[] { (int)StaffTrait.Charmer, (int)StaffTrait.Ambitious }));
            Assert.That(back.renovation.rooms.Single().tier, Is.EqualTo((int)RoomTier.Basic));
            Assert.That(back.renovation.jobs.Single().daysRemaining, Is.EqualTo(3));
            Assert.That(back.progress.day, Is.EqualTo(7));
            Assert.That(back.progress.satisfaction, Is.EqualTo(42));
        }

        [Test]
        public void EconomySystem_CaptureRestore_RoundTrip()
        {
            var cfg = ScriptableObject.CreateInstance<EconomyConfigSO>();
            cfg.startingCash = 2450;
            cfg.startingLoan = 183000;
            cfg.dailyInterestRate = 0.0015f;
            var goA = new GameObject("EconA");
            var goB = new GameObject("EconB");
            try
            {
                var a = goA.AddComponent<EconomySystem>();
                a.InitializeForTest(cfg);                 // seeds 3 default staff + loan + cash
                var liz = new StaffMember(StaffRole.Reception, "Liz", 60,
                                          new StaffAttributes(70, 80, 60), 2,
                                          new[] { StaffTrait.Charmer, StaffTrait.Ambitious });
                a.HireCandidate(liz);
                a.GiveRaise(liz, 70);                     // wage 60->70, morale 70->85

                var state = a.CaptureState();

                var b = goB.AddComponent<EconomySystem>();
                b.RestoreState(state);

                Assert.That(b.Cash, Is.EqualTo(a.Cash));
                Assert.That(b.Loan.Balance, Is.EqualTo(183000));
                Assert.That(b.Loan.DailyInterestRate, Is.EqualTo(0.0015f).Within(1e-6f));
                Assert.That(b.Payroll.Count, Is.EqualTo(a.Payroll.Count));

                var lizB = b.Payroll.Roster.First(m => m.DisplayName == "Liz");
                Assert.That(lizB.DailyWage, Is.EqualTo(70));
                Assert.That(lizB.Morale, Is.EqualTo(85));
                Assert.That(lizB.Attributes.Quality, Is.EqualTo(80));
                Assert.That(lizB.Traits.Select(t => (int)t), Is.EquivalentTo(new[] { (int)StaffTrait.Charmer, (int)StaffTrait.Ambitious }));
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void RenovationSystem_CaptureRestore_RoundTrip()
        {
            var renoCfg = ScriptableObject.CreateInstance<RenovationConfigSO>();
            var econCfg = ScriptableObject.CreateInstance<EconomyConfigSO>();
            econCfg.startingCash = 40000; econCfg.startingLoan = 0; econCfg.dailyInterestRate = 0f;
            var goEcon = new GameObject("Econ");
            var goA = new GameObject("RenoA");
            var goB = new GameObject("RenoB");
            try
            {
                var econ = goEcon.AddComponent<EconomySystem>();
                econ.InitializeForTest(econCfg);
                var a = goA.AddComponent<RenovationSystem>();
                a.InitializeForTest(renoCfg, econ, 4, 101);
                a.StartRenovation(new[] { 101 }, RoomTier.Basic); // 3 days
                a.StartRenovation(new[] { 102 }, RoomTier.Basic);
                for (int i = 0; i < renoCfg.basicDays; i++) a.AdvanceDay(); // 101,102 finish? both started same day
                // 101 & 102 now Basic; start 103 and leave it mid-flight
                a.StartRenovation(new[] { 103 }, RoomTier.Better);
                a.AdvanceDay(); // 103 has betterDays-1 left

                var state = a.CaptureState();

                var b = goB.AddComponent<RenovationSystem>();
                b.RestoreState(state);

                Assert.That(b.TierOf(101), Is.EqualTo(RoomTier.Basic));
                Assert.That(b.TierOf(102), Is.EqualTo(RoomTier.Basic));
                Assert.That(b.TierOf(104), Is.EqualTo(RoomTier.Old));
                Assert.That(b.RenovatedCount, Is.EqualTo(a.RenovatedCount));
                Assert.That(b.IsRenovating(103), Is.True);
                Assert.That(b.DaysRemaining(103), Is.EqualTo(a.DaysRemaining(103)));
                Assert.That(b.TotalRooms, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(goEcon);
                Object.DestroyImmediate(renoCfg);
                Object.DestroyImmediate(econCfg);
            }
        }

        [Test]
        public void SaveService_SaveTo_LoadFrom_TempFile()
        {
            string path = Path.Combine(Application.temporaryCachePath, "oth_save_test.json");
            try
            {
                var gs = new GameState();
                gs.economy.cash = 999;
                gs.progress.day = 5;
                SaveService.SaveTo(path, gs);
                Assert.That(File.Exists(path), Is.True);

                var back = SaveService.LoadFrom(path);
                Assert.That(back, Is.Not.Null);
                Assert.That(back.economy.cash, Is.EqualTo(999));
                Assert.That(back.progress.day, Is.EqualTo(5));

                Assert.That(SaveService.LoadFrom(Path.Combine(Application.temporaryCachePath, "nope_missing.json")), Is.Null);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
