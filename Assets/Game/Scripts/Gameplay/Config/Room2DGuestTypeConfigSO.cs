using UnityEngine;

// Story 3.5：GuestType 配置 SO。array per-SO（Q2 默认）——
// 一个 SO 文件含所有 GuestType 的 entry,多关卡平衡时复制 SO + 编辑数值。
// 字段大多是占位,Story 4-8 才填实。
[CreateAssetMenu(fileName = "GuestTypeConfig", menuName = "Old Town Hotel/Guest Type Config", order = 102)]
public sealed class Room2DGuestTypeConfigSO : ScriptableObject
{
    [System.Serializable]
    public class GuestTypeEntry
    {
        public Room2DGuestType guestType = Room2DGuestType.Business;

        [Header("BedType Distribution(Story 3 既有 → 留待 Story 4 顺手迁)")]
        // 各概率值之和应 ≤ 1;runtime 用 UnityEngine.Random.value 与累积阈值比较。
        // 占位默认值匹配 Story 3 hardcoded 分布。
        [Range(0, 1)] public float probAny;
        [Range(0, 1)] public float probSingle;
        [Range(0, 1)] public float probTwin;
        [Range(0, 1)] public float probFamily;

        [Header("Patience / Cost(占位 —— Story 4 / 8 填实)")]
        public float basePatienceSeconds = 30f;
        public int compensationCost = 50;
    }

    public GuestTypeEntry[] entries;
}
