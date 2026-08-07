using System.Collections.Generic;
using Godot;

// 员工卡，一个角色一张。
//
// ══ 为什么不是原版的「Target: Room 201 / Remaining: 45s」══
//
// 那两行在 Sim 里没有数据源。SimPipeline 是按人力池算的：StepService 数出某角色有多少人
// 在 productive，就从队列里拉相应数量的房间进 Cleaning，全程不记录「谁」在「哪间房」。
// 会产生这个关联的 TaskDispatcher 还没建（SimPipeline.cs:90 的 TODO 就是它）。
//
// 有个陷阱：Sim 里确实存在 assignedRoomNumber，但它在 Reservation 上，是**客人**的房号，
// 不是员工的。接上去会显示一个看起来很合理的错数字——比留空更糟。
//
// 所以卡片改成池子口径，只说 Sim 真正知道的事。每一行都有真实数据支撑。
public partial class WorkerPoolCard : PanelContainer
{
    private TextureRect _portrait;
    private Label _role, _line1, _line2, _line3;

    private StaffRole _boundRole;
    private RoomSimState _queueState;

    [Signal] public delegate void DetailsPressedEventHandler(int role);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, UiTokens.CardHeight);
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

        _portrait = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(116, 116),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(_portrait);

        // 用 VBox 而不是绝对坐标：原版那套固定 x/y 是从 prefab 量出来的，其中
        // RoleLabel(x181-511) 和 DetailsButton(x459-519) 本来就是重叠的已上线 bug。
        // 交给容器排版，重叠不可能再发生。
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 6);
        col.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddChild(col);

        _role  = UiTokens.MakeLabel("", UiTokens.FontCardRole, UiTokens.InkDark, bold: true);
        _line1 = UiTokens.MakeLabel("", UiTokens.FontCardBody, UiTokens.InkSoft);
        _line2 = UiTokens.MakeLabel("", UiTokens.FontCardBody, UiTokens.InkSoft);
        _line3 = UiTokens.MakeLabel("", UiTokens.FontCardBody, UiTokens.InkSoft);
        col.AddChild(_role);
        col.AddChild(_line1);
        col.AddChild(_line2);
        col.AddChild(_line3);
    }

    /// <summary>这张卡负责哪个角色，以及它盯的是哪条房间队列。</summary>
    public void Configure(StaffRole role, string displayName, RoomSimState queueState, string portraitPath)
    {
        _boundRole = role;
        _queueState = queueState;
        _role.Text = displayName;
        _portrait.Texture = GD.Load<Texture2D>(portraitPath);
    }

    public void UpdateFrom(HotelSim sim)
    {
        int onDuty = 0, working = 0;
        var entries = sim.Staff.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.member == null || e.member.Role != _boundRole) continue;
            if (e.IsOnDuty) onDuty++;
            if (e.IsProductive) working++;
        }

        int queue = sim.Rooms.CountOf(_queueState);
        List<int> next = sim.Rooms.RoomNumbersInState(_queueState, 3);

        _line1.Text = $"{onDuty} on duty, {working} working";
        _line2.Text = $"Queue: {queue} room{(queue == 1 ? "" : "s")}";
        _line3.Text = next.Count > 0 ? $"Next: {string.Join(", ", next)}" : "Next: none";
    }
}
