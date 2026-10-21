#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// The game does not use inference. Editor assistant packages otherwise ship
// hundreds of unused tensor shader permutations into every player build.
public sealed class HotelPlayerBuild : IPreprocessShaders, IPreprocessComputeShaders
{
    public int callbackOrder => 0;
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> variants)
    { if (shader.name.StartsWith("Hidden/Sentis",StringComparison.Ordinal)) variants.Clear(); }
    public void OnProcessComputeShader(ComputeShader shader, string kernel, IList<ShaderCompilerData> variants)
    { if (AssetDatabase.GetAssetPath(shader).StartsWith("Packages/com.unity.ai.inference",StringComparison.Ordinal)) variants.Clear(); }

    [MenuItem("Old Town Hotel/Build/Prepare Portable Chinese Font")]
    public static void BakeFont()
    {
        const string path = "Assets/Game/Resources/HotelFonts/HotelCjk.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Resources/HotelFonts/NotoSansSC.ttf");
        if (source == null) throw new InvalidOperationException("Bundled Noto Sans SC font not imported.");
        var asset = TMP_FontAsset.CreateFontAsset(source,48,6,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        asset.name = "Hotel CJK";
        string words = new string(Enumerable.Range(32,95).Select(i=>(char)i).ToArray());
        foreach (string file in Directory.GetFiles("Assets/Game/Scripts","*.cs",SearchOption.AllDirectories))
            words += File.ReadAllText(file);
        string characters = new string(words.Where(c=>c>=' ' && c<='~' || c>='\u3000' && c<='\u303f'
            || c>='\u4e00' && c<='\u9fff' || "·×—…★→↑↓%￥".Contains(c)).Distinct().ToArray());
        asset.TryAddCharacters(characters,out string missing);
        AssetDatabase.CreateAsset(asset,path);
        foreach (var atlas in asset.atlasTextures) if (atlas != null) AssetDatabase.AddObjectToAsset(atlas,asset);
        AssetDatabase.AddObjectToAsset(asset.material,asset);
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
        Debug.Log("Portable CJK font baked: " + characters.Length + " characters; missing " + missing.Length + ".");
    }
}
#endif
