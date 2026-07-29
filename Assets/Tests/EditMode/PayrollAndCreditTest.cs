using System.Collections.Generic;
using NUnit.Framework;

// 支付要玩家亲手按（用户设计）：工资记账 → 日付/周付 → 拖过发薪日罚士气；
// 还贷逾期 → 涨利率 → 掉评级 → 卡额度，但永远不做成即死。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class PayrollAndCreditTest
    {
        private static HotelSim BuildHotel(int startingCash = 3000)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < 8; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            staff.Register(new StaffMember(StaffRole.Housekeeper, "H", 60, new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, startingCash, 31337);
            sim.FurnishInheritedRooms();
            return sim;
        }

        // ── 工资账：日付 ──────────────────────────────────────────────────────

        [Test]
        public void DailyPayroll_IsDueEveryDay()
        {
            var payroll = new PayrollAccount { Cycle = PayrollCycle.Daily };
            payroll.Accrue(120);

            Assert.That(payroll.IsPaydayOn(1), Is.True);
            Assert.That(payroll.IsPaydayOn(3), Is.True);
            Assert.That(payroll.DaysUntilPayday(3), Is.EqualTo(0), "日付没有'还剩几天'");
        }

        [Test]
        public void NothingOwed_IsNotAPayday()
        {
            // 没欠钱的日子不该弹支付按钮，也不该记逾期
            var payroll = new PayrollAccount { Cycle = PayrollCycle.Daily };
            Assert.That(payroll.IsPaydayOn(1), Is.False);
        }

        [Test]
        public void PayingInFull_ClearsTheAccountWithoutPenalty()
        {
            var payroll = new PayrollAccount();
            payroll.Accrue(120);

            var payment = payroll.Pay(cashAvailable: 500, isPayday: true);

            Assert.That(payment.paid, Is.EqualTo(120));
            Assert.That(payment.cleared, Is.True);
            Assert.That(payment.moralePenalty, Is.EqualTo(0));
            Assert.That(payroll.Owed, Is.EqualTo(0));
            Assert.That(payroll.MissedPaydays, Is.EqualTo(0));
        }

        [Test]
        public void ShortOnPayday_CostsMoraleAndCountsAsAMiss()
        {
            var payroll = new PayrollAccount();
            payroll.Accrue(120);

            var payment = payroll.Pay(cashAvailable: 50, isPayday: true);

            Assert.That(payment.paid, Is.EqualTo(50), "有多少付多少");
            Assert.That(payment.stillOwed, Is.EqualTo(70));
            Assert.That(payment.moralePenalty, Is.GreaterThan(0), "发薪日没付清就要罚士气");
            Assert.That(payroll.MissedPaydays, Is.EqualTo(1));
        }

        [Test]
        public void MoneyOwedBeforePayday_IsNotArrears()
        {
            // 周付第 3 天账上有钱没付，那只是"还没到期"，不该罚
            var payroll = new PayrollAccount { Cycle = PayrollCycle.Weekly };
            payroll.Accrue(120);

            var payment = payroll.Pay(cashAvailable: 0, isPayday: false);

            Assert.That(payment.moralePenalty, Is.EqualTo(0), "没到发薪日就不算欠薪");
            Assert.That(payroll.MissedPaydays, Is.EqualTo(0));
        }

        // ── 工资账：周付 ──────────────────────────────────────────────────────

        [Test]
        public void WeeklyPayroll_IsDueEverySeventhDay()
        {
            var payroll = new PayrollAccount { Cycle = PayrollCycle.Weekly };
            payroll.Accrue(120);

            Assert.That(payroll.IsPaydayOn(3), Is.False);
            Assert.That(payroll.IsPaydayOn(7), Is.True, "第 7 天发薪");
            Assert.That(payroll.IsPaydayOn(14), Is.True);
            Assert.That(payroll.DaysUntilPayday(5), Is.EqualTo(2), "提醒条要说'还剩 2 天'");
        }

        [Test]
        public void WeeklyPayroll_AccumulatesTheWholeWeek()
        {
            // 用户要求 UI 常驻提醒"本周待付多少"——账要真的攒起来
            var payroll = new PayrollAccount { Cycle = PayrollCycle.Weekly };
            for (int day = 1; day <= 6; day++) payroll.Accrue(120);

            Assert.That(payroll.Owed, Is.EqualTo(720));
            Assert.That(payroll.DaysAccrued, Is.EqualTo(6));
        }

        [Test]
        public void BlowingUpAWeeklyPayday_HurtsMoreThanMissingOneDay()
        {
            // 周付不改总额，改的是**风险**：攒了一周的期待一次落空，
            // 比日付漏一天狠得多。没有这一条，周付就是白拿的六天免息贷款。
            var daily = new PayrollAccount { Cycle = PayrollCycle.Daily };
            daily.Accrue(120);
            int dailyPenalty = daily.Pay(0, isPayday: true).moralePenalty;

            var weekly = new PayrollAccount { Cycle = PayrollCycle.Weekly };
            for (int day = 1; day <= 7; day++) weekly.Accrue(120);
            int weeklyPenalty = weekly.Pay(0, isPayday: true).moralePenalty;

            Assert.That(weeklyPenalty, Is.GreaterThan(dailyPenalty * 2),
                        "周付爆掉必须明显更疼，否则周付是白拿的周转");
        }

        [Test]
        public void CannotSwitchCycleWhileWagesAreOutstanding()
        {
            // 否则玩家能在发薪日前一秒切成周付，无限期拖着不付
            var payroll = new PayrollAccount();
            payroll.Accrue(120);

            Assert.That(payroll.TrySetCycle(PayrollCycle.Weekly, out string reason), Is.False, reason);
            Assert.That(reason, Is.Not.Empty);

            payroll.Pay(500, isPayday: true);
            Assert.That(payroll.TrySetCycle(PayrollCycle.Weekly, out _), Is.True, "结清了就能切");
        }

        // ── 接进 HotelSim：钱真的从玩家手上走掉 ───────────────────────────────

        [Test]
        public void PayWages_DipsIntoTheSafeboxWhenCashIsShort()
        {
            // **修订版 3 的铁律**：钱堆在箱子里绝不能造成欠薪。
            // 按钮把箱子当收银台使——玩家照样亲手付，但"忘了按收取"不会害死他。
            var sim = BuildHotel(startingCash: 0);
            sim.BeginDay(); sim.RunToEndOfDay(); sim.SettleDay();
            sim.Clock.BeginNextDay();
            sim.BeginDay(); sim.RunToEndOfDay(); sim.SettleDay();

            Assert.That(sim.Cash, Is.EqualTo(0), "现金是空的");
            Assert.That(sim.Safebox.Balance, Is.GreaterThan(0), "钱都在箱子里");
            Assert.That(sim.Payroll.Owed, Is.GreaterThan(0));

            int boxBefore = sim.Safebox.Balance;
            int owed = sim.Payroll.Owed;
            var payment = sim.PayWages();

            Assert.That(payment.cleared, Is.True, "箱子里的钱付得出工资");
            Assert.That(payment.moralePenalty, Is.EqualTo(0));
            Assert.That(sim.Safebox.Balance, Is.EqualTo(boxBefore - owed),
                        "只从箱子里取该付的那部分——整箱收上来会抢走玩家按'收款'的那一刻");
        }

        [Test]
        public void DraggingPastPayday_HitsMoraleAtTheStartOfTheNextDay()
        {
            // 玩家是在晨报上支付的，所以"没付"要在开新的一天时才收口
            var sim = BuildHotel(startingCash: 0);
            sim.BeginDay(); sim.RunToEndOfDay(); sim.SettleDay();
            Assert.That(sim.Payroll.Owed, Is.GreaterThan(0));

            // 榨干一切，让他连箱子都付不起
            sim.CollectSafebox();
            sim.TrySpendCash(sim.Cash);
            float moraleBefore = sim.Staff.AverageMorale;

            sim.Clock.BeginNextDay();
            sim.BeginDay();   // ← 收口昨天的账

            Assert.That(sim.Staff.AverageMorale, Is.LessThan(moraleBefore),
                        "拖过发薪日 = 欠薪，士气要掉");
            Assert.That(sim.Payroll.MissedPaydays, Is.GreaterThan(0));
        }

        // ── 信用：一个数字、两条规则、能恢复 ──────────────────────────────────
        // 用户拍板简化：惩罚不能是玩家算不清的复杂算法，也不能是杀存档的主因。
        // 所以信用只有 0-100 一个分数，**完全不碰利率**，只决定能借多少新钱。

        [Test]
        public void PayingOnTime_KeepsCreditAtTheTop()
        {
            var credit = new CreditStanding();
            for (int day = 1; day <= 10; day++)
            {
                credit.RecordPayment();
                credit.CloseDay(paymentWasDue: true);
            }

            Assert.That(credit.Score, Is.EqualTo(CreditPolicy.MaxScore), "不会超过 100");
            Assert.That(credit.Rating, Is.EqualTo(CreditRating.Good));
        }

        [Test]
        public void OneRuleEachWay_MinusTwentyForAMissPlusTenForAPayment()
        {
            // 玩家要能心算：没还 -20，还了 +10。就这两条。
            var credit = new CreditStanding();
            int start = credit.Score;

            credit.CloseDay(paymentWasDue: true);          // 没还
            Assert.That(credit.Score, Is.EqualTo(start - CreditPolicy.MissPenalty));

            credit.RecordPayment();
            credit.CloseDay(paymentWasDue: true);          // 还了
            Assert.That(credit.Score,
                        Is.EqualTo(start - CreditPolicy.MissPenalty + CreditPolicy.PaymentReward));
        }

        [Test]
        public void CreditGrowsBackSoOneBadWeekIsNotPermanent()
        {
            // **必须能恢复**：只扣不回的话一个糟糕的星期永久刻在档案上，
            // 玩家再也没有翻身的叙事
            var credit = new CreditStanding();
            // 五次不还从 100 掉到 0（100 / MissPenalty），刚好触底
            for (int i = 0; i < 5; i++) credit.CloseDay(paymentWasDue: true);
            Assert.That(credit.Score, Is.EqualTo(0));
            Assert.That(credit.Rating, Is.EqualTo(CreditRating.Blacklisted));

            for (int i = 0; i < 10; i++)
            {
                credit.RecordPayment();
                credit.CloseDay(paymentWasDue: true);
            }

            Assert.That(credit.Rating, Is.EqualTo(CreditRating.Good), "坚持还款能爬回来");
        }

        [Test]
        public void RecoveryIsSlowerThanTheDamage()
        {
            // 回得比扣得慢，否则按时还款不是件要坚持的事，是随手能刷的
            Assert.That(CreditPolicy.PaymentReward, Is.LessThan(CreditPolicy.MissPenalty));
        }

        [Test]
        public void CreditOnlyLimitsBorrowing_ItNeverTouchesTheInterestRate()
        {
            // 这是"不做成死亡螺旋"的结构保证：信用差不会让债务长得更快，
            // 只会让你借不到新钱。利滚利杀存档的路被从设计上切断了。
            var type = typeof(CreditStanding);
            foreach (var member in type.GetMembers())
                Assert.That(member.Name.Contains("Rate"), Is.False,
                            "信用不该有任何和利率有关的成员：" + member.Name);
            foreach (var member in typeof(CreditPolicy).GetMembers())
                Assert.That(member.Name.Contains("Rate"), Is.False,
                            "信用政策不该有任何和利率有关的成员：" + member.Name);
        }

        [Test]
        public void EachRatingBandLendsLessThanTheOneAbove()
        {
            // 四个档位一个词一个含义，额度单调递减，玩家能预判下一档
            var credit = new CreditStanding();
            int good = credit.BorrowingLimitFor(10000);

            credit.Restore(CreditPolicy.ShakyAtOrAbove, 0);
            int shaky = credit.BorrowingLimitFor(10000);
            credit.Restore(CreditPolicy.BadAtOrAbove, 0);
            int bad = credit.BorrowingLimitFor(10000);
            credit.Restore(0, 0);
            int blacklisted = credit.BorrowingLimitFor(10000);

            Assert.That(good, Is.GreaterThan(shaky));
            Assert.That(shaky, Is.GreaterThan(bad));
            Assert.That(bad, Is.GreaterThan(blacklisted));
            Assert.That(blacklisted, Is.EqualTo(0), "拉黑就是借不到");
        }

        [Test]
        public void BlacklistedMeansNoNewMoney_ButTheGameGoesOn()
        {
            // 永远留一条自救路：借不到新钱，但可以靠经营自己爬出来
            var credit = new CreditStanding();
            for (int i = 0; i < 5; i++) credit.CloseDay(paymentWasDue: true);

            Assert.That(credit.Rating, Is.EqualTo(CreditRating.Blacklisted));
            Assert.That(credit.BorrowingLimitFor(50000), Is.EqualTo(0), "借不到新钱");
            Assert.That(credit.Score, Is.GreaterThanOrEqualTo(0), "分数不为负——没有无底洞");
        }

        [Test]
        public void TheRuleFitsInOneLine_SoTheUiCanJustSayIt()
        {
            // 惩罚必须是玩家看一眼就懂的，所以规则本身要能一行说完
            Assert.That(CreditPolicy.RuleLine, Is.Not.Empty);
            Assert.That(CreditPolicy.RuleLine.Length, Is.LessThan(90), "一行说得完");
        }

        [Test]
        public void NoDebtMeansNoMissedPayment()
        {
            // 债务清零之后的日子不该被记成逾期
            var credit = new CreditStanding();
            for (int i = 0; i < 10; i++) credit.CloseDay(paymentWasDue: false);

            Assert.That(credit.MissedPayments, Is.EqualTo(0));
            Assert.That(credit.Rating, Is.EqualTo(CreditRating.Good));
        }

        [Test]
        public void PayingTheBank_TakesTheMoneyAndClearsTodaysDuty()
        {
            var sim = BuildHotel(startingCash: 1000);

            int paid = sim.PayLoanInstallment(300);

            Assert.That(paid, Is.EqualTo(300));
            Assert.That(sim.Cash, Is.EqualTo(700), "钱真的走掉了");
            Assert.That(sim.Credit.PaidToday, Is.True);

            sim.Credit.CloseDay(paymentWasDue: true);
            Assert.That(sim.Credit.MissedPayments, Is.EqualTo(0), "付了就不算逾期");
        }

        [Test]
        public void PayingTheBankWithNoMoney_TakesNothing()
        {
            var sim = BuildHotel(startingCash: 0);
            sim.CollectSafebox();

            Assert.That(sim.PayLoanInstallment(300), Is.EqualTo(0));
            Assert.That(sim.Credit.PaidToday, Is.False, "没付成就不算付");
        }
    }
}
