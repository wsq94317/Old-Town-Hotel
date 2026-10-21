using UnityEngine;

/// <summary>The shared architectural grid. Every floor, room and map uses these coordinates.</summary>
public static class HotelFloorPlan
{
    public const int Revision=1;
    public const float Width=86, Depth=40, WingCorridorZ=12, OuterGalleryX=41;
    public static readonly Vector3 Entrance=new Vector3(0,0,-19.2f);
    public static readonly Vector3 Linen=new Vector3(-6.5f,0,8);
    public static readonly Vector2 Min=new Vector2(-42,-19), Max=new Vector2(42,19);
    public readonly struct RoomPose
    {
        public readonly Vector3 Centre, Outward;
        public readonly int Wing;
        public Vector3 Door=>Centre-Outward*2.5f;
        public RoomPose(Vector3 centre,Vector3 outward,int wing){Centre=centre;Outward=outward;Wing=wing;}
    }
    public static RoomPose Room(int number)
    {
        int n=Mathf.Clamp(number%100,1,50)-1;
        float y=FloorMath.BaseYFor(Mathf.Clamp(number/100-1,1,2));
        if(n>=48)return new RoomPose(new Vector3(n==48?37.5f:-37.5f,y,0),n==48?Vector3.left:Vector3.right,4);
        int wing=n/12,slot=n%12;
        float side=wing%2==0?-1:1,corridor=wing<2?WingCorridorZ:-WingCorridorZ,outward=slot%2==0?1:-1;
        return new RoomPose(new Vector3(side*(12.5f+slot/2*5),y,corridor+outward*4),Vector3.forward*outward,wing);
    }
    public static string WingName(int number,bool chinese)
    {
        int wing=Room(number).Wing;
        return chinese?new[]{"西北翼 A","东北翼 B","西南翼 C","东南翼 D","庭院端厅"}[wing]
            :new[]{"Northwest A","Northeast B","Southwest C","Southeast D","Garden ends"}[wing];
    }
    public static float LocalDoorSide(Transform room)
    {var wall=room.Find("Wall_Corr_L");return wall!=null?Mathf.Sign(wall.localPosition.z):(room.localPosition.z>=0?-1:1);}
    // Portrait plan: north is right, the long building axis runs up the page.
    public static Vector2 MapPoint(Vector3 world)=>new Vector2(.5f+world.z/Depth,.5f-world.x/Width);
    public static bool InCourtyard(Vector3 p)=>Mathf.Abs(p.x)>10&&Mathf.Abs(p.x)<35&&Mathf.Abs(p.z)<5.5f;
    public static Vector3 MigrateLegacyPoint(Vector3 point)
    {
        int floor=FloorMath.FloorIndexForY(point.y);
        if(floor!=1&&floor!=2)return new Vector3(Mathf.Clamp(point.x,-42,42),point.y,Mathf.Clamp(point.z,-19,19));
        int best=(floor+1)*100+1;float distance=float.MaxValue;
        for(int n=1;n<=50;n++){
            Vector2 old;
            if(n<=4)old=new Vector2(-7.5f+(n-1)*5,4);
            else if(floor==1&&n<=8)old=new Vector2(-7.5f+(n-5)*5,-4);
            else if(floor==1&&n<=12)old=new Vector2(n%2==1?-12.5f:-17.5f,n<=10?4:-4);
            else if(floor==2&&n<=6)old=new Vector2(-12.5f-(n-5)*5,4);
            else if(floor==2&&n<=12)old=new Vector2(-17.5f+(n-7)*5,-4);
            else old=new Vector2(-22.5f-((n-13)/2)*5,n%2==1?4:-4);
            float d=(old-new Vector2(point.x,point.z)).sqrMagnitude;
            if(d<distance){distance=d;best=(floor+1)*100+n;}
        }
        return Room(best).Door-Room(best).Outward;
    }
}
