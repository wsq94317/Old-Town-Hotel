using System;
using System.Collections.Generic;

// M4 事件池（纯 C#）：事件定义表 + 每日排程逻辑。
// 用代码表而非 ScriptableObject——夜间迭代快且全可测；正式内容管线再迁 SO（假设已记录）。
public enum EventAnchor { FrontDesk, Lounge, RandomOccupiedRoom }

public enum EventSpecial { None, RaiseRandomStaff, RefuseRaiseRandomStaff }

public readonly struct GameEffect
{
    public readonly int Cash, Satisfaction, StaffMorale, Prestige;
    public GameEffect(int cash, int satisfaction, int staffMorale, int prestige)
    { Cash = cash; Satisfaction = satisfaction; StaffMorale = staffMorale; Prestige = prestige; }
}

public sealed class EventOption
{
    public readonly string Label;
    public readonly GameEffect Effect;
    public readonly EventSpecial Special;
    public readonly string Story;
    public EventOption(string label, GameEffect effect, string story, EventSpecial special = EventSpecial.None)
    { Label = label; Effect = effect; Story = story; Special = special; }
}

public sealed class HotelEventDef
{
    public readonly string Id, Title, Blurb;
    public readonly float MinHour, MaxHour;   // 触发时窗（游戏小时）
    public readonly EventAnchor Anchor;
    public readonly EventOption[] Options;
    public HotelEventDef(string id, string title, string blurb, float minHour, float maxHour,
                         EventAnchor anchor, params EventOption[] options)
    { Id = id; Title = title; Blurb = blurb; MinHour = minHour; MaxHour = maxHour; Anchor = anchor; Options = options; }
}

public static class EventCatalog
{
    public static readonly IReadOnlyList<HotelEventDef> All = new List<HotelEventDef>
    {
        new HotelEventDef("health_inspector", "HEALTH INSPECTOR!", "A man with a clipboard and zero joy has arrived.",
            10f, 16f, EventAnchor.FrontDesk,
            new EventOption("Slip him $60", new GameEffect(-60, 0, 0, +1),
                "He pocketed it and rated you 'surprisingly sanitary'."),
            new EventOption("Let him inspect", new GameEffect(0, -2, 0, 0),
                "He found a sock behind the lounge. Nobody claims the sock.")),

        new HotelEventDef("guest_fight", "GUESTS ARGUING", "Two guests are re-enacting a divorce in the lounge.",
            11f, 20f, EventAnchor.Lounge,
            new EventOption("Break it up", new GameEffect(0, +1, 0, 0),
                "You separated them with the authority of a substitute teacher."),
            new EventOption("Sell popcorn", new GameEffect(+15, -2, +3, +1),
                "You charged spectators. The staff respects the hustle.")),

        new HotelEventDef("influencer", "INFLUENCER CHECK-IN", "Someone with 2M followers and a selfie stick appears.",
            10f, 15f, EventAnchor.FrontDesk,
            new EventOption("Comp EVERYTHING", new GameEffect(-50, +5, 0, +1),
                "She posted 'HIDDEN GEM!!'. Your phone hasn't stopped since."),
            new EventOption("Treat her like anyone", new GameEffect(0, -1, +2, +1),
                "She posted 'staff unbothered. iconic.' Somehow that's good.")),

        new HotelEventDef("bed_incident", "THE BED INCIDENT", "Room service reports a... situation. A damp situation.",
            9f, 18f, EventAnchor.RandomOccupiedRoom,
            new EventOption("Charge a cleaning fee", new GameEffect(+40, -2, 0, 0),
                "He paid. In coins. All coins."),
            new EventOption("Pretend it never happened", new GameEffect(0, +2, -5, 0),
                "Housekeeping saw things they cannot unsee.")),

        new HotelEventDef("raise_demand", "PAYDAY MUTINY", "A staff member wants a raise. They brought a folder.",
            13f, 19f, EventAnchor.FrontDesk,
            new EventOption("Give the raise (+$5/day)", new GameEffect(0, 0, +10, 0),
                "They framed the pay slip. Payroll weeps quietly.", EventSpecial.RaiseRandomStaff),
            new EventOption("Quote 'market conditions'", new GameEffect(0, 0, -10, 0),
                "They quoted the union. Nobody has a union. Yet.", EventSpecial.RefuseRaiseRandomStaff)),

        new HotelEventDef("drunk_lounge", "A DRUNK IN THE LOUNGE", "He's singing. It's not bad, but it's 2 PM.",
            12f, 21f, EventAnchor.Lounge,
            new EventOption("Escort him out", new GameEffect(0, +1, 0, +1),
                "Out he went, mid-chorus. The lounge applauded."),
            new EventOption("Free coffee, no questions", new GameEffect(-10, +2, +2, 0),
                "He sobered up and tipped you a full life story.")),
    };
}

// 每日排程（纯逻辑，可测）：从事件表随机抽 2-3 个不重复事件，各在自己的时窗内随机定时。
public readonly struct ScheduledEvent
{
    public readonly HotelEventDef Def;
    public readonly float TriggerHour;
    public ScheduledEvent(HotelEventDef def, float hour) { Def = def; TriggerHour = hour; }
}

public static class EventScheduleLogic
{
    public static List<ScheduledEvent> ScheduleForDay(IReadOnlyList<HotelEventDef> catalog, Random rng)
    {
        int count = 2 + rng.Next(2); // 2 或 3
        var pool = new List<HotelEventDef>(catalog);
        var result = new List<ScheduledEvent>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int pick = rng.Next(pool.Count);
            var def = pool[pick];
            pool.RemoveAt(pick);
            float hour = def.MinHour + (float)rng.NextDouble() * (def.MaxHour - def.MinHour);
            result.Add(new ScheduledEvent(def, hour));
        }
        result.Sort((a, b) => a.TriggerHour.CompareTo(b.TriggerHour));
        return result;
    }
}
