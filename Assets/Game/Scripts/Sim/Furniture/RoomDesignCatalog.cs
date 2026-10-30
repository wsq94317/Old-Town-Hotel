using System;
using System.Collections.Generic;

[Serializable]
public sealed class RoomDesignPiece
{
    public int localId, instanceId, kindId, variantId;
    public float x,z,yaw;
    public bool IsDecoration => kindId>=1000;
    public RoomDesignPiece Clone() => (RoomDesignPiece)MemberwiseClone();
}
[Serializable]
public sealed class RoomDesignPlan
{
    public int roomNumber,revision,wallStyle,floorStyle,lighting;
    public string name="";
    public List<RoomDesignPiece> pieces=new List<RoomDesignPiece>();
    public RoomDesignPlan Clone(){var p=(RoomDesignPlan)MemberwiseClone();p.pieces=new List<RoomDesignPiece>();if(pieces!=null)foreach(var x in pieces)if(x!=null)p.pieces.Add(x.Clone());return p;}
}
[Serializable]
public sealed class RoomDesignBlueprint
{
    public int slot;
    public string name;
    public RoomDesignPlan plan;
}
public sealed class RoomDesignDraft
{
    public RoomDesignPlan plan;
    public int sourceRevision;
    public string sourceFurniture;
    public RoomDesignDraft Clone()=>new RoomDesignDraft{plan=plan.Clone(),sourceRevision=sourceRevision,sourceFurniture=sourceFurniture};
    public int NextLocalId(){int n=0;foreach(var p in plan.pieces)n=Math.Max(n,p.localId);return n+1;}
}
public sealed class RoomDesignQuote
{
    public int purchases,finishes,recycling;
    public int NetCost=>purchases+finishes-recycling;
    public bool valid,roomAvailable;
    public string reason="";
    public float quality;
}
public enum RoomDecorMount { Floor, Wall, Rug }
public readonly struct RoomDecorKind
{
    public readonly int id,price,score;
    public readonly string en,cn;
    public readonly float width,depth;
    public readonly RoomDecorMount mount;
    public readonly GuestSegment segment;
    public RoomDecorKind(int id,string en,string cn,int price,int score,float width,float depth,RoomDecorMount mount,GuestSegment segment)
    {this.id=id;this.en=en;this.cn=cn;this.price=price;this.score=score;this.width=width;this.depth=depth;this.mount=mount;this.segment=segment;}
}
public static class RoomDecorCatalog
{
    public const int Fern=1001,Lamp=1002,Bookcase=1003,Mirror=1004,BotanicalArt=1005,AbstractArt=1006,SideTable=1007,Trunk=1008,TallPlant=1009,Nightstand=1010,TeaCorner=1011,Clock=1012,Runner=1013,Armchair=1014;
    public const int MaxPieces=8;
    public static readonly RoomDecorKind[] All={
        new RoomDecorKind(Fern,"Potted fern","盆栽蕨叶",70,2,.45f,.45f,RoomDecorMount.Floor,GuestSegment.Budget),
        new RoomDecorKind(Lamp,"Reading lamp","阅读落地灯",110,3,.42f,.42f,RoomDecorMount.Floor,GuestSegment.Business),
        new RoomDecorKind(Bookcase,"Oak bookcase","橡木书架",150,4,.9f,.35f,RoomDecorMount.Floor,GuestSegment.Business),
        new RoomDecorKind(Mirror,"Brass mirror","黄铜圆镜",90,2,.65f,.12f,RoomDecorMount.Wall,GuestSegment.Vip),
        new RoomDecorKind(BotanicalArt,"Botanical print","植物画作",65,2,.75f,.12f,RoomDecorMount.Wall,GuestSegment.Budget),
        new RoomDecorKind(AbstractArt,"Gallery painting","艺廊画作",105,3,.85f,.12f,RoomDecorMount.Wall,GuestSegment.Vip),
        new RoomDecorKind(SideTable,"Round side table","圆边几",85,2,.55f,.55f,RoomDecorMount.Floor,GuestSegment.Vip),
        new RoomDecorKind(Trunk,"Vintage trunk","复古旅行箱",125,3,.8f,.45f,RoomDecorMount.Floor,GuestSegment.Party),
        new RoomDecorKind(TallPlant,"Indoor tree","室内绿树",135,3,.55f,.55f,RoomDecorMount.Floor,GuestSegment.Budget),
        new RoomDecorKind(Nightstand,"Bedside cabinet","床头柜",95,2,.55f,.45f,RoomDecorMount.Floor,GuestSegment.Vip),
        new RoomDecorKind(TeaCorner,"Tea console","茶饮边柜",175,4,.95f,.5f,RoomDecorMount.Floor,GuestSegment.Business),
        new RoomDecorKind(Clock,"Station clock","复古挂钟",55,1,.5f,.12f,RoomDecorMount.Wall,GuestSegment.Business),
        new RoomDecorKind(Runner,"Woven runner","编织长地毯",95,2,1.0f,1.8f,RoomDecorMount.Rug,GuestSegment.Party),
        new RoomDecorKind(Armchair,"Reading armchair","阅读扶手椅",220,4,.85f,.85f,RoomDecorMount.Floor,GuestSegment.Vip)
    };
    public static bool TryGet(int id,out RoomDecorKind kind){foreach(var k in All)if(k.id==id){kind=k;return true;}kind=default;return false;}
    public static RoomDecorKind Get(int id){TryGet(id,out var k);return k;}
}
public readonly struct RoomSurfaceStyle
{
    public readonly int id,price,pattern;
    public readonly string en,cn,colour,secondary;
    public RoomSurfaceStyle(int id,string en,string cn,int price,int pattern,string colour,string secondary)
    {this.id=id;this.en=en;this.cn=cn;this.price=price;this.pattern=pattern;this.colour=colour;this.secondary=secondary;}
}
public static class RoomSurfaceCatalog
{
    public static readonly RoomSurfaceStyle[] Walls={
        new RoomSurfaceStyle(0,"Heritage sage","原有鼠尾草",0,0,"#A3B3A0","#F1D9AE"),
        new RoomSurfaceStyle(1,"Garden green","花园绿",80,1,"#668E7D","#E8D7B7"),
        new RoomSurfaceStyle(2,"Ink blue","静谧墨蓝",110,2,"#50687F","#E5D3AC"),
        new RoomSurfaceStyle(3,"Terracotta","暖陶杏",95,1,"#C68E76","#F5DEC0"),
        new RoomSurfaceStyle(4,"Ivory gallery","奶油艺廊",120,3,"#E4D9C4","#82694A"),
        new RoomSurfaceStyle(5,"Deco jade","玉绿装饰风",155,2,"#35655E","#D9B56E"),
        new RoomSurfaceStyle(6,"Lavender dusk","暮色紫灰",105,1,"#8E879D","#E5D8C7")
    };
    public static readonly RoomSurfaceStyle[] Floors={
        new RoomSurfaceStyle(0,"Heritage walnut","原有胡桃木",0,0,"#AF8A5D","#8B6949"),
        new RoomSurfaceStyle(1,"Honey oak","蜜色橡木",100,0,"#C9A36C","#AC8555"),
        new RoomSurfaceStyle(2,"Smoked parquet","烟熏拼木",140,1,"#75604F","#A18767"),
        new RoomSurfaceStyle(3,"Ivory terrazzo","奶油水磨石",130,2,"#DCD2BC","#AFA48F"),
        new RoomSurfaceStyle(4,"Garden tiles","花园拼砖",100,2,"#A4B6A0","#D4D6BB"),
        new RoomSurfaceStyle(5,"Herringbone","复古人字拼",165,3,"#997045","#C09B68")
    };
    public static readonly int[] LightingPrices={0,60,80,90};
    public static RoomSurfaceStyle Wall(int id)=>Walls[SimMath.Clamp(id,0,Walls.Length-1)];
    public static RoomSurfaceStyle Floor(int id)=>Floors[SimMath.Clamp(id,0,Floors.Length-1)];
}
public sealed class RoomDesignLedger
{
    private readonly Dictionary<int,RoomDesignPlan> _rooms=new Dictionary<int,RoomDesignPlan>();
    private readonly List<RoomDesignBlueprint> _blueprints=new List<RoomDesignBlueprint>();
    public IReadOnlyList<RoomDesignBlueprint> Blueprints=>_blueprints;
    public RoomDesignPlan For(int room)=>_rooms.TryGetValue(room,out var value)?value:null;
    public void Set(RoomDesignPlan plan){var clone=plan.Clone();clone.revision=(For(plan.roomNumber)?.revision??0)+1;_rooms[plan.roomNumber]=clone;}
    public List<RoomDesignPlan> Capture(){var list=new List<RoomDesignPlan>();foreach(var pair in _rooms)list.Add(pair.Value.Clone());list.Sort((a,b)=>a.roomNumber.CompareTo(b.roomNumber));return list;}
    public void Restore(List<RoomDesignPlan> plans,List<RoomDesignBlueprint> blueprints)
    {
        _rooms.Clear();_blueprints.Clear();if(plans!=null)foreach(var p in plans)if(p!=null&&p.roomNumber>0&&RoomDesignGeometry.Validate(p)=="")_rooms[p.roomNumber]=p.Clone();
        if(blueprints!=null)foreach(var b in blueprints)if(b!=null&&b.plan!=null&&b.slot>=0&&b.slot<4&&RoomDesignGeometry.Validate(b.plan)=="")SaveBlueprint(b.slot,b.name,b.plan);
    }
    public void SaveBlueprint(int slot,string name,RoomDesignPlan plan)
    {
        if(slot<0||slot>=4||plan==null)return;
        _blueprints.RemoveAll(x=>x.slot==slot);var p=plan.Clone();foreach(var item in p.pieces)item.instanceId=0;
        _blueprints.Add(new RoomDesignBlueprint{slot=slot,name=string.IsNullOrWhiteSpace(name)?"Room "+plan.roomNumber:name.Trim().Substring(0,Math.Min(28,name.Trim().Length)),plan=p});
        _blueprints.Sort((a,b)=>a.slot.CompareTo(b.slot));
    }
    public static int Score(RoomDesignPlan p)
    {
        if(p==null)return 0;int score=(p.wallStyle>0?2:0)+(p.floorStyle>0?2:0)+(p.lighting>0?1:0);var unique=new HashSet<int>();
        if(p.pieces!=null)foreach(var piece in p.pieces)if(piece!=null&&piece.IsDecoration&&unique.Add(piece.kindId)&&RoomDecorCatalog.TryGet(piece.kindId,out var k))score+=k.score;
        return Math.Min(20,score);
    }
    public float QualityBonus(int room)=>Score(For(room))*.004f;
    public float AppealFor(int room,GuestSegment segment)
    {
        var p=For(room);if(p==null)return 0;float value=0;var unique=new HashSet<int>();foreach(var item in p.pieces)if(item.IsDecoration&&unique.Add(item.kindId)&&RoomDecorCatalog.TryGet(item.kindId,out var k)&&k.segment==segment)value+=.1f;
        return Math.Min(.4f,value);
    }
}
