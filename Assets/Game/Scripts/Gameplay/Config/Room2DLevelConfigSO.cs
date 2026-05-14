using UnityEngine;

// Story 3.5：关卡级配置 SO。一个"关卡"由一个 LevelConfig 完全描述,
// 切关卡 = 换一个 LevelConfigSO 引用。
// 字段与 Room2DPrototypeRoomConfigApplier 既有 schema 兼容(向后兼容)。
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Old Town Hotel/Level Config", order = 100)]
public sealed class Room2DLevelConfigSO : ScriptableObject
{
    [Header("Level Identity")]
    public string levelName = "Showcase";
    [TextArea] public string levelDescription;

    [Header("Floor Plan(单一权威源 — Room2DSceneSetup 读取)")]
    // 数组 index = floor - 1,值 = 该层房间数。例如 [6,4,2] 表示 12 房按 6/4/2 拆到 floor 1/2/3。
    // Room2DSceneSetup 在 Play 时按顺序分配房间到各楼层并独立编号:
    //   floor 1 → 101..106(6 房,匹配 Rule 0 Single)
    //   floor 2 → 201..204(4 房,匹配 Rule 1 Twin)
    //   floor 3 → 301..302(2 房,匹配 Rule 2 Family)
    public int[] roomsPerFloor = new int[] { 6, 4, 2 };

    [Header("Room Layout Rules")]
    // 与 Room2DPrototypeRoomConfigApplier.cs 顶层 [System.Serializable]
    // 类型 Room2DPrototypeRoomConfigRule 完全兼容(Story 3 已扩展含 roomCategory)。
    public Room2DPrototypeRoomConfigRule[] roomRules;

    [Header("Default Config(没命中 rule 时用)")]
    public Room2DPrototypeRoomType defaultRoomType = Room2DPrototypeRoomType.Standard;
    public Room2DPrototypeFacing defaultFacing = Room2DPrototypeFacing.StreetFacing;
    public Room2DRoomCategory defaultRoomCategory = Room2DRoomCategory.Single;
    public bool replaceDefaultAttributes;
    public Room2DPrototypeRoomAttributeConfig[] defaultAttributes;

    [Header("Balance + Guest References")]
    // 顶层入口(层次型组织):Level 引用 Balance + GuestType 配置。
    // null 允许 —— Story 3.5 阶段这两份 SO 是占位,Story 4-8 填实。
    public Room2DBalanceConfigSO balanceConfig;
    public Room2DGuestTypeConfigSO guestTypeConfig;
}
