using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class StaffTest
    {
        [Test]
        public void Attributes_ClampTo0_100()
        {
            var a = new StaffAttributes(150, -10, 70);
            Assert.That(a.Speed, Is.EqualTo(100));
            Assert.That(a.Quality, Is.EqualTo(0));
            Assert.That(a.Stamina, Is.EqualTo(70));
        }

        [Test]
        public void LegacyCtor_HasDefaultAttributesMoraleAndNoTraits()
        {
            var s = new StaffMember(StaffRole.Reception, "Ann", 50);
            Assert.That(s.Attributes.Speed, Is.EqualTo(50));
            Assert.That(s.Morale, Is.EqualTo(StaffMember.DefaultMorale));
            Assert.That(s.Traits.Count, Is.EqualTo(0));
            Assert.That(s.EducationLevel, Is.EqualTo(0));
        }

        [Test]
        public void FullCtor_StoresAttributesEducationAndTraits()
        {
            var s = new StaffMember(StaffRole.Housekeeper, "Bob", 30,
                new StaffAttributes(40, 30, 60), 1,
                new[] { StaffTrait.Clumsy, StaffTrait.Cheap });
            Assert.That(s.Attributes.Quality, Is.EqualTo(30));
            Assert.That(s.EducationLevel, Is.EqualTo(1));
            Assert.That(s.HasTrait(StaffTrait.Clumsy), Is.True);
            Assert.That(s.HasTrait(StaffTrait.Charmer), Is.False);
            Assert.That(s.Traits.Count, Is.EqualTo(2));
        }

        [Test]
        public void AdjustMorale_Clamps()
        {
            var s = new StaffMember(StaffRole.Manager, "Mae", 90);
            s.AdjustMorale(-100);
            Assert.That(s.Morale, Is.EqualTo(0));
            s.AdjustMorale(250);
            Assert.That(s.Morale, Is.EqualTo(100));
        }

        [Test]
        public void GiveRaise_RaisesWageAndMorale_IgnoresLowerOffer()
        {
            var s = new StaffMember(StaffRole.Reception, "Liz", 60);
            s.AdjustMorale(-30); // morale 40
            s.GiveRaise(80);
            Assert.That(s.DailyWage, Is.EqualTo(80));
            Assert.That(s.Morale, Is.EqualTo(55)); // 40 + 15
            s.GiveRaise(50); // lower -> ignored
            Assert.That(s.DailyWage, Is.EqualTo(80));
        }

        [Test]
        public void WantsRaise_OnlyWhenAmbitiousAndPastThreshold()
        {
            var liz = new StaffMember(StaffRole.Reception, "Liz", 60,
                StaffAttributes.Default, 0, new[] { StaffTrait.Ambitious });
            var bob = new StaffMember(StaffRole.Housekeeper, "Bob", 30,
                StaffAttributes.Default, 0, new[] { StaffTrait.Cheap });
            Assert.That(liz.WantsRaise(5, 3), Is.True);
            Assert.That(liz.WantsRaise(2, 3), Is.False);
            Assert.That(bob.WantsRaise(99, 3), Is.False);
        }

        [Test]
        public void Traits_PositiveClassification()
        {
            Assert.That(StaffTraits.IsPositive(StaffTrait.Charmer), Is.True);
            Assert.That(StaffTraits.IsPositive(StaffTrait.Clumsy), Is.False);
        }
    }
}
