using UnityEngine;

// "Do It Yourself": the boss can cover exactly one vacant role at a time — free,
// but slower than staff. Implemented as a dedicated Housekeeper2D instance that
// is never on payroll. The single-worker tension of ADR 0004 lives on here
// (ADR 0008 decision #3): understaffed play means the boss is the bottleneck.
[DisallowMultipleComponent]
public sealed class BossCover : MonoBehaviour
{
    [SerializeField, Tooltip("Dedicated worker instance for the boss (not on payroll).")]
    private Housekeeper2D bossHousekeeper;
    [SerializeField, Range(0.1f, 1f), Tooltip("Boss works at this fraction of staff speed.")]
    private float speedFactor = 0.5f;

    public bool IsBusy => bossHousekeeper != null && bossHousekeeper.IsBusy;

    // Global strip text, e.g. "You're cleaning Room 103…" (empty when free).
    public string BusyLabel
        => IsBusy && bossHousekeeper.AssignedRoomNumber.HasValue
            ? $"You're cleaning Room {bossHousekeeper.AssignedRoomNumber.Value}…"
            : string.Empty;

    private void Awake()
    {
        if (bossHousekeeper != null && speedFactor > 0f)
            bossHousekeeper.cleaningDurationSeconds /= speedFactor; // 0.5 -> takes 2x as long
    }

    // Start cleaning `room` as the boss. Fails if he's already covering something.
    public bool TryCleanRoom(Room2DEntity room)
    {
        if (bossHousekeeper == null || room == null || IsBusy) return false;
        return bossHousekeeper.AssignRoom(room);
    }
}
