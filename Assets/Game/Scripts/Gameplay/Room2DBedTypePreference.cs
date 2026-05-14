// 客人对床型的偏好（Q2 方案 C 新增）。
// Any = 商务客无约束，Single / Twin / Family 是硬性要求。
// 生成规则：Business → Any 50% / Single 50%；Family → Family 70% / Twin 30%；VIP → Single 60% / Family 40%。
// 配对硬约束：bedTypePreference != Any && bedTypePreference != room.roomCategory → 拒绝。
// 详见 Story 3 设计文档 Q2 段落。
public enum Room2DBedTypePreference
{
    // 无床型约束（Business 客人经济舱场景）。
    Any,

    // 需要单人床房。
    Single,

    // 需要双床房。
    Twin,

    // 需要家庭套房（多床位）。
    Family
}
