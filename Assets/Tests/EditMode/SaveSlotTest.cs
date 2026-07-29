using System.Collections.Generic;
using NUnit.Framework;

// 三个存档槽位 + Sim 段真的进存档（玩家要求）。
//
// 背景：`SimState` 结构完整、`HotelSim.CaptureTo` 也写好了，但**从来没有人调用它**。
// 于是家具、预订簿、在途施工单、仓库、工资账、信用……读档一律回到出厂状态。
// "玩家自己建模"这个愿景的前提就是它——改完墙一读档全没，功能等于假的。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class SaveSlotTest
    {
        private static HotelSim BuildHotel(int rooms = 6, int startingCash = 5000, int seed = 20260729)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            staff.Register(new StaffMember(StaffRole.Housekeeper, "H", 60, new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, startingCash, seed);
            sim.FurnishInheritedRooms();
            return sim;
        }

        // ── 槽位号 ────────────────────────────────────────────────────────────

        [Test]
        public void ThereAreExactlyThreeSlots()
        {
            Assert.That(SaveSlots.Count, Is.EqualTo(3), "玩家要求三个槽位");
            Assert.That(SaveSlots.IsValid(1), Is.True);
            Assert.That(SaveSlots.IsValid(3), Is.True);
            Assert.That(SaveSlots.IsValid(0), Is.False, "0 号是老的单槽文件，不算槽位");
            Assert.That(SaveSlots.IsValid(4), Is.False);
        }

        [Test]
        public void EachSlotHasItsOwnFile()
        {
            // 三个槽位写同一个文件的话，"存到 2 号"会悄悄踩掉 1 号
            var names = new HashSet<string>();
            for (int slot = 1; slot <= SaveSlots.Count; slot++)
                Assert.That(names.Add(SaveSlots.FileNameOf(slot)), Is.True,
                            "槽位 " + slot + " 和别的槽位共用了文件名");
            Assert.That(names, Has.No.Member("slot0.json"), "不能覆盖老的单槽存档");
        }

        [Test]
        public void InvalidSlotNumbersAreIgnoredInsteadOfCorruptingTheActiveOne()
        {
            int before = SaveSlots.ActiveSlot;
            SaveSlots.SetActiveSlot(99);
            Assert.That(SaveSlots.ActiveSlot, Is.EqualTo(before), "非法槽位号不该改变当前槽位");
        }

        // ── 待读取意向：跨场景重载活下来 ──────────────────────────────────────

        [Test]
        public void ARequestedLoadSurvivesUntilSomebodyConsumesIt()
        {
            // 读档要走场景重载（第 12 天读第 3 天的档，逐个系统改回去不可能改干净），
            // 所以意向必须活过重载，且**只被消费一次**
            SaveSlots.ResetForNewGame();
            SaveSlots.RequestLoad(2);

            Assert.That(SaveSlots.PendingLoad, Is.EqualTo(2));
            Assert.That(SaveSlots.ConsumePendingLoad(), Is.EqualTo(2));
            Assert.That(SaveSlots.ConsumePendingLoad(), Is.EqualTo(-1), "消费过就没了");
        }

        [Test]
        public void TheSimRestoreIsQueuedAndConsumedExactlyOnce()
        {
            // 这是解开加载顺序死结的那一步：SaveCoordinator 在 Start 挂起，
            // 桥在首帧 Update 建完账之后取用（那时 Sim 才存在）
            SaveSlots.ResetForNewGame();
            var state = new SimState { day = 7, cash = 1234 };
            SaveSlots.QueueSimRestore(state);

            Assert.That(SaveSlots.PendingSimRestore, Is.Not.Null);
            Assert.That(SaveSlots.ConsumeSimRestore().day, Is.EqualTo(7));
            Assert.That(SaveSlots.ConsumeSimRestore(), Is.Null, "只该被取用一次");
        }

        [Test]
        public void NewGameClearsAnyPendingIntent()
        {
            // 不清的话上一局的意向会渗进新档（同 ManagerReputation.ResetForNewGame）
            SaveSlots.RequestLoad(3);
            SaveSlots.QueueSimRestore(new SimState());

            SaveSlots.ResetForNewGame();

            Assert.That(SaveSlots.PendingLoad, Is.EqualTo(-1));
            Assert.That(SaveSlots.PendingSimRestore, Is.Null);
        }

        // ── 摘要：三个槽长得一样玩家就没法选 ──────────────────────────────────

        [Test]
        public void ASlotSummaryCarriesEnoughToTellTwoGamesApart()
        {
            var state = new GameState();
            state.sim.day = 12;
            state.sim.cash = 3400;
            state.economy.reputationSamples.Add(0.8f);
            state.economy.reputationSamples.Add(0.6f);
            state.sim.roomStates.Add(new RoomStateEntry());

            var summary = SaveSlotSummary.From(2, state);

            Assert.That(summary.exists, Is.True);
            Assert.That(summary.slot, Is.EqualTo(2));
            Assert.That(summary.day, Is.EqualTo(12));
            Assert.That(summary.cash, Is.EqualTo(3400));
            // 星级走 ReputationLedger 的公式：avg 0.5→1★、1.0→3★、1.5→5★。
            // 平均 0.7 ⇒ 1 + (0.7-0.5)*4 = 1.8 星。
            // （第一版写成"平均×5"，实测摘要出现过 6.25 星这种不可能的数字）
            Assert.That(summary.stars, Is.EqualTo(1.8f).Within(1e-3f));
        }

        [Test]
        public void AnEmptyOrUnreadableSlotIsJustEmpty_NotAnException()
        {
            // 一个坏文件不能把整个存档界面炸掉——玩家至少还能存到别的槽自救
            Assert.That(SaveSlotSummary.From(1, null).exists, Is.False);
            Assert.That(SaveSlotSummary.Empty(3).slot, Is.EqualTo(3));
            Assert.That(SaveSlotSummary.Empty(3).exists, Is.False);
        }

        [Test]
        public void ASummaryFallsBackToTheV1EconomyWhenThereIsNoSimSection()
        {
            // 老档（v1/v2 只有 economy）在界面上也要显示得出天数和现金
            var legacy = new GameState();
            legacy.sim.day = 0;
            legacy.sim.cash = 0;
            legacy.progress.day = 5;
            legacy.economy.cash = 900;

            var summary = SaveSlotSummary.From(1, legacy);

            Assert.That(summary.day, Is.EqualTo(5));
            Assert.That(summary.cash, Is.EqualTo(900));
        }

        // ── Sim 段真的往返：这是整块的意义所在 ────────────────────────────────

        [Test]
        public void TheSimSectionSurvivesACaptureRestoreRoundTrip()
        {
            var sim = BuildHotel();
            sim.Warehouse.SetCapacity(40);
            sim.TryBuyMaterials(7);
            sim.BeginDay();
            sim.RunToEndOfDay();
            sim.SettleDay();

            var state = new GameState();
            sim.CaptureTo(state.sim);

            var restored = BuildHotel();
            restored.RestoreFrom(state.sim);

            Assert.That(restored.Clock.CurrentDay, Is.EqualTo(sim.Clock.CurrentDay), "天数");
            Assert.That(restored.Cash, Is.EqualTo(sim.Cash), "现金");
            Assert.That(restored.Safebox.Balance, Is.EqualTo(sim.Safebox.Balance), "保险箱");
            Assert.That(restored.Materials.Stock, Is.EqualTo(sim.Materials.Stock), "材料库存");
            Assert.That(restored.Furniture.Count, Is.EqualTo(sim.Furniture.Count), "家具件数");
        }

        [Test]
        public void FurnitureConditionSurvives_SoRepairsAndWearAreNotUndoneByLoading()
        {
            // 家具的崭新度/健康度是整套"修一张破床只能修成破床"的载体。
            // 读档把它们抹回出厂值，那套设计就白做了。
            var sim = BuildHotel();
            var bed = sim.Furniture.InRoom(201)[0];
            bed.newness = 0.11f;
            bed.health = 0.09f;
            bed.faultLineIndex = 1;
            bed.patchedUp = true;

            var state = new GameState();
            sim.CaptureTo(state.sim);

            var restored = BuildHotel();
            restored.RestoreFrom(state.sim);
            var sameBed = restored.Furniture.InRoom(201)[0];

            Assert.That(sameBed.newness, Is.EqualTo(0.11f).Within(1e-3f), "崭新度");
            Assert.That(sameBed.health, Is.EqualTo(0.09f).Within(1e-3f), "健康度");
            Assert.That(sameBed.IsFaulted, Is.True, "故障状态");
        }

        [Test]
        public void BuildJobsSurvive_SoPaidForWorkDoesNotEvaporate()
        {
            // 复原工期最长 7 天。不存等于直接吞钱（v7 修过的既有缺陷，这里守住它）
            var sim = BuildHotel(rooms: 8);
            sim.Warehouse.SetCapacity(80);
            sim.TryBuyMaterials(40);
            sim.Rooms.SetState(207, RoomSimState.Ruined);
            var rooms = new List<int> { 207 };
            Assert.That(sim.TryStartReclaim(ReclaimPlanKind.PatchUp, rooms, out string why), Is.True, why);
            int jobsBefore = sim.Renovations.RoomsUnderRenovation;
            Assert.That(jobsBefore, Is.GreaterThan(0));

            var state = new GameState();
            sim.CaptureTo(state.sim);
            var restored = BuildHotel(rooms: 8);
            restored.RestoreFrom(state.sim);

            Assert.That(restored.Renovations.RoomsUnderRenovation, Is.EqualTo(jobsBefore),
                        "在途施工单必须活过读档——花了钱和材料的东西不能凭空消失");
        }

        [Test]
        public void StarRatingSurvives_SoTwentyDaysOfGoodwillIsNotThrownAway()
        {
            // 把存档真接上之后暴露的第一个缺口：SimState 里根本没有声誉字段，
            // 世界场景的星级走 Sim 的 ReputationLedger，于是每次读档口碑归零。
            var sim = BuildHotel();
            for (int i = 0; i < 20; i++) sim.Reputation.RecordGuest(1.2f);
            float starsBefore = sim.Reputation.Stars;
            Assert.That(starsBefore, Is.GreaterThan(0f));

            var state = new GameState();
            sim.CaptureTo(state.sim);
            var restored = BuildHotel();
            restored.RestoreFrom(state.sim);

            Assert.That(restored.Reputation.Stars, Is.EqualTo(starsBefore).Within(1e-3f), "星级");
            // 摘要和内核必须用同一条公式，否则存档界面和顶栏会显示两个不同的星级
            var state2 = new GameState();
            sim.CaptureTo(state2.sim);
            Assert.That(SaveSlotSummary.From(1, state2).stars,
                        Is.EqualTo(starsBefore).Within(1e-3f),
                        "存档摘要的星级要和顶栏一致");
            Assert.That(restored.Reputation.SampleCount, Is.EqualTo(20), "样本窗口");
        }

        [Test]
        public void WarehousePayrollAndCreditAllSurvive()
        {
            // 这三套账是本次会话新加的，不存就同样读档归零
            var sim = BuildHotel();
            sim.Warehouse.SetCapacity(40);
            sim.Payroll.Accrue(260);
            sim.Payroll.Cycle = PayrollCycle.Weekly;
            sim.Credit.CloseDay(paymentWasDue: true);      // 逾期一次：信用 80

            var state = new GameState();
            sim.CaptureTo(state.sim);
            var restored = BuildHotel();
            restored.RestoreFrom(state.sim);

            Assert.That(restored.Warehouse.Capacity, Is.EqualTo(40), "仓库容量");
            Assert.That(restored.Payroll.Owed, Is.EqualTo(260), "未付工资");
            Assert.That(restored.Payroll.Cycle, Is.EqualTo(PayrollCycle.Weekly), "支付周期");
            Assert.That(restored.Credit.Score, Is.EqualTo(sim.Credit.Score), "信用分");
        }

        [Test]
        public void JunkClearingProgressSurvives_SoTripsAlreadyWalkedAreNotWasted()
        {
            // 清理一间破败房要跑四趟。读档退回 0 趟等于白干，玩家会以为按钮没生效
            var sim = BuildHotel();
            sim.Rooms.SetState(203, RoomSimState.Ruined);
            Assert.That(sim.TryStartJunkClearing(203, out string why), Is.True, why);
            sim.ApplyJunkClearingVisit(203, JunkClearingModel.WorkUnitsPerVisit);
            int workBefore = sim.Clearing.WorkDoneOn(203);
            Assert.That(workBefore, Is.GreaterThan(0));

            var state = new GameState();
            sim.CaptureTo(state.sim);
            var restored = BuildHotel();
            restored.Rooms.SetState(203, RoomSimState.Ruined);
            restored.RestoreFrom(state.sim);

            Assert.That(restored.Clearing.IsClearing(203), Is.True, "还在清理队列里");
            Assert.That(restored.Clearing.WorkDoneOn(203), Is.EqualTo(workBefore), "已经跑过的趟数");
        }

        [Test]
        public void AnOldSaveWithoutACreditScoreIsNotBlacklistedOnLoad()
        {
            // 迁移陷阱：JsonUtility 给缺失的 int 填 0，而 0 分 = 已拉黑。
            // 老玩家一读档就借不到钱，还完全不知道为什么。
            var legacy = new GameState { version = 7 };
            legacy.sim.creditScore = 0;
            legacy.MigrateToCurrentVersion();

            Assert.That(legacy.sim.creditScore, Is.EqualTo(CreditPolicy.StartingScore),
                        "旧档补满分，不是补 0");
        }

        [TearDown]
        public void ClearStaticIntentBetweenTests()
        {
            SaveSlots.ResetForNewGame();
        }
    }
}
