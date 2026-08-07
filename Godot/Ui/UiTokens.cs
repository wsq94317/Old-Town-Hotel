using Godot;

// 设计令牌 —— 界面里所有颜色、字号、间距的唯一来源。
//
// 在 Unity 那边这件事是坏的：UITheme.cs 里那套字号阶梯**运行时根本没人读**，
// 真正生效的值被写死在每个 prefab 的 TMP 组件里（见 2026-06-15 设计规格第 25 行）。
// 于是 theme 沦为文档，改一次字号要翻遍嵌套 prefab 的 override。
//
// Godot 有一等公民的 Theme 资源，所以移植顺手把这件事修好：下面的常量是唯一真相，
// 界面代码只准引用它们，不准写字面量。
//
// 参考空间 1080x1920（project.godot 的 viewport 尺寸），与 Unity CanvasScaler 基准一致，
// 所以设计规格里的像素值可以原样搬过来。
public static class UiTokens
{
    // ── 页面与结构 ────────────────────────────────────────────────────────
    public static readonly Color CreamPage  = new Color("#FFF7E8");
    public static readonly Color CardWhite  = new Color("#FFFFFF");
    public static readonly Color CreamSoft  = new Color("#F3E6D0");
    public static readonly Color BrownDeep  = new Color("#B85E3C");
    public static readonly Color GoldAccent = new Color("#C99A4A");

    // ── 文字 ──────────────────────────────────────────────────────────────
    public static readonly Color InkDark       = new Color("#3A2A1C");
    public static readonly Color InkSoft       = new Color("#6B5840");
    public static readonly Color SecondaryGrey = new Color("#8A7E6E");

    // ── 顶栏（浮在横幅上的半透明胶囊）────────────────────────────────────
    // 注意 BrownDeep #B85E3C 和房态色 Dirty #B85842 视觉上很接近但是两个 token，别混用。
    public static readonly Color TopBarPanel = new Color("#241710", 0.62f);
    public static readonly Color TopBarText  = new Color("#FBE9C8");
    public static readonly Color MoneyGold   = new Color("#F0CB78");
    public static readonly Color BannerTitle = new Color("#FFE7BE");

    // ── 房间格子 ──────────────────────────────────────────────────────────
    public static readonly Color TileText  = new Color("#FFFFFF");
    public static readonly Color BedLetter = new Color("#FFFFFF", 0.92f);

    // ── 按钮 ──────────────────────────────────────────────────────────────
    public static readonly Color ButtonGold       = new Color("#C2872F");
    public static readonly Color ButtonGoldShadow = new Color("#9C6A22");

    // ── 字号阶梯（1080x1920 参考空间）────────────────────────────────────
    // 取自 Unity prefab 里 TMP 组件的实测值，不是 UITheme 里那套没生效的。
    // 两处 prefab 与场景不一致的地方以场景为准（BedLegend 25 而非 11；卡片 24/20 而非 14/12）。
    // TMP 的 fontSize 不是像素高度，Godot 默认字体是 Open Sans 而非 LiberationSans，
    // 所以这些值是起点，需要看屏微调（侦察风险项 R5）。
    public const int FontRoomNumber = 53;
    public const int FontBannerTitle = 50;
    public const int FontTopBar = 35;
    public const int FontNavLabel = 26;
    public const int FontLegend = 25;   // 也用于格子的计时/床型文字
    public const int FontCardRole = 24;
    public const int FontCardBody = 20;
    public const int FontStateBadge = 13;
    public const int FontDetails = 12;

    // ── 间距与形状 ────────────────────────────────────────────────────────
    public const int PageMarginX = 28;
    public const int GridGap = 8;
    public const int CellSize = 250;
    public const int GridColumns = 4;
    public const int CardGutter = 20;
    public const int CardPad = 16;
    public const int CardTextX = 150;
    public const int TopBarPadH = 26;
    public const int TopBarPadV = 6;
    public const int TopBarSpacing = 14;
    public const int NavHeight = 180;

