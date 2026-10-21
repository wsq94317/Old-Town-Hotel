using System.Collections.Generic;
using UnityEngine;

public enum PortfolioHotelKind { Legacy, Value, Business, Resort }

public static class HotelEmpireProgress
{
    private static readonly List<PortfolioHotelKind> Hotels = new List<PortfolioHotelKind>();
    private static readonly List<string> SiteIds = new List<string>();
    public static int HotelCount => 1 + Hotels.Count;
    public static IReadOnlyList<PortfolioHotelKind> Properties => Hotels;
    public static int DistrictOwnedCount {
        get { int count=0; foreach(var site in HotelDistrictCatalog.Sites) if(IsOwned(site.Id)) count++; return count; }
    }
    public static bool IsOwned(string id) => id=="old-town" || SiteIds.Contains(id);
    public static bool IsUnlocked(HotelDistrictSite site)
    {
        if(site==null) return false;
        foreach(string id in site.Prerequisites) if(!IsOwned(id)) return false;
        return true;
    }
    public static HotelDistrictSite NextAvailableSite {
        get { foreach(var site in HotelDistrictCatalog.Sites)
            if(!IsOwned(site.Id) && IsUnlocked(site)) return site; return null; }
    }
    public static int AcquisitionCost(PortfolioHotelKind kind)
    {
        foreach(var site in HotelDistrictCatalog.Sites)
            if(!site.IsHome && site.Kind==kind && !IsOwned(site.Id)) return site.Price;
        return int.MaxValue;
    }
    public static int GrossOf(PortfolioHotelKind kind) => kind == PortfolioHotelKind.Business ? 1050
        : kind == PortfolioHotelKind.Resort ? 1600 : kind == PortfolioHotelKind.Legacy ? 960 : 720;
    public static int UpkeepOf(PortfolioHotelKind kind) => kind == PortfolioHotelKind.Business ? 350
        : kind == PortfolioHotelKind.Resort ? 750 : kind == PortfolioHotelKind.Legacy ? 0 : 240;
    public static int NextHotelCost => NextAvailableSite?.Price ?? int.MaxValue;
    public static int DailyIncome {
        get { int value = 0; foreach (var kind in Hotels) value += GrossOf(kind) - UpkeepOf(kind); return value; }
    }
    public static float PassiveIncomePerMinute => DailyIncome / HotelEconomyPresentation.RealMinutesPerGameDay;
    public static int IncomeForDay(int day)
    {
        int value = DailyIncome;
        for (int i = 0; i < Hotels.Count; i++) if (Hotels[i] == PortfolioHotelKind.Resort)
            value += ((day + i * 3) % 5 - 2) * 150;
        return value;
    }
    public static PortfolioHotelKind KindAt(string id)
    {
        int index=SiteIds.IndexOf(id);
        return index>=0 ? Hotels[index] : HotelDistrictCatalog.Find(id)?.Kind ?? PortfolioHotelKind.Legacy;
    }
    public static int IncomeAt(string id, int day)
    {
        int index=SiteIds.IndexOf(id);
        if(index<0) return 0;
        var kind=Hotels[index];
        return GrossOf(kind)-UpkeepOf(kind)+(kind==PortfolioHotelKind.Resort ? ((day+index*3)%5-2)*150 : 0);
    }
    public static bool TryAcquireNextHotel(HotelSim sim, out string message) =>
        TryAcquireSite(sim, NextAvailableSite?.Id, out message);
    public static bool TryAcquire(HotelSim sim, PortfolioHotelKind kind, out string message)
    {
        if(kind==PortfolioHotelKind.Legacy) kind=PortfolioHotelKind.Value;
        foreach(var site in HotelDistrictCatalog.Sites)
            if(site.Kind==kind && !IsOwned(site.Id) && IsUnlocked(site))
                return TryAcquireSite(sim,site.Id,out message);
        message=GameText.UseChinese ? "先收购地图上的前置酒店。" : "Acquire the preceding hotels on the map first.";
        return false;
    }
    public static bool TryAcquireSite(HotelSim sim, string id, out string message)
    {
        var site=HotelDistrictCatalog.Find(id);
        if(site==null) { message=GameText.UseChinese ? "街区酒店已全部收购。" : "District portfolio complete."; return false; }
        if(IsOwned(id)) { message=GameText.UseChinese ? "这家酒店已经属于你。" : "You already own this hotel."; return false; }
        if(!IsUnlocked(site)) { message=GameText.UseChinese ? "先收购地图上的前置酒店。" : "Acquire the preceding hotels first."; return false; }
        if(sim==null || !sim.TrySpendCash(site.Price)) {
            message=GameText.UseChinese ? "可用现金不足，还需要 $"+(sim==null ? site.Price : Mathf.Max(0,site.Price-sim.Cash))
                : "Insufficient cash. Price $"+site.Price; return false;
        }
        Hotels.Add(site.Kind); SiteIds.Add(id);
        message=GameText.UseChinese ? site.Name+"收购成功，驻店经理已接手经营！"
            : site.EnglishName+" acquired. Your resident manager is now operating it!";
        return true;
    }
    public static void CaptureTo(WorldState world)
    {
        if(world==null) return;
        world.hotelCount=HotelCount;
        world.portfolioKinds=new List<int>();
        foreach(var kind in Hotels) world.portfolioKinds.Add((int)kind);
        world.portfolioSiteIds=new List<string>(SiteIds);
    }
    public static void RestoreFrom(WorldState world)
    {
        ResetForNewGame();
        if(world==null) return;
        if(world.portfolioKinds!=null && world.portfolioKinds.Count>0)
            foreach(int kind in world.portfolioKinds) Hotels.Add((PortfolioHotelKind)Mathf.Clamp(kind,0,3));
        else for(int i=1;i<Mathf.Max(1,world.hotelCount);i++) Hotels.Add(PortfolioHotelKind.Legacy);
        // Older portfolios had only types/counts. Give them map locations without changing their income.
        for(int i=0;i<Hotels.Count;i++) {
            string id=world.portfolioSiteIds!=null && i<world.portfolioSiteIds.Count ? world.portfolioSiteIds[i] : null;
            var existing=HotelDistrictCatalog.Find(id);
            if(existing==null || existing.IsHome || SiteIds.Contains(id)) {
                id=null;
                foreach(var site in HotelDistrictCatalog.Sites)
                    if(!site.IsHome && !SiteIds.Contains(site.Id) && (Hotels[i]==PortfolioHotelKind.Legacy || site.Kind==Hotels[i])) { id=site.Id; break; }
                if(id==null) foreach(var site in HotelDistrictCatalog.Sites)
                    if(!site.IsHome && !SiteIds.Contains(site.Id)) { id=site.Id; break; }
            }
            SiteIds.Add(id ?? "legacy-"+i);
        }
    }
    public static void ResetForNewGame() { Hotels.Clear(); SiteIds.Clear(); }
}