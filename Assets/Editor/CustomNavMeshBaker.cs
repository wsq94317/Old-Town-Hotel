using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// v2 世界层专用烘焙器：默认 Humanoid agent（半径 0.5/跨步 0.75）会把 2 米走廊
// 侵蚀到没法点击、把矮墙当台阶。此工具用 半径 0.3 / 跨步 0.4（agentTypeID 仍为 0，
// 场景里的 NavMeshAgent 无需改动）手动构建 NavMeshData 并持久化为资产。
// 场景几何改动后：菜单 Old Town Hotel → Bake Manager NavMesh 重烘焙。
public static class CustomNavMeshBaker
{
    private const string DataAssetPath = "Assets/Game/Scenes/Hotel_Manager_25D_NavMesh.asset";

    [MenuItem("Old Town Hotel/Bake Manager NavMesh")]
    public static void Bake()
    {
        var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        if (surface == null)
        {
            Debug.LogError("[CustomNavMeshBaker] no NavMeshSurface in scene");
            return;
        }

        // 收集物理碰撞体几何（含 Ignore Raycast 层上的 NavBlock；trigger 自动排除）
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(
            null, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
            new List<NavMeshBuildMarkup>(), sources);

        // 基于 Humanoid(0) 拷贝一份设置，改小半径/跨步——agentTypeID 保持 0
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.3f;
        settings.agentClimb = 0.4f;

        var bounds = new Bounds(new Vector3(0f, 6f, 0f), new Vector3(60f, 30f, 40f));
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null)
        {
            Debug.LogError("[CustomNavMeshBaker] build failed");
            return;
        }

        // 持久化数据资产并挂到 surface（替换旧资产内容，引用不变）
        var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(DataAssetPath);
        if (existing != null) AssetDatabase.DeleteAsset(DataAssetPath);
        AssetDatabase.CreateAsset(data, DataAssetPath);

        surface.RemoveData();
        surface.navMeshData = data;
        surface.AddData();
        EditorUtility.SetDirty(surface);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(surface.gameObject.scene);
        Debug.Log("[CustomNavMeshBaker] baked r=0.3 climb=0.4, sources=" + sources.Count);
    }
}
