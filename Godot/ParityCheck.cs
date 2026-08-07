using Godot;

// Phase 1 的全部内容：证明 Godot 这一侧接线正确。
//
// 跑同一个 ParityScenario（源码在 Assets/Game/Scripts/Sim/Parity/，经由
// OldTownHotel.Sim.csproj 进到这里），把账本写到文件，然后由外部跟
// Sim/golden/parity-ledger.txt 逐字节比对。
//
// 无头运行：
//   <godot> --headless --path Godot --quit-after 2
//
// 为什么写文件而不是 GD.Print：stdout 里混着 Godot 自己的启动日志，
// diff 会被噪声污染。文件是干净的。
public partial class ParityCheck : Node
{
    private const string OutputPath = "res://parity-output.txt";

    public override void _Ready()
    {
        string ledger = ParityScenario.Run();

        // GlobalizePath 把 res:// 换成真实 OS 路径，这样能用 System.IO 直接写，
        // 且导出后的路径语义仍然正确。
        string path = ProjectSettings.GlobalizePath(OutputPath);
        System.IO.File.WriteAllText(path, ledger);

        GD.Print($"[parity] ParityScenario v{ParityScenario.FormatVersion} -> {path}");
        GD.Print($"[parity] {ledger.Length} chars, {ledger.Split('\n').Length - 1} lines");

        GetTree().Quit(0);
    }
}
