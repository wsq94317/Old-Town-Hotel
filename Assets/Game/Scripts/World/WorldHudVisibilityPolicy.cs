// 从 WorldManagementHud.cs 顶部拆出来的纯策略。
//
// 这两个类型本身零 Unity 依赖，之前只是因为「写在哪个文件里」这个偶然，被焊在了一个
// 4,234 行、拖着 TMPro/UnityEngine.UI/InputSystem 的 MonoBehaviour 上——于是
// WorldHudVisibilityPolicyTest 明明只测 7 行纯逻辑，却必须启动 Unity 才能跑。
//
// 拆出来后它进了 OldTownHotel.Sim.csproj，测试可脱离引擎运行。
// 这是整个 Godot 移植会反复用到的动作的一个缩影：先把纯逻辑从引擎类型里剥出来，
// 剥出来的部分就自动是「已移植」状态。

public enum WorldHudMode
{
    Hidden,
    Operations,
    MorningReport
}

public static class WorldHudVisibilityPolicy
{
    public static WorldHudMode Resolve(bool simulationReady, bool awaitingMorningReport)
    {
        if (!simulationReady) return WorldHudMode.Hidden;
        return awaitingMorningReport ? WorldHudMode.MorningReport : WorldHudMode.Operations;
    }
}
