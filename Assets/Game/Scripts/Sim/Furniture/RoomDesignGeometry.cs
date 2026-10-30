using System;
using System.Collections.Generic;

/// <summary>Canonical room coordinates always put the door on -Z. Unity mirrors only at presentation.</summary>
public static class RoomDesignGeometry
{
    public const float Grid=.25f, Edge=2.30f;
    public static float Snap(float value)=>(float)Math.Round(value/Grid,MidpointRounding.AwayFromZero)*Grid;
    public static float NormalizeYaw(float yaw){yaw%=360;if(yaw<0)yaw+=360;return (float)Math.Round(yaw/90)*90%360;}
    public static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
    public static RoomDecorMount Mount(int kind)
    {
        if(kind>=1000)return RoomDecorCatalog.Get(kind).mount;
        var slot=FurnitureCatalog.Get(kind).slot;return slot==FurnitureSlot.Wall?RoomDecorMount.Wall:slot==FurnitureSlot.Floor?RoomDecorMount.Rug:RoomDecorMount.Floor;
    }
    public static void Size(int kind,out float width,out float depth)
    {
        if(kind>=1000){var k=RoomDecorCatalog.Get(kind);width=k.width;depth=k.depth;return;}
        switch(FurnitureCatalog.Get(kind).slot){
            case FurnitureSlot.Bed:width=1.8f;depth=2.1f;break;
            case FurnitureSlot.Bathroom:width=1.4f;depth=1.8f;break;
            case FurnitureSlot.Entertainment:width=1.0f;depth=.6f;break;
            case FurnitureSlot.Seating:width=1.8f;depth=.9f;break;
            case FurnitureSlot.Desk:width=1.4f;depth=.75f;break;
            case FurnitureSlot.Wall:width=.85f;depth=.12f;break;
            default:width=1.6f;depth=1.6f;break;
        }
    }
    public static void OrientedSize(RoomDesignPiece p,out float w,out float d){Size(p.kindId,out w,out d);if(((int)Math.Round(p.yaw/90)&1)!=0){float t=w;w=d;d=t;}}
    public static RoomDesignPiece DefaultFurniture(FurnitureInstance item,int localId)
    {
        var p=new RoomDesignPiece{localId=localId,instanceId=item.instanceId,kindId=item.kindId,variantId=item.visualVariantId};
        switch(FurnitureCatalog.Get(item.kindId).slot){
            case FurnitureSlot.Bed:p.x=-1.25f;p.z=1.05f;break;
            case FurnitureSlot.Bathroom:p.x=1.5f;p.z=1.3f;break;
            case FurnitureSlot.Entertainment:p.x=1.55f;p.z=-.3f;break;
            case FurnitureSlot.Seating:p.x=-1.65f;p.z=-1.1f;p.yaw=270;break;
            case FurnitureSlot.Desk:p.x=1.5f;p.z=-1.45f;break;
            case FurnitureSlot.Wall:p.x=-2.25f;p.z=.65f;p.yaw=270;break;
            default:p.x=0;p.z=-.65f;break;
        }
        return p;
    }
    public static void Place(RoomDesignPiece p,float x,float z,float yaw)
    {
        p.x=Snap(x);p.z=Snap(z);p.yaw=NormalizeYaw(yaw);
        if(Mount(p.kindId)==RoomDecorMount.Wall)SnapToWall(p);
    }
    public static void SnapToWall(RoomDesignPiece p)
    {
        if(Math.Abs(p.x)>Math.Abs(p.z)) {p.x=p.x>=0?2.25f:-2.25f;p.z=Math.Max(-1.75f,Math.Min(1.75f,p.z));p.yaw=p.x>0?90:270;}
        else {p.z=p.z>=0?2.25f:-2.25f;p.x=Math.Max(-1.75f,Math.Min(1.75f,p.x));if(p.z<0&&Math.Abs(p.x)<1.2f)p.x=p.x<0?-1.5f:1.5f;p.yaw=p.z>0?0:180;}
    }
    public static void Rotate(RoomDesignPiece p)
    {
        if(Mount(p.kindId)==RoomDecorMount.Wall){float x=p.x;p.x=p.z;p.z=-x;SnapToWall(p);}
        else p.yaw=NormalizeYaw(p.yaw+90);
    }
    public static bool Overlap(RoomDesignPiece a,RoomDesignPiece b,float margin=0)
    {
        OrientedSize(a,out var aw,out var ad);OrientedSize(b,out var bw,out var bd);
        return Math.Abs(a.x-b.x)<(aw+bw)*.5f+margin-.005f&&Math.Abs(a.z-b.z)<(ad+bd)*.5f+margin-.005f;
    }
    private static bool OverlapBox(RoomDesignPiece p,float x,float z,float w,float d,float inflate=0)
    {
        OrientedSize(p,out var pw,out var pd);
        return Math.Abs(p.x-x)<(pw+w)*.5f+inflate-.005f&&Math.Abs(p.z-z)<(pd+d)*.5f+inflate-.005f;
    }
    public static string Validate(RoomDesignPlan plan,bool requireRequired=true)
    {
        if(plan==null||plan.pieces==null)return "没有可应用的设计。";
        if(plan.wallStyle<0||plan.wallStyle>=RoomSurfaceCatalog.Walls.Length||plan.floorStyle<0||plan.floorStyle>=RoomSurfaceCatalog.Floors.Length||plan.lighting<0||plan.lighting>=4)return "饰面方案不存在。";
        var ids=new HashSet<int>();var instances=new HashSet<int>();var slots=new HashSet<FurnitureSlot>();int decor=0,optional=0;
        bool bed=false,bath=false;
        foreach(var p in plan.pieces){
            if(p==null||p.localId<=0||!ids.Add(p.localId))return "有重复或无效的物件。";
            if(!Finite(p.x)||!Finite(p.z)||!Finite(p.yaw)||Math.Abs(p.yaw-NormalizeYaw(p.yaw))>.01f)return "摆放角度或坐标无效。";
            if(p.variantId<0||p.variantId>3)return "外观选项不存在。";
            if(p.IsDecoration){if(!RoomDecorCatalog.TryGet(p.kindId,out _))return "摆件不存在。";decor++;}
            else {
                if(!FurnitureCatalog.TryGet(p.kindId,out var k))return "家具不存在。";
                if(!slots.Add(k.slot))return "同一个家具功能位只能选一件。";
                bed|=k.slot==FurnitureSlot.Bed;bath|=k.slot==FurnitureSlot.Bathroom;if(!k.IsRequired)optional++;
                if(p.instanceId>0&&!instances.Add(p.instanceId))return "同一件家具不能摆放两次。";
            }
            OrientedSize(p,out var w,out var d);
            if(Math.Abs(p.x)+w*.5f>2.4f+.001f||Math.Abs(p.z)+d*.5f>2.4f+.001f)return "物件超出房间边界，请向内移动。";
            var mount=Mount(p.kindId);
            if(mount==RoomDecorMount.Wall){
                var copy=p.Clone();SnapToWall(copy);if(Math.Abs(copy.x-p.x)>.06f||Math.Abs(copy.z-p.z)>.06f||Math.Abs(copy.yaw-p.yaw)>.1f)return "墙饰需要贴墙，并朝向室内。";
            }
            if(mount==RoomDecorMount.Floor){
                if(OverlapBox(p,0,-2.25f,1.7f,.55f))return "请留出房门入口。";
                if(OverlapBox(p,0,0,.6f,.6f))return "请留出房间中央的通行空间。";
            }
        }
        if(requireRequired&&(!bed||!bath))return "需要保留一张床和一套卫浴，房间才能接待客人。";
        if(decor>RoomDecorCatalog.MaxPieces)return "每间房最多摆放 8 件摆件。";
        if(optional>FurnitureCatalog.OptionalSlotCount)return "最多安装 4 件功能家具。";
        for(int i=0;i<plan.pieces.Count;i++)for(int j=i+1;j<plan.pieces.Count;j++){
            var a=plan.pieces[i];var b=plan.pieces[j];var am=Mount(a.kindId);var bm=Mount(b.kindId);
            if(am==RoomDecorMount.Rug||bm==RoomDecorMount.Rug||am!=bm)continue;
            if(Overlap(a,b))return "有物件重叠，请移动红色物件。";
        }
        if(!HasAccess(plan))return "过道被堵住了，请留出从房门到房间中央的路线。";
        return "";
    }
    public static bool HasAccess(RoomDesignPlan plan)
    {
        const int n=17;var seen=new bool[n,n];var queue=new Queue<int>();queue.Enqueue(8);seen[8,0]=true;
        while(queue.Count>0){int v=queue.Dequeue(),x=v%n,z=v/n;if(x==8&&z==8)return true;
            foreach(int dir in new[]{-1,1,-n,n}){int q=v+dir,nx=q%n,nz=q/n;if(q<0||q>=n*n||nx<0||Math.Abs(nx-x)+Math.Abs(nz-z)!=1||seen[nx,nz])continue;
                bool blocked=false;foreach(var p in plan.pieces)if(Mount(p.kindId)==RoomDecorMount.Floor&&OverlapBox(p,(nx-8)*.25f,(nz-8)*.25f,0,0,.28f)){blocked=true;break;}
                if(!blocked){seen[nx,nz]=true;queue.Enqueue(q);}
            }
        }
        return false;
    }
    public static bool FindFreePosition(RoomDesignPlan plan,RoomDesignPiece piece)
    {
        if(Mount(piece.kindId)==RoomDecorMount.Wall){foreach(float z in new[]{-1.5f,0f,1.5f})foreach(float x in new[]{2.25f,-2.25f}){Place(piece,x,z,0);if(CanFit(plan,piece))return true;}foreach(float x in new[]{-1.5f,0,1.5f}){Place(piece,x,2.25f,0);if(CanFit(plan,piece))return true;}return false;}
        foreach(float z in new[]{1.75f,1.25f,.75f,.25f,-.25f,-.75f,-1.25f,-1.75f})foreach(float x in new[]{.25f,-.25f,.75f,-.75f,1.25f,-1.25f,1.75f,-1.75f}){Place(piece,x,z,0);if(CanFit(plan,piece))return true;}
        return false;
    }
    public static bool CanFit(RoomDesignPlan plan,RoomDesignPiece piece)
    {
        var copy=plan.Clone();copy.pieces.RemoveAll(x=>x.localId==piece.localId);copy.pieces.Add(piece.Clone());return Validate(copy,false)=="";
    }
}
