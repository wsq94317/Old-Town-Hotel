using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class WorldHudVisibilityPolicyTest
    {
        [TestCase(false, false, WorldHudMode.Hidden)]
        [TestCase(false, true, WorldHudMode.Hidden)]
        [TestCase(true, false, WorldHudMode.Operations)]
        [TestCase(true, true, WorldHudMode.MorningReport)]
        public void Resolve_NeverHidesAReadySimulationDuringMorningReport(
            bool simulationReady,
            bool awaitingMorningReport,
            WorldHudMode expected)
        {
            Assert.That(
                WorldHudVisibilityPolicy.Resolve(simulationReady, awaitingMorningReport),
                Is.EqualTo(expected));
        }
    }
}
