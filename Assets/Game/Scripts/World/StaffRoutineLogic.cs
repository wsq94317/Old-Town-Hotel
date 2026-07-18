using System;

public enum StaffShiftState
{
    OffShift,
    Arriving,
    OnDuty,
    EndShift,
    LeavingMap
}

public enum StaffActivityState
{
    AtPost,
    Idle,
    Travel,
    Working,
    BreakRoom,
    PublicToilet,
    HidingInToilet,
    Restock,
    Patrol,
    Leaving
}

public static class StaffRoutineLogic
{
    public const float FrontDeskVacancyGraceSeconds = 60f;
    public const int FrontDeskVacancySatisfactionPenalty = -2;
    public const int LegitimateToiletInspectionMoralePenalty = -4;

    public static StaffActivityState DefaultActivityFor(StaffRole role)
    {
        switch (role)
        {
            case StaffRole.Reception:
                return StaffActivityState.AtPost;
            case StaffRole.Inspector:
                return StaffActivityState.Patrol;
            default:
                return StaffActivityState.Idle;
        }
    }

    public static bool CanAutoAcceptRoomTask(
        StaffRole role,
        StaffShiftState shift,
        StaffActivityState activity,
        bool hasTask)
    {
        if (hasTask || shift != StaffShiftState.OnDuty) return false;
        if (role != StaffRole.Housekeeper && role != StaffRole.Inspector) return false;

        return activity == StaffActivityState.Idle
            || activity == StaffActivityState.Patrol
            || activity == StaffActivityState.BreakRoom;
    }

    public static bool ShouldHideInToilet(StaffMember member, Random rng)
    {
        if (member == null || rng == null) return false;

        double chance = 0.12d;
        if (member.HasTrait(StaffTrait.Lazy)) chance += 0.38d;
        if (member.Morale < 40) chance += 0.25d;
        if (member.Role == StaffRole.Reception) chance *= 0.65d;
        return rng.NextDouble() < Math.Min(0.85d, chance);
    }

    public static float NextPersonalNeedDelaySeconds(StaffRole role, Random rng)
    {
        if (rng == null) return 30f;
        double min = role == StaffRole.Reception ? 38d : 22d;
        double max = role == StaffRole.Reception ? 62d : 46d;
        return (float)(min + rng.NextDouble() * (max - min));
    }

    public static float ToiletDurationSeconds(bool hiding, Random rng)
    {
        if (rng == null) return hiding ? 24f : 6f;
        double min = hiding ? 18d : 4d;
        double max = hiding ? 32d : 8d;
        return (float)(min + rng.NextDouble() * (max - min));
    }
}
