using Godot;

// 房间格子，250x250。整块可点。
//
// 数据源是 Sim 的 RoomRecord，不经过 Room2DState（理由见 RoomStateUi.cs 顶部注释）。
//
// 关键性能约束：格子**只在房间数变化时重建**，每次刷新走 UpdateFrom() 原地改值。
// Unity 那边 0.25 秒轮询一次，2 倍速下一分钟就是 120 次——每次重建节点树不可接受。
//
// 布局全部用 SetAnchorsAndOffsetsPreset 而不是 SetAnchorsPreset：后者只改锚点，
// 并且会反算 offset 来**保持当前矩形不变**，而节点刚 new 出来时矩形是 0x0，
// 结果就是能按内容自撑的子节点撑开了、不能自撑的（ColorRect/TextureRect）直接消失。
public partial class RoomTile : Button
{
    private TextureRect _interior;
    private ColorRect _badge;
    private Label _badgeLabel;
    private Label _numberLabel;
    private Label _timerLabel;
    private Label _bedLabel;

    private Room2DRoomCategory _lastCategory = (Room2DRoomCategory)(-1);
    private RoomSimState _lastState = (RoomSimState)(-1);

    /// <summary>本格对应的房号。点击事件带它出去，避免持有 RoomRecord 的过期拷贝。</summary>
    public int RoomNumber { get; private set; }

    [Signal] public delegate void TileTappedEventHandler(int roomNumber);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(UiTokens.CellSize, UiTokens.CellSize);
        Flat = true;
        ClipContents = true;

        // 把 Button 自带的四态外观清空，这里的视觉全部由子节点提供
        foreach (var slot in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(slot, UiTokens.Blank());

        _interior = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,   // Unity 那边 preserveAspect=false
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _interior.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_interior);

        // 状态条贴底，满色；上面的内景图只上 25% 淡洗
        _badge = new ColorRect { MouseFilter = MouseFilterEnum.Ignore };
        _badge.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _badge.OffsetTop = -24;
        _badge.OffsetBottom = 0;
        AddChild(_badge);

        _badgeLabel = UiTokens.MakeLabel("", UiTokens.FontStateBadge, UiTokens.TileText,
                                         bold: true, hAlign: HorizontalAlignment.Center);
        _badgeLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _badge.AddChild(_badgeLabel);

        _numberLabel = UiTokens.MakeLabel("", UiTokens.FontRoomNumber, UiTokens.TileText,
                                          bold: true, hAlign: HorizontalAlignment.Center);
        _numberLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _numberLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _numberLabel.OffsetTop = 10;
        _numberLabel.OffsetBottom = 90;
        AddShadow(_numberLabel, new Color(0, 0, 0, 0.65f), 2, 2);
        AddChild(_numberLabel);

        // 计时文字：Sim 里目前没有任何逐房进度数据（派工系统未建），所以恒定隐藏。
        // 节点保留是为了等 TaskDispatcher 建好之后直接接上，而不是先摆一行空文字骗自己。
        _timerLabel = UiTokens.MakeLabel("", UiTokens.FontLegend, UiTokens.TileText,
                                         bold: true, hAlign: HorizontalAlignment.Center);
        _timerLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _timerLabel.OffsetTop = 130;
        _timerLabel.OffsetBottom = 158;
        _timerLabel.Visible = false;
        AddChild(_timerLabel);

        // 床型首字母，右下角
        _bedLabel = UiTokens.MakeLabel("", UiTokens.FontLegend, UiTokens.BedLetter,
                                       bold: true, hAlign: HorizontalAlignment.Center);
        _bedLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        _bedLabel.OffsetLeft = -64;
        _bedLabel.OffsetRight = -16;
        _bedLabel.OffsetTop = -62;
        _bedLabel.OffsetBottom = -28;
        AddShadow(_bedLabel, new Color(0, 0, 0, 0.5f), 1, 1);
        AddChild(_bedLabel);

        Pressed += () => EmitSignal(SignalName.TileTapped, RoomNumber);
    }

    /// <summary>原地更新。只在真的变了的时候才碰纹理和颜色——每 0.25 秒会调一次。</summary>
    public void UpdateFrom(RoomRecord room)
    {
        RoomNumber = room.number;
        _numberLabel.Text = room.number.ToString();

        if (room.category != _lastCategory)
        {
            _lastCategory = room.category;
            _interior.Texture = GD.Load<Texture2D>(RoomStateUi.InteriorPath(room.category));
            _bedLabel.Text = RoomStateUi.BedLetter(room.category);
        }

        if (room.state != _lastState)
        {
            _lastState = room.state;
            var (colour, label) = RoomStateUi.For(room.state);
            _badge.Color = colour;
            _badgeLabel.Text = label;
            _interior.SelfModulate = RoomStateUi.Wash(colour);
        }
    }

    // Unity 的 effectDistance 在 RectTransform 空间里 +y 向上，Godot 的 +y 向下，
    // 调用方传进来的已经是翻过符号的值。
    private static void AddShadow(Label l, Color colour, int dx, int dy)
    {
        l.AddThemeColorOverride("font_shadow_color", colour);
        l.AddThemeConstantOverride("shadow_offset_x", dx);
        l.AddThemeConstantOverride("shadow_offset_y", dy);
    }
}
