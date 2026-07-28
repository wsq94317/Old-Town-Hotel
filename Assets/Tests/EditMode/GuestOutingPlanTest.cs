using System;
using System.Collections.Generic;
using NUnit.Framework;

// 客人外出行程：断言的是**关系与不变量**（不许穿墙、不许赶不回来、客群作息有差别），
// 不锁具体数字——趟数与时长都是可调参数，锁死了以后没法平衡。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class GuestOutingPlanTest
    {
        private const int Morning = 8 * 60;    // 08:00
        private const int Closing = 22 * 60;   // 22:00

        private static Func<double> Rng(int seed)
        {
            var r = new Random(seed);
            return () => r.NextDouble();
        }

        private static List<GuestOuting> Plan(GuestSegment segment, int seed,
                                              int checkIn = 15 * 60, int lastReturn = Closing) =>
            GuestOutingPlan.For(segment, checkIn, lastReturn, Rng(seed));

        // ── 不变量：任何客群、任何种子都不许违反 ────────────────────────────────

        [Test]
        public void EveryOuting_LeavesAfterCheckIn_AndReturnsBeforeClosing()
        {
            foreach (GuestSegment segment in Enum.GetValues(typeof(GuestSegment)))
            for (int seed = 1; seed <= 60; seed++)
            {
                int checkIn = 13 * 60;
                var plan = Plan(segment, seed, checkIn);
                foreach (var outing in plan)
                {
                    Assert.That(outing.leaveMinute, Is.GreaterThan(checkIn),
                                $"{segment} seed{seed}：放好行李才能出门");
                    Assert.That(outing.returnMinute, Is.GreaterThan(outing.leaveMinute),
                                $"{segment} seed{seed}：回来得晚于出门");
                    Assert.That(outing.returnMinute, Is.LessThanOrEqualTo(Closing),
                                $"{segment} seed{seed}：打烊结账时人必须在房里——" +
                                "赶不回来的话退房动画会从空房里冒人");
                }
            }
        }

        [Test]
        public void OutingsNeverOverlap_AndLeaveTimeInTheRoomBetweenThem()
        {
            // 两趟挨在一起的话，客人看着像在门口鬼畜
            foreach (GuestSegment segment in Enum.GetValues(typeof(GuestSegment)))
            for (int seed = 1; seed <= 60; seed++)
            {
                var plan = Plan(segment, seed, 10 * 60);
                for (int i = 1; i < plan.Count; i++)
                {
                    int gap = plan[i].leaveMinute - plan[i - 1].returnMinute;
                    Assert.That(gap, Is.GreaterThanOrEqualTo(GuestOutingPlan.MinMinutesInRoomBetweenOutings),
                                $"{segment} seed{seed}：两趟之间要真的在房里待一会儿");
                }
            }
        }

        [Test]
        public void LateCheckIn_MeansNoTimeToGoOut()
        {
            // 21:30 才入住的客人当天不该再出门（时间上根本回不来）
            foreach (GuestSegment segment in Enum.GetValues(typeof(GuestSegment)))
            for (int seed = 1; seed <= 20; seed++)
                Assert.That(Plan(segment, seed, checkIn: 21 * 60 + 30), Is.Empty,
                            $"{segment} seed{seed}：这个点入住来不及出门");
        }

        [Test]
        public void EveningArrivals_StillGrabAQuickBite()
        {
            // 入住高峰在下午：按"整趟外出"排的话 18:00 住进来的客人一趟都排不进
            // 22:00 之前，当天大堂就此死寂。窗口不够要退化成快餐，不是不出门。
            int wentOut = 0;
            for (int seed = 1; seed <= 60; seed++)
                if (Plan(GuestSegment.Budget, seed, checkIn: 18 * 60).Count > 0) wentOut++;

            Assert.That(wentOut, Is.GreaterThan(0), "傍晚入住的客人也该有人出门吃饭");
        }

        [Test]
        public void ShortenedOutings_AreStillLongEnoughToBeWorthWatching()
        {
            // 缩短不能缩到"出门即回"——那看着像客人在门口抽搐
            foreach (GuestSegment segment in Enum.GetValues(typeof(GuestSegment)))
            for (int seed = 1; seed <= 60; seed++)
                foreach (var outing in Plan(segment, seed, checkIn: 19 * 60))
                    Assert.That(outing.returnMinute - outing.leaveMinute,
                                Is.GreaterThanOrEqualTo(GuestOutingPlan.MinOutingMinutes),
                                $"{segment} seed{seed}");
        }

        [Test]
        public void NeverMoreThanTheDailyCap()
        {
            foreach (GuestSegment segment in Enum.GetValues(typeof(GuestSegment)))
            for (int seed = 1; seed <= 60; seed++)
                Assert.That(Plan(segment, seed, Morning).Count,
                            Is.LessThanOrEqualTo(GuestOutingPlan.MaxOutingsPerDay), segment.ToString());
        }

        // ── 客群作息真的不一样 ────────────────────────────────────────────────

        [Test]
        public void BudgetGuests_GoOutMoreOften_ThanBusinessGuests()
        {
            // 大堂的"人来人往"主要靠预算客的高频短途
            Assert.That(AverageOutingCount(GuestSegment.Budget),
                        Is.GreaterThan(AverageOutingCount(GuestSegment.Business)),
                        "预算客出去找便宜饭，趟数更多");
        }

        [Test]
        public void BusinessGuests_StayOutLonger_PerTrip()
        {
            // 商务客一趟顶别人三趟：白天大堂空得有道理
            Assert.That(AverageOutingMinutes(GuestSegment.Business),
                        Is.GreaterThan(AverageOutingMinutes(GuestSegment.Budget)),
                        "出去办事一趟就是半天");
        }

        [Test]
        public void VipGuests_GoOutTheLeast()
        {
            // 酒店里就该有他要的一切
            double vip = AverageOutingCount(GuestSegment.Vip);
            foreach (GuestSegment other in Enum.GetValues(typeof(GuestSegment)))
            {
                if (other == GuestSegment.Vip) continue;
                Assert.That(vip, Is.LessThanOrEqualTo(AverageOutingCount(other)), other.ToString());
            }
        }

        // ── 查询：这一刻他在不在房里 ──────────────────────────────────────────

        [Test]
        public void IsOutAt_AnswersTheQuestionTheDirectorActuallyAsks()
        {
            var plan = new List<GuestOuting> { new GuestOuting(600, 700) };

            Assert.That(GuestOutingPlan.IsOutAt(plan, 599), Is.False, "还没出门");
            Assert.That(GuestOutingPlan.IsOutAt(plan, 600), Is.True, "出门那一刻起就不在房里");
            Assert.That(GuestOutingPlan.IsOutAt(plan, 699), Is.True);
            Assert.That(GuestOutingPlan.IsOutAt(plan, 700), Is.False, "回来那一刻算在房里");
            Assert.That(GuestOutingPlan.IsOutAt(null, 650), Is.False, "没行程=一直在房里，不能炸");
        }

        [Test]
        public void NoRandomSource_YieldsNoOutings_InsteadOfThrowing()
        {
            // 表现层拿不到随机源时要安静退化（客人一直待在房里），不能把整个场景带崩
            Assert.That(GuestOutingPlan.For(GuestSegment.Budget, Morning, Closing, null), Is.Empty);
        }

        // ── 统计辅助 ─────────────────────────────────────────────────────────

        private static double AverageOutingCount(GuestSegment segment)
        {
            int total = 0;
            const int Seeds = 200;
            for (int seed = 1; seed <= Seeds; seed++)
                total += Plan(segment, seed, Morning).Count;
            return (double)total / Seeds;
        }

        private static double AverageOutingMinutes(GuestSegment segment)
        {
            int minutes = 0, trips = 0;
            for (int seed = 1; seed <= 200; seed++)
                foreach (var outing in Plan(segment, seed, Morning))
                {
                    minutes += outing.returnMinute - outing.leaveMinute;
                    trips++;
                }
            Assert.That(trips, Is.GreaterThan(0), segment + " 一趟都没排出来，样本无意义");
            return (double)minutes / trips;
        }
    }
}
