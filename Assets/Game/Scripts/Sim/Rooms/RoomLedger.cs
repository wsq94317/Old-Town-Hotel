using System.Collections.Generic;

// 房态总账（架构 B.5）：100 间房的唯一权威 + 分区聚合池。纯 C#，不引用 UnityEngine。
//
// 聚合是 v3 能管 100 间房的关键：清洁按"区"整池消化（清洁工派到区不派到房），
// 只有 VIP / 问题房 / 装修中 / 当前可视楼层的房才 Surfaced 走逐房逻辑（预期 ≤20 间）。
//
// 计数用惰性重算 + dirty 标记：一个 tick 内多次改状态只重算一次；100 间房全量重算
// 是 100 次循环，比手工增量维护安全得多（增量维护漏一处就永久对不上账）。
public sealed class RoomLedger
{
    private const int StateCount = 7; // RoomSimState 成员数

    private readonly RoomRecord[] _rooms;
    private readonly Dictionary<int, int> _indexByNumber;
    private readonly List<byte> _zones = new List<byte>();
    private readonly int[] _stateTotals = new int[StateCount];
    private readonly Dictionary<byte, int[]> _stateByZone = new Dictionary<byte, int[]>();
    private readonly List<int> _surfaced = new List<int>();
    private bool _aggregatesDirty = true;

    public RoomLedger(IList<RoomDefinition> definitions)
    {
        int n = definitions?.Count ?? 0;
        _rooms = new RoomRecord[n];
        _indexByNumber = new Dictionary<int, int>(n);

        for (int i = 0; i < n; i++)
        {
            RoomDefinition d = definitions[i];
            _rooms[i] = new RoomRecord
            {
                number = d.number,
                floor = d.floor,
                zone = d.zone,
                category = d.category,
                tier = d.tier,
                state = d.state,
                wear = 0f,
                occupantResvId = 0,
                flags = RoomFlags.None,
            };
            _indexByNumber[d.number] = i;
            if (!_stateByZone.ContainsKey(d.zone))
            {
                _stateByZone[d.zone] = new int[StateCount];
                _zones.Add(d.zone);
            }
        }
    }

    public int Count => _rooms.Length;

    public IReadOnlyList<byte> Zones => _zones;

    public bool Contains(int roomNumber) => _indexByNumber.ContainsKey(roomNumber);

    /// <summary>按房号取记录的 ref 访问器——修改必须走这里，否则改到 struct 副本。</summary>
    public ref RoomRecord At(int roomNumber)
    {
        int index = _indexByNumber.TryGetValue(roomNumber, out int i) ? i : -1;
        if (index < 0) return ref _fallback; // 未知房号：写进哨兵，不炸也不污染真数据
        _aggregatesDirty = true;             // ref 拿出去后外部可能改 state，保守标脏
        return ref _rooms[index];
    }

    private static RoomRecord _fallback;

    /// <summary>按数组下标遍历（离线结算/批量推进用，避免字典查找）。</summary>
    public ref RoomRecord AtIndex(int index)
    {
        _aggregatesDirty = true;
        return ref _rooms[index];
    }

    /// <summary>只读快照（统计/UI 用，不标脏）。</summary>
    public RoomRecord Peek(int index) => _rooms[index];

    public bool SetState(int roomNumber, RoomSimState newState)
    {
        if (!_indexByNumber.TryGetValue(roomNumber, out int i)) return false;
        if (_rooms[i].state == newState) return true;
        _rooms[i].state = newState;
        _aggregatesDirty = true;
        return true;
    }

    public void AddFlags(int roomNumber, RoomFlags flags)
    {
        if (!_indexByNumber.TryGetValue(roomNumber, out int i)) return;
        _rooms[i].flags |= flags;
        _aggregatesDirty = true;
    }

    public void RemoveFlags(int roomNumber, RoomFlags flags)
    {
        if (!_indexByNumber.TryGetValue(roomNumber, out int i)) return;
        _rooms[i].flags &= ~flags;
        _aggregatesDirty = true;
    }

    /// <summary>是否需要单独浮出给玩家处理（VIP/问题/装修/可视层）。</summary>
    public bool IsSurfaced(int roomNumber)
    {
        if (!_indexByNumber.TryGetValue(roomNumber, out int i)) return false;
        return _rooms[i].flags != RoomFlags.None;
    }

    public IReadOnlyList<int> SurfacedRoomNumbers
    {
        get { EnsureAggregates(); return _surfaced; }
    }

