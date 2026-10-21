using System.Collections.Generic;

public enum HotelOperatingStyle { Value, Business, Boutique }

public sealed partial class HotelSim
{
    public HotelOperatingStyle OperatingStyle { get; private set; } = HotelOperatingStyle.Business;
    public bool ClearRuinsFirst { get; set; }
    public bool MatchesOperatingStyle(HotelOperatingStyle style)
    {
        var rate = style == HotelOperatingStyle.Value ? PriceTemplate.Conservative
            : style == HotelOperatingStyle.Business ? PriceTemplate.Market : PriceTemplate.Squeeze;
        return Pricing.DefaultTemplate == rate
            && Shifts.TierOf(StaffRole.Reception) == (style == HotelOperatingStyle.Value ? ShiftTier.Lean : ShiftTier.Full)
            && Shifts.TierOf(StaffRole.Housekeeper) == (style == HotelOperatingStyle.Boutique ? ShiftTier.Full : ShiftTier.Normal)
            && Shifts.TierOf(StaffRole.Inspector) == (style == HotelOperatingStyle.Value ? ShiftTier.Lean : ShiftTier.Full);
    }

    public void ApplyOperatingStyle(HotelOperatingStyle style)
    {
        OperatingStyle = style;
        Pricing.DefaultTemplate = style == HotelOperatingStyle.Value ? PriceTemplate.Conservative
            : style == HotelOperatingStyle.Business ? PriceTemplate.Market : PriceTemplate.Squeeze;
        Shifts.SetTier(StaffRole.Reception, style == HotelOperatingStyle.Value ? ShiftTier.Lean : ShiftTier.Full);
        Shifts.SetTier(StaffRole.Housekeeper, style == HotelOperatingStyle.Boutique ? ShiftTier.Full : ShiftTier.Normal);
        Shifts.SetTier(StaffRole.Inspector, style == HotelOperatingStyle.Value ? ShiftTier.Lean : ShiftTier.Full);
        Shifts.ApplyTo(Staff);
    }

    public static int[] PackageFor(HotelOperatingStyle style)
    {
        switch (style) {
            case HotelOperatingStyle.Value: return new[] { FurnitureCatalog.ProperBed, FurnitureCatalog.BasicBathroom };
            case HotelOperatingStyle.Business: return new[] { FurnitureCatalog.ProperBed,
                FurnitureCatalog.RenovatedBathroom, FurnitureCatalog.WorkDesk, FurnitureCatalog.BoxyTv };
            default: return new[] { FurnitureCatalog.MemoryFoamBed, FurnitureCatalog.RenovatedBathroom,
                FurnitureCatalog.BigFlatTv, FurnitureCatalog.Sofa, FurnitureCatalog.Rug, FurnitureCatalog.WallArt };
        }
    }

    public int QuoteRoomPackage(int roomNumber, HotelOperatingStyle style)
    {
        int cost = 0;
        foreach (int kind in PackageFor(style)) {
            bool present = false;
            foreach (var item in Furniture.InRoom(roomNumber)) if (item.kindId == kind) present = true;
            if (!present) cost += FurnitureCatalog.Get(kind).cashCost;
        }
        return cost; // Conservative upfront requirement; salvage is refunded separately.
    }

    public bool TryApplyRoomPackage(int roomNumber, HotelOperatingStyle style, out string reason)
    {
        reason = "";
        if (!Rooms.Contains(roomNumber) || Rooms.At(roomNumber).state == RoomSimState.Ruined
            || Rooms.At(roomNumber).state == RoomSimState.Occupied || Renovations.IsRenovating(roomNumber)) {
            reason = "Choose an empty, open room without construction."; return false;
        }
        var optionalSlots = new HashSet<FurnitureSlot>();
        foreach (var item in Furniture.InRoom(roomNumber)) {
            var kind = FurnitureCatalog.Get(item.kindId);
            if (!kind.IsRequired) optionalSlots.Add(kind.slot);
        }
        foreach (int id in PackageFor(style)) {
            var kind = FurnitureCatalog.Get(id);
            if (!kind.IsRequired) optionalSlots.Add(kind.slot);
        }
        if (optionalSlots.Count > FurnitureCatalog.OptionalSlotCount) {
            reason = "Remove an optional decoration to make room for the package."; return false;
        }
        int quoted = QuoteRoomPackage(roomNumber, style);
        if (Cash < quoted) { reason = "Not enough cash for the full package ($" + quoted + ")."; return false; }
        foreach (int id in PackageFor(style)) {
            bool present = false;
            foreach (var item in Furniture.InRoom(roomNumber)) if (item.kindId == id) present = true;
            if (!present && !TryBuyFurniture(roomNumber, id, out reason)) return false;
        }
        float quality = DeliveredQualityOf(roomNumber);
        SetPriceBand(roomNumber, quality >= DemandModel.ExpectedQualityOf(RoomTier.Better)
            ? RoomTier.Better : quality >= DemandModel.ExpectedQualityOf(RoomTier.Basic) ? RoomTier.Basic : RoomTier.Old);
        return true;
    }

    public bool CanRenovateRoom(int number, RenovationPlanKind plan)
    {
        if (!Rooms.Contains(number)) return false;
        var room = Rooms.At(number);
        return room.state != RoomSimState.Ruined && room.state != RoomSimState.Occupied
            && !Renovations.IsRenovating(number) && RenovationWouldHelp(plan, number);
    }
}
