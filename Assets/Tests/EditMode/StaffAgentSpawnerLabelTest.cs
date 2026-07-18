using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class StaffAgentSpawnerLabelTest
    {
        [Test]
        public void BuildWorldLabel_UsesDisplayNameAndRole()
        {
            var member = new StaffMember(StaffRole.Inspector, "Mia", 60);

            string label = StaffAgentSpawner.BuildWorldLabel(member);

            Assert.That(label, Is.EqualTo("Mia\nInspector"));
        }

        [Test]
        public void StaffBreakRoom_ProvidesDistinctLobbyIdleAnchors()
        {
            var go = new UnityEngine.GameObject("BreakRoomTest");
            go.transform.position = new UnityEngine.Vector3(-6f, FloorMath.BaseYFor(0), 1.25f);
            var room = go.AddComponent<StaffBreakRoom>();

            UnityEngine.Vector3 housekeeperAnchor = room.GetIdleAnchorForRole(StaffRole.Housekeeper, 0);
            UnityEngine.Vector3 inspectorAnchor = room.GetIdleAnchorForRole(StaffRole.Inspector, 0);

            Assert.That(FloorMath.FloorIndexForY(housekeeperAnchor.y), Is.EqualTo(0));
            Assert.That(FloorMath.FloorIndexForY(inspectorAnchor.y), Is.EqualTo(0));
            Assert.That(housekeeperAnchor, Is.Not.EqualTo(inspectorAnchor));

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
