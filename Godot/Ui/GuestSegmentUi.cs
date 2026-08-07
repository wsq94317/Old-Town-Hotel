using Godot;

// 客群 -> 头像 / 文案 / 强调色。
//
// ══ 为什么不能沿用原型那套 ══
//
// 原型用的是 Room2DGuestType（Business / Family / VIP），Sim 的权威类型是
// GuestSegment（Budget / Business / Party / Vip）。这两个**不是错位，是不同的分类法**：
//
//   Room2DGuestType : Business(0)  Family(1)   VIP(2)
//   GuestSegment    : Budget(0)    Business(1) Party(2)  Vip(3)
//
// Family 在 Sim 里根本不存在，Budget 和 Party 在原型里不存在。任何方向的数值强转都是
// 胡说八道，而且是**静默**的胡说——界面照常渲染，只是每个客人的类型都标错。
// （同类问题在房态那边已经踩过一次，见 RoomStateUi.cs。）
//
// 头像方面：仓库里只有按原型三分类做的图。Budget 和 Party 没有专属头像，
// 这里用通用路人图顶着并明确标注——不是"配好了"，是"待补"。
public static class GuestSegmentUi
{
    /// <summary>客群的展示名。用 Sim 的分类，不是原型的。</summary>
    public static string Label(GuestSegment s) => s switch
    {
        GuestSegment.Budget   => "BUDGET",
        GuestSegment.Business => "BUSINESS",
        GuestSegment.Party    => "PARTY",
        GuestSegment.Vip      => "VIP",
        _                     => "?",
    };

    /// <summary>
    /// 头像。
    /// ⚠️ Budget / Party 用的是通用路人图——美术资源是照原型的三分类做的，
    /// 这两个客群当时不存在。要真做的话得补两张图。
    /// </summary>
    public static string PortraitPath(GuestSegment s) => s switch
    {
        GuestSegment.Business => "res://art/guests/guest_business.png",
        GuestSegment.Vip      => "res://art/guests/guest_vip.png",
        GuestSegment.Budget   => "res://art/guests/guest_male.png",     // 占位
        GuestSegment.Party    => "res://art/guests/guest_female.png",   // 占位
        _                     => "res://art/guests/guest_business.png",
    };

    /// <summary>客群色条。仅作区分用，不承载状态含义。</summary>
    public static Color Accent(GuestSegment s) => s switch
    {
        GuestSegment.Budget   => new Color("#8A7E6E"),   // secondaryGrey：低消费、无所谓
        GuestSegment.Business => new Color("#5A7A9E"),   // infoBlue：讲效率
        GuestSegment.Party    => new Color("#B85E3C"),   // brownDeep：热闹、会闯祸
        GuestSegment.Vip      => new Color("#C99A4A"),   // goldAccent：贵客
        _                     => Colors.Magenta,
    };
}
