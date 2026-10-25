using System;
using System.Linq;
using Boki.ArenaIntro;
using Boki.Battery;
using Boki.Ending;
using Boki.Intro;
using Boki.Player;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // The first-arena scare is a self-contained prefab (stage, close-up Boki, the creeping enemy, overlay, audio and
    // ArenaIntroCutscene). ArenaIntro_Preview is a map-free night clearing to see it in; IntegratePohorje adds the same
    // prefab to V1_Pohorje. Like the intro and ending, it holds no map asset.
    public static class ArenaIntroPreviewSetup
    {
        public const string PrefabPath = Folder + "/ArenaIntroCutscene.prefab";
        public const string ScenePath = "Assets/Scenes/ArenaIntro_Preview.unity";
        private const string Folder = "Assets/Cutscenes/ArenaIntro";
        private const string InputPath = Folder + "/ArenaIntroInput.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Player/Boki_Rigged_Test.prefab";
        private const string PohorjeScenePath = "Assets/Scenes/V1_Pohorje.unity";
        private const string SfxFolder = "Assets/Audio/Sfx/";
        private const string CutsceneName = "Arena Intro Cutscene";
        private const uint RiderRenderingLayer = 2u;
        private static readonly string[] SynthesizedCues = { "arena_intro_rustle", "arena_intro_step", "arena_intro_stinger" };
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.86f);

        // Regenerates the prefab and ArenaIntro_Preview from scratch, keeping every asset path and GUID. Unity assigns new
        // internal ids on each save, so a scene that already uses the prefab would lose its overrides: that case is refused.
        [MenuItem("Boki/Arena Intro/Rebuild Arena Intro Preview")]
        public static void Create()
        {
            IntroPreviewSetup.RequireIdle();
            RequireNotAdopted();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureAssets();
                EnsurePrefab();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ConfigureNight();
                BuildSet();

                var player = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PlayerPrefab));
                player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -24f), Quaternion.identity);
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(ThirdPersonCamera));
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                var follow = cameraObject.GetComponent<ThirdPersonCamera>();
                IntroPreviewSetup.Set(follow, "followPivot", player.transform.Find("CameraPivot"));
                IntroPreviewSetup.Set(follow, "lookAction", Load<InputActionReference>("Assets/Input/M1/Look.asset"));
                var settings = new SerializedObject(follow);
                settings.FindProperty("obstructionMask").intValue = 1;
                settings.ApplyModifiedPropertiesWithoutUndo();
                IntroPreviewSetup.Set(player.GetComponent<ScooterController>(), "movementCamera", camera.transform);

                BuildArena(player);
                Adopt(true);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("ARENA_INTRO_PREVIEW_REBUILT: open " + ScenePath + " and ride forward into the clearing. No map, gameplay scene or build setting used or changed.");
            }
            finally
            {
                IntroPreviewSetup.RestoreScenes(previous);
            }
        }

        [MenuItem("Boki/Arena Intro/Open Arena Intro Preview")]
        public static void Open()
        {
            IntroPreviewSetup.RequireIdle();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Rebuild the arena intro preview first.");
            EditorSceneManager.OpenScene(ScenePath);
        }

        // For another map: the open scene must already hold the player, its follow camera and its combat spaces.
        // The scene is left modified but unsaved so its owner can review the change.
        [MenuItem("Boki/Arena Intro/Add Arena Intro To Open Scene")]
        public static void AddToOpenScene()
        {
            IntroPreviewSetup.RequireIdle();
            if (Object.FindAnyObjectByType<ArenaIntroCutscene>() != null)
            {
                Debug.Log("This scene already has an arena intro; nothing changed.");
                return;
            }
            ArenaIntroCutscene added = Adopt(false);
            EditorSceneManager.MarkSceneDirty(added.gameObject.scene);
            Debug.Log("ARENA_INTRO_ADDED_TO_OPEN_SCENE: review and save. It plays once, the first time Boki occupies one of the scene's combat spaces.");
        }

        // Upgrades only V1_Pohorje: one prefab instance and its scene-side references. Never rebuilds the map.
        [MenuItem("Boki/Arena Intro/Integrate Pohorje Arena Intro")]
        public static void IntegratePohorje()
        {
            IntroPreviewSetup.RequireIdle();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(PohorjeScenePath, OpenSceneMode.Single);
                if (Object.FindAnyObjectByType<ArenaIntroCutscene>() == null) Adopt(false);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("POHORJE_ARENA_INTRO_READY: V1_Pohorje plays the arena intro the first time Boki enters the arena.");
            }
            finally
            {
                IntroPreviewSetup.RestoreScenes(previous);
            }
        }

        // Puts the prefab in the open scene and assigns the scene-side references. The preview keeps the R replay; a game
        // scene clears it, so the scare plays once, on first entry.
        public static ArenaIntroCutscene Adopt(bool preview)
        {
            var movement = Object.FindAnyObjectByType<ScooterController>();
            var follow = Object.FindAnyObjectByType<ThirdPersonCamera>();
            if (movement == null || follow == null)
                throw new InvalidOperationException("Open a scene that already has the player (ScooterController) and its ThirdPersonCamera.");
            Transform visuals = movement.transform.Find("Visuals");
            if (visuals == null) throw new InvalidOperationException("The player needs its Visuals child.");
            BatteryCombatSpace[] arenas = Object.FindObjectsByType<BatteryCombatSpace>(FindObjectsInactive.Include)
                .OrderBy(space => space.name).ToArray();
            if (arenas.Length == 0) throw new InvalidOperationException("The scene needs at least one BatteryCombatSpace.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PrefabPath));
            SceneManager.MoveGameObjectToScene(instance, movement.gameObject.scene);
            instance.transform.SetPositionAndRotation(movement.transform.position, Quaternion.identity);
            // Gameplay canvases only: not this scene's own overlay, nor another cutscene's.
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)
                .Where(canvas => !canvas.transform.IsChildOf(instance.transform)
                    && canvas.GetComponentInParent<IntroCutscene>() == null && canvas.GetComponentInParent<EndingCutscene>() == null).ToArray();
            var presentation = movement.GetComponent<ScooterPresentation>();
            Behaviour[] controls = presentation != null
                ? new Behaviour[] { movement, follow, presentation }
                : new Behaviour[] { movement, follow };

            var cutscene = instance.GetComponent<ArenaIntroCutscene>();
            var so = new SerializedObject(cutscene);
            so.FindProperty("shotCamera").objectReferenceValue = follow.GetComponent<Camera>();
            so.FindProperty("riderRoot").objectReferenceValue = movement.transform;
            so.FindProperty("rider").objectReferenceValue = visuals.gameObject;
            so.FindProperty("recordedAudio").objectReferenceValue = Object.FindAnyObjectByType<RecordedGameAudio>();
            SetObjects(so.FindProperty("arenas"), arenas);
            SetObjects(so.FindProperty("controls"), controls);
            SetObjects(so.FindProperty("gameplayCanvases"), canvases);
            if (!preview) so.FindProperty("replayAction").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            return cutscene;
        }

        // The prefab holds everything that belongs to the scare. Scene-side references stay empty.
        // An existing prefab is rewritten in place, so its GUID and any scene that uses it keep working.
        private static void EnsurePrefab()
        {
            var root = new GameObject(CutsceneName, typeof(AudioSource), typeof(ArenaIntroCutscene));
            var source = root.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            var stage = new GameObject("Arena Intro Stage").transform;
            stage.SetParent(root.transform, false);
            var actors = new GameObject("Actors").transform;
            actors.SetParent(stage, false);

            // Faces the camera from just ahead of Boki, lit like the rider: by rider-layer lights only, not the headlight spill.
            var boki = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(IntroBokiBuilder.PrefabPath), actors);
            var light = new GameObject("Stage Light").AddComponent<Light>();
            light.transform.SetParent(actors, false);
            light.transform.localPosition = new Vector3(-0.7f, 2.1f, 1.5f);
            light.type = LightType.Point;
            light.color = new Color(0.72f, 0.8f, 1f);
            light.intensity = 4f;
            light.range = 5f;
            light.shadows = LightShadows.None;
            light.GetUniversalAdditionalLightData().renderingLayers = RiderRenderingLayer;

            Transform creeper = BuildCreeper(actors, out AudioSource voice, out AudioSource steps, out Animator animator);
            ArenaIntroOverlay overlay = BuildOverlay(root.transform);

            InputActionReference[] input = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>().ToArray();
            var so = new SerializedObject(root.GetComponent<ArenaIntroCutscene>());
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("actors").objectReferenceValue = actors.gameObject;
            so.FindProperty("boki").objectReferenceValue = boki.GetComponent<IntroBoki>();
            so.FindProperty("stageLight").objectReferenceValue = light;
            so.FindProperty("creeper").objectReferenceValue = creeper;
            so.FindProperty("creeperAnimator").objectReferenceValue = animator;
            so.FindProperty("creeperVoice").objectReferenceValue = voice;
            so.FindProperty("creeperSteps").objectReferenceValue = steps;
            so.FindProperty("overlay").objectReferenceValue = overlay;
            so.FindProperty("audioSource").objectReferenceValue = source;
            so.FindProperty("rustle").objectReferenceValue = Load<AudioClip>(SfxFolder + "arena_intro_rustle.wav");
            so.FindProperty("eyePop").objectReferenceValue = Load<AudioClip>(SfxFolder + "intro_eye_pop.wav");
            so.FindProperty("bokiLine").objectReferenceValue = Load<AudioClip>(SfxFolder + "Recorded/Dialogue/kaj_je_to_blo.wav");
            so.FindProperty("greeting").objectReferenceValue = Load<AudioClip>(SfxFolder + "Recorded/Enemies/greeting.wav");
            so.FindProperty("step").objectReferenceValue = Load<AudioClip>(SfxFolder + "arena_intro_step.wav");
            so.FindProperty("stinger").objectReferenceValue = Load<AudioClip>(SfxFolder + "arena_intro_stinger.wav");
            so.FindProperty("skipAction").objectReferenceValue = input.Single(reference => reference.name == "Skip");
            so.FindProperty("replayAction").objectReferenceValue = input.Single(reference => reference.name == "Replay");
            so.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("Could not save " + PrefabPath + ".");
            Object.DestroyImmediate(root);
        }

        // The forest enemy's model, material and animator, without any of its gameplay: the same look the arena fights.
        private static Transform BuildCreeper(Transform parent, out AudioSource voice, out AudioSource steps, out Animator animator)
        {
            var creeper = new GameObject("Creeper").transform;
            creeper.SetParent(parent, false);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(EnemyModelSetup.Art + "Export/CHR_Melee.fbx"), creeper);
            visual.name = "Visual";
            // The imported axis and unit conversion on the model root stays untouched.
            visual.transform.localPosition = Vector3.zero;
            var skin = Load<Material>(EnemyModelSetup.Art + "M_Melee_Forest.mat");
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = skin;
                renderer.sharedMaterials = materials;
            }
            foreach (Transform part in visual.GetComponentsInChildren<Transform>(true))
                if (part.name == "M_Melee_Jacket" || part.name == "M_Melee_Backpack") part.gameObject.SetActive(false);
            animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = Load<RuntimeAnimatorController>(EnemyModelSetup.Art + "CHR_Melee.controller");
            animator.applyRootMotion = false;
            voice = Positional(creeper, "Voice");
            steps = Positional(creeper, "Steps");
            return creeper;
        }

        private static AudioSource Positional(Transform parent, string name)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = new Vector3(0f, 1.2f, 0f);
            var source = holder.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 45f;
            source.dopplerLevel = 0f;
            return source;
        }

        private static ArenaIntroOverlay BuildOverlay(Transform parent)
        {
            var canvasObject = new GameObject("Arena Intro Overlay", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            var letterbox = IntroPreviewSetup.Rect(root, "Letterbox", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            IntroPreviewSetup.Fill(IntroPreviewSetup.Rect(letterbox, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 110f)), Color.black);
            IntroPreviewSetup.Fill(IntroPreviewSetup.Rect(letterbox, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 110f)), Color.black);

            // A soft band behind the subtitle keeps it readable over the bright closeups.
            var backing = IntroPreviewSetup.Fill(IntroPreviewSetup.Rect(root, "Subtitle Backing", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(0f, 96f)), new Color(0f, 0f, 0f, 0.5f));
            var subtitle = IntroPreviewSetup.Label(IntroPreviewSetup.Rect(root, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(1400f, 120f)),
                "Text", string.Empty, 38, Paper, TextAnchor.LowerCenter);
            subtitle.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            var hint = IntroPreviewSetup.Label(IntroPreviewSetup.Rect(root, "Skip Hint", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(400f, 36f)),
                "Text", "ENTER: SKIP", 22, new Color(Paper.r, Paper.g, Paper.b, 0.7f), TextAnchor.LowerRight);

            var flash = IntroPreviewSetup.Fill(IntroPreviewSetup.Rect(root, "Flash", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), new Color(1f, 1f, 1f, 0f));

            var overlay = canvasObject.AddComponent<ArenaIntroOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("letterbox").objectReferenceValue = letterbox.gameObject;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("subtitleBacking").objectReferenceValue = backing.gameObject;
            so.FindProperty("skipHint").objectReferenceValue = hint.transform.parent.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return overlay;
        }

        // A night clearing: black sky and fog, almost no ambient light, so only the scooter headlight shows the way.
        private static void ConfigureNight()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogDensity = 0.03f;
        }

        // Flat ground and pines scattered round a clear arena, leaving a corridor for the ride in.
        private static void BuildSet()
        {
            var set = new GameObject("Night Clearing Set").transform;
            Material ground = Stage("Ground", new Color(0.09f, 0.12f, 0.08f), 0.05f);
            Material trunk = Stage("Trunk", new Color(0.16f, 0.12f, 0.1f), 0f);
            Material needles = Stage("Needles", new Color(0.07f, 0.14f, 0.13f), 0f);
            Mesh cone = Load<Mesh>("Assets/Art/Intro/Meshes/LowPolyCone.asset");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ground";
            floor.transform.SetParent(set, false);
            floor.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            floor.transform.localScale = new Vector3(160f, 0.4f, 160f);
            floor.GetComponent<Renderer>().sharedMaterial = ground;
            floor.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var random = new System.Random(2026);
            for (int i = 0; i < 70; i++)
            {
                float x, z;
                do
                {
                    x = random.Next(-60, 61);
                    z = random.Next(-45, 70);
                }
                while (x * x + (z - 8f) * (z - 8f) < 21f * 21f || (Mathf.Abs(x) < 4f && z < -4f));
                float scale = 0.85f + random.Next(0, 10) * 0.07f;
                var pine = new GameObject("Pine " + i).transform;
                pine.SetParent(set, false);
                pine.localPosition = new Vector3(x, 0f, z);
                var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = "Trunk";
                stem.transform.SetParent(pine, false);
                stem.transform.localPosition = new Vector3(0f, 2.5f * scale, 0f);
                stem.transform.localScale = new Vector3(0.5f, 2.5f, 0.5f) * scale;
                stem.GetComponent<Renderer>().sharedMaterial = trunk;
                Tier(pine, cone, needles, 2.4f * scale, 3.2f * scale, 3.4f * scale);
                Tier(pine, cone, needles, 3.8f * scale, 3f * scale, 2.8f * scale);
                Tier(pine, cone, needles, 5.1f * scale, 2.6f * scale, 2.2f * scale);
            }
        }

        private static void Tier(Transform parent, Mesh cone, Material material, float y, float height, float diameter)
        {
            var tier = new GameObject("Needles", typeof(MeshFilter), typeof(MeshRenderer));
            tier.transform.SetParent(parent, false);
            tier.GetComponent<MeshFilter>().sharedMesh = cone;
            tier.GetComponent<MeshRenderer>().sharedMaterial = material;
            tier.transform.localPosition = new Vector3(0f, y, 0f);
            tier.transform.localScale = new Vector3(diameter, height, diameter);
        }

        // The combat space the ride-in occupies: a trigger volume bound to the player's battery, as in the real arenas.
        private static void BuildArena(GameObject player)
        {
            var volume = new GameObject("Arena Space");
            volume.transform.position = new Vector3(0f, 3f, 8f);
            var box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(34f, 8f, 30f);
            var space = volume.AddComponent<BatteryCombatSpace>();
            var so = new SerializedObject(space);
            so.FindProperty("battery").objectReferenceValue = player.GetComponent<LightBattery>();
            so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerHealth>();
            so.FindProperty("spaceId").stringValue = "arena_intro_preview";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Stage(string name, Color color, float smoothness)
            => IntroBokiBuilder.EnsureMaterial(Folder, name, color, smoothness, false);

        private static void EnsureAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Cutscenes")) AssetDatabase.CreateFolder("Assets", "Cutscenes");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Cutscenes", "ArenaIntro");

            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath) == null)
            {
                var input = ScriptableObject.CreateInstance<InputActionAsset>();
                var map = input.AddActionMap("ArenaIntro");
                map.AddAction("Skip", InputActionType.Button, "<Keyboard>/enter");
                map.AddAction("Replay", InputActionType.Button, "<Keyboard>/r");
                AssetDatabase.CreateAsset(input, InputPath);
                foreach (InputAction action in map.actions)
                {
                    var reference = InputActionReference.Create(action);
                    reference.name = action.name;
                    AssetDatabase.AddObjectToAsset(reference, input);
                }
            }
            foreach (string cue in SynthesizedCues) IntroPreviewSetup.ConfigureClip(SfxFolder + cue + ".wav");
            AssetDatabase.SaveAssets();
        }

        private static void RequireNotAdopted()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != ScenePath && AssetDatabase.GetDependencies(path, false).Contains(PrefabPath))
                    throw new InvalidOperationException(path + " already uses " + PrefabPath
                        + "; a rebuild would break its overrides. Edit the prefab directly instead.");
            }
        }

        private static void SetObjects(SerializedProperty list, Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static T Load<T>(string path) where T : Object => IntroPreviewSetup.Load<T>(path);
    }
}
