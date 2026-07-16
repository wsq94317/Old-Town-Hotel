using System.Collections;
using UnityEngine;

// M2 临时调试 HUD：左上角四行状态 + 日结后自动续天。
// 正式 HUD（顶栏/事件栈/日结演出）属 M6，届时删除本组件。
public class DebugHudOverlay : MonoBehaviour
{
    [SerializeField] private Room2DDemoDayController dayController;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private float autoContinueDelaySeconds = 3f;

    private void Awake()
    {
        if (dayController == null) dayController = FindFirstObjectByType<Room2DDemoDayController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
    }

    private void Start()
    {
        if (dayController != null) dayController.OnDaySettled += HandleDaySettled;
    }

    private void OnDestroy()
    {
        if (dayController != null) dayController.OnDaySettled -= HandleDaySettled;
    }

    // 日结后自动翻天（临时——M6 的日结 UI 会接管 Continue）。
    private void HandleDaySettled(int day, int served, DayLedger ledger)
    {
        StartCoroutine(AutoContinue());
    }

    private IEnumerator AutoContinue()
    {
        yield return new WaitForSeconds(autoContinueDelaySeconds);
        if (dayController != null) dayController.RestartDemoDay();
    }

    private void OnGUI()
    {
        if (dayController == null) return;
        int dirty = 0, cleaning = 0, awaiting = 0, occupied = 0;
        if (demandLoop != null && demandLoop.rooms != null)
        {
            foreach (var r in demandLoop.rooms)
            {
                if (r == null) continue;
                switch (r.currentState)
                {
                    case Room2DState.Dirty: dirty++; break;
                    case Room2DState.Cleaning: cleaning++; break;
                    case Room2DState.AwaitingInspection: awaiting++; break;
                    case Room2DState.Occupied: occupied++; break;
                }
            }
        }
        GUI.Label(new Rect(10, 10, 600, 22), $"Day {dayController.CurrentDay}  {dayController.Clock.CurrentTimeFormatted}  [{dayController.currentPhase}]");
        GUI.Label(new Rect(10, 32, 600, 22), $"Cash ${(economy != null ? economy.Cash : dayController.PlayerCash)}   Prestige {ManagerReputation.Prestige}");
        GUI.Label(new Rect(10, 54, 600, 22), $"Rooms  dirty:{dirty}  cleaning:{cleaning}  inspect:{awaiting}  occupied:{occupied}");
        GUI.Label(new Rect(10, 76, 600, 22), demandLoop != null ? $"Guests  served:{demandLoop.successfulDemandCount}  queue:{demandLoop.UpcomingQueueCount}  checkouts:{demandLoop.simulatedCheckoutCount}" : "");
    }
}
