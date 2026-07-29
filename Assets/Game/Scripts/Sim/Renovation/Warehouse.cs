// 仓库容量（用户要求："仓库系统可以先帮我搭一个基础。就是仓库会满"）。
// 纯 C#，不引用 UnityEngine。
//
// **这一版刻意只管材料，不做"待安装家具箱"**。并行设计审计（10 个 agent）把
// 箱子方案打回了三次，两条致命理由值得写在这里，免得以后有人"顺手补上"：
//
//   ① **箱子只有入口没有出口**：可选装饰位（电视/沙发/书桌/地毯/挂画）会抢
//      同一个格子，装不下的箱子永远躺在仓库里，而买材料又被仓库卡住——
//      玩家能花钱进去、出不来。这违反本项目的铁律（永不锁死玩家）。
//      要做箱子，必须先有"变卖/丢弃"这个动词。
//   ② **箱子会把整层装修变成物理上不可能**：FullFit 每间 6 箱 × 3 格 + 8 材料
//      = 26 格/间，整层 16 间要 416 格。而批量折扣曲线在设计文档里写明是
//      **刻意渐近**的、且"唯一的反向制衡是工期天数"。加一道硬上限等于偷偷
//      改掉那份契约。
//
// 同理**这一版也不做付费升级**：世界场景的 SaveCoordinator 里根本没有 gs.sim
// （Sim 状态还没接存档，那是 M-F 的活）。现在加一个 $18000 的升级按钮，玩家
// 付了钱、存档一读就没了，而且没有任何提示——审计原话是"cash-losing silent
// state"。等存档接好再开升级。
public sealed class Warehouse
{
    /// <summary>0 = 不设上限。**测试与旧场景的默认值**——容量是"场景参数"而不是
    /// 内核常量，所以既有的 `TryBuyMaterials(200)` 一类调用不受影响。
    /// 游戏里的两个场景都会显式设一个真实容量（否则这个功能等于不存在）。</summary>
    public const int Unlimited = 0;

    /// <summary>一份材料占几格。留成常数是为了以后家具箱进来时有个统一的尺度。</summary>
    public const int SpacePerMaterial = 1;

    /// <summary>世界场景的默认容量。取 40 是算过的：12 间房的店里一次
    /// FullFit 复原 4 间要 32 份材料——批量折扣照样吃得到；但屯到 40 就收不下了，
    /// 于是"仓库会满"是真的会撞上，又不会把玩家当前买得起的任何一批货堵死。</summary>
    public const int DefaultCapacity = 40;

    public int Capacity { get; private set; } = Unlimited;

    public bool HasLimit => Capacity > 0;

    public Warehouse(int capacity = Unlimited) => SetCapacity(capacity);

    public void SetCapacity(int capacity) => Capacity = capacity < 0 ? Unlimited : capacity;

    /// <summary>当前库存占了几格。</summary>
    public int UsedBy(int materialStock) =>
        materialStock <= 0 ? 0 : materialStock * SpacePerMaterial;

    /// <summary>还剩几格（不设上限时返回 int.MaxValue，调用方不必到处判空）。</summary>
    public int SpaceLeftWith(int materialStock)
    {
        if (!HasLimit) return int.MaxValue;
        int left = Capacity - UsedBy(materialStock);
        return left < 0 ? 0 : left;
    }

    /// <summary>还能收下几份材料。想买 10 份而只剩 3 格时返回 3——
    /// **UI 要把这个数字说出来**，玩家才知道该买几份，而不是对着一次失败发呆。</summary>
    public int UnitsThatFit(int materialStock, int wantedUnits)
    {
        if (wantedUnits <= 0) return 0;
        if (!HasLimit) return wantedUnits;
        int room = SpaceLeftWith(materialStock) / SpacePerMaterial;
        return wantedUnits < room ? wantedUnits : room;
    }

    public bool IsFullAt(int materialStock) => HasLimit && SpaceLeftWith(materialStock) <= 0;
}
