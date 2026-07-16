using UnityEngine;
using Random = System.Random; // 概率注入统一用 System.Random（可测种子）

// M3 打扫瑕疵与验房漏检概率（纯 C#，随机注入）。
// quality 属性 0..100：越高瑕疵/漏检越少（线性插值）。
public static class FlawPolicy
{
    public static float FlawChance(int quality) =>
        Mathf.Lerp(SupervisionTuning.FlawChanceAtZeroQuality,
                   SupervisionTuning.FlawChanceAtFullQuality,
                   Mathf.Clamp01(quality / 100f));

    public static float InspectorMissChance(int quality) =>
        Mathf.Lerp(SupervisionTuning.InspectorMissAtZeroQuality,
                   SupervisionTuning.InspectorMissAtFullQuality,
                   Mathf.Clamp01(quality / 100f));

    public static bool RollFlaw(int quality, Random rng) =>
        rng.NextDouble() < FlawChance(quality);

    public static bool RollInspectorMiss(int quality, Random rng) =>
        rng.NextDouble() < InspectorMissChance(quality);
}
