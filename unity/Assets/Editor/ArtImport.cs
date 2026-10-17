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
    /// </summary>
    public class ArtImport : AssetPostprocessor
    {
        /// <summary>Bumped when the rules change, so Unity re-imports the art.</summary>
        public override uint GetVersion() => 2;

        const string Folder = "Assets/Resources/Art/";
        static Dictionary<string, int> resolution;

        static int ResOf(string key)
        {
            if (resolution == null)
            {
                resolution = new Dictionary<string, int>();
                var path = Path.Combine(Application.dataPath, "Resources/Art/index.json");
                if (File.Exists(path))
                    foreach (var kv in JObject.Parse(File.ReadAllText(path))) resolution[kv.Key] = (int)kv.Value["res"];
            }
            return resolution.TryGetValue(key, out var r) ? r : 2;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            resolution = null;
            var key = Path.GetFileNameWithoutExtension(assetPath);
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var background = key.StartsWith("bg_");
            importer.mipmapEnabled = !background;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.filterMode = background ? FilterMode.Bilinear : FilterMode.Trilinear;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.spritePixelsPerUnit = 100 * ResOf(key);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            var feet = key.StartsWith("mon_") || key.StartsWith("npc_") || key.StartsWith("char_");
            settings.spriteAlignment = (int)(feet ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            importer.SetTextureSettings(settings);
        }
    }
}
