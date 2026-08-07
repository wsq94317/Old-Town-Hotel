using System.Collections.Generic;
using Godot;

// Godot 侧的宿主：持有 HotelSim，推时间，刷界面。
//
// Unity 那边这一层散在十几个 MonoBehaviour 里（Room2DDemoDayController、
// Room2DDayPhaseStateMachine、FrontDesk2D…），界面绑的是它们而不是 Sim。
// 这里不移植那一层——Sim 已经是共享、有测试、逐字节对齐过的内核，直接接它。
//
// 节点顺序照搬 Unity 的 Canvas 结构：BottomNav 是各屏的兄弟且排在后面，
// 所以永远浮在内容之上。
public partial class Main : Node
{
    private HotelSim _sim;
    private RoomsScreen _rooms;
    private FrontDeskScreen _frontDesk;
    private BottomNav _nav;
    private BottomNav.Tab _tab = BottomNav.Tab.Rooms;

    private double _uiAccumulator;
    private const double UiRefreshSeconds = 0.25;   // 与 Unity 的轮询间隔一致

    // 开发用截图钩子：跑 N 秒让模拟推进出有内容的画面，截一张然后退出。
    //   <godot> --path Godot -- --shot=8
    // 目的是让「改了布局之后看一眼」可重复、可自动化。
    private double _shotAfter = -1;
    private bool _shotTaken;

