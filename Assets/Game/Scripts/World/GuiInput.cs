using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// OnGUI 面板的触屏输入桥：纯新 Input System 下 IMGUI 收不到（模拟）触摸，
// 点击由 WorldInputController 捕获后经 PublishTap 转发到这里；
// 面板按钮用 GuiInput.Button —— IMGUI 原生命中（编辑器鼠标）与触点命中（真机）双通道。
// 坐标系：发布用屏幕坐标（Input System，原点左下），内部转虚拟 GUI 坐标（原点左上，除以 GuiScale.Factor）。
public static class GuiInput
{
    private static Vector2 _tapVirtual;
    private static int _tapFrame = -1;
    private static bool _consumed;

    private static readonly List<Rect> _zones = new List<Rect>();
    private static int _zoneFrame = -1;

    /// <summary>WorldInputController 在面板打开/点中保留区时调用，转发这次点击。</summary>
    public static void PublishTap(Vector2 screenPos)
    {
        // 换算必须与 GuiScale.Begin() 的矩阵互逆（含安全区平移），否则命中整体偏移
        _tapVirtual = GuiScale.ScreenToVirtual(screenPos);
        _tapFrame = Time.frameCount;
        _consumed = false;
    }

    /// <summary>消费诊断（探针）。</summary>
    public static string ConsumeDebug = "none";

    private static int _selfPollFrame = -1;
    private static bool _wasPressed;

    /// <summary>**自取通道**：没有 WorldInputController 转发点击的场景（如 v3 调参原型）
    /// 每帧调一次，自己从 Input System 读按下沿并发布。
    ///
    /// 为什么需要：纯新 Input System 下 IMGUI 完全收不到触摸，`GUI.Button` 在真机/
    /// Device Simulator 上是聋的——只有编辑器鼠标能点。原型场景没有巡查层的输入控制器，
    /// 于是这里补一条最小的自取路径，避免为了几个调试按钮把整套世界输入搬进来。
    /// 一帧只处理一次（同一帧被多个面板调用不会重复发布）。</summary>
    public static void PollSelfServed()
    {
        if (_selfPollFrame == Time.frameCount) return;
        _selfPollFrame = Time.frameCount;

        bool pressed = false;
        Vector2 pos = Vector2.zero;

        // 触屏优先：Device Simulator 与真机走这条
        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            pressed = true;
            pos = touch.primaryTouch.position.ReadValue();
        }
        else
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                pressed = true;
                pos = mouse.position.ReadValue();
            }
        }

        // 只在按下沿发布：按住不动不该每帧重复触发按钮
        if (pressed && !_wasPressed) PublishTap(pos);
        _wasPressed = pressed;
    }

    private static bool ConsumeTapIn(Rect r)
    {
        // 有效期 2 帧：发布可能发生在帧边界（Update 末/外部注入），下一帧 OnGUI 才轮到消费。
        if (_consumed || Time.frameCount - _tapFrame > 1 || _tapFrame < 0) return false;
        if (!r.Contains(_tapVirtual))
        {
            ConsumeDebug = $"miss: tapV={_tapVirtual:F0} rect={r}";
            return false;
        }
        _consumed = true;
        ConsumeDebug = $"CONSUMED at {_tapVirtual:F0} by rect={r}";
        return true;
    }

    /// <summary>双通道按钮：IMGUI 原生点击 或 转发触点落在矩形内。尊重 GUI.enabled。</summary>
    public static bool Button(Rect r, string label)
    {
        bool native = GUI.Button(r, label);
        if (!GUI.enabled) return false;
        return native || ConsumeTapIn(r);
    }

    /// <summary>面板未打开时也要吃点击的常驻按钮（如 HIRE）每帧登记热区。</summary>
    public static void ReserveZone(Rect r)
    {
        if (_zoneFrame != Time.frameCount)
        {
            _zones.Clear();
            _zoneFrame = Time.frameCount;
        }
        _zones.Add(r);
    }

    /// <summary>屏幕坐标是否落在（上一帧登记的）常驻热区内。</summary>
    public static bool IsInReservedZone(Vector2 screenPos)
    {
        Vector2 v = GuiScale.ScreenToVirtual(screenPos);
        for (int i = 0; i < _zones.Count; i++)
        {
            if (_zones[i].Contains(v)) return true;
        }
        return false;
    }
}
