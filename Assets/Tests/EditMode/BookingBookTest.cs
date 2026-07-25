using System.Collections.Generic;
using NUnit.Framework;

// 预订簿（架构 §B.4）：逐单 Reservation + 渠道组合 + 每晨取消掷骰。
// 掷骰一律外部注入，概率玩法必须可复现（本项目铁律）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class BookingBookTest
    {
        private static BookingBook Book() => new BookingBook();

        private static int Add(BookingBook book, int arrivalDay = 3, int nights = 1,
                               RoomTier tier = RoomTier.Basic, int channelId = BookingChannels.DirectId,
                               int lockedPrice = 130, GuestSegment segment = GuestSegment.Budget)
            => book.Add(channelId, arrivalDay, nights, tier, lockedPrice, segment).id;

        // ── 身份与状态机 ──────────────────────────────────────────────────────

        [Test]
        public void ReservationIds_AreStableAndNeverReused()
        {
            var book = Book();
            int first = Add(book);
            int second = Add(book);
            book.Cancel(first);

            int third = Add(book);

            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(third, Is.Not.EqualTo(first), "取消掉的号不能回收——房记录里存着 occupantResvId");
            Assert.That(book.Find(first), Is.Not.Null, "取消不等于删除，晨报要列出来");
            Assert.That(book.Find(first).state, Is.EqualTo(ReservationState.Cancelled));
        }

        [Test]
        public void CheckIn_BindsTheRoomNumber_AndOnlyWorksOnce()
        {
            var book = Book();
            int id = Add(book);

            Assert.That(book.CheckIn(id, roomNumber: 204), Is.True);
            Assert.That(book.Find(id).assignedRoomNumber, Is.EqualTo(204));
            Assert.That(book.Find(id).state, Is.EqualTo(ReservationState.CheckedIn));

            Assert.That(book.CheckIn(id, roomNumber: 205), Is.False, "已入住的单不能再入一次");
            Assert.That(book.Find(id).assignedRoomNumber, Is.EqualTo(204), "房号不能被第二次调用改掉");
        }

        [Test]
        public void CancelledReservation_CannotCheckIn()
        {
            var book = Book();
            int id = Add(book);
            book.Cancel(id);

            Assert.That(book.CheckIn(id, 204), Is.False);
        }

        [Test]
        public void UnknownId_IsHandledQuietly()
        {
            var book = Book();

            Assert.That(book.Find(9999), Is.Null);
            Assert.That(book.Cancel(9999), Is.False);
            Assert.That(book.CheckIn(9999, 204), Is.False);
        }

        // ── 按日查询（晨报/库存重算都靠它）────────────────────────────────────

        [Test]
        public void ArrivalsFor_ListsOnlyLiveBookingsOfThatDay()
        {
            var book = Book();
            int today = Add(book, arrivalDay: 5);
            int alsoToday = Add(book, arrivalDay: 5);
            Add(book, arrivalDay: 6);
            book.Cancel(alsoToday);

            var arrivals = book.ArrivalsFor(5);

            Assert.That(arrivals.Count, Is.EqualTo(1));
            Assert.That(arrivals[0].id, Is.EqualTo(today));
        }

        [Test]
        public void RebuildCalendarDemand_CountsEveryNightOfEveryLiveStay()
        {
            var book = Book();
            Add(book, arrivalDay: 2, nights: 3);           // 占 2/3/4
            int doomed = Add(book, arrivalDay: 3, nights: 1);
            book.Cancel(doomed);

            var cal = new AvailabilityCalendar();
            for (int d = 1; d <= 6; d++) cal.SetCapacity(d, RoomTier.Basic, 10);
            book.RebuildCalendarDemand(cal);

            Assert.That(cal.DemandOn(2, RoomTier.Basic), Is.EqualTo(1));
            Assert.That(cal.DemandOn(3, RoomTier.Basic), Is.EqualTo(1), "取消的那张不该再占间夜");
            Assert.That(cal.DemandOn(4, RoomTier.Basic), Is.EqualTo(1));
            Assert.That(cal.DemandOn(5, RoomTier.Basic), Is.EqualTo(0));
        }

        [Test]
        public void RebuildCalendarDemand_IsIdempotent()
        {
            var book = Book();
            Add(book, arrivalDay: 2, nights: 2);
            var cal = new AvailabilityCalendar();
            for (int d = 1; d <= 6; d++) cal.SetCapacity(d, RoomTier.Basic, 10);

            book.RebuildCalendarDemand(cal);
            book.RebuildCalendarDemand(cal);

            Assert.That(cal.DemandOn(2, RoomTier.Basic), Is.EqualTo(1),
                        "重算是覆盖不是累加——每晨都要重算一次，累加会滚雪球");
        }

        // ── 取消掷骰 ──────────────────────────────────────────────────────────

        [Test]
        public void CancellationRoll_OnlyTouchesFutureBookings()
        {
            var book = Book();
            int arrivingToday = Add(book, arrivalDay: 5);
            int future = Add(book, arrivalDay: 8);

            // roll 恒 0 = 必然取消
            var cancelled = book.RollCancellations(today: 5, () => 0d);

            Assert.That(cancelled, Contains.Item(future));
            Assert.That(cancelled, Has.No.Member(arrivingToday),
                        "今天就到店的单不再掷取消——那是 no-show，另一套处置");
        }

        [Test]
        public void CancellationRate_FollowsTheChannel()
        {
            // 渠道 A 取消率高（流量大但不靠谱），直营最低——roll 恰好落在两者之间
            var profileA = BookingChannels.Get(BookingChannels.PlatformAId);
            var direct = BookingChannels.Get(BookingChannels.DirectId);
            Assume.That(profileA.cancellationRate, Is.GreaterThan(direct.cancellationRate));

            double between = (profileA.cancellationRate + direct.cancellationRate) * 0.5d;

            var book = Book();
            int viaA = Add(book, arrivalDay: 9, channelId: BookingChannels.PlatformAId);
            int viaDirect = Add(book, arrivalDay: 9, channelId: BookingChannels.DirectId);

            var cancelled = book.RollCancellations(today: 5, () => between);

            Assert.That(cancelled, Contains.Item(viaA), "高取消率渠道先崩");
            Assert.That(cancelled, Has.No.Member(viaDirect), "直营单更稳");
        }

        [Test]
        public void CancellationRoll_DoesNotReCancelOrTouchCheckedIn()
        {
            var book = Book();
            int already = Add(book, arrivalDay: 9);
            book.Cancel(already);
            int staying = Add(book, arrivalDay: 9);
            book.CheckIn(staying, 204);

            var cancelled = book.RollCancellations(today: 5, () => 0d);

            Assert.That(cancelled, Is.Empty, "已取消/已入住的单不参与掷骰");
        }

        // ── 渠道表 ────────────────────────────────────────────────────────────

        [Test]
        public void Channels_TradeCommissionAgainstTraffic()
        {
            var a = BookingChannels.Get(BookingChannels.PlatformAId);
            var c = BookingChannels.Get(BookingChannels.PlatformCId);

            Assert.That(a.commission, Is.GreaterThan(c.commission), "流量大的抽成也高");
            Assert.That(a.trafficWeight, Is.GreaterThan(c.trafficWeight));
            Assert.That(BookingChannels.Get(BookingChannels.DirectId).commission, Is.EqualTo(0f),
                        "直营不抽成——这是提升口碑的回报");
        }

        [Test]
        public void UnknownChannel_FallsBackToDirect_RatherThanCrashing()
        {
            var unknown = BookingChannels.Get(4242);

            Assert.That(unknown.id, Is.EqualTo(BookingChannels.DirectId));
        }

        // ── 归档（不让簿子无限长）─────────────────────────────────────────────

        [Test]
        public void Prune_DropsFinishedStaysButKeepsAnythingStillLive()
        {
            var book = Book();
            int old = Add(book, arrivalDay: 1, nights: 2);      // 住到第 2 天
            book.CheckIn(old, 204);
            book.Complete(old);
            int stillIn = Add(book, arrivalDay: 1, nights: 30);
            book.CheckIn(stillIn, 205);
            int future = Add(book, arrivalDay: 20);

            book.PruneCompletedBefore(10);

            Assert.That(book.Find(old), Is.Null);
            Assert.That(book.Find(stillIn), Is.Not.Null, "还住着的单不能归档掉");
            Assert.That(book.Find(future), Is.Not.Null);
        }
    }
}
