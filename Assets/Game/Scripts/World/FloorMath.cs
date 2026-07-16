using UnityEngine;

// 楼层数学（v2 世界层唯一的高度换算来源）：层高 4，楼层 index 0/1/2 = 1F/2F/3F。
// 场景约定：三个楼层根节点垂直堆叠在 y = 0 / 4 / 8。
public static class FloorMath
{
    public const float FloorHeight = 4f;
    public const int FloorCount = 3;

    /// <summary>世界 y 坐标 → 楼层 index（钳制 0..FloorCount-1）。</summary>
    public static int FloorIndexForY(float y)
    {
        int index = Mathf.FloorToInt(y / FloorHeight);
        return Mathf.Clamp(index, 0, FloorCount - 1);
    }

    /// <summary>楼层 index → 该层地板基准 y（index 越界时钳制）。</summary>
    public static float BaseYFor(int floorIndex) =>
        Mathf.Clamp(floorIndex, 0, FloorCount - 1) * FloorHeight;
}
