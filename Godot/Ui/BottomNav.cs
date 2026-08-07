using Godot;

// 底部三标签导航，1080x180，贴屏底。
//
// 在 Unity 里它不是任何一屏的子节点，而是 Canvas 下和各屏并列的兄弟节点、画在最后，
// 所以永远浮在内容之上。这里照搬这个结构（见 Main.cs 的 CanvasLayer 顺序）。
//
// 修了一个已上线的 bug：Unity 的 BottomNavView 把 roomsInactive 指向了和 roomsActive
// 同一张图，所以 Rooms 标签永远不会变暗。真正的 inactive 切片一直躺在图集里没人用
// （BottomNav_Rooms_Inactive，1134,553,270,305）——顺手接上了。
public partial class BottomNav : Panel
{
    public enum Tab { FrontDesk, Rooms, Lounge }

    private readonly (Tab Tab, string Label, string Active, string Inactive)[] _defs =
    {
        (Tab.FrontDesk, "Front Desk", "res://art/nav/reception_active.png", "res://art/nav/reception_inactive.png"),
        (Tab.Rooms,     "Rooms",      "res://art/nav/rooms_active.png",     "res://art/nav/rooms_inactive.png"),
        (Tab.Lounge,    "Lounge",     "res://art/nav/lounge_active.png",    "res://art/nav/lounge_inactive.png"),
    };

    private readonly TextureRect[] _icons = new TextureRect[3];
    private readonly Label[] _labels = new Label[3];
    private Tab _current = Tab.Rooms;

    [Signal] public delegate void TabSelectedEventHandler(int tab);

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = UiTokens.CreamPage });

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        AddChild(margin);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 0);
        margin.AddChild(tabs);

        for (int i = 0; i < _defs.Length; i++)
        {
            var d = _defs[i];

            var btn = new Button { Flat = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach (var slot in new[] { "normal", "hover", "pressed", "focus" })
                btn.AddThemeStyleboxOverride(slot, UiTokens.Blank());
            tabs.AddChild(btn);

            var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            // 实测的垂直节奏：8 上 + 112 图标 + 10 间隙 + 34 文字 + 8 下 = 172。
            // 那个 10 是 Unity childForceExpand 算出来的结果而不是设置的 spacing 值，
            // 直接写死，别去复现那套机制。
            box.AddThemeConstantOverride("separation", 10);
            box.Alignment = BoxContainer.AlignmentMode.Center;
            btn.AddChild(box);

            _icons[i] = new TextureRect
            {
                Texture = GD.Load<Texture2D>(d.Inactive),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(0, 112),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            box.AddChild(_icons[i]);

            _labels[i] = UiTokens.MakeLabel(d.Label, UiTokens.FontNavLabel, UiTokens.BrownDeep,
                                            hAlign: HorizontalAlignment.Center);
            _labels[i].CustomMinimumSize = new Vector2(0, 34);
            box.AddChild(_labels[i]);

            Tab captured = d.Tab;
            btn.Pressed += () => { Select(captured); EmitSignal(SignalName.TabSelected, (int)captured); };
        }

        Select(_current);
    }

    public void Select(Tab tab)
    {
        _current = tab;
        for (int i = 0; i < _defs.Length; i++)
        {
            bool on = _defs[i].Tab == tab;
            _icons[i].Texture = GD.Load<Texture2D>(on ? _defs[i].Active : _defs[i].Inactive);
            // 原版只有图标换图，没有任何文字反馈。这里给选中项加一档字色对比，
            // 因为切片本身的明暗差在小图标上不够明显。
            _labels[i].AddThemeColorOverride("font_color", on ? UiTokens.InkDark : UiTokens.BrownDeep);
        }
    }
}
