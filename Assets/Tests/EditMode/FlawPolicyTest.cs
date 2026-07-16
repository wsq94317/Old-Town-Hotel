using System;
using NUnit.Framework;

// M3 瑕疵与漏检概率（随机注入，验证边界与单调性）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FlawPolicyTest
    {
        [Test]
        public void FlawChance_InterpolatesByQuality()
        {
            Assert.AreEqual(SupervisionTuning.FlawChanceAtZeroQuality, FlawPolicy.FlawChance(0), 0.001f);
            Assert.AreEqual(SupervisionTuning.FlawChanceAtFullQuality, FlawPolicy.FlawChance(100), 0.001f);
            // 单调递减
            Assert.Greater(FlawPolicy.FlawChance(20), FlawPolicy.FlawChance(80));
        }

        [Test]
        public void MissChance_InterpolatesByQuality()
        {
            Assert.AreEqual(SupervisionTuning.InspectorMissAtZeroQuality, FlawPolicy.InspectorMissChance(0), 0.001f);
            Assert.AreEqual(SupervisionTuning.InspectorMissAtFullQuality, FlawPolicy.InspectorMissChance(100), 0.001f);
        }

        [Test]
        public void Rolls_AreDeterministicWithSeed()
        {
            var a = new Random(42);
            var b = new Random(42);
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(FlawPolicy.RollFlaw(50, a), FlawPolicy.RollFlaw(50, b));
            }
        }

        [Test]
        public void ExtremeQuality_RollsMatchProbability()
        {
            // quality=100 → 5% 左右；quality=0 → 40% 左右（1000 次抽样容差 ±5%）
            var rng = new Random(7);
            int lowQ = 0, highQ = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (FlawPolicy.RollFlaw(0, rng)) lowQ++;
                if (FlawPolicy.RollFlaw(100, rng)) highQ++;
            }
            Assert.That(lowQ, Is.InRange(350, 450));
            Assert.That(highQ, Is.InRange(20, 90));
        }
    }
}
