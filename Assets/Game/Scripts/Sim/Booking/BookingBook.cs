using System;
using System.Collections.Generic;

// 预订簿（架构 §B.4）：walk-in 模型的结构性升级。纯 C#，不引用 UnityEngine。
//
// 逐单 Reservation，100 房 × 14 天视野 ≈ 峰值千余条纯数据，无压力。
// **绑定粒度**：普通单只绑挂牌档（RoomTier = "客人订的是哪一档"），
// 具体房号在 check-in 时由 RoomMatcher 分配——与 walk-in 走同一个分配点。
// 绑档而不绑房型（Single/Double）是因为 M-C2 之后 RoomTier 就是玩家的挂牌承诺，
// 客人按这一档付钱、也按这一档验货（交付低于挂牌 ⇒ 退款申请）；
// Room2DRoomCategory 是另一个正交轴，等房型真的有差异了在 M-G 接。

public enum ReservationState
{
    Booked,      // 已确认，未到店
    CheckedIn,   // 在住
    Completed,   // 已退房（可归档）
    Cancelled,   // 到店前取消（回补库存）
    NoShow,      // 到店日没出现
    TurnedAway   // 超售/无房，被劝走（进声誉惩罚）
}

/// <summary>一张订单。可变引用类型：状态要原地流转，struct 会到处改到副本。</summary>
public sealed class Reservation
{
    public int id;
    public int channelId;
    public int bookedOnDay;        // 下单那天。取消风险率要按**原始提前期**摊，不能按剩余
    public int arrivalDay;
    public int nights;
    public RoomTier tier;          // 订的是哪一档（= 挂牌承诺）
    public int lockedPrice;        // 下单当时锁定的房价（之后调价不影响已售出的单）
    public GuestSegment segment;
    public ReservationState state;
    public int assignedRoomNumber; // check-in 时才有；0 = 还没分房

    /// <summary>还活着的单（会占库存 / 会来人）。</summary>
    public bool IsLive => state == ReservationState.Booked || state == ReservationState.CheckedIn;

    /// <summary>下单到到店之间隔了几天（至少 1，当天单也算一次取消机会）。</summary>
    public int LeadDays => Math.Max(1, arrivalDay - bookedOnDay);

    /// <summary>最后一晚是哪天（含）。</summary>
    public int LastNight => arrivalDay + (nights < 1 ? 1 : nights) - 1;
}

public sealed class BookingBook
{
    private readonly List<Reservation> _all = new List<Reservation>();
    private readonly Dictionary<int, Reservation> _byId = new Dictionary<int, Reservation>();
    private int _nextId = 1;

    public IReadOnlyList<Reservation> All => _all;
    public int Count => _all.Count;

    /// <summary>下一个要发的单号（存档用）。</summary>
    public int NextIdSeed => _nextId;

    public Reservation Find(int id) => _byId.TryGetValue(id, out Reservation r) ? r : null;

    /// <summary>登记一张单。id 稳定且**永不回收**——房记录里存着 occupantResvId，
    /// 号一旦复用就会指向别人的住宿（本项目在身份口径上定的铁律）。</summary>
    public Reservation Add(int channelId, int arrivalDay, int nights, RoomTier tier,
                           int lockedPrice, GuestSegment segment, int bookedOnDay = 0)
    {
        var r = new Reservation
        {
            id = _nextId++,
            channelId = channelId,
            bookedOnDay = bookedOnDay,
            arrivalDay = arrivalDay,
            nights = nights < 1 ? 1 : nights,
            tier = tier,
            lockedPrice = lockedPrice,
            segment = segment,
            state = ReservationState.Booked,
            assignedRoomNumber = 0,
        };
        _all.Add(r);
        _byId[r.id] = r;
        return r;
    }

    public bool Cancel(int id)
    {
        Reservation r = Find(id);
        if (r == null || r.state != ReservationState.Booked) return false;
        r.state = ReservationState.Cancelled;
        return true;
    }

    /// <summary>入住并绑房号。只对 Booked 生效——重复调用不能改掉已绑的房号。</summary>
    public bool CheckIn(int id, int roomNumber)
    {
        Reservation r = Find(id);
        if (r == null || r.state != ReservationState.Booked) return false;
        r.state = ReservationState.CheckedIn;
        r.assignedRoomNumber = roomNumber;
        return true;
    }

    public bool Complete(int id)
    {
        Reservation r = Find(id);
        if (r == null || r.state != ReservationState.CheckedIn) return false;
        r.state = ReservationState.Completed;
        return true;
    }

