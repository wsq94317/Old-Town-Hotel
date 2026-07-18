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
        // 关掉 URP 渲染调试器的运行时面板：Device Simulator 的多指手势会误触发它，
        // 面板一开就吞掉全部游戏输入（表现为"点不动了"+ 点走廊弹出 Display Stats）。
        // 用反射调用（DebugManager 所在程序集未直接暴露给 Assembly-CSharp）。
        try
        {
            var dmType = System.Type.GetType(
                "UnityEngine.Rendering.DebugManager, Unity.RenderPipelines.Core.Runtime");
            var instance = dmType?.GetProperty("instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
            dmType?.GetProperty("enableRuntimeUI")?.SetValue(instance, false);
        }
        catch { /* 调试器不存在就算了 */ }

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

        float deadline = Time.time + 12f;
        while (Time.time < deadline)
        {
            bool everyoneOffMap = true;
            foreach (var staff in FindObjectsByType<StaffAgent>(FindObjectsSortMode.None))
            {
                if (staff != null && staff.ShiftState != StaffShiftState.OffShift)
                {
                    everyoneOffMap = false;
                    break;
                }
            }

            if (everyoneOffMap) break;
            yield return null;
        }

        if (dayController != null) dayController.RestartDemoDay();
    }

    private void OnGUI()
    {
        if (dayController == null) return;
        GuiScale.Begin(); // 高 DPI 下等比放大（左上角固定坐标随矩阵缩放）
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
        var period = DayPeriodLogic.PeriodFor(dayController.Clock.CurrentHour);
        GUI.Label(new Rect(10, 10, 640, 22), $"Day {dayController.CurrentDay}  {dayController.Clock.CurrentTimeFormatted}  · {DayPeriodLogic.Label(period)}");
        GUI.Label(new Rect(10, 32, 600, 22), $"Cash ${(economy != null ? economy.Cash : dayController.PlayerCash)}   Prestige {ManagerReputation.Prestige}");
        GUI.Label(new Rect(10, 54, 600, 22), $"Rooms  dirty:{dirty}  cleaning:{cleaning}  inspect:{awaiting}  occupied:{occupied}");
        GUI.Label(new Rect(10, 76, 600, 22), demandLoop != null ? $"Guests  served:{demandLoop.successfulDemandCount}  queue:{demandLoop.UpcomingQueueCount}  checkouts:{demandLoop.simulatedCheckoutCount}" : "");
    }
}
