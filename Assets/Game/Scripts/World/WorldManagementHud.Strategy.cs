using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class WorldManagementHud
{
    private int _batchFloor = 1;
    private int _batchSize = 2;
    private RenovationPlanKind _batchPlan = RenovationPlanKind.Economy;

    private string StyleName(HotelOperatingStyle style) => style == HotelOperatingStyle.Value
        ? L("VALUE & TURNOVER", "平价周转") : style == HotelOperatingStyle.Business
        ? L("BUSINESS & EFFICIENCY", "商务效率") : L("BOUTIQUE & COMFORT", "精品舒适");

    private void BuildRoomPriceBands(RoomRecord room)
    {
        if (room.state == RoomSimState.Ruined) return;
        float delivered = Sim.DeliveredQualityOf(room.number);
        AddSection(L("ROOM PRICE PROMISE", "客房挂牌档与品质承诺"),
            L("Delivered quality ", "实际交付品质 ") + delivered.ToString("P0")
            + L(" · new bookings only; existing orders keep their locked rate", " · 只影响新订单，已有订单保留锁定房价"));
        foreach (RoomTier band in Enum.GetValues(typeof(RoomTier))) {
            var captured = band;
            float expected = DemandModel.ExpectedQualityOf(band);
            bool meets = delivered + 0.01f >= expected;
            AddPlanButton(GameText.T(band.ToString()) + "  $" + Sim.Pricing.PriceFor(Sim.Clock.CurrentDay, band)
                + L("/night", "/晚") + (room.tier == band ? L(" · ACTIVE", " · 当前挂牌") : ""),
                L("Expected quality ", "承诺品质 ") + expected.ToString("P0")
                + (meets ? L(" · room meets this promise", " · 房间符合此档预期")
                    : L(" · quality gap: rating/refund risk", " · 品质存在落差，可能差评或退款")),
                () => {
                    Sim.SetPriceBand(room.number, captured);
                    ShowToast(L("Room price band updated for new bookings.", "客房挂牌已更新，仅对新预订生效。"));
                    FindRoomBinder(room.number)?.Pulse(1.5f);
                    ForceAllRefresh();
                }, room.tier == band ? Gold : meets ? Teal : Coral, !Sim.Renovations.IsRenovating(room.number));
        }
    }

    private void BuildOperatingStyles()
    {
        AddSection(L("HOW WILL YOU RUN THIS HOTEL?", "这家酒店走哪条路线？"),
            L("Applies pricing and shifts together. Furniture is purchased separately; no free staff or rooms.",
                "一起调整定价和排班；家具须另行购买，不会免费增加员工或房间。"));
        foreach (HotelOperatingStyle style in Enum.GetValues(typeof(HotelOperatingStyle))) {
            var captured = style;
            string description = style == HotelOperatingStyle.Value
                ? L("90% rates · lean reception/inspection · more price-sensitive guests and cleaning work",
                    "90% 房价 · 精简前台/验房 · 价格敏感客更多，周转与清洁压力更高")
                : style == HotelOperatingStyle.Business
                ? L("Market rates · full reception/inspection · invest in desks and reliable bathrooms",
                    "市场价 · 全员前台/验房 · 配置书桌与可靠卫浴，优先减少排队")
                : L("125% rates · full team · high guest expectations; furnish rooms before raising their band",
                    "125% 房价 · 全员班 · 客人期待高，先改善品质再提高挂牌档");
            AddPlanButton(StyleName(style) + (Sim.MatchesOperatingStyle(style) ? L(" · ACTIVE", " · 当前") : ""), description, () => {
                Sim.ApplyOperatingStyle(captured);
                HotelSimSceneBridge.Instance?.ApplyShiftPlan();
                ShowToast(L("Operating plan applied. Check staffing and room quality before the rush.",
                    "经营方案已执行，请在入住高峰前检查实际人手与房间品质。"));
                ForceAllRefresh();
            }, Sim.MatchesOperatingStyle(style) ? Gold : Ink, true);
        }
    }

    private string PriceForecast(PriceTemplate template)
    {
        bool weekend = PricingPolicy.IsWeekend(Sim.Clock.CurrentDay);
        float ratio = PricingPolicy.MultiplierOf(template) * (weekend ? PricingPolicy.WeekendMultiplier : 1f);
        int requests = DemandModel.ArrivalsFor(Sim.DemandCfg, Sim.Rooms.OpenRoomCount,
            Sim.Reputation.Stars, ratio, weekend, 0.5, Sim.AverageDeliveredQuality());
        var mix = DemandModel.SegmentMixFor(ratio, weekend);
        return L("Model requests ", "模型日需求 ") + requests
            + L(" · budget ", " · 预算客 ") + mix.budget.ToString("P0")
            + L(" · VIP ", " · VIP ") + mix.vip.ToString("P0");
    }

    private void BuildRoomPackages(RoomRecord room)
    {
        AddSection(L("ROOM PACKAGES · VISIBLE FURNITURE", "客群套餐 · 实际更换场景家具"),
            L("Existing matching furniture stays. Quote is before salvage refunds; the price band follows delivered quality.",
                "保留已匹配的家具；报价未扣旧物回收款，完成后按交付品质调整挂牌档。"));
        foreach (HotelOperatingStyle style in Enum.GetValues(typeof(HotelOperatingStyle))) {
            var captured = style;
            int quote = Sim.QuoteRoomPackage(room.number, style);
            string items = style == HotelOperatingStyle.Value ? L("Proper bed + basic bathroom", "标准床 + 基础卫浴")
                : style == HotelOperatingStyle.Business ? L("Proper bed + renovated bathroom + desk + TV", "标准床 + 新卫浴 + 书桌 + 电视")
                : L("Premium bed + new bathroom + large TV + sofa + rug + art", "高档床 + 新卫浴 + 大电视 + 沙发 + 地毯 + 挂画");
            AddPlanButton(StyleName(style) + "  $" + quote,
                items + "\n" + L("Cash after purchase $", "购买后现金 $") + (Sim.Cash - quote)
                + L(" · unpaid wages $", " · 待付工资 $") + Sim.Payroll.Owed,
                () => {
                    bool ok = Sim.TryApplyRoomPackage(room.number, captured, out string reason);
                    ShowToast(ok ? L("Furniture delivered. Review the room in the hotel.", "家具已送达，可在场景中查看新的房间配置。") : GameText.T(reason));
                    if (ok) { FindRoomBinder(room.number)?.Pulse(2f); ShowMoneyBurst(L("ROOM UPDATED", "客房配置已更新"), Gold); }
                    ForceAllRefresh();
                }, Gold, quote > 0 && Sim.Cash >= quote && room.state != RoomSimState.Occupied
                    && room.state != RoomSimState.Ruined && !Sim.Renovations.IsRenovating(room.number));
        }
    }

    private List<int> BatchCandidates()
    {
        var result = new List<int>();
        for (int i = 0; i < Sim.Rooms.Count && result.Count < _batchSize; i++) {
            var room = Sim.Rooms.Peek(i);
            if (room.floor == _batchFloor && Sim.CanRenovateRoom(room.number, _batchPlan)) result.Add(room.number);
        }
        return result;
    }

    private void BuildBatchRenovation()
    {
        AddSection(L("PLAN A FLOOR RENOVATION", "楼层批量装修计划"),
            L("Only empty eligible rooms are selected. Existing bookings remain your responsibility.",
                "仅选取空置且适用的房间；已有预订不会因施工自动取消。"));
        int maxFloor = 1;
        for (int i = 0; i < Sim.Rooms.Count; i++) maxFloor = Mathf.Max(maxFloor, Sim.Rooms.Peek(i).floor);
        int lastFloor = maxFloor;
        AddActionButton(L("FLOOR ", "楼层 ") + (_batchFloor + 1) + "F" + L(" · CHANGE", " · 点击切换"), () => {
            _batchFloor = _batchFloor >= lastFloor ? 1 : _batchFloor + 1; ForceDrawerRefresh();
        }, Ink, true);
        AddActionButton(L("MAX ROOMS ", "最多房间 ") + _batchSize + " · " + GameText.T(RenovationPlan.LabelOf(_batchPlan)), () => {
            int floorCount = 0;
            for (int i = 0; i < Sim.Rooms.Count; i++) if (Sim.Rooms.Peek(i).floor == _batchFloor) floorCount++;
            if (_batchSize < floorCount) _batchSize = Mathf.Min(_batchSize * 2, floorCount);
            else { _batchSize = 1; _batchPlan = (RenovationPlanKind)(((int)_batchPlan + 1) % 3); }
            ForceDrawerRefresh();
        }, Ink, true);
        var selected = BatchCandidates();
        var plan = RenovationPlan.For(_batchPlan);
        int cost = Sim.QuoteRenovation(_batchPlan, selected.Count);
        int materials = RenovationPricing.MaterialCostFor(plan, selected.Count);
        int days = RenovationPricing.BlockDaysFor(plan, selected.Count);
        int atRisk = 0;
        for (int d = 0; d < days; d++) {
            var night = Sim.OccupancyForNight(Sim.Clock.CurrentDay + d);
            atRisk += Mathf.Max(0, night.sold - Mathf.Max(0, night.capacity - selected.Count));
        }
        AddInfoCard(L("BEFORE YOU COMMIT", "开工前看清代价"),
            L("Rooms: ", "房号：") + string.Join(", ", selected)
            + "\n$" + cost + " + " + materials + L(" materials · closed ", " 材料 · 停业 ") + days + L(" days", " 天")
            + "\n" + L("Potential overbooked nights: ", "可能超售间夜：") + atRisk
            + L(" · cash left $", " · 剩余现金 $") + (Sim.Cash - cost)
            + L(" · unpaid wages $", " · 待付工资 $") + Sim.Payroll.Owed,
            atRisk > 0 ? Coral : Gold, 128f);
        AddActionButton(L("START THIS BATCH  $", "按此计划开工  $") + cost, () => {
            bool ok = Sim.TryStartRenovation(_batchPlan, selected, out string reason);
            ShowToast(ok ? L("Construction started. These rooms are off sale until handover.", "批量施工已开工，这些房间在交付前停止销售。") : GameText.T(reason));
            if (ok) foreach (int number in selected) FindRoomBinder(number)?.Pulse(2f);
            ForceAllRefresh();
        }, atRisk > 0 ? Coral : Gold, selected.Count > 0 && Sim.Cash >= cost && Sim.Materials.Stock >= materials);
    }
}
