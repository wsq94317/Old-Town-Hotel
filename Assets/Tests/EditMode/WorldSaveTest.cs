using NUnit.Framework;
using UnityEngine;

// 存档 v3（世界层）：设施解锁 / 威望 / 胶带复发 / 锁房 的序列化与恢复。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class WorldSaveTest
    {
        [Test]
        public void WorldState_JsonRoundTrip_PreservesFields()
        {
            var gs = new GameState();
            gs.world.gymUnlocked = true;
            gs.world.casinoUnlocked = false;
            gs.world.poolUnlocked = true;
            gs.world.prestige = 7;
            gs.world.tapedBreakdowns.Add(new TapedBreakdownEntry { room = 203, x = 1.5f, y = 4f, z = -2f, kind = "LEAKY PIPE" });
            gs.world.tapedBreakdowns.Add(new TapedBreakdownEntry { room = -1, x = 0f, y = 24f, z = 0.5f, kind = "POOL FILTER JAM" });
            gs.world.lockedRooms.Add(305);

            var back = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(gs));

            Assert.That(back.version, Is.EqualTo(3));
            Assert.That(back.world.gymUnlocked, Is.True);
            Assert.That(back.world.casinoUnlocked, Is.False);
            Assert.That(back.world.poolUnlocked, Is.True);
            Assert.That(back.world.prestige, Is.EqualTo(7));
            Assert.That(back.world.tapedBreakdowns.Count, Is.EqualTo(2));
            Assert.That(back.world.tapedBreakdowns[0].room, Is.EqualTo(203));
            Assert.That(back.world.tapedBreakdowns[0].kind, Is.EqualTo("LEAKY PIPE"));
            Assert.That(back.world.tapedBreakdowns[1].room, Is.EqualTo(-1)); // 设施层损坏不挂房
            Assert.That(back.world.lockedRooms, Is.EquivalentTo(new[] { 305 }));
        }

        [Test]
        public void OldV2Save_LoadsWithWorldDefaults()
        {
            // 旧档（v2，无 world 字段）读入后 world 必须是非 null 的默认值，不能炸
            string v2Json = "{\"version\":2,\"economy\":{\"cash\":500,\"loanBalance\":0,\"loanRate\":0,\"staff\":[],\"reputationSamples\":[]},\"renovation\":{\"totalRooms\":12,\"startingRoomNumber\":101,\"rooms\":[],\"jobs\":[]},\"progress\":{\"day\":3,\"satisfaction\":10},\"rooms\":{\"occupied\":[]}}";

            var gs = JsonUtility.FromJson<GameState>(v2Json);

            Assert.That(gs.economy.cash, Is.EqualTo(500));
            Assert.That(gs.world, Is.Not.Null);
            Assert.That(gs.world.prestige, Is.EqualTo(0));
            Assert.That(gs.world.tapedBreakdowns, Is.Empty);
            Assert.That(gs.world.lockedRooms, Is.Empty);
        }

        [Test]
        public void FacilitySystem_CaptureRestore_RoundTrip_RespectsDebugUnlockAll()
        {
            var before = new WorldState();
            FacilitySystem.CaptureTo(before); // 保住测试前的静态状态
            try
            {
                var w = new WorldState { gymUnlocked = true, casinoUnlocked = false, poolUnlocked = false };
                FacilitySystem.RestoreFrom(w);

                // 属性 = 真解锁 OR 调试放行；断言对常量两种取值都成立
                Assert.That(FacilitySystem.GymUnlocked, Is.True);
                Assert.That(FacilitySystem.CasinoUnlocked, Is.EqualTo(FacilitySystem.DebugUnlockAll || false));
                Assert.That(FacilitySystem.PoolUnlocked, Is.EqualTo(FacilitySystem.DebugUnlockAll || false));

                // 存档只记"真解锁"——调试放行不入档（否则调试期存的档翻回正式版全场免费）
                var captured = new WorldState();
                FacilitySystem.CaptureTo(captured);
                Assert.That(captured.gymUnlocked, Is.True);
                Assert.That(captured.casinoUnlocked, Is.False);
                Assert.That(captured.poolUnlocked, Is.False);
            }
            finally
            {
                FacilitySystem.RestoreFrom(before);
            }
        }

        [Test]
        public void MiscIncome_BanksCash_WithoutFeedingStarRating()
        {
            var cfg = ScriptableObject.CreateInstance<EconomyConfigSO>();
            cfg.startingCash = 1000; cfg.startingLoan = 0; cfg.dailyInterestRate = 0f;
            var go = new GameObject("EconMisc");
            try
            {
                var econ = go.AddComponent<EconomySystem>();
                econ.InitializeForTest(cfg);
                int samplesBefore = econ.Reputation.ExportSamples().Count;

                econ.RecordCheckout(100, 1.5f);   // 一个真客人样本
                econ.RecordMiscIncome(80);        // 酒吧流水：进账不进星级
                var ledger = econ.CloseEconomicDay(0);

                Assert.That(ledger.Income, Is.EqualTo(150 + 80)); // 100*1.5 + 80
                Assert.That(econ.Reputation.ExportSamples().Count, Is.EqualTo(samplesBefore + 1)); // 只多一个样本

                // 兜底路径（没有退房记录）也不能吞掉杂项收入
                econ.RecordMiscIncome(30);
                var ledger2 = econ.CloseEconomicDay(0);
                Assert.That(ledger2.Income, Is.EqualTo(30));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void RenovationSystem_SyncRooms_RemapsToSceneRoomNumbers()
        {
            var renoCfg = ScriptableObject.CreateInstance<RenovationConfigSO>();
            var econCfg = ScriptableObject.CreateInstance<EconomyConfigSO>();
            econCfg.startingCash = 40000; econCfg.startingLoan = 0; econCfg.dailyInterestRate = 0f;
            var goEcon = new GameObject("EconSync");
            var goReno = new GameObject("RenoSync");
            try
            {
                var econ = goEcon.AddComponent<EconomySystem>();
                econ.InitializeForTest(econCfg);
                var reno = goReno.AddComponent<RenovationSystem>();
                reno.InitializeForTest(renoCfg, econ, 8, 101); // v2 场景的序列化默认：101-108
                reno.StartRenovation(new[] { 101 }, RoomTier.Basic);
                for (int i = 0; i < renoCfg.basicDays; i++) reno.AdvanceDay(); // 101 → Basic

                // 场景真实房号是酒店惯例 2xx/3xx（201 沿用旧 101 的档位，其余新房=Old，幽灵号剔除）
                reno.SyncRooms(new[] { 101, 201, 202, 301 }); // 101 还在场景里则保留

                Assert.That(reno.TotalRooms, Is.EqualTo(4));
                Assert.That(reno.TierOf(101), Is.EqualTo(RoomTier.Basic)); // 已有档位保留
                Assert.That(reno.TierOf(201), Is.EqualTo(RoomTier.Old));
                Assert.That(reno.StartRenovation(new[] { 201 }, RoomTier.Basic), Is.True); // 真房号可装修了
                Assert.That(reno.StartRenovation(new[] { 105 }, RoomTier.Basic), Is.False); // 幽灵号不吃钱
            }
            finally
            {
                Object.DestroyImmediate(goReno);
                Object.DestroyImmediate(goEcon);
                Object.DestroyImmediate(renoCfg);
                Object.DestroyImmediate(econCfg);
            }
        }

        [Test]
        public void ManagerReputation_Restore_SetsPrestige()
        {
            int before = ManagerReputation.Prestige;
            try
            {
                ManagerReputation.Restore(7);
                Assert.That(ManagerReputation.Prestige, Is.EqualTo(7));
                ManagerReputation.Restore(-3); // 负数钳到 0
                Assert.That(ManagerReputation.Prestige, Is.EqualTo(0));
            }
            finally
            {
                ManagerReputation.Restore(before);
            }
        }
    }
}
