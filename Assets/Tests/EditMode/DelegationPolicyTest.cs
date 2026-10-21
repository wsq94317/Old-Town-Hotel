using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class DelegationPolicyTest
    {
        [TestCase(-100, 0)]
        [TestCase(0, 0)]
        [TestCase(3600, 840)]
        [TestCase(86400, 6720)]
        public void UnattendedTime_IsExplicitlyCapped(double seconds, int expected)
        {
            Assert.That(DelegationPolicy.MinutesFor(seconds), Is.EqualTo(expected));
        }
    }
}
