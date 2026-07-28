using System.Collections.Generic;
using NUnit.Framework;

// 破败房清理（用户设计）：只花人工、要一次次进屋、有进度条、清完沿用破家具。
// 断言锁的是**关系与不变量**（几趟能清完、半途而废白干、清完进正常循环），
// 不锁具体数字——工作量和每趟成果都是调参空间。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class JunkClearingTest
    {
        private static HotelSim BuildHotel(int openRooms = 4, int derelictRooms = 4)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < openRooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));
            for (int i = 0; i < derelictRooms; i++)
                defs.Add(new RoomDefinition(301 + i, floor: 2, zone: 2, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ruined));

            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            staff.Register(new StaffMember(StaffRole.Housekeeper, "H", 60, new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, 20000, 1234);
            sim.FurnishInheritedRooms();
            return sim;
        }

        // ── 只花人工：现金归零也走得通 ────────────────────────────────────────

        [Test]
        public void ClearingCostsNoCashAndNoMaterials_SoBrokePlayersAreNeverLocked()
        {
            var sim = BuildHotel();
            sim.TrySpendCash(sim.Cash);          // 榨干现金
            Assert.That(sim.Cash, Is.EqualTo(0));
            int materialsBefore = sim.Materials.Stock;

            Assert.That(sim.TryStartJunkClearing(301, out string reason), Is.True, reason);

            Assert.That(sim.Cash, Is.EqualTo(0), "清垃圾不要钱");
            Assert.That(sim.Materials.Stock, Is.EqualTo(materialsBefore), "也不要材料");
        }

        [Test]
        public void OnlyDerelictRooms_NeedClearingOut()
        {
            var sim = BuildHotel();

            Assert.That(sim.TryStartJunkClearing(201, out string reason), Is.False, "好房不用清垃圾");
            Assert.That(reason, Is.Not.Empty);
            Assert.That(sim.TryStartJunkClearing(999, out _), Is.False, "不存在的房安全失败");
        }

        [Test]
        public void CannotOrderTheSameRoomTwice()
        {
            var sim = BuildHotel();
            Assert.That(sim.TryStartJunkClearing(301, out _), Is.True);
            Assert.That(sim.TryStartJunkClearing(301, out string reason), Is.False, "重复下单");
            Assert.That(reason, Is.Not.Empty);
        }

        // ── 一次次进屋：进度真的在动 ──────────────────────────────────────────

        [Test]
        public void TakesSeveralVisits_AndProgressClimbsEachTime()
        {
            // 玩家要的就是"工作人员一次次进屋，进度条一格格涨"
            var sim = BuildHotel();
            sim.TryStartJunkClearing(301, out _);

            float previous = sim.Clearing.ProgressOf(301);
            Assert.That(previous, Is.EqualTo(0f), "刚下单还没人去过");

            int visits = 0;
            bool done = false;
            while (!done && visits < 20)
            {
                done = sim.ApplyJunkClearingVisit(301, JunkClearingModel.WorkUnitsPerVisit);
                visits++;
                if (!done)
                {
                    Assert.That(sim.Clearing.ProgressOf(301), Is.GreaterThan(previous),
                                "第 " + visits + " 趟之后进度必须比上一趟高");
                    previous = sim.Clearing.ProgressOf(301);
                }
            }

            Assert.That(done, Is.True, "总得清完");
            Assert.That(visits, Is.GreaterThan(1), "一趟就搞定的话'一次次进屋'就没意义了");
            Assert.That(visits, Is.EqualTo(JunkClearingModel.VisitsForOneRoom));
        }

        [Test]
        public void VisitsRemaining_CountsDownForTheProgressLabel()
        {
            var sim = BuildHotel();
            sim.TryStartJunkClearing(301, out _);

            int before = JunkClearingModel.VisitsRemaining(sim.Clearing.WorkDoneOn(301));
            sim.ApplyJunkClearingVisit(301, JunkClearingModel.WorkUnitsPerVisit);
            int after = JunkClearingModel.VisitsRemaining(sim.Clearing.WorkDoneOn(301));

            Assert.That(after, Is.LessThan(before), "UI 要能说'还差几趟'");
        }

        [Test]
        public void WorkOnARoomNobodyOrdered_IsIgnored()
        {
            // 不能凭空给没下单的房记工——否则派工出 bug 会让破败房自己变好
            var sim = BuildHotel();

            Assert.That(sim.ApplyJunkClearingVisit(302, 999), Is.False);
            Assert.That(sim.Rooms.At(302).state, Is.EqualTo(RoomSimState.Ruined), "房态不许被动到");
        }

        // ── 清完之后：进正常循环，沿用破家具 ──────────────────────────────────

        [Test]
        public void ClearedRoom_BecomesADirtyRoomWithTheOldFurniture()
        {
            var sim = BuildHotel();
            sim.TryStartJunkClearing(301, out _);
            Assert.That(sim.Furniture.InRoom(301), Is.Empty, "破败房本来没家具入账");

            while (!sim.ApplyJunkClearingVisit(301, JunkClearingModel.WorkUnitsPerVisit)) { }

            Assert.That(sim.Rooms.At(301).state, Is.EqualTo(RoomSimState.Dirty),
                        "清完垃圾还是脏房——施工完总得打扫，这一步照常占客房部工时");
            Assert.That(sim.Furniture.InRoom(301), Is.Not.Empty, "沿用原有的破家具");
            Assert.That(sim.Rooms.At(301).tier, Is.EqualTo(RoomTier.Old),
                        "交付垫底就只配挂 Old——想提档得掏钱换家具");
            Assert.That(sim.Furniture.DeliveredQuality(301), Is.LessThan(0.3f),
                        "白捡的房不该有好交付，否则花钱装修就成了傻事");
        }

        [Test]
        public void ClearedRoom_EventuallyBecomesSellable_ClosingTheLoop()
        {
            // 整条路走通：破败 → 清理 → 脏房 → 客房部打扫 → 可售。
            // **盯这一间房自己**，不看 SellableCount 总量——总量会被入住占用扰动
            // （聚合量陷阱：清出来一间的同时可能有三间被住进去，总数反而降）。
            var sim = BuildHotel();
            sim.TryStartJunkClearing(301, out _);
            while (!sim.ApplyJunkClearingVisit(301, JunkClearingModel.WorkUnitsPerVisit)) { }
            Assert.That(sim.Rooms.At(301).state, Is.EqualTo(RoomSimState.Dirty));

            sim.Pipeline.ServiceEnabled = true;   // 这条测 Sim 自己跑清洁
            bool becameSellable = false;
            for (int day = 1; day <= 4 && !becameSellable; day++)
            {
                sim.BeginDay();
                sim.RunToEndOfDay();
                // 一路上只要摸到 Ready 就算通了（之后可能立刻被客人住进去）
                becameSellable = sim.Rooms.At(301).state == RoomSimState.Ready
                                 || sim.Rooms.At(301).state == RoomSimState.Occupied;
                sim.SettleDay();
                sim.Clock.BeginNextDay();
            }

            Assert.That(becameSellable, Is.True,
                        "清出来的房最终要能卖（或者已经住上人了）——否则这套操作对玩家没有回报");
        }

        [Test]
        public void ClearedRoom_DropsTheDerelictCount()
        {
            var sim = BuildHotel(derelictRooms: 4);
            int derelictBefore = sim.Rooms.CountOf(RoomSimState.Ruined);

            sim.TryStartJunkClearing(301, out _);
            while (!sim.ApplyJunkClearingVisit(301, JunkClearingModel.WorkUnitsPerVisit)) { }

            Assert.That(sim.Rooms.CountOf(RoomSimState.Ruined), Is.EqualTo(derelictBefore - 1),
                        "顶栏的'破败'数字要真的减一——玩家靠它确认操作有效");
        }

        // ── 放弃与排序 ────────────────────────────────────────────────────────

        [Test]
        public void GivingUpHalfway_ThrowsAwayTheProgress()
        {
            // 半途而废就是白干（否则玩家会反复开工关工来卡进度）
            var sim = BuildHotel();
            sim.TryStartJunkClearing(301, out _);
            sim.ApplyJunkClearingVisit(301, JunkClearingModel.WorkUnitsPerVisit);
            Assert.That(sim.Clearing.ProgressOf(301), Is.GreaterThan(0f));

            Assert.That(sim.CancelJunkClearing(301), Is.True);

            Assert.That(sim.Clearing.IsClearing(301), Is.False);
            Assert.That(sim.Clearing.ProgressOf(301), Is.EqualTo(0f), "进度归零");
            Assert.That(sim.Rooms.At(301).state, Is.EqualTo(RoomSimState.Ruined), "房间还是破败");
        }

        [Test]
        public void WorkersFinishTheClosestRoomFirst_NotAllTenAtOnce()
        {
            // 同时清十间半成品，在玩家眼里等于"什么都没完成"
            var sim = BuildHotel();
            sim.TryStartJunkClearing(301, out _);
            sim.TryStartJunkClearing(302, out _);
            sim.TryStartJunkClearing(303, out _);
            sim.ApplyJunkClearingVisit(302, JunkClearingModel.WorkUnitsPerVisit);

            var order = sim.RoomsBeingCleared();

            Assert.That(order[0], Is.EqualTo(302), "进度最高的排最前");
            Assert.That(order.Count, Is.EqualTo(3));
        }

        [Test]
        public void RoomsNeedingClearing_HidesTheOnesAlreadyUnderway()
        {
            var sim = BuildHotel(derelictRooms: 3);
            Assert.That(sim.RoomsNeedingClearing(10).Count, Is.EqualTo(3));

            sim.TryStartJunkClearing(301, out _);

            var waiting = sim.RoomsNeedingClearing(10);
            Assert.That(waiting, Has.No.Member(301), "已经在清的不该再出现在待指派清单里");
            Assert.That(waiting.Count, Is.EqualTo(2));
        }

        // ── 存档接口（世界场景接存档要到 M-F，先锁住往返） ─────────────────────

        [Test]
        public void ProgressSurvivesARoundTrip()
        {
            var queue = new JunkClearingQueue();
            queue.Begin(301);
            queue.Begin(302);
            queue.ApplyVisit(301, JunkClearingModel.WorkUnitsPerVisit);

            var restored = new JunkClearingQueue();
            restored.Restore(queue.Export());

            Assert.That(restored.WorkDoneOn(301), Is.EqualTo(queue.WorkDoneOn(301)));
            Assert.That(restored.IsClearing(302), Is.True);
            Assert.That(restored.Count, Is.EqualTo(2));
        }
    }
}
