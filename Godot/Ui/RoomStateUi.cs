using Godot;

// 房态 -> 颜色/文字。Unity 侧 RoomStateUiMap 的移植版，但**换了输入类型**。
//
// ══ 为什么不照搬 ══
//
// Unity 的 RoomStateUiMap 吃的是 Room2DState（6 个成员），而 Sim 权威类型是
// RoomSimState（7 个成员，Ruined 插在序号 0）。两者每个共有成员都错位 +1：
//
//   RoomSimState.Ruined(0)  -(int)->  Room2DState.Dirty
//   RoomSimState.Dirty(1)   -(int)->  Room2DState.Cleaning
//   ...
//   RoomSimState.Blocked(6) -(int)->  越界
//
// 也就是说数值转换对 7 个值全错，其中 6 个**不报错**——只是每间房显示错误的颜色和标签。
// 这种 bug 上线后极难发现：界面看起来完全正常，只是在说谎。
//
// 现成的 RoomStateMapping.ToLegacy 也不能用：它的 default 分支把 Blocked 和 Ruined
// 一起吞成 Blocked（源码注释自己写了「镜像期用」）。而 HotelSim 的注释说**开局大部分
// 房间是 Ruined**——走那条路的话，玩家第一眼看到的整屏房间会全被标成「装修中」。
//
// 所以这里直接对 RoomSimState 做七分支穷举，不经过任何中间类型。
public static class RoomStateUi
{
    // Ruined 在 Unity 的 UITheme 里没有颜色——那边只有 6 个房态色。
    // #4A3B33 / "DERELICT" 是本次移植新增的设计决策：比 Occupied 的 #6E4E3A 更暗更沉，
    // 读作「还没启用的死资产」，和 Blocked 的中性灰 #6B6B6B 明确区分开。
    public static readonly Color Ready    = new Color("#6A9F5C");
    public static readonly Color Dirty    = new Color("#B85842");
    public static readonly Color Cleaning = new Color("#4A6FA5");
    public static readonly Color Insp     = new Color("#84598E");
    public static readonly Color Occupied = new Color("#6E4E3A");
    public static readonly Color Blocked  = new Color("#6B6B6B");
    public static readonly Color Ruined   = new Color("#4A3B33");

    /// <summary>徽章底色 + 徽章文字。未知值故意返回洋红 + "?"，让漏掉的枚举成员刺眼。</summary>
    public static (Color Colour, string Label) For(RoomSimState state) => state switch
    {
        RoomSimState.Ruined             => (Ruined,   "DERELICT"),
        RoomSimState.Dirty              => (Dirty,    "DIRTY"),
        RoomSimState.Cleaning           => (Cleaning, "CLEAN"),
        RoomSimState.AwaitingInspection => (Insp,     "INSP"),
        RoomSimState.Ready              => (Ready,    "READY"),
        RoomSimState.Occupied           => (Occupied, "OCC"),
        RoomSimState.Blocked            => (Blocked,  "BLOCK"),
        _                               => (Colors.Magenta, "?"),
    };

    /// <summary>
    /// 格子底图的淡化色。Unity 是 Color.Lerp(white, state, 0.25f) —— 是**淡洗**不是满色。
    /// 刻意在运行时算而不是写死十六进制：其中两个通道的结果正好落在 .5 的取整边界上
    /// （Cleaning 的 B=232.5、Insp 的 G=213.5），手写的话取整方式一变就差一档。
    /// </summary>
    public static Color Wash(Color stateColour) => Colors.White.Lerp(stateColour, 0.25f);

    /// <summary>床型首字母。运行时取枚举名首字母，与 Unity 的 ExtractBedLetter 一致。</summary>
    public static string BedLetter(Room2DRoomCategory category)
    {
        string name = category.ToString();
        return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
    }

    /// <summary>房型内景图。四类都有专图——侦察一度报告 King 没有分支，实为误读。</summary>
    public static string InteriorPath(Room2DRoomCategory category) => category switch
    {
        Room2DRoomCategory.Single => "res://art/rooms/room_single.png",
        Room2DRoomCategory.Twin   => "res://art/rooms/room_twin.png",
        Room2DRoomCategory.Family => "res://art/rooms/room_family.png",
        _                         => "res://art/rooms/room_king.png",
    };
}
