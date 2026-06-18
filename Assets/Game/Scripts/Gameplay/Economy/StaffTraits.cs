// Trait metadata helpers (Phase 4 Staff). Gameplay effects live in the systems
// that read these; here we only classify + label for UI/decisions.
public static class StaffTraits
{
    public static bool IsPositive(StaffTrait t)
    {
        switch (t)
        {
            case StaffTrait.Charmer:
            case StaffTrait.FastHands:
            case StaffTrait.Tidy:
            case StaffTrait.Calm:
                return true;
            default:
                return false;
        }
    }

    public static string Label(StaffTrait t) => t.ToString();
}
