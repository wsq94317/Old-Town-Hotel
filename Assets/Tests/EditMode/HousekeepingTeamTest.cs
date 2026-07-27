using System.Collections.Generic;
using NUnit.Framework;

// 客房部人数的非线性 + 日内疲劳（试玩反馈："HSK 用人应该是最关键的部分，但我没体会到"）。
// 断的是形状与取舍，不是具体数值——倍率都是调参旋钮。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class HousekeepingTeamTest
    {
        private static StaffRoster RosterWith(int housekeepers)
        {
            var roster = new StaffRoster();
            for (int i = 0; i < housekeepers; i++)
                roster.Register(new StaffMember(StaffRole.Housekeeper, "H" + i, 60,
                                                new StaffAttributes(55, 55, 55), 1, null));
            var plan = new ShiftPlan();
            plan.SetTier(StaffRole.Housekeeper, ShiftTier.Full);
            plan.ApplyTo(roster);
            // ApplyTo 只把人排到 Available，而**只有 Working 才产出**（Available→Working
            // 由 SimPipeline 每分钟推进）。单测不跑管线，所以这里直接置 Working，
            // 否则吞吐恒为 0、疲劳也不累积，测出来的全是假象。
            foreach (var e in roster.Entries) roster.SetState(e.staffId, StaffOperationalState.Working);
            return roster;
        }

        // ── 团队规模的形状 ────────────────────────────────────────────────────

        [Test]
        public void WorkingAlone_IsPenalised()
        {
            Assert.That(HousekeepingTeamModel.PerPersonFactor(1), Is.LessThan(1f),
                        "一个人要自己搬布草、跑楼层、开门关门——独自干就该被罚");
            Assert.That(HousekeepingTeamModel.PerPersonFactor(2), Is.GreaterThan(
                        HousekeepingTeamModel.PerPersonFactor(1)),
                        "第二个人到位，人均效率就回到正常");
        }

        [Test]
        public void HiringTheSecondPerson_MoreThanDoublesThroughput()
        {
            float solo = 1f * HousekeepingTeamModel.PerPersonFactor(1);
            float pair = 2f * HousekeepingTeamModel.PerPersonFactor(2);

            Assert.That(pair, Is.GreaterThan(solo * 2f),
                        "1→2 人必须是**超线性**的：这正是'再雇一个人像换了台机器'的手感");
        }

        [Test]
        public void BigTeams_StartTreadingOnEachOther()
        {
            float comfortable = HousekeepingTeamModel.PerPersonFactor(
                HousekeepingTeamModel.ComfortableSize);
            float crowded = HousekeepingTeamModel.PerPersonFactor(
                HousekeepingTeamModel.CrowdedAtSize);

            Assert.That(crowded, Is.LessThan(comfortable), "走廊、货梯、备品车会互相挤");
            Assert.That(HousekeepingTeamModel.PerPersonFactor(20),
                        Is.EqualTo(HousekeepingTeamModel.CrowdedFactor).Within(1e-4f),
                        "拥堵有下限，不会一路掉到零");
        }

        [Test]
        public void TheCurveHasNoCliff()
        {
            // 每多一个人，人均效率的变化都必须是平滑的——
            // 否则会出现"多雇一人反而断崖式变差"这种没人看得懂的反直觉
            float previous = HousekeepingTeamModel.PerPersonFactor(2);
            for (int n = 3; n <= 12; n++)
            {
                float current = HousekeepingTeamModel.PerPersonFactor(n);
                Assert.That(previous - current, Is.LessThan(0.1f),
                            $"{n - 1}→{n} 人的人均效率跌了 {previous - current:0.000}，太陡了");
                previous = current;
            }
        }

        [Test]
        public void NobodyOnDuty_MeansNoThroughput()
        {
            Assert.That(HousekeepingTeamModel.PerPersonFactor(0), Is.EqualTo(0f));
            Assert.That(ServiceCapacityModel.CleanRoomsPerHour(RosterWith(0), 1f), Is.EqualTo(0f));
        }

        // ── 接进真实吞吐 ──────────────────────────────────────────────────────

        [Test]
        public void TotalThroughput_IsNonLinearInHeadcount()
        {
            float one = ServiceCapacityModel.CleanRoomsPerHour(RosterWith(1), 1f);
            float two = ServiceCapacityModel.CleanRoomsPerHour(RosterWith(2), 1f);

            Assert.That(one, Is.GreaterThan(0f));
            Assert.That(two, Is.GreaterThan(one * 2f),
                        "两个人的总吞吐要多于单人的两倍（人数不是线性相加）");
        }

        [Test]
        public void OneHousekeeper_TakesMostOfTheShiftToClearTenRooms()
        {
            // 用户给的手感基准：10 间房，一个人要 8 小时（会耽误 check-in），两个人 3 小时。
            // 断的是"一个人撑不住、两个人才够"这个关系，容差给得很宽——数值是旋钮。
            float solo = ServiceCapacityModel.CleanRoomsPerHour(RosterWith(1), 1f);
            float pair = ServiceCapacityModel.CleanRoomsPerHour(RosterWith(2), 1f);

            float soloHours = HousekeepingTeamModel.HoursToClear(10, solo);
            float pairHours = HousekeepingTeamModel.HoursToClear(10, pair);

            Assert.That(soloHours, Is.GreaterThan(6f),
                        $"一个人清 10 间该是「几乎干一整天」（实测 {soloHours:0.0} 小时）");
            Assert.That(pairHours, Is.LessThan(4f),
                        $"两个人该在半天内清完（实测 {pairHours:0.0} 小时）");
            Assert.That(pairHours * 2f, Is.LessThan(soloHours), "翻倍人手要换来多于翻倍的速度");
        }

        [Test]
        public void HoursToClear_HandlesNothingToDoAndNobodyToDoIt()
        {
            Assert.That(HousekeepingTeamModel.HoursToClear(0, 3f), Is.EqualTo(0f));
            Assert.That(HousekeepingTeamModel.HoursToClear(10, 0f), Is.LessThan(0f),
                        "没人干活要能被识别出来，而不是返回一个假的小时数");
        }

        // ── 日内疲劳 ──────────────────────────────────────────────────────────

        [Test]
        public void FatigueAccumulatesDuringTheDay_NotOnlyAtSettlement()
        {
            var roster = RosterWith(2);
            float before = roster.Entries[0].fatigue;

            for (int m = 0; m < 300; m++) roster.TickMinute();

            Assert.That(roster.Entries[0].fatigue, Is.GreaterThan(before),
                        "干了五个小时就该累了——以前疲劳只在日结算一次，日内恒定，"
                        + "「干到下午会变慢」压根没建模");
        }

        [Test]
        public void ThroughputSagsAsTheShiftWearsOn()
        {
            var roster = RosterWith(2);
            float fresh = ServiceCapacityModel.CleanRoomsPerHour(roster, 1f);

            for (int m = 0; m < 480; m++) roster.TickMinute();   // 干满八小时
            float tired = ServiceCapacityModel.CleanRoomsPerHour(roster, 1f);

            Assert.That(tired, Is.LessThan(fresh), "累了就慢了");
            Assert.That(tired, Is.GreaterThan(fresh * 0.5f), "但不能慢到停摆——酒店不该被疲劳锁死");
        }

        [Test]
        public void FatigueIsNotDoubleCountedAtSettlement()
        {
            var roster = RosterWith(1);
            for (int m = 0; m < 600; m++) roster.TickMinute();
            float endOfShift = roster.Entries[0].fatigue;
            Assert.That(endOfShift, Is.GreaterThan(0f));

            roster.SettleDay(wagesPaid: true);

            // 日结只做夜间恢复。若这里又按当日工时加一次，疲劳会被算两遍
            Assert.That(roster.Entries[0].fatigue,
                        Is.EqualTo(SimMath.Clamp01(endOfShift - StaffDayModel.FatigueRecovery())).Within(1e-4f),
                        "日结只该恢复，不该再加一次当日疲劳");
        }

        [Test]
        public void RestingOvernight_ActuallyHelps()
        {
            var roster = RosterWith(1);
            for (int m = 0; m < 600; m++) roster.TickMinute();
            float worn = roster.Entries[0].fatigue;

            roster.SettleDay(wagesPaid: true);

            Assert.That(roster.Entries[0].fatigue, Is.LessThan(worn), "睡一觉要能缓过来");
        }

        [Test]
        public void SlackingDoesNotMakeYouTired()
        {
            var roster = RosterWith(1);
            var entry = roster.Entries[0];
            roster.SetState(entry.staffId, StaffOperationalState.Slacking);
            float before = entry.fatigue;

            // 摸鱼有 5-20 分钟时长，到点自己回去干活——只能在"还在摸"的窗口里断言，
            // 否则测到的是他摸完之后又干活攒的疲劳（第一版就这么挂的）
            int ticked = 0;
            while (entry.state == StaffOperationalState.Slacking && ticked < 60)
            {
                roster.TickMinute();
                ticked++;
            }

            Assert.That(ticked, Is.GreaterThan(0), "得真的摸了几分钟才测得到");
            Assert.That(entry.fatigue, Is.EqualTo(before).Within(1e-6f),
                        "摸鱼不累——摸鱼的代价是产出，不是疲劳");
        }
    }
}
