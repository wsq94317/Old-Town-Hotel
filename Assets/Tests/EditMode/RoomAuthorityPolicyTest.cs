using NUnit.Framework;

// B1b 权威切分验收：Sim 管客人、v1 管清洁，每个房间每帧只能听一边的。
// 这些断言锁的是**关系**（谁盖过谁），不是具体状态值——两个死锁和一次
// 房费蒸发都出在这张表判错，所以每条规则都得有反例守着。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class RoomAuthorityPolicyTest
    {
        private static RoomSyncDirection Decide(RoomSimState sim, Room2DState legacy, bool hasStay,
                                                out Room2DState target) =>
            RoomAuthorityPolicy.Decide(sim, legacy, hasStay, out target);

        // ── 台账压过一切 ──────────────────────────────────────────────────────

        [Test]
        public void AGuestInTheLedger_AlwaysWinsOverBothStates()
        {
            // v1 说什么都不重要：台账里有人，房间就得是 Occupied。
            // （这条判错就是"同一间房卖两次、前一位客人房费蒸发"的成因）
            foreach (Room2DState legacy in System.Enum.GetValues(typeof(Room2DState)))
            {
                var dir = Decide(RoomSimState.Ready, legacy, hasStay: true, out Room2DState target);
                Assert.That(dir, Is.EqualTo(RoomSyncDirection.PushToLegacy), "legacy=" + legacy);
                Assert.That(target, Is.EqualTo(Room2DState.Occupied), "legacy=" + legacy);
            }
        }

        [Test]
        public void GhostGuestInLegacy_GetsEvicted_BecauseGuestsAreSimsCall()
        {
            // v1 觉得有人住，台账里没这人 = v1 自己发明的客人，必须清掉
            var dir = Decide(RoomSimState.Dirty, Room2DState.Occupied, hasStay: false, out Room2DState target);

            Assert.That(dir, Is.EqualTo(RoomSyncDirection.PushToLegacy));
            Assert.That(target, Is.EqualTo(Room2DState.Dirty), "按 Sim 的说法收口");
        }

        // ── 封房：Sim 判，v1 跟 ───────────────────────────────────────────────

        [Test]
        public void BrokenAndDerelictRooms_GetBlockedInLegacy_SoNobodyCleansThem()
        {
            foreach (var blocking in new[] { RoomSimState.Blocked, RoomSimState.Ruined })
            {
                var dir = Decide(blocking, Room2DState.Dirty, hasStay: false, out Room2DState target);
                Assert.That(dir, Is.EqualTo(RoomSyncDirection.PushToLegacy), blocking.ToString());
                Assert.That(target, Is.EqualTo(Room2DState.Blocked), blocking.ToString());
            }
        }

        [Test]
        public void RepairedRoom_UnblocksInsteadOfDeadlocking()
        {
            // 家具修好了：Sim 放行而 v1 还封着。这里**必须推**——
            // 读回来又变 Blocked，房间就永远解不开封。
            var dir = Decide(RoomSimState.Dirty, Room2DState.Blocked, hasStay: false, out Room2DState target);

            Assert.That(dir, Is.EqualTo(RoomSyncDirection.PushToLegacy), "读回来会死锁");
            Assert.That(target, Is.EqualTo(Room2DState.Dirty));
        }

        // ── 清洁链归 v1 ───────────────────────────────────────────────────────

        [Test]
        public void CleaningChain_IsLegacysJob_AndSimJustWatches()
        {
            // 管家的实体劳动：Sim 无权插手，否则刚打扫的进度会被抹掉
            var chain = new[]
            {
                Room2DState.Dirty, Room2DState.Cleaning,
                Room2DState.AwaitingInspection, Room2DState.Ready,
            };
            foreach (var legacy in chain)
            {
                var dir = Decide(RoomSimState.Dirty, legacy, hasStay: false, out _);
                if (legacy == Room2DState.Ready)
                    continue;   // 这一格另有规则（见下一条测试）
                Assert.That(dir, Is.EqualTo(RoomSyncDirection.PullFromLegacy), "legacy=" + legacy);
            }
        }

        [Test]
        public void CheckoutAtClosing_PushesDirtyOnce_SoCleaningActuallyStarts()
        {
            // 打烊结账把房弄脏了，v1 还显示可售。不推这一次的话，
            // 管家永远不知道有活干，脏房积压在 Sim 里没人管。
            var dir = Decide(RoomSimState.Dirty, Room2DState.Ready, hasStay: false, out Room2DState target);

            Assert.That(dir, Is.EqualTo(RoomSyncDirection.PushToLegacy));
            Assert.That(target, Is.EqualTo(Room2DState.Dirty));
        }

        [Test]
        public void CleanedRoom_StaysReady_WithoutBouncingBackToDirty()
        {
            // 管家清完 → v1 Ready、Sim 也 Ready：这时两边一致，读是无害的。
            // 反过来如果这里判成"推 Dirty"，房间会在 Ready/Dirty 之间抖动。
            var dir = Decide(RoomSimState.Ready, Room2DState.Ready, hasStay: false, out _);

            Assert.That(dir, Is.EqualTo(RoomSyncDirection.PullFromLegacy));
        }

        [Test]
        public void EveryCombination_ResolvesToExactlyOneDirection_NoUndefinedCells()
        {
            // 穷举整张表：任何组合都必须有明确裁决，且推的目标是合法状态。
            // 漏一格就是一个"某些房间偶尔卡住"的玄学 bug。
            foreach (RoomSimState sim in System.Enum.GetValues(typeof(RoomSimState)))
            foreach (Room2DState legacy in System.Enum.GetValues(typeof(Room2DState)))
            foreach (bool hasStay in new[] { false, true })
            {
                var dir = Decide(sim, legacy, hasStay, out Room2DState target);
                Assert.That(System.Enum.IsDefined(typeof(RoomSyncDirection), dir),
                            $"sim={sim} legacy={legacy} stay={hasStay}");
                if (dir == RoomSyncDirection.PushToLegacy)
                    Assert.That(System.Enum.IsDefined(typeof(Room2DState), target),
                                $"sim={sim} legacy={legacy} stay={hasStay}");
            }
        }
    }
}
