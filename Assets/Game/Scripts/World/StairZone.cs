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

    private void OnTriggerEnter(Collider other)
    {
        var manager = other.GetComponentInParent<ManagerController>();
        if (manager == null || exitPoint == null) return;

        var agent = manager.GetComponent<NavMeshAgent>();
        if (agent == null) return;
        agent.Warp(exitPoint.position);
        agent.ResetPath();

        if (floorVisibility != null) floorVisibility.ShowFloor(targetFloor);
    }
}
