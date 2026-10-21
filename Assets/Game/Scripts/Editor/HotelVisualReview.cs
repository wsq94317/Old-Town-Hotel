#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class HotelVisualReview
{
    private const string Folder="Assets/Game/Resources/HotelArt/Revival";
    private static string _recordName;private static int _recordFrame,_recordCount;private static double _nextFrame;
    public static void StartRecording(string name,int frames=36)
    {
        _recordName=name;_recordCount=frames;_recordFrame=0;_nextFrame=EditorApplication.timeSinceStartup;
        Directory.CreateDirectory("Captures/"+name);EditorApplication.update-=RecordFrame;EditorApplication.update+=RecordFrame;
    }
    private static void RecordFrame()
    {
        if(!EditorApplication.isPlaying||_recordFrame>=_recordCount){EditorApplication.update-=RecordFrame;return;}
        if(EditorApplication.timeSinceStartup<_nextFrame)return;
        Capture(_recordName+"/frame-"+_recordFrame.ToString("D3"));_recordFrame++;_nextFrame=EditorApplication.timeSinceStartup+.1;
    }
    [MenuItem("Old Town Hotel/Art/Bake Revival Visuals Into Current Scene")]
    public static void Bake()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before baking scene art.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="Hotel_Manager_25D")throw new InvalidOperationException("Open the manager hotel scene first.");
        Directory.CreateDirectory("Captures");
        string backup="Captures/Hotel_Manager_before_visual.unity";if(!File.Exists(backup))File.Copy(scene.path,backup);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        HotelArtDirection.Install();
        HotelLightingDirector.Install();
        foreach(var room in Object.FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
            Combine(room.transform.Find("RevivalArchitecture"),"RoomArchitecture_"+room.RoomNumber);
            Combine(room.transform.Find("RevivalRuins"),"RoomRuins_"+room.RoomNumber);
            if(room.RoomNumber>=201&&room.RoomNumber<=208&&room.transform.Find("RevivalFurniturePreview")==null){
                var preview=new GameObject("RevivalFurniturePreview");preview.tag="EditorOnly";preview.transform.SetParent(room.transform,false);
                if(FurnitureCatalog.TryGet(FurnitureCatalog.ProperBed,out var kind)){var bed=HotelFurnitureArt.Create(kind,0);bed.transform.SetParent(preview.transform,false);bed.transform.localPosition=new Vector3(-.6f,0,.4f);PersistMaterials(bed);}
            }
        }
        var lobby=GameObject.Find("World/Floor1/RevivalArchitecture");if(lobby!=null)Combine(lobby.transform,"LobbyArchitecture");
        var manager=Object.FindFirstObjectByType<ManagerController>();if(manager!=null){HotelCharacterArt.Create(manager.transform,StaffRole.Manager);manager.transform.Find("Visual").rotation=Quaternion.Euler(0,225,0);PersistMaterials(manager.gameObject);}
        var catalog=FurnitureVisualCatalogSO.LoadDefault();
        foreach(var entry in catalog.Entries) {
            if(!FurnitureCatalog.TryGet(entry.kindId,out var kind))continue;
            entry.EnsureSlots();
            for(int v=0;v<4;v++){
                var model=HotelFurnitureArt.Create(kind,v);if(model==null)continue;
                Combine(model.transform,"Furniture_"+entry.kindId+"_"+v);
                PersistMaterials(model);
                var prefab=PrefabUtility.SaveAsPrefabAsset(model,Folder+"/Furniture_"+entry.kindId+"_"+v+".prefab");
                entry.variants[v].prefab=prefab;entry.variants[v].rendererMaterialOverrides=Array.Empty<Material>();
                entry.variants[v].displayName=new[]{"Concierge","Garden","Heritage","Midnight"}[v];
                Object.DestroyImmediate(model);
            }
        }
        EditorUtility.SetDirty(catalog);
        foreach(StaffRole role in Enum.GetValues(typeof(StaffRole))) {
            var root=new GameObject("Cast_"+role);HotelCharacterArt.Create(root.transform,role);PersistMaterials(root);
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Cast_"+role+".prefab");Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Revival art baked: furnished scene, four articulated uniforms, twenty-four furniture prefabs, shared static meshes.");
    }
    private static void PersistMaterials(GameObject root)
    {
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)) {
            var mesh=filter.sharedMesh;if(mesh==null||!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))continue;
            string id=System.Text.RegularExpressions.Regex.Replace(mesh.name,"[^A-Za-z0-9_.-]","_");
            string path="Assets/Game/Resources/HotelArt/Surfaces/Shape_"+id+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null)AssetDatabase.CreateAsset(mesh,path);else { EditorUtility.CopySerialized(mesh,existing);filter.sharedMesh=existing; }
        }
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var mat in r.sharedMaterials) {
            if(mat==null||!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat)))continue;
            string path=mat.name.StartsWith("Craft_")?"Assets/Game/Resources/HotelArt/Surfaces/"+mat.name+".mat":Folder+"/Mat_"+ColorUtility.ToHtmlStringRGB(mat.color)+".mat";
            var asset=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(asset==null){mat.enableInstancing=true;AssetDatabase.CreateAsset(mat,path);}else{
                var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)if(mats[i]==mat)mats[i]=asset;r.sharedMaterials=mats;
            }
        }
    }
    private static void Combine(Transform root,string id)
    {
        if(root==null||root.Find("CombinedArt")!=null)return;
        PersistMaterials(root.gameObject);
        var filters=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.GetComponent<MeshRenderer>()!=null&&f.sharedMesh!=null).ToArray();
        var combinedRoot=new GameObject("CombinedArt");combinedRoot.layer=2;combinedRoot.transform.SetParent(root,false);
        foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial)) {
            var mesh=new Mesh(){name=id+"_"+group.Key.name,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            string path=Folder+"/Mesh_"+id+"_"+group.Key.name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
            var go=new GameObject(group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(combinedRoot.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var filter in filters)Object.DestroyImmediate(filter.gameObject);
    }
    public static string Capture(string name)
    {
        var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(type);window.Focus();window.Repaint();
        var rt=(RenderTexture)type.GetField("m_RenderTexture",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
        if(rt==null)throw new InvalidOperationException("Repaint Game View before capture.");
        var prior=RenderTexture.active;RenderTexture.active=rt;
        var shot=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);shot.Apply();RenderTexture.active=prior;
        if(SystemInfo.graphicsUVStartsAtTop){var pixels=shot.GetPixels();var flipped=new Color[pixels.Length];for(int y=0;y<rt.height;y++)Array.Copy(pixels,y*rt.width,flipped,(rt.height-1-y)*rt.width,rt.width);shot.SetPixels(flipped);shot.Apply();}
        string path=Path.GetFullPath("Captures/"+name+".png");File.WriteAllBytes(path,ImageConversion.EncodeToPNG(shot));Object.DestroyImmediate(shot);return path;
    }
}
#endif
