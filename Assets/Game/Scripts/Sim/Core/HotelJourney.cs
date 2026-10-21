using System;
using System.Collections.Generic;

[Serializable]
public sealed class HotelJourneyState
{
    public int checkIns, collected, profitableStreak, longestStreak, lastClosedDay;
    public List<int> restored = new List<int>();
    public List<int> renovated = new List<int>();
}

// Progress is awarded by completed business actions, including actions completed
// before their objective is shown. Reloading never awards a day twice.
public sealed class HotelJourney
{
    private HotelJourneyState _state = new HotelJourneyState();
    public HotelJourneyState State => _state;
    public void CheckIn() => _state.checkIns++;
    public void Collect(int amount) { if (amount > 0) _state.collected += amount; }
    public void RestoreRoom(int room) { if (!_state.restored.Contains(room)) _state.restored.Add(room); }
    public void RenovateRoom(int room) { if (!_state.renovated.Contains(room)) _state.renovated.Add(room); }
    public void CloseDay(int day, int net)
    {
        if (day <= _state.lastClosedDay) return;
        _state.lastClosedDay = day;
        _state.profitableStreak = net > 0 ? _state.profitableStreak + 1 : 0;
        _state.longestStreak = Math.Max(_state.longestStreak, _state.profitableStreak);
    }
    public int Stage
    {
        get {
            if (_state.checkIns == 0) return 0;
            if (_state.collected == 0) return 1;
            if (_state.restored.Count == 0) return 2;
            if (_state.renovated.Count == 0) return 3;
            if (_state.longestStreak < 3) return 4;
            return 5;
        }
    }
    public HotelJourneyState Capture() => new HotelJourneyState {
        checkIns = _state.checkIns, collected = _state.collected,
        profitableStreak = _state.profitableStreak, longestStreak = _state.longestStreak,
        lastClosedDay = _state.lastClosedDay,
        restored = new List<int>(_state.restored), renovated = new List<int>(_state.renovated)
    };
    public void Restore(HotelJourneyState state)
    {
        _state = state ?? new HotelJourneyState();
        if (_state.restored == null) _state.restored = new List<int>();
        if (_state.renovated == null) _state.renovated = new List<int>();
    }
}
