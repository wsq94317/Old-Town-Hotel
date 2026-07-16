using NUnit.Framework;

// v2 M2：跨层路线规划——员工/客人跨楼层走"到楼梯→瞬移"的逐层 hop。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FloorRouteTest
    {
        [Test]
        public void SameFloor_NoHops()
        {
            Assert.AreEqual(0, FloorNavigator.PlanHops(1, 1).Count);
        }

        [Test]
        public void OneUp_SingleHop()
        {
            var hops = FloorNavigator.PlanHops(0, 1);
            Assert.AreEqual(1, hops.Count);
            Assert.AreEqual((0, 1), hops[0]);
        }

        [Test]
        public void GroundToTop_TwoHops()
        {
            var hops = FloorNavigator.PlanHops(0, 2);
            Assert.AreEqual(2, hops.Count);
            Assert.AreEqual((0, 1), hops[0]);
            Assert.AreEqual((1, 2), hops[1]);
        }

        [Test]
        public void TopToGround_TwoHopsDown()
        {
            var hops = FloorNavigator.PlanHops(2, 0);
            Assert.AreEqual(2, hops.Count);
            Assert.AreEqual((2, 1), hops[0]);
            Assert.AreEqual((1, 0), hops[1]);
        }
    }
}
