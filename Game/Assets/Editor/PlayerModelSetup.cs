using System;
using Boki.Player;
using Boki.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Editor
{
    public static class PlayerModelSetup
    {
        private const string Art = "Assets/Art/Player/";
        private const string ScenePath = "Assets/Scenes/Player_Model_Test.unity";
        private const string PrefabPath = "Assets/Prefabs/Player/Boki_Rigged_Test.prefab";

        [MenuItem("Boki/Player Model/Create Test Setup")]
        public static void Create()
        {
            RequireCleanScenes();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("Player model test already exists. Use Boki/Player Model/Open Test Scene.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("Test prefab already exists; refusing to overwrite it.");

            ConfigureModel("CHR_Boki", true);
            ConfigureModel("PROP_Scooter", false);
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Art + "Textures/T_Boki_BaseColor.png");
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.maxTextureSize = 512;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.mipmapEnabled = true;
            textureImporter.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Boki_Atlas.mat");
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
                material = new Material(shader) { name = "Boki_Atlas" };
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/T_Boki_BaseColor.png"));
                material.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(material, Art + "Boki_Atlas.mat");
            }

            AnimationClip ride = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Art + "Export/CHR_Boki.fbx"))
                if (asset is AnimationClip clip && clip.name == "Boki_RideIdle") ride = clip;
            if (ride == null) throw new InvalidOperationException("Boki_RideIdle did not import.");
            var animation = AssetDatabase.LoadAssetAtPath<AnimatorController>(Art + "Boki_Ride.controller");
            if (animation == null)
            {
                animation = AnimatorController.CreateAnimatorControllerAtPath(Art + "Boki_Ride.controller");
                animation.AddMotion(ride);
            }

            var previous = SceneManager.GetActiveScene();
            Scene scene = default;
            try
            {
                // Duplicate the existing movement arena; never save into the original scene or prefab.
                if (!AssetDatabase.CopyAsset("Assets/Scenes/Gameplay_Test.unity", ScenePath))
                    throw new InvalidOperationException("Could not copy the movement arena.");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                ScooterController movement = null;
                ThirdPersonCamera follow = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent<ScooterController>(out var found)) movement = found;
                    if (root.TryGetComponent<ThirdPersonCamera>(out var foundCamera)) follow = foundCamera;
                }
                if (movement == null || follow == null)
                    throw new InvalidOperationException("Movement arena must contain its existing player and follow camera.");

                var player = movement.gameObject;
                foreach (string name in new[] { "Boki placeholder", "Scooter deck", "Forward marker" })
                {
                    var placeholder = player.transform.Find(name);
                    if (placeholder == null) throw new InvalidOperationException("Missing placeholder: " + name);
                    placeholder.gameObject.SetActive(false);
                }
                var visuals = new GameObject("Visuals");
                visuals.layer = player.layer;
                SceneManager.MoveGameObjectToScene(visuals, scene);
                visuals.transform.SetParent(player.transform, false);
                var character = AddModel("CHR_Boki", visuals.transform, material);
                AddModel("PROP_Scooter", visuals.transform, material);
                var animator = character.GetComponent<Animator>();
                if (animator == null) throw new InvalidOperationException("Generic character Animator is missing.");
                animator.runtimeAnimatorController = animation;
                animator.applyRootMotion = false;
                // Show the riding pose in the Scene view as well as in Play Mode.
                ride.SampleAnimation(character, 0f);
                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(player, PrefabPath, InteractionMode.AutomatedAction);
                if (prefab == null) throw new InvalidOperationException("Could not save the rigged test prefab.");
                SetReference(movement, "movementCamera", follow.transform);
                SetReference(follow, "followPivot", player.transform.Find("CameraPivot"));
                ConfigurePresentation(scene, movement, follow.GetComponent<Camera>());
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                var skin = character.GetComponentInChildren<SkinnedMeshRenderer>();
                Debug.Log($"Player model setup ready: {ScenePath}; bones={skin.bones.Length}, "
                    + $"bounds={skin.bounds.size}, avatarValid={animator.avatar != null && animator.avatar.isValid}. "
                    + "Existing movement, input, camera and capsule retained. Root motion disabled.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Boki/Player Model/Open Test Scene")]
        public static void Open()
        {
            RequireCleanScenes();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Create the player model test setup first.");
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Boki/Player Model/Upgrade Test Presentation")]
        public static void UpgradePresentation()
        {
            RequireCleanScenes();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                ScooterController movement = null;
                Camera camera = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent<ScooterController>(out var found)) movement = found;
                    if (root.TryGetComponent<Camera>(out var foundCamera)) camera = foundCamera;
                }
                if (movement == null || camera == null)
                    throw new InvalidOperationException("The test player and camera must exist before upgrading presentation.");
                ConfigurePresentation(scene, movement, camera);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("PLAYER_PRESENTATION_READY: visual lean, slope tilt, wheels, speed FOV and boost HUD wired in the owned test scene.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ConfigurePresentation(Scene scene, ScooterController movement, Camera camera)
        {
            Transform visuals = movement.transform.Find("Visuals");
            if (visuals == null) throw new InvalidOperationException("The player visual hierarchy is missing.");
            Transform front = null, rear = null;
            foreach (var child in visuals.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "M_Wheel_F") front = child;
                if (child.name == "M_Wheel_R") rear = child;
            }
            if (front == null || rear == null) throw new InvalidOperationException("Both scooter wheel pivots must exist.");
            var presentation = movement.GetComponent<ScooterPresentation>();
            if (presentation == null) presentation = movement.gameObject.AddComponent<ScooterPresentation>();
            SetReference(presentation, "movement", movement);
            SetReference(presentation, "visualRoot", visuals);
            SetReference(presentation, "frontWheel", front);
            SetReference(presentation, "rearWheel", rear);
            SetReference(presentation, "speedCamera", camera);

            GameObject hud = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "MovementHUD") hud = root;
            if (hud == null)
            {
                hud = new GameObject("MovementHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(hud, scene);
                hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = hud.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                var background = new GameObject("BoostBackground", typeof(RectTransform), typeof(Image));
                background.transform.SetParent(hud.transform, false);
                SetRect(background.GetComponent<RectTransform>(), new Vector2(24f, 24f), new Vector2(240f, 8f));
                background.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f, 0.85f);
                var fill = new GameObject("BoostProgress", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(hud.transform, false);
                SetRect(fill.GetComponent<RectTransform>(), new Vector2(24f, 24f), new Vector2(240f, 8f));
                var image = fill.GetComponent<Image>();
                image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.color = new Color(0.72f, 0.95f, 0.22f);
                image.raycastTarget = false;
                var label = new GameObject("BoostStatus", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(hud.transform, false);
                SetRect(label.GetComponent<RectTransform>(), new Vector2(24f, 38f), new Vector2(320f, 28f));
                var text = label.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 18;
                text.color = Color.white;
                text.raycastTarget = false;
                text.text = "BOOST READY · LEFT SHIFT";
            }
            var view = hud.GetComponent<BoostView>();
            if (view == null) view = hud.AddComponent<BoostView>();
            SetReference(view, "movement", movement);
            SetReference(view, "statusText", hud.transform.Find("BoostStatus").GetComponent<Text>());
            SetReference(view, "progress", hud.transform.Find("BoostProgress").GetComponent<Image>());
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void ConfigureModel(string name, bool character)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Art + "Export/" + name + ".fbx");
            if (importer == null) throw new InvalidOperationException("Missing model: " + name);
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = character;
            if (character)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.motionNodeName = "root";
                importer.clipAnimations = new[]
                {
                    new ModelImporterClipAnimation
                    {
                        name = "Boki_RideIdle", takeName = importer.defaultClipAnimations[0].takeName,
                        firstFrame = 1f, lastFrame = 30f, loopTime = true,
                        lockRootRotation = true, lockRootHeightY = true, lockRootPositionXZ = true
                    }
                };
            }
            importer.SaveAndReimport();
        }

        [MenuItem("Boki/Player Model/Inspect Test Player")]
        public static void Inspect()
        {
            foreach (string name in new[] { "CHR_Boki", "PROP_Scooter" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Export/" + name + ".fbx");
                Debug.Log($"Imported {name}: rotation={asset.transform.localEulerAngles}, scale={asset.transform.localScale}");
            }
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root.TryGetComponent<ScooterController>(out _)) continue;
                Debug.Log($"Player position={root.transform.position}, scale={root.transform.lossyScale}");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    Debug.Log($"{renderer.name}: bounds={renderer.bounds}, scale={renderer.transform.lossyScale}, "
                        + $"enabled={renderer.enabled}, material={renderer.sharedMaterial.name}", renderer);
                foreach (var child in root.GetComponentsInChildren<Transform>())
                    if (child.name.Contains("Wheel") || child.name == "Steering")
                        Debug.Log($"{child.name}: position={child.position}, forward={child.forward}, scale={child.lossyScale}", child);
            }
        }

        private static GameObject AddModel(string name, Transform parent, Material material)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Export/" + name + ".fbx"), parent);
            model.transform.localPosition = Vector3.zero;
            // Preserve the FBX importer's axis and unit conversion on the model root.
            foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = parent.gameObject.layer;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            return model;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireCleanScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before setting up player assets.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard dirty scenes before setting up player assets.");
        }
    }
}
