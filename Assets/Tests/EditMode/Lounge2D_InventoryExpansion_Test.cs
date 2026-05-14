using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试：Lounge2D 库存模型扩展（设计 UI mockup 2026-05-14 v1）。
//
// 背景：旧版 Lounge2D 只有 teaCoffeeStock 一个字段；UI mockup 显示 6 项：
//   Clean Cups / Dirty Cups / Milk / Tea / Coffee / Syrup。
// 本次重构：teaCoffeeStock → teaStock（重命名 + FormerlySerializedAs 保留 scene 值），
//          新增 coffeeStock / syrupStock（仅为 UI 数据字段，暂无消耗规则）。
//
// 测试范围：
//   1) 默认值匹配（coffee 与 tea 同基线；syrup 走 mockup 的较低基线 15）
//   2) lowStockThreshold 同时作用于 tea / coffee / syrup
//   3) 既有的服务消耗仍然减的是 teaStock，不是 coffeeStock / syrupStock
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class Lounge2D_InventoryExpansion_Test
    {
        private GameObject _loungeGo;
        private Lounge2D _lounge;

        [SetUp]
        public void SetUp()
        {
            _loungeGo = new GameObject("TestLounge");
            _lounge = _loungeGo.AddComponent<Lounge2D>();
            // 关闭自动找引用 & 自动服务循环，避免 Update 副作用干扰 EditMode 断言。
            _lounge.autoFindReferences = false;
            _lounge.runDuringPlay = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_loungeGo);
        }

        [Test]
        public void test_default_values_for_coffee_and_syrup_match_inventory_baseline()
        {
            // Arrange：AddComponent 后字段为类型默认值。
            // Act：手动调用 ResetPrototypeLounge 以拿到“规范基线”而不是 Inspector 默认。
            _lounge.ResetPrototypeLounge();

            // Assert
            Assert.That(_lounge.teaStock, Is.EqualTo(12),
                "teaStock 基线应与重构前的 teaCoffeeStock 一致（12）。");
            Assert.That(_lounge.coffeeStock, Is.EqualTo(12),
                "coffeeStock 基线应与 teaStock 一致（12），匹配 UI mockup。");
            Assert.That(_lounge.syrupStock, Is.EqualTo(15),
                "syrupStock 基线应为 15，匹配 UI mockup 的 15/30 读数。");
        }

        [Test]
        public void test_lowStockThreshold_applies_to_tea_coffee_syrup()
        {
            // Arrange：把阈值拉高到 5；milk 充足。
            _lounge.ResetPrototypeLounge();
            _lounge.lowStockThreshold = 5;
            _lounge.milkStock = 20;
            _lounge.teaStock = 20;
            _lounge.coffeeStock = 20;
            _lounge.syrupStock = 20;

            // baseline：充足时不应触发 stock risk
            Assert.That(_lounge.HasStockRisk(), Is.False,
                "全部充足时 HasStockRisk 必须为 false。");

            // Act + Assert：依次让 tea / coffee / syrup 跌破阈值，验证三者各自能触发 HasStockRisk。
            _lounge.teaStock = 4;
            Assert.That(_lounge.HasStockRisk(), Is.True,
                "tea 跌破 lowStockThreshold 时 HasStockRisk 必须为 true。");
            _lounge.teaStock = 20;

            _lounge.coffeeStock = 4;
            Assert.That(_lounge.HasStockRisk(), Is.True,
                "coffee 跌破 lowStockThreshold 时 HasStockRisk 必须为 true。");
            _lounge.coffeeStock = 20;

            _lounge.syrupStock = 4;
            Assert.That(_lounge.HasStockRisk(), Is.True,
                "syrup 跌破 lowStockThreshold 时 HasStockRisk 必须为 true。");
        }

        [Test]
        public void test_teaStock_consumption_unchanged_after_rename()
        {
            // Arrange：与重构前一致——ServeOneLoungeDemand 应消耗 1 clean cup / 1 milk / 1 tea。
            _lounge.ResetPrototypeLounge();
            int cleanBefore = _lounge.cleanCups;
            int dirtyBefore = _lounge.dirtyCups;
            int milkBefore = _lounge.milkStock;
            int teaBefore = _lounge.teaStock;
            int coffeeBefore = _lounge.coffeeStock;
            int syrupBefore = _lounge.syrupStock;

            // Act
            _lounge.ServeOneLoungeDemand();

            // Assert：tea 必须被减；coffee / syrup 不应受影响（暂无消耗规则）。
            Assert.That(_lounge.teaStock, Is.EqualTo(teaBefore - 1),
                "ServeOneLoungeDemand 应当只减 teaStock（重命名前是 teaCoffeeStock）。");
            Assert.That(_lounge.milkStock, Is.EqualTo(milkBefore - 1),
                "milkStock 消耗行为未变。");
            Assert.That(_lounge.cleanCups, Is.EqualTo(cleanBefore - 1),
                "cleanCups 消耗行为未变。");
            Assert.That(_lounge.dirtyCups, Is.EqualTo(dirtyBefore + 1),
                "dirtyCups 自增行为未变。");
            Assert.That(_lounge.coffeeStock, Is.EqualTo(coffeeBefore),
                "coffeeStock 仅为 UI 字段，本次消耗不应改变其值。");
            Assert.That(_lounge.syrupStock, Is.EqualTo(syrupBefore),
                "syrupStock 仅为 UI 字段，本次消耗不应改变其值。");
        }
    }
}
