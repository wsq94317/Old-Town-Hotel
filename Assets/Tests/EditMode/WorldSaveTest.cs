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

                // 调试全解锁开着时恢复结果被 OR 上去；关着时如实恢复——断言对常量成立
                Assert.That(FacilitySystem.GymUnlocked, Is.True);
                Assert.That(FacilitySystem.CasinoUnlocked, Is.EqualTo(FacilitySystem.DebugUnlockAll || false));
                Assert.That(FacilitySystem.PoolUnlocked, Is.EqualTo(FacilitySystem.DebugUnlockAll || false));

                var captured = new WorldState();
                FacilitySystem.CaptureTo(captured);
                Assert.That(captured.gymUnlocked, Is.EqualTo(FacilitySystem.GymUnlocked));
                Assert.That(captured.casinoUnlocked, Is.EqualTo(FacilitySystem.CasinoUnlocked));
            }
            finally
            {
                FacilitySystem.RestoreFrom(before);
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
