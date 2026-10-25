using System;
using System.Collections.Generic;
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
    // The intro is a self-contained prefab (stage, Boki Intro, overlay, audio, IntroCutscene). Intro_Preview is its own
    // set when first built. IntegratePohorje replaces that set with the actual V1 scene.
    public static class IntroPreviewSetup
    {
        private const string Folder = "Assets/Cutscenes/Intro";
        private const string ScenePath = "Assets/Scenes/Intro_Preview.unity";
        private const string IntroPrefab = Folder + "/IntroCutscene.prefab";
        private const string PlayerPrefab = "Assets/Prefabs/Player/Boki_Rigged_Test.prefab";
        private const string AtlasPath = "Assets/Art/Player/Boki_Atlas.mat";
        private const string BeepPath = "Assets/Audio/Sfx/intro_phone_beep.wav";
        private const string EyePopPath = "Assets/Audio/Sfx/intro_eye_pop.wav";
        private const string DashPath = "Assets/Audio/Sfx/intro_dash.wav";
        private const string BokiLinePath = "Assets/Audio/Sfx/fak_nespet.mp3";
        private const string CutsceneName = "Intro Cutscene";
        private const string FramesVariable = "BOKI_INTRO_FRAMES";
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.86f);
        private static readonly Color Lcd = new Color(0.62f, 0.72f, 0.3f);

        // Regenerates the prefabs and Intro_Preview from scratch, keeping every asset path and GUID. Unity assigns new internal
        // ids on each save, so a scene that already uses the prefab would lose its overrides: that case is refused.
        [MenuItem("Boki/Intro/Rebuild Intro Preview")]
        public static void Create()
        {
            RequireIdle();
            RequireNotAdopted();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureAssets();
                IntroBokiBuilder.Build();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsurePrefab();
                var sun = new GameObject("Directional Light").AddComponent<Light>();
                ConfigureDawn(sun);
                BuildSet();

                var player = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PlayerPrefab));
                player.transform.SetPositionAndRotation(new Vector3(0f, IntroActing.ScooterSpot.y, 0f), Quaternion.identity);
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(ThirdPersonCamera));
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 400f;
                camera.clearFlags = CameraClearFlags.Skybox;
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
                Debug.Log("INTRO_PREVIEW_REBUILT: open " + ScenePath + ". No map, gameplay scene or build setting used or changed.");
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
                throw new InvalidOperationException("Rebuild the intro preview first.");
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

        // Upgrade only the requested scenes; never rebuild maps or shared prefabs.
        [MenuItem("Boki/Intro/Integrate Pohorje Intro")]
        public static void IntegratePohorje()
        {
            RequireIdle();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/V1_Pohorje.unity", OpenSceneMode.Single);
                WireSceneIntro(true);
                EditorSceneManager.SaveScene(scene);
                // Keep the established preview path and GUID, using the real level as its set.
                EditorSceneManager.SaveScene(scene, ScenePath, true);
                EditorSceneManager.OpenScene("Assets/Scenes/Player_Model_Test.unity", OpenSceneMode.Single);
                WireSceneIntro(false);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Debug.Log("POHORJE_INTRO_READY: V1_Pohorje and Intro_Preview start with the intro; Player_Model_Test has I to replay.");
            }
            finally { RestoreScenes(previous); }
        }

        private static void WireSceneIntro(bool autoplay)
        {
            var movement = Object.FindFirstObjectByType<ScooterController>();
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (movement == null || follow == null) throw new InvalidOperationException("Missing player or follow camera.");
            if (Object.FindFirstObjectByType<IntroCutscene>() == null) Adopt(movement, follow, autoplay);
            var intro = Object.FindFirstObjectByType<IntroCutscene>();
            var settings = new SerializedObject(intro);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(canvas => !canvas.transform.IsChildOf(intro.transform)).ToArray();
            var canvasReferences = settings.FindProperty("gameplayCanvases");
            canvasReferences.arraySize = canvases.Length;
            for (int i = 0; i < canvases.Length; i++) canvasReferences.GetArrayElementAtIndex(i).objectReferenceValue = canvases[i];
            settings.FindProperty("playOnStart").boolValue = autoplay;
            // Match the authored follow camera instead of assuming the preview's distance.
            settings.FindProperty("followDistance").floatValue = new SerializedObject(follow).FindProperty("followDistance").floatValue;
            if (autoplay) settings.FindProperty("replayAction").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // Renders review frames of every beat into output/intro-preview/frames (or $BOKI_INTRO_FRAMES); the scene is not saved.
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
                string folder = Environment.GetEnvironmentVariable(FramesVariable);
                if (string.IsNullOrEmpty(folder))
                    folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/intro-preview/frames"));
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
                    (0, 0.05f), (0, 0.5f), (0, 0.97f), (1, 0.6f), (1, 1f), (2, 0.7f),
                    (3, 0.15f), (3, 0.34f), (3, 0.5f), (3, 0.95f),
                    (4, 0.1f), (4, 0.3f), (4, 0.5f), (4, 0.9f),
                    (5, 0.2f), (5, 0.6f), (5, 0.9f),
                    (6, 0.06f), (6, 0.17f), (6, 0.28f), (6, 0.4f), (6, 0.55f), (6, 0.75f), (6, 0.9f), (6, 1f),
                    (7, 0.05f), (7, 0.6f), (7, 1f),
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
            var yaw = Quaternion.Euler(0f, movement.transform.eulerAngles.y, 0f);
            instance.transform.SetPositionAndRotation(movement.transform.position - yaw * IntroActing.ScooterSpot, yaw);
            var so = new SerializedObject(instance.GetComponent<IntroCutscene>());
            so.FindProperty("shotCamera").objectReferenceValue = follow.GetComponent<Camera>();
            so.FindProperty("rider").objectReferenceValue = visuals.gameObject;
            so.FindProperty("riderRoot").objectReferenceValue = movement.transform;
            so.FindProperty("followPivot").objectReferenceValue = pivot;
            so.FindProperty("headlight").objectReferenceValue = headlight;
            var controls = so.FindProperty("controls");
            var presentation = movement.GetComponent<ScooterPresentation>();
            controls.arraySize = presentation != null ? 3 : 2;
            controls.GetArrayElementAtIndex(0).objectReferenceValue = movement;
            controls.GetArrayElementAtIndex(1).objectReferenceValue = follow;
            if (presentation != null) controls.GetArrayElementAtIndex(2).objectReferenceValue = presentation;
            so.FindProperty("playOnStart").boolValue = playOnStart;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The prefab holds everything that belongs to the intro. Scene-side references (camera, player) stay empty.
        // An existing prefab is rewritten in place, so its GUID and any scene that uses it keep working.
        private static void EnsurePrefab()
        {
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

            var boki = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(IntroBokiBuilder.PrefabPath), wake);

            var scooter = new GameObject("Fallen Scooter").transform;
            scooter.SetParent(wake, false);
            scooter.localPosition = new Vector3(IntroActing.ScooterSpot.x, 0.12f, 0.2f);
            scooter.localRotation = Quaternion.Euler(0f, 20f, 90f);
            AddModel("PROP_Scooter", scooter, atlas);

            Transform bottle = BuildBottle(wake);
            BuildLeftovers(wake);

            var light = new GameObject("Stage Light").AddComponent<Light>();
            light.transform.SetParent(wake, false);
            light.transform.localPosition = new Vector3(0.6f, 2.4f, -0.4f);
            light.type = LightType.Point;
            light.color = new Color(0.7f, 0.76f, 0.95f);
            light.intensity = 9f;
            light.range = 14f;
            light.shadows = LightShadows.None;

            IntroOverlay overlay = BuildOverlay(root.transform);
            var input = AssetDatabase.LoadAllAssetsAtPath(Folder + "/IntroInput.asset").OfType<InputActionReference>().ToArray();
            var so = new SerializedObject(root.GetComponent<IntroCutscene>());
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("overlay").objectReferenceValue = overlay;
            so.FindProperty("wakeActors").objectReferenceValue = wake.gameObject;
            so.FindProperty("boki").objectReferenceValue = boki.GetComponent<IntroBoki>();
            so.FindProperty("fallenScooter").objectReferenceValue = scooter;
            so.FindProperty("bottle").objectReferenceValue = bottle;
            so.FindProperty("audioSource").objectReferenceValue = source;
            so.FindProperty("phoneBeep").objectReferenceValue = Load<AudioClip>(BeepPath);
            so.FindProperty("eyePop").objectReferenceValue = Load<AudioClip>(EyePopPath);
            so.FindProperty("dashWhoosh").objectReferenceValue = Load<AudioClip>(DashPath);
            so.FindProperty("bokiLine").objectReferenceValue = Load<AudioClip>(BokiLinePath);
            so.FindProperty("skipAction").objectReferenceValue = input.Single(r => r.name == "Skip");
            so.FindProperty("replayAction").objectReferenceValue = input.Single(r => r.name == "Replay");
            so.FindProperty("playOnStart").boolValue = false;
            CutsceneAmbienceSetup.AssignIntro(so);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.SaveAsPrefabAsset(root, IntroPrefab) == null)
                throw new InvalidOperationException("Could not save " + IntroPrefab + ".");
            Object.DestroyImmediate(root);
        }

        // Body, neck and cap, lying where Boki's sit-up will hit it.
        private static Transform BuildBottle(Transform parent)
        {
            var bottle = new GameObject("Plastic Bottle").transform;
            bottle.SetParent(parent, false);
            bottle.localPosition = new Vector3(0.78f, 0.04f, -0.5f);
            bottle.localRotation = Quaternion.Euler(0f, 25f, 90f);
            Shape(bottle, PrimitiveType.Cylinder, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.1f, 0.07f), "Bottle");
            Shape(bottle, PrimitiveType.Cylinder, "Neck", new Vector3(0f, 0.125f, 0f), new Vector3(0.028f, 0.025f, 0.028f), "Bottle");
            Shape(bottle, PrimitiveType.Cylinder, "Cap", new Vector3(0f, 0.158f, 0f), new Vector3(0.033f, 0.008f, 0.033f), "BottleCap");
            return bottle;
        }

        // The morning after: glow sticks and a cup in the grass beside him. Story §3 PROLOGUE shot 1.
        private static void BuildLeftovers(Transform parent)
        {
            var leftovers = new GameObject("Rave Leftovers").transform;
            leftovers.SetParent(parent, false);
            Shape(leftovers, PrimitiveType.Cylinder, "Glow Stick Lime", new Vector3(-0.62f, 0.014f, -0.25f), new Vector3(0.022f, 0.11f, 0.022f), "GlowLime", Quaternion.Euler(0f, 70f, 90f));
            Shape(leftovers, PrimitiveType.Cylinder, "Glow Stick Pink", new Vector3(-0.78f, 0.014f, 0.05f), new Vector3(0.022f, 0.11f, 0.022f), "GlowPink", Quaternion.Euler(0f, -20f, 90f));
            Shape(leftovers, PrimitiveType.Cylinder, "Glow Stick Cyan", new Vector3(0.7f, 0.014f, 0.5f), new Vector3(0.022f, 0.11f, 0.022f), "GlowCyan", Quaternion.Euler(0f, 115f, 90f));
            Shape(leftovers, PrimitiveType.Cylinder, "Party Cup", new Vector3(0.95f, 0.03f, 0.1f), new Vector3(0.075f, 0.03f, 0.075f), "Cup", Quaternion.Euler(0f, 0f, 78f));
        }

        // A neutral set: dawn sky, flat meadow, a skyline toward +Z (the city Boki cannot reach yet), pines at the sides.
        private static void BuildSet()
        {
            var set = new GameObject("Bellevue Set").transform;
            Mesh cone = IntroBokiBuilder.EnsureMesh("LowPolyCone", BuildCone(8));
            Mesh sphere = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Intro/Meshes/LowPolySphere.asset");

            Backdrop(Shape(set, PrimitiveType.Cube, "Ground", new Vector3(0f, -0.1f, 0f), new Vector3(240f, 0.2f, 240f), "Ground"));
            Shape(set, PrimitiveType.Cylinder, "Dirt Patch", new Vector3(0.9f, 0.004f, 0.3f), new Vector3(5.6f, 0.0012f, 4.4f), "Dirt").GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var random = new System.Random(1998);
            float[] rowDepth = { 88f, 118f, 152f };
            string[] rowMaterial = { "Silhouette", "SkylineMid", "SkylineFar" };
            for (int row = 0; row < rowDepth.Length; row++)
            {
                int count = 20 - row * 2;
                for (int i = 0; i < count; i++)
                {
                    float width = 4f + random.Next(0, 7);
                    float height = (7f + row * 3f) + random.Next(0, 17);
                    float x = Mathf.Lerp(-120f, 120f, i / (float)(count - 1)) + random.Next(-4, 5);
                    Backdrop(Shape(set, PrimitiveType.Cube, $"Skyline {row}.{i}", new Vector3(x, height / 2f, rowDepth[row] + random.Next(-6, 7)),
                        new Vector3(width, height, width * 0.8f), rowMaterial[row]));
                }
            }

            // Pines in two stands left and right, leaving the +Z corridor clear. Trunk plus three stacked cones.
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (16f + random.Next(0, 30));
                float z = -12f + random.Next(0, 70);
                float scale = 0.85f + random.Next(0, 10) * 0.07f;
                var pine = new GameObject("Pine " + i).transform;
                pine.SetParent(set, false);
                pine.localPosition = new Vector3(x, 0f, z);
                Shape(pine, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 0.9f * scale, 0f), new Vector3(0.45f, 0.9f, 0.45f) * scale, "Trunk");
                Tier(pine, cone, 1.6f * scale, 4.4f * scale, 3.4f * scale);
                Tier(pine, cone, 3.1f * scale, 3.6f * scale, 3.0f * scale);
                Tier(pine, cone, 4.5f * scale, 2.8f * scale, 2.6f * scale);
            }

            // Grass tufts and a few rocks around the dirt patch, so the ground reads as a meadow and not a slab.
            for (int i = 0; i < 48; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                float radius = 3.4f + (float)random.NextDouble() * 9f;
                var tuft = Part(set, cone, "Grass " + i, "Grass");
                tuft.localPosition = new Vector3(0.9f + Mathf.Cos(angle) * radius * 1.2f, 0.07f, 0.3f + Mathf.Sin(angle) * radius);
                tuft.localScale = new Vector3(0.16f, 0.18f + random.Next(0, 4) * 0.05f, 0.16f);
                tuft.localRotation = Quaternion.Euler(random.Next(-8, 9), random.Next(0, 360), random.Next(-8, 9));
                tuft.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Vector3[] rocks = { new Vector3(-3.4f, 0.25f, 4.2f), new Vector3(4.6f, 0.3f, 1.4f), new Vector3(-5.8f, 0.2f, -2.2f), new Vector3(2.9f, 0.18f, -4.1f), new Vector3(6.2f, 0.28f, 6.8f) };
            for (int i = 0; i < rocks.Length; i++)
            {
                var rock = Part(set, sphere, "Rock " + i, "Rock");
                rock.localPosition = rocks[i];
                rock.localScale = new Vector3(0.9f + i * 0.15f, 0.55f + (i % 2) * 0.15f, 0.7f + i * 0.1f);
                rock.localRotation = Quaternion.Euler(0f, i * 57f, 0f);
            }
        }

        private static void Tier(Transform parent, Mesh cone, float y, float height, float diameter)
        {
            var tier = Part(parent, cone, "Needles", "Pine");
            tier.localPosition = new Vector3(0f, y, 0f);
            tier.localScale = new Vector3(diameter, height, diameter);
        }

        // Backdrop only: long skyline shadows would streak across the stage.
        private static void Backdrop(Transform shape) => shape.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        private static Transform Shape(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, string material)
            => Shape(parent, type, name, position, scale, material, Quaternion.identity);

        private static Transform Shape(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, string material, Quaternion rotation)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.SetLocalPositionAndRotation(position, rotation);
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = Load<Material>(Folder + "/" + material + ".mat");
            return shape.transform;
        }

        private static Transform Part(Transform parent, Mesh mesh, string name, string material)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = Load<Material>(Folder + "/" + material + ".mat");
            return part.transform;
        }

        // Unit cone: base diameter 1 at y = 0, apex at y = 1.
        private static Mesh BuildCone(int sides)
        {
            var triangles = new List<Vector3>();
            var apex = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                float b = 2f * Mathf.PI * (i + 1) / sides;
                var p0 = 0.5f * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) ;
                var p1 = 0.5f * new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
                IntroBokiBuilder.AddTriangle(triangles, p0, p1, apex, new Vector3(p0.x + p1.x, 0.5f, p0.z + p1.z));
                IntroBokiBuilder.AddTriangle(triangles, p0, p1, Vector3.zero, Vector3.down);
            }
            return IntroBokiBuilder.FlatMesh(triangles);
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

            // A soft band behind the subtitle keeps it readable over the bright closeups.
            var backing = Fill(Rect(root, "Subtitle Backing", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(0f, 96f)), new Color(0f, 0f, 0f, 0.5f));
            var subtitle = Label(Rect(root, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(1400f, 120f)),
                "Text", string.Empty, 38, Paper, TextAnchor.LowerCenter);
            subtitle.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            var objective = Label(Rect(root, "Objective", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -135f), new Vector2(700f, 90f)),
                "Text", "OBJECTIVE: GET TO FERI\nDON'T LOOK BACK", 28, Paper, TextAnchor.UpperLeft);
            objective.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            objective.transform.parent.gameObject.SetActive(false);

            var hint = Label(Rect(root, "Skip Hint", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(400f, 36f)),
                "Text", "ENTER: SKIP", 22, new Color(Paper.r, Paper.g, Paper.b, 0.7f), TextAnchor.LowerRight);

            var flash = Fill(Rect(root, "Flash", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), new Color(1f, 1f, 1f, 0f));
            var fade = Fill(Rect(root, "Fade", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Color.black);

            var overlay = canvasObject.AddComponent<IntroOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("fade").objectReferenceValue = fade;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("letterbox").objectReferenceValue = letterbox.gameObject;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("subtitleBacking").objectReferenceValue = backing.gameObject;
            so.FindProperty("watchReadout").objectReferenceValue = group;
            so.FindProperty("objective").objectReferenceValue = objective.transform.parent.gameObject;
            so.FindProperty("skipHint").objectReferenceValue = hint.transform.parent.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return overlay;
        }

        internal static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
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

        internal static Image Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static Text Label(RectTransform parent, string name, string content, int size, Color color, TextAnchor alignment)
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

        // Dawn behind the city with a sickly red cast: Story §3 "something wrong with the sky's color".
        private static void ConfigureDawn(Light sun)
        {
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.7f, 0.55f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(12f, 205f, 0f);

            var sky = EnsureMaterial("DawnSky", "Skybox/Procedural", new Color(0.55f, 0.5f, 0.55f));
            sky.SetFloat("_SunSize", 0.06f);
            sky.SetFloat("_AtmosphereThickness", 1.5f);
            sky.SetFloat("_Exposure", 1f);
            sky.SetColor("_GroundColor", new Color(0.33f, 0.27f, 0.29f));
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.46f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.32f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.09f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.58f, 0.45f, 0.46f);
            RenderSettings.fogDensity = 0.0065f;
        }

        private static Material EnsureMaterial(string name, string shaderName, Color tint)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null) throw new InvalidOperationException(shaderName + " is unavailable.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_SkyTint", tint);
            return material;
        }

        private static void EnsureAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Cutscenes")) AssetDatabase.CreateFolder("Assets", "Cutscenes");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Cutscenes", "Intro");

            // Set pieces, tinted for a cold dawn. Existing materials are updated in place so their GUIDs stay.
            Stage("Bottle", new Color(0.72f, 0.84f, 0.88f), 0.7f);
            Stage("BottleCap", new Color(0.86f, 0.2f, 0.18f), 0.4f);
            Stage("Cup", new Color(0.94f, 0.38f, 0.29f), 0.3f);
            Stage("Ground", new Color(0.2f, 0.27f, 0.2f), 0.05f);
            Stage("Dirt", new Color(0.38f, 0.32f, 0.24f), 0.05f);
            Stage("Grass", new Color(0.26f, 0.34f, 0.22f), 0.05f);
            Stage("Rock", new Color(0.33f, 0.33f, 0.35f), 0.1f);
            Stage("Trunk", new Color(0.16f, 0.12f, 0.1f), 0f);
            Stage("Pine", new Color(0.1f, 0.18f, 0.17f), 0f);
            Stage("Silhouette", new Color(0.14f, 0.12f, 0.17f), 0f);
            Stage("SkylineMid", new Color(0.21f, 0.17f, 0.22f), 0f);
            Stage("SkylineFar", new Color(0.3f, 0.24f, 0.29f), 0f);
            IntroBokiBuilder.EnsureMaterial(Folder, "GlowLime", new Color32(0xCF, 0xEF, 0x75, 0xFF), 0f, true);
            IntroBokiBuilder.EnsureMaterial(Folder, "GlowPink", new Color(1f, 0.35f, 0.7f), 0f, true);
            IntroBokiBuilder.EnsureMaterial(Folder, "GlowCyan", new Color(0.3f, 0.9f, 1f), 0f, true);

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

            foreach (string path in new[] { BeepPath, EyePopPath, DashPath }) ConfigureClip(path);
            AssetDatabase.SaveAssets();
        }

        private static void Stage(string name, Color color, float smoothness)
            => IntroBokiBuilder.EnsureMaterial(Folder, name, color, smoothness, false);

        internal static void ConfigureClip(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Missing " + path + "; run music/generate_intro_sfx.py.");
            // Short clip: decode once on load so playback has no streaming latency.
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        internal static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing " + typeof(T).Name + " at " + path + ".");
            return asset;
        }

        internal static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireNotAdopted()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != ScenePath && AssetDatabase.GetDependencies(path, false).Contains(IntroPrefab))
                    throw new InvalidOperationException(path + " already uses " + IntroPrefab
                        + "; a rebuild would break its overrides. Edit the prefab directly instead.");
            }
        }

        internal static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
        }

        internal static void RestoreScenes(SceneSetup[] previous)
        {
            if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
