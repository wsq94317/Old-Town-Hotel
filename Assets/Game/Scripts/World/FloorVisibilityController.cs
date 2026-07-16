using UnityEngine;

// 只显示当前楼层：其他层根节点整体 SetActive(false)。
// 相机通过 OnFloorChanged 订阅切层跳变。
public class FloorVisibilityController : MonoBehaviour
{
    [SerializeField] private GameObject[] floorRoots = new GameObject[FloorMath.FloorCount];

    /// <summary>楼层切换事件（参数=新楼层 index）。同层重复调用不触发。</summary>
    public event System.Action<int> OnFloorChanged;

    public int CurrentFloor { get; private set; } = 0;

    /// <summary>EditMode 测试接缝：注入楼层根节点数组。</summary>
    public void SetFloorsForTesting(GameObject[] floors) => floorRoots = floors;

    public void ShowFloor(int index)
    {
        index = Mathf.Clamp(index, 0, FloorMath.FloorCount - 1);
        bool changed = index != CurrentFloor;
        CurrentFloor = index;
        for (int i = 0; i < floorRoots.Length; i++)
        {
            if (floorRoots[i] != null) floorRoots[i].SetActive(i == index);
        }
        if (changed) OnFloorChanged?.Invoke(index);
    }

    private void Start() => ShowFloor(CurrentFloor);
}
