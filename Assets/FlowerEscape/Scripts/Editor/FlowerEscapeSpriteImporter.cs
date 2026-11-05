using UnityEditor;
using UnityEngine;

namespace FlowerEscape.EditorTools
{
    /// <summary>Import settings for the sprites exported from Figma (exported at 3x the design size).</summary>
    public class FlowerEscapeSpriteImporter : AssetPostprocessor
    {
        public const string SpriteFolder = "Assets/FlowerEscape/Sprites/";
        public const float ExportScale = 3f;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteFolder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.spritePixelsPerUnit = 100f * ExportScale;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);
            // 9-slice borders are assigned by FlowerEscapeSceneBuilder.EnsureBorders (kept across reimports).
        }

        /// <summary>Sets a 9-slice border (texture pixels: left, bottom, right, top) on a sprite and reimports it.</summary>
        public static void SetBorder(string key, Vector4 border)
        {
            string path = SpriteFolder + key + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null || ti.spriteBorder == border) return;
            ti.spriteBorder = border;
            ti.SaveAndReimport();
        }
    }
}
