using System.Collections.Generic;
using Godot;

// Rooms 屏。直接绑 HotelSim，不经过 Unity 那套原型控制器
// （Room2DDemoDayController / FrontDesk2D / Housekeeper2D / Room2DEntity[]）——
// 那一层是 v1/v2 的脚手架，移植它等于把要扔的东西再翻译一遍。
//
// 子节点顺序即绘制顺序，TopBar 刻意放最后：Unity 那边它排在横幅前面，
// 被不透明横幅整个盖住，玩家从来没见过顶栏。
//
// 布局一律用 SetAnchorsAndOffsetsPreset。SetAnchorsPreset 只改锚点、并反算 offset
// 保持当前矩形不变（刚 new 出来是 0x0），会导致整屏塌成零尺寸。
public partial class RoomsScreen : Control
{
    private TextureRect _banner;
    private TopBar _topBar;
    private Button _safeboxPill;
    private Label _safeboxLabel;
    private GridContainer _grid;
    private WorkerPoolCard _hskCard, _inspCard;

    private readonly List<RoomTile> _tiles = new List<RoomTile>();
    private HotelSim _sim;

    [Signal] public delegate void RoomTappedEventHandler(int roomNumber);
    [Signal] public delegate void CollectSafeboxEventHandler();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var page = new ColorRect { Color = UiTokens.CreamPage, MouseFilter = MouseFilterEnum.Ignore };
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(page);

        BuildBanner();
        BuildGrid();
        BuildLegend();
        BuildWorkerCards();
        BuildSafeboxPill();

