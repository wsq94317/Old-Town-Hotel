using System.Collections.Generic;
using UnityEngine;

// 预分配控制器：管理 Preparation 阶段"选 guest slot → 选 room → 确认"的状态机。
// 纯逻辑层；UI 绑定在 Phase 5 (Room2DShowcaseViewController) 里完成。
public sealed class Room2DPreAssignmentController : MonoBehaviour
{
    // ── Inspector 引用（若留空则在 Awake 自动寻找） ──────────────────────────

    [Header("Scene References")]
    [Tooltip("阶段状态机引用；为 null 时 Awake 自动查找场景内第一个实例。")]
    [SerializeField] private Room2DDayPhaseStateMachine phaseStateMachine;

    [Tooltip("需求循环引用；为 null 时 Awake 自动查找场景内第一个实例。")]
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;

    // ── 公开状态属性 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 当前选中的 guest slot 索引（-1 表示未选中）。
    /// UI 用此值高亮对应的 guest 卡片。
    /// </summary>
    public int SelectedSlotIndex { get; private set; } = -1;

    /// <summary>
    /// 最近一次操作的反馈文本（Q3 方案 A: Last Action 文本区）。
    /// UI 每次收到 OnStateChanged 后读此字段刷新提示区。
    /// 无操作时为空字符串。
    /// </summary>
    public string LastActionMessage { get; private set; } = string.Empty;

