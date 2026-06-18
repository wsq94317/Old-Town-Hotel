using System;

// Pure-C# hotel asset valuation + bank credit limit. (Phase 3 Finance.)
// Value rises with how many rooms are open and how many are renovated;
// the bank lends a fraction of value minus what you already owe.
public static class HotelValuation
{
    // baseValue + openRooms*perRoom + renovatedRooms*renovatedBonus.
    public static int Compute(int openRooms, int renovatedRooms,
                              int baseValue, int perRoomValue, int renovatedRoomBonus)
    {
        openRooms = Math.Max(0, openRooms);
        renovatedRooms = Math.Clamp(renovatedRooms, 0, openRooms);
        return Math.Max(0, baseValue)
             + openRooms * Math.Max(0, perRoomValue)
             + renovatedRooms * Math.Max(0, renovatedRoomBonus);
    }

    // How much the bank will still lend: value*factor minus outstanding debt (never negative).
    public static int CreditLimit(int hotelValue, float factor, int outstandingLoan)
    {
        int max = (int)Math.Round(Math.Max(0, hotelValue) * (double)Math.Max(0f, factor),
                                  MidpointRounding.AwayFromZero);
        return Math.Max(0, max - Math.Max(0, outstandingLoan));
    }
}
