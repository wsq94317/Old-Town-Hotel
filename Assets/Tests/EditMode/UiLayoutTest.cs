using NUnit.Framework;
using UnityEngine;

// UI 布局契约（B1c）：五区互不重叠、模态一次只开一个、提示条排队不互压。
// 这些不变量决定了"点击会不会落到看不见的面板上"——试玩里表现为
// "速度莫名忽快忽慢"（点 GO 实际点到了压在下面的跳段按钮）。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class UiLayoutTest
    {
        // 手机竖屏的典型虚拟尺寸（GuiScale 把短边固定成 460）
        private const float W = 460f;
        private const float H = 920f;

        [TearDown]
        public void ReleaseModalBetweenTests()
        {
            // 令牌是静态的：不清的话上一条测试的持有者会毒到下一条
            UiLayout.CloseModal(_ownerA);
            UiLayout.CloseModal(_ownerB);
        }

        private static readonly Object _ownerA = new Material(Shader.Find("Sprites/Default"));
        private static readonly Object _ownerB = new Material(Shader.Find("Sprites/Default"));

        // ── 五区互不重叠 ──────────────────────────────────────────────────────

        [Test]
        public void TopBar_And_NotificationRail_DoNotOverlap()
        {
            Rect top = UiLayout.TopBar(W);
            Rect rail = UiLayout.NotificationRail(W, H);

            Assert.That(rail.yMin, Is.GreaterThanOrEqualTo(top.yMax),
                        "通知栏压在顶栏上 → 手机推送会遮住钱和评分");
        }

        [Test]
        public void NotificationRail_StaysAboveTheDrawer()
        {
            Rect rail = UiLayout.NotificationRail(W, H);
            float drawerTop = UiLayout.DrawerTop(H, collapsed: true);

            Assert.That(rail.yMax, Is.LessThanOrEqualTo(drawerTop),
                        "通知栏伸到抽屉里 → 警报的 GO 按钮和操作台按钮抢同一块地方");
        }

        [Test]
        public void NotificationRail_LeavesTheWorldVisibleOnTheLeft()
        {
            // 世界区要留得住：整条屏宽都给 UI 的话玩家没法点场景
            Rect rail = UiLayout.NotificationRail(W, H);
            Assert.That(rail.xMin, Is.GreaterThan(W * 0.5f), "通知栏不该吃掉一半以上的屏宽");
        }

        [Test]
        public void OpenDrawer_LeavesRoomForTheWorld()
        {
            float open = UiLayout.DrawerTop(H, collapsed: false);
            float collapsed = UiLayout.DrawerTop(H, collapsed: true);

            Assert.That(open, Is.LessThan(collapsed), "展开的抽屉应该更高");
            Assert.That(open, Is.GreaterThan(UiLayout.TopBarHeight),
                        "抽屉顶到顶栏底下 → 世界区被挤成零");
        }

        // ── 模态：一次只开一个 ────────────────────────────────────────────────

        [Test]
        public void SecondPanel_CannotOpenWhileTheFirstHoldsTheToken()
        {
            Assert.That(UiLayout.TryOpenModal(_ownerA), Is.True);
            Assert.That(UiLayout.TryOpenModal(_ownerB), Is.False,
                        "两个模态叠着画时点击会落进看不见的那个");
            Assert.That(UiLayout.ModalOpen, Is.True);
        }

        [Test]
        public void SamePanel_CanReopenItsOwnTokenEveryFrame()
        {
            // OnGUI 每帧都会申请一次——同一个持有者必须一直拿得到，
            // 否则面板会隔帧闪烁
            Assert.That(UiLayout.TryOpenModal(_ownerA), Is.True);
            Assert.That(UiLayout.TryOpenModal(_ownerA), Is.True);
            Assert.That(UiLayout.TryOpenModal(_ownerA), Is.True);
        }

        [Test]
        public void ClosingSomebodyElsesModal_DoesNothing()
        {
            // 防串台：B 关不掉 A 的面板，不然 A 画着一半令牌就被抢走
            UiLayout.TryOpenModal(_ownerA);
            UiLayout.CloseModal(_ownerB);

            Assert.That(UiLayout.ModalOpen, Is.True, "只能关自己的模态");
            Assert.That(UiLayout.TryOpenModal(_ownerB), Is.False);
        }

        [Test]
        public void AfterClosing_TheNextPanelGetsItsTurn()
        {
            UiLayout.TryOpenModal(_ownerA);
            UiLayout.CloseModal(_ownerA);

            Assert.That(UiLayout.ModalOpen, Is.False, "关了就该放锁——漏放=面板再也弹不出来");
            Assert.That(UiLayout.TryOpenModal(_ownerB), Is.True);
        }

        [Test]
        public void Modal_SitsInsideTheScreen_AtEveryHeight()
        {
            foreach (float height in new[] { 80f, 130f, 158f, 300f })
            {
                Rect modal = UiLayout.Modal(W, H, height);
                Assert.That(modal.xMin, Is.GreaterThanOrEqualTo(0f), "height=" + height);
                Assert.That(modal.xMax, Is.LessThanOrEqualTo(W), "height=" + height);
                Assert.That(modal.yMin, Is.GreaterThanOrEqualTo(0f), "height=" + height);
                Assert.That(modal.yMax, Is.LessThanOrEqualTo(H), "height=" + height);
            }
        }

        // ── 提示条：同一帧里排队，不互相压 ────────────────────────────────────

        [Test]
        public void TwoToastsInTheSameFrame_StackInsteadOfOverlapping()
        {
            // 六个面板各自画"结果文案"，两条同时出现时后画的会把先画的压掉一半，
            // 玩家读到的是两句话的碎片
            Rect first = UiLayout.NextToast(W, H);
            Rect second = UiLayout.NextToast(W, H);

            Assert.That(second.yMin, Is.GreaterThanOrEqualTo(first.yMax),
                        "第二条提示必须排在第一条下面");
        }

        [Test]
        public void Toasts_StayClearOfTheTopBar_AndTheNotificationRail()
        {
            Rect toast = UiLayout.NextToast(W, H);

            Assert.That(toast.yMin, Is.GreaterThanOrEqualTo(UiLayout.TopBarHeight),
                        "提示压在顶栏上 → 遮住时间和倍速指示");
            Assert.That(toast.xMax, Is.LessThanOrEqualTo(UiLayout.NotificationRail(W, H).xMin + 1f),
                        "提示伸进通知栏 → 和警报卡片重叠");
        }
    }
}
