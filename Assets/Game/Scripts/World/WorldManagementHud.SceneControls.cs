using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class WorldManagementHud
{
    private RectTransform _sceneToolbar,_sceneViewPanel,_sceneFloorPanel,_sceneNavigator;
    private RectTransform _sceneTimeControls;
    private TextMeshProUGUI _scenePauseLabel,_sceneSpeedLabel;
    private float _sceneResumeSpeed=1;
    private TextMeshProUGUI _sceneModeLabel,_sceneFloorLabel,_sceneNavigatorTitle;
    private ManagerCameraRig _sceneRig;
    private WorldInputController _sceneInput;
    private TaskDispatcher _sceneDispatcher;
    private readonly Dictionary<int,Button> _sceneRoomButtons=new Dictionary<int,Button>();
    private string _sceneNavigationKey;
    public static bool SceneCameraInputBlocked {
        get {
            var h=ActiveInstance;if(h==null)return false;
            return h._drawerOpen || h._hudMode==WorldHudMode.MorningReport
                || (h._sessionMenu!=null&&h._sessionMenu.gameObject.activeInHierarchy)
                || (h._sceneNavigator!=null&&h._sceneNavigator.gameObject.activeInHierarchy);
        }
    }
    private int ServicePriorityRevision=>_sceneDispatcher!=null ? _sceneDispatcher.PriorityRevision : 0;
    private void BuildSceneControls()
    {
        _sceneToolbar=CreatePanel("SceneControlBar",_safeRoot,Color.clear);
        SetAnchors(_sceneToolbar,Vector2.up,Vector2.one,new Vector2(82,-168),new Vector2(-10,-132));
        var row=_sceneToolbar.gameObject.AddComponent<HorizontalLayoutGroup>();row.spacing=5;row.childControlWidth=true;row.childForceExpandWidth=true;
        RectTransform rect; TextMeshProUGUI label;
        var mode=CreateButton("DirectorWalkMode",_sceneToolbar,out rect,out _sceneModeLabel,"",Teal,()=>{
            if(_sceneInput!=null)_sceneInput.SetDirectorMode(!_sceneInput.IsDirectorMode); RefreshSceneControls();
        });rect.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;_sceneModeLabel.fontSize=11;
        CreateButton("FloorPicker",_sceneToolbar,out rect,out _sceneFloorLabel,"",Walnut,()=>ToggleScenePanel(_sceneFloorPanel));
        rect.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;_sceneFloorLabel.fontSize=11;
        CreateButton("CameraControls",_sceneToolbar,out rect,out label,L("VIEW","镜头"),Walnut,()=>ToggleScenePanel(_sceneViewPanel));
        rect.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;label.fontSize=11;
        CreateButton("RoomNavigator",_sceneToolbar,out rect,out label,L("ROOM MAP","房间导航"),Walnut,OpenSceneNavigator);
        rect.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;label.fontSize=11;
        _sceneViewPanel=CreatePanel("CameraControlPopover",_safeRoot,RevivalPaper);
        SetAnchors(_sceneViewPanel,Vector2.one,Vector2.one,new Vector2(-222,-397),new Vector2(-10,-176));
        var hint=CreateText("CameraGestureHint",_sceneViewPanel,10,Slate,TextAlignmentOptions.TopLeft,false);
        hint.text=L("Drag to pan · wheel to zoom\nRight drag / two fingers to orbit","拖动平移 · 滚轮/双指缩放\n右键拖动/双指旋转 · Q/E 转向");
        hint.textWrappingMode=TextWrappingModes.Normal;
        SetAnchors(hint.rectTransform,Vector2.up,Vector2.one,new Vector2(10,-43),new Vector2(-8,-7));
        AddSceneCameraButton("RotateLeft",L("↶ 45°","左转 45°"),0,0,()=>_sceneRig?.OrbitBy(-45));
        AddSceneCameraButton("RotateRight",L("45° ↷","右转 45°"),1,0,()=>_sceneRig?.OrbitBy(45));
        AddSceneCameraButton("ZoomIn",L("ZOOM +","放大 +"),0,1,()=>_sceneRig?.ZoomBy(-1.25f));
        AddSceneCameraButton("ZoomOut",L("ZOOM −","缩小 −"),1,1,()=>_sceneRig?.ZoomBy(1.25f));
        AddSceneCameraButton("OverheadView",L("FLOOR OVERVIEW","整层俯瞰"),0,2,()=>{if(_sceneRig!=null){_sceneRig.ViewEntireFloor();CloseScenePanels();}});
        AddSceneCameraButton("ResetView",L("RESET","恢复构图"),1,2,()=>_sceneRig?.ResetView());
        AddSceneCameraButton("LocateManager",L("MANAGER","定位经理"),0,3,()=>{_sceneRig?.ReturnToTarget();CloseScenePanels();});
        AddSceneCameraButton("HideCameraControls",L("DONE","收起"),1,3,CloseScenePanels);
        _sceneViewPanel.gameObject.SetActive(false);
        _sceneFloorPanel=CreatePanel("FloorPickerPopover",_safeRoot,RevivalPaper);
        SetAnchors(_sceneFloorPanel,Vector2.one,Vector2.one,new Vector2(-222,-466),new Vector2(-10,-176));
        for(int i=0;i<FloorMath.FloorCount;i++) {
            int floor=i;
            CreateButton("ViewFloor_"+i,_sceneFloorPanel,out rect,out label,SceneFloorName(i),i<3?Teal:Walnut,()=>ViewSceneFloor(floor));
            SetAnchors(rect,Vector2.up,Vector2.one,new Vector2(8,-44-i*40),new Vector2(-8,-8-i*40));label.fontSize=12;
        }
        _sceneFloorPanel.gameObject.SetActive(false);
        _sceneTimeControls=CreatePanel("PlanningTimeControls",_safeRoot,Color.clear);
        SetAnchors(_sceneTimeControls,Vector2.one,Vector2.one,new Vector2(-145,-214),new Vector2(-10,-176));
        CreateButton("PlanningPause",_sceneTimeControls,out rect,out _scenePauseLabel,"",Walnut,()=>{
            if(_speed>0){_sceneResumeSpeed=_speed;SetSpeed(0);}else SetSpeed(_sceneResumeSpeed);
            RefreshSceneControls();
        });SetAnchors(rect,Vector2.zero,new Vector2(.38f,1),Vector2.zero,new Vector2(-3,0));
        CreateButton("PlanningSpeed",_sceneTimeControls,out rect,out _sceneSpeedLabel,"",Walnut,()=>{
            SetSpeed(_speed<1?1:_speed<3?3:_speed<6?6:1);_sceneResumeSpeed=_speed;RefreshSceneControls();
        });SetAnchors(rect,new Vector2(.38f,0),Vector2.one,new Vector2(3,0),Vector2.zero);
        _scenePauseLabel.fontSize=14;_sceneSpeedLabel.fontSize=12;
        BuildSceneNavigator();
    }
    private void AddSceneCameraButton(string name,string text,int column,int row,System.Action action)
    {
        RectTransform rect;TextMeshProUGUI label;
        CreateButton(name,_sceneViewPanel,out rect,out label,text,Teal,action);
        SetAnchors(rect,new Vector2(column*.5f,1),new Vector2((column+1)*.5f,1),new Vector2(7,-83-row*34),new Vector2(-7,-53-row*34));label.fontSize=10;
    }
    private void BuildSceneNavigator()
    {
        _sceneNavigator=CreatePanel("RoomNavigationOverlay",_safeRoot,RevivalPaper);
        SetAnchors(_sceneNavigator,Vector2.zero,Vector2.one,new Vector2(10,88),new Vector2(-10,-176));
        _sceneNavigatorTitle=CreateText("Title",_sceneNavigator,17,RevivalInk,TextAlignmentOptions.MidlineLeft,true);
        SetAnchors(_sceneNavigatorTitle.rectTransform,Vector2.up,Vector2.one,new Vector2(12,-39),new Vector2(-50,-7));
        RectTransform rect;TextMeshProUGUI label;
        CreateButton("CloseRoomNavigator",_sceneNavigator,out rect,out label,"",Walnut,CloseScenePanels);
        SetAnchors(rect,Vector2.one,Vector2.one,new Vector2(-40,-39),new Vector2(-8,-7));AddCloseGlyph(rect);label.gameObject.SetActive(false);
        var hint=CreateText("Legend",_sceneNavigator,10,Slate,TextAlignmentOptions.MidlineLeft,false);
        hint.text=L("Actual floor plan · tap a room · N →","真实楼层平面 · 点击房间直达 · 北 →");
        SetAnchors(hint.rectTransform,Vector2.up,Vector2.one,new Vector2(12,-63),new Vector2(-10,-40));
        var map=CreatePanel("SpatialFloorPlan",_sceneNavigator,new Color(.79f,.76f,.65f));
        SetAnchors(map,Vector2.zero,Vector2.one,new Vector2(12,13),new Vector2(-12,-70));
        AddPlanBlock(map,"Core",Vector3.zero,new Vector2(20,40),new Color(.89f,.85f,.72f));
        foreach(int side in new[]{-1,1}) {
            AddPlanBlock(map,"Courtyard",new Vector3(side*22.5f,0,0),new Vector2(25,11),new Color(.58f,.69f,.54f));
            AddPlanCaption(map,side<0?L("WEST GARDEN","西庭院"):L("EAST GARDEN","东庭院"),new Vector3(side*22.5f,0,0),10);
            AddPlanBlock(map,"EndGallery",new Vector3(side*41,0,0),new Vector2(2,27),new Color(.94f,.90f,.77f));
            foreach(int z in new[]{-1,1})AddPlanBlock(map,"WingCorridor",new Vector3(side*25,0,z*12),new Vector2(32,2.4f),new Color(.94f,.90f,.77f));
        }
        AddPlanCaption(map,L("LIFTS / HALL","电梯 / 中央厅"),new Vector3(0,0,0),10);
        AddPlanCaption(map,L("LINEN","布草间"),HotelFloorPlan.Linen,8);
        for(int i=1;i<=50;i++) {
            int offset=i;
            var button=CreateButton("NavigateRoom_"+i,map,out rect,out label,"",Teal,()=>{
                if(_sceneRig==null)return;
                int room=(_sceneRig.ViewedFloor+1)*100+offset;
                CloseScenePanels();SelectRoom(room);FocusRoom(room);_sceneRig.SetZoom(6.2f);
            });
            var point=HotelFloorPlan.MapPoint(HotelFloorPlan.Room(200+i).Centre);
            rect.anchorMin=point-new Vector2(2.35f/HotelFloorPlan.Depth,2.35f/HotelFloorPlan.Width);
            rect.anchorMax=point+new Vector2(2.35f/HotelFloorPlan.Depth,2.35f/HotelFloorPlan.Width);
            rect.offsetMin=Vector2.one;rect.offsetMax=-Vector2.one;
            Stretch(label.rectTransform,1);
            label.fontSize=11;label.textWrappingMode=TextWrappingModes.Normal;label.enableAutoSizing=false;
            label.overflowMode=TextOverflowModes.Overflow;
            _sceneRoomButtons.Add(i,button);
        }
        _sceneNavigator.gameObject.SetActive(false);
    }
    private void AddPlanBlock(RectTransform map,string name,Vector3 centre,Vector2 size,Color colour)
    {
        var block=CreatePanel(name,map,colour);var point=HotelFloorPlan.MapPoint(centre);
        block.anchorMin=point-new Vector2(size.y/HotelFloorPlan.Depth,size.x/HotelFloorPlan.Width)*.5f;
        block.anchorMax=point+new Vector2(size.y/HotelFloorPlan.Depth,size.x/HotelFloorPlan.Width)*.5f;
        block.offsetMin=block.offsetMax=Vector2.zero;block.GetComponent<Image>().raycastTarget=false;
    }
    private void AddPlanCaption(RectTransform map,string text,Vector3 centre,int size)
    {
        var label=CreateText("PlanLabel",map,size,RevivalInk,TextAlignmentOptions.Center,true);label.text=text;
        var point=HotelFloorPlan.MapPoint(centre);label.rectTransform.anchorMin=label.rectTransform.anchorMax=point;
        label.rectTransform.sizeDelta=new Vector2(90,24);label.rectTransform.anchoredPosition=Vector2.zero;
    }
    private void ToggleScenePanel(RectTransform panel)
    {
        bool open=panel!=null&&!panel.gameObject.activeSelf;CloseScenePanels();if(panel!=null)panel.gameObject.SetActive(open);RefreshSceneControls();
    }
    private void CloseScenePanels()
    {
        foreach(var panel in new[]{_sceneViewPanel,_sceneFloorPanel,_sceneNavigator})if(panel!=null)panel.gameObject.SetActive(false);
    }
    private void OpenSceneNavigator()
    {
        if(_sceneRig==null)return;
        if(_sceneRig.ViewedFloor!=1&&_sceneRig.ViewedFloor!=2)ViewSceneFloor(1);
        ToggleScenePanel(_sceneNavigator);_sceneNavigationKey=null;RefreshSceneControls();
    }
    private void ViewSceneFloor(int floor)
    {
        CloseScenePanels();
        bool available=floor!=FacilitySystem.GymFloor&&floor!=FacilitySystem.CasinoFloor&&floor!=FacilitySystem.PoolFloor
            || floor==FacilitySystem.GymFloor&&FacilitySystem.GymUnlocked
            || floor==FacilitySystem.CasinoFloor&&FacilitySystem.CasinoUnlocked
            || floor==FacilitySystem.PoolFloor&&FacilitySystem.PoolUnlocked;
        if(!available){ShowToast(L("Unlock this facility in Home Investment first.","先在本店投资中解锁这层设施。"));return;}
        if(_sceneInput!=null&&!_sceneInput.IsDirectorMode)_sceneInput.SetDirectorMode(true);
        ClearWorldSelection();_sceneRig?.ViewFloor(floor);_sceneNavigationKey=null;RefreshSceneControls();
    }
    private static string SceneFloorName(int floor)
    {
        string[] names=GameText.UseChinese ? new[]{"大堂 / 庭院","客房 · 四翼","客房 · 四翼","餐厅","健身房","赌场","泳池"}
            : new[]{"Lobby","Guest rooms","Upper rooms","Restaurant","Gym","Casino","Pool"};
        return (floor+1)+"F · "+names[Mathf.Clamp(floor,0,names.Length-1)];
    }
    private void RefreshSceneControls()
    {
        if(_sceneToolbar==null)return;
        if(_sceneRig==null)_sceneRig=FindFirstObjectByType<ManagerCameraRig>();
        if(_sceneInput==null)_sceneInput=FindFirstObjectByType<WorldInputController>();
        if(_sceneDispatcher==null)_sceneDispatcher=FindFirstObjectByType<TaskDispatcher>();
        bool visible=_hudMode==WorldHudMode.Operations&&!_drawerOpen;
        _sceneToolbar.gameObject.SetActive(visible);
        _sceneTimeControls.gameObject.SetActive(visible&&!_sceneViewPanel.gameObject.activeSelf&&!_sceneFloorPanel.gameObject.activeSelf&&!_sceneNavigator.gameObject.activeSelf);
        _scenePauseLabel.text=_speed==0 ? "▶" : "Ⅱ";
        _sceneSpeedLabel.text=_speed==0 ? L("PLANNING","规划中") : _speed.ToString("0")+"×";
        if(!visible){CloseScenePanels();return;}
        _sceneModeLabel.text=_sceneInput!=null&&_sceneInput.IsDirectorMode ? L("DIRECTOR","经营指挥") : L("WALK","亲自巡视");
        _sceneFloorLabel.text=_sceneRig!=null ? SceneFloorName(_sceneRig.ViewedFloor) : "";
        _sceneFloorLabel.enableAutoSizing=true;_sceneFloorLabel.fontSizeMin=8;_sceneFloorLabel.fontSizeMax=11;
        if(_sceneNavigator.gameObject.activeSelf&&Sim!=null&&_sceneRig!=null) {
            int floor=_sceneRig.ViewedFloor;
            long states=17;for(int i=1;i<=50;i++){int n=(floor+1)*100+i;if(Sim.Rooms.Contains(n))states=unchecked(states*31+(int)Sim.Rooms.At(n).state);}
            string key=floor+"|"+states+"|"+Sim.Rooms.OpenRoomCount+"|"+Sim.Renovations.RoomsUnderRenovation+"|"+ServicePriorityRevision;
            if(key==_sceneNavigationKey)return;_sceneNavigationKey=key;
            _sceneNavigatorTitle.text=SceneFloorName(floor)+L(" / PLAN"," / 楼层平面");
            foreach(var pair in _sceneRoomButtons) {
                int number=(floor+1)*100+pair.Key;var button=pair.Value;
                bool exists=Sim.Rooms.Contains(number);button.interactable=exists;if(!exists)continue;
                var room=Sim.Rooms.At(number);var job=Sim.Renovations.JobFor(number);
                Color tint=job!=null ? Gold : RoomStatePalette.ColorOf(room.state);
                button.GetComponent<Image>().color=tint;
                var label=button.GetComponentInChildren<TextMeshProUGUI>();
                label.text=number+"\n"+(job!=null ? L("BUILDING","施工中") : _sceneDispatcher!=null&&_sceneDispatcher.IsPriority(number) ? L("PRIORITY","优先服务") : GameText.T(RoomStatePalette.WordOf(room.state)));
                label.color=room.state==RoomSimState.Ruined ? Cream : RevivalInk;
            }
        }
    }
    private float RoomWorkProgress(int number)
    {
        foreach(var agent in FindObjectsByType<StaffAgent>(FindObjectsSortMode.None))if(agent.AssignedRoomNumber==number)return agent.WorkProgress;
        return 0;
    }
    private void FitSceneActionCard()
    {
        float width=Mathf.Max(160,_roomDetail.rectTransform.rect.width);
        float detailHeight=Mathf.Clamp(_roomDetail.GetPreferredValues(_roomDetail.text,width,0).y+4,34,72);
        _roomCard.offsetMax=new Vector2(-8,88+121+detailHeight);
        SetAnchors(_roomDetail.rectTransform,Vector2.up,Vector2.one,new Vector2(16,-67-detailHeight),new Vector2(-14,-67));
    }
    private bool ConfigureServiceRoom(RoomRecord room)
    {
        if(room.state!=RoomSimState.Dirty&&room.state!=RoomSimState.Cleaning&&room.state!=RoomSimState.AwaitingInspection)return false;
        bool priority=_sceneDispatcher!=null&&_sceneDispatcher.IsPriority(room.number);
        bool cleaning=room.state==RoomSimState.Cleaning;
        _roomHeadline.text=cleaning ? L("CLEANING ","清洁进度 ")+Mathf.RoundToInt(RoomWorkProgress(room.number)*100)+"%"
            : priority ? L("PRIORITY SERVICE QUEUE","已加入优先服务队列") : L("RETURN THIS ROOM TO SALE","让这间房重新接待客人");
        _roomDetail.text=priority ? L("The next available worker will handle this room first. Your manager can keep inspecting elsewhere.","员工空闲后优先处理这间房；清洁、验房完成后即可出售，你可以继续查看其他区域。")
            : L("Direct your team from here. Cleaning and inspection restore sellable stock without walking your manager across the hotel.","直接安排团队服务。清洁、验房完成后恢复可售，无需经理逐间赶路。");
        SetRoomButton(_roomPrimary,_roomPrimaryLabel,cleaning ? L("HOUSEKEEPING ACTIVE","管家正在处理")
            : priority ? L("PRIORITY REQUESTED","已安排优先服务")
            : room.state==RoomSimState.AwaitingInspection ? L("PRIORITIZE INSPECTION","优先安排验房") : L("PRIORITIZE CLEANING","优先安排清洁"),
            ()=>PrioritizeRoomService(room.number),!priority&&!cleaning,Teal);
        SetRoomButton(_roomSecondary,_roomSecondaryLabel,L("FURNISH / UPGRADE","布置 / 升级"),OpenAssetsForSelectedRoom,true,Ink);
        return true;
    }
    private void PrioritizeRoomService(int number)
    {
        if(_sceneDispatcher==null)_sceneDispatcher=FindFirstObjectByType<TaskDispatcher>();
        var binder=FindRoomBinder(number);var room=binder!=null?binder.GetComponent<Room2DEntity>():null;
        if(room==null)foreach(var entity in FindObjectsByType<Room2DEntity>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(entity.roomNumber==number){room=entity;break;}
        if(_sceneDispatcher==null)return;
        bool ok=_sceneDispatcher.PrioritizeRoom(room,out string message);ShowToast(message);
        if(ok){binder?.Pulse();FindFirstObjectByType<SaveCoordinator>()?.RequestProgressAutosave();}
        ForceAllRefresh();
    }
}
