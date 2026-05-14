// Story 3 配对硬约束规则 —— Q2 方案 C(完整 RoomCategory 房型系统)。
//
// 设计意图:
//   - 纯静态类,无 MonoBehaviour 依赖 —— EditMode 测试可直接 Assert,
//     不需要 GameObject / AddComponent / scene 加载。
//   - 单一入口 `CanReserve(...)`,所有硬约束规则在此聚合,避免散落在 Controller 内。
//   - 失败 reason 直接渲染到 Prep panel 顶部 "Last Action" 文本框(Q3 方案 A),
//     必须含具体房号 + 客人 bedType 需求,让玩家立刻看懂为什么拒绝。
//
// 调用流:
//   Room2DPreAssignmentController.AttemptReserve(room)
//     → Room2DPreAssignmentRules.CanReserve(room, bedPref, guestType)
//       → ok=true:Controller 调 demandLoop.ReserveRoomForUpcomingDemand(slotIndex, room)
//       → ok=false:Controller 把 reason 推到 Last Action 文本(2s 自动淡出,Coroutine 实现)
//
// 详见 production/iterations/stories/story-03-pre-assignment-tap-pair.md(Q2 段落 + AC3/AC4)。
public static class Room2DPreAssignmentRules
{
    // 检查能否把指定 room 预分配给携带 bedTypePreference 的指定 guestType 客人。
    //
    // 返回:
    //   ok = true  → reason 为空字符串。调用者写入 reservation。
    //   ok = false → reason 含具体房号 + 客人床型需求,直接给 UI 显示。
    //
    // 硬约束规则(按顺序):
    //   1. room == null              → refuse "No room selected"(防御性 guard)
    //   2. bedTypePreference == Any  → accept(Business 经济舱客户无约束)
    //   3. bedTypePreference == room.roomCategory → accept(精确匹配)
    //   4. 否则 → refuse,reason 含 (room.roomName, room.roomCategory, guestType, bedTypePreference)
    public static (bool ok, string reason) CanReserve(
        Room2DEntity room,
        Room2DBedTypePreference bedTypePreference,
        Room2DGuestType guestType)
    {
        // Rule 1:null-room guard。Controller 不应该调进来,但防御性 check 兜底。
        if (room == null)
        {
            return (false, "No room selected");
        }

        // Rule 2:Any = 无约束。Business 客人 50% 概率拿到 Any,任何房型都接受。
        if (bedTypePreference == Room2DBedTypePreference.Any)
        {
            return (true, string.Empty);
        }

        // Rule 3:精确匹配 BedType ↔ RoomCategory(enum 值对齐:Single/Twin/Family)。
        // 注意:Room2DBedTypePreference.Any 已在 Rule 2 拦截,这里只比较 Single/Twin/Family。
        if (MatchesCategory(bedTypePreference, room.roomCategory))
        {
            return (true, string.Empty);
        }

        // Rule 4:不匹配。拼一段 UI 能直接显示的拒绝理由,含具体房号 + 双方类型。
        // 示例:"Room 101 (Single) doesn't match VIP guest needs (Family bed)."
        string reason = "Room " + room.roomName
            + " (" + room.roomCategory + ")"
            + " doesn't match " + guestType + " guest needs"
            + " (" + bedTypePreference + " bed).";
        return (false, reason);
    }

    // 私有 helper:BedTypePreference ↔ RoomCategory 枚举值映射。
    // 当前两个 enum 的 Single/Twin/Family 一一对应,所以直接字符串相等比较即可;
    // 未来若两个枚举出现命名分歧或新加 BedType,改这一处即可。
    private static bool MatchesCategory(
        Room2DBedTypePreference bedTypePreference,
        Room2DRoomCategory roomCategory)
    {
        switch (bedTypePreference)
        {
            case Room2DBedTypePreference.Single: return roomCategory == Room2DRoomCategory.Single;
            case Room2DBedTypePreference.Twin:   return roomCategory == Room2DRoomCategory.Twin;
            case Room2DBedTypePreference.Family: return roomCategory == Room2DRoomCategory.Family;
            default: return false; // Any 在外面已拦截;意外值默认不匹配。
        }
    }
}
