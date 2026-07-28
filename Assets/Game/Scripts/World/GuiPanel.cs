using System.Collections.Generic;
using UnityEngine;

// 一个会自己算高度的文本面板（临时 OnGUI 期的共用件，M-H 换 UGUI 时退役）。
//
// 以前每个 GUI.Box 的高度都是写死的，而内容行数是变的（条件行到处都是）——
// 结果内容溢出框外，或者被后面的按钮压住（试玩截图里两种都出现了）。
// 攒行再画就不用猜高度了。原型场景与世界场景共用同一份。
public sealed class GuiPanel
{
    private readonly List<string> _lines = new List<string>();

    public const float LineHeight = 19f;
    public const float HeaderHeight = 24f;
    public const float PaddingBottom = 8f;

    public int LineCount => _lines.Count;

    public void Add(string line) => _lines.Add(line);

    /// <summary>条件行的便利写法：条件不成立就不占位置。</summary>
    public void AddIf(bool condition, string line) { if (condition) _lines.Add(line); }

    public void Clear() => _lines.Clear();

    public float Height => HeaderHeight + _lines.Count * LineHeight + PaddingBottom;

    /// <summary>在 (x, y) 画出框与所有行，返回框底部的 y。</summary>
    public float Draw(float x, float y, float width, string title)
    {
        GUI.Box(new Rect(x, y, width, Height), title);
        float lineY = y + HeaderHeight;
        for (int i = 0; i < _lines.Count; i++)
        {
            GUI.Label(new Rect(x + 10, lineY, width - 20, LineHeight), _lines[i]);
            lineY += LineHeight;
        }
        return y + Height;
    }
}
