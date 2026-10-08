using System;
using System.IO;
using System.Linq;
using ColonyGame.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ColonyGame.Editor
{
    public static class SettlementPrototypeSetup
    {
        private const string ScenePath = "Assets/Scenes/SettlementPrototype.unity";
        private const string MaterialFolder = "Assets/ColonyGame/PrototypeMaterials";

        [MenuItem("ColonyGame/Create Settlement Prototype Scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("SettlementPrototype already exists; it was not overwritten.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("The installed URP Unlit shader could not be found.");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/ColonyGame", "PrototypeMaterials");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = new GameObject("Settlement Prototype");
            var view = world.AddComponent<SettlementPrototypeView>();
            var serialized = new SerializedObject(view);
            Assign(serialized, "claimedGround", new Color(0.46f, 0.62f, 0.39f), shader);
            Assign(serialized, "wildernessGround", new Color(0.16f, 0.30f, 0.23f), shader);
            Assign(serialized, "footprintGround", new Color(0.75f, 0.61f, 0.30f), shader);
            Assign(serialized, "gridBackground", new Color(0.07f, 0.12f, 0.11f), shader);
            Assign(serialized, "treeTrunk", new Color(0.36f, 0.22f, 0.13f), shader);
            Assign(serialized, "treeCanopy", new Color(0.22f, 0.44f, 0.27f), shader);
            Assign(serialized, "hallWalls", new Color(0.88f, 0.81f, 0.65f), shader);
            Assign(serialized, "hallRoof", new Color(0.39f, 0.23f, 0.20f), shader);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("Prototype Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.rotation = Quaternion.Euler(55, 0, 0);
            cameraObject.transform.position = new Vector3(10, 0, 10) - cameraObject.transform.forward * 45;
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 19;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.065f, 0.09f);
            camera.targetDisplay = 0;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var rendering = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            rendering.renderPostProcessing = false;
            rendering.renderShadows = false;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<PrototypeCamera>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            int menu = scenes.FindIndex(s => s.path == "Assets/Scenes/MainMenu.unity");
            scenes.Insert(menu >= 0 ? menu + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("SETTLEMENT_PROTOTYPE_SETUP_PASS: Scene created. Enter Play Mode to inspect the grid.");
        }

        private static void Assign(SerializedObject target, string property, Color color, Shader shader)
        {
            var path = MaterialFolder + "/" + property + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = property };
                material.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(material, path);
            }
            target.FindProperty(property).objectReferenceValue = material;
        }
    }
}