    public override void _Ready()
    {
        // 先解析影响开局配置的参数，再建 Sim——房间数这类东西必须在构造时就定下来。
        int rooms = 12;
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--rooms="))
                rooms = arg.Substring(8).ToInt();

        _sim = BuildHotel(rooms);

        var layer = new CanvasLayer();
        AddChild(layer);

        // 各屏是彼此重叠的兄弟节点，靠 Visible 切换——和 Unity 那边
        // HotelUIFlow.SwitchToTab 的做法一致（三个屏 prefab 叠在一起，切显隐）。
        _frontDesk = new FrontDeskScreen();
        layer.AddChild(_frontDesk);
        _frontDesk.Bind(_sim);
        _frontDesk.DecisionResolved += what => GD.Print($"[ui] {what}");

        _rooms = new RoomsScreen();
        layer.AddChild(_rooms);
        _rooms.Bind(_sim);
        _rooms.RoomTapped += n => GD.Print($"[ui] room {n} tapped");
        _rooms.CollectSafebox += () => GD.Print($"[ui] collected ${_sim.CollectSafebox()}");

        _nav = new BottomNav();
        layer.AddChild(_nav);                   // 后加 = 画在上层
        _nav.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        _nav.OffsetTop = -UiTokens.NavHeight;
        _nav.OffsetBottom = 0;
        _nav.TabSelected += t => SwitchTab((BottomNav.Tab)t);
        _nav.Select(_tab);
        SwitchTab(_tab);

        _sim.BeginDay();

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--shot="))
            {
                _shotAfter = arg.Substring(7).ToFloat();
                // 截图运行必须屏蔽 GUI 输入，否则抓到的不是一个确定的状态。
                // 实测过一次：一轮 110 秒的截图跑里凭空出现了 room 201..208 的点击
                // （正好是网格的蛇形焦点导航序）、一次收款、和十几次标签切换——
                // 大概率是手柄轴漂移在驱动 Godot 内置的 ui_left/ui_right/ui_accept。
                // 界面能被键盘/手柄导航是好事，但截图harness 需要的是可复现，不是可操作。
                GetViewport().GuiDisableInput = true;
            }
            // 倍速走 SimClock 自己的 SpeedMultiplier：它只改 tick 产出速率，
            // 不改任何游戏时间语义（一天仍是 840 分钟），所以快进出来的状态是真状态。
            else if (arg.StartsWith("--speed="))
                _sim.Clock.SpeedMultiplier = arg.Substring(8).ToFloat();
            // 故意超售是真实的游戏策略（OverbookingAllowance 是公开设置），调高它产生的是
            // Sim 真正生成的超售事件，不是塞给界面的假数据——这样截图验证的才是真链路。
            else if (arg.StartsWith("--overbook="))
                _sim.OverbookingAllowance = arg.Substring(11).ToInt();
            else if (arg == "--tab=frontdesk")
                { _nav.Select(BottomNav.Tab.FrontDesk); SwitchTab(BottomNav.Tab.FrontDesk); }
        }
    }

    public override void _Process(double delta)
    {
        // Sim 是 tick 驱动的：时钟吃真实时间，攒够一分钟就吐一个 tick。
        _sim.Clock.Advance((float)delta);
        while (_sim.Clock.TryConsumeTick()) _sim.StepMinute();

        if (_sim.Clock.DayEndReached) EndDay();

        // 界面刷新与 tick 解耦并节流：2 倍速下一分钟会有 120 个 tick，
        // 每个 tick 都重刷界面是纯浪费。
        _uiAccumulator += delta;
        if (_uiAccumulator >= UiRefreshSeconds)
        {
            _uiAccumulator = 0;
            // 只刷当前可见的那一屏。隐藏的屏没人看，刷它纯属浪费。
            if (_tab == BottomNav.Tab.Rooms) _rooms.Refresh();
            else if (_tab == BottomNav.Tab.FrontDesk) _frontDesk.Refresh();
        }

        if (_shotAfter > 0 && !_shotTaken)
        {
            _shotAfter -= delta;
            if (_shotAfter <= 0) { _shotTaken = true; Capture(); }
        }
    }

    private void SwitchTab(BottomNav.Tab tab)
    {
        _tab = tab;
        _rooms.Visible = tab == BottomNav.Tab.Rooms;
        _frontDesk.Visible = tab == BottomNav.Tab.FrontDesk;
        // Lounge 还没做——先留在 Rooms 上，而不是切到一片空白。
        if (tab == BottomNav.Tab.Lounge)
        {
            GD.Print("[ui] Lounge screen not built yet");
            _rooms.Visible = true;
            _tab = BottomNav.Tab.Rooms;
            _nav.Select(BottomNav.Tab.Rooms);
        }

        if (_tab == BottomNav.Tab.Rooms) _rooms.Refresh();
        else _frontDesk.Refresh();
    }

    private async void Capture()
    {
        // 必须等 FramePostDraw：早一帧抓到的图尺寸正确但内容是空的，
        // 那种失败看起来像「布局没生效」，其实只是抓早了。
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = ProjectSettings.GlobalizePath("res://Dev/rooms-screen.png");
        Error err = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print(err == Error.Ok ? $"[shot] {path}" : $"[shot] FAILED {err}");
        GetTree().Quit(err == Error.Ok ? 0 : 1);
    }

    private void EndDay()
    {
        var result = _sim.SettleDay();
        GD.Print($"[sim] day {_sim.Clock.CurrentDay} settled: net ${result.NetProfit}, box ${_sim.Safebox.Balance}");

        // 脚本化的「称职玩家」：发工资 + 报修，跟 ParityScenario 里同一套动作。
        // 不做这两件事的话酒店会在第 9 天前后死透（欠薪 -> 士气崩，家具坏 -> 锁房），
        // 界面就只剩一屏零，看不出任何东西。
        _sim.PayWages();
        DoMaintenance(_sim);

        _sim.Clock.BeginNextDay();
        _sim.BeginDay();
    }

    private static void DoMaintenance(HotelSim sim)
    {
        if (sim.Materials.Stock < 4) sim.TryBuyMaterials(8);

        var repairable = new List<int>();
        var all = sim.Furniture.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i].IsRepairable) repairable.Add(all[i].instanceId);
        for (int i = 0; i < repairable.Count; i++)
            sim.TryRepairFurniture(repairable[i], out _);
    }

    /// <summary>
    /// 默认与 ParityScenario 相同的开局配置（12 房），方便把屏幕上的数字和账本对上。
    /// rooms 可由 --rooms=N 覆盖：房间少于当日客量时会真的挤出超售事件，
    /// 那是验证前台决策卡最省事的真实路径。
    /// </summary>
    private static HotelSim BuildHotel(int rooms)
    {
        var defs = new List<RoomDefinition>();
        for (int i = 0; i < rooms; i++)
            defs.Add(new RoomDefinition(201 + i, floor: 1, zone: 1, Room2DRoomCategory.Single,
                                        RoomTier.Old, RoomSimState.Ready));

        var staff = new StaffRoster();
        for (int i = 0; i < 2; i++)
            staff.Register(new StaffMember(StaffRole.Housekeeper, "HSK" + i, 60,
                                           new StaffAttributes(55, 55, 55), 1, null));
        staff.Register(new StaffMember(StaffRole.Inspector, "INSP0", 70,
                                       new StaffAttributes(55, 55, 55), 1, null));
        for (int i = 0; i < 2; i++)
            staff.Register(new StaffMember(StaffRole.Reception, "RCP" + i, 65,
                                           new StaffAttributes(55, 55, 55), 1, null));

        var sim = new HotelSim(new RoomLedger(defs), staff, RoomRateTable.Default,
                               DemandConfig.Default, startingCash: 2000, rngSeed: 90210);
        sim.FurnishInheritedRooms();
        return sim;
    }
}
