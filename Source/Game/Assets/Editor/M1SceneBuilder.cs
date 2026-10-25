using System;
using System.IO;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    public static class M1SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Gameplay_Test.unity";
        private const string PrefabPath = "Assets/Prefabs/Player/Boki_Player.prefab";
        [MenuItem("Boki/Create M1 Test Arena")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating M1 assets.");
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Save untitled open scenes before creating the M1 arena.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("M1 scene or prefab already exists; refusing to overwrite it.");

            Directory.CreateDirectory("Assets/Prefabs/Player");
            Directory.CreateDirectory("Assets/Input/M1");
            AssetDatabase.Refresh();
            const string inputPath = "Assets/InputSystem_Actions.inputactions";
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
            var sprint = input.FindAction("Player/Sprint", false);
            if (sprint != null)
            {
                sprint.Rename("Boost");
                File.WriteAllText(inputPath, input.ToJson());
                AssetDatabase.ImportAsset(inputPath, ImportAssetOptions.ForceUpdate);
                input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
            }

            var move = Reference(input, "Move");
            var jump = Reference(input, "Jump");
            var boost = Reference(input, "Boost");
            var look = Reference(input, "Look");

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var player = new GameObject("Boki_Player");
                player.layer = 2; // Built-in Ignore Raycast layer; excluded by the camera mask.
                player.transform.position = new Vector3(0f, 0.1f, -8f);
                var controller = player.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = 0.35f;
                controller.center = new Vector3(0f, 0.9f, 0f);
                controller.stepOffset = 0.25f;
                controller.slopeLimit = 45f;
                controller.skinWidth = 0.035f;
                var scooter = player.AddComponent<ScooterController>();
                Primitive("Boki placeholder", new Vector3(0f, 1f, 0f), new Vector3(0.5f, 1f, 0.5f), player.transform, false);
                Primitive("Scooter deck", new Vector3(0f, 0.2f, 0.1f), new Vector3(0.5f, 0.15f, 1.1f), player.transform, false);
                Primitive("Forward marker", new Vector3(0f, 0.65f, 0.5f), new Vector3(0.12f, 0.9f, 0.12f), player.transform, false);
                var pivot = new GameObject("CameraPivot");
                pivot.transform.SetParent(player.transform, false);
                pivot.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                SetReference(scooter, "moveAction", move);
                SetReference(scooter, "jumpAction", jump);
                SetReference(scooter, "boostAction", boost);
                // The scene camera is assigned on the prefab instance below.
                PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
                UnityEngine.Object.DestroyImmediate(player);
                player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);

                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                cameraObject.AddComponent<AudioListener>();
                var follow = cameraObject.AddComponent<ThirdPersonCamera>();
                SetReference(follow, "followPivot", player.transform.Find("CameraPivot"));
                SetReference(follow, "lookAction", look);
                var cameraSettings = new SerializedObject(follow);
                cameraSettings.FindProperty("obstructionMask").intValue = 1; // Default environment layer only.
                cameraSettings.ApplyModifiedPropertiesWithoutUndo();
                SetReference(player.GetComponent<ScooterController>(), "movementCamera", camera.transform);
                cameraObject.transform.position = player.transform.Find("CameraPivot").position
                    - cameraObject.transform.forward * 5f;

                Primitive("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(32f, 1f, 32f));
                Primitive("North wall", new Vector3(0f, 3f, 16f), new Vector3(33f, 6f, 1f));
                Primitive("South wall", new Vector3(0f, 3f, -16f), new Vector3(33f, 6f, 1f));
                Primitive("West wall", new Vector3(-16f, 3f, 0f), new Vector3(1f, 6f, 32f));
                Primitive("East wall", new Vector3(16f, 3f, 0f), new Vector3(1f, 6f, 32f));
                var ramp = Primitive("Ramp", new Vector3(-7f, 0.65f, 2f), new Vector3(4f, 0.5f, 6f));
                ramp.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
                Primitive("Jump obstacle", new Vector3(0f, 0.3f, 1f), new Vector3(3f, 0.6f, 0.6f));
                Primitive("Overhead obstacle", new Vector3(6f, 2.5f, 1f), new Vector3(4f, 0.5f, 4f));
                Primitive("Overhead support", new Vector3(8f, 1.1f, 1f), new Vector3(0.3f, 2.2f, 4f));
                Primitive("Camera wall", new Vector3(5f, 2f, 8f), new Vector3(0.6f, 4f, 7f));
                Primitive("Camera corner", new Vector3(7f, 2f, 11f), new Vector3(4f, 4f, 0.6f));
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("M1 test arena created. Open Assets/Scenes/Gameplay_Test.unity to verify it.");
            }
            finally
            {
                // Preserve the developer's active scene, including any unsaved work.
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static InputActionReference Reference(InputActionAsset input, string name)
        {
            var reference = InputActionReference.Create(input.FindAction("Player/" + name, true));
            AssetDatabase.CreateAsset(reference, "Assets/Input/M1/" + name + ".asset");
            return reference;
        }

        private static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Primitive(string name, Vector3 position, Vector3 scale,
            Transform parent = null, bool collider = true)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            if (!collider)
            {
                UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
                item.layer = 2;
            }
            return item;
        }
    }
}
