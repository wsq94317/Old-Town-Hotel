#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HotelDoorVisualBaker
{
    [MenuItem("Old Town Hotel/Art/Fix Room Door Clearances")]
    public static void Bake()
    {
        if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop play mode before updating room doors.");
        foreach(string path in new[]{"Assets/Game/Prefabs/Maps/Floors/Floor2.prefab","Assets/Game/Prefabs/Maps/Floors/Floor3.prefab"}) {
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                foreach(var door in root.GetComponentsInChildren<RoomDoor>(true)) UpdateDoor(door);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach(var door in Object.FindObjectsByType<RoomDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
            // Refreshing a linked scene instance before its prefab would create duplicate added-object overrides.
            var panel=door.transform.Find("DoorPanel");
            if(panel!=null) foreach(var child in panel.GetComponentsInChildren<Transform>(true).Where(x=>x.name=="DoorCraftwork"||x.name=="DoorDetailCombined"||x.name.StartsWith("Craft_")).ToArray())
                if(child!=null && PrefabUtility.IsAddedGameObjectOverride(child.gameObject)) Object.DestroyImmediate(child.gameObject);
            UpdateDoor(door);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }
    private static void UpdateDoor(RoomDoor door)
    {
        door.RefreshDoorVisuals(); EditorUtility.SetDirty(door);
        var detail=door.transform.Find("DoorPanel/DoorCraftwork");
        if(detail!=null && detail.Find("DoorDetailCombined")==null) {
            var filters=detail.GetComponentsInChildren<MeshFilter>(true);
            var combined=new GameObject("DoorDetailCombined"); combined.layer=2; combined.transform.SetParent(detail,false);
            foreach(var group in filters.GroupBy(x=>x.GetComponent<MeshRenderer>().sharedMaterial)) {
                string meshPath="Assets/Game/Resources/HotelArt/Surfaces/Shape_Door_"+group.Key.name+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(mesh==null) {
                    mesh=new Mesh {name="Door_"+group.Key.name};
                    mesh.CombineMeshes(group.Select(x=>new CombineInstance {
                        mesh=x.sharedMesh,transform=detail.worldToLocalMatrix*x.transform.localToWorldMatrix
                    }).ToArray(),true,true);
                    AssetDatabase.CreateAsset(mesh,meshPath);
                }
                var part=new GameObject(group.Key.name,typeof(MeshFilter),typeof(MeshRenderer)); part.layer=2;
                part.transform.SetParent(combined.transform,false); part.GetComponent<MeshFilter>().sharedMesh=mesh;
                part.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var filter in filters) Object.DestroyImmediate(filter.gameObject);
        }
        foreach(var renderer in door.GetComponentsInChildren<Renderer>(true)) {
            var material=renderer.sharedMaterial;
            if(material==null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material))) continue;
            string path="Assets/Game/Resources/HotelArt/Surfaces/"+material.name+".mat";
            var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(saved==null) AssetDatabase.CreateAsset(material,path); else renderer.sharedMaterial=saved;
        }
    }
}
#endif
