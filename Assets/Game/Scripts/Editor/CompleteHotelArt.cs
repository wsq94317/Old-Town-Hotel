#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CompleteHotelArt
{
    [MenuItem("Old Town Hotel/Art/Complete Furniture Style Collection")]
    public static void Build()
    {
        const string folder = "Assets/Game/Resources/HotelArt/Styles";
        Directory.CreateDirectory(folder);
        var catalog = FurnitureVisualCatalogSO.LoadDefault();
        if (catalog == null) return;
        var colors = new[] { new Color(.28f,.55f,.47f), new Color(.67f,.34f,.26f), new Color(.35f,.42f,.62f) };
        var labels = new[] { "Garden", "Heritage", "Midnight" };
        foreach (var entry in catalog.Entries)
        {
            entry.EnsureSlots();
            var source = entry.variants[0].prefab;
            if (source == null) continue;
            for (int v = 1; v < 4; v++)
            {
                // Artist-provided options always win over generated additions.
                if (entry.variants[v].IsConfigured) continue;
                string path = folder + "/Furniture_" + entry.kindId + "_" + labels[v-1] + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var model = Object.Instantiate(source); model.name = "Furniture_" + entry.kindId + "_" + labels[v-1];
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer.name.StartsWith("Status_")) continue;
                        var original = renderer.sharedMaterial; if (original == null) continue;
                        string materialPath = folder + "/" + original.name.Replace("/","_") + "_" + labels[v-1] + ".mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (material == null)
                        {
                            material = new Material(original); material.name = original.name + "_" + labels[v-1];
                            material.color = Color.Lerp(original.color, colors[v-1], .3f); material.enableInstancing = true;
                            AssetDatabase.CreateAsset(material, materialPath);
                        }
                        renderer.sharedMaterial = material;
                    }
                    prefab = PrefabUtility.SaveAsPrefabAsset(model,path); Object.DestroyImmediate(model);
                }
                entry.variants[v].prefab = prefab; entry.variants[v].displayName = labels[v-1];
            }
        }
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Complete furniture style collection is ready.");
    }
}
#endif
