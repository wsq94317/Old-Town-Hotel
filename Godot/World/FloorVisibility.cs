using System;
using System.Collections.Generic;
using Godot;

// 楼层显隐：只显示当前层，其余整层隐藏。Unity 侧 FloorVisibilityController 的移植。
//
// 楼层高度不在这里重新定义——直接用 Sim 库里的 FloorMath（层高 4，index 0..6）。
// 那个文件在 Phase 0 就被抽进 OldTownHotel.Sim.csproj 了，所以 Unity 和 Godot 读的是
// **同一份**楼层约定，不存在两边各写一个 4 然后有一天改岔的可能。
public partial class FloorVisibility : Node
{
    private readonly List<Node3D> _floors = new List<Node3D>();

    /// <summary>楼层切换事件（参数 = 新楼层 index）。同层重复调用不触发。</summary>
    public event Action<int> FloorChanged;

    public int CurrentFloor { get; private set; }
    public int FloorCount => _floors.Count;

    public void Bind(IEnumerable<Node3D> floorRoots)
    {
        _floors.Clear();
        _floors.AddRange(floorRoots);
        Apply();
    }

    public void ShowFloor(int index)
    {
        if (_floors.Count == 0) return;
        index = Mathf.Clamp(index, 0, _floors.Count - 1);
        bool changed = index != CurrentFloor;
        CurrentFloor = index;
        Apply();
        if (changed) FloorChanged?.Invoke(index);
    }

    public void Step(int delta) => ShowFloor(CurrentFloor + delta);

    /// <summary>显示全部楼层（剖面总览）。不改 CurrentFloor，所以退出总览能回到原来那层。</summary>
    public void ShowAll()
    {
        foreach (var f in _floors) f.Visible = true;
    }

    private void Apply()
    {
        for (int i = 0; i < _floors.Count; i++)
            _floors[i].Visible = i == CurrentFloor;
    }
}
