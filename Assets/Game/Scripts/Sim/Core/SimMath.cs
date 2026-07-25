using System;

// Sim 内核的数学工具。
// 铁律：Sim/ 下任何文件都不得 using UnityEngine——包括 Mathf。
// 这样模拟内核可在纯 .NET 下测试/跑批，也不会被 Unity 主线程假设污染。
public static class SimMath
{
    public static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;

    public static float Clamp(float value, float min, float max) =>
        value < min ? min : value > max ? max : value;

    public static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;

    public static float Clamp01(float value) => Clamp(value, 0f, 1f);

    public static double Clamp01(double value) => Clamp(value, 0d, 1d);

    public static int FloorToInt(float value) => (int)Math.Floor(value);

    public static int FloorToInt(double value) => (int)Math.Floor(value);

    public static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    public static int RoundToInt(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
}
