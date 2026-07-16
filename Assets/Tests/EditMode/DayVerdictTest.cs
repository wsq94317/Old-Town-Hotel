using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DayVerdictTest
    {
        [Test]
        public void Verdict_TiersAreDistinct()
        {
            Assert.AreNotEqual(DayVerdictLogic.Line(500), DayVerdictLogic.Line(150));
            Assert.AreNotEqual(DayVerdictLogic.Line(150), DayVerdictLogic.Line(50));
            Assert.AreNotEqual(DayVerdictLogic.Line(50), DayVerdictLogic.Line(0));
            Assert.AreNotEqual(DayVerdictLogic.Line(0), DayVerdictLogic.Line(-50));
            Assert.AreNotEqual(DayVerdictLogic.Line(-50), DayVerdictLogic.Line(-500));
        }

        [Test]
        public void Verdict_BoundariesLandInRightTier()
        {
            StringAssert.Contains("MOGUL", DayVerdictLogic.Line(300));
            StringAssert.Contains("stapler", DayVerdictLogic.Line(100));
            StringAssert.Contains("Frame it", DayVerdictLogic.Line(1));
            StringAssert.Contains("bank", DayVerdictLogic.Line(-100));
        }
    }
}
