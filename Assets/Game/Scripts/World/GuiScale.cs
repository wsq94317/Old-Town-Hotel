using UnityEngine;

// 临时 OnGUI 面板的全局缩放 + 安全区内缩：高 DPI 手机竖屏下固定像素 UI 会小到看不清，
// 而刘海/挖孔/圆角会吃掉屏幕边缘——顶栏第一行文字正好落在刘海底下（用户实测）。
//
// 每个 OnGUI 开头调用 Begin()：设置"缩放 + 平移到安全区左上角"的矩阵，
// 并返回**安全区内**的虚拟尺寸。布局代码照旧从 (0,0) 写起，自动落在安全区里，
// 不必每处手动加 inset。
// （Unity 在每个 OnGUI 回调开始时重置 GUI.matrix，无需手动还原。）
public static class GuiScale
{
    // 参考短边（虚拟单位）：手机竖屏宽 ~1170px 时约放大 2.5 倍
    public const float ReferenceShortSide = 460f;

    public static float Factor =>
        Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / ReferenceShortSide);

    /// <summary>安全区（屏幕像素，原点左下）。取不到时退回整屏。</summary>
    private static Rect SafeAreaPixels
    {
        get
        {
            Rect safe = Screen.safeArea;
            // 部分编辑器/平台下 safeArea 会返回零矩形，那时按整屏算，别把 UI 缩成一条
            if (safe.width <= 1f || safe.height <= 1f)
                return new Rect(0f, 0f, Screen.width, Screen.height);
            return safe;
        }
    }

    /// <summary>安全区左上角在屏幕上的偏移（像素）：x 向右，y 从屏幕顶部往下。</summary>
    public static Vector2 SafeOriginPixels
    {
        get
        {
            Rect safe = SafeAreaPixels;
            return new Vector2(safe.x, Screen.height - safe.yMax);
        }
    }

    /// <summary>设置缩放+安全区矩阵，返回安全区内的虚拟尺寸 (w, h)。</summary>
    public static Vector2 Begin()
    {
        float f = Factor;
        Rect safe = SafeAreaPixels;
        Vector2 origin = SafeOriginPixels;

        // 屏幕坐标 = 平移(安全区左上) × 缩放(f) × 虚拟坐标
        GUI.matrix = Matrix4x4.Translate(new Vector3(origin.x, origin.y, 0f))
                   * Matrix4x4.Scale(new Vector3(f, f, 1f));

        return new Vector2(safe.width / f, safe.height / f);
    }

    /// <summary>屏幕坐标（原点左下）→ 虚拟 GUI 坐标（原点=安全区左上）。
    /// **必须与 Begin() 的矩阵严格互逆**，否则触点命中会整体偏一个刘海的高度。</summary>
    public static Vector2 ScreenToVirtual(Vector2 screenPos)
    {
        float f = Factor;
        Vector2 origin = SafeOriginPixels;
        return new Vector2((screenPos.x - origin.x) / f,
                           (Screen.height - screenPos.y - origin.y) / f);
    }
}
