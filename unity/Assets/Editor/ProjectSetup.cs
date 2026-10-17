using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Project settings the port relies on (docs/UNITY_PORT.md): 1280x720 design space as an orthographic camera of size
    /// 3.6, sprites at 2x with 200 pixels per unit, web builds gzipped. Run from the menu or in batch mode:
    ///   Unity -batchmode -projectPath unity -executeMethod Drakepg.EditorTools.ProjectSetup.SetupAndQuit
    ///   Unity -batchmode -projectPath unity -executeMethod Drakepg.EditorTools.ProjectSetup.BuildWebGL
    /// </summary>
    public static class ProjectSetup
    {
        public const string BootScene = "Assets/Scenes/Boot.unity";
        /// <summary>Loaded by GameRoot from Resources: the UI panel (1 UI px = 1 screen px; GameRoot scales the 1280x720 design root itself).</summary>
        public const string PanelSettingsPath = "Assets/Resources/UI/PanelSettings.asset";
        const string ThemePath = "Assets/Resources/UI/Theme.tss";
        public const float CameraSize = 3.6f;      // half of 720 px at 100 px per unit
        public const float PixelsPerUnit2x = 200f; // @2x art: 2560x1440 backgrounds fill the camera exactly
        static readonly Color Night = new Color32(0x0d, 0x0b, 0x12, 0xff);

        [MenuItem("Drakepg/Setup Project")]
        public static void Setup()
        {
            PlayerSettings.companyName = "drakepg";
            PlayerSettings.productName = "drakepg";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            // Blend like the browser canvas did (and drawn shapes keep their vertex colors as written).
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            // Gzip with a fallback plays from any static host (itch.io, GitHub Pages, a local server).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            CreatePanelSettings();
            CreateBootScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("drakepg: project setup done");
        }

        public static void SetupAndQuit()
        {
            Setup();
            EditorApplication.Exit(0);
        }

        /// <summary>Game art: @2x sprites, no mipmaps, high quality.</summary>
        public static void ImportSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit2x;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        /// <summary>The UI Toolkit panel and its theme (the default runtime theme: built-in controls and font).</summary>
        public static void CreatePanelSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PanelSettingsPath));
            if (!File.Exists(ThemePath))
            {
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var create = settings == null;
            if (create) settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1;
            settings.clearColor = false;
            settings.sortingOrder = 0;
            if (create) AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            else EditorUtility.SetDirty(settings);
        }

        static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var camera = cam.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Night;
            cam.transform.position = new Vector3(0, 0, -10);

            // Everything else (data, UI, screens) is made in code by GameRoot.
            new GameObject("GameRoot", typeof(Boot), typeof(GameRoot));
            EditorSceneManager.SaveScene(scene, BootScene);
        }

        [MenuItem("Drakepg/Build WebGL")]
        public static void BuildWebGL()
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "Build/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"drakepg: WebGL build {report.summary.result}, {report.summary.totalSize / 1048576f:F1} MB, {report.summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
