using System;
using UnityEngine;

public partial class HotelSimSceneBridge
{
    public bool IsSuspended { get; set; }
    public DelegationReport LastDelegation { get; private set; }

    public void RestoreDelegationReport(DelegationReport report) => LastDelegation = report;

    public void ApplyElapsedDelegation(double seconds)
    {
        int minutes = DelegationPolicy.MinutesFor(seconds);
        if (minutes <= 0 || Sim == null) return;
        AdvanceDelegatedMinutes(minutes);
    }

    public void DelegateUntilTomorrow()
    {
        if (Sim == null) return;
        AdvanceDelegatedMinutes(AwaitingMorningReport ? SimClock.MinutesPerDay : Clock.MinutesRemainingToday);
    }

    private void AdvanceDelegatedMinutes(int minutes)
    {
        int debtBefore = economy != null && economy.Loan != null ? economy.Loan.Balance : 0;
        var report = new DelegationReport { minutes = minutes, debtBefore = debtBefore, safeBefore = Sim.Safebox.Balance };
        SyncAmenities();
        bool wasServiceEnabled = Sim.Pipeline.ServiceEnabled;
        bool wasCovered = Sim.FrontDeskCoverageAvailable;
        Sim.Pipeline.ServiceEnabled = true;
        Sim.Pipeline.ManagerOnFloor = false;
        Sim.FrontDeskCoverageAvailable = true;
        try {
            while (minutes > 0) {
                if (AwaitingMorningReport || _needsBeginDay) {
                    if (AwaitingMorningReport) {
                        report.wagesPaid += Sim.PayWages().paid;
                        if (economy != null && economy.Loan != null && economy.Loan.Balance > 0) {
                            int due = DebtPolicy.ScheduledRepaymentFor(economy.Loan.Balance,
                                Sim.LastSettlement.NetProfit - Sim.WagesAccruedToday,
                                Sim.Cash + Sim.Safebox.Balance, 150);
                            economy.RepayLoan(due);
                        }
                        // A delegated manager honours affordable refund claims.
                        for (int i = Sim.PendingRefunds.Count - 1; i >= 0; i--) {
                            var request = Sim.PendingRefunds[i];
                            if (Sim.Cash >= request.amount) Sim.ApproveRefund(request.requestId);
                            else Sim.RejectRefund(request.requestId);
                        }
                    }
                    AwaitingMorningReport = false;
                    Sim.BeginDay(Clock.CurrentDay > 1 && debtBefore > 0);
                    _needsBeginDay = false;
                }
                int step = Math.Min(minutes, Clock.MinutesRemainingToday);
                int before = Sim.ArrivalsCheckedInToday;
                Clock.FastForwardTo(Clock.CurrentMinute + step);
                while (Clock.TryConsumeTick()) {
                    bool clearing = Clock.CurrentMinute < PhaseScheduler.CheckInPeakStart
                        && Sim.Clearing.Count > 0
                        && (Sim.ClearRuinsFirst || (Rooms.DirtyBacklog == 0
                            && Rooms.CountOf(RoomSimState.Cleaning) == 0
                            && Rooms.CountOf(RoomSimState.AwaitingInspection) == 0));
                    Sim.Pipeline.ServiceEnabled = !clearing;
                    Sim.StepMinute();
                    if (clearing && Clock.CurrentMinute % 15 == 0) {
                        int workers = Staff.ProductiveCountOfRole(StaffRole.Housekeeper);
                        var jobs = Sim.RoomsBeingCleared();
                        if (workers > 0 && jobs.Count > 0)
                            Sim.ApplyJunkClearingVisit(jobs[0], workers * JunkClearingModel.WorkUnitsPerVisit);
                    }
                }
                report.guests += Math.Max(0, Sim.ArrivalsCheckedInToday - before);
                minutes -= step;
                if (Clock.DayEndReached) {
                    Sim.RecordMiscIncome(HotelEmpireProgress.IncomeForDay(Clock.CurrentDay));
                    // Conservative venue income; no unattended casino gambling.
                    Sim.RecordMiscIncome(Sim.ActiveStayCount * 8);
                    LastInterestAccrued = economy != null && economy.Loan != null ? economy.Loan.AccrueDailyInterest() : 0;
                    LastFacilityUpkeep = Sim.FacilityDailyUpkeep;
                    Sim.SettleDay(supplies: LastFacilityUpkeep, capitalisedInterest: LastInterestAccrued);
                    report.receipts += Sim.LastSettlement.netToSafebox + Sim.LastSettlement.overflowed;
                    report.daysClosed++;
                    Clock.BeginNextDay();
                    AwaitingMorningReport = true;
                }
            }
            report.safeAfter = Sim.Safebox.Balance;
            report.overflowAfter = Sim.Overflow.Balance;
            report.debtAfter = economy != null && economy.Loan != null ? economy.Loan.Balance : 0;
            report.brokenRooms = Rooms.CountOf(RoomSimState.Blocked);
            LastDelegation = report;
            // Live staff no longer own tasks completed by the delegated service.
            if (spawner != null) foreach (var agent in spawner.Agents) if (agent != null) agent.AbortTask();
            if (demandLoop != null && demandLoop.rooms != null) foreach (var room in demandLoop.rooms)
                if (room != null && Rooms.Contains(room.roomNumber)) {
                    var state = Rooms.At(room.roomNumber).state;
                    room.SetState(state == RoomSimState.Ruined ? Room2DState.Blocked : ToLegacy(state));
                }
            dayController?.RestoreOperationalClock(Clock.CurrentDay, Clock.CurrentMinute, AwaitingMorningReport);
            _mirrorTimer = 0;
        }
        finally {
            Sim.Pipeline.ServiceEnabled = wasServiceEnabled;
            Sim.FrontDeskCoverageAvailable = wasCovered;
        }
    }
}
