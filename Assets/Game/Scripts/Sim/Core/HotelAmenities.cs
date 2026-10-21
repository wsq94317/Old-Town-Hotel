using System;

[Flags]
public enum HotelAmenities { None = 0, Gym = 1, Casino = 2, Pool = 4 }

public sealed partial class HotelSim
{
    public HotelAmenities Amenities { get; set; }
    public int FacilityDailyUpkeep => ((Amenities & HotelAmenities.Gym) != 0 ? 25 : 0)
        + ((Amenities & HotelAmenities.Casino) != 0 ? 50 : 0)
        + ((Amenities & HotelAmenities.Pool) != 0 ? 40 : 0);
    public float AmenityBonus(GuestSegment segment)
    {
        float bonus = 0;
        if ((Amenities & HotelAmenities.Gym) != 0 && segment == GuestSegment.Business) bonus += 0.04f;
        if ((Amenities & HotelAmenities.Pool) != 0 && (segment == GuestSegment.Vip || segment == GuestSegment.Party)) bonus += 0.05f;
        return bonus;
    }
}
