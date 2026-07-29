using NUnit.Framework;

// 翻译表的健全性。
//
// **这条测试的来历**：我往表里加了一条已经存在的 "RENOVATE"，
// `Dictionary` 的集合初始化器抛 ArgumentException——而那是**静态**构造函数，
// 一炸整个 GameText 类型就永久损坏，之后每次 T() 都抛，**游戏里 UI 全没了**。
// 玩家看到的是一个空壳子，控制台里一屏红。
//
// 一个漏译键的爆炸半径不该是整个界面。所以做了两件事：
//   ① 容器改成"重复就忽略并记下来"（最坏结果是后一条不生效）；
//   ② 这条测试，把重复挡在运行之前。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class GameTextTest
    {
        [Test]
        public void TheTranslationTableHasNoDuplicateKeys()
        {
            var duplicates = GameText.DuplicateKeys();
            Assert.That(duplicates, Is.Empty,
                        "重复的键：" + string.Join(", ", duplicates)
                        + "。后加的那条不会生效——把它删掉，或者改成一个新键。");
        }

        [Test]
        public void TheTableIsActuallyPopulated()
        {
            // 防一个愚蠢的回归：容器换过一次，要是 Add 被写坏了，
            // 表会静默变空，界面全变英文而没人报错
            Assert.That(GameText.EntryCount, Is.GreaterThan(200));
        }

        [Test]
        public void AMissingKeyFallsBackToEnglishInsteadOfThrowing()
        {
            // 漏译只该显示英文，绝不能空白或报错
            const string never = "THIS KEY IS DEFINITELY NOT IN THE TABLE 12345";
            Assert.That(GameText.T(never), Is.EqualTo(never));
        }

        [Test]
        public void FormattingWorksThroughTheTranslationLayer()
        {
            // F() 先翻译模板再填参数。模板漏译时也得能填参数，不能把占位符吐出来
            string result = GameText.F("THIS TEMPLATE IS NOT TRANSLATED {0}", 7);
            Assert.That(result, Is.EqualTo("THIS TEMPLATE IS NOT TRANSLATED 7"));
        }

        [Test]
        public void NullAndEmptyAreSafe()
        {
            Assert.That(GameText.T(null), Is.Null);
            Assert.That(GameText.T(""), Is.Empty);
            Assert.That(GameText.F(null), Is.Null);
        }

        [Test]
        public void EveryTranslationKeepsItsPlaceholders()
        {
            // 中文译文漏掉 {0} 会让玩家看到一句没有数字的话（"仓库只装得下 份了"），
            // 多写一个 {2} 会在 string.Format 里抛 FormatException——那又是一次
            // 运行时爆炸。两种都该在这里被挡住。
            foreach (var pair in GameText.AllPairs())
            {
                int englishSlots = CountPlaceholders(pair.Key);
                int chineseSlots = CountPlaceholders(pair.Value);
                Assert.That(chineseSlots, Is.EqualTo(englishSlots),
                            "占位符数量不一致：\n  英文：" + pair.Key + "\n  中文：" + pair.Value);
            }
        }

        /// <summary>数出 {0}..{9} 里出现过的不同占位符个数（重复出现只算一次）。</summary>
        private static int CountPlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            for (int index = 0; index <= 9; index++)
                if (text.Contains("{" + index + "}")) count++;
            return count;
        }
    }
}
