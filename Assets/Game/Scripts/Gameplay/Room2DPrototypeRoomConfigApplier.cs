using UnityEngine;

[System.Serializable]
public class Room2DPrototypeRoomAttributeConfig
{
    public Room2DAttributeType type;
    [Range(0, 100)] public int condition = 100;
    public string note;
}

[System.Serializable]
public class Room2DPrototypeRoomConfigRule
{
    public string ruleName = "Room Config Rule";

    [Header("Match Rooms")]
    public int floorNumber = 1;
    public int roomNumberStart = 101;
    public int roomNumberEnd = 199;

    [Header("Apply Config")]
    public Room2DPrototypeRoomType roomType = Room2DPrototypeRoomType.Standard;
    public Room2DPrototypeFacing facing = Room2DPrototypeFacing.StreetFacing;
    // Story 3 Q2 方案 C：床型分类（Single/Twin/Family）。预分配硬约束用。
    public Room2DRoomCategory roomCategory = Room2DRoomCategory.Single;

    [Header("Optional Attributes")]
    public bool replaceRoomAttributes;
    public Room2DPrototypeRoomAttributeConfig[] roomAttributes;
}

// 场景级房间配置器。
// 用来批量给很多房间写入房型、朝向、房间属性，避免每个 Prefab 实例都手动改。
public class Room2DPrototypeRoomConfigApplier : MonoBehaviour
{
    [Header("Rooms")]
    // 原型阶段建议打开：复制房间后可以自动重新查找。
    public bool autoFindRooms = true;
    public Room2DEntity[] rooms;

    [Header("ScriptableObject Source(可选,设了就优先于 inline 规则)")]
    [Tooltip("若设置了 LevelConfig,Apply Rules 时会从 SO 读 rules + defaults;否则走 inline 路径(Story 3 收尾的既有行为)。")]
    [SerializeField] private Room2DLevelConfigSO levelConfig;

    [Header("Default Config")]
    // 没有命中规则的房间会使用这里的默认配置。
    public Room2DPrototypeRoomType defaultRoomType = Room2DPrototypeRoomType.Standard;
    public Room2DPrototypeFacing defaultFacing = Room2DPrototypeFacing.StreetFacing;
    // Story 3：默认床型分类。规则没匹配上时所有房都用这个值。
    public Room2DRoomCategory defaultRoomCategory = Room2DRoomCategory.Single;
    public bool replaceDefaultAttributes;
    public Room2DPrototypeRoomAttributeConfig[] defaultAttributes;

    [Header("Rules")]
    // 规则按顺序应用；后面的规则可以覆盖前面的结果。
    public Room2DPrototypeRoomConfigRule[] roomRules;

    [Header("Debug Result")]
    public int lastFoundRoomCount;
    public int lastConfiguredRoomCount;
    public string lastApplyResult = "None";

    [ContextMenu("Find Rooms In Scene")]
    public void FindRoomsInScene()
    {
        rooms = FindObjectsByType<Room2DEntity>(FindObjectsSortMode.None);
        SortRoomsByFloorAndNumber();

        lastFoundRoomCount = rooms != null ? rooms.Length : 0;
        lastApplyResult = "Found " + lastFoundRoomCount + " rooms.";
    }

    [ContextMenu("Apply Defaults To All Rooms")]
    public void ApplyDefaultsToAllRooms()
    {
        FindRoomsIfNeeded();

        // Story 3.5：选 SO 还是 inline 作为权威源。
        GetEffectiveConfig(
            out _,
            out Room2DPrototypeRoomType effectiveDefaultRoomType,
            out Room2DPrototypeFacing effectiveDefaultFacing,
            out Room2DRoomCategory effectiveDefaultRoomCategory,
            out bool effectiveReplaceDefaultAttributes,
            out Room2DPrototypeRoomAttributeConfig[] effectiveDefaultAttributes);

        int changedCount = 0;
        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null)
            {
                continue;
            }

            ApplyBaseConfig(room, effectiveDefaultRoomType, effectiveDefaultFacing, effectiveDefaultRoomCategory);

            if (effectiveReplaceDefaultAttributes)
            {
                ApplyAttributes(room, effectiveDefaultAttributes);
            }

