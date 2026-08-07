using Godot;

// 验证链路的探针，不是产品代码。
//
// Phase 3 要在 Godot 里重建界面，而「重建得对不对」只能用眼睛判断。所以在写任何
// UI 之前，先证明这条路走得通：起一个窗口 -> 画点东西 -> 等一帧真正画完 -> 存 PNG。
//
// 关键在「等一帧真正画完」：截图早了会拿到空白帧或上一帧，那种失败很隐蔽——
// 你会以为是布局写错了，其实只是抓早了。
//
// 跑法：
//   <godot> --path Godot res://Dev/ScreenshotSpike.tscn
public partial class ScreenshotSpike : Control
{
    private const string OutPath = "res://Dev/spike-shot.png";

    public override void _Ready()
    {
        // 1080x1920 竖屏参考分辨率下的一个色块 + 文字，用真实的设计 token 配色，
        // 顺便验证颜色和字号在截图里是不是所见即所得。
        var bg = new ColorRect
        {
            Color = new Color("#FFF7E8"),          // creamPage
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        AddChild(bg);

        var card = new PanelContainer
        {
            OffsetLeft = 32, OffsetTop = 200,      // 设计规格里的 32px 安全内边距
            OffsetRight = -32, OffsetBottom = 600,
            AnchorRight = 1,
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color("#FFFFFF"),        // cardWhite
            CornerRadiusTopLeft = 28, CornerRadiusTopRight = 28,
            CornerRadiusBottomLeft = 28, CornerRadiusBottomRight = 28,
            ShadowColor = new Color(0.47f, 0.31f, 0.16f, 0.10f),
            ShadowSize = 12,
        };
        card.AddThemeStyleboxOverride("panel", style);
        AddChild(card);

        var label = new Label
        {
            Text = "SPIKE\n1080x1920\ncreamPage / cardWhite",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeColorOverride("font_color", new Color("#3A2A1C"));  // inkDark
        label.AddThemeFontSizeOverride("font_size", 48);                  // displayXl
        card.AddChild(label);

        CallDeferred(nameof(Capture));
    }

    private async void Capture()
    {
        // 等两帧再抓：第一帧只保证节点树就绪，不保证已经光栅化。
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        Image img = GetViewport().GetTexture().GetImage();
        string abs = ProjectSettings.GlobalizePath(OutPath);
        Error err = img.SavePng(abs);

        GD.Print(err == Error.Ok
            ? $"[spike] OK {img.GetWidth()}x{img.GetHeight()} -> {abs}"
            : $"[spike] FAILED to save: {err}");

        GetTree().Quit(err == Error.Ok ? 0 : 1);
    }
}