        // ── 最后一个子节点：这一行就是那个 bug 的修复 ──
        _topBar = new TopBar();
        _topBar.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _topBar.OffsetLeft = UiTokens.PageMarginX;
        _topBar.OffsetRight = -UiTokens.PageMarginX;
        _topBar.OffsetTop = UiTokens.TopBarTop;
        _topBar.OffsetBottom = UiTokens.TopBarTop + UiTokens.TopBarHeight;
        AddChild(_topBar);
    }

    private void BuildBanner()
    {
        _banner = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://art/banners/rooms_banner_2.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            // 刻意用 Scale 而不是 KeepAspect：源图 1659x948（1.750）塞进 1080x681（1.586），
            // 原版就是这样压扁约 10% 的，保持一致。
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _banner.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _banner.OffsetBottom = UiTokens.BannerHeight;
        AddChild(_banner);

        var title = UiTokens.MakeLabel("Rooms", UiTokens.FontBannerTitle, UiTokens.BannerTitle, bold: true);
        title.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        title.OffsetLeft = 56;
        title.OffsetRight = 56 + 640;
        title.OffsetTop = UiTokens.BannerTitleY;
        title.OffsetBottom = UiTokens.BannerTitleY + 64;
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.70f));
        title.AddThemeConstantOverride("shadow_offset_x", 2);
        title.AddThemeConstantOverride("shadow_offset_y", 3);   // Unity 是 -3（+y 向上），这里翻符号
        _banner.AddChild(title);
    }

    private void BuildGrid()
    {
        // Unity 那边网格没有裁剪也没有 ContentSizeFitter，12 间房正好三行；房间一多就会
        // 溢出去压住下面的图例和员工卡。而酒店会变大是这游戏的核心，所以这里套 ScrollContainer。
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        scroll.OffsetLeft = UiTokens.PageMarginX;
        scroll.OffsetRight = -UiTokens.PageMarginX;
        scroll.OffsetTop = UiTokens.GridTop;
        scroll.OffsetBottom = UiTokens.GridBottom;
        AddChild(scroll);

        _grid = new GridContainer { Columns = UiTokens.GridColumns };
        _grid.AddThemeConstantOverride("h_separation", UiTokens.GridGap);
        _grid.AddThemeConstantOverride("v_separation", UiTokens.GridGap);
        scroll.AddChild(_grid);
    }

    private void BuildLegend()
    {
        // 原版图例写的是 "S: Standard"，但枚举成员叫 Single，而首字母是从枚举名取的——
        // 字母对上纯属巧合。这里统一成 Single。
        var legend = UiTokens.MakeLabel("K: King    T: Twin    F: Family    S: Single",
                                        UiTokens.FontLegend, UiTokens.SecondaryGrey,
                                        hAlign: HorizontalAlignment.Center);
        legend.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        legend.OffsetLeft = 20;
        legend.OffsetRight = -20;
        legend.OffsetTop = UiTokens.LegendTop;
        legend.OffsetBottom = UiTokens.LegendTop + UiTokens.LegendHeight;
        AddChild(legend);
    }

    private void BuildWorkerCards()
    {
        var row = new HBoxContainer();
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        row.OffsetLeft = UiTokens.PageMarginX;
        row.OffsetRight = -UiTokens.PageMarginX;
        row.OffsetTop = UiTokens.CardsTop;
        row.OffsetBottom = UiTokens.CardsBottom;
        row.AddThemeConstantOverride("separation", UiTokens.CardGutter);
        AddChild(row);

        _hskCard = new WorkerPoolCard();
        row.AddChild(_hskCard);
        _hskCard.Configure(StaffRole.Housekeeper, "HOUSEKEEPING", RoomSimState.Cleaning,
                           "res://art/staff/worker_housekeeper.png");

        _inspCard = new WorkerPoolCard();
        row.AddChild(_inspCard);
        _inspCard.Configure(StaffRole.Inspector, "INSPECTION", RoomSimState.AwaitingInspection,
                            "res://art/staff/worker_inspector.png");
    }

    private void BuildSafeboxPill()
    {
        // 保险箱不和现金并排。它是「已赚未收」，口径和 Cash 不同（不能直接花），
        // 两个数字挨在一起会诱人相加——这个项目已经在「两个数字看着矛盾」上栽过三次。
        // 做成金色可点胶囊，视觉上明确是个动作而不是一个余额读数。
        _safeboxPill = new Button { Flat = true };
        var style = new StyleBoxFlat { BgColor = UiTokens.ButtonGold };
        style.SetCornerRadiusAll(UiTokens.Radius);
        style.CornerDetail = UiTokens.CornerDetail;
        style.ShadowColor = new Color(UiTokens.ButtonGoldShadow, 0.55f);
        style.ShadowSize = 4;
        style.ShadowOffset = new Vector2(0, 4);
        foreach (var slot in new[] { "normal", "hover", "pressed", "focus" })
            _safeboxPill.AddThemeStyleboxOverride(slot, style);

        _safeboxPill.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        _safeboxPill.OffsetLeft = -(UiTokens.PageMarginX + 320);
        _safeboxPill.OffsetRight = -UiTokens.PageMarginX;
        _safeboxPill.OffsetTop = UiTokens.TopBarTop + UiTokens.TopBarHeight + 16;
        _safeboxPill.OffsetBottom = UiTokens.TopBarTop + UiTokens.TopBarHeight + 80;
        _safeboxPill.Visible = false;
        _safeboxPill.Pressed += () => EmitSignal(SignalName.CollectSafebox);
        AddChild(_safeboxPill);

        _safeboxLabel = UiTokens.MakeLabel("", UiTokens.FontCardRole, UiTokens.CreamPage,
                                           bold: true, hAlign: HorizontalAlignment.Center);
        _safeboxLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _safeboxPill.AddChild(_safeboxLabel);
    }

    public void Bind(HotelSim sim)
    {
        _sim = sim;
        Refresh();
    }

    /// <summary>每 0.25 秒调一次。只在房间数变了才动节点树，其余全是原地改值。</summary>
    public void Refresh()
    {
        if (_sim == null) return;

        if (_tiles.Count != _sim.Rooms.Count) RebuildTiles();
        for (int i = 0; i < _tiles.Count; i++)
            _tiles[i].UpdateFrom(_sim.Rooms.Peek(i));   // Peek 不脏化聚合缓存，At()/AtIndex() 会

        _topBar.UpdateFrom(_sim);
        _hskCard.UpdateFrom(_sim);
        _inspCard.UpdateFrom(_sim);

        int box = _sim.Safebox.Balance;
        _safeboxPill.Visible = box > 0;
        if (box > 0)
            _safeboxLabel.Text = _sim.Safebox.IsFull ? $"COLLECT ${box:N0} (FULL)" : $"COLLECT ${box:N0}";
    }

    private void RebuildTiles()
    {
        foreach (var t in _tiles) t.QueueFree();
        _tiles.Clear();

        for (int i = 0; i < _sim.Rooms.Count; i++)
        {
            var tile = new RoomTile();
            _grid.AddChild(tile);
            tile.TileTapped += n => EmitSignal(SignalName.RoomTapped, n);
            _tiles.Add(tile);
        }
    }
}
