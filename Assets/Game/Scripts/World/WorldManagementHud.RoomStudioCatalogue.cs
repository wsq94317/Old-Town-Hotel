using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class WorldManagementHud
{
    private void RebuildStudioCatalogue()
    {
        if(_studioContent==null||_studioDraft==null)return;
        foreach(Transform child in _studioContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        if(_studioCategory==0)BuildStudioFurniture();
        else if(_studioCategory==1){StudioNote(L("14 objects · up to 8 per room · varied pieces improve appeal","14 种摆件 · 每间最多 8 件 · 不同品类提升设计评分与客群偏好"));
            int i=0;RectTransform row=null;foreach(var kind in RoomDecorCatalog.All){if(i++%3==0)row=StudioRow(126);var k=kind;StudioProduct(row,"Decor_"+k.id,L(k.en,k.cn),"$"+k.price+" · +"+k.score,_studioPreview.Thumbnail(k.id),()=>StudioAddDecor(k.id));}}
        else if(_studioCategory==2||_studioCategory==3){bool wall=_studioCategory==2;StudioNote(L("Finishes are charged only when you apply the design.","饰面在应用时结算；保留当前款式不重复收费。"));int i=0;RectTransform row=null;
            foreach(var style in wall?RoomSurfaceCatalog.Walls:RoomSurfaceCatalog.Floors){if(i++%3==0)row=StudioRow(104);var s=style;bool selected=(wall?_studioDraft.plan.wallStyle:_studioDraft.plan.floorStyle)==s.id;var card=StudioProduct(row,"Surface_"+s.id,L(s.en,s.cn),(selected?L("SELECTED · ","已选 · "):"")+"$"+s.price,null,()=>{StudioChange(()=>{if(wall)_studioDraft.plan.wallStyle=s.id;else _studioDraft.plan.floorStyle=s.id;});RebuildStudioCatalogue();});
                var sample=CreatePanel("FinishSample",card,Hex(s.colour));SetAnchors(sample,new Vector2(0,.45f),Vector2.one,new Vector2(8,0),new Vector2(-8,-7));sample.GetComponent<Image>().raycastTarget=false;
                for(int n=0;n<4;n++){var stripe=CreatePanel("Pattern",sample,Hex(s.secondary));stripe.GetComponent<Image>().raycastTarget=false;SetAnchors(stripe,new Vector2(n*.25f,0),new Vector2(n*.25f+.02f,1),Vector2.zero,Vector2.zero);}
            }}
        else if(_studioCategory==4){StudioNote(L("Lighting sets the mood; furniture wear and faults stay unchanged.","改变房间氛围；家具磨损和故障仍然保留。"));string[] names={L("Heritage warm","原有暖光"),L("Amber evening","琥珀晚灯"),L("Daylight reading","明亮阅读"),L("Rose boutique","玫瑰精品")};string[] colours={"#F3DCAB","#EDB667","#D7E6E6","#E6B4BE"};var row=StudioRow(122);
            for(int i=0;i<4;i++){int light=i;var card=StudioProduct(row,"Lighting_"+i,names[i],(_studioDraft.plan.lighting==i?L("SELECTED ","已选 "):"")+"$"+RoomSurfaceCatalog.LightingPrices[i],null,()=>{StudioChange(()=>_studioDraft.plan.lighting=light);RebuildStudioCatalogue();});var swatch=CreatePanel("LightSample",card,Hex(colours[i]));SetAnchors(swatch,new Vector2(.2f,.5f),new Vector2(.8f,.94f),Vector2.zero,Vector2.zero);swatch.GetComponent<Image>().raycastTarget=false;}}
        else BuildStudioPlans();
        _studioCatalogue.GetComponent<ScrollRect>().verticalNormalizedPosition=1;RefreshRoomStudio();
    }
    private RectTransform StudioRow(float height)
    {
        var row=CreateRect("CatalogueRow",_studioContent);row.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
        var h=row.gameObject.AddComponent<HorizontalLayoutGroup>();h.spacing=6;h.childControlWidth=true;h.childControlHeight=true;h.childForceExpandWidth=true;return row;
    }
    private void StudioNote(string text)
    {var label=CreateText("CatalogueNote",_studioContent,11,RevivalInk,TextAlignmentOptions.MidlineLeft,false);label.text=text;label.textWrappingMode=TextWrappingModes.Normal;label.gameObject.AddComponent<LayoutElement>().preferredHeight=35;}
    private RectTransform StudioProduct(RectTransform row,string id,string name,string detail,Texture texture,Action action)
    {
        RectTransform card;TextMeshProUGUI label;CreateButton(id,row,out card,out label,"",Hex("#F9F3E2"),()=>action());label.gameObject.SetActive(false);
        card.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
        if(texture!=null){var image=CreateRect("ModelThumbnail",card).gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;SetAnchors(image.rectTransform,new Vector2(0,.36f),Vector2.one,new Vector2(5,0),new Vector2(-5,-5));}
        var title=CreateText("Name",card,11,RevivalInk,TextAlignmentOptions.Center,true);title.text=name;title.textWrappingMode=TextWrappingModes.Normal;SetAnchors(title.rectTransform,new Vector2(0,.17f),new Vector2(1,.39f),new Vector2(3,0),new Vector2(-3,0));title.raycastTarget=false;
        var price=CreateText("Price",card,10,Teal,TextAlignmentOptions.Center,false);price.text=detail;SetAnchors(price.rectTransform,Vector2.zero,new Vector2(1,.18f),new Vector2(3,1),new Vector2(-3,0));price.raycastTarget=false;return card;
    }
    private void BuildStudioFurniture()
    {
        var header=StudioRow(32);RectTransform r;TextMeshProUGUI label;
        CreateButton("StudioShop",header,out r,out label,L("CATALOGUE","购买目录"),!_studioInventory?Teal:Walnut,()=>{_studioInventory=false;RebuildStudioCatalogue();});label.fontSize=12;
        CreateButton("StudioStorage",header,out r,out label,L("STORAGE","仓库家具"),_studioInventory?Teal:Walnut,()=>{_studioInventory=true;RebuildStudioCatalogue();});label.fontSize=12;
        StudioNote(L("Choose a model to replace its slot. Drag it in the room; R rotates.","选款替换同类家具；直接在上方房间拖动位置，旋转按钮调整朝向。"));
        int i=0;RectTransform row=null;
        if(_studioInventory){foreach(var owned in Sim.Furniture.InRoom(0)){if(i++%3==0)row=StudioRow(130);var f=owned;StudioProduct(row,"Stored_"+f.instanceId,GameText.T(FurnitureCatalog.Get(f.kindId).name),L("OWNED · ","已拥有 · ")+Mathf.RoundToInt(f.newness*100)+"%",_studioPreview.Thumbnail(f.kindId,f.visualVariantId),()=>StudioAddFurniture(f.kindId,f.instanceId));}if(i==0)StudioNote(L("Storage is empty. Replaced furniture is kept here, with its original wear.","仓库暂时为空。替换下来的家具会收入这里，并保留原有磨损。"));}
        else foreach(var kind in FurnitureCatalog.All){if(i++%3==0)row=StudioRow(130);var k=kind;bool installed=_studioDraft.plan.pieces.Exists(p=>!p.IsDecoration&&p.kindId==k.kindId);StudioProduct(row,"Furniture_"+k.kindId,GameText.T(k.name),installed?L("IN ROOM · select","房内已有 · 选择"):"$"+k.cashCost,_studioPreview.Thumbnail(k.kindId),()=>{var current=_studioDraft.plan.pieces.Find(p=>!p.IsDecoration&&p.kindId==k.kindId);if(current!=null){_studioSelected=current.localId;_studioNeedsRefresh=true;RefreshRoomStudio();}else StudioAddFurniture(k.kindId,0);});}
    }
    private void StudioAddFurniture(int kind,int inventory)
    {
        var before=_studioDraft.Clone();if(Sim.TryAddDesignFurniture(_studioDraft,kind,inventory,out var reason)){RememberStudio(before);_studioSelected=_studioDraft.plan.pieces.Find(p=>!p.IsDecoration&&p.kindId==kind).localId;RebuildStudioCatalogue();}else{_studioMessage=reason;RefreshRoomStudio();}
    }
    private void StudioAddDecor(int kind)
    {var before=_studioDraft.Clone();if(Sim.TryAddRoomDecoration(_studioDraft,kind,out var id,out var reason)){RememberStudio(before);_studioSelected=id;}else _studioMessage=reason;RefreshRoomStudio();}
    private void BuildStudioPlans()
    {
        var nameRow=StudioRow(38);var namePanel=CreatePanel("DesignName",nameRow,Hex("#F9F3E2"));namePanel.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
        var input=namePanel.gameObject.AddComponent<TMP_InputField>();var nameText=CreateText("NameText",namePanel,13,RevivalInk,TextAlignmentOptions.MidlineLeft,false);Stretch(nameText.rectTransform,8);input.textViewport=namePanel;input.textComponent=nameText;input.characterLimit=28;input.lineType=TMP_InputField.LineType.SingleLine;
        var placeholder=CreateText("NamePlaceholder",namePanel,12,Teal,TextAlignmentOptions.MidlineLeft,false);Stretch(placeholder.rectTransform,8);placeholder.text=L("Name your room / blueprint…","为这间客房和方案取个名字…");input.placeholder=placeholder;input.text=_studioDraft.plan.name??"";
        input.onEndEdit.AddListener(value=>StudioChange(()=>_studioDraft.plan.name=value.Trim()));
        StudioNote(L("Style starters keep existing furniture. Saved plans can be reused in any room.","风格起点保留现有家具；个人方案可复制到其他房间，已有家具优先复用。"));
        var row=StudioRow(55);string[] titles={L("GARDEN","花园客房"),L("READING","阅读之夜"),L("GALLERY","老城艺廊")};
        for(int i=0;i<3;i++){int preset=i;RectTransform r;TextMeshProUGUI label;CreateButton("StudioPreset_"+i,row,out r,out label,titles[i],Teal,()=>StudioStyleStarter(preset));label.fontSize=12;}
        for(int i=0;i<4;i++){
            int slot=i;var saved=FindStudioBlueprint(slot);var line=StudioRow(48);var title=CreateText("BlueprintName",line,12,RevivalInk,TextAlignmentOptions.MidlineLeft,true);title.text=(slot+1)+" · "+(saved?.name??L("Empty plan","空方案"));title.gameObject.AddComponent<LayoutElement>().flexibleWidth=2;
            RectTransform r;TextMeshProUGUI label;var load=CreateButton("StudioLoadPlan_"+slot,line,out r,out label,L("LOAD","载入"),Teal,()=>{StudioChange(()=>Sim.ApplyDesignBlueprint(_studioDraft,FindStudioBlueprint(slot).plan));_studioSelected=0;});label.fontSize=11;load.interactable=saved!=null;r.gameObject.AddComponent<LayoutElement>().preferredWidth=66;
            CreateButton("StudioSavePlan_"+slot,line,out r,out label,L("SAVE","保存"),Walnut,()=>{var error=RoomDesignGeometry.Validate(_studioDraft.plan);if(error!=""){_studioMessage=error;RefreshRoomStudio();return;}Sim.RoomDesigns.SaveBlueprint(slot,string.IsNullOrWhiteSpace(_studioDraft.plan.name)?L("Room ","客房 ")+_studioDraft.plan.roomNumber:_studioDraft.plan.name,_studioDraft.plan);FindFirstObjectByType<SaveCoordinator>()?.RequestProgressAutosave();_studioMessage=L("Blueprint saved. No purchases were made.","方案已保存；尚未购置或扣钱。");RebuildStudioCatalogue();});label.fontSize=11;r.gameObject.AddComponent<LayoutElement>().preferredWidth=66;
        }
        StudioNote(L("DESIGN COMMISSIONS · once per hotel · committed, guest-ready rooms only","设计委托 · 每家酒店各领取一次 · 应用设计并清洁验房后结算"));
        foreach(var commission in RoomDesignCommissions.All){var c=commission;bool claimed=Sim.DesignCommissionClaimed(c.id);int eligible=Sim.EligibleDesignRoom(c.id);var line=StudioRow(78);var description=CreateText("CommissionBrief",line,11,RevivalInk,TextAlignmentOptions.MidlineLeft,false);description.text="<b>"+L(c.en,c.cn)+"</b>  +$"+c.reward+"\n"+L(c.briefEn,c.briefCn)+(eligible>0?"\n"+L("Ready: room ","已达标：客房 ")+eligible:"");description.textWrappingMode=TextWrappingModes.Normal;description.gameObject.AddComponent<LayoutElement>().flexibleWidth=3;
            RectTransform r;TextMeshProUGUI label;var claim=CreateButton("StudioClaim_"+c.id,line,out r,out label,claimed?L("DONE","已领取"):eligible>0?L("CLAIM","领取"):L("IN PROGRESS","进行中"),claimed?Walnut:Teal,()=>{if(Sim.TryClaimDesignCommission(c.id,out var reason)){FindFirstObjectByType<SaveCoordinator>()?.RequestProgressAutosave();_studioMessage=reason;RebuildStudioCatalogue();}else{_studioMessage=reason;RefreshRoomStudio();}});claim.interactable=!claimed&&eligible>0;r.gameObject.AddComponent<LayoutElement>().preferredWidth=82;label.fontSize=11;
        }
    }
    private RoomDesignBlueprint FindStudioBlueprint(int slot){foreach(var b in Sim.RoomDesigns.Blueprints)if(b.slot==slot)return b;return null;}
    private void StudioStyleStarter(int preset)
    {
        var before=_studioDraft.Clone();_studioDraft.plan.wallStyle=preset==0?1:preset==1?2:5;_studioDraft.plan.floorStyle=preset==0?1:preset==1?2:5;_studioDraft.plan.lighting=preset==0?0:preset==1?1:3;
        int[][] pieces={new[]{RoomDecorCatalog.Fern,RoomDecorCatalog.BotanicalArt},new[]{RoomDecorCatalog.Lamp,RoomDecorCatalog.Bookcase},new[]{RoomDecorCatalog.AbstractArt,RoomDecorCatalog.Mirror}};
        foreach(int kind in pieces[preset])if(!_studioDraft.plan.pieces.Exists(p=>p.kindId==kind))Sim.TryAddRoomDecoration(_studioDraft,kind,out _,out _);
        RememberStudio(before);_studioMessage=L("Starter applied to preview. Adjust it before checkout.","已生成风格预览，可以继续移动、增减物件。 ");RefreshRoomStudio();
    }
}