    /// <summary>找一间处于某状态的房（数组序，稳定=可复现）。清洁池消化用。</summary>
    public bool TryFindFirstInState(RoomSimState state, out int roomNumber)
    {
        for (int i = 0; i < _rooms.Length; i++)
        {
            if (_rooms[i].state != state) continue;
            roomNumber = _rooms[i].number;
            return true;
        }
        roomNumber = 0;
        return false;
    }

    /// <summary>花钱解锁一间破败房：变成脏房（还得打扫才能卖）。</summary>
    public bool TryUnlockRuinedRoom(int roomNumber)
    {
        if (!_indexByNumber.TryGetValue(roomNumber, out int i)) return false;
        if (_rooms[i].state != RoomSimState.Ruined) return false;
        _rooms[i].state = RoomSimState.Dirty;
        _aggregatesDirty = true;
        return true;
    }

    // ── 聚合查询 ─────────────────────────────────────────────────────────────

    public int CountOf(RoomSimState state)
    {
        EnsureAggregates();
        return _stateTotals[(int)state];
    }

    /// <summary>营业房量（排除破败未解锁房）。</summary>
    public int OpenRoomCount
    {
        get { EnsureAggregates(); return _rooms.Length - _stateTotals[(int)RoomSimState.Ruined]; }
    }

    /// <summary>当前可售房数（只有 Ready 算）。</summary>
    public int SellableCount => CountOf(RoomSimState.Ready);

    /// <summary>脏房积压（服务压力的核心指标）。在清洁中的不算。</summary>
    public int DirtyBacklog => CountOf(RoomSimState.Dirty);

    public int BlockedCount => CountOf(RoomSimState.Blocked);

    public ZoneAggregate AggregateForZone(byte zone)
    {
        EnsureAggregates();
        return _stateByZone.TryGetValue(zone, out int[] counts)
            ? new ZoneAggregate(zone, counts)
            : new ZoneAggregate(zone, new int[StateCount]);
    }

    public ZoneAggregate AggregateForZone(int zone) => AggregateForZone((byte)zone);

    /// <summary>营业房的平均档位归一到 0..1（全 Old=0，全 Better=1）。喂需求乘数：
    /// 酒店整体越好越有人来，这条耦合让装修成为成长杠杆而不是陷阱。</summary>
    public float AverageTierNormalised
    {
        get
        {
            int open = 0;
            float sum = 0f;
            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_rooms[i].state == RoomSimState.Ruined) continue;
                open++;
                sum += (int)_rooms[i].tier / 2f;   // Old=0, Basic=0.5, Better=1
            }
            return open == 0 ? 0f : sum / open;
        }
    }

    /// <summary>平均磨损（喂损坏概率：满房加速磨损 → 客流咬资产）。</summary>
    public float AverageWear
    {
        get
        {
            if (_rooms.Length == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _rooms.Length; i++) sum += _rooms[i].wear;
            return sum / _rooms.Length;
        }
    }

    private void EnsureAggregates()
    {
        if (!_aggregatesDirty) return;
        _aggregatesDirty = false;

        for (int s = 0; s < StateCount; s++) _stateTotals[s] = 0;
        foreach (var kv in _stateByZone)
        {
            int[] counts = kv.Value;
            for (int s = 0; s < StateCount; s++) counts[s] = 0;
        }
        _surfaced.Clear();

        for (int i = 0; i < _rooms.Length; i++)
        {
            int state = (int)_rooms[i].state;
            _stateTotals[state]++;
            if (_stateByZone.TryGetValue(_rooms[i].zone, out int[] zoneCounts)) zoneCounts[state]++;
            if (_rooms[i].flags != RoomFlags.None) _surfaced.Add(_rooms[i].number);
        }
    }
}

/// <summary>一个区域（楼层/房型分组）的状态计数快照——战术层区域卡片直接读它。</summary>
public readonly struct ZoneAggregate
{
    public readonly byte zone;
    private readonly int[] _counts;

    public ZoneAggregate(byte zone, int[] counts)
    {
        this.zone = zone;
        _counts = counts;
    }

    public int CountOf(RoomSimState state) => _counts != null ? _counts[(int)state] : 0;

    public int total
    {
        get
        {
            if (_counts == null) return 0;
            int sum = 0;
            for (int i = 0; i < _counts.Length; i++) sum += _counts[i];
            return sum;
        }
    }

    public int sellable => CountOf(RoomSimState.Ready);
    public int dirtyBacklog => CountOf(RoomSimState.Dirty);
    public int ruined => CountOf(RoomSimState.Ruined);
}
