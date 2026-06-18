// One hired staff member (Phase 1 = role + name + daily wage only;
// stats/traits/morale arrive in a later phase).
public sealed class StaffMember
{
    public StaffRole Role { get; }
    public string DisplayName { get; }
    public int DailyWage { get; }

    public StaffMember(StaffRole role, string displayName, int dailyWage)
    {
        Role = role;
        DisplayName = displayName;
        DailyWage = dailyWage;
    }
}
