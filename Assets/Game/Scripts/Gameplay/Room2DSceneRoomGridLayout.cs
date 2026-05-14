using System.Collections.Generic;
using UnityEngine;

// 场景级房间网格布局工具。
// 用途:复制 Room_A_2D prefab 后,把所有子房间按行列网格批量排版,避免手动调每个 prefab 的 Transform。
//
// 使用流程:
//   1. 找到场景里的 "Rooms" GameObject(12 个 Room2DEntity prefab 的父对象);若没有,挂在任何
//      Room2DEntity 的共同父对象上即可
//   2. Add Component → Room2DSceneRoomGridLayout
//   3. Inspector 调 columns / cellSize / originLocal
//   4. 右键组件标题 → "Layout Children In Grid"
//   5. 完成。12 房按 columns × ceil(N/columns) 行展开,无重叠
//
// 设计依据(对齐项目惯例):
//   - 与 Room2DPrototypeRoomConfigApplier 同样的 ContextMenu + lastResult debug 字段风格
//   - 不依赖 Room2DEntity 的 floorNumber/roomNumber 字段(那些字段由 Room2DOverview 在布局后
//     按场景位置自动重新编号 —— 详见 Room2DOverview.numberRoomsByScenePosition)
//   - 子物体顺序按 Hierarchy 顺序读取;若想换布局顺序,在 Hierarchy 里上下拖拽即可
public class Room2DSceneRoomGridLayout : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("每行容纳多少个房间。12 房常用值:4(=3 行 4 列)或 6(=2 行 6 列)。")]
    [Min(1)] public int columns = 4;

    [Tooltip("X / Y 方向房间间距(world units)。默认匹配既有 Room_A_2D prefab 的 1.6 / 3 spacing。")]
    public Vector2 cellSize = new Vector2(1.6f, 3.0f);

    [Tooltip("起始 localPosition(网格的左下角第一间房)。默认对齐既有 prefab 实例。")]
    public Vector2 originLocal = new Vector2(-275.07f, -492.2f);

    [Tooltip("Z 坐标(深度;默认 0.102 匹配既有 prefab Z 偏移)。")]
    public float zLocal = 0.102f;

    [Header("Filter")]
    [Tooltip("只对 Room2DEntity 类型子对象生效;避免误移动 UI / 装饰物。建议勾上。")]
    public bool onlyRoom2DEntity = true;

    [Header("Debug Result")]
    [Tooltip("最近一次 Layout 操作的结果(用于 Inspector 反馈)。")]
    public int lastLaidOutCount;
    public string lastResult = "None";

    [ContextMenu("Layout Children In Grid")]
    public void LayoutChildrenInGrid()
    {
        // 收集要排版的子 Transform。
        List<Transform> targets = new List<Transform>();
        if (onlyRoom2DEntity)
        {
            // 只挑 Room2DEntity 组件的子物体(避免误动 UI 节点)。
            // GetComponentsInChildren 返回顺序按 Hierarchy 深度优先;直接子物体次序即 Hierarchy 顺序。
            Room2DEntity[] entities = GetComponentsInChildren<Room2DEntity>(true);
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] != null && entities[i].transform.parent == transform)
                {
                    targets.Add(entities[i].transform);
                }
            }
        }
        else
        {
            // 直接遍历所有直接子物体。
            for (int i = 0; i < transform.childCount; i++)
            {
                targets.Add(transform.GetChild(i));
            }
        }

        if (targets.Count == 0)
        {
            lastLaidOutCount = 0;
            lastResult = "No children matched filter under " + name + ".";
            return;
        }

        // 网格布局:row = floor(index / columns),col = index % columns。
        // Y 方向向上递增(row 0 在最下面 = originLocal.y),符合"楼层往上"直觉。
        int safeCols = Mathf.Max(1, columns);
        for (int i = 0; i < targets.Count; i++)
        {
            int row = i / safeCols;
            int col = i % safeCols;

            Vector3 newPos = new Vector3(
                originLocal.x + col * cellSize.x,
                originLocal.y + row * cellSize.y,
                zLocal);

            targets[i].localPosition = newPos;
        }

        lastLaidOutCount = targets.Count;
        int totalRows = Mathf.CeilToInt(targets.Count / (float)safeCols);
        lastResult = "Laid out " + targets.Count + " rooms in "
            + safeCols + " cols × " + totalRows + " rows."
            + (onlyRoom2DEntity ? " (Room2DEntity filter on)" : "");
    }

    [ContextMenu("Reset Last Result")]
    public void ResetLastResult()
    {
        lastLaidOutCount = 0;
        lastResult = "None";
    }
}
