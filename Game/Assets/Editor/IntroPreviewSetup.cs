using System;
using System.IO;
using System.Linq;
using Boki.Battery;
using Boki.Intro;
using Boki.Player;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // The intro is a self-contained prefab (stage, overlay, audio, IntroCutscene). Intro_Preview is its own sound stage;
    // no map asset is used. A map adopts the prefab later through "Add Intro To Open Scene".
    public static class IntroPreviewSetup
    {
        private const string Folder = "Assets/Cutscenes/Intro";
        private const string ScenePath = "Assets/Scenes/Intro_Preview.unity";
        private const string IntroPrefab = Folder + "/IntroCutscene.prefab";
        private const string PlayerPrefab = "Assets/Prefabs/Player/Boki_Rigged_Test.prefab";
        private const string AtlasPath = "Assets/Art/Player/Boki_Atlas.mat";
        private const string BeepPath = "Assets/Audio/Sfx/intro_phone_beep.wav";
        private const string CutsceneName = "Intro Cutscene";
        // The player's start sits here in stage space: where the fallen scooter lies, so Boki mounts it.
        private static readonly Vector3 RideSpot = new Vector3(-1.15f, 0.05f, 0.3f);
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.86f);
        private static readonly Color Lcd = new Color(0.62f, 0.72f, 0.3f);

        [MenuItem("Boki/Intro/Create Intro Preview")]
        public static void Create()
        {
            RequireIdle();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("Intro preview already exists; open " + ScenePath + ". Existing edits were preserved.");
                return;
            }
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureAssets();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsurePrefab();
                ConfigureDawn();
                var sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(0.95f, 0.72f, 0.62f);
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(14f, 205f, 0f);
                BuildSet();

                var player = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PlayerPrefab));
                player.transform.SetPositionAndRotation(new Vector3(0f, RideSpot.y, 0f), Quaternion.identity);
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(ThirdPersonCamera));
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 400f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = RenderSettings.fogColor;
                var follow = cameraObject.GetComponent<ThirdPersonCamera>();
                Set(follow, "followPivot", player.transform.Find("CameraPivot"));
                Set(follow, "lookAction", Load<InputActionReference>("Assets/Input/M1/Look.asset"));
                var settings = new SerializedObject(follow);
                settings.FindProperty("obstructionMask").intValue = 1;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var movement = player.GetComponent<ScooterController>();
                Set(movement, "movementCamera", camera.transform);

                Adopt(movement, follow, true);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("INTRO_PREVIEW_CREATED: open " + ScenePath + ". No map, gameplay scene or build setting used or changed.");
            }
            finally
            {
                RestoreScenes(previous);
            }
        }

        [MenuItem("Boki/Intro/Open Intro Preview")]
        public static void Open()
        {
            RequireIdle();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Create the intro preview first.");
            EditorSceneManager.OpenScene(ScenePath);
        }

        // For the integration step: the open scene must already hold the player and its follow camera.
        // The scene is left modified but unsaved so its owner can review the change.
        [MenuItem("Boki/Intro/Add Intro To Open Scene")]
        public static void AddToOpenScene()
        {
            RequireIdle();
            var movement = Object.FindFirstObjectByType<ScooterController>();
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (movement == null || follow == null)
                throw new InvalidOperationException("Open a scene that already has the player (ScooterController) and its ThirdPersonCamera.");
            if (Object.FindFirstObjectByType<IntroCutscene>() != null)
            {
                Debug.Log("This scene already has an intro cutscene; nothing changed.");
                return;
            }
            Adopt(movement, follow, false);
            EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
            Debug.Log("INTRO_ADDED_TO_OPEN_SCENE: review, save, then call IntroCutscene.Play() from the New Run flow. "
                + "The stage sits at the player's start; move 'Intro Cutscene/Intro Stage' if the ground there is uneven.");
        }

        // Renders review frames of every shot into output/intro-preview/frames; the scene is not saved.
        [MenuItem("Boki/Intro/Capture Preview Frames")]
        public static void CaptureFrames()
        {
            RequireIdle();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var cutscene = Object.FindFirstObjectByType<IntroCutscene>();
                var camera = Object.FindFirstObjectByType<ThirdPersonCamera>().GetComponent<Camera>();
                var canvas = Object.FindFirstObjectByType<IntroOverlay>().GetComponent<Canvas>();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/intro-preview/frames"));
                Directory.CreateDirectory(folder);
                // Edit Mode leaves the player's serialized lights on; at runtime LightBattery keeps them off until the mount shot.
                foreach (Light light in Object.FindFirstObjectByType<ScooterController>().GetComponentsInChildren<Light>())
                    light.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.01f;
                var skins = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
                var texture = new RenderTexture(1280, 720, 24);
                camera.targetTexture = texture;
                var frames = new (int shot, float progress)[]
                {
                    (0, 0.05f), (0, 0.5f), (0, 1f), (1, 0.5f), (1, 1f), (2, 0.6f),
                    (3, 0.5f), (4, 0.2f), (4, 0.95f), (5, 0.1f), (5, 0.6f), (5, 1f),
                };
                cutscene.ShowFrame(frames[0].shot, frames[0].progress);
                camera.Render(); // warm-up: the first render after load lacks textures and lights
                foreach (var (shot, progress) in frames)
                {
                    cutscene.ShowFrame(shot, progress);
                    // Edit Mode does not re-skin after bone changes unless the renderer is re-registered.
                    foreach (var skin in skins) { skin.enabled = false; skin.enabled = true; }
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = texture;
                    var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    File.WriteAllBytes(Path.Combine(folder, $"shot{shot}-{Mathf.RoundToInt(progress * 100):000}.png"), image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                RenderTexture.active = null;
                camera.targetTexture = null;
                texture.Release();
                Debug.Log("INTRO_FRAMES_CAPTURED: " + folder);
            }
            finally
            {
                RestoreScenes(previous);
            }
        }

        // Puts the prefab in the open scene at the player's start and assigns the scene-side references.
        private static void Adopt(ScooterController movement, ThirdPersonCamera follow, bool playOnStart)
        {
            var headlight = movement.GetComponent<LightBattery>();
            if (headlight == null) throw new InvalidOperationException("The player needs its LightBattery headlight.");
            var visuals = movement.transform.Find("Visuals");
            var pivot = movement.transform.Find("CameraPivot");
            if (visuals == null || pivot == null) throw new InvalidOperationException("The player needs its Visuals and CameraPivot children.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(IntroPrefab));
            SceneManager.MoveGameObjectToScene(instance, movement.gameObject.scene);
            var yaw =Quaternion.Euler(0f, movement.transform.eulerAngles.y, 0f);
            instance.transform.SetPositionAndRotation(movement.transform.position - yaw * RideSpot, yaw);
            var so = new SerializedObject(instance.GetComponent<IntroCutscene>());
            so.FindProperty("shotCamera").objectReferenceValue = follow.GetComponent<Camera>();
            so.FindProperty("rider").objectReferenceValue = visuals.gameObject;
            so.FindProperty("riderRoot").objectReferenceValue = movement.transform;
            so.FindProperty("followPivot").objectReferenceValue = pivot;
            so.FindProperty("headlight").objectReferenceValue = headlight;
            var controls = so.FindProperty("controls");
            controls.arraySize = 2;
            controls.GetArrayElementAtIndex(0).objectReferenceValue = movement;
            controls.GetArrayElementAtIndex(1).objectReferenceValue = follow;
            so.FindProperty("playOnStart").boolValue = playOnStart;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The prefab holds everything that belongs to the intro. Scene-side references (camera, player) stay empty.
        private static void EnsurePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(IntroPrefab) != null) return;
            var atlas = Load<Material>(AtlasPath);
            var root = new GameObject(CutsceneName, typeof(AudioSource), typeof(IntroCutscene));
            var source = root.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            var stage = new GameObject("Intro Stage").transform;
            stage.SetParent(root.transform, false);
            var wake = new GameObject("Wake Actors").transform;
            wake.SetParent(stage, false);

            var lying = new GameObject("Boki Lying").transform;
            lying.SetParent(wake, false);
            lying.localPosition = new Vector3(0f, 0.14f, 0.86f);
            lying.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            GameObject boki = AddModel("CHR_Boki", lying, atlas);
            var animator = boki.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            var scooter = new GameObject("Fallen Scooter").transform;
            scooter.SetParent(wake, false);
            scooter.localPosition = new Vector3(RideSpot.x, 0.12f, 0.2f);
            scooter.localRotation = Quaternion.Euler(0f, 20f, 90f);
            AddModel("PROP_Scooter", scooter, atlas);

            var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottle.name = "Plastic Bottle";
            Object.DestroyImmediate(bottle.GetComponent<Collider>());
            bottle.transform.SetParent(wake, false);
            bottle.transform.localPosition = new Vector3(0.55f, 0.07f, -1.25f);
            bottle.transform.localRotation = Quaternion.Euler(0f, 25f, 90f);
            bottle.transform.localScale = new Vector3(0.07f, 0.11f, 0.07f);
            bottle.GetComponent<Renderer>().sharedMaterial = Load<Material>(Folder + "/Bottle.mat");

            var light = new GameObject("Stage Light").AddComponent<Light>();
            light.transform.SetParent(wake, false);
            light.transform.localPosition = new Vector3(0.6f, 2.4f, -0.4f);
            light.type = LightType.Point;
            light.color = new Color(0.65f, 0.72f, 0.9f);
            light.intensity = 6f;
            light.range = 12f;
            light.shadows = LightShadows.None;

            IntroOverlay overlay = BuildOverlay(root.transform);
            var input = AssetDatabase.LoadAllAssetsAtPath(Folder + "/IntroInput.asset").OfType<InputActionReference>().ToArray();
            var so = new SerializedObject(root.GetComponent<IntroCutscene>());
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("overlay").objectReferenceValue = overlay;
            so.FindProperty("wakeActors").objectReferenceValue = wake.gameObject;
            so.FindProperty("head").objectReferenceValue = Bone(boki, "head");
            so.FindProperty("leftUpperArm").objectReferenceValue = Bone(boki, "upper_arm.L");
            so.FindProperty("leftLowerArm").objectReferenceValue = Bone(boki, "lower_arm.L");
            so.FindProperty("leftHand").objectReferenceValue = Bone(boki, "hand.L");
            so.FindProperty("audioSource").objectReferenceValue = source;
            so.FindProperty("phoneBeep").objectReferenceValue = Load<AudioClip>(BeepPath);
            so.FindProperty("skipAction").objectReferenceValue = input.Single(r => r.name == "Skip");
            so.FindProperty("replayAction").objectReferenceValue = input.Single(r => r.name == "Replay");
            so.FindProperty("playOnStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.SaveAsPrefabAsset(root, IntroPrefab) == null)
                throw new InvalidOperationException("Could not save " + IntroPrefab + ".");
            Object.DestroyImmediate(root);
        }

        // A neutral sound stage: flat ground, a skyline toward +Z (the city Boki cannot reach yet) and a few dead trees.
        private static void BuildSet()
        {
            var set = new GameObject("Sound Stage").transform;
            Primitive(set, PrimitiveType.Cube, "Ground", new Vector3(0f, -0.1f, 0f), new Vector3(200f, 0.2f, 200f), "Ground.mat");
            var random = new System.Random(1998);
            for (int i = 0; i < 28; i++)
            {
                float width = 6f + random.Next(0, 9);
                float height = 8f + random.Next(0, 28);
                float x = Mathf.Lerp(-95f, 95f, i / 27f) + random.Next(-3, 4);
                Primitive(set, PrimitiveType.Cube, "Skyline " + i, new Vector3(x, height / 2f, 70f + random.Next(0, 40)),
                    new Vector3(width, height, width * 0.8f), "Silhouette.mat");
            }
            for (int i = 0; i < 36; i++)
            {
                float angle = i * 10f * Mathf.Deg2Rad;
                float radius = 22f + random.Next(0, 25);
                // Keep the view down the +Z corridor clear of trunks.
                if (Mathf.Abs(Mathf.Sin(angle)) < 0.55f && Mathf.Cos(angle) > 0f) continue;
                float trunk = 3f + random.Next(0, 3);
                Primitive(set, PrimitiveType.Cylinder, "Dead Tree " + i,
                    new Vector3(Mathf.Sin(angle) * radius, trunk, Mathf.Cos(angle) * radius), new Vector3(0.5f, trunk, 0.5f), "Silhouette.mat");
            }
        }

        private static void Primitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, string material)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            var renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = Load<Material>(Folder + "/" + material);
            // Backdrop only: long skyline shadows would streak across the stage.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static GameObject AddModel(string name, Transform parent, Material material)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Art/Player/Export/" + name + ".fbx"), parent);
            model.transform.localPosition = Vector3.zero;
            // The imported axis and unit conversion on the model root stays untouched.
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            return model;
        }

        private static Transform Bone(GameObject model, string name)
        {
            var bone = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (bone == null) throw new InvalidOperationException("CHR_Boki has no bone named " + name + ".");
            return bone;
        }

        private static IntroOverlay BuildOverlay(Transform parent)
        {
            var canvasObject = new GameObject("Intro Overlay", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            var readout = Rect(root, "Watch Readout", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 460f));
            var group = readout.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Fill(Rect(readout, "Bezel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), new Color(0.02f, 0.02f, 0.02f, 1f));
            Fill(Rect(readout, "LCD Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 400f)), new Color(0.05f, 0.07f, 0.04f, 1f));
            Label(readout, "Time", "07:58", 300, Lcd, TextAnchor.MiddleCenter);

            var letterbox = Rect(root, "Letterbox", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Fill(Rect(letterbox, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 110f)), Color.black);
            Fill(Rect(letterbox, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 110f)), Color.black);

            var subtitle = Label(Rect(root, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(1400f, 120f)),
                "Text", string.Empty, 38, Paper, TextAnchor.LowerCenter);
            subtitle.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            var objective = Label(Rect(root, "Objective", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -135f), new Vector2(700f, 90f)),
                "Text", "OBJECTIVE: GET TO FERI\nDON'T LOOK BACK", 28, Paper, TextAnchor.UpperLeft);
            objective.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            objective.transform.parent.gameObject.SetActive(false);

            var hint = Label(Rect(root, "Skip Hint", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(400f, 36f)),
                "Text", "ENTER: SKIP", 22, new Color(Paper.r, Paper.g, Paper.b, 0.7f), TextAnchor.LowerRight);

            var fade = Fill(Rect(root, "Fade", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Color.black);

            var overlay = canvasObject.AddComponent<IntroOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("fade").objectReferenceValue = fade;
            so.FindProperty("letterbox").objectReferenceValue = letterbox.gameObject;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("watchReadout").objectReferenceValue = group;
            so.FindProperty("objective").objectReferenceValue = objective.transform.parent.gameObject;
            so.FindProperty("skipHint").objectReferenceValue = hint.transform.parent.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return overlay;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(RectTransform parent, string name, string content, int size, Color color, TextAnchor alignment)
        {
            RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.text = content;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // Gray dawn fog with a sickly red tint: Story §3 "something wrong with the sky's color".
        private static void ConfigureDawn()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.36f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.24f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.07f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.36f, 0.3f, 0.31f);
            RenderSettings.fogDensity = 0.012f;
        }

        private static void EnsureAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Cutscenes")) AssetDatabase.CreateFolder("Assets", "Cutscenes");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Cutscenes", "Intro");

            EnsureMaterial("Bottle", new Color(0.72f, 0.84f, 0.88f), 0.7f);
            EnsureMaterial("Ground", new Color(0.2f, 0.15f, 0.13f), 0.1f);
            EnsureMaterial("Silhouette", new Color(0.07f, 0.06f, 0.07f), 0f);

            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(Folder + "/IntroInput.asset") == null)
            {
                var input = ScriptableObject.CreateInstance<InputActionAsset>();
                var map = input.AddActionMap("Intro");
                map.AddAction("Skip", InputActionType.Button, "<Keyboard>/enter");
                map.AddAction("Replay", InputActionType.Button, "<Keyboard>/i");
                AssetDatabase.CreateAsset(input, Folder + "/IntroInput.asset");
                foreach (InputAction action in map.actions)
                {
                    var reference = InputActionReference.Create(action);
                    reference.name = action.name;
                    AssetDatabase.AddObjectToAsset(reference, input);
                }
            }

            AssetDatabase.ImportAsset(BeepPath);
            var importer = AssetImporter.GetAtPath(BeepPath) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Missing " + BeepPath + "; run music/generate_intro_sfx.py.");
            // Short clip: decode once on load so playback has no streaming latency.
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureMaterial(string name, Color color, float smoothness)
        {
            string path = Folder + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing " + typeof(T).Name + " at " + path + ".");
            return asset;
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
        }

        private static void RestoreScenes(SceneSetup[] previous)
        {
            if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
