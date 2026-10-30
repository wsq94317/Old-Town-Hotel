using System;
using System.Collections.Generic;

public readonly struct RoomDesignCommission
{
    public readonly int id,reward;
    public readonly string en,cn,briefEn,briefCn;
    public RoomDesignCommission(int id,int reward,string en,string cn,string briefEn,string briefCn){this.id=id;this.reward=reward;this.en=en;this.cn=cn;this.briefEn=briefEn;this.briefCn=briefCn;}
}
public static class RoomDesignCommissions
{
    public static readonly RoomDesignCommission[] All={
        new RoomDesignCommission(1,150,"First signature room","第一间特色客房","Design score 8; clean, inspected and ready.","设计评分达到 8；清洁验房并可接待。"),
        new RoomDesignCommission(2,180,"Garden escape","花园小憩","Garden green walls, fern and botanical print; ready.","花园绿墙面、盆栽蕨叶、植物画作；可接待。"),
        new RoomDesignCommission(3,220,"Business retreat","商务阅读角","Work desk, bookcase and reading lamp; ready.","办公桌、橡木书架、阅读落地灯；可接待。"),
        new RoomDesignCommission(4,250,"Old town gallery","老城私人艺廊","Jade walls, gallery painting and brass mirror; ready.","玉绿墙面、艺廊画作、黄铜圆镜；可接待。")
    };
    public static bool Meets(int id,RoomDesignPlan plan)
    {
        if(plan==null)return false;
        bool Has(int kind)=>plan.pieces.Exists(x=>x.kindId==kind);
        switch(id){case 1:return RoomDesignLedger.Score(plan)>=8;
            case 2:return plan.wallStyle==1&&Has(RoomDecorCatalog.Fern)&&Has(RoomDecorCatalog.BotanicalArt);
            case 3:return Has(FurnitureCatalog.WorkDesk)&&Has(RoomDecorCatalog.Bookcase)&&Has(RoomDecorCatalog.Lamp);
            case 4:return plan.wallStyle==5&&Has(RoomDecorCatalog.AbstractArt)&&Has(RoomDecorCatalog.Mirror);
            default:return false;}
    }
}
public sealed partial class HotelSim
{
    private readonly HashSet<int> _claimedDesignCommissions=new HashSet<int>();
    public bool DesignCommissionClaimed(int id)=>_claimedDesignCommissions.Contains(id);
    public int EligibleDesignRoom(int id)
    {
        foreach(var plan in RoomDesigns.Capture())if(Rooms.Contains(plan.roomNumber)&&Rooms.At(plan.roomNumber).state==RoomSimState.Ready&&Furniture.RequiredFurnitureWorking(plan.roomNumber)&&RoomDesignCommissions.Meets(id,plan))return plan.roomNumber;
        return 0;
    }
    public bool TryClaimDesignCommission(int id,out string reason)
    {
        reason="";if(DesignCommissionClaimed(id)){reason="这份委托已领取。";return false;}
        foreach(var c in RoomDesignCommissions.All)if(c.id==id){if(EligibleDesignRoom(id)==0){reason="先应用满足要求的设计，再清洁验房，达到可接待状态。";return false;}_claimedDesignCommissions.Add(id);RecordCashCredit(c.reward);reason="委托完成，获得 $"+c.reward+"。";return true;}
        reason="委托不存在。";return false;
    }
}
