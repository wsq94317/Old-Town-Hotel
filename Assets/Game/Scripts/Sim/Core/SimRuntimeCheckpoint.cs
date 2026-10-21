using System;
using System.Collections.Generic;

[Serializable]
public sealed class RuntimeStayEntry
{
    public int room;
    public HotelSim.Stay stay;
}

[Serializable]
public sealed class RuntimeReputationEntry
{
    public int cause, count;
    public float delta;
}

// In-day save data: assets alone cannot restore a running business. Keeping
// queues, stays and unpaid claims prevents reloads from duplicating guests/money.
[Serializable]
public sealed class SimRuntimeCheckpoint
{
    public List<RuntimeStayEntry> stays = new List<RuntimeStayEntry>();
    public List<int> pending = new List<int>();
    public List<int> desk = new List<int>();
    public List<HotelSim.RefundRequest> refunds = new List<HotelSim.RefundRequest>();
    public List<HotelSim.OverbookingIncident> overbookings = new List<HotelSim.OverbookingIncident>();
    public int gross, roomIncome, miscIncome, commission, released, planned;
    public int checkedIn, turnedAway, totalWait, peakWait, checkouts, wagesAccrued;
    public int nextRefundId, nextOverbookingId;
    public double deskCredit;
    public int deposited, overflowed, reservePaid, cashAfter, unpaid;
    public bool wagesPaid;
    public List<RuntimeReputationEntry> reputation = new List<RuntimeReputationEntry>();
}

public sealed partial class HotelSim
{
    private SimRuntimeCheckpoint CaptureCheckpoint()
    {
        var c = new SimRuntimeCheckpoint {
            gross = _grossIncomeToday, roomIncome = _roomIncomeToday,
            miscIncome = _miscIncomeToday, commission = _commissionToday,
            released = _arrivalsReleasedToday, deskCredit = _deskCredit,
            planned = ArrivalsPlannedToday, checkedIn = ArrivalsCheckedInToday,
            turnedAway = ArrivalsTurnedAwayToday, totalWait = TotalCheckInWaitToday,
            peakWait = PeakCheckInWaitToday, checkouts = _checkoutsToday,
            wagesAccrued = WagesAccruedToday, nextRefundId = _nextRefundId,
            nextOverbookingId = _nextOverbookingId,
            deposited = LastSettlement.netToSafebox, overflowed = LastSettlement.overflowed,
            reservePaid = LastSettlement.cashPaidFromReserve, cashAfter = LastSettlement.cashAfter,
            unpaid = LastSettlement.unpaidAmount, wagesPaid = LastSettlement.wagesPaid
        };
        foreach (var pair in _stays) c.stays.Add(new RuntimeStayEntry { room = pair.Key, stay = pair.Value });
        c.pending.AddRange(_pendingArrivals);
        c.desk.AddRange(_deskQueue);
        c.refunds.AddRange(_refunds);
        c.overbookings.AddRange(_overbookings);
        foreach (ReputationCause cause in Enum.GetValues(typeof(ReputationCause)))
            if (Breakdown.CountOf(cause) > 0) c.reputation.Add(new RuntimeReputationEntry {
                cause = (int)cause, count = Breakdown.CountOf(cause), delta = Breakdown.TotalOf(cause)
            });
        return c;
    }

    private void RestoreCheckpoint(SimRuntimeCheckpoint c)
    {
        if (c == null) return; // v9 saves keep their original morning behaviour.
        _stays.Clear();
        if (c.stays != null) foreach (var e in c.stays)
            if (Rooms.Contains(e.room)) {
                _stays[e.room] = e.stay;
                Rooms.At(e.room).occupantResvId = e.stay.reservationId;
            }
        _pendingArrivals.Clear();
        if (c.pending != null) foreach (int ticket in c.pending) _pendingArrivals.Enqueue(ticket);
        _deskQueue.Clear();
        if (c.desk != null) foreach (int ticket in c.desk) _deskQueue.Enqueue(ticket);
        _refunds.Clear(); if (c.refunds != null) _refunds.AddRange(c.refunds);
        _overbookings.Clear(); if (c.overbookings != null) _overbookings.AddRange(c.overbookings);
        _nextRefundId = c.nextRefundId; _nextOverbookingId = c.nextOverbookingId;
        _grossIncomeToday = c.gross; _roomIncomeToday = c.roomIncome;
        _miscIncomeToday = c.miscIncome; _commissionToday = c.commission;
        _arrivalsReleasedToday = c.released; _deskCredit = c.deskCredit;
        ArrivalsPlannedToday = c.planned; ArrivalsCheckedInToday = c.checkedIn;
        ArrivalsTurnedAwayToday = c.turnedAway; TotalCheckInWaitToday = c.totalWait;
        PeakCheckInWaitToday = c.peakWait; _checkoutsToday = c.checkouts;
        WagesAccruedToday = c.wagesAccrued;
        Breakdown.Reset();
        if (c.reputation != null) foreach (var entry in c.reputation)
            for (int i = 0; i < entry.count; i++)
                Breakdown.Add((ReputationCause)entry.cause, entry.delta / entry.count);
        LastSettlement = new DaySettlementResult(c.deposited, c.overflowed, c.reservePaid,
            c.cashAfter, c.wagesPaid, c.unpaid);
        RefreshCapacityForHorizon();
    }
}
