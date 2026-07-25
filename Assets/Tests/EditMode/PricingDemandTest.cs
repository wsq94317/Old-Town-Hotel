using NUnit.Framework;

// 定价 = 策略模板 + 日期覆盖（玩家选策略，永不填数字）。
// 需求 = 弹性 × 星级 × 周末，并按价格档改变**客群混合**（修订版 3 取代一维 quality）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class PricingDemandTest
    {
        private static readonly RoomRateTable Rates = new RoomRateTable(old: 80, basic: 110, better: 190);

        // ── 定价模板 ─────────────────────────────────────────────────────────

        [Test]
        public void Templates_OrderPricesFromClearanceToSqueeze()
        {
            var policy = new PricingPolicy(Rates);

            policy.DefaultTemplate = PriceTemplate.Clearance;
            int clearance = policy.PriceFor(day: 1, RoomTier.Old);
            policy.DefaultTemplate = PriceTemplate.Conservative;
            int conservative = policy.PriceFor(day: 1, RoomTier.Old);
            policy.DefaultTemplate = PriceTemplate.Market;
            int market = policy.PriceFor(day: 1, RoomTier.Old);
            policy.DefaultTemplate = PriceTemplate.Squeeze;
            int squeeze = policy.PriceFor(day: 1, RoomTier.Old);

            Assert.That(clearance, Is.LessThan(conservative));
            Assert.That(conservative, Is.LessThan(market));
            Assert.That(market, Is.LessThan(squeeze));
            Assert.That(market, Is.EqualTo(80), "市场价=参考价本身（tier 表就是系统建议价）");
        }

        [Test]
        public void BetterTier_AlwaysPricesAboveOldTier()
        {
            var policy = new PricingPolicy(Rates) { DefaultTemplate = PriceTemplate.Market };
            Assert.That(policy.PriceFor(1, RoomTier.Better), Is.GreaterThan(policy.PriceFor(1, RoomTier.Basic)));
            Assert.That(policy.PriceFor(1, RoomTier.Basic), Is.GreaterThan(policy.PriceFor(1, RoomTier.Old)));
        }

        [Test]
        public void DateOverride_BeatsDefaultAndCanBeCleared()
        {
            var policy = new PricingPolicy(Rates) { DefaultTemplate = PriceTemplate.Market };

            policy.SetOverride(day: 5, PriceTemplate.Squeeze);

            Assert.That(policy.TemplateFor(5), Is.EqualTo(PriceTemplate.Squeeze));
            Assert.That(policy.TemplateFor(4), Is.EqualTo(PriceTemplate.Market), "只覆盖那一天");
            Assert.That(policy.PriceFor(5, RoomTier.Old), Is.GreaterThan(policy.PriceFor(4, RoomTier.Old)));

            policy.ClearOverride(5);
            Assert.That(policy.TemplateFor(5), Is.EqualTo(PriceTemplate.Market));
        }

        [Test]
        public void Weekend_CostsMoreThanWeekday()
        {
            var policy = new PricingPolicy(Rates) { DefaultTemplate = PriceTemplate.Market };
            int weekday = -1, weekend = -1;
            for (int day = 1; day <= 7; day++)
            {
                if (PricingPolicy.IsWeekend(day)) weekend = policy.PriceFor(day, RoomTier.Old);
                else weekday = policy.PriceFor(day, RoomTier.Old);
            }
            Assert.That(weekend, Is.GreaterThan(weekday), "周末溢价");
            Assert.That(weekday, Is.GreaterThan(0));
        }

        [Test]
        public void PriceRatio_IsOneAtMarket_AndTracksTemplate()
        {
            var policy = new PricingPolicy(Rates) { DefaultTemplate = PriceTemplate.Market };
            Assert.That(policy.PriceRatioFor(1), Is.EqualTo(1f).Within(1e-4f));

            policy.DefaultTemplate = PriceTemplate.Squeeze;
            Assert.That(policy.PriceRatioFor(1), Is.GreaterThan(1f));
            policy.DefaultTemplate = PriceTemplate.Clearance;
            Assert.That(policy.PriceRatioFor(1), Is.LessThan(1f));
        }

        // ── 需求弹性 ─────────────────────────────────────────────────────────

        [Test]
        public void Elasticity_CheaperFillsMore_PricierFillsLess()
        {
            Assert.That(DemandModel.Elasticity(1f), Is.EqualTo(1f).Within(1e-3f));
            Assert.That(DemandModel.Elasticity(0.7f), Is.GreaterThan(1.5f), "清仓价大幅拉需求");
            Assert.That(DemandModel.Elasticity(1.25f), Is.LessThan(0.8f), "榨利润明显压需求");
            Assert.That(DemandModel.Elasticity(0f), Is.GreaterThan(0f), "零价不该炸/无穷");
        }

        [Test]
        public void Arrivals_RespondToTemplateChoice()
        {
            var cfg = DemandConfig.Default;
            int cheap = DemandModel.ArrivalsFor(cfg, openRooms: 12, stars: 3f, priceRatio: 0.7f,
                                                weekend: false, roll: 0.5);
            int rich = DemandModel.ArrivalsFor(cfg, openRooms: 12, stars: 3f, priceRatio: 1.25f,
                                               weekend: false, roll: 0.5);

            Assert.That(cheap, Is.GreaterThan(rich), "改模板 → 客量按弹性变化（M-B 验收点）");
        }

        [Test]
        public void Arrivals_ScaleWithStarsAndWeekend()
        {
            var cfg = DemandConfig.Default;
            int lowStars = DemandModel.ArrivalsFor(cfg, 12, stars: 1f, priceRatio: 1f, weekend: false, roll: 0.5);
            int highStars = DemandModel.ArrivalsFor(cfg, 12, stars: 5f, priceRatio: 1f, weekend: false, roll: 0.5);
            int weekend = DemandModel.ArrivalsFor(cfg, 12, stars: 5f, priceRatio: 1f, weekend: true, roll: 0.5);

            Assert.That(highStars, Is.GreaterThan(lowStars), "评分→客源（正循环）");
            Assert.That(weekend, Is.GreaterThanOrEqualTo(highStars), "周末不少于平日");
        }

        [Test]
        public void Arrivals_NeverStarveBelowGuaranteedFloor()
        {
            // "位置极好：硬件再差也有基础客源，不会完全卖不出去"——防前期死局的保底
            var cfg = DemandConfig.Default;
            int worst = DemandModel.ArrivalsFor(cfg, openRooms: 12, stars: 0f, priceRatio: 2f,
                                                weekend: false, roll: 0.0);
            Assert.That(worst, Is.GreaterThanOrEqualTo(cfg.guaranteedArrivals));
            Assert.That(cfg.guaranteedArrivals, Is.GreaterThan(0));
        }

        [Test]
        public void Arrivals_AreCappedByOpenRooms_ButDemandCanExceedIt()
        {
            var cfg = DemandConfig.Default;
            int tinyHotel = DemandModel.ArrivalsFor(cfg, openRooms: 2, stars: 5f, priceRatio: 0.7f,
                                                    weekend: true, roll: 0.99);
            Assert.That(tinyHotel, Is.GreaterThan(0));
            Assert.That(tinyHotel, Is.LessThanOrEqualTo(cfg.MaxArrivalsFor(2)),
                        "需求可以超过房量，但到店人数有上限（超出部分是流失/超售，M-D 处理）");
        }

        [Test]
        public void Arrivals_AreDeterministicForSameRoll()
        {
            var cfg = DemandConfig.Default;
            int a = DemandModel.ArrivalsFor(cfg, 12, 3f, 1f, false, roll: 0.31);
            int b = DemandModel.ArrivalsFor(cfg, 12, 3f, 1f, false, roll: 0.31);
            Assert.That(a, Is.EqualTo(b));
        }

        // ── 客群风险混合（取代"低价=坏客人"一维标量） ──────────────────────────

        [Test]
        public void SegmentMix_WeightsAlwaysNormalise()
        {
            foreach (float ratio in new[] { 0.7f, 0.9f, 1f, 1.25f })
            foreach (bool weekend in new[] { false, true })
            {
                var mix = DemandModel.SegmentMixFor(ratio, weekend);
                float sum = mix.budget + mix.business + mix.party + mix.vip;
                Assert.That(sum, Is.EqualTo(1f).Within(1e-3f), $"ratio={ratio} weekend={weekend} 权重必须归一");
                Assert.That(mix.budget, Is.GreaterThanOrEqualTo(0f));
                Assert.That(mix.vip, Is.GreaterThanOrEqualTo(0f));
            }
        }

        [Test]
        public void ClearancePrice_BringsBudgetCrowd_NotJustBadReviews()
        {
            var cheap = DemandModel.SegmentMixFor(priceRatio: 0.7f, weekend: false);
            var pricey = DemandModel.SegmentMixFor(priceRatio: 1.25f, weekend: false);

            Assert.That(cheap.budget, Is.GreaterThan(pricey.budget), "清仓价吸引预算客");
            Assert.That(pricey.vip, Is.GreaterThan(cheap.vip), "榨利润吸引 VIP");
            // 关键：便宜不是"引来差评"，而是换来另一种经营压力
            Assert.That(GuestSegmentProfile.For(GuestSegment.Budget).extraCleaningLoad,
                        Is.GreaterThan(GuestSegmentProfile.For(GuestSegment.Business).extraCleaningLoad),
                        "预算客高周转 → 额外清洁负担（交换的是服务压力）");
        }

        [Test]
        public void Weekend_BringsThePartyCrowd()
        {
            var weekday = DemandModel.SegmentMixFor(1f, weekend: false);
            var weekend = DemandModel.SegmentMixFor(1f, weekend: true);
            Assert.That(weekend.party, Is.GreaterThan(weekday.party));
        }

        [Test]
        public void SegmentProfiles_EncodeDistinctPressures()
        {
            var budget = GuestSegmentProfile.For(GuestSegment.Budget);
            var business = GuestSegmentProfile.For(GuestSegment.Business);
            var party = GuestSegmentProfile.For(GuestSegment.Party);
            var vip = GuestSegmentProfile.For(GuestSegment.Vip);

            Assert.That(business.waitPenaltyMultiplier, Is.GreaterThan(budget.waitPenaltyMultiplier),
                        "商务客最恨排队");
            Assert.That(party.incidentMultiplier, Is.GreaterThan(business.incidentMultiplier),
                        "派对客更容易出噪音/损坏事件");
            Assert.That(party.spendMultiplier, Is.GreaterThan(budget.spendMultiplier),
                        "派对客设施消费高（这是它的甜头）");
            Assert.That(vip.satisfactionWeight, Is.GreaterThan(business.satisfactionWeight),
                        "VIP 的差评更致命");
            Assert.That(budget.tierExpectation, Is.LessThan(vip.tierExpectation),
                        "预算客容忍旧装修");
        }

        [Test]
        public void MixedSatisfaction_PenalisesTheWrongCrowdMoreHarshly()
        {
            // 同样等 20 分钟：商务客扣得比预算客狠
            float budgetHit = DemandModel.WaitSatisfactionPenalty(GuestSegment.Budget, waitMinutes: 20);
            float businessHit = DemandModel.WaitSatisfactionPenalty(GuestSegment.Business, waitMinutes: 20);

            Assert.That(businessHit, Is.GreaterThan(budgetHit));
            Assert.That(DemandModel.WaitSatisfactionPenalty(GuestSegment.Business, waitMinutes: 5),
                        Is.EqualTo(0f), "短等待不扣分（有宽容窗口）");
        }
    }
}
