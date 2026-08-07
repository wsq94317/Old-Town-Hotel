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
    private BottomNav _nav;

    private double _uiAccumulator;
    private const double UiRefreshSeconds = 0.25;   // 与 Unity 的轮询间隔一致

    // 开发用截图钩子：跑 N 秒让模拟推进出有内容的画面，截一张然后退出。
    //   <godot> --path Godot -- --shot=8
    // 目的是让「改了布局之后看一眼」可重复、可自动化。
    private double _shotAfter = -1;
    private bool _shotTaken;

    public override void _Ready()
    {
        _sim = BuildHotel();

        var layer = new CanvasLayer();
        AddChild(layer);

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
        _nav.Select(BottomNav.Tab.Rooms);

        _sim.BeginDay();

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--shot="))
                _shotAfter = arg.Substring(7).ToFloat();
            // 倍速走 SimClock 自己的 SpeedMultiplier：它只改 tick 产出速率，
            // 不改任何游戏时间语义（一天仍是 840 分钟），所以快进出来的状态是真状态。
            else if (arg.StartsWith("--speed="))
                _sim.Clock.SpeedMultiplier = arg.Substring(8).ToFloat();
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
            _rooms.Refresh();
        }

        if (_shotAfter > 0 && !_shotTaken)
        {
            _shotAfter -= delta;
            if (_shotAfter <= 0) { _shotTaken = true; Capture(); }
        }
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

    /// <summary>与 ParityScenario 相同的开局配置，方便把屏幕上的数字和账本对上。</summary>
    private static HotelSim BuildHotel()
    {
        var defs = new List<RoomDefinition>();
        for (int i = 0; i < 12; i++)
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
