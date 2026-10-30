using System;
using System.Collections.Generic;
using System.Text;

public sealed partial class HotelSim
{
    public RoomDesignLedger RoomDesigns { get; }=new RoomDesignLedger();
    private string DesignFurnitureStamp(int room)
    {
        var b=new StringBuilder();foreach(var f in Furniture.InRoom(room))b.Append(f.instanceId).Append(':').Append(f.kindId).Append(':').Append(f.visualVariantId).Append(';');return b.ToString();
    }
    public RoomDesignDraft CreateRoomDesignDraft(int room)
    {
        if(!Rooms.Contains(room))return null;
        var saved=RoomDesigns.For(room);var plan=saved?.Clone()??new RoomDesignPlan{roomNumber=room};
        plan.pieces.RemoveAll(x=>!x.IsDecoration);int id=0;
        if(saved!=null)foreach(var p in saved.pieces)id=Math.Max(id,p.localId);
        foreach(var f in Furniture.InRoom(room)){
            RoomDesignPiece pose=null;if(saved!=null)foreach(var p in saved.pieces)if(!p.IsDecoration&&p.instanceId==f.instanceId&&p.kindId==f.kindId){pose=p.Clone();break;}
            if(pose==null)pose=RoomDesignGeometry.DefaultFurniture(f,++id);pose.variantId=f.visualVariantId;plan.pieces.Add(pose);
        }
        return new RoomDesignDraft{plan=plan,sourceRevision=saved?.revision??0,sourceFurniture=DesignFurnitureStamp(room)};
    }
    public bool CanDecorateRoom(int room,out string reason)
    {
        reason="";if(!Rooms.Contains(room)){reason="房间不存在。";return false;}
        var state=Rooms.At(room).state;
        if(state==RoomSimState.Occupied){reason="客人入住中：可预览和保存方案，退房后再应用。";return false;}
        if(state==RoomSimState.Ruined){reason="请先修复房间；现在可以预览并保存设计方案。";return false;}
        if(Renovations.IsRenovating(room)){reason="施工中：可预览和保存方案，交付后再应用。";return false;}
        if(state==RoomSimState.Cleaning){reason="员工正在打扫：请等本次服务完成后应用。";return false;}
        return true;
    }
    public RoomDesignQuote QuoteRoomDesign(RoomDesignDraft draft)
    {
        var quote=new RoomDesignQuote();
        if(draft?.plan==null){quote.reason="没有可应用的设计。";return quote;}
        var p=draft.plan;var saved=RoomDesigns.For(p.roomNumber);
        quote.roomAvailable=CanDecorateRoom(p.roomNumber,out var availability);
        if((saved?.revision??0)!=draft.sourceRevision||DesignFurnitureStamp(p.roomNumber)!=draft.sourceFurniture){quote.reason="房间已发生变化，请重新打开工作室。";return quote;}
        quote.reason=RoomDesignGeometry.Validate(p);
        if(quote.reason!="")return quote;
        if(p.wallStyle!=(saved?.wallStyle??0))quote.finishes+=RoomSurfaceCatalog.Wall(p.wallStyle).price;
        if(p.floorStyle!=(saved?.floorStyle??0))quote.finishes+=RoomSurfaceCatalog.Floor(p.floorStyle).price;
        if(p.lighting!=(saved?.lighting??0))quote.finishes+=RoomSurfaceCatalog.LightingPrices[p.lighting];
        float decor=0;
        foreach(var obj in p.pieces){
            if(obj.IsDecoration){
                bool owned=false;if(saved!=null)foreach(var old in saved.pieces)if(old.IsDecoration&&old.localId==obj.localId&&old.kindId==obj.kindId){owned=true;break;}
                if(!owned)quote.purchases+=RoomDecorCatalog.Get(obj.kindId).price;
            }else{
                if(obj.instanceId==0){quote.purchases+=FurnitureCatalog.Get(obj.kindId).cashCost;decor+=FurnitureCatalog.Get(obj.kindId).decorPoints;}
                else {
                    if(!Furniture.TryGet(obj.instanceId,out var existing)||existing.kindId!=obj.kindId||(existing.roomNumber!=0&&existing.roomNumber!=p.roomNumber)){quote.reason="某件家具已经不在仓库或这间房里，请重新选择。";return quote;}
                    decor+=FurnitureWearModel.EffectiveDecor(FurnitureCatalog.Get(obj.kindId).decorPoints,existing.newness,existing.IsFaulted);
                }
            }
        }
        if(saved!=null)foreach(var old in saved.pieces)if(old.IsDecoration&&!p.pieces.Exists(x=>x.IsDecoration&&x.localId==old.localId&&x.kindId==old.kindId))quote.recycling+=RoomDecorCatalog.Get(old.kindId).price/4;
        quote.quality=SimMath.Clamp01(decor/FurnitureCatalog.FullyFurnishedDecor+RoomDesignLedger.Score(p)*.004f);
        quote.valid=true;if(!quote.roomAvailable)quote.reason=availability;
        return quote;
    }
    public bool TryApplyRoomDesign(RoomDesignDraft draft,out string reason)
    {
        var q=QuoteRoomDesign(draft);reason=q.reason;
        if(!q.valid||!q.roomAvailable)return false;
        if(Cash<Math.Max(0,q.NetCost)){reason="差额需要 $"+q.NetCost+"，当前现金 $"+Cash+"。";return false;}
        var plan=draft.plan.Clone();var keep=new HashSet<int>();foreach(var p in plan.pieces)if(!p.IsDecoration&&p.instanceId>0)keep.Add(p.instanceId);
        // All checks complete before any money, item or room-state mutation.
        if(q.NetCost>0)TrySpendCash(q.NetCost);else Cash-=q.NetCost;
        foreach(var f in new List<FurnitureInstance>(Furniture.InRoom(plan.roomNumber)))if(!keep.Contains(f.instanceId))Furniture.MoveToRoom(f.instanceId,0);
        foreach(var p in plan.pieces)if(!p.IsDecoration){
            FurnitureInstance f;
            if(p.instanceId==0){f=Furniture.Place(plan.roomNumber,p.kindId,visualVariantId:p.variantId);p.instanceId=f.instanceId;}
            else {Furniture.MoveToRoom(p.instanceId,plan.roomNumber);Furniture.TryGet(p.instanceId,out f);f.visualVariantId=p.variantId;}
        }
        RoomDesigns.Set(plan);
        if(!Furniture.RequiredFurnitureWorking(plan.roomNumber))Rooms.SetState(plan.roomNumber,RoomSimState.Blocked);
        else if(Rooms.At(plan.roomNumber).state==RoomSimState.Ready||Rooms.At(plan.roomNumber).state==RoomSimState.AwaitingInspection)Rooms.SetState(plan.roomNumber,RoomSimState.Dirty);
        reason="布置完成，场景已更新。拆下的家具已收进仓库。";return true;
    }
    public void ApplyDesignBlueprint(RoomDesignDraft draft,RoomDesignPlan template)
    {
        if(draft?.plan==null||template==null)return;
        var original=CreateRoomDesignDraft(draft.plan.roomNumber);var p=template.Clone();p.roomNumber=draft.plan.roomNumber;p.revision=draft.sourceRevision;
        var used=new HashSet<int>();int id=0;
        foreach(var item in p.pieces){item.localId=++id;item.instanceId=0;if(item.IsDecoration)continue;
            foreach(var owned in Furniture.InRoom(p.roomNumber))if(owned.kindId==item.kindId&&!used.Contains(owned.instanceId)){item.instanceId=owned.instanceId;used.Add(owned.instanceId);break;}
            if(item.instanceId==0)foreach(var owned in Furniture.InRoom(0))if(owned.kindId==item.kindId&&!used.Contains(owned.instanceId)){item.instanceId=owned.instanceId;used.Add(owned.instanceId);break;}
        }
        // Keep matching owned decorative pieces when applying a template, so copying does not repurchase them.
        foreach(var item in p.pieces)if(item.IsDecoration){
            var old=original.plan.pieces.Find(x=>x.IsDecoration&&x.kindId==item.kindId&&!used.Contains(-x.localId));
            if(old!=null){used.Add(-old.localId);int occupied=p.pieces.FindIndex(x=>x!=item&&x.localId==old.localId);if(occupied>=0)p.pieces[occupied].localId=++id;item.localId=old.localId;}
        }
        draft.plan=p;
    }
    public bool TryAddDesignFurniture(RoomDesignDraft draft,int kindId,int inventoryId,out string reason)
    {
        reason="";if(draft?.plan==null||!FurnitureCatalog.TryGet(kindId,out var kind)){reason="家具不存在。";return false;}
        var plan=draft.plan;FurnitureInstance source=null;
        if(inventoryId>0){if(!Furniture.TryGet(inventoryId,out source)||source.kindId!=kindId||(source.roomNumber!=0&&source.roomNumber!=plan.roomNumber)){reason="这件家具不在可用仓库里。";return false;}}
        if(source==null)foreach(var owned in Furniture.InRoom(plan.roomNumber))if(owned.kindId==kindId){source=owned;break;}
        var old=plan.pieces.Find(x=>!x.IsDecoration&&FurnitureCatalog.Get(x.kindId).slot==kind.slot);
        var fake=source??new FurnitureInstance{kindId=kindId};var piece=RoomDesignGeometry.DefaultFurniture(fake,old?.localId??draft.NextLocalId());
        piece.kindId=kindId;piece.instanceId=source?.instanceId??0;
        var copy=plan.Clone();copy.pieces.RemoveAll(x=>!x.IsDecoration&&FurnitureCatalog.Get(x.kindId).slot==kind.slot);copy.pieces.Add(piece);
        var error=RoomDesignGeometry.Validate(copy,false);if(error!=""){reason=error;return false;}draft.plan=copy;return true;
    }
    public bool TryAddRoomDecoration(RoomDesignDraft draft,int kindId,out int localId,out string reason)
    {
        localId=0;reason="";if(draft?.plan==null||!RoomDecorCatalog.TryGet(kindId,out _)){reason="摆件不存在。";return false;}
        int count=0;foreach(var p in draft.plan.pieces)if(p.IsDecoration)count++;
        if(count>=RoomDecorCatalog.MaxPieces){reason="每间房最多摆放 8 件摆件。";return false;}
        var item=new RoomDesignPiece{localId=draft.NextLocalId(),kindId=kindId};
        if(!RoomDesignGeometry.FindFreePosition(draft.plan,item)){reason="暂时没有合适位置，请先调整已有物件。";return false;}
        draft.plan.pieces.Add(item);localId=item.localId;return true;
    }
}
