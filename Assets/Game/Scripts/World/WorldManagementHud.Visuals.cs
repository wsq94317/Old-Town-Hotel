using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class WorldManagementHud
{
    private static readonly Color RevivalPaper = Hex("#FFF4DA"), RevivalInk=Hex("#304D43");
    private RectTransform _menuCard;
    private static HotelGlyph Glyph(Transform parent,string kind,Color color,Vector2 min,Vector2 max)
    {
        var rect=CreateRect(kind+"Icon",parent);SetAnchors(rect,min,max,Vector2.zero,Vector2.zero);
        var glyph=rect.gameObject.AddComponent<HotelGlyph>();glyph.kind=kind;glyph.color=color;glyph.raycastTarget=false;return glyph;
    }
    private void BuildTop()
    {
        _topPanel=CreatePanel("EconomyHeader",_safeRoot,RevivalPaper);
        SetAnchors(_topPanel,Vector2.up,Vector2.one,new Vector2(10,-152),new Vector2(-10,-10));
        var brand=CreateText("Brand",_topPanel,10,Teal,TextAlignmentOptions.MidlineLeft,true);
        brand.text=L("OLD TOWN / FRONT DESK", "老城酒店 / 今日经营");
        SetAnchors(brand.rectTransform,new Vector2(0,.78f),new Vector2(.63f,1),new Vector2(14,0),new Vector2(0,-3));
        _dayTimeLabel=CreateText("DayTime",_topPanel,11,RevivalInk,TextAlignmentOptions.MidlineRight,true);
        SetAnchors(_dayTimeLabel.rectTransform,new Vector2(.45f,.78f),Vector2.one,Vector2.zero,new Vector2(-12,-3));
        _dayTimeLabel.enableAutoSizing=true;_dayTimeLabel.fontSizeMin=8;_dayTimeLabel.fontSizeMax=11;
        Glyph(_topPanel,"coin",Gold,new Vector2(.03f,.47f),new Vector2(.115f,.77f));
        _cashLabel=CreateText("Cash",_topPanel,24,RevivalInk,TextAlignmentOptions.MidlineLeft,true);
        SetAnchors(_cashLabel.rectTransform,new Vector2(.13f,.48f),new Vector2(.68f,.78f),Vector2.zero,Vector2.zero);
        _cashLabel.enableAutoSizing=true;_cashLabel.fontSizeMin=16;_cashLabel.fontSizeMax=24;
        _netRateLabel=CreateText("NetRate",_topPanel,11,Teal,TextAlignmentOptions.MidlineLeft,true);
        SetAnchors(_netRateLabel.rectTransform,new Vector2(.04f,.30f),new Vector2(.55f,.50f),Vector2.zero,Vector2.zero);
        _todayProgressLabel=CreateText("TodayProgress",_topPanel,10,Slate,TextAlignmentOptions.MidlineRight,true);
        SetAnchors(_todayProgressLabel.rectTransform,new Vector2(.49f,.30f),new Vector2(.96f,.50f),Vector2.zero,Vector2.zero);
        RectTransform safe;_safeboxButton=CreateButton("Safebox",_topPanel,out safe,out _safeboxLabel,"",Teal,CollectSafebox);
        SetAnchors(safe,new Vector2(.70f,.52f),new Vector2(.97f,.77f),Vector2.zero,Vector2.zero);
        _safeboxButtonImage=_safeboxButton.GetComponent<Image>();
        var safeTrack=CreatePanel("SafeTrack",safe,GoldDark);SetAnchors(safeTrack,Vector2.zero,new Vector2(1,0),new Vector2(5,2),new Vector2(-5,4));
        _safeboxFill=CreatePanel("SafeFill",safeTrack,Gold).GetComponent<Image>();Stretch(_safeboxFill.rectTransform);
        var track=CreatePanel("RevenueTrack",_topPanel,Hex("#E2D6BA"));SetAnchors(track,new Vector2(.04f,.28f),new Vector2(.96f,.28f),Vector2.zero,new Vector2(0,3));
        _todayProgressFill=CreatePanel("TodayFill",track,Teal).GetComponent<Image>();Stretch(_todayProgressFill.rectTransform);
        RectTransform goal;TextMeshProUGUI empty;
        _goalButton=CreateButton("NextGoal",_topPanel,out goal,out empty,"",Hex("#EBE8CC"),OpenJourneyObjective);
        SetAnchors(goal,new Vector2(.025f,.035f),new Vector2(.975f,.255f),Vector2.zero,Vector2.zero);empty.gameObject.SetActive(false);
        Glyph(goal,"key",Teal,new Vector2(.02f,.15f),new Vector2(.09f,.85f));
        _goalTitleLabel=CreateText("GoalTitle",goal,11,RevivalInk,TextAlignmentOptions.TopLeft,true);
        SetAnchors(_goalTitleLabel.rectTransform,new Vector2(.115f,.42f),new Vector2(.98f,.98f),Vector2.zero,Vector2.zero);
        _goalBenefitLabel=CreateText("GoalBenefit",goal,9,Slate,TextAlignmentOptions.TopLeft,false);
        SetAnchors(_goalBenefitLabel.rectTransform,new Vector2(.115f,.08f),new Vector2(.98f,.48f),Vector2.zero,Vector2.zero);
        var goalTrack=CreatePanel("GoalTrack",goal,Hex("#DBD4B7"));SetAnchors(goalTrack,Vector2.zero,new Vector2(1,0),new Vector2(6,1),new Vector2(-6,3));
        _goalProgressFill=CreatePanel("GoalFill",goalTrack,Gold).GetComponent<Image>();Stretch(_goalProgressFill.rectTransform);
    }
    private void BuildBottomNavigation()
    {
        _bottomNav=CreatePanel("BottomNavigation",_safeRoot,RevivalPaper);
        SetAnchors(_bottomNav,Vector2.zero,Vector2.right,new Vector2(10,8),new Vector2(-10,80));
        var layout=_bottomNav.gameObject.AddComponent<HorizontalLayoutGroup>();layout.padding=new RectOffset(5,5,5,5);layout.spacing=4;layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=true;
        string[] labels={L("HOTEL","酒店"),L("RATES","房价"),L("TEAM","员工"),L("EMPIRE","帝国"),L("ROOMS","客房")};
        string[] icons={"hotel","coin","team","case","bed"};
        for(int i=0;i<5;i++){
            DeskTab tab=(DeskTab)i;RectTransform rect;TextMeshProUGUI label;
            var button=CreateButton(tab.ToString(),_bottomNav,out rect,out label,labels[i],RevivalPaper,()=>SelectTab(tab));
            rect.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
            SetAnchors(label.rectTransform,Vector2.zero,new Vector2(1,.40f),Vector2.zero,Vector2.zero);label.fontSize=11;
            Glyph(rect,icons[i],Teal,new Vector2(.30f,.43f),new Vector2(.70f,.90f));
            _navButtons.Add(button);_navLabels.Add(label);
        }
    }
    private void AddEmployeeVisualCard(StaffMember member)
    {
        var card=CreatePanel("EmployeePortraitCard",_drawerContent,RevivalPaper);card.gameObject.AddComponent<LayoutElement>().preferredHeight=104;
        string key=member.Role==StaffRole.Housekeeper?"housekeeper":member.Role==StaffRole.Inspector?"inspector":"reception";
        var portrait=CreatePanel("PortraitFrame",card,Hex("#E6D5AD"));SetAnchors(portrait,Vector2.zero,new Vector2(0,1),new Vector2(10,10),new Vector2(87,-10));
        var texture=Resources.Load<Texture2D>("GeneratedPlaceholders/UI/worker_"+key);
        if(texture!=null){var r=CreateRect("Portrait",portrait);Stretch(r,2);r.gameObject.AddComponent<RectMask2D>();var img=CreateRect("Image",r).gameObject.AddComponent<RawImage>();Stretch(img.rectTransform);img.texture=texture;img.raycastTarget=false;}
        var title=CreateText("Employee",card,15,RevivalInk,TextAlignmentOptions.TopLeft,true);title.text=member.DisplayName+" · "+RoleName(member.Role);
        SetAnchors(title.rectTransform,Vector2.zero,Vector2.one,new Vector2(100,61),new Vector2(-10,-12));
        var status=CreateText("Morale",card,11,Slate,TextAlignmentOptions.TopLeft,false);status.text=L("MORALE ","士气 ")+member.Morale+"/100  ·  $"+member.DailyWage+L("/day","/天");
        SetAnchors(status.rectTransform,Vector2.zero,Vector2.one,new Vector2(100,37),new Vector2(-10,-42));
        var bar=CreatePanel("MoraleTrack",card,Hex("#E5DCC2"));SetAnchors(bar,Vector2.zero,Vector2.right,new Vector2(100,20),new Vector2(-14,26));
        var fill=CreatePanel("MoraleFill",bar,member.Morale<35?Coral:Teal);SetAnchors(fill,Vector2.zero,new Vector2(Mathf.Clamp01(member.Morale/100f),1),Vector2.zero,Vector2.zero);
    }
    private void AddMenuArtwork()
    {
        var imageObject=CreateRect("HotelIllustration",_sessionMenu);Stretch(imageObject);imageObject.SetAsFirstSibling();imageObject.gameObject.AddComponent<RectMask2D>();
        var image=CreateRect("Artwork",imageObject).gameObject.AddComponent<RawImage>();image.texture=Resources.Load<Texture2D>("HotelArt/Revival/MenuLobby");image.raycastTarget=false;
        Stretch(image.rectTransform);var fit=image.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=2f/3f;
        var brand=CreatePanel("TitlePlaque",_sessionMenu,new Color(.08f,.19f,.16f,.90f));SetAnchors(brand,new Vector2(.06f,.76f),new Vector2(.94f,.96f),Vector2.zero,Vector2.zero);
        var title=CreateText("GameTitle",brand,36,Cream,TextAlignmentOptions.Center,true);title.text=L("OLD TOWN\nHOTEL","老 城 酒 店");SetAnchors(title.rectTransform,new Vector2(0,.28f),new Vector2(1,.93f),Vector2.zero,Vector2.zero);
        var subtitle=CreateText("Subtitle",brand,11,Hex("#E4C28A"),TextAlignmentOptions.Center,true);subtitle.text="O L D  T O W N  H O T E L";SetAnchors(subtitle.rectTransform,new Vector2(0,.10f),new Vector2(1,.33f),Vector2.zero,Vector2.zero);
        Glyph(brand,"key",Gold,new Vector2(.44f,.0f),new Vector2(.56f,.17f));
    }
    private RectTransform CreateEmployeeActionRow()
    {
        var row=CreatePanel("EmployeeActions",_drawerContent,Color.clear);row.gameObject.AddComponent<LayoutElement>().preferredHeight=36;
        var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=6;layout.childControlWidth=true;layout.childForceExpandWidth=true;
        return row;
    }
    private void AddEmployeeAction(Transform row,string label,System.Action action,Color color,bool enabled)
    {
        RectTransform rect;TextMeshProUGUI text;var button=CreateButton("EmployeeAction",row,out rect,out text,label,color,action);
        rect.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;text.fontSize=11;button.interactable=enabled;
    }
    private void BuildRoomFilterPills()
    {
        var floors=CreateEmployeeActionRow();
        for(int i=1;i<=2;i++){int f=i;AddEmployeeAction(floors,(i+1)+"F · "+L("GUEST ROOMS","客房层"),()=>{_roomFloor=f;_drawerScrollReset=true;ForceDrawerRefresh();},_roomFloor==i?Teal:Paper,true);}
        var states=CreateEmployeeActionRow();
        string[] labels=GameText.UseChinese?new[]{"全部","破败","施工","待清洁","可售"}:new[]{"All","Ruins","Work","Dirty","Ready"};
        for(int i=0;i<labels.Length;i++){int f=i;AddEmployeeAction(states,labels[i],()=>{_roomFilter=f;_drawerScrollReset=true;ForceDrawerRefresh();},_roomFilter==i?Teal:Paper,true);}
        var types=CreateEmployeeActionRow();
        for(int i=-1;i<=2;i++){int f=i;AddEmployeeAction(types,i<0?L("ALL","全部房型"):RoomTypeName((Room2DRoomCategory)i),()=>{_roomTypeFilter=f;_drawerScrollReset=true;ForceDrawerRefresh();},_roomTypeFilter==i?Teal:Paper,true);}
    }
    private void AddRoomVisualCard(Transform row,RoomRecord room)
    {
        int number=room.number;var job=Sim.Renovations.JobFor(number);RectTransform rect;TextMeshProUGUI empty;
        var button=CreateButton("Room_"+number,row,out rect,out empty,"",RevivalPaper,()=>{RoomSelection.Clear();RoomSelection.Select(number);FocusRoom(number);SetDrawerOpen(false);});
        empty.gameObject.SetActive(false);rect.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
        var thumbnail=CreatePanel("FurnitureThumbnail",rect,Hex("#EBDFC2"));SetAnchors(thumbnail,new Vector2(.035f,.12f),new Vector2(.43f,.91f),Vector2.zero,Vector2.zero);
        var texture=Resources.Load<Texture2D>("GeneratedPlaceholders/UI/room_"+(room.category==Room2DRoomCategory.Single?"single":room.category==Room2DRoomCategory.Twin?"twin":"king"));
        if(texture!=null){var img=CreateRect("BedImage",thumbnail).gameObject.AddComponent<RawImage>();Stretch(img.rectTransform,3);img.texture=texture;img.raycastTarget=false;}
        var title=CreateText("RoomNumber",rect,21,RevivalInk,TextAlignmentOptions.TopLeft,true);title.text=number.ToString();SetAnchors(title.rectTransform,new Vector2(.48f,.57f),new Vector2(.97f,.96f),Vector2.zero,Vector2.zero);
        var type=CreateText("RoomCategory",rect,10,Slate,TextAlignmentOptions.MidlineLeft,false);type.text=RoomTypeName(room.category);SetAnchors(type.rectTransform,new Vector2(.48f,.38f),new Vector2(.97f,.60f),Vector2.zero,Vector2.zero);
        Color accent=job!=null?Gold:room.IsSellable?Teal:room.state==RoomSimState.Ruined?Slate:Coral;
        var pill=CreatePanel("RoomState",rect,accent);SetAnchors(pill,new Vector2(.47f,.11f),new Vector2(.96f,.35f),Vector2.zero,Vector2.zero);
        var state=CreateText("Status",pill,10,Cream,TextAlignmentOptions.Center,true);Stretch(state.rectTransform);state.text=job!=null?L("WORK ","施工 ")+Mathf.RoundToInt(job.Progress*100)+"%":GameText.T(RoomStatePalette.WordOf(room.state));
        if(job!=null){var track=CreatePanel("WorkTrack",rect,Hex("#DDD1B5"));SetAnchors(track,Vector2.zero,Vector2.right,new Vector2(6,3),new Vector2(-6,6));var fill=CreatePanel("WorkDone",track,Gold);SetAnchors(fill,Vector2.zero,new Vector2(job.Progress,1),Vector2.zero,Vector2.zero);}
    }
}

