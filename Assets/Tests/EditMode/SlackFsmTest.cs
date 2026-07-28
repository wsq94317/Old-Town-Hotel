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
        public void SlackingEndsOnItsOwn_EvenIfTheManagerNeverComes()
        {
            // **防死锁的硬约束**：原设计只有"经理进层"一条出路，于是经理不去的
            // 楼层会永久卡住（实测验房员 3.78/4 秒的活干了 205 秒，两间房整天
            // 回不到可售）。100 间房的酒店经理不可能无处不在。
            var fsm = AlwaysSlackFsm();
            while (fsm.Current == SlackFsm.State.Working) fsm.Tick(1f, false, false);
            Assert.IsFalse(fsm.IsProductive, "先真的进入偷懒");

            // 经理**从不出现**，只让时间流过
            for (int i = 0; i < 200 && !fsm.IsProductive; i++) fsm.Tick(0.5f, false, false);

            Assert.AreEqual(SlackFsm.State.Working, fsm.Current, "懒够了要自己回去干活");
            Assert.IsTrue(fsm.HasRecentSlackRecord, "自己回去了不等于没偷懒——质询照样问得出来");
        }

        [Test]
        public void SelfRecovery_TakesAtLeastTheMinimumSlackTime()
        {
            // 自愈不能是"眨眼就好"，否则偷懒就没有代价，监督玩法失去意义
            var fsm = AlwaysSlackFsm();
            while (fsm.Current == SlackFsm.State.Working) fsm.Tick(1f, false, false);

            fsm.Tick(SupervisionTuning.SlackMinSeconds - 0.5f, false, false);

            Assert.AreEqual(SlackFsm.State.Slacking, fsm.Current,
                            "最短偷懒时长内不许自愈");
        }

        [Test]
        public void LazyTrait_SlacksLonger_BeforeSelfRecovering()
        {
            // 倍率作用在**区间**上，单次抽样可能落在低端，所以断言只能对
            // 多个种子的平均值下手（同一个陷阱在需求随机性上踩过：
            // 不断言逐次单调，只断言累计量的关系）。
            Assert.That(AverageSlackSeconds(lazy: true),
                        Is.GreaterThan(AverageSlackSeconds(lazy: false)),
                        "Lazy 特质平均懒得更久");
        }

        /// <summary>多种子平均：进入偷懒后到自愈为止的秒数。</summary>
        private static float AverageSlackSeconds(bool lazy)
        {
            const float Step = 0.1f;
            float total = 0f;
            int samples = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var fsm = new SlackFsm(new Random(seed), lazy, () => 100);
                for (int i = 0; i < 10000 && fsm.Current == SlackFsm.State.Working; i++)
                    fsm.Tick(1f, false, false);
                if (fsm.Current != SlackFsm.State.Slacking) continue;

                float elapsed = 0f;
                // 经理从不出现，只数自愈用了多久
                for (int i = 0; i < 2000 && fsm.Current == SlackFsm.State.Slacking; i++)
                {
                    fsm.Tick(Step, false, false);
                    elapsed += Step;
                }
                total += elapsed;
                samples++;
            }
            Assert.That(samples, Is.GreaterThan(0), "至少要有一个样本真的偷懒了");
            return total / samples;
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
