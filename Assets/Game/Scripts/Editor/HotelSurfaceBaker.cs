#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class HotelSurfaceBaker
{
    [MenuItem("Old Town Hotel/Art/Polish Surfaces And Interior Lighting")]
    public static void Bake()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop the running game before baking.");
        foreach(string name in new[]{"Walnut","Plaster","Linen"}){
            var importer=AssetImporter.GetAtPath("Assets/Game/Resources/HotelArt/Surfaces/"+name+".png") as TextureImporter;
            importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.anisoLevel=4;importer.maxTextureSize=1024;
            var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=1024;android.format=TextureImporterFormat.ASTC_6x6;importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
        }
        foreach(string path in new[]{"Assets/Settings/Mobile_RPAsset.asset","Assets/Settings/PC_RPAsset.asset"}){
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);if(pipeline==null)continue;
            var settings=new SerializedObject(pipeline);
            settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true;
            settings.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=8;
            settings.ApplyModifiedPropertiesWithoutUndo();pipeline.shadowDistance=35;EditorUtility.SetDirty(pipeline);
        }
        foreach(string id in AssetDatabase.FindAssets("t:UniversalRendererData",new[]{"Assets/Settings"})){
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id));
            var type=typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            var feature=renderer.rendererFeatures.FirstOrDefault(f=>f!=null&&f.GetType()==type);
            if(feature==null){feature=(ScriptableRendererFeature)ScriptableObject.CreateInstance(type);feature.name="Hotel Contact Shadows";AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);}
            var so=new SerializedObject(feature);var ss=so.FindProperty("m_Settings");
            ss.FindPropertyRelative("Intensity").floatValue=.9f;ss.FindPropertyRelative("Radius").floatValue=.32f;ss.FindPropertyRelative("Downsample").boolValue=true;ss.FindPropertyRelative("DirectLightingStrength").floatValue=.3f;
            ss.FindPropertyRelative("Samples").enumValueIndex=1;ss.FindPropertyRelative("BlurQuality").enumValueIndex=1;
            so.ApplyModifiedPropertiesWithoutUndo();feature.SetActive(true);EditorUtility.SetDirty(feature);renderer.SetDirty();EditorUtility.SetDirty(renderer);
        }
        // Rebuild only generated art roots. Gameplay objects, authoring anchors and room data stay in place.
        foreach(var binder in Object.FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Include,FindObjectsSortMode.None))foreach(string name in new[]{"RevivalArchitecture","RevivalRuins","RevivalFurniturePreview"}){
            var child=binder.transform.Find(name);if(child!=null)Object.DestroyImmediate(child.gameObject);
        }
        var lobby=GameObject.Find("World/Floor1/RevivalArchitecture");if(lobby!=null)Object.DestroyImmediate(lobby);
        var look=Object.FindFirstObjectByType<HotelLook>();
        if(look!=null){var settings=new SerializedObject(look);settings.FindProperty("contrast").floatValue=8;settings.FindProperty("saturation").floatValue=2;settings.FindProperty("bloomIntensity").floatValue=.23f;settings.FindProperty("vignetteIntensity").floatValue=.12f;settings.ApplyModifiedPropertiesWithoutUndo();}
        HotelVisualReview.Bake();
        var legacyLobby=GameObject.Find("World/Floor1/ArtPass_LowPoly");
        if(legacyLobby!=null)foreach(var t in legacyLobby.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("LobbySofa"))foreach(var oldRenderer in t.GetComponentsInChildren<Renderer>(true))oldRenderer.enabled=false;
        foreach(string materialId in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Game/Resources/HotelArt/Prefabs","Assets/Game/Resources/HotelArt/Styles"})) UpgradeMaterial(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(materialId)));
        foreach(string materialId in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Game/Resources/HotelArt"})) {
            string path=AssetDatabase.GUIDToAssetPath(materialId);
            if(path.Contains("/Surfaces/")||path.Contains("/Revival/"))continue;
            UpgradeMaterial(AssetDatabase.LoadAssetAtPath<Material>(path));
        }
        // Upgrade legacy public-floor props through the same surface rules.
        var world=GameObject.Find("World");
        foreach(var r in world.GetComponentsInChildren<MeshRenderer>(true)){
            if(r.GetComponent<TextMesh>()!=null||r.name=="Darkness"||r.name=="StateChip"||r.name=="DoorPanel")continue;
            var old=r.sharedMaterial;if(old==null||old.shader.name=="OldTownHotel/Crafted Surface"||old.mainTexture!=null)continue;
            var material=HotelSurfaceLibrary.For(r.name+"_"+old.name,old.color);Persist(material);r.sharedMaterial=material;
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
    }
    private static void UpgradeMaterial(Material old)
    {
        if(old==null||old.shader.name!="Universal Render Pipeline/Lit")return;
        var material=HotelSurfaceLibrary.For(old.name,old.color);old.shader=material.shader;old.CopyPropertiesFromMaterial(material);EditorUtility.SetDirty(old);
    }
    private static void Persist(Material material)
    {
        if(!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))return;
        AssetDatabase.CreateAsset(material,"Assets/Game/Resources/HotelArt/Surfaces/"+material.name+".mat");
    }
}
#endif