            changedCount++;
        }

        lastConfiguredRoomCount = changedCount;
        lastApplyResult = "Applied defaults to " + changedCount + " rooms.";
        RefreshRelatedPrototypeViews();
    }

    [ContextMenu("Apply Rules To Rooms")]
    public void ApplyRulesToRooms()
    {
        FindRoomsIfNeeded();

        // Story 3.5：选 SO 还是 inline 作为权威源。
        // SO 优先(ADR 0005 SSoT —— SO 一旦设置即唯一源,不混用)。
        GetEffectiveConfig(
            out Room2DPrototypeRoomConfigRule[] effectiveRules,
            out Room2DPrototypeRoomType effectiveDefaultRoomType,
            out Room2DPrototypeFacing effectiveDefaultFacing,
            out Room2DRoomCategory effectiveDefaultRoomCategory,
            out bool effectiveReplaceDefaultAttributes,
            out Room2DPrototypeRoomAttributeConfig[] effectiveDefaultAttributes);

        int changedCount = 0;
        for (int i = 0; i < rooms.Length; i++)
        {
            Room2DEntity room = rooms[i];
            if (room == null)
            {
                continue;
            }

            // 先写默认值，保证没有命中规则的房间也有统一配置。
            ApplyBaseConfig(room, effectiveDefaultRoomType, effectiveDefaultFacing, effectiveDefaultRoomCategory);

            if (effectiveReplaceDefaultAttributes)
            {
                ApplyAttributes(room, effectiveDefaultAttributes);
            }

            if (effectiveRules == null)
            {
                changedCount++;
                continue;
            }

            for (int ruleIndex = 0; ruleIndex < effectiveRules.Length; ruleIndex++)
            {
                Room2DPrototypeRoomConfigRule rule = effectiveRules[ruleIndex];
                if (rule == null || !DoesRuleMatchRoom(rule, room))
                {
                    continue;
                }

                ApplyBaseConfig(room, rule.roomType, rule.facing, rule.roomCategory);

                if (rule.replaceRoomAttributes)
                {
                    ApplyAttributes(room, rule.roomAttributes);
                }
            }

            changedCount++;
        }

        lastConfiguredRoomCount = changedCount;
        string source = levelConfig != null
            ? "Applied rules to " + changedCount + " rooms (from LevelConfig: " + levelConfig.levelName + ")"
            : "Applied rules to " + changedCount + " rooms (inline)";
        lastApplyResult = source;
        RefreshRelatedPrototypeViews();
    }

    // Story 3.5：选 SO 还是 inline 作为权威源。
    // SO 优先(ADR 0005 SSoT —— SO 一旦设置即唯一源,不混用)。
    private void GetEffectiveConfig(
        out Room2DPrototypeRoomConfigRule[] effectiveRules,
        out Room2DPrototypeRoomType effectiveDefaultRoomType,
        out Room2DPrototypeFacing effectiveDefaultFacing,
        out Room2DRoomCategory effectiveDefaultRoomCategory,
        out bool effectiveReplaceDefaultAttributes,
        out Room2DPrototypeRoomAttributeConfig[] effectiveDefaultAttributes)
    {
        if (levelConfig != null)
        {
            effectiveRules = levelConfig.roomRules;
            effectiveDefaultRoomType = levelConfig.defaultRoomType;
            effectiveDefaultFacing = levelConfig.defaultFacing;
            effectiveDefaultRoomCategory = levelConfig.defaultRoomCategory;
            effectiveReplaceDefaultAttributes = levelConfig.replaceDefaultAttributes;
            effectiveDefaultAttributes = levelConfig.defaultAttributes;
        }
        else
        {
            effectiveRules = roomRules;
            effectiveDefaultRoomType = defaultRoomType;
            effectiveDefaultFacing = defaultFacing;
            effectiveDefaultRoomCategory = defaultRoomCategory;
            effectiveReplaceDefaultAttributes = replaceDefaultAttributes;
            effectiveDefaultAttributes = defaultAttributes;
        }
    }

    private void FindRoomsIfNeeded()
    {
        if (autoFindRooms || rooms == null || rooms.Length == 0)
        {
            FindRoomsInScene();
        }
    }

    private bool DoesRuleMatchRoom(Room2DPrototypeRoomConfigRule rule, Room2DEntity room)
    {
        return room.floorNumber == rule.floorNumber
            && room.roomNumber >= rule.roomNumberStart
            && room.roomNumber <= rule.roomNumberEnd;
    }

    private void ApplyBaseConfig(Room2DEntity room, Room2DPrototypeRoomType roomType, Room2DPrototypeFacing facing, Room2DRoomCategory roomCategory)
    {
        room.prototypeRoomType = roomType;
        room.prototypeFacing = facing;
        // Story 3：床型分类一并批量写入。Inspector ContextMenu "Apply Rules To Rooms" 触发后生效。
        room.roomCategory = roomCategory;
    }

    private void ApplyAttributes(Room2DEntity room, Room2DPrototypeRoomAttributeConfig[] attributeConfigs)
    {
        if (attributeConfigs == null || attributeConfigs.Length == 0)
        {
            room.roomAttributes = new Room2DAttribute[0];
            return;
        }

        room.roomAttributes = new Room2DAttribute[attributeConfigs.Length];
        for (int i = 0; i < attributeConfigs.Length; i++)
        {
            Room2DPrototypeRoomAttributeConfig config = attributeConfigs[i];
            if (config == null)
            {
                room.roomAttributes[i] = new Room2DAttribute();
                continue;
            }

            room.roomAttributes[i] = new Room2DAttribute
            {
                type = config.type,
                condition = config.condition,
                note = config.note
            };
        }
    }

    private void RefreshRelatedPrototypeViews()
    {
        Room2DController[] controllers = FindObjectsByType<Room2DController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                controllers[i].ApplyStateVisual();
            }
        }

        Room2DOverview overview = FindFirstObjectByType<Room2DOverview>();
        if (overview != null)
        {
            overview.RefreshSummary();
        }
    }

    private void SortRoomsByFloorAndNumber()
    {
        if (rooms == null)
        {
            return;
        }

        System.Array.Sort(rooms, CompareRooms);
    }

    private int CompareRooms(Room2DEntity left, Room2DEntity right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int floorCompare = left.floorNumber.CompareTo(right.floorNumber);
        if (floorCompare != 0)
        {
            return floorCompare;
        }

        return left.roomNumber.CompareTo(right.roomNumber);
    }
}
