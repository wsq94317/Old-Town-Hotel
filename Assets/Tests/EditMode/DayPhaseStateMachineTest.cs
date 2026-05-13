using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试：Room2DDayPhaseStateMachine 阶段状态机。
// 因为 Awake() 在 AddComponent 时立即执行，Start() 在 EditMode 下不自动执行，
// 所以测试流程为：
//   1. AddComponent（触发 Awake，静默设置初始状态）
//   2. 订阅事件
//   3. 调用 InitialiseForTesting()（模拟 Start，广播初始 Preparation 进入事件）
// 这样能保证订阅者不会错过初始事件，且测试完全同步、无随机、无时间依赖。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DayPhaseStateMachineTest
    {
        private GameObject _go;
        private Room2DDayPhaseStateMachine _sm;

        [SetUp]
        public void SetUp()
        {
            // Awake 在 AddComponent 时立即触发，静默初始化状态（不广播事件）。
            _go = new GameObject("TestDayPhaseStateMachine");
            _sm = _go.AddComponent<Room2DDayPhaseStateMachine>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── 测试 1：初始阶段为 Preparation ────────────────────────────────────

        [Test]
        public void Test_StartsInPreparation()
        {
            // Arrange / Act: AddComponent 已在 SetUp 中完成。
            // Assert
            Assert.That(_sm.CurrentPhase,
                Is.EqualTo(Room2DDayPhaseStateMachine.Room2DDayPhase.Preparation),
                "新实例的 CurrentPhase 应为 Preparation。");
        }

        // ── 测试 2：三次推进恰好经过全部四个阶段 ─────────────────────────────

        [Test]
        public void Test_FullForwardTraversal_HitsAllFourPhasesInOrder()
        {
            // Arrange
            var phases = new System.Collections.Generic.List<Room2DDayPhaseStateMachine.Room2DDayPhase>();
            _sm.OnPhaseEntered += p => phases.Add(p);
            _sm.InitialiseForTesting(); // 广播初始 Preparation 进入事件

            // Act
            _sm.RequestAdvancePhase(); // Preparation → CheckInPeak
            _sm.RequestAdvancePhase(); // CheckInPeak  → Recovery
            _sm.RequestAdvancePhase(); // Recovery     → Ended

            // Assert
            Assert.That(phases.Count, Is.EqualTo(4), "应收到 4 次 OnPhaseEntered 事件。");
            Assert.That(phases[0], Is.EqualTo(Room2DDayPhaseStateMachine.Room2DDayPhase.Preparation));
            Assert.That(phases[1], Is.EqualTo(Room2DDayPhaseStateMachine.Room2DDayPhase.CheckInPeak));
            Assert.That(phases[2], Is.EqualTo(Room2DDayPhaseStateMachine.Room2DDayPhase.Recovery));
            Assert.That(phases[3], Is.EqualTo(Room2DDayPhaseStateMachine.Room2DDayPhase.Ended));
        }

        // ── 测试 3：多次调用不会跳级 ──────────────────────────────────────────

        [Test]
        public void Test_CannotSkipPhase()
        {
            // Arrange
            _sm.InitialiseForTesting();

            // Act: 连续调用两次（同帧内快速双击场景）
            _sm.RequestAdvancePhase(); // 第 1 次：Preparation → CheckInPeak
            _sm.RequestAdvancePhase(); // 第 2 次：CheckInPeak  → Recovery

            // Assert: 每次调用恰好推进一步，不会一次跳到 Ended。
            Assert.That(_sm.CurrentPhase,
                Is.EqualTo(Room2DDayPhaseStateMachine.Room2DDayPhase.Recovery),
                "两次推进后应在 Recovery，而非跳过到 Ended。");
        }

        // ── 测试 4：OnPhaseEntered 整个完整遍历恰好触发 4 次 ──────────────────

        [Test]
        public void Test_OnPhaseEntered_FiresExactlyOnce()
        {
            // Arrange
            int enteredCount = 0;
            _sm.OnPhaseEntered += _ => enteredCount++;
            _sm.InitialiseForTesting(); // Preparation 进入（第 1 次）

            // Act
            _sm.RequestAdvancePhase(); // CheckInPeak 进入（第 2 次）
            _sm.RequestAdvancePhase(); // Recovery    进入（第 3 次）
            _sm.RequestAdvancePhase(); // Ended       进入（第 4 次）

            // Assert
            Assert.That(enteredCount, Is.EqualTo(4),
                "完整遍历（含初始 Preparation）共应触发 4 次 OnPhaseEntered。");
        }

        // ── 测试 5：OnPhaseExited 整个完整遍历恰好触发 3 次（Ended 无退出事件） ─

        [Test]
        public void Test_OnPhaseExited_FiresExactlyOnce_PerPhase()
        {
            // Arrange
            int exitedCount = 0;
            _sm.OnPhaseExited += _ => exitedCount++;
            _sm.InitialiseForTesting();

            // Act
            _sm.RequestAdvancePhase(); // Preparation 退出（第 1 次）
            _sm.RequestAdvancePhase(); // CheckInPeak  退出（第 2 次）
            _sm.RequestAdvancePhase(); // Recovery     退出（第 3 次）
            // Ended 是终态，不应触发退出事件。

            // Assert
            Assert.That(exitedCount, Is.EqualTo(3),
                "Preparation/CheckInPeak/Recovery 各退出一次，共 3 次；Ended 无退出事件。");
        }

        // ── 测试 6：Ended 后继续调用为无操作 ────────────────────────────────

        [Test]
        public void Test_AdvancingPastEnded_IsNoOp()
        {
            // Arrange
            int enteredCount = 0;
            int exitedCount = 0;
            _sm.OnPhaseEntered += _ => enteredCount++;
            _sm.OnPhaseExited += _ => exitedCount++;
            _sm.InitialiseForTesting(); // Preparation 进入（1 次）

            // 到达 Ended
            _sm.RequestAdvancePhase();
            _sm.RequestAdvancePhase();
            _sm.RequestAdvancePhase();

            // 记录抵达 Ended 时的计数
            int enteredAtEnded = enteredCount; // 应为 4
            int exitedAtEnded = exitedCount;   // 应为 3

            // Act: 在 Ended 状态下再调用
            _sm.RequestAdvancePhase();
            _sm.RequestAdvancePhase();

            // Assert: 状态不变，事件计数不变。
            Assert.That(_sm.CurrentPhase,
                Is.EqualTo(Room2DDayPhaseStateMachine.Room2DDayPhase.Ended),
                "Ended 后调用 RequestAdvancePhase() 不应改变状态。");

            Assert.That(enteredCount, Is.EqualTo(enteredAtEnded),
                "Ended 后调用不应再触发 OnPhaseEntered。");

            Assert.That(exitedCount, Is.EqualTo(exitedAtEnded),
                "Ended 后调用不应再触发 OnPhaseExited。");
        }
    }
}
