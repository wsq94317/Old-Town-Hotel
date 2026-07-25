using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 存档 v4 增量第一批：Sim 内核状态入档 + v2/v3 旧档读入不丢数据。
// 迁移策略是"逐期加字段 + 补默认值"，不是到后期一次性大爆炸迁移
// （本项目在存档口径上踩过 day+1 的坑，越晚改越危险）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class SimSaveMigrationTest
    {
        private static HotelSim BuildHotel(int seed = 555)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < 8; i++)
                defs.Add(new RoomDefinition(201 + i, 1, 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Housekeeper, "Ann", 60));
            staff.Register(new StaffMember(StaffRole.Inspector, "Ivan", 70));
            return new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                DemandConfig.Default, startingCash: 1500, rngSeed: seed);
        }

        [Test]
        public void SimState_RoundTripsThroughJson()
        {
            var sim = BuildHotel();
            sim.Pricing.DefaultTemplate = PriceTemplate.Squeeze;
            sim.Pricing.SetOverride(day: 6, PriceTemplate.Clearance);
            sim.Shifts.SetTier(StaffRole.Housekeeper, ShiftTier.Skeleton);
            sim.Safebox.Deposit(450);
            sim.Overflow.Add(200);
            sim.BeginDay();
            sim.RunToEndOfDay();

            var gs = new GameState();
            sim.CaptureTo(gs.sim);
            var back = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(gs));

            Assert.That(back.version, Is.EqualTo(GameState.CurrentVersion));
            Assert.That(back.sim.safeboxBalance, Is.EqualTo(450));
            Assert.That(back.sim.overflowBalance, Is.EqualTo(200));
            Assert.That(back.sim.defaultPriceTemplate, Is.EqualTo((int)PriceTemplate.Squeeze));
            Assert.That(back.sim.priceOverrides.Count, Is.EqualTo(1));
            Assert.That(back.sim.priceOverrides[0].day, Is.EqualTo(6));
            Assert.That(back.sim.staff.Count, Is.EqualTo(2));
            Assert.That(back.sim.nextStaffId, Is.GreaterThan(0), "id 计数器必须入档");
            Assert.That(back.sim.minute, Is.EqualTo(SimClock.DayEndMinute), "钟面停在打烊");
            Assert.That(back.sim.totalMinutesElapsed, Is.GreaterThan(0));
        }

        [Test]
        public void RestoreFrom_RebuildsClockMoneyPricingAndStaff()
        {
            var original = BuildHotel();
            original.Pricing.DefaultTemplate = PriceTemplate.Conservative;
            original.Pricing.SetOverride(3, PriceTemplate.Squeeze);
            original.Shifts.SetTier(StaffRole.Inspector, ShiftTier.Lean);
            original.Safebox.Deposit(700);
            original.Overflow.Add(120);
            original.BeginDay();
            original.RunToEndOfDay();
            original.SettleDay();
            original.CollectSafebox();

            var state = new SimState();
            original.CaptureTo(state);

            var restored = BuildHotel();
            restored.RestoreFrom(state);

            Assert.That(restored.Clock.CurrentDay, Is.EqualTo(original.Clock.CurrentDay));
            Assert.That(restored.Clock.CurrentMinute, Is.EqualTo(original.Clock.CurrentMinute));
            Assert.That(restored.Clock.TotalMinutesElapsed, Is.EqualTo(original.Clock.TotalMinutesElapsed));
            Assert.That(restored.Cash, Is.EqualTo(original.Cash));
            Assert.That(restored.Safebox.Balance, Is.EqualTo(original.Safebox.Balance));
            Assert.That(restored.Safebox.Level, Is.EqualTo(original.Safebox.Level));
            Assert.That(restored.Overflow.Balance, Is.EqualTo(original.Overflow.Balance));
            Assert.That(restored.Pricing.DefaultTemplate, Is.EqualTo(PriceTemplate.Conservative));
            Assert.That(restored.Pricing.TemplateFor(3), Is.EqualTo(PriceTemplate.Squeeze));
            Assert.That(restored.Shifts.TierOf(StaffRole.Inspector), Is.EqualTo(ShiftTier.Lean));
        }

        [Test]
        public void RestoredStaffIds_DoNotCollideWithNewHires()
        {
            var original = BuildHotel();
            original.BeginDay();
            var state = new SimState();
            original.CaptureTo(state);

            var restored = BuildHotel();
            restored.RestoreFrom(state);

            int newHireId = restored.Staff.Register(new StaffMember(StaffRole.Housekeeper, "NewGuy", 55));
            foreach (var e in restored.Staff.Entries)
                if (e.member.DisplayName != "NewGuy")
                    Assert.That(newHireId, Is.Not.EqualTo(e.staffId), "新雇的人不能复用历史 id");
        }

        [Test]
        public void StaffFatigueAndSlackState_SurviveTheRoundTrip()
        {
            var original = BuildHotel();
            original.Shifts.ApplyTo(original.Staff);
            var first = original.Staff.Entries[0];
            first.state = StaffOperationalState.Slacking;
            first.slackMinutesRemaining = 7;
            first.fatigue = 0.42f;

            var state = new SimState();
            original.CaptureTo(state);
            var restored = BuildHotel();
            restored.RestoreFrom(state);

            var back = restored.Staff.Entries[0];
            Assert.That(back.state, Is.EqualTo(StaffOperationalState.Slacking),
                        "摸鱼是 Sim 事实，得能存下来");
            Assert.That(back.slackMinutesRemaining, Is.EqualTo(7));
            Assert.That(back.fatigue, Is.EqualTo(0.42f).Within(1e-3f));
        }

        // ── 旧档迁移 ─────────────────────────────────────────────────────────

        [Test]
        public void V3Save_LoadsWithSimDefaults_WithoutLosingWorldData()
        {
            // v3 档：有 world 段（设施解锁/威望/胶带），没有 sim 段
            string v3 = "{\"version\":3," +
                "\"economy\":{\"cash\":900,\"loanBalance\":150000,\"loanRate\":0.0015,\"staff\":[],\"reputationSamples\":[]}," +
                "\"renovation\":{\"totalRooms\":12,\"startingRoomNumber\":201,\"rooms\":[],\"jobs\":[]}," +
                "\"progress\":{\"day\":9,\"satisfaction\":33}," +
                "\"rooms\":{\"occupied\":[]}," +
                "\"world\":{\"gymUnlocked\":true,\"casinoUnlocked\":false,\"poolUnlocked\":false,\"prestige\":6," +
                "\"tapedBreakdowns\":[{\"room\":203,\"x\":1,\"y\":4,\"z\":2,\"kind\":\"LEAKY PIPE\"}],\"lockedRooms\":[205]}}";

            var gs = JsonUtility.FromJson<GameState>(v3);
            gs.MigrateToCurrentVersion();

            Assert.That(gs.version, Is.EqualTo(GameState.CurrentVersion));
            // v3 数据一条不丢
            Assert.That(gs.economy.cash, Is.EqualTo(900));
            Assert.That(gs.economy.loanBalance, Is.EqualTo(150000));
            Assert.That(gs.world.prestige, Is.EqualTo(6));
            Assert.That(gs.world.gymUnlocked, Is.True);
            Assert.That(gs.world.tapedBreakdowns.Count, Is.EqualTo(1));
            Assert.That(gs.world.lockedRooms, Is.EquivalentTo(new[] { 205 }));
            Assert.That(gs.progress.day, Is.EqualTo(9));
            // sim 段补默认值，并用进度日号对齐钟面
            Assert.That(gs.sim, Is.Not.Null);
            Assert.That(gs.sim.day, Is.EqualTo(9), "读档即是那天早上");
            Assert.That(gs.sim.minute, Is.EqualTo(SimClock.DayStartMinute));
            Assert.That(gs.sim.safeboxLevel, Is.EqualTo(1));
            Assert.That(gs.sim.safeboxBalance, Is.EqualTo(0), "老档没有保险箱，空箱起步");
            Assert.That(gs.sim.staff, Is.Empty);
            Assert.That(gs.sim.settledDayIds, Is.Not.Null);
        }

        [Test]
        public void V2Save_LoadsWithBothWorldAndSimDefaults()
        {
            string v2 = "{\"version\":2," +
                "\"economy\":{\"cash\":500,\"loanBalance\":0,\"loanRate\":0,\"staff\":[],\"reputationSamples\":[]}," +
                "\"renovation\":{\"totalRooms\":12,\"startingRoomNumber\":101,\"rooms\":[],\"jobs\":[]}," +
                "\"progress\":{\"day\":3,\"satisfaction\":10}," +
                "\"rooms\":{\"occupied\":[{\"room\":103,\"stayQuality\":1,\"guestType\":0}]}}";

            var gs = JsonUtility.FromJson<GameState>(v2);
            gs.MigrateToCurrentVersion();

            Assert.That(gs.version, Is.EqualTo(GameState.CurrentVersion));
            Assert.That(gs.economy.cash, Is.EqualTo(500));
            Assert.That(gs.rooms.occupied.Count, Is.EqualTo(1), "v2 的过夜占用不能丢");
            Assert.That(gs.world, Is.Not.Null);
            Assert.That(gs.world.prestige, Is.EqualTo(0));
            Assert.That(gs.sim, Is.Not.Null);
            Assert.That(gs.sim.day, Is.EqualTo(3));
        }

        [Test]
        public void MigrationIsIdempotent()
        {
            var gs = new GameState();
            gs.sim.safeboxBalance = 321;
            gs.MigrateToCurrentVersion();
            gs.MigrateToCurrentVersion();

            Assert.That(gs.version, Is.EqualTo(GameState.CurrentVersion));
            Assert.That(gs.sim.safeboxBalance, Is.EqualTo(321), "重复迁移不该动已有数据");
        }

        [Test]
        public void SaveService_RoundTripsSimStateThroughDisk()
        {
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "oth_sim_v4_test.json");
            try
            {
                var gs = new GameState();
                gs.sim.safeboxBalance = 888;
                gs.sim.day = 12;
                gs.sim.cash = 77;
                SaveService.SaveTo(path, gs);

                var back = SaveService.LoadFrom(path);

                Assert.That(back, Is.Not.Null);
                Assert.That(back.version, Is.EqualTo(GameState.CurrentVersion));
                Assert.That(back.sim.safeboxBalance, Is.EqualTo(888));
                Assert.That(back.sim.day, Is.EqualTo(12));
                Assert.That(back.sim.cash, Is.EqualTo(77));
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }
    }
}
