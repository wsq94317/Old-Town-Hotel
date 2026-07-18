using NUnit.Framework;

namespace OldTownHotel.Tests.EditMode
{
    public class StaffRoutineLogicTest
    {
        [Test]
        public void DefaultActivities_MatchRoleResponsibilities()
        {
            Assert.AreEqual(StaffActivityState.AtPost,
                StaffRoutineLogic.DefaultActivityFor(StaffRole.Reception));
            Assert.AreEqual(StaffActivityState.Patrol,
                StaffRoutineLogic.DefaultActivityFor(StaffRole.Inspector));
            Assert.AreEqual(StaffActivityState.Idle,
                StaffRoutineLogic.DefaultActivityFor(StaffRole.Housekeeper));
        }

        [Test]
        public void OnlyOnDutyFieldStaff_AutoAcceptRoomTasks()
        {
            Assert.IsTrue(StaffRoutineLogic.CanAutoAcceptRoomTask(
                StaffRole.Housekeeper,
                StaffShiftState.OnDuty,
                StaffActivityState.Idle,
                false));
            Assert.IsTrue(StaffRoutineLogic.CanAutoAcceptRoomTask(
                StaffRole.Inspector,
                StaffShiftState.OnDuty,
                StaffActivityState.Patrol,
                false));
            Assert.IsFalse(StaffRoutineLogic.CanAutoAcceptRoomTask(
                StaffRole.Reception,
                StaffShiftState.OnDuty,
                StaffActivityState.AtPost,
                false));
            Assert.IsFalse(StaffRoutineLogic.CanAutoAcceptRoomTask(
                StaffRole.Housekeeper,
                StaffShiftState.LeavingMap,
                StaffActivityState.Leaving,
                false));
            Assert.IsFalse(StaffRoutineLogic.CanAutoAcceptRoomTask(
                StaffRole.Housekeeper,
                StaffShiftState.OnDuty,
                StaffActivityState.Working,
                true));
        }

        [Test]
        public void RoutineTiming_UsesPlayableBounds()
        {
            var rng = new System.Random(1234);
            float needDelay = StaffRoutineLogic.NextPersonalNeedDelaySeconds(StaffRole.Housekeeper, rng);
            float normalToilet = StaffRoutineLogic.ToiletDurationSeconds(false, rng);
            float hiding = StaffRoutineLogic.ToiletDurationSeconds(true, rng);

            Assert.That(needDelay, Is.InRange(22f, 46f));
            Assert.That(normalToilet, Is.InRange(4f, 8f));
            Assert.That(hiding, Is.InRange(18f, 32f));
            Assert.Greater(hiding, normalToilet);
        }

        [Test]
        public void ExternalSatisfactionPenalty_HasConfirmedOneMinuteGrace()
        {
            Assert.AreEqual(60f, StaffRoutineLogic.FrontDeskVacancyGraceSeconds);
            Assert.Less(StaffRoutineLogic.FrontDeskVacancySatisfactionPenalty, 0);
        }
    }
}
