using UnityEngine;

// Story 3.5：全局 tunable SO（跨关卡复用）。
// 字段大多是占位,Story 4-8 才陆续把现有 SerializeField 迁过来。
// 当前 demand loop / FrontDesk2D 等仍读自己的 SerializeField,不强行 migrate。
[CreateAssetMenu(fileName = "BalanceConfig", menuName = "Old Town Hotel/Balance Config", order = 101)]
public sealed class Room2DBalanceConfigSO : ScriptableObject
{
    [Header("Game Clock (day-cycle v2)")]
    [Tooltip("一天(dayStartHour→closeHour)映射的真实秒数")]
    public float dayLengthRealSeconds = 180f;
    public float dayStartHour = 8f;      // 日开始 & 早晨退房潮
    public float openDoorsHour = 10f;    // 开门迎客(CheckInPeak)
    public float stopArrivalsHour = 18f; // 停止新客(Recovery)
    public float closeHour = 22f;        // 打烊自动日结(Ended)

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
