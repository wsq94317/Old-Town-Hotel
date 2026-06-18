using System.Collections.Generic;

// One hired staff member. Phase 1 created these with (role, name, wage);
// Phase 4 adds attributes, education, traits and mutable morale/wage.
public sealed class StaffMember
{
    public StaffRole Role { get; }
    public string DisplayName { get; }
    public int DailyWage { get; private set; }   // mutable via raises
    public StaffAttributes Attributes { get; }
    public int EducationLevel { get; }            // 0..n, higher = better potential
    public int Morale { get; private set; }       // 0..100

    private readonly List<StaffTrait> _traits;
    public IReadOnlyList<StaffTrait> Traits => _traits;

    public const int DefaultMorale = 70;

    // Phase 1 compatible: default attributes, no traits, default morale.
    public StaffMember(StaffRole role, string displayName, int dailyWage)
        : this(role, displayName, dailyWage, StaffAttributes.Default, 0, null) { }

    public StaffMember(StaffRole role, string displayName, int dailyWage,
                       StaffAttributes attributes, int educationLevel, IEnumerable<StaffTrait> traits)
    {
        Role = role;
        DisplayName = displayName;
        DailyWage = dailyWage;
        Attributes = attributes;
        EducationLevel = educationLevel;
        Morale = DefaultMorale;
        _traits = traits != null ? new List<StaffTrait>(traits) : new List<StaffTrait>();
    }

    public bool HasTrait(StaffTrait t) => _traits.Contains(t);

    public void AdjustMorale(int delta)
    {
        Morale = System.Math.Clamp(Morale + delta, 0, 100);
    }

    // Apply a raise: only accepts a higher wage; bumps morale.
    public void GiveRaise(int newDailyWage)
    {
        if (newDailyWage > DailyWage)
        {
            DailyWage = newDailyWage;
            AdjustMorale(+15);
        }
    }

    // Ambitious staff want a raise after enough days without one.
    public bool WantsRaise(int daysSinceLastRaise, int thresholdDays)
        => HasTrait(StaffTrait.Ambitious) && daysSinceLastRaise >= thresholdDays;
}
