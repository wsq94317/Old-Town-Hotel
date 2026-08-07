#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 把烘焙好的酒店几何导成 Godot 能读的 JSON。
//
// 为什么是「导出快照」而不是「移植生成器」：
// 几何的设计源头是 Editor/HotelEnvironmentArtBuilder.cs（61 KB），把它改写成 Godot 版
// 更可维护——但前提是预制体没被手工调过。这个导出器是忠实快照，先保证「Unity 里看到
// 什么，Godot 里就是什么」，同时也是判断有没有手改的依据（拿它和生成器重跑的结果比）。
//
// 只导出内置图元（Cube / Sphere / Cylinder / Capsule / Plane / Quad）。整座酒店几乎全是
// Cube，所以这一层覆盖够用；碰到非图元网格会单独记进 skipped 里**并计数**——
// 静默跳过会让 Godot 那边缺一块墙而没人知道。
//
// 用法：菜单 Tools / Godot Port / Export Hotel Geometry
public static class GodotGeometryExporter
{
    private const string OutPath = "Godot/data/hotel-geometry.json";

    // Unity 内置图元网格的 fileID -> 名字。图元 mesh 全部来自同一个内置资源 guid。
    private static readonly Dictionary<string, string> PrimitiveByMeshName = new Dictionary<string, string>
    {
        { "Cube", "cube" }, { "Sphere", "sphere" }, { "Cylinder", "cylinder" },
        { "Capsule", "capsule" }, { "Plane", "plane" }, { "Quad", "quad" },
    };

    [MenuItem("Tools/Godot Port/Export Hotel Geometry")]
    public static void Export()
    {
        var floors = new List<string>();
        for (int i = 1; i <= 7; i++)
        {
            string p = FindPrefab("Floor" + i);
            if (p != null) floors.Add(p);
            else Debug.LogWarning($"[GodotExport] Floor{i}.prefab not found — skipping");
        }

        if (floors.Count == 0)
        {
            Debug.LogError("[GodotExport] no floor prefabs found; nothing exported");
            return;
        }

        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        int total = 0, skipped = 0;
        var skippedMeshes = new SortedDictionary<string, int>();

        sb.Append("{\n  \"format\": 1,\n  \"floors\": [\n");

        for (int f = 0; f < floors.Count; f++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(floors[f]);
            if (prefab == null) continue;

            // 楼层的垂直偏移只存在于**场景实例**上（World/FloorN 的 localPosition.y），
            // 预制体资产里的几何全部在局部原点。第一版漏了这一步，7 层楼在 Godot 里
            // 叠成了一层——包围盒 Y 只有 2.97 而不是 28，但画面看着"像是"渲染成功了。
            // 从活场景读真实值，读不到才退回 FloorMath 的约定（index × 4）并告警。
            float baseY = LookUpFloorY(prefab.name, f);

            sb.Append("    {\n      \"name\": \"").Append(prefab.name)
              .Append("\",\n      \"baseY\": ").Append(baseY.ToString("0.####", inv))
              .Append(",\n      \"parts\": [\n");

            var filters = prefab.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            var parts = new List<string>();

            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                if (!PrimitiveByMeshName.TryGetValue(mf.sharedMesh.name, out string prim))
                {
                    skipped++;
                    skippedMeshes.TryGetValue(mf.sharedMesh.name, out int n);
                    skippedMeshes[mf.sharedMesh.name] = n + 1;
                    continue;
                }

                Transform t = mf.transform;
                // 用 lossy（世界）变换：预制体内部层级有嵌套缩放，逐级还原到 Godot
                // 容易在某一层错一个乘法。世界空间是唯一不会歧义的口径。
                Vector3 pos = t.position;
                Quaternion rot = t.rotation;
                Vector3 scl = t.lossyScale;

                var mr = mf.GetComponent<MeshRenderer>();
                Color c = mr != null && mr.sharedMaterial != null && mr.sharedMaterial.HasProperty("_BaseColor")
                    ? mr.sharedMaterial.GetColor("_BaseColor")
                    : mr != null && mr.sharedMaterial != null && mr.sharedMaterial.HasProperty("_Color")
                        ? mr.sharedMaterial.color
                        : Color.magenta;   // 刺眼的兜底，别让"没找到颜色"看起来像设计

                string matName = mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "none";

                parts.Add(string.Format(inv,
                    "        {{\"n\":\"{0}\",\"m\":\"{1}\",\"p\":[{2:0.####},{3:0.####},{4:0.####}]," +
                    "\"r\":[{5:0.####},{6:0.####},{7:0.####},{8:0.####}],\"s\":[{9:0.####},{10:0.####},{11:0.####}]," +
                    "\"c\":[{12:0.####},{13:0.####},{14:0.####},{15:0.####}],\"mat\":\"{16}\",\"vis\":{17}}}",
                    Escape(t.name), prim,
                    pos.x, pos.y, pos.z,
                    rot.x, rot.y, rot.z, rot.w,
                    scl.x, scl.y, scl.z,
                    c.r, c.g, c.b, c.a,
                    Escape(matName),
                    (mr != null && mr.enabled && IsActiveInPrefab(t, prefab.transform)) ? "true" : "false"));
                total++;
            }

            sb.Append(string.Join(",\n", parts));
            sb.Append("\n      ]\n    }");
            if (f < floors.Count - 1) sb.Append(',');
            sb.Append('\n');
        }

