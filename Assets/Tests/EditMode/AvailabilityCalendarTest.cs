using NUnit.Framework;

// room-night 库存（架构 §B.4 修订版 3）。
//
// 这个类的存在理由就是**多晚住宿必须逐日各占一个间夜**。修订版 2 原本按
// "stayNights 减一次" 记账，一张 3 晚的单被减了三次容量却只算一天，
// 重复计数会凭空判出超售。所以这里的测试重点全在"覆盖哪些日子"上。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class AvailabilityCalendarTest
    {
        private const RoomTier Basic = RoomTier.Basic;

        private static AvailabilityCalendar WithCapacity(int perDay, int days = 14, int fromDay = 1)
        {
            var cal = new AvailabilityCalendar();
            for (int d = fromDay; d < fromDay + days; d++) cal.SetCapacity(d, Basic, perDay);
            return cal;
        }

        [Test]
        public void FreshCalendar_HasNoCapacityAndNothingFits()
        {
            var cal = new AvailabilityCalendar();

            Assert.That(cal.CapacityOn(5, Basic), Is.EqualTo(0));
            Assert.That(cal.RemainingOn(5, Basic), Is.EqualTo(0));
            Assert.That(cal.CanAccept(arrivalDay: 5, nights: 1, Basic), Is.False,
                        "没设容量的日子不能接单——默认拒绝，不默认放行");
        }

        [Test]
        public void ThreeNightStay_OccupiesThreeSeparateRoomNights()
        {
            var cal = WithCapacity(10);

            cal.Reserve(arrivalDay: 3, nights: 3, Basic);

            Assert.That(cal.DemandOn(2, Basic), Is.EqualTo(0), "到店前一天不占");
            Assert.That(cal.DemandOn(3, Basic), Is.EqualTo(1));
            Assert.That(cal.DemandOn(4, Basic), Is.EqualTo(1));
            Assert.That(cal.DemandOn(5, Basic), Is.EqualTo(1));
            Assert.That(cal.DemandOn(6, Basic), Is.EqualTo(0), "第 3 晚住完就退，第 6 天不占");

            // 每一天各减一间，而不是把 3 晚一次性从某一天减掉（修订版 2 的重复计数 bug）
            Assert.That(cal.RemainingOn(3, Basic), Is.EqualTo(9));
            Assert.That(cal.RemainingOn(5, Basic), Is.EqualTo(9));
        }

        [Test]
        public void Release_GivesTheRoomNightsBack()
        {
            var cal = WithCapacity(4);
            cal.Reserve(arrivalDay: 2, nights: 2, Basic);
            Assume.That(cal.RemainingOn(2, Basic), Is.EqualTo(3));

            cal.Release(arrivalDay: 2, nights: 2, Basic);

            Assert.That(cal.DemandOn(2, Basic), Is.EqualTo(0), "取消要回补库存");
            Assert.That(cal.DemandOn(3, Basic), Is.EqualTo(0));
            Assert.That(cal.RemainingOn(2, Basic), Is.EqualTo(4));
        }

        [Test]
        public void Release_NeverDrivesDemandNegative()
        {
            var cal = WithCapacity(4);

            cal.Release(arrivalDay: 2, nights: 5, Basic);   // 没订过就取消

            Assert.That(cal.DemandOn(2, Basic), Is.EqualTo(0), "需求不能被减成负数，否则容量凭空变多");
            Assert.That(cal.RemainingOn(2, Basic), Is.EqualTo(4));
        }

        [Test]
        public void CanAccept_RejectsWhenAnySingleNightIsFull()
        {
            var cal = new AvailabilityCalendar();
            cal.SetCapacity(1, Basic, 5);
            cal.SetCapacity(2, Basic, 5);
            cal.SetCapacity(3, Basic, 5);
            for (int i = 0; i < 5; i++) cal.Reserve(2, 1, Basic);   // 只把第 2 天填满

            Assert.That(cal.CanAccept(arrivalDay: 1, nights: 1, Basic), Is.True);
            Assert.That(cal.CanAccept(arrivalDay: 1, nights: 3, Basic), Is.False,
                        "整段里只要有一晚满了，整张连住单就接不了");
            Assert.That(cal.CanAccept(arrivalDay: 3, nights: 1, Basic), Is.True);
        }

        [Test]
        public void DeliberateOverbooking_LetsYouTakeMoreThanYouHave()
        {
            var cal = WithCapacity(3);
            for (int i = 0; i < 3; i++) cal.Reserve(4, 1, Basic);
            Assume.That(cal.CanAccept(4, 1, Basic), Is.False);

            Assert.That(cal.CanAccept(4, 1, Basic, overbookingAllowance: 2), Is.True,
                        "故意超售是玩家可调的档——赌取消率");
            Assert.That(cal.CanAccept(4, 1, Basic, overbookingAllowance: 0), Is.False);
        }

        [Test]
        public void BlockingRoomsForRenovation_CanOversellADayThatWasAlreadyBooked()
        {
            var cal = WithCapacity(6);
            for (int i = 0; i < 5; i++) cal.Reserve(7, 1, Basic);
            Assume.That(cal.IsOversold(7, Basic), Is.False);

            cal.SetCapacity(7, Basic, 2);   // 装修 Block 掉 4 间

            Assert.That(cal.RemainingOn(7, Basic), Is.EqualTo(-3));
            Assert.That(cal.IsOversold(7, Basic), Is.True,
                        "装修把已经卖掉的间夜 Block 掉 ⇒ 那天超售（这就是装修的真代价）");
            Assert.That(cal.OversoldCountOn(7, Basic), Is.EqualTo(3));
        }

        [Test]
        public void TiersAreIndependentInventories()
        {
            var cal = new AvailabilityCalendar();
            cal.SetCapacity(1, RoomTier.Old, 2);
            cal.SetCapacity(1, RoomTier.Basic, 2);

            cal.Reserve(1, 1, RoomTier.Old);
            cal.Reserve(1, 1, RoomTier.Old);

            Assert.That(cal.RemainingOn(1, RoomTier.Old), Is.EqualTo(0));
            Assert.That(cal.RemainingOn(1, RoomTier.Basic), Is.EqualTo(2),
                        "老房卖光不该影响 Basic 的库存");
        }

        [Test]
        public void ZeroOrNegativeNights_IsTreatedAsOneNight()
        {
            var cal = WithCapacity(4);

            cal.Reserve(arrivalDay: 3, nights: 0, Basic);

            Assert.That(cal.DemandOn(3, Basic), Is.EqualTo(1),
                        "0 晚的单没有意义，按 1 晚记——绝不能静默变成不占库存的白住单");
        }

        [Test]
        public void HorizonPruning_DropsPastDaysWithoutTouchingTheFuture()
        {
            var cal = WithCapacity(5, days: 20);
            cal.Reserve(3, 1, Basic);
            cal.Reserve(12, 1, Basic);

            cal.PruneBefore(10);

            Assert.That(cal.DemandOn(3, Basic), Is.EqualTo(0), "过去的日子清掉，不无限占内存");
            Assert.That(cal.DemandOn(12, Basic), Is.EqualTo(1), "未来的预订一条不能丢");
            Assert.That(cal.CapacityOn(12, Basic), Is.EqualTo(5));
        }
    }
}
