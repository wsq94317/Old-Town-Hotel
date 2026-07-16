using NUnit.Framework;
using UnityEngine;

// v2 世界层：楼层可见性——只显示当前层，切层 fire 事件（相机订阅跳层）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FloorVisibilityTest
    {
        private GameObject _root;
        private FloorVisibilityController _ctrl;
        private GameObject _f0, _f1, _f2;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("floors-root");
            _f0 = new GameObject("Floor1"); _f0.transform.SetParent(_root.transform);
            _f1 = new GameObject("Floor2"); _f1.transform.SetParent(_root.transform);
            _f2 = new GameObject("Floor3"); _f2.transform.SetParent(_root.transform);
            _ctrl = _root.AddComponent<FloorVisibilityController>();
            _ctrl.SetFloorsForTesting(new[] { _f0, _f1, _f2 });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ShowFloor_ActivatesOnlyTarget()
        {
            _ctrl.ShowFloor(1);
            Assert.IsFalse(_f0.activeSelf);
            Assert.IsTrue(_f1.activeSelf);
            Assert.IsFalse(_f2.activeSelf);
            Assert.AreEqual(1, _ctrl.CurrentFloor);
        }

        [Test]
        public void ShowFloor_FiresEventOnce_AndIgnoresSameFloor()
        {
            int fired = 0;
            _ctrl.OnFloorChanged += _ => fired++;
            _ctrl.ShowFloor(2);
            _ctrl.ShowFloor(2); // 同层重复调用不再触发
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void ShowFloor_ClampsIndex()
        {
            _ctrl.ShowFloor(99);
            Assert.AreEqual(2, _ctrl.CurrentFloor);
        }
    }
}
