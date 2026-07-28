using System.Collections.Generic;
using NUnit.Framework;

// 解雇的人必须真的从总账里消失。
//
// 玩家实测的 bug：把前台开了，客人照常入住。模型本身没错
// （ServiceCapacityModel 在前台无人时返回 0），错在世界场景的桥只把"雇了的人"
// 同步进 Sim、从不摘掉"解雇的人"——名册里人没了，总账里他还在上班。
// 一般化：**镜像同步必须处理"消失"**，只处理"出现"的同步迟早对不上账。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class StaffFiringTest
    {
        private static HotelSim BuildHotel(out int receptionStaffId, int rooms = 8)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            receptionStaffId = staff.Register(
                new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            staff.StartShift(receptionStaffId);
            int hsk = staff.Register(
                new StaffMember(StaffRole.Housekeeper, "H", 60, new StaffAttributes(55, 55, 55), 1, null));
            staff.StartShift(hsk);

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, 4242);
            sim.FurnishInheritedRooms();
            return sim;
        }

        [Test]
        public void FiringTheOnlyReceptionist_StopsCheckIns()
        {
            var sim = BuildHotel(out int receptionId);
            sim.BeginDay();

            // 先跑到入住高峰中段，确认前台在的时候人是进得来的
            sim.Clock.FastForwardTo(17 * 60);
            while (sim.Clock.TryConsumeTick()) sim.StepMinute();
            int checkedInWhileStaffed = sim.ArrivalsCheckedInToday;
            Assert.That(checkedInWhileStaffed, Is.GreaterThan(0), "有前台时客人该办得进来");

            // 开了他——总账里也必须消失（桥现在就是这么做的）
            sim.Staff.EndShift(receptionId);
            Assert.That(sim.Staff.Remove(receptionId), Is.True);

            sim.Clock.FastForwardTo(SimClock.DayEndMinute);
            while (sim.Clock.TryConsumeTick()) sim.StepMinute();

            Assert.That(sim.ArrivalsCheckedInToday, Is.EqualTo(checkedInWhileStaffed),
                        "前台没了就一个也办不进来——队伍只会越排越长");
            Assert.That(ServiceCapacityModel.CheckInsPerHour(sim.Staff), Is.EqualTo(0f),
                        "办入住能力必须归零");
        }

        [Test]
        public void FiringOneOfTwoReceptionists_HalvesTheDeskInsteadOfClosingIt()
        {
            // 全开了才是零；开一个只是变慢——玩家要能感觉到"人手不足"和"没人"的区别
            var staff = new StaffRoster();
            int a = staff.Register(new StaffMember(StaffRole.Reception, "A", 65, new StaffAttributes(55, 55, 55), 1, null));
            int b = staff.Register(new StaffMember(StaffRole.Reception, "B", 65, new StaffAttributes(55, 55, 55), 1, null));
            // StartShift 只到 Available，而**只有 Working 算产出**（摸鱼/待命/赶路都不算）
            staff.SetState(a, StaffOperationalState.Working);
            staff.SetState(b, StaffOperationalState.Working);

            float both = ServiceCapacityModel.CheckInsPerHour(staff);
            staff.EndShift(b);
            staff.Remove(b);
            float one = ServiceCapacityModel.CheckInsPerHour(staff);

            Assert.That(both, Is.GreaterThan(0f));
            Assert.That(one, Is.EqualTo(both / 2f).Within(0.01f), "两个人的一半");
            Assert.That(one, Is.GreaterThan(0f), "还有人就不是关门");
        }

        [Test]
        public void RemovedStaff_StopsCountingAsOnDuty()
        {
            // 摘掉之后所有"在班/在产出"的统计都不能再看见他，
            // 否则晨报会显示"客房部 1 人"而实际上没人干活
            var sim = BuildHotel(out int receptionId);

            Assert.That(sim.Staff.OnDutyCountOfRole(StaffRole.Reception), Is.EqualTo(1));

            sim.Staff.EndShift(receptionId);
            sim.Staff.Remove(receptionId);

            Assert.That(sim.Staff.OnDutyCountOfRole(StaffRole.Reception), Is.EqualTo(0));
            Assert.That(sim.Staff.ProductiveCountOfRole(StaffRole.Reception), Is.EqualTo(0));
            Assert.That(sim.Staff.TryGet(receptionId, out _), Is.False, "id 也该查不到了");
        }

        [Test]
        public void FiringAHousekeeper_ReleasesTheRoomTheyWereCleaning()
        {
            // 人走了房间不能永远停在 Cleaning：没人干活也没人接手，
            // 那间房就此退出可售循环（同一类"消失没人管"的 bug）
            var sim = BuildHotel(out _);
            sim.Pipeline.ServiceEnabled = true;   // 这条测清洁池，需要 Sim 自己跑清洁

            sim.Rooms.SetState(201, RoomSimState.Dirty);
            sim.BeginDay();
            sim.StepMinute();
            Assert.That(sim.Rooms.CountOf(RoomSimState.Cleaning), Is.GreaterThan(0),
                        "得先有人开始打扫");   // 前提用 Assert：Assume 失败会静默跳过

            foreach (var entry in new List<StaffSimEntry>(sim.Staff.Entries))
                if (entry.member != null && entry.member.Role == StaffRole.Housekeeper)
                {
                    sim.Staff.EndShift(entry.staffId);
                    sim.Staff.Remove(entry.staffId);
                }

            sim.StepMinute();   // 下一分钟就该把房退回脏房池

            Assert.That(sim.Rooms.CountOf(RoomSimState.Cleaning), Is.EqualTo(0),
                        "没人了就不该有房还挂在'正在清洁'上");
            Assert.That(sim.Rooms.CountOf(RoomSimState.Dirty), Is.GreaterThan(0),
                        "房间退回脏房池等新人来打扫");
        }
    }
}
