using System.Collections.Generic;
using NUnit.Framework;

// 把"看得见的房间"和"账上的房间"缝在一起（场景基础架构）。
// 玩家反馈："场景里还没有玩家可以解锁和装修的房间"——症结不是缺功能，
// 是那些功能和那栋楼没有关系（房间在场景里连碰撞体都没有）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class RoomSceneBindingTest
    {
        // ── 房号解析：可见房间的名字是唯一的线索 ──────────────────────────────

        [Test]
        public void RoomNumberComesOutOfTheObjectName()
        {
            Assert.That(RoomSceneBinder.ParseRoomNumber("Room_301"), Is.EqualTo(301));
            Assert.That(RoomSceneBinder.ParseRoomNumber("Room_201"), Is.EqualTo(201));
        }

        [Test]
        public void OnlyTheFirstRunOfDigitsCounts()
        {
            // "Room_301_Anchor" 之类的名字不能解析成 3011
            Assert.That(RoomSceneBinder.ParseRoomNumber("Room_301_Anchor"), Is.EqualTo(301));
            Assert.That(RoomSceneBinder.ParseRoomNumber("Room_204_Extra_7"), Is.EqualTo(204));
        }

        [Test]
        public void NamesWithoutDigitsAreIgnoredInsteadOfCrashing()
        {
            // 挂错物体时组件要安静地什么都不做，不能把整个场景带崩
            Assert.That(RoomSceneBinder.ParseRoomNumber("Wall_N"), Is.EqualTo(0));
            Assert.That(RoomSceneBinder.ParseRoomNumber(""), Is.EqualTo(0));
            Assert.That(RoomSceneBinder.ParseRoomNumber(null), Is.EqualTo(0));
        }

        // ── 配色：每种状态都必须一眼分得开 ────────────────────────────────────

        [Test]
        public void EveryRoomStateLooksDifferentFromEveryOther()
        {
            // 玩家实测抱怨过：破败房（301-304）和"家具坏了封房"的 201/202/205
            // 在场景里长得一模一样。颜色必须两两可分。
            var seen = new List<UnityEngine.Color>();
            foreach (RoomSimState state in System.Enum.GetValues(typeof(RoomSimState)))
            {
                UnityEngine.Color color = RoomStatePalette.ColorOf(state);
                foreach (var other in seen)
                {
                    float distance = UnityEngine.Mathf.Abs(color.r - other.r)
                                   + UnityEngine.Mathf.Abs(color.g - other.g)
                                   + UnityEngine.Mathf.Abs(color.b - other.b);
                    Assert.That(distance, Is.GreaterThan(0.12f),
                                state + " 的颜色和另一种状态太接近，手机屏幕上分不出来");
                }
                seen.Add(color);
            }
        }

        [Test]
        public void DerelictIsTheDarkestAndReadyIsNot()
        {
            // 破败 = 几乎黑（一眼看出"这里没人管"），可售 = 明亮
            float derelict = Luminance(RoomStatePalette.ColorOf(RoomSimState.Ruined));
            float ready = Luminance(RoomStatePalette.ColorOf(RoomSimState.Ready));

            Assert.That(derelict, Is.LessThan(ready), "破败房该比可售房暗得多");
            foreach (RoomSimState state in System.Enum.GetValues(typeof(RoomSimState)))
                if (state != RoomSimState.Ruined)
                    Assert.That(derelict, Is.LessThanOrEqualTo(Luminance(RoomStatePalette.ColorOf(state))),
                                "破败该是最暗的那个：" + state);
        }

        [Test]
        public void EveryStateHasAWordForTheLabel()
        {
            // 颜色只说"不一样"，词才说"是什么"。头顶标签靠它。
            var words = new HashSet<string>();
            foreach (RoomSimState state in System.Enum.GetValues(typeof(RoomSimState)))
            {
                string word = RoomStatePalette.WordOf(state);
                Assert.That(word, Is.Not.Empty, state.ToString());
                Assert.That(words.Add(word), Is.True, "两种状态用了同一个词：" + word);
            }
        }

        // ── 选中：一个房号就够，不需要事件总线 ────────────────────────────────

        [Test]
        public void TappingARoomSelectsIt_AndTappingAgainDeselects()
        {
            RoomSelection.Clear();

            RoomSelection.Select(301);
            Assert.That(RoomSelection.Selected, Is.EqualTo(301));

            RoomSelection.Select(301);
            Assert.That(RoomSelection.Selected, Is.EqualTo(0), "再点一下取消选中");
        }

        [Test]
        public void SelectingAnotherRoomSwitchesInsteadOfStacking()
        {
            RoomSelection.Clear();
            RoomSelection.Select(301);
            RoomSelection.Select(202);

            Assert.That(RoomSelection.Selected, Is.EqualTo(202), "一次只选一间");
        }

        [TearDown]
        public void ClearSelectionBetweenTests()
        {
            // 静态状态：不清的话上一条测试会毒到下一条
            RoomSelection.Clear();
        }

        private static float Luminance(UnityEngine.Color c) =>
            0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
