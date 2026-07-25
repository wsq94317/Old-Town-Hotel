using NUnit.Framework;

// 资金骨架（修订版 3）：
//   · 保险箱固定等级容量表（不随收入浮动——否则升级的可感知价值被自动增长吞掉）
//   · 溢出进临时账本，只能拿回 65%，另有失窃风险
//   · 日结顺序 = 固定成本前置：毛收入−佣金−固定成本 = 入箱净利
//     欠薪只源于真实经营亏损，**绝不因为玩家没点收取键**
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class SafeboxSettlementTest
    {
        // ── 保险箱 ───────────────────────────────────────────────────────────

        [Test]
        public void CapacityTable_IsFixedAndIncreasesWithLevel()
        {
            int l1 = SafeboxLevels.CapacityFor(1);
            int l2 = SafeboxLevels.CapacityFor(2);
            int lMax = SafeboxLevels.CapacityFor(SafeboxLevels.MaxLevel);

            Assert.That(l1, Is.GreaterThan(0));
            Assert.That(l2, Is.GreaterThan(l1), "升级要有可感知的容量提升");
            Assert.That(lMax, Is.GreaterThan(l2));
            Assert.That(SafeboxLevels.CapacityFor(0), Is.EqualTo(l1), "越界钳到 1 级");
            Assert.That(SafeboxLevels.CapacityFor(99), Is.EqualTo(lMax), "越界钳到满级");
        }

        [Test]
        public void Deposit_FillsUpToCapacityAndReportsOverflow()
        {
            var box = new Safebox(level: 1);
            int capacity = box.Capacity;

            int overflow = box.Deposit(capacity - 100);
            Assert.That(overflow, Is.EqualTo(0));
            Assert.That(box.Balance, Is.EqualTo(capacity - 100));

            overflow = box.Deposit(500);
            Assert.That(box.Balance, Is.EqualTo(capacity), "装满即止");
            Assert.That(overflow, Is.EqualTo(400), "多出来的报给溢出账本");
            Assert.That(box.IsFull, Is.True);
            Assert.That(box.FillRatio, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Deposit_IgnoresNonPositive()
        {
            var box = new Safebox(level: 1);
            Assert.That(box.Deposit(0), Is.EqualTo(0));
            Assert.That(box.Deposit(-50), Is.EqualTo(0));
            Assert.That(box.Balance, Is.EqualTo(0));
        }

        [Test]
        public void Collect_HandsEverythingOverAndEmptiesTheBox()
        {
            var box = new Safebox(level: 2);
            box.Deposit(700);

            int collected = box.Collect();

            Assert.That(collected, Is.EqualTo(700));
            Assert.That(box.Balance, Is.EqualTo(0));
            Assert.That(box.Collect(), Is.EqualTo(0), "空箱再收是 0，不是负数");
        }

        [Test]
        public void Upgrade_RaisesCapacityWithoutLosingContents()
        {
            var box = new Safebox(level: 1);
            box.Deposit(SafeboxLevels.CapacityFor(1));
            int before = box.Balance;

            box.Upgrade();

            Assert.That(box.Level, Is.EqualTo(2));
            Assert.That(box.Capacity, Is.EqualTo(SafeboxLevels.CapacityFor(2)));
            Assert.That(box.Balance, Is.EqualTo(before), "升级不倒钱");
            Assert.That(box.IsFull, Is.False, "扩容后又能装了");
        }

        // ── 溢出账本 ─────────────────────────────────────────────────────────

        [Test]
        public void Overflow_RecoversOnlyPartOfIt()
        {
            var ledger = new OverflowLedger();
            ledger.Add(1000);

            int recovered = ledger.Recover();

            Assert.That(recovered, Is.EqualTo(SimMath.FloorToInt(1000 * OverflowLedger.RecoveryRate)));
            Assert.That(recovered, Is.LessThan(1000), "满箱之后的收入只能捞回一部分");
            Assert.That(recovered, Is.GreaterThan(0), "但不是清零——忙一两天不该白干");
            Assert.That(ledger.Balance, Is.EqualTo(0));
        }

        [Test]
        public void Overflow_TheftOnlyBitesSometimes()
        {
            var unlucky = new OverflowLedger();
            unlucky.Add(1000);
            int stolen = unlucky.RollTheft(roll: 0.0);
            Assert.That(stolen, Is.GreaterThan(0), "低骰=被偷一部分");
            Assert.That(unlucky.Balance, Is.EqualTo(1000 - stolen));

            var lucky = new OverflowLedger();
            lucky.Add(1000);
            Assert.That(lucky.RollTheft(roll: 0.99), Is.EqualTo(0), "高骰这次没事");
            Assert.That(lucky.Balance, Is.EqualTo(1000));

            var empty = new OverflowLedger();
            Assert.That(empty.RollTheft(roll: 0.0), Is.EqualTo(0), "空账本没什么可偷");
        }

        // ── 日结顺序：固定成本前置 ───────────────────────────────────────────

        [Test]
        public void ProfitableDay_DepositsNetProfitAfterCostsAndCommission()
        {
            var input = new DaySettlementInput(
                grossIncome: 1000, commission: 100,
                wages: 200, interest: 50, scheduledRepayment: 100, supplies: 50,
                cashOnHand: 500);

            var result = DaySettlement.Resolve(input, safeboxRoom: 10000);

            Assert.That(result.netToSafebox, Is.EqualTo(1000 - 100 - 400), "毛收入−佣金−固定成本=入箱净利");
            Assert.That(result.overflowed, Is.EqualTo(0));
            Assert.That(result.wagesPaid, Is.True);
            Assert.That(result.cashAfter, Is.EqualTo(500), "赚钱的日子不用动储备现金");
            Assert.That(result.cashPaidFromReserve, Is.EqualTo(0));
            Assert.That(result.unpaidAmount, Is.EqualTo(0));
        }

        [Test]
        public void NetProfitBeyondCapacity_SpillsIntoOverflow()
        {
            var input = new DaySettlementInput(1000, 0, 0, 0, 0, 0, cashOnHand: 0);

            var result = DaySettlement.Resolve(input, safeboxRoom: 600);

            Assert.That(result.netToSafebox, Is.EqualTo(600));
            Assert.That(result.overflowed, Is.EqualTo(400), "装不下的进临时账本（65% 找回）");
        }

        [Test]
        public void ThinDay_CoversShortfallFromCashReserve()
        {
            // 毛收入不够付固定成本 → 从现金垫付，工资照发
            var input = new DaySettlementInput(
                grossIncome: 300, commission: 30,
                wages: 200, interest: 50, scheduledRepayment: 100, supplies: 50,
                cashOnHand: 500);

            var result = DaySettlement.Resolve(input, safeboxRoom: 10000);

            int shortfall = 400 - (300 - 30);
            Assert.That(result.netToSafebox, Is.EqualTo(0), "亏损日没有净利可入箱");
            Assert.That(result.cashPaidFromReserve, Is.EqualTo(shortfall));
            Assert.That(result.cashAfter, Is.EqualTo(500 - shortfall));
            Assert.That(result.wagesPaid, Is.True, "现金够垫就不欠薪");
        }

        [Test]
        public void BrokeDay_TriggersUnpaidWages_NotBecausePlayerForgotToCollect()
        {
            // 真实经营亏损 + 现金见底 → 欠薪链。这是唯一该欠薪的情形。
            var input = new DaySettlementInput(
                grossIncome: 100, commission: 10,
                wages: 300, interest: 100, scheduledRepayment: 0, supplies: 0,
                cashOnHand: 50);

            var result = DaySettlement.Resolve(input, safeboxRoom: 10000);

            Assert.That(result.wagesPaid, Is.False);
            Assert.That(result.unpaidAmount, Is.GreaterThan(0));
            Assert.That(result.cashAfter, Is.EqualTo(0), "现金被榨干但不为负");
            Assert.That(result.cashPaidFromReserve, Is.EqualTo(50));
            Assert.That(result.netToSafebox, Is.EqualTo(0));
        }

        [Test]
        public void SafeboxFull_DoesNotCauseUnpaidWages()
        {
            // 关键反例：钱堆在保险箱里 ≠ 发不出工资（修订版 3 推翻的旧设计）
            var input = new DaySettlementInput(
                grossIncome: 5000, commission: 0,
                wages: 300, interest: 0, scheduledRepayment: 0, supplies: 0,
                cashOnHand: 0);

            var result = DaySettlement.Resolve(input, safeboxRoom: 100);

            Assert.That(result.wagesPaid, Is.True, "当日收入先付工资——箱子满不满与发薪无关");
            Assert.That(result.netToSafebox, Is.EqualTo(100));
            Assert.That(result.overflowed, Is.EqualTo(5000 - 300 - 100));
            Assert.That(result.unpaidAmount, Is.EqualTo(0));
        }

        [Test]
        public void ZeroEverything_IsANoOpDay()
        {
            var result = DaySettlement.Resolve(new DaySettlementInput(0, 0, 0, 0, 0, 0, 0), safeboxRoom: 500);
            Assert.That(result.netToSafebox, Is.EqualTo(0));
            Assert.That(result.overflowed, Is.EqualTo(0));
            Assert.That(result.wagesPaid, Is.True, "没有工资要发就不算欠薪");
            Assert.That(result.unpaidAmount, Is.EqualTo(0));
        }

        // ── 排班档位 ─────────────────────────────────────────────────────────

        [Test]
        public void ShiftTiers_PutProgressivelyMorePeopleOnDuty()
        {
            int skeleton = RunTier(ShiftTier.Skeleton);
            int lean = RunTier(ShiftTier.Lean);
            int normal = RunTier(ShiftTier.Normal);
            int full = RunTier(ShiftTier.Full);

            Assert.That(skeleton, Is.LessThanOrEqualTo(lean));
            Assert.That(lean, Is.LessThan(full));
            Assert.That(normal, Is.LessThanOrEqualTo(full));
            Assert.That(full, Is.EqualTo(4), "全员档=所有人上班");
            Assert.That(skeleton, Is.GreaterThan(0), "再抠也得留人看店");
        }

        private static int RunTier(ShiftTier tier)
        {
            var roster = new StaffRoster();
            for (int i = 0; i < 4; i++)
                roster.Register(new StaffMember(StaffRole.Housekeeper, "H" + i, 60));
            var plan = new ShiftPlan();
            plan.SetTier(StaffRole.Housekeeper, tier);
            plan.ApplyTo(roster);
            return roster.OnDutyCountOfRole(StaffRole.Housekeeper);
        }

        [Test]
        public void LeanShift_CostsLessWages_ThatIsTheWholeTradeoff()
        {
            var roster = new StaffRoster();
            for (int i = 0; i < 4; i++)
                roster.Register(new StaffMember(StaffRole.Housekeeper, "H" + i, 60));

            var lean = new ShiftPlan();
            lean.SetTier(StaffRole.Housekeeper, ShiftTier.Skeleton);
            lean.ApplyTo(roster);
            int leanWages = lean.DailyWageCost(roster);

            var full = new ShiftPlan();
            full.SetTier(StaffRole.Housekeeper, ShiftTier.Full);
            full.ApplyTo(roster);
            int fullWages = full.DailyWageCost(roster);

            Assert.That(leanWages, Is.LessThan(fullWages), "只给在班的人发钱——省钱是真的");
            Assert.That(fullWages, Is.EqualTo(4 * 60));
        }

        [Test]
        public void LeanShift_AlsoCutsCleaningThroughput()
        {
            // 省钱的代价立刻反映在吞吐上（压力网的耦合边）
            var roster = new StaffRoster();
            for (int i = 0; i < 4; i++)
            {
                int id = roster.Register(new StaffMember(StaffRole.Housekeeper, "H" + i, 60));
                roster.SetState(id, StaffOperationalState.OffShift);
            }

            var plan = new ShiftPlan();
            plan.SetTier(StaffRole.Housekeeper, ShiftTier.Skeleton);
            plan.ApplyTo(roster);
            foreach (var e in roster.Entries)
                if (e.IsOnDuty) e.state = StaffOperationalState.Working;
            float leanRate = ServiceCapacityModel.CleanRoomsPerHour(roster, 1f);

            plan.SetTier(StaffRole.Housekeeper, ShiftTier.Full);
            plan.ApplyTo(roster);
            foreach (var e in roster.Entries)
                if (e.IsOnDuty) e.state = StaffOperationalState.Working;
            float fullRate = ServiceCapacityModel.CleanRoomsPerHour(roster, 1f);

            Assert.That(fullRate, Is.GreaterThan(leanRate));
        }
    }
}
