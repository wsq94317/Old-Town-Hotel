using System.Collections.Generic;
using NUnit.Framework;

// 家具摆放锚点（玩家设计："预定义一些格子内的锚点，可以摆放在锚点上"）。
// 尺寸量自真实场景：房间 5×5、墙厚 0.2、门洞在 x∈[-0.9,0.9] ⇒ 净内空 4.8×4.8，
// 切成 3×3 个 1.6 的格子，门口那一格刻意留空 ⇒ 8 个锚点。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FurnitureAnchorTest
    {
        private static HotelSim BuildHotel(int rooms = 4)
        {
            var defs = new List<RoomDefinition>();
            for (int i = 0; i < rooms; i++)
                defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                            RoomTier.Old, RoomSimState.Ready));
            var staff = new StaffRoster();
            staff.Register(new StaffMember(StaffRole.Reception, "R", 65, new StaffAttributes(55, 55, 55), 1, null));
            return new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                                DemandConfig.Default, 20000, 606);
        }

        // ── 布局本身 ──────────────────────────────────────────────────────────

        [Test]
        public void EightAnchorsPerRoom_WithTheDoorwayCellLeftEmpty()
        {
            // 3×3 = 9 格，门口那格留空（客人一进门不该撞在柜子上）
            Assert.That(FurnitureAnchors.For(doorOnPositiveZ: false).Count,
                        Is.EqualTo(FurnitureAnchors.PerRoom));
            Assert.That(FurnitureAnchors.PerRoom, Is.EqualTo(8));
        }

        [Test]
        public void EveryAnchorSitsInsideTheRoom()
        {
            // 净内空 4.8×4.8，所以任何锚点的坐标绝对值不该超过 2.4——
            // 超了就是摆到墙里或走廊上
            foreach (bool mirrored in new[] { false, true })
                foreach (var anchor in FurnitureAnchors.For(mirrored))
                {
                    Assert.That(Mathf.Abs(anchor.localX), Is.LessThan(2.4f), anchor.kind.ToString());
                    Assert.That(Mathf.Abs(anchor.localZ), Is.LessThan(2.4f), anchor.kind.ToString());
                }
        }

        [Test]
        public void NoTwoAnchorsShareASpot()
        {
            var seen = new HashSet<string>();
            foreach (var anchor in FurnitureAnchors.For(doorOnPositiveZ: false))
                Assert.That(seen.Add(anchor.localX + "," + anchor.localZ), Is.True,
                            "两个锚点重合了：" + anchor.kind);
        }

        [Test]
        public void AnchorIdsAreStableAndStartAtOne()
        {
            // 0 留给"未指派"（旧档），而且存档存的是 id ⇒ **永不重排**
            var ids = new HashSet<int>();
            foreach (var anchor in FurnitureAnchors.For(doorOnPositiveZ: false))
            {
                Assert.That(anchor.anchorId, Is.GreaterThan(FurnitureAnchors.Unassigned));
                Assert.That(ids.Add(anchor.anchorId), Is.True, "id 重复：" + anchor.anchorId);
            }
        }

        // ── 镜像：南北两排房朝向相反 ──────────────────────────────────────────

        [Test]
        public void MirroringFlipsZ_ButKeepsTheAnchorIds()
        {
            // 翻 id 的话同一个存档在两排房之间会错位（家具跳到别的位置）
            var north = FurnitureAnchors.For(doorOnPositiveZ: false);
            var south = FurnitureAnchors.For(doorOnPositiveZ: true);

            Assert.That(south.Count, Is.EqualTo(north.Count));
            for (int i = 0; i < north.Count; i++)
            {
                Assert.That(south[i].anchorId, Is.EqualTo(north[i].anchorId), "id 必须一致");
                Assert.That(south[i].kind, Is.EqualTo(north[i].kind));
                Assert.That(south[i].localX, Is.EqualTo(north[i].localX).Within(1e-4f), "x 不翻");
                Assert.That(south[i].localZ, Is.EqualTo(-north[i].localZ).Within(1e-4f), "z 要翻");
            }
        }

        [Test]
        public void TheBedIsNeverAtTheDoorwayInEitherOrientation()
        {
            // 门口那一格必须空着。北排门在 -z、南排门在 +z——
            // 单一布局对三分之一的房间是错的（审计点名的坑）
            foreach (bool doorOnPositiveZ in new[] { false, true })
            {
                float doorZ = doorOnPositiveZ ? 1.6f : -1.6f;
                foreach (var anchor in FurnitureAnchors.For(doorOnPositiveZ))
                    if (anchor.kind == AnchorKind.BedNook)
                        Assert.That(anchor.localZ, Is.Not.EqualTo(doorZ).Within(1e-4f),
                                    "床摆在了门口那一格");
            }
        }

        // ── 收哪些家具：必备位必须永远有地方放 ────────────────────────────────

        [Test]
        public void EveryRequiredSlotHasSomewhereToGo()
        {
            // 没有这条，某间房会永远配不齐而不可售，玩家还找不出原因
            Assert.That(FurnitureAnchors.EveryRequiredSlotHasAnAnchor(), Is.True);
        }

        [Test]
        public void ARugCannotTakeTheOnlyBedSpot()
        {
            // 必备位有专属锚点：否则玩家能把地毯摆在唯一能放床的格子上，
            // 亲手把这间房锁死
            var bedAnchors = FurnitureAnchors.AnchorsAccepting(FurnitureSlot.Bed);
            var rugAnchors = FurnitureAnchors.AnchorsAccepting(FurnitureSlot.Floor);

            Assert.That(bedAnchors, Is.Not.Empty);
            foreach (int id in rugAnchors)
                Assert.That(bedAnchors, Has.No.Member(id), "地毯不能占床位");
        }

        // ── 接进账本：Place 是唯一创建点，所以所有路径自动正确 ────────────────

        [Test]
        public void EveryPieceGetsAnAnchorTheMomentItIsCreated()
        {
            // FurnitureLedger.Place() 是家具唯一的创建点。在那里自动指派，
            // 继承破家具/买新/复原/两档装修全部一行新逻辑都不用
            var sim = BuildHotel();
            sim.FurnishInheritedRooms();

            var items = sim.Furniture.InRoom(201);
            Assert.That(items, Is.Not.Empty);
            foreach (var item in items)
                Assert.That(item.anchorId, Is.Not.EqualTo(FurnitureAnchors.Unassigned),
                            FurnitureCatalog.Get(item.kindId).name + " 没拿到锚点");
        }

        [Test]
        public void TwoPiecesNeverEndUpOnTheSameAnchor()
        {
            var sim = BuildHotel();
            sim.FurnishInheritedRooms();
            sim.Furniture.FurnishWithNewRequired(202);
            sim.Furniture.ReplaceRoomWithTopTier(203);

            foreach (int room in new[] { 201, 202, 203 })
            {
                var used = new HashSet<int>();
                foreach (var item in sim.Furniture.InRoom(room))
                {
                    if (item.anchorId == FurnitureAnchors.Unassigned) continue;
                    Assert.That(used.Add(item.anchorId), Is.True,
                                "房 " + room + " 有两件家具占了同一个锚点 " + item.anchorId);
                }
            }
        }

        [Test]
        public void MovingAPieceToAnIllegalSpotIsRefusedWithAReason()
        {
            // 静默失败会让玩家以为拖拽坏了
            var sim = BuildHotel();
            sim.FurnishInheritedRooms();
            var bed = sim.Furniture.InRoom(201)[0];
            int rugSpot = FurnitureAnchors.AnchorsAccepting(FurnitureSlot.Floor)[0];

            Assert.That(sim.Furniture.TryMoveToAnchor(bed.instanceId, rugSpot, false, out string reason),
                        Is.False);
            Assert.That(reason, Is.Not.Empty, "要说明为什么放不了");
        }

        [Test]
        public void MovingAPieceOntoAnOccupiedSpotIsRefused()
        {
            var sim = BuildHotel();
            sim.Furniture.ReplaceRoomWithTopTier(201);
            var items = sim.Furniture.InRoom(201);
            Assert.That(items.Count, Is.GreaterThan(1));

            // 找两件同位置的家具是不可能的（一个位置一件），所以拿第二件的锚点
            // 去挤第一件——不同 slot，所以应当被"不收这种家具"拦下
            int occupied = items[1].anchorId;
            Assert.That(sim.Furniture.TryMoveToAnchor(items[0].instanceId, occupied, false, out string why),
                        Is.False, why);
        }

        [Test]
        public void AMoveToALegalFreeSpotSucceedsAndUpdatesTheCachedPosition()
        {
            var sim = BuildHotel();
            sim.Furniture.Place(201, FurnitureCatalog.WallArt);
            var art = sim.Furniture.InRoom(201)[0];
            var wallSpots = FurnitureAnchors.AnchorsAccepting(FurnitureSlot.Wall);
            Assert.That(wallSpots.Count, Is.GreaterThan(1), "挂画要有两个位置这条测试才有意义");

            int target = wallSpots[0] == art.anchorId ? wallSpots[1] : wallSpots[0];
            Assert.That(sim.Furniture.TryMoveToAnchor(art.instanceId, target, false, out string why),
                        Is.True, why);

            Assert.That(art.anchorId, Is.EqualTo(target));
            FurnitureAnchors.TryGet(target, false, out FurnitureAnchor anchor);
            Assert.That(art.posX, Is.EqualTo(anchor.localX).Within(1e-4f), "缓存坐标要跟着更新");
            Assert.That(art.posY, Is.EqualTo(anchor.localZ).Within(1e-4f));
        }

        // ── 存档：旧档补派不能抢走新档记着的位置 ──────────────────────────────

        [Test]
        public void AnchorsSurviveASaveRoundTrip()
        {
            var sim = BuildHotel();
            sim.Furniture.ReplaceRoomWithTopTier(201);
            var before = new Dictionary<int, int>();
            foreach (var item in sim.Furniture.InRoom(201)) before[item.instanceId] = item.anchorId;

            var state = new GameState();
            sim.CaptureTo(state.sim);
            var restored = BuildHotel();
            restored.RestoreFrom(state.sim);

            foreach (var item in restored.Furniture.InRoom(201))
                Assert.That(item.anchorId, Is.EqualTo(before[item.instanceId]),
                            "读档后家具跳位置了");
        }

        [Test]
        public void ALegacySaveWithoutAnchorsGetsThemBackfilledWithoutCollisions()
        {
            // **两趟补派**：先落位存档里写明的，再给没有的补。反过来的话旧档条目
            // 会抢走新档明确记着的位置，两件家具叠在一起（审计点名的坑）。
            var sim = BuildHotel();
            sim.Furniture.ReplaceRoomWithTopTier(201);
            var state = new GameState();
            sim.CaptureTo(state.sim);

            // 模拟混合存档：一半条目没有锚点（旧档），一半有
            for (int i = 0; i < state.sim.furniture.Count; i += 2)
                state.sim.furniture[i].anchorId = FurnitureAnchors.Unassigned;

            var restored = BuildHotel();
            restored.RestoreFrom(state.sim);

            var used = new HashSet<int>();
            foreach (var item in restored.Furniture.InRoom(201))
            {
                Assert.That(item.anchorId, Is.Not.EqualTo(FurnitureAnchors.Unassigned),
                            "旧条目该被补派");
                Assert.That(used.Add(item.anchorId), Is.True,
                            "补派撞车了：锚点 " + item.anchorId);
            }
        }

        private static class Mathf
        {
            public static float Abs(float v) => v < 0f ? -v : v;
        }
    }
}
