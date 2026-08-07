using System.Collections.Generic;
using System.Text;
using Godot;

// FrontDesk 屏。
//
// ══ 这屏为什么和 Unity 原版长得不一样 ══
//
// 原版显示「当前客人卡 + 到店排队列表」，绑在 Room2DPrototypeDemandLoop 上。那套东西在
// Sim 里没有对应物，而且不是"私有"是"不存在"：
//   · _deskQueue 是 private Queue<int>，只公开了长度
//   · 队列元素是裸 int 票据，walk-in 就是数字 0
//   · walk-in 的客群直到 AdmitOneGuest 执行那一刻才用私有 _rng 掷出来
// 也就是说排队中客人的身份**尚未被创造**，加个访问器也拿不到。
//
// 更根本的是：Sim 里前台放人是自动的（RoomMatcher 每分钟自己配房），
// 原版那个"给这位客人选房"的决策在 Sim 中根本不存在，玩家从来不会被问。
//
// 所以这屏改成显示**真正等着玩家拍板的事**：超售处置和退款审批。这两样是前台数据里
// 唯一同时满足「公开 + 逐项 + 有身份 + 有提交动作」的，而且每个按钮都真的会改变模拟状态。
//
// 排队本身仍然显示，但只用它诚实拿得到的两个数：队长和推算等待。
public partial class FrontDeskScreen : Control
{
    private TopBar _topBar;
    private Label _phaseLabel, _queueLabel, _waitLabel, _arrivalsLabel, _inHouseLabel;
    private Label _decisionsHeader;
    private VBoxContainer _decisionsList;

    private readonly List<DeskDecisionCard> _cards = new List<DeskDecisionCard>();
    private string _lastSignature = "";
    private HotelSim _sim;

    [Signal] public delegate void DecisionResolvedEventHandler(string what);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var page = new ColorRect { Color = UiTokens.CreamPage, MouseFilter = MouseFilterEnum.Ignore };
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(page);

        BuildBanner();
        BuildStatusCard();
        BuildDecisions();

        // 最后一个子节点。Unity 那边顶栏排在不透明横幅前面，被整个盖住，玩家从没见过。
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
        var banner = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://art/banners/frontdesk_banner.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        banner.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        banner.OffsetBottom = UiTokens.BannerHeight;
        AddChild(banner);

