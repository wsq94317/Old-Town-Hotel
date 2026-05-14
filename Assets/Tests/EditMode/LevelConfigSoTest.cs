using NUnit.Framework;
using UnityEngine;

// Story 3.5：ScriptableObject 配置基础设施回归测试。
// 不依赖 LevelConfig_Showcase.asset 真实文件 —— 用 ScriptableObject.CreateInstance<T>() 临时构造。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class LevelConfigSoTest
    {
        private GameObject _applierGo;
        private Room2DPrototypeRoomConfigApplier _applier;
        private Room2DLevelConfigSO _levelSo;

        [SetUp]
        public void SetUp()
        {
            _applierGo = new GameObject("TestApplierForSoTest");
            _applier = _applierGo.AddComponent<Room2DPrototypeRoomConfigApplier>();
            _levelSo = ScriptableObject.CreateInstance<Room2DLevelConfigSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_applierGo);
            if (_levelSo != null) Object.DestroyImmediate(_levelSo);
        }

        // AC5：SO 字段读写 round-trip。
        [Test]
        public void Test_LevelConfigSo_InstantiateAndSetFields_ReadsBackCorrectly()
        {
            _levelSo.levelName = "TestLevel";
            _levelSo.defaultRoomType = Room2DPrototypeRoomType.Better;
            _levelSo.defaultRoomCategory = Room2DRoomCategory.Family;
            _levelSo.roomRules = new Room2DPrototypeRoomConfigRule[]
            {
                new Room2DPrototypeRoomConfigRule
                {
                    ruleName = "TestRule",
                    floorNumber = 1,
                    roomNumberStart = 101,
                    roomNumberEnd = 199,
                    roomType = Room2DPrototypeRoomType.Standard,
                    facing = Room2DPrototypeFacing.StreetFacing,
                    roomCategory = Room2DRoomCategory.Twin
                }
            };

            Assert.That(_levelSo.levelName, Is.EqualTo("TestLevel"));
            Assert.That(_levelSo.defaultRoomType, Is.EqualTo(Room2DPrototypeRoomType.Better));
            Assert.That(_levelSo.defaultRoomCategory, Is.EqualTo(Room2DRoomCategory.Family));
            Assert.That(_levelSo.roomRules, Is.Not.Null);
            Assert.That(_levelSo.roomRules.Length, Is.EqualTo(1));
            Assert.That(_levelSo.roomRules[0].roomCategory, Is.EqualTo(Room2DRoomCategory.Twin));
            Assert.That(_levelSo.roomRules[0].ruleName, Is.EqualTo("TestRule"));
        }

        // AC3：applier 在 SO 引用 set 时优先读 SO,不读 inline。
        [Test]
        public void Test_Applier_WithLevelConfigSo_AppliesSoRules_NotInlineRules()
        {
            // Arrange：构造一间 floor=1 / roomNumber=101 的房间
            var roomGo = new GameObject("TestRoom101");
            var room = roomGo.AddComponent<Room2DEntity>();
            room.floorNumber = 1;
            room.roomNumber = 101;
            room.roomName = "Room 101";
            room.roomCategory = Room2DRoomCategory.Single; // 初始值

            // Inline rule:roomCategory=Family（不应被采用）
            _applier.roomRules = new Room2DPrototypeRoomConfigRule[]
            {
                new Room2DPrototypeRoomConfigRule
                {
                    ruleName = "InlineRule",
                    floorNumber = 1,
                    roomNumberStart = 101,
                    roomNumberEnd = 101,
                    roomType = Room2DPrototypeRoomType.Standard,
                    facing = Room2DPrototypeFacing.StreetFacing,
                    roomCategory = Room2DRoomCategory.Family
                }
            };

            // SO rule:roomCategory=Twin（应被采用）
            _levelSo.roomRules = new Room2DPrototypeRoomConfigRule[]
            {
                new Room2DPrototypeRoomConfigRule
                {
                    ruleName = "SoRule",
                    floorNumber = 1,
                    roomNumberStart = 101,
                    roomNumberEnd = 101,
                    roomType = Room2DPrototypeRoomType.Standard,
                    facing = Room2DPrototypeFacing.StreetFacing,
                    roomCategory = Room2DRoomCategory.Twin
                }
            };

            // 反射注入 levelConfig private field
            var soField = typeof(Room2DPrototypeRoomConfigApplier).GetField(
                "levelConfig",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            soField.SetValue(_applier, _levelSo);

            // Inject room registry（避开 FindObjectsByType,测试 isolated）
            _applier.autoFindRooms = false;
            _applier.rooms = new Room2DEntity[] { room };

            try
            {
                // Act
                _applier.ApplyRulesToRooms();

                // Assert：SO 规则胜出
                Assert.That(room.roomCategory, Is.EqualTo(Room2DRoomCategory.Twin),
                    "SO 设置时 applier 应读 SO 规则(roomCategory=Twin),实际写入了 " + room.roomCategory);
            }
            finally
            {
                Object.DestroyImmediate(roomGo);
            }
        }

        // AC4：applier 在 SO 引用 null 时回退 inline（向后兼容 Story 3）。
        [Test]
        public void Test_Applier_WithNullLevelConfigSo_FallsBackToInlineRules()
        {
            var roomGo = new GameObject("TestRoom101Fallback");
            var room = roomGo.AddComponent<Room2DEntity>();
            room.floorNumber = 1;
            room.roomNumber = 101;
            room.roomName = "Room 101";
            room.roomCategory = Room2DRoomCategory.Single;

            _applier.roomRules = new Room2DPrototypeRoomConfigRule[]
            {
                new Room2DPrototypeRoomConfigRule
                {
                    ruleName = "OnlyInlineRule",
                    floorNumber = 1,
                    roomNumberStart = 101,
                    roomNumberEnd = 101,
                    roomType = Room2DPrototypeRoomType.Standard,
                    facing = Room2DPrototypeFacing.StreetFacing,
                    roomCategory = Room2DRoomCategory.Family
                }
            };
            _applier.autoFindRooms = false;
            _applier.rooms = new Room2DEntity[] { room };
            // levelConfig 保持 null

            try
            {
                _applier.ApplyRulesToRooms();
                Assert.That(room.roomCategory, Is.EqualTo(Room2DRoomCategory.Family),
                    "SO null 时 applier 应回退 inline 规则(roomCategory=Family),实际 " + room.roomCategory);
            }
            finally
            {
                Object.DestroyImmediate(roomGo);
            }
        }
    }
}
