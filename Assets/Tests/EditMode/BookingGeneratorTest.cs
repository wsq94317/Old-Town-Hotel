using System.Collections.Generic;
using NUnit.Framework;

// 需求 → 订单意图（架构 §B.4）。掷骰注入，同种子可复现。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class BookingGeneratorTest
    {
        private static SegmentMix EvenMix => new SegmentMix(1f, 1f, 1f, 1f);

        /// <summary>固定序列的假掷骰：断言不该依赖真随机。</summary>
        private static System.Func<double> Rolls(params double[] values)
        {
            int i = 0;
            return () => values[i++ % values.Length];
        }

        // ── 需求切分 ──────────────────────────────────────────────────────────

        [Test]
        public void DemandSplits_IntoAdvanceBookingsAndWalkIns_WithoutLosingAnyone()
        {
            for (int demand = 0; demand <= 40; demand++)
            {
                int advance = BookingGenerator.AdvanceDemandFor(demand);
                int walkIn = BookingGenerator.WalkInDemandFor(demand);

                Assert.That(advance + walkIn, Is.EqualTo(demand),
                            $"需求 {demand} 切分后人数必须守恒——四舍五入不能凭空造人或吃人");
                Assert.That(advance, Is.GreaterThanOrEqualTo(0));
                Assert.That(walkIn, Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test]
        public void AdvanceBookings_AreTheMajority_WalkInsAreTheRemainder()
        {
            Assert.That(BookingGenerator.AdvanceDemandFor(100),
                        Is.GreaterThan(BookingGenerator.WalkInDemandFor(100)),
                        "提前预订是主流量，walk-in 只是剩余需求流");
        }

        // ── 只卖你挂出来的档位 ────────────────────────────────────────────────

        [Test]
        public void GuestsOnlyBookBandsTheHotelActuallyAdvertises()
        {
            var onlyOld = new List<RoomTier> { RoomTier.Old };

            foreach (GuestSegment segment in new[] { GuestSegment.Budget, GuestSegment.Business,
                                                     GuestSegment.Party, GuestSegment.Vip })
                Assert.That(BookingGenerator.PreferredBandFor(segment, onlyOld), Is.EqualTo(RoomTier.Old),
                            "全店只挂 Old 时，连 VIP 也只能订 Old——你只卖挂出来的东西");
        }

        [Test]
        public void HigherExpectationSegments_ReachForHigherBands()
        {
            var all = new List<RoomTier> { RoomTier.Old, RoomTier.Basic, RoomTier.Better };

            RoomTier budget = BookingGenerator.PreferredBandFor(GuestSegment.Budget, all);
            RoomTier vip = BookingGenerator.PreferredBandFor(GuestSegment.Vip, all);

            Assert.That((int)vip, Is.GreaterThan((int)budget), "VIP 订得比预算客高");
            Assert.That(budget, Is.EqualTo(RoomTier.Old), "预算客期待 0.08，最接近 Old 的 0.05");
            Assert.That(vip, Is.EqualTo(RoomTier.Better), "VIP 期待 0.70，正好是 Better");
        }

        [Test]
        public void NoBandsOffered_FallsBackToOld_RatherThanCrashing()
        {
            Assert.That(BookingGenerator.PreferredBandFor(GuestSegment.Vip, null), Is.EqualTo(RoomTier.Old));
            Assert.That(BookingGenerator.PreferredBandFor(GuestSegment.Vip, new List<RoomTier>()),
                        Is.EqualTo(RoomTier.Old));
        }

        // ── 住几晚 ────────────────────────────────────────────────────────────

        [Test]
        public void Nights_AreMostlyOne_ButBusinessTravellersStayLonger()
        {
            // roll 0.6：预算客还在"一晚"区间，商务客已经进"两晚"
            Assert.That(BookingGenerator.NightsFor(GuestSegment.Budget, 0.6d), Is.EqualTo(1));
            Assert.That(BookingGenerator.NightsFor(GuestSegment.Business, 0.6d), Is.EqualTo(2));

            Assert.That(BookingGenerator.NightsFor(GuestSegment.Budget, 0d), Is.EqualTo(1));
            Assert.That(BookingGenerator.NightsFor(GuestSegment.Vip, 1d), Is.EqualTo(3));
        }

        [Test]
        public void Nights_AreAlwaysAtLeastOne()
        {
            foreach (GuestSegment s in new[] { GuestSegment.Budget, GuestSegment.Business,
                                               GuestSegment.Party, GuestSegment.Vip })
                for (double r = 0d; r <= 1d; r += 0.1d)
                    Assert.That(BookingGenerator.NightsFor(s, r), Is.GreaterThanOrEqualTo(1),
                                "0 晚的单不占库存，是最难查的账目 bug");
        }

        // ── 摊成订单 ──────────────────────────────────────────────────────────

        [Test]
        public void Intents_SpreadAcrossChannels_ProportionalToTraffic()
        {
            var bands = new List<RoomTier> { RoomTier.Old };
            var intents = BookingGenerator.IntentsFor(targetDay: 5, advanceDemand: 40, EvenMix, bands,
                                                      Rolls(0.5d));

            var perChannel = new Dictionary<int, int>();
            foreach (var it in intents)
            {
                perChannel.TryGetValue(it.channelId, out int n);
                perChannel[it.channelId] = n + 1;
                Assert.That(it.arrivalDay, Is.EqualTo(5), "订单要落在目标日");
            }

            Assert.That(perChannel.ContainsKey(BookingChannels.PlatformAId), Is.True);
            Assert.That(perChannel[BookingChannels.PlatformAId],
                        Is.GreaterThan(perChannel[BookingChannels.PlatformCId]),
                        "流量权重高的渠道带来的单更多");
        }

        [Test]
        public void ZeroDemand_ProducesNothing()
        {
            var bands = new List<RoomTier> { RoomTier.Old };

            Assert.That(BookingGenerator.IntentsFor(5, 0, EvenMix, bands, Rolls(0.5d)), Is.Empty);
            Assert.That(BookingGenerator.IntentsFor(5, -3, EvenMix, bands, Rolls(0.5d)), Is.Empty);
        }

        [Test]
        public void NullRoll_IsHandledQuietly()
        {
            Assert.That(BookingGenerator.IntentsFor(5, 20, EvenMix,
                                                    new List<RoomTier> { RoomTier.Old }, null), Is.Empty);
        }

        [Test]
        public void SameRollSequence_ProducesIdenticalOrders()
        {
            var bands = new List<RoomTier> { RoomTier.Old, RoomTier.Basic };

            var first = BookingGenerator.IntentsFor(7, 25, EvenMix, bands, Rolls(0.1d, 0.4d, 0.7d, 0.9d));
            var second = BookingGenerator.IntentsFor(7, 25, EvenMix, bands, Rolls(0.1d, 0.4d, 0.7d, 0.9d));

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].channelId, Is.EqualTo(first[i].channelId));
                Assert.That(second[i].tier, Is.EqualTo(first[i].tier));
                Assert.That(second[i].nights, Is.EqualTo(first[i].nights));
                Assert.That(second[i].segment, Is.EqualTo(first[i].segment));
            }
        }
    }
}
