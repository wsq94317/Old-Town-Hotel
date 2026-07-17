using UnityEngine;

// 楼层数学（v2 世界层唯一的高度换算来源）：层高 4，index 0..6 =
// 1F 大堂+Lounge / 2F 客房 / 3F VIP / 4F 餐厅酒吧 / 5F 健身房 / 6F 赌场 / 7F 屋顶泳池。
// 场景约定：楼层根节点垂直堆叠在 y = index * 4。
public static class FloorMath
{
    public const float FloorHeight = 4f;
    public const int FloorCount = 7;

    public static readonly string[] FloorNames =
    { "Lobby", "Rooms", "VIP Rooms", "Restaurant & Bar", "Gym", "Casino", "Roof Pool" };

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
