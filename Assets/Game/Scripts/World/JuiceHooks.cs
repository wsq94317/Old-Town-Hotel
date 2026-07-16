using UnityEngine;

// JuiceKit 接入点：订阅模拟层事件 → 飘字/震屏。纯表现，不改任何模拟状态。
//   退房入账 → 房间头顶绿色 +$ 飘字
//   现场抓包 → 震屏 + 员工头顶红色 "CAUGHT!"
public class JuiceHooks : MonoBehaviour
{
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;

    private void Start()
    {
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (demandLoop != null) demandLoop.OnDepartureCheckedOut += HandleCheckout;
        StaffAgent.OnAnyCaught += HandleCaught;
    }

    private void OnDestroy()
    {
        if (demandLoop != null) demandLoop.OnDepartureCheckedOut -= HandleCheckout;
        StaffAgent.OnAnyCaught -= HandleCaught;
    }

    private void HandleCheckout(Room2DEntity room, int amount, bool byPlayer)
    {
        if (room == null || amount <= 0) return;
        FloatingTextFx.Spawn(room.transform.position, "+$" + amount, new Color(0.35f, 0.95f, 0.4f));
    }

    private void HandleCaught(StaffAgent agent)
    {
        if (agent == null) return;
        CameraShaker.Shake(0.18f, 0.35f);
        FloatingTextFx.Spawn(agent.transform.position, "CAUGHT!", new Color(1f, 0.3f, 0.25f), 1.2f);
    }
}
