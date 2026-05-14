// 房间的床型分类（Q2 方案 C：引入三档房型硬约束）。
// 用于 Prep 阶段预分配时的硬性匹配：客人 BedTypePreference 必须与房间 RoomCategory 匹配，
// 或客人选择 Any（无约束）才允许配对。
// 详见 ADR 0006 — 3-Phase Day Structure，Story 3 设计决策。
public enum Room2DRoomCategory
{
    // 单人床房：6 间（50%）。商务旅客友好，最常见。
    Single,

    // 双人/双床房：4 间（33%）。适合情侣或家庭轻出行。
    Twin,

    // 家庭套房：2 间（17%）。稀缺资源，VIP / 家庭客人争抢点。
    Family
}
