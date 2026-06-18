using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class CandidateGeneratorTest
    {
        private StaffArchetypeSO _arch;

        [SetUp]
        public void SetUp()
        {
            _arch = ScriptableObject.CreateInstance<StaffArchetypeSO>();
            _arch.role = StaffRole.Housekeeper;
            _arch.namePool = new[] { "Bob", "Liz", "Ann" };
            _arch.wageMin = 30; _arch.wageMax = 60;
            _arch.speedRange = new Vector2Int(40, 80);
            _arch.qualityRange = new Vector2Int(20, 70);
            _arch.staminaRange = new Vector2Int(50, 90);
            _arch.educationLevel = 1;
            _arch.possibleTraits = new[] { StaffTrait.Clumsy, StaffTrait.Cheap, StaffTrait.FastHands };
            _arch.minTraits = 1; _arch.maxTraits = 2;
        }

        [TearDown]
        public void TearDown() { if (_arch != null) Object.DestroyImmediate(_arch); }

        [Test]
        public void Generate_SameSeed_IsDeterministic()
        {
            var a = CandidateGenerator.Generate(_arch, 42);
            var b = CandidateGenerator.Generate(_arch, 42);
            Assert.That(b.Role, Is.EqualTo(a.Role));
            Assert.That(b.DisplayName, Is.EqualTo(a.DisplayName));
            Assert.That(b.DailyWage, Is.EqualTo(a.DailyWage));
            Assert.That(b.Attributes.Speed, Is.EqualTo(a.Attributes.Speed));
            Assert.That(b.Attributes.Quality, Is.EqualTo(a.Attributes.Quality));
            Assert.That(b.Attributes.Stamina, Is.EqualTo(a.Attributes.Stamina));
            Assert.That(b.Traits, Is.EqualTo(a.Traits));
        }

        [Test]
        public void Generate_RespectsWageAndAttributeRanges()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var c = CandidateGenerator.Generate(_arch, seed);
                Assert.That(c.Role, Is.EqualTo(StaffRole.Housekeeper));
                Assert.That(c.DailyWage, Is.InRange(30, 60));
                Assert.That(c.Attributes.Speed, Is.InRange(40, 80));
                Assert.That(c.Attributes.Quality, Is.InRange(20, 70));
                Assert.That(c.Attributes.Stamina, Is.InRange(50, 90));
                Assert.That(c.Traits.Count, Is.InRange(1, 2));
                Assert.That(c.EducationLevel, Is.EqualTo(1));
            }
        }

        [Test]
        public void GeneratePool_ReturnsRequestedCount()
        {
            var pool = CandidateGenerator.GeneratePool(new[] { _arch }, 3, 7);
            Assert.That(pool.Count, Is.EqualTo(3));
        }

        [Test]
        public void GeneratePool_EmptyOrZero_ReturnsEmpty()
        {
            Assert.That(CandidateGenerator.GeneratePool(new StaffArchetypeSO[0], 3, 1).Count, Is.EqualTo(0));
            Assert.That(CandidateGenerator.GeneratePool(new[] { _arch }, 0, 1).Count, Is.EqualTo(0));
        }
    }
}
