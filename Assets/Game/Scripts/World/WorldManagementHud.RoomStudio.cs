using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class WorldManagementHud
{
    private bool _studioOpen,_studioInventory,_studioGrid=true,_studioNeedsRefresh,_studioWasSuspended;
    private float _studioPreviousSpeed=1,_studioNextTick;
    private int _studioCategory,_studioSelected;
    private RectTransform _studioRoot,_studioViewport,_studioTools,_studioTabs,_studioCatalogue,_studioContent,_studioFooter,_studioConfirm;
    private TextMeshProUGUI _studioTitle,_studioSummary,_studioStatus,_studioSelection,_studioApplyLabel;
    private RawImage _studioImage;private Button _studioApply,_studioUndoButton,_studioRedoButton;
    private readonly List<Button> _studioTabButtons=new List<Button>();
    private readonly List<RoomDesignDraft> _studioUndo=new List<RoomDesignDraft>(),_studioRedo=new List<RoomDesignDraft>();
    private RoomDesignDraft _studioDraft,_studioInitial,_studioDragBefore;private RoomStudioPreview _studioPreview;
    private Vector2 _studioGrab,_studioLastPointer;private bool _studioDragging,_studioOrbit;private string _studioMessage="";
    public static bool RoomStudioOpen=>_instance!=null&&_instance._studioOpen;
    public static void OpenRoomStudio(int room=0)
    {
        var h=ActiveInstance;if(h==null||h.Sim==null)return;
        if(room==0)room=RoomSelection.Selected;
        if(!h.Sim.Rooms.Contains(room)){room=0;for(int i=0;i<h.Sim.Rooms.Count;i++){var r=h.Sim.Rooms.Peek(i);if(r.state!=RoomSimState.Ruined){room=r.number;break;}}}
        if(room==0)return;h.BeginRoomStudio(room);
    }
    private void BeginRoomStudio(int room)
    {
        if(_studioOpen)EndRoomStudio(false);
        SetDrawerOpen(false);CloseScenePanels();
        if(RoomDoor.ActivePrompt!=null)RoomDoor.ActivePrompt.DeclineEntry();
        _studioDraft=Sim.CreateRoomDesignDraft(room);if(_studioDraft==null)return;
        _studioInitial=_studioDraft.Clone();_studioSelected=0;_studioCategory=0;_studioInventory=false;_studioUndo.Clear();_studioRedo.Clear();_studioMessage="";
        _studioWasSuspended=HotelSimSceneBridge.Instance.IsSuspended;_studioPreviousSpeed=_speed;
        HotelSimSceneBridge.Instance.IsSuspended=true;SetSpeed(0);_studioOpen=true;
        if(_studioRoot==null)BuildRoomStudio();_studioRoot.gameObject.SetActive(true);_studioRoot.SetAsLastSibling();
        _studioPreview=new RoomStudioPreview();_studioImage.texture=_studioPreview.Texture;_studioNeedsRefresh=true;
        LayoutRoomStudio();RebuildStudioCatalogue();RefreshRoomStudio();
    }
    private void BuildRoomStudio()
    {
        _studioRoot=CreatePanel("RoomDesignStudio",_safeRoot,RevivalPaper);Stretch(_studioRoot);
        _studioTitle=CreateText("StudioTitle",_studioRoot,20,RevivalInk,TextAlignmentOptions.MidlineLeft,true);
        SetAnchors(_studioTitle.rectTransform,Vector2.up,Vector2.one,new Vector2(16,-52),new Vector2(-78,-12));
        RectTransform rect;TextMeshProUGUI label;
        CreateButton("StudioClose",_studioRoot,out rect,out label,L("CLOSE","关闭"),Walnut,RequestCloseRoomStudio);
        SetAnchors(rect,Vector2.one,Vector2.one,new Vector2(-70,-50),new Vector2(-14,-14));label.fontSize=12;
        _studioSummary=CreateText("StudioSummary",_studioRoot,11,Teal,TextAlignmentOptions.TopLeft,false);_studioSummary.textWrappingMode=TextWrappingModes.Normal;
        SetAnchors(_studioSummary.rectTransform,Vector2.up,Vector2.one,new Vector2(16,-96),new Vector2(-14,-57));
        _studioViewport=CreateRect("StudioViewport",_studioRoot);SetAnchors(_studioViewport,new Vector2(0,.49f),Vector2.one,new Vector2(12,12),new Vector2(-12,-103));
        _studioImage=_studioViewport.gameObject.AddComponent<RawImage>();_studioImage.color=Color.white;
        _studioViewport.gameObject.AddComponent<RoomStudioPointer>().owner=this;
        var caption=CreatePanel("PreviewCaption",_studioViewport,new Color(.98f,.95f,.85f,.94f));
        SetAnchors(caption,Vector2.up,new Vector2(.55f,1),new Vector2(7,-34),new Vector2(0,-7));caption.GetComponent<Image>().raycastTarget=false;
        _studioSelection=CreateText("SelectedObject",caption,11,RevivalInk,TextAlignmentOptions.MidlineLeft,true);Stretch(_studioSelection.rectTransform,5);
        for(int i=0;i<4;i++){int command=i;CreateButton("StudioView"+i,_studioViewport,out rect,out label,new[]{"<",">","-","+"}[i],Teal,()=>{if(command<2)_studioPreview.Yaw+=(command==0?-45:45);else _studioPreview.Zoom+=command==2?.35f:-.35f;_studioPreview.UpdateCamera();});SetAnchors(rect,Vector2.one,Vector2.one,new Vector2(-137+i*32,-35),new Vector2(-108+i*32,-7));label.fontSize=13;Stretch(label.rectTransform,1);}
        var hint=CreateText("StudioGestureHelp",_studioViewport,10,Cream,TextAlignmentOptions.BottomLeft,false);hint.color=Cream;hint.text=L("Drag object to place · drag space to orbit · +/- zoom","拖动物件布置 · 拖动空白转视角 · +/- 缩放");SetAnchors(hint.rectTransform,Vector2.zero,Vector2.right,new Vector2(10,7),new Vector2(-5,26));
        _studioTools=CreateRect("StudioObjectTools",_studioRoot);SetAnchors(_studioTools,new Vector2(0,.49f),new Vector2(1,.49f),new Vector2(12,-40),new Vector2(-12,-4));
        var toolLayout=_studioTools.gameObject.AddComponent<HorizontalLayoutGroup>();toolLayout.spacing=4;toolLayout.childControlWidth=true;toolLayout.childForceExpandWidth=true;
        _studioUndoButton=CreateButton("StudioUndo",_studioTools,out rect,out label,L("UNDO","撤销"),Teal,StudioUndo);label.fontSize=11;
        _studioRedoButton=CreateButton("StudioRedo",_studioTools,out rect,out label,L("REDO","重做"),Teal,StudioRedo);label.fontSize=11;
        CreateButton("StudioRotate",_studioTools,out rect,out label,L("ROTATE","旋转"),Walnut,()=>StudioEditSelected(p=>RoomDesignGeometry.Rotate(p)));label.fontSize=11;
        CreateButton("StudioVariant",_studioTools,out rect,out label,L("STYLE","换色"),Walnut,()=>StudioEditSelected(p=>p.variantId=(p.variantId+1)%4));label.fontSize=11;
        CreateButton("StudioStore",_studioTools,out rect,out label,L("REMOVE","收纳"),Walnut,StudioRemoveSelected);label.fontSize=11;
        CreateButton("StudioGrid",_studioTools,out rect,out label,L("GRID","网格"),Walnut,()=>{_studioGrid=!_studioGrid;_studioNeedsRefresh=true;});label.fontSize=11;
        _studioTabs=CreateRect("StudioCatalogueTabs",_studioRoot);SetAnchors(_studioTabs,new Vector2(0,.43f),new Vector2(1,.43f),new Vector2(12,-28),new Vector2(-12,8));
        var tabs=_studioTabs.gameObject.AddComponent<HorizontalLayoutGroup>();tabs.spacing=3;tabs.childControlWidth=true;tabs.childForceExpandWidth=true;
        string[] titles={L("FURNITURE","家具"),L("DECOR","摆件"),L("WALLS","墙面"),L("FLOORS","地面"),L("LIGHTS","灯光"),L("PLANS","方案")};
        for(int i=0;i<titles.Length;i++){int category=i;var b=CreateButton("StudioTab_"+i,_studioTabs,out rect,out label,titles[i],Teal,()=>{_studioCategory=category;RebuildStudioCatalogue();});label.fontSize=11;_studioTabButtons.Add(b);}
        _studioCatalogue=CreatePanel("StudioCatalogue",_studioRoot,Hex("#E8DEC5"));SetAnchors(_studioCatalogue,Vector2.zero,new Vector2(1,.43f),new Vector2(12,106),new Vector2(-12,-36));
        var scroll=_studioCatalogue.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
        var viewport=CreateRect("Viewport",_studioCatalogue);Stretch(viewport,4);viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        _studioContent=CreateRect("Content",viewport);_studioContent.anchorMin=new Vector2(0,1);_studioContent.anchorMax=Vector2.one;_studioContent.pivot=new Vector2(.5f,1);_studioContent.sizeDelta=Vector2.zero;
        var layout=_studioContent.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=6;layout.padding=new RectOffset(4,4,4,4);layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
        _studioContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=_studioContent;
        var scrollTrack=CreatePanel("CatalogueScrollTrack",_studioCatalogue,new Color(.20f,.35f,.28f,.12f));SetAnchors(scrollTrack,Vector2.right,Vector2.one,new Vector2(-5,6),new Vector2(-1,-6));
        var scrollbar=scrollTrack.gameObject.AddComponent<Scrollbar>();var handle=CreatePanel("Handle",scrollTrack,Teal);Stretch(handle);scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<Image>();scrollbar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        _studioFooter=CreatePanel("StudioCheckout",_studioRoot,RevivalPaper);SetAnchors(_studioFooter,Vector2.zero,Vector2.right,new Vector2(12,10),new Vector2(-12,98));
        _studioStatus=CreateText("StudioCostAndStatus",_studioFooter,11,RevivalInk,TextAlignmentOptions.TopLeft,false);_studioStatus.textWrappingMode=TextWrappingModes.Normal;
        SetAnchors(_studioStatus.rectTransform,Vector2.zero,Vector2.one,new Vector2(4,33),new Vector2(-4,-5));
        _studioApply=CreateButton("StudioApply",_studioFooter,out rect,out _studioApplyLabel,"",Teal,ApplyRoomStudio);
        SetAnchors(rect,Vector2.zero,Vector2.one,new Vector2(4,0),new Vector2(-4,-52));_studioApplyLabel.fontSize=14;
    }
    private void TickRoomStudio()
    {
        LayoutRoomStudio();
        Time.timeScale=0;var bridge=HotelSimSceneBridge.Instance;if(bridge!=null)bridge.IsSuspended=true;
        var keys=Keyboard.current;
        if(keys!=null&&!(UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>()?.isFocused??false)){if(keys.escapeKey.wasPressedThisFrame)RequestCloseRoomStudio();
            if(keys.rKey.wasPressedThisFrame)StudioEditSelected(p=>RoomDesignGeometry.Rotate(p));
            if(keys.deleteKey.wasPressedThisFrame)StudioRemoveSelected();
            bool control=keys.leftCtrlKey.isPressed||keys.rightCtrlKey.isPressed;
            if(control&&keys.zKey.wasPressedThisFrame)StudioUndo();if(control&&keys.yKey.wasPressedThisFrame)StudioRedo();}
        if(_studioPreview==null)return;
        var size=_studioViewport.rect.size;_studioPreview.Resize(Mathf.RoundToInt(size.x*2),Mathf.RoundToInt(size.y*2));_studioImage.texture=_studioPreview.Texture;
        if(_studioNeedsRefresh){_studioPreview.Show(_studioDraft.plan,_studioSelected,_studioGrid);_studioNeedsRefresh=false;}
        _studioPreview.UpdateCamera();_studioNextTick-=Time.unscaledDeltaTime;if(_studioNextTick<=0){_studioNextTick=.1f;RefreshRoomStudio();}
    }
    private Vector2 _studioLayoutSize;
    private void LayoutRoomStudio()
    {
        if(_studioRoot==null)return;var size=_studioRoot.rect.size;if((size-_studioLayoutSize).sqrMagnitude<1)return;_studioLayoutSize=size;
        bool wide=size.x>size.y*.88f;
        if(wide){SetAnchors(_studioViewport,Vector2.zero,new Vector2(.60f,1),new Vector2(12,66),new Vector2(-5,-103));SetAnchors(_studioTools,Vector2.zero,new Vector2(.60f,0),new Vector2(12,15),new Vector2(-5,55));SetAnchors(_studioTabs,new Vector2(.61f,1),Vector2.one,new Vector2(0,-140),new Vector2(-12,-104));SetAnchors(_studioCatalogue,new Vector2(.61f,0),Vector2.one,new Vector2(0,106),new Vector2(-12,-150));SetAnchors(_studioFooter,new Vector2(.61f,0),Vector2.right,new Vector2(0,10),new Vector2(-12,98));}
        else {SetAnchors(_studioViewport,new Vector2(0,.49f),Vector2.one,new Vector2(12,12),new Vector2(-12,-103));SetAnchors(_studioTools,new Vector2(0,.49f),new Vector2(1,.49f),new Vector2(12,-40),new Vector2(-12,-4));SetAnchors(_studioTabs,new Vector2(0,.43f),new Vector2(1,.43f),new Vector2(12,-28),new Vector2(-12,8));SetAnchors(_studioCatalogue,Vector2.zero,new Vector2(1,.43f),new Vector2(12,106),new Vector2(-12,-36));SetAnchors(_studioFooter,Vector2.zero,Vector2.right,new Vector2(12,10),new Vector2(-12,98));}
    }
    private string StudioSignature(RoomDesignDraft draft)=>JsonUtility.ToJson(draft.plan);
    private bool StudioChanged=>_studioDraft!=null&&_studioInitial!=null&&StudioSignature(_studioDraft)!=StudioSignature(_studioInitial);
    private void RefreshRoomStudio()
    {
        if(!_studioOpen||_studioDraft==null)return;
        var plan=_studioDraft.plan;var quote=Sim.QuoteRoomDesign(_studioDraft);var selected=plan.pieces.Find(x=>x.localId==_studioSelected);
        _studioTitle.text=L("ROOM ","房间 ")+plan.roomNumber+L(" / DESIGN STUDIO"," / 客房工作室");
        _studioSummary.text=L("Design ","设计评分 ")+RoomDesignLedger.Score(plan)+"/20    "+L("Quality ","交付品质 ")+Mathf.RoundToInt(Sim.DeliveredQualityOf(plan.roomNumber)*100)+"% → "+Mathf.RoundToInt(quote.quality*100)+"%\n"+L("Preview first; pay only when applying. Wear stays with existing furniture.","自由预览，应用时结算；旧家具的磨损会保留。");
        _studioSelection.text=selected==null?L("Select and drag an object","点击物件后拖动摆放"):StudioPieceName(selected)+L(" · drag to place"," · 拖动摆放");
        string detail=quote.reason!=""?quote.reason:_studioMessage!=""?_studioMessage:L("Clear doorway and circulation. Removed furniture goes to storage.","门口和过道保持畅通；拆下的家具收入仓库。");
        _studioStatus.text=L("Purchase $","购置 $")+quote.purchases+L(" · finishes $"," · 饰面 $")+quote.finishes+(quote.recycling>0?L(" · recycle $"," · 回收 $")+quote.recycling:"")+L(" · cash $"," · 现金 $")+Sim.Cash+"\n"+detail;
        _studioStatus.color=!quote.valid||!quote.roomAvailable||Sim.Cash<Mathf.Max(0,quote.NetCost)?Hex("#9C4939"):RevivalInk;
        _studioApplyLabel.text=L("APPLY DESIGN  ","应用设计  ")+(quote.NetCost>=0?"$"+quote.NetCost:L("REFUND $","回收 $")+-quote.NetCost);
        _studioApply.interactable=quote.valid&&quote.roomAvailable&&StudioChanged&&Sim.Cash>=Mathf.Max(0,quote.NetCost);
        _studioUndoButton.interactable=_studioUndo.Count>0;_studioRedoButton.interactable=_studioRedo.Count>0;
        for(int i=0;i<_studioTabButtons.Count;i++)_studioTabButtons[i].GetComponent<Image>().color=i==_studioCategory?Teal:Walnut;
    }
    private string StudioPieceName(RoomDesignPiece p)=>p.IsDecoration?L(RoomDecorCatalog.Get(p.kindId).en,RoomDecorCatalog.Get(p.kindId).cn):GameText.T(FurnitureCatalog.Get(p.kindId).name);
    private void RememberStudio(RoomDesignDraft before){_studioUndo.Add(before);if(_studioUndo.Count>32)_studioUndo.RemoveAt(0);_studioRedo.Clear();_studioNeedsRefresh=true;_studioMessage="";}
    private void StudioChange(Action change){if(!_studioOpen||_studioDraft==null)return;var before=_studioDraft.Clone();change();if(StudioSignature(before)!=StudioSignature(_studioDraft))RememberStudio(before);RefreshRoomStudio();}
    private void StudioEditSelected(Action<RoomDesignPiece> change){var piece=_studioDraft?.plan.pieces.Find(x=>x.localId==_studioSelected);if(piece==null){_studioMessage=L("Select an object first.","请先点击场景中的物件。");RefreshRoomStudio();return;}StudioChange(()=>change(piece));}
    private void StudioUndo(){if(_studioUndo.Count==0)return;_studioRedo.Add(_studioDraft.Clone());_studioDraft=_studioUndo[_studioUndo.Count-1];_studioUndo.RemoveAt(_studioUndo.Count-1);_studioNeedsRefresh=true;_studioMessage="";RebuildStudioCatalogue();}
    private void StudioRedo(){if(_studioRedo.Count==0)return;_studioUndo.Add(_studioDraft.Clone());_studioDraft=_studioRedo[_studioRedo.Count-1];_studioRedo.RemoveAt(_studioRedo.Count-1);_studioNeedsRefresh=true;_studioMessage="";RebuildStudioCatalogue();}
    private void StudioRemoveSelected()
    {
        var p=_studioDraft.plan.pieces.Find(x=>x.localId==_studioSelected);if(p==null)return;
        if(!p.IsDecoration&&FurnitureCatalog.Get(p.kindId).IsRequired){_studioMessage=L("Replace the bed/bathroom from the furniture catalogue.","床和卫浴需要保留；请在家具目录中直接选择替换款。");RefreshRoomStudio();return;}
        StudioChange(()=>{_studioDraft.plan.pieces.Remove(p);_studioSelected=0;});
    }
    private void ApplyRoomStudio()
    {
        if(!Sim.TryApplyRoomDesign(_studioDraft,out var reason)){_studioMessage=reason;RefreshRoomStudio();return;}
        int room=_studioDraft.plan.roomNumber;var binder=FindRoomBinder(room);binder?.GetComponent<RoomFurnitureView>()?.RefreshNow();binder?.GetComponent<RoomDecorationView>()?.RefreshNow();
        FindFirstObjectByType<SaveCoordinator>()?.RequestProgressAutosave();EndRoomStudio(true);SelectRoom(room);FocusRoom(room);ShowToast(L("Design installed. Clean and inspect before the next guest.","布置已落到房间里；清洁验房后即可接待下一位客人。"));ForceAllRefresh();
    }
    private void RequestCloseRoomStudio()
    {
        if(!StudioChanged){EndRoomStudio(false);return;}
        if(_studioConfirm!=null){_studioConfirm.gameObject.SetActive(true);_studioConfirm.SetAsLastSibling();return;}
        _studioConfirm=CreatePanel("StudioDiscardConfirmation",_studioRoot,new Color(.1f,.18f,.15f,.86f));Stretch(_studioConfirm);
        var card=CreatePanel("Card",_studioConfirm,RevivalPaper);SetAnchors(card,new Vector2(.08f,.36f),new Vector2(.92f,.64f),Vector2.zero,Vector2.zero);
        var text=CreateText("Question",card,16,RevivalInk,TextAlignmentOptions.Center,true);text.text=L("Discard this preview?\nYour hotel and cash are unchanged.","放弃本次预览？\n尚未扣钱，也没有改动酒店。");text.textWrappingMode=TextWrappingModes.Normal;SetAnchors(text.rectTransform,new Vector2(0,.42f),Vector2.one,new Vector2(12,0),new Vector2(-12,-12));
        RectTransform r;TextMeshProUGUI label;
        CreateButton("StudioKeepEditing",card,out r,out label,L("KEEP EDITING","继续设计"),Teal,()=>_studioConfirm.gameObject.SetActive(false));SetAnchors(r,Vector2.zero,new Vector2(.5f,.36f),new Vector2(12,12),new Vector2(-5,0));
        CreateButton("StudioDiscard",card,out r,out label,L("DISCARD","放弃更改"),Walnut,()=>EndRoomStudio(false));SetAnchors(r,new Vector2(.5f,0),new Vector2(1,.36f),new Vector2(5,12),new Vector2(-12,0));
    }
    private void EndRoomStudio(bool applied)
    {
        _studioOpen=false;if(_studioRoot!=null)_studioRoot.gameObject.SetActive(false);if(_studioConfirm!=null)_studioConfirm.gameObject.SetActive(false);
        _studioPreview?.Dispose();_studioPreview=null;if(_studioImage!=null)_studioImage.texture=null;
        var b=HotelSimSceneBridge.Instance;if(b!=null)b.IsSuspended=_studioWasSuspended;SetSpeed(_studioPreviousSpeed);_studioDraft=null;_studioInitial=null;_studioDragBefore=null;
        _roomStateKey="";_refreshTimer=0;ForceAllRefresh();
    }
    internal void StudioPointerDown(PointerEventData e)
    {
        if(!_studioOpen||!StudioUV(e.position,out var uv))return;_studioLastPointer=e.position;
        int id=_studioPreview.Pick(uv);_studioOrbit=e.button!=PointerEventData.InputButton.Left||id==0;_studioDragging=!_studioOrbit;
        if(_studioDragging){_studioSelected=id;_studioDragBefore=_studioDraft.Clone();var item=_studioDraft.plan.pieces.Find(x=>x.localId==id);if(_studioPreview.Project(uv,out var point))_studioGrab=new Vector2(item.x,item.z)-point;}
        else {_studioSelected=0;_studioDragBefore=null;}_studioNeedsRefresh=true;RefreshRoomStudio();
    }
    internal void StudioPointerDrag(PointerEventData e)
    {
        if(!_studioOpen)return;
        if(_studioOrbit){var delta=e.position-_studioLastPointer;_studioPreview.Yaw+=delta.x*.35f;_studioPreview.Pitch=Mathf.Clamp(_studioPreview.Pitch-delta.y*.16f,35,80);_studioPreview.UpdateCamera();}
        else if(_studioDragging&&StudioUV(e.position,out var uv)&&_studioPreview.Project(uv,out var point)){
            var p=_studioDraft.plan.pieces.Find(x=>x.localId==_studioSelected);if(p!=null)RoomDesignGeometry.Place(p,point.x+_studioGrab.x,point.y+_studioGrab.y,p.yaw);_studioNeedsRefresh=true;RefreshRoomStudio();
        }
        _studioLastPointer=e.position;
    }
    internal void StudioPointerUp(PointerEventData e)
    {if(_studioOpen&&_studioDraft!=null&&_studioDragBefore!=null&&StudioSignature(_studioDragBefore)!=StudioSignature(_studioDraft))RememberStudio(_studioDragBefore);_studioDragBefore=null;_studioDragging=false;_studioOrbit=false;}
    internal void StudioScroll(PointerEventData e){if(_studioPreview!=null){_studioPreview.Zoom-=e.scrollDelta.y*.2f;_studioPreview.UpdateCamera();}}
    private bool StudioUV(Vector2 screen,out Vector2 uv)
    {
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(_studioViewport,screen,null,out var p)){var rect=_studioViewport.rect;uv=new Vector2((p.x-rect.xMin)/rect.width,(p.y-rect.yMin)/rect.height);return true;}uv=default;return false;
    }
}
