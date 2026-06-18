using System;

// Core staff stats, each 0-100. Pure value type. (Phase 4 Staff.)
public readonly struct StaffAttributes
{
    public readonly int Speed;    // work speed
    public readonly int Quality;  // low quality => mistakes / lower satisfaction
    public readonly int Stamina;  // how long before tiring

    public StaffAttributes(int speed, int quality, int stamina)
    {
        Speed = Clamp(speed);
        Quality = Clamp(quality);
        Stamina = Clamp(stamina);
    }

    private static int Clamp(int v) => Math.Clamp(v, 0, 100);

    public static StaffAttributes Default => new StaffAttributes(50, 50, 50);
}