    public bool MarkNoShow(int id) => SetTerminal(id, ReservationState.NoShow);

    public bool MarkTurnedAway(int id) => SetTerminal(id, ReservationState.TurnedAway);

    private bool SetTerminal(int id, ReservationState terminal)
    {
        Reservation r = Find(id);
        if (r == null || r.state != ReservationState.Booked) return false;
        r.state = terminal;
        return true;
    }

    /// <summary>某日该到店的活单（晨报 + check-in 流用）。</summary>
    public List<Reservation> ArrivalsFor(int day)
    {
        var list = new List<Reservation>();
        for (int i = 0; i < _all.Count; i++)
        {
            Reservation r = _all[i];
            if (r.state == ReservationState.Booked && r.arrivalDay == day) list.Add(r);
        }
        return list;
    }

    /// <summary>在住的单（退房潮用）。</summary>
    public List<Reservation> InHouse()
    {
        var list = new List<Reservation>();
        for (int i = 0; i < _all.Count; i++)
            if (_all[i].state == ReservationState.CheckedIn) list.Add(_all[i]);
        return list;
    }

    /// <summary>按簿子**重算**日历上的需求（不是累加）。每晨调一次。
    /// 增量维护库存漏一处就永久对不上账，全量重算 O(单数)，便宜且不会错。</summary>
    public void RebuildCalendarDemand(AvailabilityCalendar calendar)
    {
        if (calendar == null) return;
        calendar.ClearDemand();
        for (int i = 0; i < _all.Count; i++)
        {
            Reservation r = _all[i];
            if (!r.IsLive) continue;
            calendar.Reserve(r.arrivalDay, r.nights, r.tier);
        }
    }

    /// <summary>每晨对**未来**的单掷取消骰。返回被取消的单号。
    /// 今天就该到店的不掷——那种情况是 no-show，走另一套处置。
    ///
    /// 渠道给的是**一张单的总取消率**，这里按该单的原始提前期摊成每日风险率
    /// （直接拿总率当每日率用，13 天提前期会复利成八成取消，见 DailyCancellationHazard）。</summary>
    public List<int> RollCancellations(int today, Func<double> roll)
    {
        var cancelled = new List<int>();
        if (roll == null) return cancelled;

        for (int i = 0; i < _all.Count; i++)
        {
            Reservation r = _all[i];
            if (r.state != ReservationState.Booked) continue;
            if (r.arrivalDay <= today) continue;

            // 按**原始**提前期摊：用剩余天数会每天重新摊一次，累计总率被推高两三倍
            double hazard = BookingChannels.DailyCancellationHazard(
                BookingChannels.Get(r.channelId).cancellationRate, r.LeadDays);
            if (roll() >= hazard) continue;
            r.state = ReservationState.Cancelled;
            cancelled.Add(r.id);
        }
        return cancelled;
    }

    /// <summary>归档已了结且最后一晚早于 day 的单。还活着的一条都不动。</summary>
    public void PruneCompletedBefore(int day)
    {
        for (int i = _all.Count - 1; i >= 0; i--)
        {
            Reservation r = _all[i];
            if (r.IsLive) continue;
            if (r.LastNight >= day) continue;
            _byId.Remove(r.id);
            _all.RemoveAt(i);
        }
    }

    // ── 存档支持 ─────────────────────────────────────────────────────────────

    public void Clear()
    {
        _all.Clear();
        _byId.Clear();
        _nextId = 1;
    }

    public void RestoreIdSeed(int nextId)
    {
        if (nextId > _nextId) _nextId = nextId;
    }

    /// <summary>读档用：按存下来的字段原样恢复一张单（不重新发号）。</summary>
    public Reservation RestoreReservation(int id, int channelId, int arrivalDay, int nights,
                                          RoomTier tier, int lockedPrice, GuestSegment segment,
                                          ReservationState state, int assignedRoomNumber,
                                          int bookedOnDay = 0)
    {
        var r = new Reservation
        {
            id = id,
            channelId = channelId,
            bookedOnDay = bookedOnDay,
            arrivalDay = arrivalDay,
            nights = nights < 1 ? 1 : nights,
            tier = tier,
            lockedPrice = lockedPrice,
            segment = segment,
            state = state,
            assignedRoomNumber = assignedRoomNumber,
        };
        _all.Add(r);
        _byId[id] = r;
        if (id >= _nextId) _nextId = id + 1;
        return r;
    }
}
