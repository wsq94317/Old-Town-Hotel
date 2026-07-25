using NUnit.Framework;

// v3 员工模拟总账：摸鱼/产能/士气全部是 Sim 事实（修订版 3 的强化项）。
// StaffAgent 只负责演出——"这个员工正在摸鱼"必须先在 Sim 里成立，
// 否则玩家停在剖面层不进巡查层时，在线/巡查/离线会掷出三种现实。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class StaffRosterTest
    {
        private static StaffMember Housekeeper(string name, int speed = 60, int quality = 60)
            => new StaffMember(StaffRole.Housekeeper, name, 60,
                               new StaffAttributes(speed, quality, 60), 1, null);

        private static StaffMember LazyHousekeeper(string name)
            => new StaffMember(StaffRole.Housekeeper, name, 45,
                               new StaffAttributes(50, 45, 50), 0, new[] { StaffTrait.Lazy });

        [Test]
        public void Register_AssignsStableIncreasingIds()
        {
            var roster = new StaffRoster();

            int a = roster.Register(Housekeeper("Ann"));
            int b = roster.Register(Housekeeper("Bob"));

            Assert.That(a, Is.GreaterThan(0));
            Assert.That(b, Is.GreaterThan(a), "id 单调递增，是稳定身份（不能用对象引用当身份）");
            Assert.That(roster.Count, Is.EqualTo(2));

            roster.Remove(a);
            int c = roster.Register(Housekeeper("Cal"));
            Assert.That(c, Is.GreaterThan(b), "删除后 id 不复用——存档/事件引用不会张冠李戴");
            Assert.That(roster.Count, Is.EqualTo(2));
        }

        [Test]
        public void TryGet_FindsEntryAndFailsSafelyOnUnknownId()
        {
            var roster = new StaffRoster();
            int id = roster.Register(Housekeeper("Ann"));

            Assert.That(roster.TryGet(id, out StaffSimEntry entry), Is.True);
            Assert.That(entry.member.DisplayName, Is.EqualTo("Ann"));
            Assert.That(entry.state, Is.EqualTo(StaffOperationalState.OffShift), "未排班默认下班状态");

            Assert.That(roster.TryGet(9999, out _), Is.False);
            Assert.DoesNotThrow(() => roster.SetState(9999, StaffOperationalState.Working));
        }

        [Test]
        public void StartShift_PutsStaffOnDutyAndEndShiftSendsThemHome()
        {
            var roster = new StaffRoster();
            int id = roster.Register(Housekeeper("Ann"));

            roster.StartShift(id);
            Assert.That(roster.StateOf(id), Is.EqualTo(StaffOperationalState.Available));
            Assert.That(roster.OnDutyCount, Is.EqualTo(1));
            Assert.That(roster.OnDutyCountOfRole(StaffRole.Housekeeper), Is.EqualTo(1));

            roster.EndShift(id);
            Assert.That(roster.StateOf(id), Is.EqualTo(StaffOperationalState.OffShift));
            Assert.That(roster.OnDutyCount, Is.EqualTo(0));
        }

        [Test]
        public void TickMinute_AccumulatesWorkedAndSlackMinutesSeparately()
        {
            var roster = new StaffRoster();
            int id = roster.Register(Housekeeper("Ann"));
            roster.StartShift(id);
            roster.SetState(id, StaffOperationalState.Working);

            for (int i = 0; i < 10; i++) roster.TickMinute();
            roster.SetState(id, StaffOperationalState.Slacking);
            for (int i = 0; i < 5; i++) roster.TickMinute();

            roster.TryGet(id, out StaffSimEntry e);
            Assert.That(e.workedMinutesToday, Is.EqualTo(10));
            Assert.That(e.slackMinutesToday, Is.EqualTo(5), "摸鱼分钟单独计——产能损失从这里算");
            Assert.That(roster.StateOf(id), Is.EqualTo(StaffOperationalState.Slacking),
                        "摸鱼状态是 Sim 事实，不是动画状态");
        }

        [Test]
        public void RollSlackDecision_IsSimAuthority_AndOnlyFiresWhileUnsupervised()
        {
            var roster = new StaffRoster();
            int lazy = roster.Register(LazyHousekeeper("Slacker"));
            roster.StartShift(lazy);
            roster.SetState(lazy, StaffOperationalState.Working);

            // 经理在场：无论骰子多小都不摸鱼
            Assert.That(roster.RollSlackDecision(lazy, roll: 0.0, managerOnFloor: true), Is.False);
            Assert.That(roster.StateOf(lazy), Is.EqualTo(StaffOperationalState.Working));

            // 经理不在场 + 低骰 → 摸鱼
            Assert.That(roster.RollSlackDecision(lazy, roll: 0.0, managerOnFloor: false), Is.True);
            Assert.That(roster.StateOf(lazy), Is.EqualTo(StaffOperationalState.Slacking));

            // 高骰不触发
            roster.SetState(lazy, StaffOperationalState.Working);
            Assert.That(roster.RollSlackDecision(lazy, roll: 0.999, managerOnFloor: false), Is.False);
        }

        [Test]
        public void LazyTrait_SlacksMoreThanDiligentStaff()
        {
            var roster = new StaffRoster();
            int lazy = roster.Register(LazyHousekeeper("Slacker"));
            int good = roster.Register(Housekeeper("Ann", speed: 80, quality: 80));
            roster.StartShift(lazy);
            roster.StartShift(good);

            Assert.That(roster.SlackChanceOf(lazy), Is.GreaterThan(roster.SlackChanceOf(good)),
                        "便宜/懒惰员工更容易出问题（设计要求）");
        }

        [Test]
        public void ReportCaught_AppliesMoraleHitAndPutsThemBackToWork()
        {
            var roster = new StaffRoster();
            int id = roster.Register(LazyHousekeeper("Slacker"));
            roster.StartShift(id);
            roster.SetState(id, StaffOperationalState.Slacking);
            roster.TryGet(id, out StaffSimEntry e);
            int moraleBefore = e.member.Morale;

            bool caught = roster.ReportCaught(id);

            Assert.That(caught, Is.True, "巡查层上报玩家抓包行为，Sim 判定是否真在摸鱼");
            Assert.That(e.caughtCountToday, Is.EqualTo(1));
            Assert.That(e.member.Morale, Is.LessThan(moraleBefore), "被抓掉士气");
            Assert.That(roster.StateOf(id), Is.EqualTo(StaffOperationalState.Working), "被抓后立刻装作在干活");
        }

        [Test]
        public void ReportCaught_OnHonestStaff_IsFalseAndCostsMorale()
        {
            // 错怪好人：抓包判定查 Sim 状态而非动画，冤枉了要付代价（v2 质询玩法的语义）
            var roster = new StaffRoster();
            int id = roster.Register(Housekeeper("Ann"));
            roster.StartShift(id);
            roster.SetState(id, StaffOperationalState.Working);
            roster.TryGet(id, out StaffSimEntry e);
            int moraleBefore = e.member.Morale;

            bool caught = roster.ReportCaught(id);

            Assert.That(caught, Is.False);
            Assert.That(e.caughtCountToday, Is.EqualTo(0));
            Assert.That(e.member.Morale, Is.LessThan(moraleBefore), "错怪好人士气大降");
        }

        [Test]
        public void SettleDay_ResetsCountersAndAccumulatesFatigue()
        {
            var roster = new StaffRoster();
            int id = roster.Register(Housekeeper("Ann"));
            roster.StartShift(id);
            roster.SetState(id, StaffOperationalState.Working);
            for (int i = 0; i < 600; i++) roster.TickMinute(); // 满班 10 小时

            roster.SettleDay(wagesPaid: true);

            roster.TryGet(id, out StaffSimEntry e);
            Assert.That(e.workedMinutesToday, Is.EqualTo(0), "日结清零当日计数");
            Assert.That(e.slackMinutesToday, Is.EqualTo(0));
            Assert.That(e.caughtCountToday, Is.EqualTo(0));
            Assert.That(e.fatigue, Is.GreaterThan(0f), "满班累积疲劳");
            Assert.That(e.state, Is.EqualTo(StaffOperationalState.OffShift));
        }

        [Test]
        public void SettleDay_UnpaidWages_CrushMoraleAcrossWholeRoster()
        {
            var roster = new StaffRoster();
            int a = roster.Register(Housekeeper("Ann"));
            int b = roster.Register(Housekeeper("Bob"));
            roster.TryGet(a, out StaffSimEntry ea);
            roster.TryGet(b, out StaffSimEntry eb);
            int before = ea.member.Morale;

            roster.SettleDay(wagesPaid: false);

            Assert.That(ea.member.Morale, Is.EqualTo(before + StaffDayModel.UnpaidWageMoralePenalty));
            Assert.That(eb.member.Morale, Is.EqualTo(before + StaffDayModel.UnpaidWageMoralePenalty));
        }

        [Test]
        public void MoraleFactor_StaysWithinDesignedBand()
        {
            Assert.That(StaffDayModel.MoraleFactor(0), Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(StaffDayModel.MoraleFactor(100), Is.EqualTo(1.1f).Within(1e-4f));
            Assert.That(StaffDayModel.MoraleFactor(50), Is.InRange(0.7f, 1.1f));
            Assert.That(StaffDayModel.MoraleFactor(-999), Is.EqualTo(0.7f).Within(1e-4f), "越界钳住");
            Assert.That(StaffDayModel.MoraleFactor(999), Is.EqualTo(1.1f).Within(1e-4f));
        }

        [Test]
        public void CleanRoomsPerHour_ScalesWithSpeedMoraleAndFatigue()
        {
            var fast = Housekeeper("Fast", speed: 90);
            var slow = Housekeeper("Slow", speed: 30);

            float fastRate = StaffDayModel.CleanRoomsPerHour(fast, fatigue: 0f);
            float slowRate = StaffDayModel.CleanRoomsPerHour(slow, fatigue: 0f);
            Assert.That(fastRate, Is.GreaterThan(slowRate), "速度属性影响吞吐");

            float tiredRate = StaffDayModel.CleanRoomsPerHour(fast, fatigue: 1f);
            Assert.That(tiredRate, Is.LessThan(fastRate), "疲劳降吞吐");
            Assert.That(tiredRate, Is.GreaterThan(0f), "再累也不该归零（否则酒店永久卡死）");

            fast.AdjustMorale(-70);
            Assert.That(StaffDayModel.CleanRoomsPerHour(fast, fatigue: 0f), Is.LessThan(fastRate),
                        "低士气降吞吐——排班精简→士气崩→周转慢的链路起点");
        }

        [Test]
        public void QuitIntent_OnlyAtRockBottomMorale()
        {
            Assert.That(StaffDayModel.WantsToQuit(morale: 70, roll: 0.0), Is.False, "正常士气不会走人");
            Assert.That(StaffDayModel.WantsToQuit(morale: 5, roll: 0.0), Is.True, "士气见底 + 低骰 → 主动离职");
            Assert.That(StaffDayModel.WantsToQuit(morale: 5, roll: 0.999), Is.False, "高骰这天忍了");
        }

        [Test]
        public void ProductiveHeadcount_ExcludesSlackersAndOffShift()
        {
            var roster = new StaffRoster();
            int working = roster.Register(Housekeeper("Ann"));
            int slacking = roster.Register(LazyHousekeeper("Slacker"));
            int home = roster.Register(Housekeeper("Cal"));

            roster.StartShift(working);
            roster.SetState(working, StaffOperationalState.Working);
            roster.StartShift(slacking);
            roster.SetState(slacking, StaffOperationalState.Slacking);
            // home 不排班

            Assert.That(roster.ProductiveCountOfRole(StaffRole.Housekeeper), Is.EqualTo(1),
                        "摸鱼的和下班的都不产出——产能损失直接反映在这里");
            Assert.That(roster.OnDutyCountOfRole(StaffRole.Housekeeper), Is.EqualTo(2));
        }
    }
}
