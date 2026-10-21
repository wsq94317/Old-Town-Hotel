using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class WorldManagementHud
{
    private RectTransform _empireMap;
    private bool _empireMapOpen;
    private string _empireSelectedSite = "station", _empireMapStateKey;
    private TextMeshProUGUI _empireStats, _empireTitle, _empireDescription, _empireIncome, _empireCondition, _empireActionLabel;
    private Button _empireAction;
    private HotelDistrictRoutes _empireRoutes;
    private readonly Dictionary<string, EmpireMarker> _empireMarkers = new Dictionary<string, EmpireMarker>();

    private sealed class EmpireMarker
    {
        public Image Surface;
        public TextMeshProUGUI Name, Status;
        public HotelMapMarkerPulse Pulse;
    }

    private void OpenEmpireMap()
    {
        if (Sim == null) return;
        if (_empireMap == null) BuildEmpireMap();
        _tab = DeskTab.Assets;
        _empireMapOpen = true;
        _empireMapStateKey = null;
        SetDrawerOpen(true);
    }

    private void BuildEmpireMap()
    {
        _empireMap = CreatePanel("EmpireDistrictMap", _safeRoot, RevivalPaper);
        SetAnchors(_empireMap, Vector2.zero, Vector2.one, new Vector2(10,88), new Vector2(-10,-10));
        _empireMap.SetSiblingIndex(_drawer.GetSiblingIndex()+1);
        AddOutline(_empireMap, BrassLine, 1.2f);

        var title = CreateText("DistrictTitle", _empireMap, 22, RevivalInk, TextAlignmentOptions.MidlineLeft, true);
        title.text = L("THE OLD TOWN", "老城 · 酒店帝国");
        SetAnchors(title.rectTransform, Vector2.up, Vector2.one, new Vector2(14,-41),new Vector2(-144,-7));
        var subtitle = CreateText("DistrictSubtitle",_empireMap,10,Slate,TextAlignmentOptions.MidlineLeft,false);
        subtitle.text = L("One neighbourhood. Seven hotels. Your story.","从一家老店，走向整座街区。点击酒店查看与收购。");
        SetAnchors(subtitle.rectTransform,Vector2.up,Vector2.one,new Vector2(14,-63),new Vector2(-10,-41));
        _empireStats=CreateText("DistrictStats",_empireMap,11,Teal,TextAlignmentOptions.MidlineLeft,true);
        SetAnchors(_empireStats.rectTransform,Vector2.up,Vector2.one,new Vector2(14,-88),new Vector2(-10,-64));

        RectTransform rect; TextMeshProUGUI label;
        CreateButton("HomeInvestment",_empireMap,out rect,out label,L("HOME","本店投资"),Teal,OpenAssetsForSelectedRoom);
        SetAnchors(rect,Vector2.one,Vector2.one,new Vector2(-136,-41),new Vector2(-48,-9)); label.fontSize=11;
        CreateButton("CloseDistrict",_empireMap,out rect,out label,"×",Walnut,()=>SetDrawerOpen(false));
        SetAnchors(rect,Vector2.one,Vector2.one,new Vector2(-41,-41),new Vector2(-9,-9)); label.fontSize=22;
        label.gameObject.SetActive(false); AddCloseGlyph(rect);

        var viewport=CreatePanel("DistrictMapViewport",_empireMap,Hex("#D6D2AE"));
        SetAnchors(viewport,Vector2.zero,Vector2.one,new Vector2(8,234),new Vector2(-8,-96));
        viewport.gameObject.AddComponent<RectMask2D>();
        var map=CreateRect("DistrictMapArtwork",viewport); Stretch(map);
        var texture=Resources.Load<Texture2D>("HotelArt/Empire/OldTownDistrict");
        var art=map.gameObject.AddComponent<RawImage>(); art.texture=texture; art.raycastTarget=false;
        var fit=map.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio=texture!=null ? (float)texture.width/texture.height : .8f;
        var routes=CreateRect("ExpansionRoutes",map); Stretch(routes);
        _empireRoutes=routes.gameObject.AddComponent<HotelDistrictRoutes>(); _empireRoutes.raycastTarget=false;
        foreach(var site in HotelDistrictCatalog.Sites) BuildEmpireMarker(site,map);

        var legend=CreateText("MapLegend",_empireMap,10,Slate,TextAlignmentOptions.Center,false);
        legend.text=L("● OWNED    ◆ AVAILABLE    ○ LOCKED", "<color=#2E6D61>● 已拥有</color>　<color=#94651E>◆ 可收购</color>　<color=#6E685E>○ 待解锁</color>");
        SetAnchors(legend.rectTransform,Vector2.zero,Vector2.right,new Vector2(4,214),new Vector2(-4,234));

        var card=CreatePanel("SelectedDistrictHotel",_empireMap,Hex("#F1E5CB"));
        SetAnchors(card,Vector2.zero,Vector2.right,new Vector2(8,8),new Vector2(-8,210));
        AddOutline(card,BrassLine,1);
        _empireTitle=CreateText("HotelName",card,19,RevivalInk,TextAlignmentOptions.MidlineLeft,true);
        SetAnchors(_empireTitle.rectTransform,Vector2.up,Vector2.one,new Vector2(12,-34),new Vector2(-12,-5));
        _empireDescription=CreateText("HotelDescription",card,11,Slate,TextAlignmentOptions.TopLeft,false);
        SetAnchors(_empireDescription.rectTransform,Vector2.up,Vector2.one,new Vector2(12,-68),new Vector2(-12,-35));
        _empireDescription.textWrappingMode=TextWrappingModes.Normal;
        _empireIncome=CreateText("HotelIncome",card,12,Teal,TextAlignmentOptions.MidlineLeft,true);
        SetAnchors(_empireIncome.rectTransform,Vector2.zero,Vector2.right,new Vector2(12,94),new Vector2(-12,130));
        _empireIncome.textWrappingMode=TextWrappingModes.Normal;
        _empireCondition=CreateText("HotelUnlockCondition",card,11,RevivalInk,TextAlignmentOptions.MidlineLeft,false);
        SetAnchors(_empireCondition.rectTransform,Vector2.zero,Vector2.right,new Vector2(12,50),new Vector2(-12,93));
        _empireCondition.textWrappingMode=TextWrappingModes.Normal;
        _empireAction=CreateButton("AcquireDistrictHotel",card,out rect,out _empireActionLabel,"",Teal,AcquireSelectedDistrictHotel);
        SetAnchors(rect,Vector2.zero,Vector2.right,new Vector2(10,8),new Vector2(-10,47));
        _empireActionLabel.fontSize=13;
        _empireMap.gameObject.SetActive(false);
    }

    private void BuildEmpireMarker(HotelDistrictSite site, Transform parent)
    {
        RectTransform rect; TextMeshProUGUI label;
        var button=CreateButton("HotelPin_"+site.Id,parent,out rect,out label,"",Teal,()=>SelectDistrictHotel(site.Id));
        SetAnchors(rect,site.MapPosition,site.MapPosition,new Vector2(-49,-22),new Vector2(49,22));
        label.gameObject.SetActive(false);
        var ring=CreatePanel("SelectionRing",rect,Color.clear);
        Stretch(ring,-4); ring.SetAsFirstSibling(); ring.GetComponent<Image>().raycastTarget=false;
        AddEmpireSelectionEdge(ring,Vector2.up,Vector2.one,new Vector2(0,-2),Vector2.zero);
        AddEmpireSelectionEdge(ring,Vector2.zero,Vector2.right,Vector2.zero,new Vector2(0,2));
        AddEmpireSelectionEdge(ring,Vector2.zero,Vector2.up,Vector2.zero,new Vector2(2,0));
        AddEmpireSelectionEdge(ring,Vector2.right,Vector2.one,new Vector2(-2,0),Vector2.zero);
        var pulse=ring.gameObject.AddComponent<HotelMapMarkerPulse>();
        var name=CreateText("PinName",rect,12,Cream,TextAlignmentOptions.Center,true);
        SetAnchors(name.rectTransform,new Vector2(0,.36f),Vector2.one,new Vector2(3,0),new Vector2(-3,-2));
        name.text=L(site.EnglishName,site.Name); name.enableAutoSizing=true;name.fontSizeMin=8;name.fontSizeMax=12;
        var status=CreateText("PinState",rect,9,Cream,TextAlignmentOptions.Center,false);
        SetAnchors(status.rectTransform,Vector2.zero,new Vector2(1,.4f),new Vector2(2,1),new Vector2(-2,0));
        _empireMarkers.Add(site.Id,new EmpireMarker { Surface=button.GetComponent<Image>(),Name=name,Status=status,Pulse=pulse });
    }

    private void SelectDistrictHotel(string id)
    {
        if(HotelDistrictCatalog.Find(id)==null) return;
        _empireSelectedSite=id; _empireMapStateKey=null;
        RefreshEmpireMap();
    }

    private static void AddEmpireSelectionEdge(RectTransform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        var edge=CreatePanel("SelectionEdge",parent,Hex("#FFE4A0"));
        SetAnchors(edge,min,max,offsetMin,offsetMax); edge.GetComponent<Image>().raycastTarget=false;
    }

    private void RefreshEmpireMap()
    {
        if(_empireMap==null || Sim==null) return;
        string key=_empireSelectedSite+"|"+Sim.Cash+"|"+Sim.Clock.CurrentDay+"|"+HotelEmpireProgress.HotelCount+"|"+GameText.UseChinese;
        if(key==_empireMapStateKey) return;
        _empireMapStateKey=key;
        _empireStats.text=L("OWNED ","已拥有 ")+HotelEmpireProgress.DistrictOwnedCount+" / "+HotelDistrictCatalog.Sites.Count
            +L("   ·   CASH $","   ·   现金 $")+Sim.Cash.ToString("N0")
            +L("   ·   NET +$","   ·   托管 +$")+HotelEmpireProgress.IncomeForDay(Sim.Clock.CurrentDay)+L("/day","/日");
        _empireStats.enableAutoSizing=true; _empireStats.fontSizeMin=8; _empireStats.fontSizeMax=11;
        foreach(var site in HotelDistrictCatalog.Sites) {
            var marker=_empireMarkers[site.Id];
            bool owned=HotelEmpireProgress.IsOwned(site.Id), unlocked=HotelEmpireProgress.IsUnlocked(site);
            marker.Surface.color=owned ? Teal : unlocked ? Gold : Hex("#716F62");
            marker.Name.color=owned || !unlocked ? Cream : Hex("#312918");
            marker.Status.color=marker.Name.color;
            marker.Status.text=owned ? (site.IsHome ? L("HOME · OWNED","本店 · 已拥有") : L("OPERATING","已拥有 · 经营中"))
                : unlocked ? L("BUY $","收购 $")+ShortMapMoney(site.Price) : L("LOCKED","待解锁");
            marker.Pulse.Selected=site.Id==_empireSelectedSite;
        }
        _empireRoutes.SetVerticesDirty();
        var selected=HotelDistrictCatalog.Find(_empireSelectedSite) ?? HotelDistrictCatalog.Sites[0];
        bool isOwned=HotelEmpireProgress.IsOwned(selected.Id), isUnlocked=HotelEmpireProgress.IsUnlocked(selected);
        _empireTitle.text=L(selected.EnglishName,selected.Name)+" · "+(selected.IsHome ? L("HOME","本店") : PropertyName(HotelEmpireProgress.KindAt(selected.Id)));
        _empireTitle.enableAutoSizing=true;_empireTitle.fontSizeMin=13;_empireTitle.fontSizeMax=19;
        _empireDescription.text=L(selected.EnglishDescription,selected.Description);
        if(selected.IsHome) {
            _empireIncome.text=L("OPEN ROOMS ","营业客房 ")+Sim.Rooms.OpenRoomCount+" / "+Sim.Rooms.Count;
            _empireCondition.text=L("Operate and restore this hotel to fund your district expansion.","修复与经营本店，积累收购下一家酒店的资金。");
            _empireActionLabel.text=L("MANAGE HOME HOTEL","本店装修与设施"); _empireAction.interactable=true;
        }
        else {
            var kind=HotelEmpireProgress.KindAt(selected.Id);
            int net=isOwned ? HotelEmpireProgress.IncomeAt(selected.Id,Sim.Clock.CurrentDay)
                : HotelEmpireProgress.GrossOf(kind)-HotelEmpireProgress.UpkeepOf(kind);
            _empireIncome.text=L("NET +$","日净回款 +$")+net+L("/day   ·   upkeep $","　·　日运营费 $")+HotelEmpireProgress.UpkeepOf(kind)
                +(kind==PortfolioHotelKind.Resort ? L("\nSeasonal range: base ±$300/day","\n五日淡旺季：基准净回款 ±$300/日") : "");
            if(isOwned) {
                _empireCondition.text=L("Resident manager operating · receipts settle into your safe at day close.","驻店经理托管经营，净回款在日结时汇入保险箱。");
                _empireActionLabel.text=L("OWNED · OPERATING","已拥有 · 托管经营中");
            }
            else if(!isUnlocked) {
                var missing=new List<string>();
                foreach(string id in selected.Prerequisites) if(!HotelEmpireProgress.IsOwned(id)) {
                    var prior=HotelDistrictCatalog.Find(id); missing.Add(L(prior.EnglishName,prior.Name));
                }
                _empireCondition.text=L("UNLOCK: acquire ","解锁条件：先收购")+string.Join(L(" + ","、"),missing);
                _empireActionLabel.text=L("LOCKED · PRICE $","待解锁 · 收购价 $")+selected.Price.ToString("N0");
            }
            else {
                _empireCondition.text=Sim.Cash>=selected.Price
                    ? L("Cash after purchase $","收购后现金 $")+(Sim.Cash-selected.Price).ToString("N0")+L(" · instant delegated operation"," · 即时开始托管经营")
                    : L("Unlocked. Need $","已解锁，还需积累 $")+(selected.Price-Sim.Cash).ToString("N0")+L(" more cash."," 现金。");
                _empireActionLabel.text=L("ACQUIRE HOTEL  $","收购酒店  $")+selected.Price.ToString("N0");
            }
            _empireAction.interactable=!isOwned && isUnlocked && Sim.Cash>=selected.Price;
        }
        _empireAction.GetComponent<Image>().color=selected.IsHome || isOwned ? Teal : isUnlocked ? Gold : Slate;
        _empireActionLabel.color=selected.IsHome || isOwned || !isUnlocked ? Cream : Hex("#312918");
        _empireActionLabel.enableAutoSizing=true;_empireActionLabel.fontSizeMin=10;_empireActionLabel.fontSizeMax=13;
    }

    private void AcquireSelectedDistrictHotel()
    {
        var site=HotelDistrictCatalog.Find(_empireSelectedSite);
        if(site==null) return;
        if(site.IsHome) { OpenAssetsForSelectedRoom(); return; }
        if(!HotelEmpireProgress.TryAcquireSite(Sim,site.Id,out string message)) { ShowToast(message); return; }
        FindFirstObjectByType<SaveCoordinator>()?.RequestProgressAutosave();
        ShowToast(message); ShowMoneyBurst(L("HOTEL ACQUIRED  -$","新酒店开业  -$")+site.Price.ToString("N0"),Gold);
        _empireMarkers[site.Id].Pulse.Celebrate();
        StartCoroutine(BumpCash()); _empireMapStateKey=null;
        ForceAllRefresh(); RefreshEmpireMap();
    }

    private static string ShortMapMoney(int amount) => amount>=1000 ? (amount/1000f).ToString("0.#")+"k" : amount.ToString();
}
