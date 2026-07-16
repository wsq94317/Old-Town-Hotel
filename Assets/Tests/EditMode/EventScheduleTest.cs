using System;
using NUnit.Framework;

// M4 事件排程：2-3 个不重复、时窗内定时、按时间排序、种子确定。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class EventScheduleTest
    {
        [Test]
        public void Schedules_TwoOrThree_DistinctEvents()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var list = EventScheduleLogic.ScheduleForDay(EventCatalog.All, new Random(seed));
                Assert.That(list.Count, Is.InRange(2, 3), "seed " + seed);
                var ids = new System.Collections.Generic.HashSet<string>();
                foreach (var s in list) Assert.IsTrue(ids.Add(s.Def.Id), "duplicate in seed " + seed);
            }
        }

        [Test]
        public void TriggerHours_WithinWindow_AndSorted()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var list = EventScheduleLogic.ScheduleForDay(EventCatalog.All, new Random(seed));
                float prev = float.MinValue;
                foreach (var s in list)
                {
                    Assert.GreaterOrEqual(s.TriggerHour, s.Def.MinHour);
                    Assert.LessOrEqual(s.TriggerHour, s.Def.MaxHour);
                    Assert.GreaterOrEqual(s.TriggerHour, prev);
                    prev = s.TriggerHour;
                }
            }
        }

        [Test]
        public void SameSeed_SameSchedule()
        {
            var a = EventScheduleLogic.ScheduleForDay(EventCatalog.All, new Random(42));
            var b = EventScheduleLogic.ScheduleForDay(EventCatalog.All, new Random(42));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Def.Id, b[i].Def.Id);
                Assert.AreEqual(a[i].TriggerHour, b[i].TriggerHour, 0.0001f);
            }
        }

        [Test]
        public void Catalog_HasSixEvents_EachWithTwoOptions()
        {
            Assert.AreEqual(6, EventCatalog.All.Count);
            foreach (var e in EventCatalog.All) Assert.AreEqual(2, e.Options.Length, e.Id);
        }
    }
}
