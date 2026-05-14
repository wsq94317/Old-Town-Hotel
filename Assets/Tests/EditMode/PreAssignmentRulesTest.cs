using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试:Story 3 配对硬约束(Q2 方案 C 完整房型系统)。
//
// 测试隔离策略:
//   1. [SetUp] 每个测试 new 一个 Room2DEntity GameObject + 用反射 / public field 设 roomCategory
//   2. Rules 是 static 类,直接 Room2DPreAssignmentRules.CanReserve(...) 调用,无 MonoBehaviour 依赖
//   3. [TearDown] DestroyImmediate(GameObject) —— 与 Story 1/2 风格保持一致
//
// 命名风格沿用 Story 1/2 的 Test_PascalCase(项目级 TD-005 已记录,本 sprint 不切换)。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class PreAssignmentRulesTest
    {
        private GameObject _roomGo;
        private Room2DEntity _room;

        [SetUp]
        public void SetUp()
        {
            _roomGo = new GameObject("TestRoomForPreAssignmentRules");
            _room = _roomGo.AddComponent<Room2DEntity>();
            _room.roomName = "Room 101";
            SetRoomCategory(_room, Room2DRoomCategory.Single);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_roomGo);
        }

        // helper:统一构造 fixture。roomCategory 现在是 public 字段(项目惯例,
        // 对齐 prototypeRoomType / prototypeFacing 等),直接赋值即可。
        private static void SetRoomCategory(Room2DEntity room, Room2DRoomCategory category)
        {
            room.roomCategory = category;
        }

        // ── 测试 1:Any 偏好对任意房型都接受(Business 经济舱场景) ─────────────────

        [Test]
        public void Test_CanReserve_BedTypeAny_AnyCategory_Accepted()
        {
            // 对 Single / Twin / Family 三档房型轮流断言 Any 都接受。
            foreach (Room2DRoomCategory cat in System.Enum.GetValues(typeof(Room2DRoomCategory)))
            {
                // Arrange
                SetRoomCategory(_room, cat);

                // Act
                var result = Room2DPreAssignmentRules.CanReserve(
                    _room, Room2DBedTypePreference.Any, Room2DGuestType.Business);

                // Assert
                Assert.That(result.ok, Is.True,
                    "Any bedType 偏好应接受 " + cat + " 房型;返回 ok=false 说明硬约束规则错误地拒绝了 Any。");
                Assert.That(result.reason, Is.Empty,
                    "accept 时 reason 必须为空字符串(UI 不显示 Last Action)。");
            }
        }

        // ── 测试 2:Single 偏好 + Single 房 → 接受(精确匹配 positive case) ──────

        [Test]
        public void Test_CanReserve_BedTypeSingle_SingleRoom_Accepted()
        {
            // Arrange
            SetRoomCategory(_room, Room2DRoomCategory.Single);

            // Act
            var result = Room2DPreAssignmentRules.CanReserve(
                _room, Room2DBedTypePreference.Single, Room2DGuestType.Business);

            // Assert
            Assert.That(result.ok, Is.True, "Single bedType + Single 房应接受。");
            Assert.That(result.reason, Is.Empty);
        }

        // ── 测试 3:Single 偏好 + Family 房 → 拒绝(硬约束 mismatch) ───────────

        [Test]
        public void Test_CanReserve_BedTypeSingle_FamilyRoom_Refused()
        {
            // Arrange
            SetRoomCategory(_room, Room2DRoomCategory.Family);

            // Act
            var result = Room2DPreAssignmentRules.CanReserve(
                _room, Room2DBedTypePreference.Single, Room2DGuestType.Business);

            // Assert
            Assert.That(result.ok, Is.False, "Single bedType + Family 房应拒绝。");
            Assert.That(result.reason, Is.Not.Empty, "拒绝时必须给 UI 一段非空 reason。");
        }

        // ── 测试 4:Twin 偏好 + Family 房 → 拒绝 ────────────────────────────

        [Test]
        public void Test_CanReserve_BedTypeTwin_FamilyRoom_Refused()
        {
            // Arrange
            SetRoomCategory(_room, Room2DRoomCategory.Family);

            // Act
            var result = Room2DPreAssignmentRules.CanReserve(
                _room, Room2DBedTypePreference.Twin, Room2DGuestType.Family);

            // Assert
            Assert.That(result.ok, Is.False, "Twin bedType + Family 房应拒绝(房型不一致即拒)。");
            Assert.That(result.reason, Is.Not.Empty);
        }

        // ── 测试 5:Family 偏好 + Single 房 → 拒绝(VIP/Family 客人塞不下) ─────

        [Test]
        public void Test_CanReserve_BedTypeFamily_SingleRoom_Refused()
        {
            // Arrange
            SetRoomCategory(_room, Room2DRoomCategory.Single);

            // Act
            var result = Room2DPreAssignmentRules.CanReserve(
                _room, Room2DBedTypePreference.Family, Room2DGuestType.VIP);

            // Assert
            Assert.That(result.ok, Is.False, "Family bedType + Single 房应拒绝;VIP 客人需要 Family 房。");
            Assert.That(result.reason, Is.Not.Empty);
        }

        // ── 测试 6:Family 偏好 + Family 房 → 接受(稀缺资源精确匹配) ──────────

        [Test]
        public void Test_CanReserve_BedTypeFamily_FamilyRoom_Accepted()
        {
            // Arrange
            SetRoomCategory(_room, Room2DRoomCategory.Family);

            // Act
            var result = Room2DPreAssignmentRules.CanReserve(
                _room, Room2DBedTypePreference.Family, Room2DGuestType.Family);

            // Assert
            Assert.That(result.ok, Is.True, "Family bedType + Family 房应接受(稀缺资源精确匹配)。");
            Assert.That(result.reason, Is.Empty);
        }

        // ── 测试 7:null room 拒绝并给 UI 友好的 reason ─────────────────────

        [Test]
        public void Test_CanReserve_NullRoom_Refused()
        {
            // Act
            var result = Room2DPreAssignmentRules.CanReserve(
                null, Room2DBedTypePreference.Single, Room2DGuestType.Business);

            // Assert
            Assert.That(result.ok, Is.False, "null room 应拒绝。");
            Assert.That(result.reason, Is.Not.Empty,
                "null room 必须给一段非空 reason,UI 才有得显示。");
            Assert.That(result.reason.ToLowerInvariant(), Does.Contain("room"),
                "null room 的 reason 必须提到 'room' 关键字;当前 reason: " + result.reason);
        }

        // ── 测试 8:拒绝 reason 必须含具体房号 + 客人 bedType 需求 ─────────────
        //
        // 业务意图:玩家点错房后,Last Action 提示必须直接指出"哪间房 / 客人要什么",
        // 否则就是普通的红字 toast,玩家学不到信息。

        [Test]
        public void Test_CanReserve_RefusalReason_ContainsRoomCategoryAndBedType()
        {
            // Arrange
            SetRoomCategory(_room, Room2DRoomCategory.Single);
            _room.roomName = "Room 207";

            // Act
            var result = Room2DPreAssignmentRules.CanReserve(
                _room, Room2DBedTypePreference.Family, Room2DGuestType.VIP);

            // Assert
            Assert.That(result.ok, Is.False);
            Assert.That(result.reason, Does.Contain("Room 207"),
                "拒绝 reason 必须含具体房号(roomName)便于玩家定位;当前 reason: " + result.reason);
            Assert.That(result.reason, Does.Contain("Single"),
                "拒绝 reason 必须含房间实际 RoomCategory;当前 reason: " + result.reason);
            Assert.That(result.reason, Does.Contain("Family"),
                "拒绝 reason 必须含客人 bedTypePreference;当前 reason: " + result.reason);
        }
    }
}