    /// <summary>
    /// 确认按钮门控：当且仅当至少一个 slot 已预留房间时返回 true。
    /// </summary>
    public bool CanConfirm
    {
        get
        {
            // 遍历已预留列表，只要存在非 null 条目即可确认
            IReadOnlyList<Room2DEntity> reserved = demandLoop?.ReservedRoomsForUpcomingDemands;
            if (reserved == null) return false;
            for (int i = 0; i < reserved.Count; i++)
            {
                if (reserved[i] != null) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 当前是否处于 Preparation 阶段。
    /// Phase 5 UI 根据此值决定预分配面板的显隐。
    /// </summary>
    public bool IsPrepPhaseActive =>
        phaseStateMachine != null &&
        phaseStateMachine.CurrentPhase == Room2DDayPhaseStateMachine.Room2DDayPhase.Preparation;

    // ── 事件 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 任意状态变化（slot 选中/取消、预留写入/清除、LastActionMessage 更新）后触发。
    /// UI 订阅此事件做整体刷新。
    /// </summary>
    public event System.Action OnStateChanged;

    // ── Unity 生命周期 ───────────────────────────────────────────────────────

    private void Awake()
    {
        // 自动查找场景引用（Unity 6 API：FindFirstObjectByType，非已弃用的 FindObjectOfType）
        if (phaseStateMachine == null)
            phaseStateMachine = Object.FindFirstObjectByType<Room2DDayPhaseStateMachine>();

        if (demandLoop == null)
            demandLoop = Object.FindFirstObjectByType<Room2DPrototypeDemandLoop>();

        if (phaseStateMachine == null)
            Debug.LogError("[Room2DPreAssignmentController] 未能找到 Room2DDayPhaseStateMachine，功能将不可用。");

        if (demandLoop == null)
            Debug.LogError("[Room2DPreAssignmentController] 未能找到 Room2DPrototypeDemandLoop，功能将不可用。");

        // 订阅阶段事件；必须与 OnDestroy 里的 -= 配对
        if (phaseStateMachine != null)
        {
            phaseStateMachine.OnPhaseEntered += HandlePhaseEntered;
            phaseStateMachine.OnPhaseExited  += HandlePhaseExited;
        }
    }

    private void OnDestroy()
    {
        // 必须取消订阅，防止销毁后事件回调导致空引用或内存泄漏（Story 1 技术债教训）
        if (phaseStateMachine != null)
        {
            phaseStateMachine.OnPhaseEntered -= HandlePhaseEntered;
            phaseStateMachine.OnPhaseExited  -= HandlePhaseExited;
        }
    }

    // ── 公开 API（UI 调用） ───────────────────────────────────────────────────

    /// <summary>
    /// 点击 guest 卡片时调用。
    /// 同一 slot 再次点击 → 取消选中；不同 slot → 切换；越界索引 → 静默忽略。
    /// </summary>
    /// <param name="slotIndex">0-based slot 索引</param>
    public void SelectGuestSlot(int slotIndex)
    {
        // 越界索引：静默忽略，不改变状态
        if (demandLoop == null) return;
        if (slotIndex < 0 || slotIndex >= demandLoop.UpcomingQueueCount) return;

        if (slotIndex == SelectedSlotIndex)
        {
            // 再次点击同一 slot → 取消选中
            SelectedSlotIndex = -1;
            LastActionMessage = "Selection cleared";
        }
        else
        {
            // 切换到新 slot
            SelectedSlotIndex = slotIndex;
            LastActionMessage = "Selected guest slot " + (slotIndex + 1);
        }

        FireStateChanged();
    }

    /// <summary>
    /// 点击房间卡片时调用。
    /// 若无选中 slot 则给出提示；否则校验规则并写入预留。
    /// 成功后不自动取消 slot 选中，方便用户重新分配。
    /// </summary>
    /// <param name="room">被点击的房间实体</param>
    public void AttemptReserve(Room2DEntity room)
    {
        // 未选中 guest slot
        if (SelectedSlotIndex < 0)
        {
            LastActionMessage = "Select a guest first";
            FireStateChanged();
            return;
        }

        // 防御性 null 检查
        if (room == null)
        {
            LastActionMessage = "No room";
            FireStateChanged();
            return;
        }

        // 从需求循环读取当前 slot 的偏好数据
        Room2DBedTypePreference bedPref  = demandLoop.GetUpcomingBedTypePreference(SelectedSlotIndex);
        Room2DGuestType          guestType = demandLoop.GetUpcomingGuestType(SelectedSlotIndex);

        // 规则校验（纯静态，无副作用）
        (bool ok, string reason) result = Room2DPreAssignmentRules.CanReserve(room, bedPref, guestType);

        if (!result.ok)
        {
            // 规则拒绝：显示原因，不写入预留
            LastActionMessage = result.reason;
            FireStateChanged();
            return;
        }

        // 写入预留（demandLoop 内部处理同房间跨 slot 转移）
        bool success = demandLoop.ReserveRoomForUpcomingDemand(SelectedSlotIndex, room);
        LastActionMessage = success
            ? "Reserved " + room.roomName + " for slot " + (SelectedSlotIndex + 1)
            : "Reserve failed";

        // 成功后保留 SelectedSlotIndex，让用户可继续重新分配
        FireStateChanged();
    }

    /// <summary>
    /// 点击 Confirm 按钮时调用。
    /// 至少一个 slot 已预留才可推进；否则给出提示。
    /// </summary>
    public void RequestConfirm()
    {
        if (!CanConfirm)
        {
            // 门控未通过
            LastActionMessage = "Reserve at least one room first";
            FireStateChanged();
            return;
        }

        // 推进阶段；实际转换由状态机完成
        LastActionMessage = "Confirming...";
        FireStateChanged();

        phaseStateMachine.RequestAdvancePhase();
    }

    // ── 私有事件处理 ─────────────────────────────────────────────────────────

    // 进入新阶段时：若为 Preparation 则重置选中状态并通知 UI
    private void HandlePhaseEntered(Room2DDayPhaseStateMachine.Room2DDayPhase phase)
    {
        if (phase == Room2DDayPhaseStateMachine.Room2DDayPhase.Preparation)
        {
            // 每次 Preparation 重置，防止跨天残留选中
            SelectedSlotIndex = -1;
            LastActionMessage = string.Empty;
            FireStateChanged();
        }
    }

    // 退出阶段时：若退出 Preparation 则通知 UI 隐藏面板（IsPrepPhaseActive 此时已为 false）
    private void HandlePhaseExited(Room2DDayPhaseStateMachine.Room2DDayPhase phase)
    {
        if (phase == Room2DDayPhaseStateMachine.Room2DDayPhase.Preparation)
        {
            FireStateChanged();
        }
    }

    // ── 私有辅助 ─────────────────────────────────────────────────────────────

    private void FireStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
