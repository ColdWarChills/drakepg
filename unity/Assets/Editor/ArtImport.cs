using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Import rules for the game art that scripts/unity_art.py copies into Assets/Resources/Art: sprites at 100 pixels
    /// per unit per resolution step (a @2x file is 200), so every key draws at its 1x design size; characters pivot
    /// at the feet, everything else at the center. Everything but backgrounds gets mipmaps: characters and weapons are
    /// drawn 2-4x smaller than their art, and without mipmaps thin lines (a sword's edges) alias and crawl when the
    /// figure breathes or swings.
    /// Size: the web build only compresses a mipmapped texture when its sides are powers of two, so unity_art.py puts
    /// each sprite in the top-left corner of such a canvas and this cuts it back out at its own size (index.json w, h).
    /// Everything is crunch-compressed (DXT, decoded when it loads); item icons and weapons are capped at the size
    /// they are drawn at on a 1440p screen.
    /// </summary>
    public class ArtImport : AssetPostprocessor
    {
        /// <summary>Bumped when the rules change, so Unity re-imports the art.</summary>
        public override uint GetVersion() => 3;

        const string Folder = "Assets/Resources/Art/";
        /// <summary>Crunch quality (0-100): higher keeps flat colors and soft skies cleaner, at a bigger download.</summary>
        const int Quality = 80;
        static Dictionary<string, JToken> index;

        static JToken Entry(string key)
        {
            if (index == null)
            {
                index = new Dictionary<string, JToken>();
                var path = Path.Combine(Application.dataPath, "Resources/Art/index.json");
                if (File.Exists(path))
                    foreach (var kv in JObject.Parse(File.ReadAllText(path))) index[kv.Key] = kv.Value;
            }
            return index.TryGetValue(key, out var e) ? e : null;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            index = null;
            var key = Path.GetFileNameWithoutExtension(assetPath);
            var entry = Entry(key);
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            var background = key.StartsWith("bg_");
            importer.mipmapEnabled = !background;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.filterMode = background ? FilterMode.Bilinear : FilterMode.Trilinear;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = key.StartsWith("icon_") ? 256 : key.StartsWith("wpn_") ? 512 : 4096;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = Quality;
            importer.ClearPlatformTextureSettings("WebGL");
            importer.ClearPlatformTextureSettings("Standalone");
            importer.spritePixelsPerUnit = 100 * (entry != null ? (int)entry["res"] : 2);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            var feet = key.StartsWith("mon_") || key.StartsWith("npc_") || key.StartsWith("char_");
            var alignment = (int)(feet ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            settings.spriteAlignment = alignment;
            importer.SetTextureSettings(settings);

            // A sprite padded to a power-of-two canvas: one sprite, the picture's own rect in the top-left corner.
            importer.GetSourceTextureWidthAndHeight(out var texW, out var texH);
            if (background || entry == null || ((int)entry["w"] == texW && (int)entry["h"] == texH))
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                return;
            }
            float w = (int)entry["w"], h = (int)entry["h"];
            importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 618 // spritesheet: the replacement (ISpriteEditorDataProvider) needs the 2D Sprite package
            importer.spritesheet = new[]
            {
                new SpriteMetaData { name = key, rect = new Rect(0, texH - h, w, h), alignment = alignment, pivot = feet ? new Vector2(0.5f, 0) : new Vector2(0.5f, 0.5f) },
            };
#pragma warning restore 618
        }
    }
}
