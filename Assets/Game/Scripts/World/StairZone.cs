using UnityEngine;
using UnityEngine.AI;

// 楼梯触发区：经理走进来 → NavMeshAgent.Warp 到目标层楼梯口 + 切层（电梯式，
// 不做跨层寻路）。exitPoint 落点须错开目标层的 StairZone 触发盒，防来回弹。
[RequireComponent(typeof(Collider))]
public class StairZone : MonoBehaviour
{
    [SerializeField] private int targetFloor;
    [SerializeField] private Transform exitPoint;   // 目标层楼梯口落点
    [SerializeField] private FloorVisibilityController floorVisibility;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (floorVisibility == null) floorVisibility = FindFirstObjectByType<FloorVisibilityController>();
    }

    [Tooltip("意图判定半径：只有寻路目的地落在楼梯格附近才触发，路过不触发")]
    [SerializeField] private float intentRadius = 1.6f;

    private void OnTriggerEnter(Collider other)
    {
        var manager = other.GetComponentInParent<ManagerController>();
        if (manager == null || exitPoint == null) return;

        var agent = manager.GetComponent<NavMeshAgent>();
        if (agent == null) return;

        // 意图判定：目的地必须就是这块楼梯格（水平距离），否则视为路过，不切层。
        // 防止去走廊尽头房间的寻路擦到触发盒被误传送。
        Vector3 dest = agent.hasPath ? agent.destination : manager.transform.position;
        Vector2 destXZ = new Vector2(dest.x, dest.z);
        Vector2 padXZ = new Vector2(transform.position.x, transform.position.z);
        if (Vector2.Distance(destXZ, padXZ) > intentRadius) return;

        agent.Warp(exitPoint.position);
        agent.ResetPath();

        if (floorVisibility != null) floorVisibility.ShowFloor(targetFloor);
    }
}
