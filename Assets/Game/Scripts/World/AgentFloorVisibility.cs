using UnityEngine;

// 纸片人跨层可见性：只有当 agent 所在楼层 == 当前显示楼层时渲染。
// （楼层根隐藏管不到 agent——它们不在楼层树里。）
public class AgentFloorVisibility : MonoBehaviour
{
    private FloorVisibilityController _floors;
    private Renderer[] _renderers;

    private void Awake()
    {
        _floors = FindFirstObjectByType<FloorVisibilityController>();
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void LateUpdate()
    {
        if (_floors == null) return;
        bool visible = FloorMath.FloorIndexForY(transform.position.y) == _floors.CurrentFloor;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null) _renderers[i].enabled = visible;
        }
    }
}
