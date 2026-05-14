using UnityEngine;

// Story 3.5：全局 tunable SO（跨关卡复用）。
// 字段大多是占位,Story 4-8 才陆续把现有 SerializeField 迁过来。
// 当前 demand loop / FrontDesk2D 等仍读自己的 SerializeField,不强行 migrate。
[CreateAssetMenu(fileName = "BalanceConfig", menuName = "Old Town Hotel/Balance Config", order = 101)]
public sealed class Room2DBalanceConfigSO : ScriptableObject
{
    [Header("Day Length(占位 —— Story 6 填实)")]
    public float prepPhaseSeconds = 45f;
    public float peakPhaseSeconds = 120f;
    public float recoveryPhaseSeconds = 45f;

    [Header("Demand Loop")]
    public int upcomingQueueCapacity = 2;
    public int guestsPerDay = 5;

    [Header("Front Desk Patience(占位 —— Story 4)")]
    public float impatientThresholdSeconds = 12f;
    public float criticalThresholdSeconds = 24f;

    [Header("Scoring Weights(占位 —— Story 4)")]
    public float weightStay = 1f;
    public float weightCleanliness = 1f;
    public float weightFacility = 1f;
    public float weightService = 1f;
}
