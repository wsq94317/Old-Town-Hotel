using UnityEngine;

// 临时 OnGUI 面板的全局缩放：高 DPI 手机竖屏下固定像素 UI 会小到看不清。
// 每个 OnGUI 开头调用 Begin()：设置缩放矩阵并返回"虚拟屏幕"尺寸，
// 布局代码照旧用虚拟坐标写，字体/按钮/点击命中全部随矩阵等比放大。
// （Unity 在每个 OnGUI 回调开始时重置 GUI.matrix，无需手动还原。）
public static class GuiScale
{
    // 参考短边（虚拟单位）：手机竖屏宽 ~1170px 时约放大 2.5 倍
    public const float ReferenceShortSide = 460f;

    public static float Factor =>
        Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / ReferenceShortSide);

    /// <summary>设置缩放矩阵，返回虚拟屏幕尺寸 (w, h)。</summary>
    public static Vector2 Begin()
    {
        float f = Factor;
        GUI.matrix = Matrix4x4.Scale(new Vector3(f, f, 1f));
        return new Vector2(Screen.width / f, Screen.height / f);
    }
}