        var title = UiTokens.MakeLabel("Front Desk", UiTokens.FontBannerTitle, UiTokens.BannerTitle, bold: true);
        title.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        title.OffsetLeft = 56;
        title.OffsetRight = 56 + 720;
        title.OffsetTop = UiTokens.BannerTitleY;
        title.OffsetBottom = UiTokens.BannerTitleY + 64;
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.70f));
        title.AddThemeConstantOverride("shadow_offset_x", 2);
        title.AddThemeConstantOverride("shadow_offset_y", 3);
        banner.AddChild(title);
    }

    private void BuildStatusCard()
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", UiTokens.CardStyle(UiTokens.CardWhite));
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        card.OffsetLeft = UiTokens.PageMarginX;
        card.OffsetRight = -UiTokens.PageMarginX;
        card.OffsetTop = UiTokens.GridTop;
        card.OffsetBottom = UiTokens.GridTop + 210;
        AddChild(card);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", UiTokens.CardPad);
        card.AddChild(margin);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        margin.AddChild(col);

        _phaseLabel    = UiTokens.MakeLabel("", UiTokens.FontCardRole, UiTokens.InkDark, bold: true);
        _queueLabel    = UiTokens.MakeLabel("", UiTokens.FontCardBody, UiTokens.InkSoft);
        _waitLabel     = UiTokens.MakeLabel("", UiTokens.FontCardBody, UiTokens.InkSoft);
        _arrivalsLabel = UiTokens.MakeLabel("", UiTokens.FontCardBody, UiTokens.InkSoft);
        _inHouseLabel  = UiTokens.MakeLabel("", UiTokens.FontCardBody, UiTokens.InkSoft);
        col.AddChild(_phaseLabel);
        col.AddChild(_queueLabel);
        col.AddChild(_waitLabel);
        col.AddChild(_arrivalsLabel);
        col.AddChild(_inHouseLabel);
    }

    private void BuildDecisions()
    {
        _decisionsHeader = UiTokens.MakeLabel("", UiTokens.FontLegend, UiTokens.SecondaryGrey);
        _decisionsHeader.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _decisionsHeader.OffsetLeft = UiTokens.PageMarginX;
        _decisionsHeader.OffsetRight = -UiTokens.PageMarginX;
        _decisionsHeader.OffsetTop = UiTokens.GridTop + 226;
        _decisionsHeader.OffsetBottom = UiTokens.GridTop + 262;
        AddChild(_decisionsHeader);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        scroll.OffsetLeft = UiTokens.PageMarginX;
        scroll.OffsetRight = -UiTokens.PageMarginX;
        scroll.OffsetTop = UiTokens.GridTop + 270;
        scroll.OffsetBottom = UiTokens.CardsBottom;
        AddChild(scroll);

        _decisionsList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _decisionsList.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(_decisionsList);
    }

    public void Bind(HotelSim sim)
    {
        _sim = sim;
        Refresh();
    }

    public void Refresh()
    {
        if (_sim == null) return;

        _topBar.UpdateFrom(_sim);
        RefreshStatus();
        RefreshDecisions();
    }

    private void RefreshStatus()
    {
        _phaseLabel.Text = PhaseScheduler.Label(PhaseScheduler.PhaseFor(_sim.Clock.CurrentMinute));

        int queue = _sim.DeskQueueLength;
        _queueLabel.Text = queue == 0 ? "Nobody waiting at the desk"
                                      : $"{queue} guest{(queue == 1 ? "" : "s")} waiting to check in";

        // 等待时间：复刻 Sim 私有的 EstimatedWaitMinutes，但**不复刻它的哨兵值**。
        // 前台无人时 CurrentCheckInsPerHour 返回 0，原式会吐出常数 60——那不是一个测量结果，
        // 是一个"没人管"的标记。把它当分钟数显示出来就是在编一个具体的数字。
        float perHour = _sim.CurrentCheckInsPerHour;
        if (queue == 0)
            _waitLabel.Text = "No wait";
        else if (!_sim.FrontDeskCoverageAvailable || perHour <= 0f)
            _waitLabel.Text = "Front desk unmanned — nobody is being checked in";
        else
            _waitLabel.Text = $"About {Mathf.RoundToInt(queue / perHour * 60f)} min to clear the queue";

        _arrivalsLabel.Text = $"Today: {_sim.ArrivalsCheckedInToday} in of {_sim.ArrivalsPlannedToday} expected"
                            + (_sim.ArrivalsTurnedAwayToday > 0 ? $", {_sim.ArrivalsTurnedAwayToday} turned away" : "");

        // 用房态数而不是 ActiveStays()：后者遍历 Dictionary，顺序不稳定，
        // 而这里只要一个计数，房态是同一事实的稳定来源。
        _inHouseLabel.Text = $"In house: {_sim.Rooms.CountOf(RoomSimState.Occupied)} · checked out today: {_sim.CheckoutsToday}";
    }

    private void RefreshDecisions()
    {
        // 必须先取快照：PendingRefunds / PendingOverbookings 是活视图，
        // 而处理动作会从底层 List 里移除元素。
        List<DeskDecision> decisions = DeskDecision.Collect(_sim);

        _decisionsHeader.Text = decisions.Count == 0
            ? "NOTHING NEEDS YOU RIGHT NOW"
            : $"NEEDS YOU · {decisions.Count}";

        // 只在集合真的变了才重建节点。用 kind+id 组成的签名判断，
        // 光比数量会漏掉"一个进一个出"的情况。
        var sb = new StringBuilder();
        foreach (var d in decisions) sb.Append((int)d.Kind).Append(':').Append(d.Id).Append('|');
        string signature = sb.ToString();
        if (signature == _lastSignature)
        {
            for (int i = 0; i < _cards.Count && i < decisions.Count; i++)
                _cards[i].Bind(_sim, decisions[i]);
            return;
        }
        _lastSignature = signature;

        foreach (var c in _cards) c.QueueFree();
        _cards.Clear();

        foreach (var d in decisions)
        {
            var card = new DeskDecisionCard();
            _decisionsList.AddChild(card);
            card.Bind(_sim, d);
            card.Resolved += what =>
            {
                _lastSignature = "";                  // 强制下一帧重建
                EmitSignal(SignalName.DecisionResolved, what);
            };
            _cards.Add(card);
        }
    }
}
