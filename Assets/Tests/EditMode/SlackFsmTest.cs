using System;
using NUnit.Framework;

// M3 偷懒状态机：只在经理不在场时偷懒；惊醒/装忙窗口时序；抓包事件；质询判定；决策效果。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class SlackFsmTest
    {
        private static SlackFsm AlwaysSlackFsm(bool lazy = false)
        {
            // 种子扫描：找一个第一次 roll 就命中的种子太脆——改用大 dt 放大概率到必中。
            return new SlackFsm(new Random(1), lazy, () => 100);
        }

        [Test]
        public void ManagerOnFloor_NeverSlacks()
        {
            var fsm = new SlackFsm(new Random(1), true, () => 0);
            for (int i = 0; i < 10000; i++) fsm.Tick(1f, managerOnFloor: true, managerNear: false);
            Assert.AreEqual(SlackFsm.State.Working, fsm.Current);
            Assert.IsFalse(fsm.HasRecentSlackRecord);
        }

        [Test]
        public void ManagerAway_EventuallySlacks_AndRecords()
        {
            var fsm = AlwaysSlackFsm();
            for (int i = 0; i < 10000 && fsm.Current == SlackFsm.State.Working; i++)
                fsm.Tick(1f, false, false);
            Assert.AreEqual(SlackFsm.State.Slacking, fsm.Current);
            Assert.IsTrue(fsm.HasRecentSlackRecord);
            Assert.IsFalse(fsm.IsProductive);
        }

        [Test]
        public void WakeWindow_CatchFiresWhenManagerNear()
        {
            var fsm = AlwaysSlackFsm();
            while (fsm.Current == SlackFsm.State.Working) fsm.Tick(1f, false, false);

            bool caught = false;
            fsm.OnCaught += () => caught = true;

            fsm.Tick(0.1f, true, false);  // 经理进层 → Waking
            Assert.AreEqual(SlackFsm.State.Waking, fsm.Current);

            fsm.Tick(0.1f, true, true);   // 惊醒窗口内靠近 → 抓包
            Assert.IsTrue(caught);
            Assert.AreEqual(SlackFsm.State.Working, fsm.Current);
        }

        [Test]
        public void FullWindowsElapse_ReturnsToWork_NoCatch()
        {
            var fsm = AlwaysSlackFsm();
            while (fsm.Current == SlackFsm.State.Working) fsm.Tick(1f, false, false);
            bool caught = false;
            fsm.OnCaught += () => caught = true;

            fsm.Tick(0.01f, true, false); // → Waking
            fsm.Tick(SupervisionTuning.WakeDelaySeconds + 0.01f, true, false); // → PanicFaking
            Assert.AreEqual(SlackFsm.State.PanicFaking, fsm.Current);
            Assert.IsFalse(fsm.IsProductive); // 装忙不推进进度

            fsm.Tick(SupervisionTuning.PanicFakeSeconds + 0.01f, true, false); // → Working
            Assert.AreEqual(SlackFsm.State.Working, fsm.Current);
            Assert.IsFalse(caught);
            Assert.IsTrue(fsm.HasRecentSlackRecord); // 逃过现场但记录仍在（质询可抓）
        }

        [Test]
        public void LazyTrait_HasLongerWakeDelay()
        {
            var fsm = AlwaysSlackFsm(lazy: true);
            while (fsm.Current == SlackFsm.State.Working) fsm.Tick(1f, false, false);
            fsm.Tick(0.01f, true, false); // → Waking
            // 普通惊醒时长过去后 Lazy 仍在 Waking
            fsm.Tick(SupervisionTuning.WakeDelaySeconds + 0.1f, true, false);
            Assert.AreEqual(SlackFsm.State.Waking, fsm.Current);
        }

        [Test]
        public void Interrogation_Verdicts()
        {
            Assert.AreEqual(InterrogationVerdict.Caught, InterrogateLogic.Verdict(true));
            Assert.AreEqual(InterrogationVerdict.WrongAccusation, InterrogateLogic.Verdict(false));
        }

        [Test]
        public void CatchResolution_Effects()
        {
            var plain = new StaffMember(StaffRole.Housekeeper, "P", 45);
            var diva = new StaffMember(StaffRole.Housekeeper, "D", 45,
                StaffAttributes.Default, 0, new[] { StaffTrait.Diva });

            var urge = CatchResolutionLogic.Resolve(CatchChoice.Urge, plain);
            Assert.AreEqual(SupervisionTuning.UrgeMoraleDelta, urge.MoraleDelta);
            Assert.IsTrue(urge.SpeedBuff);

            var scold = CatchResolutionLogic.Resolve(CatchChoice.Scold, diva);
            Assert.AreEqual(SupervisionTuning.ScoldMoraleDelta - 10, scold.MoraleDelta);
            Assert.IsTrue(scold.GrudgeTriggered);

            var ignore = CatchResolutionLogic.Resolve(CatchChoice.Ignore, plain);
            Assert.IsTrue(ignore.ContagionSignal);
            Assert.AreEqual(SupervisionTuning.IgnoreMoraleDelta, ignore.MoraleDelta);
        }
    }
}
