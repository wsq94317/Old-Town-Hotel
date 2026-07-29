using System.Collections.Generic;
using NUnit.Framework;

// 仓库容量（用户要求"仓库会满"）。这一版只管材料——箱子系统被并行设计审计
// 打回了（只有入口没有出口 + 会把整层装修变成物理上不可能），详见 Warehouse.cs 的注释。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class WarehouseTest
    {
        private static HotelSim BuildHotel(int capacity, int startingCash = 20000)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < 8; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));

            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));

            var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                   DemandConfig.Default, startingCash, 909);
            sim.Warehouse.SetCapacity(capacity);
            return sim;
        }

        // ── 默认不设上限：容量是场景参数，不是内核常量 ────────────────────────

        [Test]
        public void ByDefaultThereIsNoLimit_SoOldScenariosAndTestsAreUntouched()
        {
            // 十几处既有测试买到 200 份材料。容量若是内核默认值，加这个功能
            // 就会静默改掉所有历史调参——所以默认不设限，由场景显式设定。
            var warehouse = new Warehouse();

            Assert.That(warehouse.HasLimit, Is.False);
            Assert.That(warehouse.UnitsThatFit(materialStock: 9999, wantedUnits: 200), Is.EqualTo(200));
            Assert.That(warehouse.IsFullAt(999999), Is.False);
        }

        [Test]
        public void BothPlayableScenesSetARealCapacity()
        {
            // 不设的话这个功能等于不存在。默认值本身要够大，不堵死玩家买得起的批量。
            Assert.That(Warehouse.DefaultCapacity, Is.GreaterThan(0));
            Assert.That(Warehouse.DefaultCapacity, Is.GreaterThanOrEqualTo(32),
                        "12 间房的店一次 FullFit 复原 4 间要 32 份材料，批量折扣不能被堵死");
        }

        // ── 会满，而且满了要说清楚 ────────────────────────────────────────────

        [Test]
        public void TheWarehouseFillsUpAndThenRefusesDeliveries()
        {
            var sim = BuildHotel(capacity: 10);

            Assert.That(sim.TryBuyMaterials(10), Is.True, "刚好装满");
            Assert.That(sim.WarehouseIsFull, Is.True);
            Assert.That(sim.TryBuyMaterials(1), Is.False, "满了就收不下");
        }

        [Test]
        public void ARefusedDeliveryCostsNothing()
        {
            // 装不下就**一份也不收、一分钱也不扣**。部分收货会让玩家付了 10 份的钱
            // 只拿到 3 份，那是最难被原谅的一种"惩罚"。
            var sim = BuildHotel(capacity: 5);
            int cashBefore = sim.Cash;

            Assert.That(sim.TryBuyMaterials(10), Is.False);

            Assert.That(sim.Cash, Is.EqualTo(cashBefore), "钱一分没动");
            Assert.That(sim.Materials.Stock, Is.EqualTo(0), "货一份没进");
        }

        [Test]
        public void TheUiCanAlwaysSayExactlyHowManyWouldFit()
        {
            // "仓库只剩 3 格"比干巴巴一句失败有用得多——玩家知道下一步该买几份
            var sim = BuildHotel(capacity: 10);
            sim.TryBuyMaterials(7);

            Assert.That(sim.MaterialsThatFit(10), Is.EqualTo(3));
            Assert.That(sim.WarehouseSpaceLeft, Is.EqualTo(3));
            Assert.That(sim.TryBuyMaterials(3), Is.True, "按它说的份数买就该成功");
        }

        [Test]
        public void WithoutALimitTheUiKnowsNotToShowAFraction()
        {
            var sim = BuildHotel(capacity: Warehouse.Unlimited);
            Assert.That(sim.WarehouseSpaceLeft, Is.EqualTo(-1), "不设上限时 UI 不该画分数");
        }

        // ── 消耗腾出空间：这是"出口"，箱子系统缺的就是它 ──────────────────────

        [Test]
        public void SpendingMaterialsFreesTheSpaceBackUp()
        {
            // 仓库必须有出口。材料的出口是装修消耗——所以材料版仓库不会
            // 变成"能花钱进去出不来"的死角（审计打回箱子方案的头号理由）。
            var sim = BuildHotel(capacity: 10);
            sim.TryBuyMaterials(10);
            Assert.That(sim.TryBuyMaterials(1), Is.False);

            Assert.That(sim.Materials.TryConsume(6), Is.True);

            Assert.That(sim.WarehouseSpaceLeft, Is.EqualTo(6));
            Assert.That(sim.TryBuyMaterials(6), Is.True, "腾出空间之后又能进货了");
        }

        [Test]
        public void GrantedMaterialsAreNotGatedByCapacity()
        {
            // Materials.Add 是"赠予/读档"的通道（开局送货、存档恢复、测试构造），
            // 仓库只卡**采购**（卸货口）。分清这两个通道，读档才不会把库存吃掉。
            var sim = BuildHotel(capacity: 5);
            sim.Materials.Add(50);

            Assert.That(sim.Materials.Stock, Is.EqualTo(50), "赠予不被容量卡");
            Assert.That(sim.WarehouseSpaceLeft, Is.EqualTo(0), "但确实是超载状态");
            Assert.That(sim.TryBuyMaterials(1), Is.False, "超载时买不进新的");
        }

        // ── 纯逻辑边界 ────────────────────────────────────────────────────────

        [Test]
        public void CapacityMathHandlesTheEdges()
        {
            var warehouse = new Warehouse(10);

            Assert.That(warehouse.UnitsThatFit(0, 0), Is.EqualTo(0));
            Assert.That(warehouse.UnitsThatFit(0, -5), Is.EqualTo(0), "负数不炸");
            Assert.That(warehouse.SpaceLeftWith(99), Is.EqualTo(0), "超载不返回负数");
            Assert.That(warehouse.UsedBy(-3), Is.EqualTo(0));

            warehouse.SetCapacity(-1);
            Assert.That(warehouse.HasLimit, Is.False, "负容量按不设限处理");
        }
    }
}
