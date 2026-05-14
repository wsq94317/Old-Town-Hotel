using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// EditMode 单元测试：Story 2 客人 minimal identity（type × preference）。
//
// 测试隔离策略：
//   1. [SetUp] 用固定 seed 调用 UnityEngine.Random.InitState() — 让 PickRandomGuest* 完全确定
//   2. 在测试用 GameObject 上 AddComponent<Room2DPrototypeDemandLoop>() — Awake 立即触发
//   3. PickRandomGuestType / PickRandomGuestPreference 是 public seam — 不需要 rooms/overview/scene
//   4. [TearDown] DestroyImmediate(GameObject) — 测试间无残留
//
// 命名风格沿用 Story 1 的 Test_PascalCase（与 .claude/rules/test-standards.md 的 snake_case
// 风格冲突，但全 sprint 内一致；命名风格统一是项目级 tech debt，非单个 story 决策）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class GuestProfileGenerationTest
    {
        private const int DETERMINISTIC_SEED = 12345;
        private const int DISTRIBUTION_SAMPLE_SIZE = 100;
        private const int DISTRIBUTION_MAX_COUNT_PER_BUCKET = 60;

        private GameObject _go;
        private Room2DPrototypeDemandLoop _loop;

        [SetUp]
        public void SetUp()
        {
            // 固定 seed 保证 PickRandom* 在每次测试运行中产生相同序列；
            // 避免 CI 与本地之间出现 flaky 分布。
            Random.InitState(DETERMINISTIC_SEED);

            _go = new GameObject("TestDemandLoopForGuestProfile");
            _loop = _go.AddComponent<Room2DPrototypeDemandLoop>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── AC1：生成的客人 type / preference 非空（枚举值有效） ─────────────────

        [Test]
        public void Test_GeneratedGuest_HasNonNullTypeAndPreference()
        {
            // Arrange / Act
            Room2DGuestType guestType = _loop.PickRandomGuestType();
            Room2DGuestPreference guestPref = _loop.PickRandomGuestPreference();

            // Assert: 枚举值落在已定义范围内（IsDefined 防御未来若新增/移除枚举值导致漏赋）。
            Assert.That(System.Enum.IsDefined(typeof(Room2DGuestType), guestType),
                Is.True,
                "PickRandomGuestType() 返回值必须是已定义的 Room2DGuestType 枚举值。");
            Assert.That(System.Enum.IsDefined(typeof(Room2DGuestPreference), guestPref),
                Is.True,
                "PickRandomGuestPreference() 返回值必须是已定义的 Room2DGuestPreference 枚举值。");
        }

        // ── AC2：type 分布"足够均匀"（防常量返回） ────────────────────────────

        [Test]
        public void Test_GeneratedGuests_TypeDistribution_RoughlyEven()
        {
            // Arrange
            var counts = new Dictionary<Room2DGuestType, int>
            {
                { Room2DGuestType.Business, 0 },
                { Room2DGuestType.Family, 0 },
                { Room2DGuestType.VIP, 0 }
            };

            // Act
            for (int i = 0; i < DISTRIBUTION_SAMPLE_SIZE; i++)
            {
                counts[_loop.PickRandomGuestType()]++;
            }

            // Assert：理想情况每桶 ~33；门槛 60 显著宽松，仅捕"常量返回"或"完全不随机"。
            foreach (KeyValuePair<Room2DGuestType, int> bucket in counts)
            {
                Assert.That(bucket.Value, Is.LessThanOrEqualTo(DISTRIBUTION_MAX_COUNT_PER_BUCKET),
                    "Room2DGuestType." + bucket.Key + " 在 100 次抽样中出现 "
                    + bucket.Value + " 次，超过 60 次上限 — 怀疑常量返回或权重失衡。");
            }

            // 同时保证每个值至少出现过一次（防"两个分支永远走不到"的 bug）。
            foreach (KeyValuePair<Room2DGuestType, int> bucket in counts)
            {
                Assert.That(bucket.Value, Is.GreaterThan(0),
                    "Room2DGuestType." + bucket.Key + " 在 100 次抽样中一次都没出现 — switch 分支缺失。");
            }
        }

        // ── AC3：preference 分布"足够均匀"（防常量返回） ──────────────────────

        [Test]
        public void Test_GeneratedGuests_PreferenceDistribution_RoughlyEven()
        {
            // Arrange
            var counts = new Dictionary<Room2DGuestPreference, int>
            {
                { Room2DGuestPreference.QuietFloor, 0 },
                { Room2DGuestPreference.HighFloor, 0 },
                { Room2DGuestPreference.GroundFloor, 0 }
            };

            // Act
            for (int i = 0; i < DISTRIBUTION_SAMPLE_SIZE; i++)
            {
                counts[_loop.PickRandomGuestPreference()]++;
            }

            // Assert
            foreach (KeyValuePair<Room2DGuestPreference, int> bucket in counts)
            {
                Assert.That(bucket.Value, Is.LessThanOrEqualTo(DISTRIBUTION_MAX_COUNT_PER_BUCKET),
                    "Room2DGuestPreference." + bucket.Key + " 在 100 次抽样中出现 "
                    + bucket.Value + " 次，超过 60 次上限 — 怀疑常量返回或权重失衡。");
            }

            foreach (KeyValuePair<Room2DGuestPreference, int> bucket in counts)
            {
                Assert.That(bucket.Value, Is.GreaterThan(0),
                    "Room2DGuestPreference." + bucket.Key + " 在 100 次抽样中一次都没出现 — switch 分支缺失。");
            }
        }

        // ── AC6：room-type 匹配检查的回归保护 ─────────────────────────────────
        //
        // 设计备注：当前垂直切片里 room-type 是"软偏好"而非"硬约束"
        // （Room2DPrototypeDemandLoop.cs::DoesRoomMatchRoomTypePreference 注释明示：
        // "房型不再阻止入住，只作为投诉和匹配质量风险"）。
        // 故本测试覆盖的是「BetterRoomPreferred 偏好 + Standard 房型 → 匹配检查返回 false」
        // 这一既有行为不因 Story 2 的字段追加而退化。
        // 真正的硬约束（King/Twin/Family Bed）由未来的 story 引入。

        [Test]
        public void Test_FitCheck_StillRejectsMismatchedRoomType()
        {
            // Arrange：构造一间 Standard 房间。
            var roomGo = new GameObject("StandardRoomForFitCheck");
            var room = roomGo.AddComponent<Room2DEntity>();
            room.prototypeRoomType = Room2DPrototypeRoomType.Standard;

            try
            {
                // Act：客人需要 Better 房型，但提供的是 Standard 房。
                bool matches = _loop.DoesRoomMatchRoomTypePreferenceForTesting(
                    room, Room2DPrototypeDemandLoop.Room2DRoomPreference.BetterRoomPreferred);

                // Assert
                Assert.That(matches, Is.False,
                    "Standard 房 + BetterRoomPreferred 客人偏好应判定为不匹配；"
                    + "若该断言失败，说明 Story 2 字段追加意外影响了房型匹配逻辑。");

                // 正向对照：AnyRoom 偏好应永远匹配。
                bool anyRoomMatches = _loop.DoesRoomMatchRoomTypePreferenceForTesting(
                    room, Room2DPrototypeDemandLoop.Room2DRoomPreference.AnyRoom);
                Assert.That(anyRoomMatches, Is.True,
                    "Standard 房 + AnyRoom 偏好必须匹配；若失败说明匹配逻辑反转。");
            }
            finally
            {
                Object.DestroyImmediate(roomGo);
            }
        }
    }
}
