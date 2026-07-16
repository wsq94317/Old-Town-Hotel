using NUnit.Framework;

// M4 客诉决策结果表：roll 外部注入，全分支确定性验证。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class ComplaintDecisionTest
    {
        private const int Rate = 80;

        [Test]
        public void Pay_CostsHalfRate_RestoresSatisfaction()
        {
            var o = ComplaintDecisionLogic.Resolve(ComplaintChoice.Pay, Rate, 0.99);
            Assert.AreEqual(-40, o.CashDelta);
            Assert.AreEqual(+2, o.SatisfactionDelta);
            Assert.AreEqual(0f, o.SkipGameHours);
        }

        [Test]
        public void ColdShoulder_TwoBranches()
        {
            var mild = ComplaintDecisionLogic.Resolve(ComplaintChoice.ColdShoulder, Rate, 0.2);
            Assert.AreEqual(-1, mild.SatisfactionDelta);
            Assert.AreEqual(0, mild.CashDelta);

            var bad = ComplaintDecisionLogic.Resolve(ComplaintChoice.ColdShoulder, Rate, 0.8);
            Assert.AreEqual(-4, bad.SatisfactionDelta);
        }

        [Test]
        public void Fight_Win_PrestigeAndStaffMoraleUp()
        {
            var o = ComplaintDecisionLogic.Resolve(ComplaintChoice.Fight, Rate, 0.1);
            Assert.AreEqual(+2, o.PrestigeDelta);
            Assert.AreEqual(+5, o.StaffMoraleDelta);
            Assert.IsFalse(o.ManagerKnockedDown);
        }

        [Test]
        public void Fight_Lose_KnockedDownAndPays()
        {
            var o = ComplaintDecisionLogic.Resolve(ComplaintChoice.Fight, Rate, 0.6);
            Assert.IsTrue(o.ManagerKnockedDown);
            Assert.AreEqual(-Rate, o.CashDelta);
            Assert.AreEqual(+8, o.StaffMoraleDelta); // 员工看老板挨揍士气离奇上升
        }

        [Test]
        public void Fight_Jail_SkipsThreeHours()
        {
            var o = ComplaintDecisionLogic.Resolve(ComplaintChoice.Fight, Rate, 0.9);
            Assert.AreEqual(3f, o.SkipGameHours);
            Assert.AreEqual(-40, o.CashDelta); // 保释金 = 半价房费
        }

        [Test]
        public void Pay_HasMinimumCost()
        {
            var o = ComplaintDecisionLogic.Resolve(ComplaintChoice.Pay, 4, 0.5);
            Assert.AreEqual(-10, o.CashDelta); // 下限 10
        }
    }
}
