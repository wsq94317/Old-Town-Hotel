using UnityEngine;

// 单个房间内部属性。
// [System.Serializable] 让这个普通 C# 类可以显示在 Unity Inspector 里。
[System.Serializable]
public class Room2DAttribute
{
    // 属性类型，例如床、地板、衣柜。
    public Room2DAttributeType type;

    // 0-100 的状态数值。越高代表越好。
    [Range(0, 100)] public int condition = 100;

    // 给人看的备注，后续可用于 UI 或调试。
    public string note;

    // 目前先用 50 作为原型阶段的问题阈值。
    public bool HasProblem()
    {
        return condition < 50;
    }

    // 给 UI 或调试文字使用的简短显示文本。
    public string GetDisplayName()
    {
        return type + ": " + condition;
    }
}