/// <summary>Small resolution-independent hotel pictograms, drawn as UI geometry.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class HotelGlyph : MaskableGraphic
{
    public string kind;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if(kind=="coin"){Ring(vh,new Vector2(.5f,.5f),.40f,.065f);Line(vh,.5f,.22f,.5f,.78f,.06f);Line(vh,.32f,.65f,.67f,.65f,.055f);Line(vh,.32f,.35f,.67f,.35f,.055f);Line(vh,.32f,.35f,.32f,.5f,.055f);Line(vh,.67f,.5f,.67f,.65f,.055f);Line(vh,.32f,.5f,.67f,.5f,.055f);}
        else if(kind=="key"){Ring(vh,new Vector2(.31f,.67f),.22f,.07f);Line(vh,.43f,.50f,.78f,.15f,.10f);Line(vh,.64f,.28f,.76f,.40f,.10f);}
        else if(kind=="team"){Ring(vh,new Vector2(.5f,.72f),.15f,.13f);Ring(vh,new Vector2(.18f,.6f),.12f,.10f);Ring(vh,new Vector2(.82f,.6f),.12f,.10f);Line(vh,.32f,.22f,.68f,.22f,.21f);Line(vh,.05f,.18f,.27f,.18f,.17f);Line(vh,.73f,.18f,.95f,.18f,.17f);}
        else if(kind=="bed"){Line(vh,.1f,.15f,.1f,.85f,.08f);Line(vh,.9f,.15f,.9f,.65f,.08f);Line(vh,.12f,.35f,.90f,.35f,.22f);Line(vh,.26f,.58f,.38f,.58f,.17f);Line(vh,.48f,.55f,.82f,.55f,.20f);}
        else if(kind=="case"){Line(vh,.12f,.2f,.88f,.2f,.10f);Line(vh,.12f,.70f,.88f,.70f,.10f);Line(vh,.12f,.2f,.12f,.7f,.10f);Line(vh,.88f,.2f,.88f,.7f,.10f);Line(vh,.35f,.7f,.35f,.9f,.08f);Line(vh,.65f,.7f,.65f,.9f,.08f);Line(vh,.35f,.9f,.65f,.9f,.08f);Line(vh,.12f,.48f,.88f,.48f,.06f);}
        else {Line(vh,.15f,.12f,.85f,.12f,.08f);Line(vh,.22f,.15f,.22f,.8f,.07f);Line(vh,.78f,.15f,.78f,.8f,.07f);Line(vh,.22f,.8f,.5f,.98f,.08f);Line(vh,.5f,.98f,.78f,.8f,.08f);for(int x=0;x<2;x++)for(int y=0;y<2;y++)Line(vh,.36f+x*.28f,.45f+y*.20f,.41f+x*.28f,.45f+y*.20f,.08f);Line(vh,.48f,.15f,.48f,.32f,.13f);}
    }
    private Vector2 P(Vector2 p)=>rectTransform.rect.min+Vector2.Scale(p,rectTransform.rect.size);
    private void Line(VertexHelper vh,float x1,float y1,float x2,float y2,float width){Vector2 a=P(new Vector2(x1,y1)),b=P(new Vector2(x2,y2)),n=new Vector2(-(b-a).y,(b-a).x).normalized*width*Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;int i=vh.currentVertCount;vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
    private void Ring(VertexHelper vh,Vector2 center,float radius,float width){for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24,b=(i+1)*Mathf.PI*2/24;Line(vh,center.x+Mathf.Cos(a)*radius,center.y+Mathf.Sin(a)*radius,center.x+Mathf.Cos(b)*radius,center.y+Mathf.Sin(b)*radius,width);}}
}
