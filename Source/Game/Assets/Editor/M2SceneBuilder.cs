using System;
using System.IO;
using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    public static class M2SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Combat_Test.unity";
        private const string EnemyPath = "Assets/Prefabs/Enemies/Melee_Test.prefab";

        [MenuItem("Boki/Create M2 Combat Arena")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating M2 assets.");
            if (File.Exists(ScenePath) || File.Exists(EnemyPath))
                throw new InvalidOperationException("M2 scene or enemy prefab already exists; refusing to overwrite it.");
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Save untitled scenes before creating M2 assets.");
            Directory.CreateDirectory("Assets/Prefabs/Enemies");
            Directory.CreateDirectory("Assets/Art/M2");
            AssetDatabase.Refresh();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Boki_Player.prefab");
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                player.transform.position = new Vector3(0f, 0.1f, -6f);
                var health = player.AddComponent<PlayerHealth>();
                var origin = new GameObject("LaserOrigin").transform;
                origin.SetParent(player.transform, false);
                origin.localPosition = new Vector3(0f, 1f, 0.6f);
                var beam = new GameObject("Laser Beam").AddComponent<LineRenderer>();
                beam.transform.SetParent(player.transform, false);
                beam.startWidth = beam.endWidth = 0.06f;
                beam.positionCount = 2;
                beam.useWorldSpace = true;
                beam.sharedMaterial = Material("Laser", new Color(0.1f, 1f, 0.8f), true);
                beam.enabled = false;
                var laser = player.AddComponent<AutoLaser>();
                Ref(laser, "playerHealth", health);
                Ref(laser, "beamOrigin", origin);
                Ref(laser, "beam", beam);

                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                cameraObject.AddComponent<AudioListener>();
                var follow = cameraObject.AddComponent<ThirdPersonCamera>();
                var pivot = player.transform.Find("CameraPivot");
                Ref(follow, "followPivot", pivot);
                Ref(follow, "lookAction", AssetDatabase.LoadAssetAtPath<InputActionReference>("Assets/Input/M1/Look.asset"));
                Int(follow, "obstructionMask", 1);
                Ref(player.GetComponent<ScooterController>(), "movementCamera", camera.transform);
                camera.transform.position = pivot.position - camera.transform.forward * 5f;

                var floorMaterial = Material("Floor", new Color(0.16f, 0.19f, 0.23f));
                var wallMaterial = Material("Wall", new Color(0.32f, 0.36f, 0.42f));
                Cube("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(32f, 1f, 32f), floorMaterial);
                Cube("North wall", new Vector3(0f, 2f, 16f), new Vector3(33f, 4f, 1f), wallMaterial);
                Cube("South wall", new Vector3(0f, 2f, -16f), new Vector3(33f, 4f, 1f), wallMaterial);
                Cube("West wall", new Vector3(-16f, 2f, 0f), new Vector3(1f, 4f, 32f), wallMaterial);
                Cube("East wall", new Vector3(16f, 2f, 0f), new Vector3(1f, 4f, 32f), wallMaterial);
                Cube("Occlusion wall", new Vector3(5f, 1.5f, 2f), new Vector3(4f, 3f, 0.5f), wallMaterial);
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

                var enemy = new GameObject("Melee_Test");
                enemy.layer = 2; // Explicit registry targeting; excluded from environment rays and camera casts.
                var controller = enemy.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = 0.35f;
                controller.center = Vector3.up * 0.9f;
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Shadow placeholder";
                visual.layer = 2;
                visual.transform.SetParent(enemy.transform, false);
                visual.transform.localPosition = Vector3.up * 0.9f;
                visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial = Material("Melee", new Color(0.65f, 0.13f, 0.23f));
                var aim = new GameObject("AimPoint").transform;
                aim.SetParent(enemy.transform, false);
                aim.localPosition = Vector3.up;
                var enemyHealth = enemy.AddComponent<EnemyHealth>();
                Ref(enemyHealth, "aimPoint", aim);
                enemy.AddComponent<MeleeEnemy>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(enemy, EnemyPath);
                Object.DestroyImmediate(enemy);
                var enemies = new MeleeEnemy[4];
                var positions = new[] { new Vector3(0f, 0.1f, 1f), new Vector3(-6f, 0.1f, 4f),
                    new Vector3(5f, 0.1f, 5f), new Vector3(9f, 0.1f, 8f) };
                for (int i = 0; i < enemies.Length; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.name = "Melee " + (i + 1);
                    instance.transform.position = positions[i];
                    enemies[i] = instance.GetComponent<MeleeEnemy>();
                    Ref(enemies[i], "target", health);
                }
                var view = CreateUI();
                var session = new GameObject("Combat Session").AddComponent<CombatSession>();
                Ref(session, "playerHealth", health);
                Ref(session, "movement", player.GetComponent<ScooterController>());
                Ref(session, "cameraController", follow);
                Ref(session, "weapon", laser);
                Ref(session, "view", view);
                var serialized = new SerializedObject(session);
                var array = serialized.FindProperty("enemies");
                array.arraySize = enemies.Length;
                for (int i = 0; i < enemies.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("M2 combat arena created; source M1 assets were not changed.");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static CombatView CreateUI()
        {
            var root = new GameObject("Combat UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            var hp = Label("HP", root.transform, "HP 100 / 100", 28, new Vector2(-470f, 300f), new Vector2(260f, 50f));
            Label("Controls", root.transform, "WASD move  |  Space jump  |  Shift boost  |  Laser automatic", 19,
                new Vector2(0f, -320f), new Vector2(1000f, 40f));
            var panel = new GameObject("Death Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.93f);
            Label("Death Title", panel.transform, "ABSENT", 64, new Vector2(0f, 70f), new Vector2(500f, 100f));
            var buttonObject = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false);
            buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(230f, 65f);
            buttonObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -50f);
            buttonObject.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.4f);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            Label("Retry Label", buttonObject.transform, "Retry", 30, Vector2.zero, new Vector2(230f, 65f));
            panel.SetActive(false);
            var view = root.AddComponent<CombatView>();
            Ref(view, "healthText", hp);
            Ref(view, "deathPanel", panel);
            Ref(view, "retryButton", button);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return view;
        }

        private static Text Label(string name, Transform parent, string content, int fontSize, Vector2 position, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            var material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, "Assets/Art/M2/" + name + ".mat");
            return material;
        }

        private static void Cube(string name, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Ref(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Int(Object target, string name, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
