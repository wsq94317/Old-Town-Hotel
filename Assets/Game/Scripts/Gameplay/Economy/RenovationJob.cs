// One in-progress room renovation: counts down day by day. (Phase 5.)
public sealed class RenovationJob
{
    public int RoomNumber { get; }
    public RoomTier TargetTier { get; }
    public int DaysRemaining { get; private set; }

    public RenovationJob(int roomNumber, RoomTier targetTier, int days)
    {
        RoomNumber = roomNumber;
        TargetTier = targetTier;
        DaysRemaining = days < 0 ? 0 : days;
    }

    public bool IsComplete => DaysRemaining <= 0;

    public void TickDay()
    {
        if (DaysRemaining > 0) DaysRemaining--;
    }
}
