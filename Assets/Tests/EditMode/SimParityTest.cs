using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;

// 跨引擎一致性基准（Godot 移植 Phase 1）。
//
// ParityScenario 用固定种子跑 14 天，输出逐行账本；本测试断言它与 golden 文件逐字节相同。
// 同一份场景 + 同一份 Sim 源码，由 dotnet test / Unity / Godot 三方运行——
// 谁的输出偏了，谁的接线就有问题。
//
// 更新 golden（**只在你确认数字变化是有意的之后**）：
//     PARITY_UPDATE=1 dotnet test Sim.Tests/OldTownHotel.Sim.Tests.csproj
// PowerShell 下：
//     $env:PARITY_UPDATE=1; dotnet test Sim.Tests\OldTownHotel.Sim.Tests.csproj; $env:PARITY_UPDATE=$null
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class SimParityTest
    {
        [Test]
        public void Ledger_MatchesGolden()
        {
            string actual = Normalize(ParityScenario.Run());
            string path = GoldenPath();

            if (Environment.GetEnvironmentVariable("PARITY_UPDATE") == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, actual);
                Assert.Ignore("PARITY_UPDATE=1: golden 已重写 -> " + path
                              + "\n请 review diff 后再提交，然后不带该变量重跑一次。");
            }

            Assert.That(File.Exists(path), Is.True,
                        "golden 文件不存在：" + path + "\n用 PARITY_UPDATE=1 生成一份。");

            string expected = Normalize(File.ReadAllText(path));
            Assert.That(actual, Is.EqualTo(expected),
                        "Sim 输出与 golden 不符。若是有意的规则改动，用 PARITY_UPDATE=1 重写 golden；"
                        + "若不是，说明有东西悄悄改变了模拟结果。");
        }

        [Test]
        public void Run_IsDeterministic_AcrossRepeatedCalls()
        {
            // golden 比对只能抓「和上次不一样」；这条抓的是「同一次运行内部就不稳定」
            // ——静态状态残留、字典遍历顺序、时间/随机源泄漏都会在这里现形。
            Assert.That(ParityScenario.Run(), Is.EqualTo(ParityScenario.Run()));
        }

        /// <summary>
        /// golden 路径。用 CallerFilePath 在**编译期**拿到本文件的绝对路径再往上走，
        /// 因为运行时的工作目录三个宿主各不相同（Unity 下 AppContext.BaseDirectory
        /// 指向编辑器安装目录，根本不在工程里）。
        /// </summary>
        private static string GoldenPath([CallerFilePath] string thisFile = "")
        {
            // <repo>/Assets/Tests/EditMode/SimParityTest.cs -> 上溯 3 层到 <repo>
            var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile));
            for (int i = 0; i < 3 && dir != null; i++) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "无法从 " + thisFile + " 定位仓库根目录");
            return Path.Combine(dir.FullName, "Sim", "golden", "parity-ledger.txt");
        }

        /// <summary>行尾统一 —— 免得 git autocrlf 把 golden 变成假失败。</summary>
        private static string Normalize(string s) => s.Replace("\r\n", "\n");
    }
}
