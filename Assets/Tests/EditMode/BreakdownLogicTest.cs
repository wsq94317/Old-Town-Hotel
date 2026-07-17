using NUnit.Framework;

// 损坏处理结果表：roll 注入全分支验证。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class BreakdownLogicTest
    {
        [Test]
        public void DIY_SuccessAndSlapstick()
        {
            var win = BreakdownLogic.Resolve(BreakdownFix.DIY, 0.3, false, false);
            Assert.IsTrue(win.Fixed);
            Assert.AreEqual(+20, win.CashDelta);

            var fail = BreakdownLogic.Resolve(BreakdownFix.DIY, 0.9, false, false);
            Assert.IsFalse(fail.Fixed);
            Assert.IsTrue(fail.ManagerSlapstick); // 喷一脸水/头发竖起
        }

        [Test]
        public void SendStaff_ClumsyCanMakeItWorse()
        {
            var worse = BreakdownLogic.Resolve(BreakdownFix.SendStaff, 0.1, staffClumsy: true, staffFastHands: false);
            Assert.IsFalse(worse.Fixed);
            Assert.AreEqual(+1, worse.SeverityDelta); // 越修越坏

            var ok = BreakdownLogic.Resolve(BreakdownFix.SendStaff, 0.9, staffClumsy: true, staffFastHands: false);
            Assert.IsTrue(ok.Fixed);

            var fast = BreakdownLogic.Resolve(BreakdownFix.SendStaff, 0.5, false, staffFastHands: true);
            Assert.IsTrue(fast.Fixed);
            StringAssert.Contains("Show-off", fast.Story);
        }

        [Test]
        public void DuctTape_ResolvesButRecurs()
        {
            var tape = BreakdownLogic.Resolve(BreakdownFix.DuctTape, 0.5, false, false);
            Assert.IsTrue(tape.Fixed);
            Assert.IsTrue(tape.TapedRecurrence); // 明天复发更严重
        }

        [Test]
        public void LockRoom_SealsWithSatCost()
        {
            var l = BreakdownLogic.Resolve(BreakdownFix.LockRoom, 0.5, false, false);
            Assert.IsTrue(l.Fixed);
            Assert.IsTrue(l.LockedRoom);
            Assert.AreEqual(-1, l.SatisfactionDelta);
        }
    }
}
