using System.Collections.Generic;
using UnityEngine;

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
        float f = GuiScale.Factor;
        _tapVirtual = new Vector2(screenPos.x / f, (Screen.height - screenPos.y) / f);
        _tapFrame = Time.frameCount;
        _consumed = false;
    }

    /// <summary>消费诊断（探针）。</summary>
    public static string ConsumeDebug = "none";

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
        float f = GuiScale.Factor;
        var v = new Vector2(screenPos.x / f, (Screen.height - screenPos.y) / f);
        for (int i = 0; i < _zones.Count; i++)
        {
            if (_zones[i].Contains(v)) return true;
        }
        return false;
    }
}
