using NUnit.Framework;
using UnityEngine;

// day-cycle v2：18:00 打烊清场（Recovery 进入时送走所有等待入住的客人）回归测试。
namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class DemandLoopClosingTest
    {
        private GameObject _root;
        private Room2DPrototypeDemandLoop _loop;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("closing-test-root");
            _loop = _root.AddComponent<Room2DPrototypeDemandLoop>();
            _loop.autoFindReferences = false;
            _loop.rooms = new Room2DEntity[0];
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void ClearWaitingGuests_ClearsUpcomingQueue()
        {
            _loop.ScheduleUpcomingDemandPreview(); // 填满到 capacity（默认 2）
            Assert.Greater(_loop.UpcomingQueueCount, 0);
            int queued = _loop.UpcomingQueueCount;

            _loop.ClearWaitingGuestsForClosing();

            Assert.AreEqual(0, _loop.UpcomingQueueCount);
            Assert.AreEqual("None", _loop.upcomingDemandPreviewText);
            Assert.IsNull(_loop.reservedRoomForUpcomingDemand);
            Assert.AreEqual(queued, _loop.lastClosingClearedGuestCount);
        }

        [Test]
        public void ClearWaitingGuests_ClearsActiveWaitingGuest()
        {
            _loop.activeDemandWaitingForManualAssignment = true;
            _loop.activeDemandWaitSeconds = 30f;

            _loop.ClearWaitingGuestsForClosing();

            Assert.IsFalse(_loop.activeDemandWaitingForManualAssignment);
            Assert.AreEqual(0f, _loop.activeDemandWaitSeconds);
            Assert.AreEqual(1, _loop.lastClosingClearedGuestCount);
        }

        [Test]
        public void ClearWaitingGuests_ClearsComplaintReassignmentWaiting()
        {
            _loop.complaintWaitingForReassignment = true;

            _loop.ClearWaitingGuestsForClosing();

            Assert.IsFalse(_loop.complaintWaitingForReassignment);
            Assert.AreEqual(1, _loop.lastClosingClearedGuestCount);
        }

        [Test]
        public void ClearWaitingGuests_AlsoKillsBrewingComplaintSeed()
        {
            // 种子（计时器阶段）过夜会在次日清晨爆成愤怒客人——打烊必须连根拔
            var roomGo = new GameObject("room");
            roomGo.transform.SetParent(_root.transform);
            _loop.pendingComplaintRoom = roomGo.AddComponent<Room2DEntity>();
            _loop.pendingComplaintTimerSeconds = 55f;

            _loop.ClearWaitingGuestsForClosing();

            Assert.IsNull(_loop.pendingComplaintRoom, "投诉种子必须清掉");
            Assert.AreEqual(0f, _loop.pendingComplaintTimerSeconds);
            Assert.AreEqual(0, _loop.lastClosingClearedGuestCount,
                            "种子还没变成站在前台的人，不计入送走数");
        }

        [Test]
        public void ClearWaitingGuests_EmptyStateIsNoOp()
        {
            _loop.ClearWaitingGuestsForClosing();
            Assert.AreEqual(0, _loop.lastClosingClearedGuestCount);
        }
    }
}
