using System.Collections.Generic;
using UnityEngine;

// 跨层导航：纯逻辑的逐层 hop 规划 + 场景侧楼梯点注册表。
// 员工/客人跨层 = 走到本层楼梯点 → Warp 到相邻层楼梯出口 → 重复。
// 楼梯点与 StairZone 同源坐标（pad x8.5 z0 / exit x6.5 z0，每层）。
public static class FloorNavigator
{
    /// <summary>逐层 hop 列表：(fromFloor, toFloor) 逐相邻层；同层返回空表。</summary>
    public static List<(int from, int to)> PlanHops(int fromFloor, int toFloor)
    {
        var hops = new List<(int, int)>();
        int step = toFloor > fromFloor ? 1 : -1;
        for (int f = fromFloor; f != toFloor; f += step)
        {
            hops.Add((f, f + step));
        }
        return hops;
    }

    // ── 场景楼梯点注册表（世界坐标，场景接线时注入） ────────────────────────
    private static readonly Vector3[] _stairPad = new Vector3[FloorMath.FloorCount];
    private static readonly Vector3[] _stairExit = new Vector3[FloorMath.FloorCount];
    private static bool _registered;

    /// <summary>场景接线：注册每层的楼梯 pad（走向目标）与到达出口点。</summary>
    public static void RegisterStairs(Vector3[] pads, Vector3[] exits)
    {
        for (int i = 0; i < FloorMath.FloorCount; i++)
        {
            _stairPad[i] = pads[i];
            _stairExit[i] = exits[i];
        }
        _registered = true;
    }

    public static bool StairsRegistered => _registered;

    /// <summary>某层的楼梯 pad 位置（agent 走向它以离开该层）。</summary>
    public static Vector3 StairPadOf(int floor) => _stairPad[Mathf.Clamp(floor, 0, FloorMath.FloorCount - 1)];

    /// <summary>某层的楼梯出口位置（agent 从相邻层到达时落点）。</summary>
    public static Vector3 StairExitOf(int floor) => _stairExit[Mathf.Clamp(floor, 0, FloorMath.FloorCount - 1)];
}
