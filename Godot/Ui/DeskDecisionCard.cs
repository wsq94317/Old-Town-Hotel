using Godot;

// 一条待决事项的卡片：头像 + 标题 + 一行事实 +（退款才有的）客人原话 + 动作按钮。
//
// 超售给三个按钮（Upgrade / Compensate / Walk Away），退款给两个（Approve / Reject）。
// 按钮数量随类型变，所以每次 Bind 时重建按钮行——一屏最多几张卡，这个代价可以忽略。
//
// Upgrade 按钮在没房时会被禁用而不是隐藏：让玩家看见"本来可以升级，但你现在没空房"，
// 比让选项凭空消失更能解释发生了什么。
public partial class DeskDecisionCard : PanelContainer
{
    private TextureRect _portrait;
    private ColorRect _accent;
    private Label _headline, _detail, _quote;
    private HBoxContainer _actions;

    private DeskDecision _bound;

    [Signal] public delegate void ResolvedEventHandler(string what);

    private HotelSim _sim;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", UiTokens.CardStyle(UiTokens.CardWhite));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UiTokens.CardPad);
        margin.AddThemeConstantOverride("margin_right", UiTokens.CardPad);
        margin.AddThemeConstantOverride("margin_top", UiTokens.CardPad);
        margin.AddThemeConstantOverride("margin_bottom", UiTokens.CardPad);
        AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        margin.AddChild(row);

        // 客群色条：一眼区分 VIP / 商务 / 派对 / 预算，不承载状态含义
        _accent = new ColorRect { CustomMinimumSize = new Vector2(6, 0), MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(_accent);

        _portrait = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(104, 104),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(_portrait);

        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 6);
        row.AddChild(col);

        _headline = UiTokens.MakeLabel("", UiTokens.FontCardRole, UiTokens.InkDark, bold: true);
        _detail   = UiTokens.MakeLabel("", UiTokens.FontCardBody, UiTokens.InkSoft);
        _quote    = UiTokens.MakeLabel("", UiTokens.FontCardBody, UiTokens.SecondaryGrey);
        _quote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_headline);
        col.AddChild(_detail);
        col.AddChild(_quote);

        _actions = new HBoxContainer();
        _actions.AddThemeConstantOverride("separation", 10);
        col.AddChild(_actions);
    }

    public void Bind(HotelSim sim, DeskDecision d)
    {
        _sim = sim;
        _bound = d;

        _accent.Color = GuestSegmentUi.Accent(d.Segment);
        _portrait.Texture = GD.Load<Texture2D>(GuestSegmentUi.PortraitPath(d.Segment));
        _headline.Text = d.Headline;
        _detail.Text = d.Detail;

        // 客人原话只有退款请求有。没有就整行收起来，不留空白行。
        _quote.Visible = !string.IsNullOrEmpty(d.Quote);
        _quote.Text = d.Quote != null ? $"“{d.Quote}”" : "";

        foreach (var c in _actions.GetChildren()) ((Node)c).QueueFree();

        if (d.Kind == DeskDecisionKind.Overbooking)
        {
            // 没空房时禁用而不是隐藏——让玩家看见这个选项存在但当前不可行。
            AddAction("Upgrade", primary: true, enabled: d.CanUpgrade,
                      () => Resolve(OverbookingResolution.Upgrade));
            AddAction("Compensate", primary: false, enabled: true,
                      () => Resolve(OverbookingResolution.Compensate));
            AddAction("Walk away", primary: false, enabled: true,
                      () => Resolve(OverbookingResolution.WalkAway));
        }
        else
        {
            AddAction("Approve", primary: true, enabled: true, () =>
            {
                _sim.ApproveRefund(_bound.Id);
                EmitSignal(SignalName.Resolved, $"refund {_bound.Id} approved");
            });
            AddAction("Reject", primary: false, enabled: true, () =>
            {
                _sim.RejectRefund(_bound.Id);
                EmitSignal(SignalName.Resolved, $"refund {_bound.Id} rejected");
            });
        }
    }

    private void Resolve(OverbookingResolution how)
    {
        // out reason 是 Sim 给的失败原因，直接转出去而不是吞掉——
        // 按钮点了没反应是最难查的一类问题。
        bool ok = _sim.TryResolveOverbooking(_bound.Id, how, out string reason);
        EmitSignal(SignalName.Resolved, ok ? $"overbooking {_bound.Id} -> {how}"
                                           : $"overbooking {_bound.Id} refused: {reason}");
    }

    private void AddAction(string text, bool primary, bool enabled, System.Action onPressed)
    {
        var b = new Button { Text = text, Disabled = !enabled };
        // 注意不能设 Flat = true：Godot 把 normal/hover/pressed 的 stylebox 绘制包在
        // if (!flat) 里，配了自定义样式还设 Flat 就等于什么都不画（见 RoomsScreen 的收款胶囊）。
        var style = new StyleBoxFlat { BgColor = primary ? UiTokens.ButtonGold : UiTokens.CreamSoft };
        style.SetCornerRadiusAll(UiTokens.Radius);
        style.CornerDetail = UiTokens.CornerDetail;
        style.ContentMarginLeft = 16;
        style.ContentMarginRight = 16;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 6;

        var dim = (StyleBoxFlat)style.Duplicate();
        dim.BgColor = new Color(style.BgColor, 0.35f);

        b.AddThemeStyleboxOverride("normal", style);
        b.AddThemeStyleboxOverride("hover", style);
        b.AddThemeStyleboxOverride("pressed", style);
        b.AddThemeStyleboxOverride("disabled", dim);
        b.AddThemeFontOverride("font", UiTokens.Bold);
        b.AddThemeFontSizeOverride("font_size", UiTokens.FontCardBody);
        b.AddThemeColorOverride("font_color", primary ? UiTokens.CreamPage : UiTokens.InkDark);
        b.AddThemeColorOverride("font_disabled_color", new Color(UiTokens.InkSoft, 0.45f));
        if (enabled) b.Pressed += () => onPressed();
        _actions.AddChild(b);
    }
}
