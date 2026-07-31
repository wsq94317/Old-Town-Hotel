using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class HotelEconomyPresentationTest
    {
        [TearDown]
        public void TearDown()
        {
            HotelEmpireProgress.ResetForNewGame();
        }

        [Test]
        public void RoomRevenuePerMinute_UsesRealDayLength()
        {
            float result = 120f * 0.75f / HotelEconomyPresentation.RealMinutesPerGameDay;

            Assert.That(result, Is.EqualTo(0.75f).Within(0.001f));
        }

        [TestCase(900, 3f, 300)]
        [TestCase(0, 3f, 0)]
        [TestCase(900, 0f, 99999)]
        public void PaybackMinutes_HandlesNormalAndUnavailableReturns(
            int investment,
            float gainPerMinute,
            int expected)
        {
            Assert.That(
                HotelEconomyPresentation.PaybackMinutes(investment, gainPerMinute),
                Is.EqualTo(expected));
        }

        [Test]
        public void OpportunityScore_PrioritizesActiveLoss()
        {
            float expansion = HotelEconomyPresentation.OpportunityScore(
                900, 900, 1.5f, 0f, true, false);
            float stopLoss = HotelEconomyPresentation.OpportunityScore(
                350, 900, 0f, 2f, false, true);

            Assert.That(stopLoss, Is.GreaterThan(expansion));
        }

        [Test]
        public void OpportunityScore_FavorsAffordableInvestment()
        {
            float affordable = HotelEconomyPresentation.OpportunityScore(
                500, 600, 1f, 0f, false, false);
            float distant = HotelEconomyPresentation.OpportunityScore(
                500, 100, 1f, 0f, false, false);

            Assert.That(affordable, Is.GreaterThan(distant));
        }

        [Test]
        public void PortfolioIncome_UsesTheSameDayLengthAsTheHud()
        {
            HotelEmpireProgress.RestoreFrom(new WorldState { hotelCount = 3 });

            Assert.That(HotelEmpireProgress.PassiveIncomePerMinute, Is.EqualTo(16f));
            Assert.That(HotelEmpireProgress.DailyIncome, Is.EqualTo(1920));
        }
    }
}
