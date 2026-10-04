using System;
using System.IO;
using System.Linq;
using ColonyGame.Presentation;
using ColonyGame.Profiles;
using ColonyGame.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering.Universal;

namespace ColonyGame.Editor
{
    public static class ShellSetup
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        [MenuItem("ColonyGame/Create Main Menu Scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Main Menu scene already exists; it was not overwritten.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string panelPath = "Assets/ColonyGame/MenuPanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 720);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0.5f;
                AssetDatabase.CreateAsset(panel, panelPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/ColonyGame/MenuTheme.tss");
            EditorUtility.SetDirty(panel);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var shell = new GameObject("Menu Shell");
            shell.AddComponent<UIDocument>().panelSettings = panel;
            shell.AddComponent<LobbyAudio>();
            shell.AddComponent<MenuShell>();
            shell.AddComponent<AudioListener>();
            CreateMenuCamera();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToArray();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(previous).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("COLONYGAME_SETUP_PASS: MainMenu scene created and placed first in build settings.");
        }

        // A clear-only camera renders Display 1 behind the screen-space UI.
        private static Camera CreateMenuCamera()
        {
            var cameraObject = new GameObject("Menu Background Camera");
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.065f, 0.09f, 1);
            camera.cullingMask = 0;
            camera.targetDisplay = 0;
            camera.depth = -100;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            var rendering = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            rendering.renderShadows = false;
            rendering.renderPostProcessing = false;
            rendering.requiresColorTexture = false;
            rendering.requiresDepthTexture = false;
            return camera;
        }

        [MenuItem("ColonyGame/Validation/Validate Profile Storage")]
        public static void ValidateProfiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "ColonyGameTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new ProfileStore(root);
                var first = store.Create("First profile");
                var second = store.Create("Second profile");
                Assert(first.id != second.id, "Unique IDs");
                Assert(first.saveFormatVersion == 1 && !string.IsNullOrEmpty(first.createdUtc), "Version and creation timestamp");
                Assert(new ProfileStore(root).Load(out _).Count == 2, "Profiles persist across store instances");
                store.MarkPlayed(first);
                Assert(new ProfileStore(root).Load(out _).Single(p => p.id == first.id).lastPlayedUtc == first.lastPlayedUtc, "Last played timestamp persists");
                store.Delete(first);
                Assert(store.Load(out _).Single().id == second.id, "Deletion preserves other profiles");
                File.WriteAllText(Path.Combine(root, second.id, "profile.json"), "invalid JSON");
                Assert(store.Load(out var skipped).Count == 0 && skipped == 1, "Corrupt metadata skipped without deletion");
                Assert(File.Exists(Path.Combine(root, second.id, "profile.json")), "Corrupt data preserved");
                bool rejected = false;
                try { store.Create("  "); } catch (ArgumentException) { rejected = true; }
                Assert(rejected, "Blank name rejected");
                Debug.Log("COLONYGAME_PROFILE_TESTS_PASS");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        [MenuItem("ColonyGame/Validation/Validate Audio Settings")]
        public static void ValidateSettings()
        {
            var prefix = "ColonyGame.Tests." + Guid.NewGuid().ToString("N") + ".";
            var audioObject = new GameObject("Temporary Audio Test");
            try
            {
                var preferences = new AudioPreferences(prefix);
                preferences.SetMusicVolume(0.63f);
                preferences.SetLobbyMusic(false);
                var reloaded = new AudioPreferences(prefix);
                Assert(!reloaded.LobbyMusicEnabled && Mathf.Approximately(reloaded.MusicVolume, 0.63f), "Settings persist independently");
                var audio = audioObject.AddComponent<LobbyAudio>();
                audio.Initialize(reloaded);
                var source = audioObject.GetComponent<AudioSource>();
                Assert(source.mute && Mathf.Approximately(source.volume, 0.63f), "Music off retains volume");
                reloaded.SetLobbyMusic(true);
                audio.Apply(reloaded);
                Assert(!source.mute && Mathf.Approximately(source.volume, 0.63f), "Music on restores selected volume");
                Assert(source.clip == null && !source.isPlaying, "Missing clip is safe");
                Debug.Log("COLONYGAME_SETTINGS_TESTS_PASS");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioObject);
                PlayerPrefs.DeleteKey(prefix + "LobbyMusicEnabled");
                PlayerPrefs.DeleteKey(prefix + "MusicVolume");
                PlayerPrefs.Save();
            }
        }
        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("Profile test failed: " + name);
        }
        public static void SetupAndValidate()
        {
            Create();
            ValidateProfiles();
            ValidateSettings();
        }
    }
}



