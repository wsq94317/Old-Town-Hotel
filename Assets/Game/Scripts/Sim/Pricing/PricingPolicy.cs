using System.Collections.Generic;

// 定价系统（架构 B.4）。纯 C#，不引用 UnityEngine。
// 设计铁律（spec §9.4）：**玩家选策略，不填数字**——手机上手动调未来 14 天每一格价格
// 是表格模拟器，不是游戏。玩家只碰模板 + 少量日期覆盖。
public enum PriceTemplate
{
    Clearance,     // 清仓价：拉满入住率，换来预算客与额外清洁负担
    Conservative,  // 保守价：稳
    Market,        // 市场价：参考价本身（= tier 表，UI 上呈现为"系统建议价"）
    Squeeze        // 榨利润：高利润，但抬高客人期待、差评概率升
}

/// <summary>各档位参考价的纯数据 DTO。ScriptableObject 只在场景宿主里转成它注入 Sim。</summary>
public readonly struct RoomRateTable
{
    private readonly int _old, _basic, _better;

    public RoomRateTable(int old, int basic, int better)
    {
        _old = old; _basic = basic; _better = better;
    }

    public int For(RoomTier tier)
    {
        switch (tier)
        {
            case RoomTier.Better: return _better;
            case RoomTier.Basic: return _basic;
            default: return _old;
        }
    }

    /// <summary>M-C 调参：v1 的 80/110/190 让"翻新"回本要 40+ 天，装修从核心玩法变成陷阱
    /// （首轮试玩实测：装修路线 20 天现金比什么都不干少一半）。档位差价拉开到接近现实
    /// ——翻新过的房本就该贵得多，这样装修才是真正的成长杠杆。</summary>
    public static RoomRateTable Default => new RoomRateTable(80, 130, 220);
}

public sealed class PricingPolicy
{
    public const float ClearanceMultiplier = 0.70f;
    public const float ConservativeMultiplier = 0.90f;
    public const float MarketMultiplier = 1.00f;
    public const float SqueezeMultiplier = 1.25f;
    public const float WeekendMultiplier = 1.15f;

    private readonly RoomRateTable _rates;
    private readonly Dictionary<int, PriceTemplate> _overrides = new Dictionary<int, PriceTemplate>();

    public PricingPolicy(RoomRateTable rates)
    {
        _rates = rates;
    }

    public PriceTemplate DefaultTemplate { get; set; } = PriceTemplate.Market;

    /// <summary>未来 N 天里被玩家单独覆盖的日期（周末/特殊日）。</summary>
    public IReadOnlyDictionary<int, PriceTemplate> Overrides => _overrides;

    public void SetOverride(int day, PriceTemplate template) => _overrides[day] = template;

    public void ClearOverride(int day) => _overrides.Remove(day);

    public void ClearAllOverrides() => _overrides.Clear();

    public PriceTemplate TemplateFor(int day) =>
        _overrides.TryGetValue(day, out PriceTemplate t) ? t : DefaultTemplate;

    /// <summary>第 1 天算周一；周末 = 第 6、7 天（以及每 7 天一轮）。</summary>
    public static bool IsWeekend(int day)
    {
        int dow = ((day - 1) % 7 + 7) % 7; // 0=周一 … 6=周日
        return dow >= 5;
    }

    public static float MultiplierOf(PriceTemplate template)
    {
        switch (template)
        {
            case PriceTemplate.Clearance: return ClearanceMultiplier;
            case PriceTemplate.Conservative: return ConservativeMultiplier;
            case PriceTemplate.Squeeze: return SqueezeMultiplier;
            default: return MarketMultiplier;
        }
    }

    /// <summary>相对市场价的价格比（需求弹性与"期待惩罚"的输入）。含周末溢价。</summary>
    public float PriceRatioFor(int day)
    {
        float ratio = MultiplierOf(TemplateFor(day));
        if (IsWeekend(day)) ratio *= WeekendMultiplier;
        return ratio;
    }

    /// <summary>某天某档位的实际房价。</summary>
    public int PriceFor(int day, RoomTier tier)
    {
        float price = _rates.For(tier) * PriceRatioFor(day);
        int rounded = SimMath.RoundToInt(price);
        return rounded < 1 ? 1 : rounded;
    }

    /// <summary>UI 文案（英文）。</summary>
    public static string LabelOf(PriceTemplate template)
    {
        switch (template)
        {
            case PriceTemplate.Clearance: return "CLEARANCE - fill every room, whoever shows up";
            case PriceTemplate.Conservative: return "CONSERVATIVE - play it safe";
            case PriceTemplate.Squeeze: return "SQUEEZE - charge what the sign says you're worth";
            default: return "MARKET - the going rate";
        }
    }
}