    // Unity 用的是内置 UISprite 九宫格（border 10）。那张图不在仓库里——它随 Unity 发行，
    // 没有任何 GUID 能解析到文件路径，很容易漏。这里用 StyleBoxFlat 的圆角替代。
    public const int Radius = 10;
    public const int CornerDetail = 12;   // 默认 8，在 >20px 圆角上会看出多边形棱角

    // ── 布局带（Layout B：修正版，见下）──────────────────────────────────
    // 原 Unity 布局有三个已上线的 bug，这里没有照搬：
    //   1. 顶栏被不透明横幅整个盖住（子节点顺序问题）—— 这里放到最后一个子节点
    //   2. 员工卡 y 到 1780，压在 y1740 起的底栏下面 —— 这里收到 1710
    //   3. DetailsButton 压在 RoleLabel 上 —— 这里把 RoleLabel 宽度 330 收到 262
    public const int BannerHeight = 681;
    public const int TopBarTop = 70;
    public const int TopBarHeight = 60;
    public const int BannerTitleY = 591;
    public const int GridTop = 700;
    public const int GridBottom = 1480;
    public const int LegendTop = 1490;
    public const int LegendHeight = 40;
    public const int CardsTop = 1550;
    public const int CardsBottom = 1710;
    public const int CardHeight = 160;
    public const int CardRoleLabelWidth = 262;

    // ── 阴影 ──────────────────────────────────────────────────────────────
    // Unity 的 Shadow.effectDistance 在 RectTransform 空间里 +y 向上，Godot 的阴影偏移
    // +y 向下。所有 y 分量都翻了符号（Unity (3,-5) -> Godot (3,+5)）。
    // 另外 Unity 的阴影是硬拷贝无模糊，StyleBoxFlat.ShadowSize 是模糊半径且为 0 时不画，
    // 这里接受柔和阴影而不是硬边。
    public static readonly Color CardShadow = new Color(InkSoft, 0.30f);
    public static readonly Vector2 CardShadowOffset = new Vector2(3, 5);
    public const int CardShadowSize = 6;

    /// <summary>标准卡片底：圆角 + 柔和投影。</summary>
    public static StyleBoxFlat CardStyle(Color fill)
    {
        var sb = new StyleBoxFlat { BgColor = fill, ShadowColor = CardShadow, ShadowSize = CardShadowSize, ShadowOffset = CardShadowOffset };
        sb.SetCornerRadiusAll(Radius);
        sb.CornerDetail = CornerDetail;
        return sb;
    }

    /// <summary>无边框无底的 StyleBox，用于把 Button 的四态外观清空。</summary>
    public static StyleBoxEmpty Blank() => new StyleBoxEmpty();

    // ── 字体 ──────────────────────────────────────────────────────────────
    // 用 Unity 那边同一个 LiberationSans.ttf（从 TextMesh Pro/Fonts 拷来的）。
    // Godot 默认字体是 Open Sans，度量不同——换成同一个 ttf 之后，从 prefab 里量出来的
    // 字号才能原样使用，否则每个标签的宽度都要重新试（侦察风险项 R5）。
    //
    // 仓库里没有 Bold 变体：Unity 那边的粗体是 TMP 用 SDF 材质伪造的。
    // Godot 的等价做法是 FontVariation 的字重合成。
    private static FontFile _regular;
    private static FontVariation _bold;

    public static FontFile Regular =>
        _regular ??= GD.Load<FontFile>("res://art/fonts/LiberationSans.ttf");

    public static FontVariation Bold =>
        _bold ??= new FontVariation { BaseFont = Regular, VariationEmbolden = 0.55f };

    /// <summary>建一个统一了字体/字号/颜色的 Label，避免各处重复三行样板。</summary>
    public static Label MakeLabel(string text, int size, Color colour, bool bold = false,
                                  HorizontalAlignment hAlign = HorizontalAlignment.Left)
    {
        var l = new Label
        {
            Text = text,
            HorizontalAlignment = hAlign,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        l.AddThemeFontOverride("font", bold ? (Font)Bold : Regular);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", colour);
        return l;
    }
}
