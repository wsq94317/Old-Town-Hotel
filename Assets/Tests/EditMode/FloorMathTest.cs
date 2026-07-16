using NUnit.Framework;

// v2 世界层：楼层数学（层高 4，index 0/1/2 = 1F/2F/3F）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FloorMathTest
    {
        [Test]
        public void FloorIndexForY_MapsHeightBands()
        {
            Assert.AreEqual(0, FloorMath.FloorIndexForY(0f));
            Assert.AreEqual(0, FloorMath.FloorIndexForY(3.9f));
            Assert.AreEqual(1, FloorMath.FloorIndexForY(4f));
            Assert.AreEqual(1, FloorMath.FloorIndexForY(7.5f));
            Assert.AreEqual(2, FloorMath.FloorIndexForY(8f));
        }

        [Test]
        public void FloorIndexForY_ClampsOutOfRange()
        {
            Assert.AreEqual(0, FloorMath.FloorIndexForY(-2f));
            Assert.AreEqual(2, FloorMath.FloorIndexForY(99f));
        }

        [Test]
        public void BaseYFor_ReturnsFloorBase()
        {
            Assert.AreEqual(0f, FloorMath.BaseYFor(0), 0.0001f);
            Assert.AreEqual(4f, FloorMath.BaseYFor(1), 0.0001f);
            Assert.AreEqual(8f, FloorMath.BaseYFor(2), 0.0001f);
        }

        [Test]
        public void BaseYFor_ClampsIndex()
        {
            Assert.AreEqual(0f, FloorMath.BaseYFor(-1), 0.0001f);
            Assert.AreEqual(8f, FloorMath.BaseYFor(99), 0.0001f);
        }
    }
}