        sb.Append("  ],\n");
        sb.Append("  \"exportedParts\": ").Append(total.ToString(inv)).Append(",\n");
        sb.Append("  \"skippedNonPrimitive\": ").Append(skipped.ToString(inv)).Append(",\n");
        sb.Append("  \"skippedMeshes\": {");
        var entries = new List<string>();
        foreach (var kv in skippedMeshes)
            entries.Add($"\"{Escape(kv.Key)}\": {kv.Value}");
        sb.Append(string.Join(", ", entries));
        sb.Append("}\n}\n");

        string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString().Replace("\r\n", "\n"));

        Debug.Log($"[GodotExport] {total} primitive parts from {floors.Count} floors -> {OutPath}" +
                  (skipped > 0 ? $"  |  SKIPPED {skipped} non-primitive meshes: {string.Join(", ", entries)}" : "  |  no non-primitive meshes"));
    }

    /// <summary>楼层在场景里的实际高度。场景没开就退回 FloorMath 的约定并告警。</summary>
    private static float LookUpFloorY(string floorName, int index)
    {
        GameObject world = GameObject.Find("World");
        if (world != null)
        {
            Transform t = world.transform.Find(floorName);
            if (t != null) return t.localPosition.y;
        }
        float fallback = index * 4f;   // FloorMath.FloorHeight
        Debug.LogWarning($"[GodotExport] {floorName} not found under World in the open scene; " +
                         $"falling back to FloorMath convention y={fallback}. " +
                         $"Open Hotel_Manager_25D and re-export to capture the real placement.");
        return fallback;
    }

    /// <summary>
    /// 预制体**资产**里的有效激活状态。
    ///
    /// 不能用 activeInHierarchy：预制体资产不在任何场景里，所以它对资产内的每一个对象
    /// 都返回 false。第一版就是栽在这儿——1,390 个零件全部导成 vis:false，Godot 那边
    /// 渲染出一片纯背景色，而且因为包围盒是零，相机还走了兜底分支，看起来像"数据没加载"。
    /// 正确做法是从自身往上 walk 到预制体根，逐级看 activeSelf。
    /// </summary>
    private static bool IsActiveInPrefab(Transform t, Transform root)
    {
        for (Transform cur = t; cur != null; cur = cur.parent)
        {
            if (!cur.gameObject.activeSelf) return false;
            if (cur == root) break;
        }
        return true;
    }

    private static string FindPrefab(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return path;
        }
        return null;
    }

    private static string Escape(string s) =>
        s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
#endif
