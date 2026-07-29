using System.Collections.Generic;
using NUnit.Framework;

// 夜班（用户要求）：加成算得清、夜班的人白天不干活、夜间前台接住打烊时排队的客人、
// 不排夜班要吃差评。断言锁**关系与因果**，倍率数字本身随时可调。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class NightShiftTest
    {
        // 第 1 天是周一（PricingPolicy：dow 0=周一，>=5 才是周末）
        private const int Weekday = 1;
        private const int Weekend = 6;   // 周六

        /// <summary>receptionSpeed 低 = 一个手慢的前台。想造出"排到打烊还没办完"
        /// 的局面必须让**到店量真的超过前台产能**：默认 55 速的前台一天能办 80 多个，
        /// 8 间房的小店永远排不起队（第一版测试就是这么写错的）。</summary>
        private static HotelSim BuildHotel(int rooms = 8, int receptionists = 2, int housekeepers = 1,
                                           int receptionSpeed = 55)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            for (int i = 0; i < receptionists; i++)
                staff.Register(new StaffMember(StaffRole.Reception, "RCP" + i, 65,
                                               new StaffAttributes(receptionSpeed, 55, 55), 1, null));
            for (int i = 0; i < housekeepers; i++)
                staff.Register(new StaffMember(StaffRole.Housekeeper, "HSK" + i, 60,
                                               new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, 5150);
            sim.FurnishInheritedRooms();
            return sim;
        }

        // ── 加成：能心算 ──────────────────────────────────────────────────────

        [Test]
        public void WeekendNight_IsAdditive_NotMultiplicative()
        {
            // 1.3 × 1.5 = 1.95 会让玩家觉得被暗算；1 + 0.3 + 0.5 = 1.8 一眼能算
            float weekendNight = ShiftPremium.MultiplierFor(ShiftSlot.Night, weekend: true);

            Assert.That(weekendNight, Is.EqualTo(1.8f).Within(1e-4f));
            Assert.That(weekendNight, Is.LessThan(
                ShiftPremium.MultiplierFor(ShiftSlot.Day, true) *
                ShiftPremium.MultiplierFor(ShiftSlot.Night, false)),
                "加法必须比乘法便宜，否则'加法更好懂'这个理由就白说了");
        }

        [Test]
        public void EveryPremiumCostsMoreThanPlainWeekdayDay()
        {
            int baseWage = 65;
            int plain = ShiftPremium.WageFor(baseWage, ShiftSlot.Day, weekend: false);
            int weekendDay = ShiftPremium.WageFor(baseWage, ShiftSlot.Day, weekend: true);
            int weekdayNight = ShiftPremium.WageFor(baseWage, ShiftSlot.Night, weekend: false);
            int weekendNight = ShiftPremium.WageFor(baseWage, ShiftSlot.Night, weekend: true);

            Assert.That(plain, Is.EqualTo(baseWage), "平日白班就是基准");
            Assert.That(weekendDay, Is.GreaterThan(plain));
            Assert.That(weekdayNight, Is.GreaterThan(weekendDay), "没人愿意上夜班，夜班比周末更贵");
            Assert.That(weekendNight, Is.GreaterThan(weekdayNight));
        }

        [Test]
        public void WagesRise_OnWeekends_WithoutHiringAnybody()
        {
            // 同一批人、同一档排班，只是日子变了——玩家在周末账单上要看得出来
            var sim = BuildHotel();
            sim.Shifts.ApplyTo(sim.Staff);

            int weekdayBill = sim.Shifts.DailyWageCost(sim.Staff, Weekday);
            int weekendBill = sim.Shifts.DailyWageCost(sim.Staff, Weekend);

            Assert.That(weekdayBill, Is.GreaterThan(0));
            Assert.That(weekendBill, Is.GreaterThan(weekdayBill), "周末班要更高工资");
        }

        [Test]
        public void WageCostWithoutADay_StaysOnTheOldBaseline()
        {
            // 不传日号的旧调用（原型场景、既有测试）必须还按平日白班算，
            // 否则加夜班这件事会静默改掉所有历史调参
            var sim = BuildHotel();
            sim.Shifts.ApplyTo(sim.Staff);

            Assert.That(sim.Shifts.DailyWageCost(sim.Staff),
                        Is.EqualTo(sim.Shifts.DailyWageCost(sim.Staff, Weekday)));
        }

        // ── 夜班的人白天在睡觉 ────────────────────────────────────────────────

        [Test]
        public void NightStaff_DoNotWorkTheDayShift_ButStillGetPaid()
        {
            var sim = BuildHotel(receptionists: 2);
            sim.Shifts.SetTier(StaffRole.Reception, ShiftTier.Full);
            sim.Shifts.ApplyTo(sim.Staff);
            int dayOnlyBill = sim.Shifts.DailyWageCost(sim.Staff, Weekday);
            int deskByDay = sim.Staff.OnDutyCountOfRole(StaffRole.Reception);

            sim.Shifts.SetNightCount(StaffRole.Reception, 1);
            sim.Shifts.ApplyTo(sim.Staff);

            Assert.That(sim.Shifts.NightStaffOnDuty, Is.EqualTo(1));
            Assert.That(sim.Staff.OnDutyCountOfRole(StaffRole.Reception),
                        Is.EqualTo(deskByDay - 1), "上夜班的人白天不在岗");
            Assert.That(sim.Shifts.DailyWageCost(sim.Staff, Weekday), Is.GreaterThan(dayOnlyBill - 65),
                        "夜班工资照发，而且带加成——总账不该因为他白天不在就变便宜");
        }

        [Test]
        public void NightShift_NeverEmptiesTheDayShift()
        {
            // 全店都去上夜班的话白天没人开门——这条守卫必须挡住玩家的手滑
            var sim = BuildHotel(receptionists: 2);
            sim.Shifts.SetNightCount(StaffRole.Reception, 99);
            sim.Shifts.ApplyTo(sim.Staff);

            Assert.That(sim.Staff.OnDutyCountOfRole(StaffRole.Reception), Is.GreaterThan(0),
                        "白天必须还有人守前台");
        }

        [Test]
        public void NobodyWorksBothShifts()
        {
            // 同一个人白天黑夜连轴转 = 工资翻倍而产能没变（早期原型踩过的逻辑坑）
            var sim = BuildHotel(receptionists: 3);
            sim.Shifts.SetTier(StaffRole.Reception, ShiftTier.Full);
            sim.Shifts.SetNightCount(StaffRole.Reception, 1);
            sim.Shifts.ApplyTo(sim.Staff);

            foreach (var entry in sim.Staff.Entries)
                if (sim.Shifts.IsOnNightShift(entry.staffId))
                    Assert.That(entry.IsOnDuty, Is.False,
                                entry.member.DisplayName + " 白天黑夜连轴转了");
        }

        // ── 夜间前台：接住打烊时排队的客人 ────────────────────────────────────

        [Test]
        public void WithoutANightDesk_GuestsQueuedAtClosingAreLostAndAngry()
        {
            // 以前这些人在 BeginDay 被静默清掉：白丢房费，玩家还看不到
            // 40 间房只有一个手慢的前台：到店量真的超过产能，队伍排到打烊
            var sim = BuildHotel(rooms: 40, receptionists: 1, receptionSpeed: 10);
            sim.BeginDay();
            sim.RunToEndOfDay();
            int queuedAtClosing = sim.DeskQueueLength;
            Assert.That(queuedAtClosing, Is.GreaterThan(0), "得先有人排到打烊，这条对照才有意义");

            float satBefore = sim.Reputation.AverageSatisfaction;
            sim.SettleDay();

            Assert.That(sim.NightLockedOutToday, Is.EqualTo(queuedAtClosing),
                        "排到打烊的人全都吃了闭门羹");
            Assert.That(sim.NightCheckInsToday, Is.EqualTo(0));
            Assert.That(sim.Reputation.AverageSatisfaction, Is.LessThan(satBefore),
                        "对着锁门站一夜要记差评——比'没来'更伤");
            Assert.That(sim.DeskQueueLength, Is.EqualTo(0), "队列必须被处理干净，不能留到明天");
        }

        [Test]
        public void ANightDesk_TurnsThoseSameGuestsIntoPaidRoomNights()
        {
            // 同种子对照：唯一差别是排了夜班
            var noNight = BuildHotel(rooms: 40, receptionists: 2, receptionSpeed: 10);
            var withNight = BuildHotel(rooms: 40, receptionists: 2, receptionSpeed: 10);
            // 两边白班都只留一个人（Skeleton），唯一差别是有没有人值夜
            foreach (var sim in new[] { noNight, withNight })
                sim.Shifts.SetTier(StaffRole.Reception, ShiftTier.Skeleton);
            withNight.Shifts.SetNightCount(StaffRole.Reception, 1);

            foreach (var sim in new[] { noNight, withNight })
            {
                sim.BeginDay();
                sim.RunToEndOfDay();
                sim.SettleDay();
            }

            Assert.That(withNight.NightCheckInsToday, Is.GreaterThan(0), "夜班要真的接到人");
            Assert.That(withNight.NightLockedOutToday, Is.LessThan(noNight.NightLockedOutToday),
                        "排了夜班，吃闭门羹的人就更少");
            Assert.That(withNight.Rooms.CountOf(RoomSimState.Occupied),
                        Is.GreaterThan(noNight.Rooms.CountOf(RoomSimState.Occupied)),
                        "夜里接进来的客人真的住进了房间——这才是夜班挣回来的钱");
        }

        [Test]
        public void NightDeskCapacity_IsSmallerThanADaysArrivals()
        {
            // 夜班不能是万能补丁：要是一夜能办完一天的量，白班人手不足就再也不疼了
            Assert.That(NightDeskModel.CapacityFor(1),
                        Is.LessThan(SimMath.RoundToInt(ServiceCapacityModel.BaseCheckInsPerHour * 14f)),
                        "一夜的产能必须远小于白班一整天");
            Assert.That(NightDeskModel.CapacityFor(0), Is.EqualTo(0), "没人值夜就是零");
        }

        [Test]
        public void NightDeskShowsUpInTheReputationBreakdown()
        {
            // 晨报要能回答"今天为什么掉分"——夜里锁门是个玩家能改的原因
            var sim = BuildHotel(rooms: 40, receptionists: 1, receptionSpeed: 10);
            sim.BeginDay();
            sim.RunToEndOfDay();
            Assert.That(sim.DeskQueueLength, Is.GreaterThan(0));

            sim.SettleDay();

            Assert.That(sim.Breakdown.CountOf(ReputationCause.NightDeskClosed), Is.GreaterThan(0),
                        "闭门羹要出现在明细里，不能只是星级悄悄掉");
        }
    }
}
