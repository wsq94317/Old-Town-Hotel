using System.Collections.Generic;
using NUnit.Framework;

// tick 管线：确定性（倍速/分批不改业务结果）、清洁链路、质量闸门、多日不崩。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class SimPipelineTest
    {
        private const int Seed = 4242;

        private sealed class Rig
        {
            public SimClock clock;
            public RoomLedger rooms;
            public StaffRoster staff;
            public SimPipeline pipeline;
        }

        private static Rig BuildRig(int housekeepers = 1, int inspectors = 0, int dirtyRooms = 8)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < 12; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old,
                                            i < dirtyRooms ? RoomSimState.Dirty : RoomSimState.Ready));

            var rig = new Rig
            {
                clock = new SimClock(),
                rooms = new RoomLedger(defs),
                staff = new StaffRoster(),
            };

            for (int i = 0; i < housekeepers; i++)
            {
                int id = rig.staff.Register(new StaffMember(StaffRole.Housekeeper, "HSK" + i, 60,
                                            new StaffAttributes(50, 50, 50), 1, null));
                rig.staff.StartShift(id);
            }
            for (int i = 0; i < inspectors; i++)
            {
                int id = rig.staff.Register(new StaffMember(StaffRole.Inspector, "INSP" + i, 70,
                                            new StaffAttributes(50, 50, 50), 1, null));
                rig.staff.StartShift(id);
            }

            rig.pipeline = new SimPipeline(rig.clock, rig.rooms, rig.staff, Seed);
            return rig;
        }

        // 相对推进（FastForwardTo 收的是绝对分钟，写成绝对值会让重复调用只跑第一次）
        private static void RunTicks(Rig rig, int ticks)
        {
            rig.clock.FastForwardTo(rig.clock.CurrentMinute + ticks);
            while (rig.clock.TryConsumeTick()) rig.pipeline.StepMinute();
        }

        [Test]
        public void Cleaning_DrainsDirtyBacklogIntoSellableRooms()
        {
            var rig = BuildRig(housekeepers: 1, inspectors: 0, dirtyRooms: 8);
            Assert.That(rig.rooms.DirtyBacklog, Is.EqualTo(8));
            Assert.That(rig.rooms.SellableCount, Is.EqualTo(4));

            RunTicks(rig, 120); // 两小时：属性 50 的管家 ≈ 2 间/时

            Assert.That(rig.rooms.DirtyBacklog, Is.LessThan(8), "两小时应该清掉几间");
            Assert.That(rig.rooms.SellableCount, Is.GreaterThan(4));
            Assert.That(rig.rooms.DirtyBacklog + rig.rooms.SellableCount, Is.EqualTo(12),
                        "没有 Inspector 时清完直接可售，房间不会消失");
        }

        [Test]
        public void MoreHousekeepers_ClearBacklogFaster()
        {
            var lean = BuildRig(housekeepers: 1, dirtyRooms: 12);
            var staffed = BuildRig(housekeepers: 3, dirtyRooms: 12);

            RunTicks(lean, 180);
            RunTicks(staffed, 180);

            Assert.That(staffed.rooms.DirtyBacklog, Is.LessThan(lean.rooms.DirtyBacklog),
                        "排班精简 → 周转慢：压力网的第一根耦合边");
            Assert.That(staffed.rooms.SellableCount, Is.GreaterThan(lean.rooms.SellableCount),
                        "可售房少 → 入住率降 → 现金紧（链路下游在 M-B/M-C 接上）");
        }

        [Test]
        public void InspectorOnDuty_AddsQualityGateBeforeRoomsGoOnSale()
        {
            var withInspector = BuildRig(housekeepers: 2, inspectors: 1, dirtyRooms: 8);

            RunTicks(withInspector, 30); // 半小时：清洁已产出，但检查还没全消化

            Assert.That(withInspector.rooms.CountOf(RoomSimState.AwaitingInspection) +
                        withInspector.rooms.CountOf(RoomSimState.Ready),
                        Is.GreaterThan(4), "清洁确实在产出");

            RunTicks(withInspector, 300); // 再跑够久，检查队列会被清干
            Assert.That(withInspector.rooms.CountOf(RoomSimState.AwaitingInspection), Is.EqualTo(0));
            Assert.That(withInspector.rooms.SellableCount, Is.EqualTo(12));
        }

        [Test]
        public void NoInspector_SkipsTheGateEntirely()
        {
            var noInspector = BuildRig(housekeepers: 2, inspectors: 0, dirtyRooms: 8);

            RunTicks(noInspector, 60);

            Assert.That(noInspector.rooms.CountOf(RoomSimState.AwaitingInspection), Is.EqualTo(0),
                        "没有验房员：清完直接上架（快，但代价是瑕疵率——M-G 接满意度）");
        }

        [Test]
        public void Determinism_BatchSizeDoesNotChangeOutcome()
        {
            // 4x 加速与跳段本质就是"一帧里多跑几个 tick"——结果必须与 1x 逐帧一致
            var oneAtATime = BuildRig(housekeepers: 2, inspectors: 1, dirtyRooms: 10);
            var oneBigBatch = BuildRig(housekeepers: 2, inspectors: 1, dirtyRooms: 10);

            for (int i = 0; i < 300; i++) RunTicks(oneAtATime, 1);
            RunTicks(oneBigBatch, 300);

            Assert.That(oneAtATime.pipeline.TicksRun, Is.EqualTo(oneBigBatch.pipeline.TicksRun));
            for (int i = 0; i < oneAtATime.rooms.Count; i++)
            {
                Assert.That(oneAtATime.rooms.Peek(i).state, Is.EqualTo(oneBigBatch.rooms.Peek(i).state),
                            "逐 tick 与批量 tick 的房态必须逐间一致");
            }
            Assert.That(oneAtATime.rooms.SellableCount, Is.EqualTo(oneBigBatch.rooms.SellableCount));
        }

        [Test]
        public void Determinism_SameSeedReproducesSlackingExactly()
        {
            var a = BuildRig(housekeepers: 3, dirtyRooms: 12);
            var b = BuildRig(housekeepers: 3, dirtyRooms: 12);

            RunTicks(a, 240);
            RunTicks(b, 240);

            var ea = a.staff.Entries;
            var eb = b.staff.Entries;
            for (int i = 0; i < ea.Count; i++)
            {
                Assert.That(ea[i].slackMinutesToday, Is.EqualTo(eb[i].slackMinutesToday),
                            "同种子摸鱼分钟逐人一致（可复现才能调平衡）");
                Assert.That(ea[i].workedMinutesToday, Is.EqualTo(eb[i].workedMinutesToday));
            }
        }

        [Test]
        public void ManagerOnFloor_SuppressesSlacking()
        {
            var watched = BuildRig(housekeepers: 3, dirtyRooms: 12);
            var unwatched = BuildRig(housekeepers: 3, dirtyRooms: 12);
            watched.pipeline.ManagerOnFloor = true;

            RunTicks(watched, 400);
            RunTicks(unwatched, 400);

            int watchedSlack = 0, unwatchedSlack = 0;
            foreach (var e in watched.staff.Entries) watchedSlack += e.slackMinutesToday;
            foreach (var e in unwatched.staff.Entries) unwatchedSlack += e.slackMinutesToday;

            Assert.That(watchedSlack, Is.EqualTo(0), "经理在场：一分钟也不敢摸");
            Assert.That(unwatchedSlack, Is.GreaterThan(0), "没人盯着就有人摸鱼（否则监督玩法没意义）");
        }

        [Test]
        public void LowSupply_HalvesThroughputAtWorst()
        {
            var stocked = BuildRig(housekeepers: 2, dirtyRooms: 12);
            var dry = BuildRig(housekeepers: 2, dirtyRooms: 12);
            dry.pipeline.SupplyFactor = ServiceCapacityModel.SupplyFactor(inventory: 0, dailyNeed: 10);

            RunTicks(stocked, 180);
            RunTicks(dry, 180);

            Assert.That(dry.rooms.DirtyBacklog, Is.GreaterThan(stocked.rooms.DirtyBacklog),
                        "缺货拖慢周转（补货 → 服务压力的另一条输入）");
            Assert.That(dry.rooms.SellableCount, Is.GreaterThan(0), "缺货不该让酒店彻底停摆");
        }

        [Test]
        public void TenDaysHeadless_RunsWithoutBlowingUp()
        {
            var rig = BuildRig(housekeepers: 2, inspectors: 1, dirtyRooms: 12);

            for (int day = 1; day <= 10; day++)
            {
                RunTicks(rig, SimClock.MinutesPerDay);
                Assert.That(rig.clock.DayEndReached, Is.True, "第 " + day + " 天应跑到打烊");

                rig.pipeline.SettleDay(wagesPaid: true);
                rig.clock.BeginNextDay();

                // 次日又脏一批房（模拟退房），检验管线能长期消化
                for (int i = 0; i < 6; i++) rig.rooms.SetState(201 + i, RoomSimState.Dirty);
            }

            Assert.That(rig.clock.CurrentDay, Is.EqualTo(11));
            Assert.That(rig.pipeline.TicksRun, Is.EqualTo(10L * SimClock.MinutesPerDay));
            Assert.That(rig.rooms.Count, Is.EqualTo(12), "十天下来房间没凭空增减");
        }

        [Test]
        public void SettleDay_ClearsPartialCleaningProgress()
        {
            var rig = BuildRig(housekeepers: 1, dirtyRooms: 12);
            RunTicks(rig, 20); // 攒了不足一间的清洁进度
            int backlogBefore = rig.rooms.DirtyBacklog;

            rig.pipeline.SettleDay(wagesPaid: true);
            rig.clock.BeginNextDay();
            RunTicks(rig, 1);

            Assert.That(rig.rooms.DirtyBacklog, Is.EqualTo(backlogBefore),
                        "跨日不该把昨天的半间进度兑成今天的一间房");
        }
    }
}
