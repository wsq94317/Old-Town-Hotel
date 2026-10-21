#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CourtyardFloorPlanTest
{
    [TestCase(2)] [TestCase(3)]
    public void FiftyRoomsFitTheBuildingWithoutOverlappingEachOtherOrTheLightWells(int floor)
    {
        var centres=new List<Vector3>();var wings=new int[5];
        for(int i=1;i<=50;i++){
            var pose=HotelFloorPlan.Room(floor*100+i);wings[pose.Wing]++;
            Assert.That(Mathf.Abs(pose.Centre.x)+2.5f,Is.LessThan(HotelFloorPlan.Width*.5f));
            Assert.That(Mathf.Abs(pose.Centre.z)+2.5f,Is.LessThan(HotelFloorPlan.Depth*.5f));
            Assert.That(Vector3.Distance(pose.Centre,pose.Door),Is.EqualTo(2.5f).Within(.001f));
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                Assert.That(HotelFloorPlan.InCourtyard(pose.Centre+new Vector3(x*2.49f,0,z*2.49f)),Is.False,"Room "+i);
            foreach(var prior in centres)Assert.That(Mathf.Abs(prior.x-pose.Centre.x)<4.99f&&Mathf.Abs(prior.z-pose.Centre.z)<4.99f,Is.False,"Room "+i+" overlaps another room.");
            centres.Add(pose.Centre);
        }
        CollectionAssert.AreEqual(new[]{12,12,12,12,2},wings);
    }
    [Test]
    public void ConsecutiveRoomNumbersFaceEachOtherAcrossTheSameShortCorridor()
    {
        for(int n=1;n<=48;n+=2){
            var a=HotelFloorPlan.Room(200+n);var b=HotelFloorPlan.Room(201+n);
            Assert.That(a.Centre.x,Is.EqualTo(b.Centre.x));Assert.That(a.Outward,Is.EqualTo(-b.Outward));
            Assert.That(Vector3.Distance(a.Door,b.Door),Is.EqualTo(3).Within(.001f));
        }
    }
    [Test]
    public void StructuralGridAndRoomStacksMatchOnBothGuestFloors()
    {
        for(int n=1;n<=50;n++){
            var lower=HotelFloorPlan.Room(200+n);var upper=HotelFloorPlan.Room(300+n);
            Assert.That(upper.Centre-lower.Centre,Is.EqualTo(Vector3.up*4));
            Assert.That(upper.Outward,Is.EqualTo(lower.Outward));
        }
        Assert.That(HotelFloorPlan.Width/HotelFloorPlan.Depth,Is.LessThan(2.2f));
    }
    [Test]
    public void OldWestWingCameraAndManagerPositionsMigrateToTheSameNumberedRoom()
    {
        var target=HotelFloorPlan.MigrateLegacyPoint(new Vector3(-112.5f,4,-4));
        var pose=HotelFloorPlan.Room(250);
        Assert.That(target,Is.EqualTo(pose.Door-pose.Outward));
        Assert.That(HotelFloorPlan.InCourtyard(target),Is.False);
    }
    [Test]
    public void SpatialMapKeepsEveryRoomTargetInsideThePlan()
    {
        for(int i=1;i<=50;i++){
            var p=HotelFloorPlan.MapPoint(HotelFloorPlan.Room(200+i).Centre);
            Assert.That(p.x,Is.InRange(.065f,.935f));Assert.That(p.y,Is.InRange(.029f,.971f));
        }
    }
}

#endif
