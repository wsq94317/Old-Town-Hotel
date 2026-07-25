using System.Collections.Generic;
using NUnit.Framework;

// 装修系统（核心长线玩法）：三方案取舍、批量折扣 vs 工期拉长、材料消耗。
// 真正的代价不是造价，是"这几间房这几天不能卖"。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class RenovationPlanTest
    {
        [Test]
        public void ThreePlans_FormARealTradeoff()
        {
            var economy = RenovationPlan.For(RenovationPlanKind.Economy);
            var standard = RenovationPlan.For(RenovationPlanKind.Standard);
            var luxury = RenovationPlan.For(RenovationPlanKind.Luxury);

            Assert.That(economy.cashPerRoom, Is.LessThan(standard.cashPerRoom), "经济方案更便宜");
            Assert.That(economy.blockDays, Is.GreaterThan(standard.blockDays),
                        "但房间关得更久——省钱换工期");
            Assert.That(economy.targetTier, Is.EqualTo(standard.targetTier),
                        "经济与标准到达同一档位，差别只在钱与时间");

            Assert.That(luxury.targetTier, Is.EqualTo(RoomTier.Better), "豪华直接到顶档");
            Assert.That(luxury.cashPerRoom, Is.GreaterThan(standard.cashPerRoom));
            Assert.That(luxury.materialsPerRoom, Is.GreaterThan(standard.materialsPerRoom));
        }

        [Test]
        public void BatchDiscount_RewardsVolume_ButIsCapped()
        {
            var plan = RenovationPlan.For(RenovationPlanKind.Economy);

            Assert.That(RenovationPricing.DiscountFor(1), Is.EqualTo(0f), "单间没折扣");
            Assert.That(RenovationPricing.DiscountFor(5), Is.GreaterThan(0f));
            Assert.That(RenovationPricing.DiscountFor(50), Is.EqualTo(RenovationPricing.MaxDiscount),
                        "折扣有上限，不能无限刷");

            int onePer = RenovationPricing.CashPerRoomFor(plan, 1);
            int tenPer = RenovationPricing.CashPerRoomFor(plan, 10);
            Assert.That(tenPer, Is.LessThan(onePer), "批量单价更低（玩家该算这笔账）");
        }

        [Test]
        public void BatchCost_ScalesWithRoomCount_AndTotalStillRises()
        {
            var plan = RenovationPlan.For(RenovationPlanKind.Standard);
            int one = RenovationPricing.CashCostFor(plan, 1);
            int five = RenovationPricing.CashCostFor(plan, 5);

            Assert.That(five, Is.GreaterThan(one), "单价降了但总额仍然更大——一次性现金压力是真的");
            Assert.That(five, Is.LessThan(one * 5), "确实打了折");
            Assert.That(RenovationPricing.CashCostFor(plan, 0), Is.EqualTo(0));
        }

        [Test]
        public void Materials_DoNotGetADiscount()
        {
            var plan = RenovationPlan.For(RenovationPlanKind.Luxury);
            Assert.That(RenovationPricing.MaterialCostFor(plan, 4),
                        Is.EqualTo(plan.materialsPerRoom * 4), "材料按件算，没有批量折扣");
        }

        [Test]
        public void BiggerBatch_TakesLongerToFinish()
        {
            var plan = RenovationPlan.For(RenovationPlanKind.Standard);
            int oneRoom = RenovationPricing.BlockDaysFor(plan, 1);
            int eightRooms = RenovationPricing.BlockDaysFor(plan, 8);

            Assert.That(oneRoom, Is.EqualTo(plan.blockDays));
            Assert.That(eightRooms, Is.GreaterThan(oneRoom),
                        "一支施工队干不完那么多房——批量的隐性代价");
        }

        [Test]
        public void Queue_TracksRoomsAndFinishesOnSchedule()
        {
            var queue = new SimRenovationQueue();
            var plan = RenovationPlan.For(RenovationPlanKind.Standard); // 2 天
            int jobId = queue.Enqueue(plan, new List<int> { 201, 202 });

            Assert.That(jobId, Is.GreaterThan(0));
            Assert.That(queue.RoomsUnderRenovation, Is.EqualTo(2));
            Assert.That(queue.IsRenovating(201), Is.True);
            Assert.That(queue.IsRenovating(999), Is.False);
            Assert.That(queue.DaysRemainingFor(201), Is.EqualTo(2));

            Assert.That(queue.TickDay(), Is.Empty, "第一天还没完工");
            Assert.That(queue.DaysRemainingFor(201), Is.EqualTo(1));

            var finished = queue.TickDay();
            Assert.That(finished.Count, Is.EqualTo(1));
            Assert.That(finished[0].roomNumbers, Is.EquivalentTo(new[] { 201, 202 }));
            Assert.That(finished[0].targetTier, Is.EqualTo(RoomTier.Basic));
            Assert.That(queue.RoomsUnderRenovation, Is.EqualTo(0));
            Assert.That(queue.IsRenovating(201), Is.False);
        }

        [Test]
        public void Queue_HandlesConcurrentJobsInOrder()
        {
            var queue = new SimRenovationQueue();
            queue.Enqueue(RenovationPlan.For(RenovationPlanKind.Standard), new List<int> { 201 }); // 2 天
            queue.Enqueue(RenovationPlan.For(RenovationPlanKind.Luxury), new List<int> { 202 });   // 5 天

            Assert.That(queue.RoomsUnderRenovation, Is.EqualTo(2));
            queue.TickDay();
            var day2 = queue.TickDay();

            Assert.That(day2.Count, Is.EqualTo(1), "标准方案先完工");
            Assert.That(day2[0].roomNumbers, Is.EquivalentTo(new[] { 201 }));
            Assert.That(queue.IsRenovating(202), Is.True, "豪华方案还在施工");
        }

        [Test]
        public void Queue_IgnoresEmptyOrders()
        {
            var queue = new SimRenovationQueue();
            Assert.That(queue.Enqueue(RenovationPlan.For(RenovationPlanKind.Economy), null), Is.EqualTo(0));
            Assert.That(queue.Enqueue(RenovationPlan.For(RenovationPlanKind.Economy), new List<int>()), Is.EqualTo(0));
            Assert.That(queue.RoomsUnderRenovation, Is.EqualTo(0));
        }

        [Test]
        public void MaterialStore_BuysAndConsumes()
        {
            var store = new MaterialStore(stock: 5);

            Assert.That(store.PriceFor(10), Is.EqualTo(10 * MaterialStore.DefaultUnitPrice));
            Assert.That(store.TryConsume(3), Is.True);
            Assert.That(store.Stock, Is.EqualTo(2));
            Assert.That(store.TryConsume(5), Is.False, "库存不够就装不了");
            Assert.That(store.Stock, Is.EqualTo(2), "失败不该扣库存");

            store.Add(10);
            Assert.That(store.Stock, Is.EqualTo(12));
            Assert.That(store.TryConsume(0), Is.True, "零消耗是合法空操作");
        }
    }
}
