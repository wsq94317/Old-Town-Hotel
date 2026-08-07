using Godot;

// 顶栏胶囊：天数 / 时间 / 心情 / 现金，浮在横幅上。
//
// ══ 修了 Unity 那边的两个 bug ══
//
// 1. 原版顶栏是 UI_RoomsScreen 的子节点 0，横幅是子节点 1 且不透明——uGUI 后画的盖前画的，
//    所以这条栏**玩家从来没看见过**。这里把它放在最后一个子节点（见 RoomsScreen.cs）。
// 2. 原版 8 个子元素在场景里全被存成 0x0。这里用基础 prefab 的尺寸重建。
//
// ══ 钱只显示一个数 ══
//
// Sim 把钱拆成三份：Cash（可花）、Safebox（已赚未收、有上限）、Overflow（溢出、只能收回 65%）。
// 顶栏只放 Cash。保险箱做成**单独的可点胶囊**，视觉上明显不同，读作「这是待收的，不是你的余额」。
// 把两个口径不同的数并排摆是本项目已经栽过三次的形态（相邻数字诱人相加，但一个能花一个不能）。
public partial class TopBar : PanelContainer
{
    private Label _day, _time, _mood, _money;
    private TextureRect _moodIcon;

    private static readonly Texture2D IconTime  = GD.Load<Texture2D>("res://art/icons/time.png");
    private static readonly Texture2D IconMoney = GD.Load<Texture2D>("res://art/icons/money.png");
    private static readonly Texture2D IconGear  = GD.Load<Texture2D>("res://art/icons/setting.png");

    private static readonly Texture2D FaceHappy = GD.Load<Texture2D>("res://art/mood/happy.png");
    private static readonly Texture2D FacePeace = GD.Load<Texture2D>("res://art/mood/peace.png");
    private static readonly Texture2D FaceSad   = GD.Load<Texture2D>("res://art/mood/sad.png");
    private static readonly Texture2D FaceAngry = GD.Load<Texture2D>("res://art/mood/angry.png");

    [Signal] public delegate void SettingsPressedEventHandler();

    public override void _Ready()
    {
        var panel = new StyleBoxFlat { BgColor = UiTokens.TopBarPanel };
        panel.SetCornerRadiusAll(UiTokens.Radius);
        panel.CornerDetail = UiTokens.CornerDetail;
        panel.ContentMarginLeft = UiTokens.TopBarPadH;
        panel.ContentMarginRight = UiTokens.TopBarPadH;
        panel.ContentMarginTop = UiTokens.TopBarPadV;
        panel.ContentMarginBottom = UiTokens.TopBarPadV;
        AddThemeStyleboxOverride("panel", panel);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", UiTokens.TopBarSpacing);
        AddChild(row);

        _day = Cell(row, "DAY 1", 120, UiTokens.TopBarText, bold: true);
        Icon(row, IconTime);
        _time = Cell(row, "08:00", 100, UiTokens.TopBarText);
        _moodIcon = Icon(row, FacePeace);
        _mood = Cell(row, "50%", 100, UiTokens.TopBarText);
        Icon(row, IconMoney);
        _money = Cell(row, "$0", 150, UiTokens.MoneyGold, bold: true, HorizontalAlignment.Left);

        var gear = new TextureButton
        {
            TextureNormal = IconGear,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            IgnoreTextureSize = true,
            CustomMinimumSize = new Vector2(50, 50),
        };
        gear.Pressed += () => EmitSignal(SignalName.SettingsPressed);
        row.AddChild(gear);
    }

    public void UpdateFrom(HotelSim sim)
    {
        _day.Text = $"DAY {sim.Clock.CurrentDay}";
        _time.Text = sim.Clock.TimeFormatted;
        _money.Text = $"${sim.Cash:N0}";

        // 口径换算：Sim 的权威是 Reputation.Stars（1.0-5.0 的滚动窗口均值），
        // 原型那个 SatisfactionScore 是个无界累加的整数、在视图层硬钳到 0-100，本身没有意义。
        // 映射成百分比是本次移植定的口径，不是原样搬运——新开的酒店正好落在 50%，
        // 所以下面的表情阈值也跟着重挑过，不能沿用原型的。
        float pct = Mathf.Clamp((sim.Reputation.Stars - 1f) / 4f * 100f, 0f, 100f);
        _mood.Text = $"{Mathf.RoundToInt(pct)}%";
        _moodIcon.Texture = pct >= 75f ? FaceHappy
                          : pct >= 50f ? FacePeace
                          : pct >= 25f ? FaceSad
                          : FaceAngry;
    }

    private static Label Cell(HBoxContainer row, string text, int width, Color colour,
                              bool bold = false, HorizontalAlignment align = HorizontalAlignment.Center)
    {
        var l = UiTokens.MakeLabel(text, UiTokens.FontTopBar, colour, bold, align);
        l.CustomMinimumSize = new Vector2(width, 22);
        row.AddChild(l);
        return l;
    }

    private static TextureRect Icon(HBoxContainer row, Texture2D tex)
    {
        // 50px 图标比 6/6 内边距留下的 48px 内容高度高 2px，上下各溢出 1px。
        // 原版也是这样，别"修"成别的 y，否则整排的视觉重心会偏。
        var r = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(50, 50),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(r);
        return r;
    }
}
