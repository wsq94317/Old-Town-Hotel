using NUnit.Framework;

// 债务服务策略：还款按昨日净利分成、固定额作上限。
// 断言的是"不会把玩家泵到 0"这条性质本身，不是任何一档具体数字——
// profitShare / cap 都是会调的旋钮。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DebtPolicyTest
    {
        private const int Cap = 150;

        [Test]
        public void NoDebt_NoRepayment()
        {
            Assert.That(DebtPolicy.ScheduledRepaymentFor(0, yesterdayNetProfit: 500, cashOnHand: 900, cap: Cap),
                        Is.EqualTo(0));
        }

        [Test]
        public void EarnMore_RepayMore_UpToTheCap()
        {
            int lean = DebtPolicy.ScheduledRepaymentFor(50000, yesterdayNetProfit: 100, cashOnHand: 300, cap: Cap);
            int fat = DebtPolicy.ScheduledRepaymentFor(50000, yesterdayNetProfit: 300, cashOnHand: 900, cap: Cap);

            Assert.That(fat, Is.GreaterThan(lean), "赚得多就该还得多");
            Assert.That(DebtPolicy.ScheduledRepaymentFor(50000, 100000, 100000, Cap),
                        Is.EqualTo(Cap), "单日上限封顶");
        }

        // 这条是这个类存在的理由。固定 $150/天配上"收入不够就从 Cash 垫付"
        // 等于一台抽水泵：玩家从保险箱收多少当天就被抽走多少，现金永远钉在 $0，
        // 攒不出任何一笔投资的首付（100 天实测卡死在 14 间房、债务还反涨）。
        [Test]
        public void LosingDay_LeavesEnoughToEverAccumulate()
        {
            // 亏损日：只象征性还一点，绝不按上限硬扣
            int onALoss = DebtPolicy.ScheduledRepaymentFor(183000, yesterdayNetProfit: 0, cashOnHand: 2000, cap: Cap);

            Assert.That(onALoss, Is.GreaterThan(0), "债务曲线要始终在动，不能完全停手");
            Assert.That(onALoss, Is.LessThan(Cap), "亏损日不该按上限硬扣——那就是把玩家泵到 0 的那台泵");

            // 连续亏损日也攒得下钱：还款额远小于手上的现金
            Assert.That(onALoss * 10, Is.LessThan(2000),
                        "十个亏损日的还款加起来仍远小于本金，玩家攒得出首付");
        }

        [Test]
        public void BrokeAndLosing_PaysNothing()
        {
            Assert.That(DebtPolicy.ScheduledRepaymentFor(183000, yesterdayNetProfit: 0, cashOnHand: 0, cap: Cap),
                        Is.EqualTo(0), "身无分文时不该凭空扣钱（欠薪链只该源于真实亏损）");
        }

        [Test]
        public void NeverOverpaysTheRemainingBalance()
        {
            Assert.That(DebtPolicy.ScheduledRepaymentFor(40, yesterdayNetProfit: 5000, cashOnHand: 9000, cap: Cap),
                        Is.EqualTo(40), "最后一笔只还剩下的那点");
        }
    }
}
