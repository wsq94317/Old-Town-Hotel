using NUnit.Framework;

public class IncidentDailyBudgetTest
{
    [SetUp]
    public void SetUp()
    {
        IncidentDailyBudget.ResetForTests();
    }

    [Test]
    public void DailyLimit_IsAlwaysOneOrTwo()
    {
        for (int day = 1; day <= 100; day++)
        {
            int limit = IncidentDailyBudget.LimitForDay(day);
            Assert.That(limit, Is.InRange(1, 2));
        }
    }

    [Test]
    public void Claims_CannotExceedDailyLimit()
    {
        int day = 7;
        int limit = IncidentDailyBudget.LimitForDay(day);

        for (int i = 0; i < limit; i++)
            Assert.That(IncidentDailyBudget.TryClaim(day, "incident_" + i), Is.True);

        Assert.That(IncidentDailyBudget.TryClaim(day, "overflow"), Is.False);
        Assert.That(IncidentDailyBudget.ClaimedForDay(day), Is.EqualTo(limit));
    }

    [Test]
    public void DuplicateClaim_IsIdempotent()
    {
        Assert.That(IncidentDailyBudget.TryClaim(3, "fire"), Is.True);
        Assert.That(IncidentDailyBudget.TryClaim(3, "fire"), Is.True);
        Assert.That(IncidentDailyBudget.ClaimedForDay(3), Is.EqualTo(1));
    }

    [Test]
    public void NewDay_ResetsClaims()
    {
        Assert.That(IncidentDailyBudget.TryClaim(4, "breakdown"), Is.True);
        Assert.That(IncidentDailyBudget.TryClaim(5, "daily_event"), Is.True);
        Assert.That(IncidentDailyBudget.ClaimedForDay(5), Is.EqualTo(1));
    }
}
