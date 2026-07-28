using UnityEngine;

// **UI 布局契约**（OnGUI 时代的设计语言，M-H 换 UGUI 时原样翻译成 RectTransform 锚点）。
//
// 背景：世界场景里有 14 个 OnGUI 画手各画各的，没有任何协调——手机通知、电梯面板、
// 质询面板、图例、调试行全叠在一起（试玩截图惨不忍睹）。更糟的是 IMGUI 没有 z 序，
// 谁先画谁吃点击：玩家点"GO"可能实际触发了压在下面的"跳到下一时段"，
// 表现为"速度莫名忽快忽慢"。
//
// 契约把竖屏切成五个**互不重叠**的区域，所有面板只能往自己的区域里画：
//
//   ┌─────────────────────────┐
//   │ TopBar   顶栏（常驻三行）│  ← 钱/评分/时间 + 倍速指示，永远可见
//   ├─────────────────────────┤
//   │          │ Notification │  ← 通知右栏：手机推送、警报，只在这一条竖带里堆叠
//   │  World   │     Rail     │
//   │  世界区  ├──────────────┤
//   │ （点击   │              │
//   │  给场景）│              │
//   ├─────────────────────────┤
//   │ Drawer   底部抽屉        │  ← 经营操作台（可折叠）
//   └─────────────────────────┘
//   Modal 模态区：屏幕中央，**一次只允许一个**（TryOpenModal 令牌）。
//
// 三条规则：
//   1. 常驻面板只能在自己的区域里画（越界=重叠=误触）。
//   2. 模态面板必须先 TryOpenModal 拿到令牌，画完调 CloseModal——拿不到就别画。
//   3. 世界区永远留空给场景点击；谁都不许在那里放常驻按钮。
public static class UiLayout
{
    // 全部为虚拟坐标（GuiScale.Begin 之后的坐标系，短边恒 460）

    public const float TopBarHeight = 68f;

    /// <summary>底部抽屉占屏比例（WorldOperationsPanel 用）。</summary>
    public const float DrawerShare = 0.46f;

    /// <summary>顶栏区。</summary>
    public static Rect TopBar(float w) => new Rect(6f, 6f, w - 12f, TopBarHeight - 6f);

    /// <summary>通知右栏：顶栏之下、抽屉之上的右侧竖带。手机推送/警报只许在这里堆。</summary>
    public static Rect NotificationRail(float w, float h)
    {
        float top = TopBarHeight + 6f;
        float bottom = DrawerTop(h, collapsed: true) - 6f;
        float width = 200f;
        return new Rect(w - width - 6f, top, width, bottom - top);
    }

    /// <summary>抽屉顶边 y。</summary>
    public static float DrawerTop(float h, bool collapsed) =>
        collapsed ? h - 34f : h * (1f - DrawerShare);

    /// <summary>模态面板的标准矩形：屏幕中央，宽 90%。</summary>
    public static Rect Modal(float w, float h, float height)
    {
        float width = w * 0.9f;
        return new Rect((w - width) / 2f, (h - height) / 2.4f, width, height);
    }

    // ── 模态令牌：一次只开一个 ────────────────────────────────────────────────
    // IMGUI 没有 z 序，两个模态叠着画时点击会落进看不见的那个——所以从源头禁掉。

    private static Object _modalOwner;

    /// <summary>申请打开模态。已有别人开着就返回 false，调用方**这帧别画**。</summary>
    public static bool TryOpenModal(Object owner)
    {
        if (_modalOwner != null && _modalOwner != owner) return false;
        _modalOwner = owner;
        return true;
    }

    /// <summary>关掉自己的模态（只能关自己的）。</summary>
    public static void CloseModal(Object owner)
    {
        if (_modalOwner == owner) _modalOwner = null;
    }

    /// <summary>当前有没有模态开着（WorldInputController 判定点击去向时用）。</summary>
    public static bool ModalOpen => _modalOwner != null;

    /// <summary>场景切换/域重载后的兜底：持有者死了就放锁。</summary>
    public static void Sweep()
    {
        if (_modalOwner == null) return;
        // UnityEngine.Object 的“假 null”：Destroy 后引用非空但 == null 为真
        if (_modalOwner == null || !_modalOwner) _modalOwner = null;
    }
}
