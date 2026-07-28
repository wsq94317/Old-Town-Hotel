using UnityEngine;

// 模态面板的统一入口（B1c）。
//
// 以前六个决策面板各自硬编码 `h * 0.32f` 之类的位置，两个同时弹出就叠在一起——
// IMGUI 没有 z 序，谁先画谁吃点击，玩家点"赔钱"可能实际点到了下面那个面板的
// "打架"。UiLayout 的模态令牌本来就是为此存在的，但每个面板都要自己记得申请、
// 记得释放，漏一处就是一个"某个面板再也弹不出来"的死锁。
//
// 这里把"申请令牌 + 拿标准矩形 + 登记触摸热区"打包成一次调用：
//
//   if (!GuiModal.Begin(this, w, h, 136f, out Rect box)) return;  // 拿不到令牌就这帧别画
//   GUI.Box(box, "ANGRY GUEST");
//   ...
//   GuiModal.End(this);                                      // 面板关闭时释放
//
// 忘了 End 也不会永久死锁：Begin 的持有者已被销毁时 UiLayout.Sweep 会放锁。
public static class GuiModal
{
    /// <summary>申请画一个模态面板。拿到令牌返回 true 并给出标准矩形（居中、宽 90%）。
    /// 顺便把矩形登记成触摸热区——不登记的话点击会穿到世界里，
    /// 玩家点按钮的同时经理会跑过去（试玩截图抓到过）。</summary>
    public static bool Begin(Object owner, float w, float h, float height, out Rect box)
    {
        box = UiLayout.Modal(w, h, height);

        if (!UiLayout.TryOpenModal(owner)) return false;

        GuiInput.ReserveZone(box);
        return true;
    }

    /// <summary>面板关闭时释放令牌。**每条关闭路径都要调**（选完了、超时了、被打断了）。</summary>
    public static void End(Object owner) => UiLayout.CloseModal(owner);

    /// <summary>模态里第 index 行按钮的矩形（从上往下排，行高统一）。
    /// 每个面板自己算按钮位置的话，行高会各不相同，看着像三套 UI。</summary>
    public static Rect Row(Rect box, int index, float top = 34f,
                           float rowHeight = 26f, float gap = 4f)
    {
        float y = box.y + top + index * (rowHeight + gap);
        return new Rect(box.x + 12f, y, box.width - 24f, rowHeight);
    }
}
