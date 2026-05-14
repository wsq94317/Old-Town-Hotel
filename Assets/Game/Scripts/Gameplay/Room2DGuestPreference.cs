// 客人的单一软偏好。本 sprint 只承载"匹配 / 不匹配"二态信息，
// 不参与满意度数学（那是 Story 4 的 scope）。
// 偏好的展示位置：upcoming-guest 卡（Prep 阶段）+ Front Desk 等候卡（Peak 阶段）。
// 详见 ADR 0006 — 3-Phase Day Structure。
public enum Room2DGuestPreference
{
    // 偏好安静楼层。
    QuietFloor,

    // 偏好高楼层（视野/隔噪）。
    HighFloor,

    // 偏好低/底楼层（出入便利、行李少走楼梯）。
    GroundFloor
}
