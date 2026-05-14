using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试：Room2DDemoDayController 的 UI 只读访问器（ui-spec.md §6 / §3.2）。
// 覆盖：
//   - CurrentDay 直通 demoDayIndex
//   - PlayerCash 默认值与可通过反射写入私有字段后正确反映
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class Room2DDemoDayController_UiAccessors_Test
    {
        private GameObject _go;
        private Room2DDemoDayController _controller;

        [SetUp]
        public void SetUp()
        {
            // Arrange：构造受测组件，关闭自动引用查找以避免 Start 副作用拉入其他系统。
            _go = new GameObject("TestRoom2DDemoDayController");
            _controller = _go.AddComponent<Room2DDemoDayController>();
            _controller.autoFindReferences = false;
            _controller.startInPreparation = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── CurrentDay ──────────────────────────────────────────────────────

        [Test]
        public void test_currentDay_reflects_demoDayIndex_default()
        {
            // Arrange：默认 demoDayIndex = 1（字段初值）。
            // Act
            int day = _controller.CurrentDay;

            // Assert
            Assert.That(day, Is.EqualTo(1),
                "CurrentDay 应直通 demoDayIndex 的默认值 1。");
        }

        [Test]
        public void test_currentDay_reflects_demoDayIndex_after_update()
        {
            // Arrange
            _controller.demoDayIndex = 7;

            // Act
            int day = _controller.CurrentDay;

            // Assert
            Assert.That(day, Is.EqualTo(7),
                "CurrentDay 应直通 demoDayIndex 修改后的值。");
        }

        // ── PlayerCash ──────────────────────────────────────────────────────

        [Test]
        public void test_playerCash_default_matches_mockup_value()
        {
            // Arrange / Act
            int cash = _controller.PlayerCash;

            // Assert
            Assert.That(cash, Is.EqualTo(2450),
                "PlayerCash 默认值应为 2450（ui-spec.md §3.2 mockup 数值）。");
        }

        [Test]
        public void test_playerCash_reflects_private_field_after_reflection_set()
        {
            // Arrange：通过反射写入私有 playerCash 字段，模拟未来真实经济系统的更新。
            FieldInfo field = typeof(Room2DDemoDayController)
                .GetField("playerCash", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "测试前置：私有字段 playerCash 必须存在。");
            field.SetValue(_controller, 9999);

            // Act
            int cash = _controller.PlayerCash;

            // Assert
            Assert.That(cash, Is.EqualTo(9999),
                "PlayerCash getter 必须反映 playerCash 私有字段的当前值。");
        }
    }
}
