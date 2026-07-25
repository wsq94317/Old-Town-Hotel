using System.Collections.Generic;
using NUnit.Framework;

// 房间分配：walk-in 与预订共用的同一个打分函数（架构 §B.4 「绑定粒度」）。
// 断言的是"该不该给"的偏好次序，不是具体分值——分值只用于互相比较。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class RoomMatcherTest
    {
        private static RoomCandidate Room(int number, RoomTier band, float delivered = 0.35f,
                                          float appeal = 0f, bool flawed = false, bool sellable = true)
            => new RoomCandidate(number, band, delivered, appeal, flawed, sellable);

        private static RoomRequest Wants(RoomTier band, GuestSegment segment = GuestSegment.Budget)
            => new RoomRequest(band, segment);

        [Test]
        public void UnsellableRoom_IsNeverOffered()
        {
            var broken = Room(201, RoomTier.Basic, sellable: false);

            Assert.That(RoomMatcher.ScoreFor(Wants(RoomTier.Basic), broken),
                        Is.EqualTo(RoomMatcher.NoMatch), "床塌了的房不能卖，不管多合适");

            Assert.That(RoomMatcher.TryPick(Wants(RoomTier.Basic), new List<RoomCandidate> { broken },
                                            out int _), Is.False);
        }

        [Test]
        public void ExactBandWins_OverAnUpgrade()
        {
            var rooms = new List<RoomCandidate>
            {
                Room(201, RoomTier.Better, delivered: 0.80f),   // 更好，但客人只订了 Basic
                Room(202, RoomTier.Basic,  delivered: 0.35f),
            };

            Assert.That(RoomMatcher.TryPick(Wants(RoomTier.Basic), rooms, out int picked), Is.True);
            Assert.That(picked, Is.EqualTo(202),
                        "别把 Better 贱卖给订 Basic 的人——真正订 Better 的来了就没房了（库存保护）");
        }

        [Test]
        public void Upgrade_StillBeatsADowngrade_WhenNothingExactIsLeft()
        {
            var rooms = new List<RoomCandidate>
            {
                Room(201, RoomTier.Old,    delivered: 0.05f),
                Room(202, RoomTier.Better, delivered: 0.80f),
            };

            Assert.That(RoomMatcher.TryPick(Wants(RoomTier.Basic), rooms, out int picked), Is.True);
            Assert.That(picked, Is.EqualTo(202),
                        "没有对档的房时，升级远好过让客人住进比订单更差的房");
        }

        [Test]
        public void Downgrade_IsAllowedButIsTheLastResort()
        {
            var onlyWorse = new List<RoomCandidate> { Room(201, RoomTier.Old, delivered: 0.05f) };

            Assert.That(RoomMatcher.TryPick(Wants(RoomTier.Better), onlyWorse, out int picked), Is.True,
                        "降级不是禁止——总比把人赶走好，代价由满意度/退款链承担");
            Assert.That(picked, Is.EqualTo(201));

            Assert.That(RoomMatcher.ScoreFor(Wants(RoomTier.Better), Room(201, RoomTier.Old, 0.05f)),
                        Is.LessThan(RoomMatcher.ScoreFor(Wants(RoomTier.Better), Room(202, RoomTier.Better, 0.75f))));
        }

        [Test]
        public void BetterDelivery_WinsAmongEquallyBandedRooms()
        {
            var rooms = new List<RoomCandidate>
            {
                Room(201, RoomTier.Basic, delivered: 0.30f),
                Room(202, RoomTier.Basic, delivered: 0.50f),
            };

            Assert.That(RoomMatcher.TryPick(Wants(RoomTier.Basic), rooms, out int picked), Is.True);
            Assert.That(picked, Is.EqualTo(202), "同档之内挑交付更好的那间");
        }

        [Test]
        public void SegmentAppeal_BreaksTheTie()
        {
            var rooms = new List<RoomCandidate>
            {
                Room(201, RoomTier.Basic, delivered: 0.40f, appeal: 0f),
                Room(202, RoomTier.Basic, delivered: 0.40f, appeal: 0.12f),
            };

            Assert.That(RoomMatcher.TryPick(Wants(RoomTier.Basic, GuestSegment.Business), rooms,
                                            out int picked), Is.True);
            Assert.That(picked, Is.EqualTo(202), "家具对得上客群偏好的那间优先");
        }

        [Test]
        public void FlawedRoom_LosesToACleanOne_AndHurtsVipMost()
        {
            var rooms = new List<RoomCandidate>
            {
                Room(201, RoomTier.Basic, delivered: 0.40f, flawed: true),
                Room(202, RoomTier.Basic, delivered: 0.40f),
            };

            Assert.That(RoomMatcher.TryPick(Wants(RoomTier.Basic), rooms, out int picked), Is.True);
            Assert.That(picked, Is.EqualTo(202), "有干净房就别把没验的房推出去");

            var flawed = Room(201, RoomTier.Better, delivered: 0.75f, flawed: true);
            float vipLoss = RoomMatcher.ScoreFor(Wants(RoomTier.Better, GuestSegment.Vip),
                                                 Room(202, RoomTier.Better, 0.75f))
                          - RoomMatcher.ScoreFor(Wants(RoomTier.Better, GuestSegment.Vip), flawed);
            float budgetLoss = RoomMatcher.ScoreFor(Wants(RoomTier.Better, GuestSegment.Budget),
                                                    Room(202, RoomTier.Better, 0.75f))
                             - RoomMatcher.ScoreFor(Wants(RoomTier.Better, GuestSegment.Budget), flawed);

            Assert.That(vipLoss, Is.GreaterThan(budgetLoss),
                        "瑕疵房塞给 VIP 的代价更大——他们的差评权重是两倍");
        }

        [Test]
        public void PickIsStable_SameInputSamePick()
        {
            // 同分必须取房号小的：不稳定的分房让同种子对照实验全部失效
            var rooms = new List<RoomCandidate>
            {
                Room(207, RoomTier.Basic, delivered: 0.40f),
                Room(203, RoomTier.Basic, delivered: 0.40f),
                Room(205, RoomTier.Basic, delivered: 0.40f),
            };

            RoomMatcher.TryPick(Wants(RoomTier.Basic), rooms, out int first);
            rooms.Reverse();
            RoomMatcher.TryPick(Wants(RoomTier.Basic), rooms, out int second);

            Assert.That(first, Is.EqualTo(203));
            Assert.That(second, Is.EqualTo(first), "候选顺序变了，选出来的房不能变");
        }

        [Test]
        public void EmptyOrNullCandidates_ReturnFalse()
        {
            Assert.That(RoomMatcher.TryPick(Wants(RoomTier.Basic), new List<RoomCandidate>(), out int _),
                        Is.False);
            Assert.That(RoomMatcher.TryPick(Wants(RoomTier.Basic), null, out int _), Is.False);
        }
    }
}
