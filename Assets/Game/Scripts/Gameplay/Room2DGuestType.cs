// 客人类型（personas）。B+A Hybrid sprint 的"minimal identity"范围：
// 3 种 type × 1 preference，无 value tier、无 ETA、无 hidden comfort vars。
// 视觉抛光（图标 sprite）留给 Story 7 juice 层处理；本 sprint 文本标签即可。
// 详见 ADR 0006 — 3-Phase Day Structure。
public enum Room2DGuestType
{
    // 商务客：单人短住、对环境噪音敏感。
    Business,

    // 家庭客：成员多、对楼层和地面便利度敏感。
    Family,

    // VIP：高期望、容易在等待中变得不耐烦。
    VIP
}
