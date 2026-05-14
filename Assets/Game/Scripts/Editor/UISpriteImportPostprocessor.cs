using UnityEditor;
using UnityEngine;

internal sealed class UISpriteImportPostprocessor : AssetPostprocessor
{
    private const string UiSpriteRoot = "Assets/Game/UI/Sprites/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(UiSpriteRoot)) return;

        var importer = (TextureImporter)assetImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMode == (int)SpriteImportMode.None)
        {
            settings.spriteMode = (int)SpriteImportMode.Single;
        }
        settings.spriteMeshType = SpriteMeshType.Tight;
        importer.SetTextureSettings(settings);

        var platformSettings = importer.GetDefaultPlatformTextureSettings();
        platformSettings.textureCompression = TextureImporterCompression.CompressedHQ;
        platformSettings.crunchedCompression = false;
        importer.SetPlatformTextureSettings(platformSettings);
    }
}
