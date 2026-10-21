using System.Collections.Generic;
using UnityEngine;

/// <summary>Stable property identities shared by the district map and save games.</summary>
public sealed class HotelDistrictSite
{
    public readonly string Id, Name, EnglishName, Description, EnglishDescription;
    public readonly PortfolioHotelKind Kind;
    public readonly int Price;
    public readonly Vector2 MapPosition;
    public readonly string[] Prerequisites;
    public bool IsHome => Id == "old-town";
    public HotelDistrictSite(string id, string name, string englishName, PortfolioHotelKind kind,
        int price, float x, float y, string description, string englishDescription, params string[] prerequisites)
    {
        Id=id; Name=name; EnglishName=englishName; Kind=kind; Price=price;
        MapPosition=new Vector2(x,y); Description=description; EnglishDescription=englishDescription;
        Prerequisites=prerequisites;
    }
}

public static class HotelDistrictCatalog
{
    private static readonly HotelDistrictSite[] Entries = {
        new HotelDistrictSite("old-town", "老城酒店", "Old Town Hotel", PortfolioHotelKind.Legacy,
            0, .255f,.28f, "你的第一家酒店。一切从这里开始。", "Your first hotel. Every empire starts here."),
        new HotelDistrictSite("station", "车站旅馆", "Station House", PortfolioHotelKind.Value,
            25000,.63f,.18f, "接待往来旅客，稳定的平价住宿生意。", "Reliable affordable stays for arriving travellers.", "old-town"),
        new HotelDistrictSite("market", "集市客栈", "Market Inn", PortfolioHotelKind.Value,
            45000,.23f,.51f, "市集人潮带来稳定客源，向老城腹地扩张。", "A dependable inn beside the bustling market.", "station"),
        new HotelDistrictSite("canal", "运河商旅", "Canal House", PortfolioHotelKind.Business,
            70000,.64f,.49f, "桥畔商务酒店，为商旅客提供安静的落脚点。", "A quiet business hotel beside the canal bridge.", "station"),
        new HotelDistrictSite("theatre", "剧院公馆", "Theatre House", PortfolioHotelKind.Business,
            110000,.36f,.73f, "剧院广场旁的精品商务酒店。", "A boutique business hotel on Theatre Square.", "market"),
        new HotelDistrictSite("garden", "花园温泉酒店", "Garden Retreat", PortfolioHotelKind.Resort,
            160000,.83f,.73f, "花园与温泉迎来度假客，回款随五日淡旺季变化。", "A garden spa retreat with a five-day seasonal cycle.", "canal"),
        new HotelDistrictSite("grand", "滨河大酒店", "Riverside Grand", PortfolioHotelKind.Resort,
            240000,.53f,.90f, "汇集两条扩张路线，拿下老城地标酒店。", "Unite both expansion routes and acquire the district landmark.", "theatre", "garden")
    };
    public static IReadOnlyList<HotelDistrictSite> Sites => Entries;
    public static HotelDistrictSite Find(string id)
    { foreach(var site in Entries) if(site.Id==id) return site; return null; }
}
