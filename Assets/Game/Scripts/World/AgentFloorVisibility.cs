using UnityEngine;

// 散养物体跨层可见性：只有当自身所在楼层 == 当前显示楼层时渲染。
// 适用于一切不在楼层树里的运行时生成物（纸片人/损坏标记/烟雾/飘字…）——
// 楼层根 SetActive(false) 管不到它们。
// 渲染器懒缓存：AddComponent 当帧子物体可能还没建出来（如 SmokePuffs 在 Start
// 里生成烟团），首个 LateUpdate 再收集；子物体增删自动失效重收。
// EmoteBubble 自管显隐（要表达"隐藏"语义），这里跳过以免每帧把它掰回可见。
public class AgentFloorVisibility : MonoBehaviour
{
    private FloorVisibilityController _floors;
    private Renderer[] _renderers;
    private Collider[] _colliders;
    private bool _forceHidden;

    private void Awake() => _floors = FindFirstObjectByType<FloorVisibilityController>();

    private void OnTransformChildrenChanged()
    {
        _renderers = null;
        _colliders = null;
    }

    public void SetForceHidden(bool hidden)
    {
        _forceHidden = hidden;
        ApplyVisibility();
    }

    private void LateUpdate()
    {
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (_renderers == null) _renderers = Collect();
        if (_colliders == null) _colliders = GetComponentsInChildren<Collider>(true);
        bool visible = !_forceHidden
            && (_floors == null || FloorMath.FloorIndexForY(transform.position.y) == _floors.CurrentFloor);
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null) _renderers[i].enabled = visible;
        }
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null) _colliders[i].enabled = visible;
        }
    }

    private Renderer[] Collect()
    {
        var all = GetComponentsInChildren<Renderer>(true);
        var list = new System.Collections.Generic.List<Renderer>(all.Length);
        foreach (var r in all)
            if (r.GetComponent<EmoteBubble>() == null) list.Add(r);
        return list.ToArray();
    }
}
