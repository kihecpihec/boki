using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Boki.Ending;
using Boki.Enemies;
using Boki.Intro;
using Boki.Player;
using Boki.Routes;
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
    // The ending is a self-contained prefab (stage, calendar, lights, cat professor, Boki, overlay, audio, EndingCutscene).
    // Ending_Preview is its own set with no map asset. IntegrateFeri wires the same prefab into V1_FERI.
    public static class EndingPreviewSetup
    {
        private const string Folder = "Assets/Cutscenes/Ending";
        private const string ScenePath = "Assets/Scenes/Ending_Preview.unity";
        private const string FeriScenePath = "Assets/Scenes/V1_FERI.unity";
        private const string CutscenePrefab = Folder + "/EndingCutscene.prefab";
        private const string InputPath = Folder + "/EndingInput.asset";
        private const string SfxFolder = "Assets/Audio/Sfx/";
        private const string EyePopPath = SfxFolder + "intro_eye_pop.wav";
        private const string AtlasPath = "Assets/Art/Player/Boki_Atlas.mat";
                private const string CutsceneName = "Ending Cutscene";
        private const string StageName = "Ending Stage";
        private const string FramesVariable = "BOKI_ENDING_FRAMES";
        private static readonly string[] Clips = { "ending_deflate", "ending_zoom", "ending_thump", "ending_meow", "ending_lick" };
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.86f);

        // Regenerates the prefabs and Ending_Preview from scratch, keeping every asset path and GUID. Unity assigns new
        // internal ids on each save, so a scene that already uses the prefab would lose its overrides: that case is refused.
        [MenuItem("Boki/Ending/Rebuild Ending Preview")]
        public static void Create()
        {
            RequireIdle();
            RequireNotAdopted();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureAssets();
                EndingProfessorBuilder.Build();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsurePrefab();
                var sun = new GameObject("Directional Light").AddComponent<Light>();
                ConfigureRoom(sun);
                BuildRoom();

                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transform.position = new Vector3(0f, 2.7f, 6f);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(CutscenePrefab));
                var so = new SerializedObject(instance.GetComponent<EndingCutscene>());
                so.FindProperty("shotCamera").objectReferenceValue = camera;
                so.FindProperty("playOnStart").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("ENDING_PREVIEW_REBUILT: open " + ScenePath + ". No map, gameplay scene or build setting used or changed.");
            }
            finally
            {
                RestoreScenes(previous);
            }
        }

        [MenuItem("Boki/Ending/Open Ending Preview")]
        public static void Open()
        {
            RequireIdle();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Rebuild the ending preview first.");
            EditorSceneManager.OpenScene(ScenePath);
        }

        // Upgrades only V1_FERI: the stage goes where the professor stands, turned to face the classroom entry, and the boss
        // arena hands the victory to the ending. It never rebuilds the map or shared prefabs; the scene is saved.
        [MenuItem("Boki/Ending/Integrate FERI Ending")]
        public static void IntegrateFeri()
        {
            RequireIdle();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(FeriScenePath, OpenSceneMode.Single);
                var arena = Object.FindAnyObjectByType<V1BossArena>(FindObjectsInactive.Include);
                var boss = Object.FindAnyObjectByType<ProfessorBoss>(FindObjectsInactive.Include);
                var movement = Object.FindAnyObjectByType<ScooterController>();
                var follow = Object.FindAnyObjectByType<ThirdPersonCamera>();
                if (arena == null || boss == null || movement == null || follow == null)
                    throw new InvalidOperationException("V1_FERI needs its boss arena, professor, player and follow camera.");

                var cutscene = Object.FindAnyObjectByType<EndingCutscene>();
                if (cutscene == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(CutscenePrefab));
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    cutscene = instance.GetComponent<EndingCutscene>();
                    Vector3 towardEntry = Vector3.ProjectOnPlane(arena.transform.position - boss.transform.position, Vector3.up);
                    instance.transform.Find(StageName).SetPositionAndRotation(boss.transform.position,
                        Quaternion.LookRotation(towardEntry.sqrMagnitude > 0.01f ? towardEntry : Vector3.back, Vector3.up));
                }
                WireScene(cutscene, movement, follow, false);
                var arenaSettings = new SerializedObject(arena);
                arenaSettings.FindProperty("ending").objectReferenceValue = cutscene;
                arenaSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("FERI_ENDING_READY: V1_FERI plays the ending when the professor falls, then shows the results panel.");
            }
            finally
            {
                RestoreScenes(previous);
            }
        }

        // Scene-side references: the camera, what the ending replaces, and what must be switched off while it plays.
        private static void WireScene(EndingCutscene cutscene, ScooterController movement, ThirdPersonCamera follow, bool playOnStart)
        {
            var so = new SerializedObject(cutscene);
            so.FindProperty("shotCamera").objectReferenceValue = follow.GetComponent<Camera>();
            var hidden = new List<GameObject>();
            Transform visuals = movement.transform.Find("Visuals");
            if (visuals != null) hidden.Add(visuals.gameObject);
            // The player's own lights would glow in the middle of the classroom.
            foreach (Light light in movement.GetComponentsInChildren<Light>(true)) hidden.Add(light.gameObject);
            SetObjects(so.FindProperty("hiddenDuringEnding"), hidden.Distinct().ToArray());
            var controls = new List<Object> { movement, follow };
            var presentation = movement.GetComponent<ScooterPresentation>();
            if (presentation != null) controls.Add(presentation);
            SetObjects(so.FindProperty("controls"), controls.ToArray());
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)
                .Where(canvas => !canvas.transform.IsChildOf(cutscene.transform)).ToArray();
            SetObjects(so.FindProperty("gameplayCanvases"), canvases);
            so.FindProperty("playOnStart").boolValue = playOnStart;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjects(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        [MenuItem("Boki/Ending/Capture Preview Frames")]
        public static void CapturePreviewFrames() => CaptureFrames(ScenePath, "preview");

        [MenuItem("Boki/Ending/Capture FERI Frames")]
        public static void CaptureFeriFrames() => CaptureFrames(FeriScenePath, "feri");

        // Renders review frames of every beat into output/ending-preview/<name> (or $BOKI_ENDING_FRAMES/<name>); no scene is saved.
        private static void CaptureFrames(string scenePath, string name)
        {
            RequireIdle();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var cutscene = Object.FindAnyObjectByType<EndingCutscene>();
                if (cutscene == null) throw new InvalidOperationException(scenePath + " has no EndingCutscene.");
                var camera = new SerializedObject(cutscene).FindProperty("shotCamera").objectReferenceValue as Camera;
                var canvas = cutscene.GetComponentInChildren<EndingOverlay>(true).GetComponent<Canvas>();
                string folder = Environment.GetEnvironmentVariable(FramesVariable);
                if (string.IsNullOrEmpty(folder))
                    folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/ending-preview"));
                folder = Path.Combine(folder, name);
                Directory.CreateDirectory(folder);

                // Edit Mode shows the stage as authored: switch on what Play() would, and switch off the host's own overlays.
                foreach (Canvas other in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                    if (other != canvas) other.enabled = false;
                var settings = new SerializedObject(cutscene);
                SerializedProperty hiddenReferences = settings.FindProperty("hiddenDuringEnding");
                for (int i = 0; i < hiddenReferences.arraySize; i++)
                    (hiddenReferences.GetArrayElementAtIndex(i).objectReferenceValue as GameObject)?.SetActive(false);
                ((GameObject)settings.FindProperty("actors").objectReferenceValue).SetActive(true);
                ((GameObject)settings.FindProperty("stageLights").objectReferenceValue).SetActive(true);
                canvas.gameObject.SetActive(true);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.01f;
                var skins = Object.FindObjectsByType<SkinnedMeshRenderer>();
                var texture = new RenderTexture(1280, 720, 24);
                camera.targetTexture = texture;
                var frames = new (int shot, float progress)[]
                {
                    (0, 0.02f), (0, 0.2f), (0, 0.4f), (0, 0.55f), (0, 0.75f), (0, 1f),
                    (1, 0.2f), (1, 0.5f), (1, 0.85f),
                    (2, 0.3f), (2, 0.6f), (2, 0.95f),
                    (3, 0.25f), (3, 0.55f), (3, 0.9f),
                    (4, 0.15f), (4, 0.5f), (4, 1f),
                    (5, 0.1f), (5, 0.5f), (5, 0.8f), (5, 0.95f), (5, 1f),
                    (6, 0.1f), (6, 0.4f), (6, 0.6f), (6, 0.8f), (6, 1f),
                    (7, 0.1f), (7, 0.3f), (7, 0.45f), (7, 0.55f), (7, 0.8f), (7, 0.95f),
                    (8, 0.05f), (8, 0.25f), (8, 0.6f), (8, 0.9f),
                    (9, 1f),
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
                Debug.Log("ENDING_FRAMES_CAPTURED: " + folder);
            }
            finally
            {
                RestoreScenes(previous);
            }
        }

        // The prefab holds everything that belongs to the ending. Scene-side references (camera, player parts) stay empty.
        // An existing prefab is rewritten in place, so its GUID and any scene that uses it keep working.
        private static void EnsurePrefab()
        {
            var root = new GameObject(CutsceneName, typeof(AudioSource), typeof(EndingCutscene));
            var source = root.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            var stage = new GameObject(StageName).transform;
            stage.SetParent(root.transform, false);
            var actors = new GameObject("Ending Actors").transform;
            actors.SetParent(stage, false);

            var professor = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(EndingProfessorBuilder.PrefabPath), actors);
            professor.name = "Professor Ending";
            var boki = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(IntroBokiBuilder.PrefabPath), actors);
            boki.name = "Boki Ending";
            boki.transform.SetLocalPositionAndRotation(EndingActing.BokiSpot, Quaternion.identity);

            var scooter = new GameObject("Parked Scooter").transform;
            scooter.SetParent(actors, false);
            scooter.SetLocalPositionAndRotation(new Vector3(-2.7f, 0f, 1.9f), Quaternion.Euler(0f, 70f, 0f));
            AddModel("PROP_Scooter", scooter, Load<Material>(AtlasPath));

            Transform calendar = BuildCalendar(actors, out Transform marker);
            GameObject lights = BuildLights(stage, out Light red);
            EndingOverlay overlay = BuildOverlay(root.transform);

            var input = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>().ToArray();
            var so = new SerializedObject(root.GetComponent<EndingCutscene>());
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("actors").objectReferenceValue = actors.gameObject;
            so.FindProperty("professor").objectReferenceValue = professor.GetComponent<EndingProfessor>();
            so.FindProperty("boki").objectReferenceValue = boki.GetComponent<IntroBoki>();
            so.FindProperty("calendar").objectReferenceValue = calendar;
            so.FindProperty("calendarMarker").objectReferenceValue = marker;
            so.FindProperty("stageLights").objectReferenceValue = lights;
            so.FindProperty("redLight").objectReferenceValue = red;
            so.FindProperty("overlay").objectReferenceValue = overlay;
            so.FindProperty("audioSource").objectReferenceValue = source;
            so.FindProperty("deflate").objectReferenceValue = Load<AudioClip>(SfxFolder + "ending_deflate.wav");
            so.FindProperty("zoom").objectReferenceValue = Load<AudioClip>(SfxFolder + "ending_zoom.wav");
            so.FindProperty("thump").objectReferenceValue = Load<AudioClip>(SfxFolder + "ending_thump.wav");
            so.FindProperty("meow").objectReferenceValue = Load<AudioClip>(SfxFolder + "ending_meow.wav");
            so.FindProperty("lick").objectReferenceValue = Load<AudioClip>(SfxFolder + "ending_lick.wav");
            so.FindProperty("eyePop").objectReferenceValue = Load<AudioClip>(EyePopPath);
            so.FindProperty("skipAction").objectReferenceValue = input.Single(r => r.name == "Skip");
            so.FindProperty("replayAction").objectReferenceValue = input.Single(r => r.name == "Replay");
            so.FindProperty("playOnStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.SaveAsPrefabAsset(root, CutscenePrefab) == null)
                throw new InvalidOperationException("Could not save " + CutscenePrefab + ".");
            Object.DestroyImmediate(root);
        }

        // A wall calendar laid out from CalendarMonth: header, weekday row, the days, and a ring that pops around the date.
        private static Transform BuildCalendar(Transform parent, out Transform marker)
        {
            var month = new CalendarMonth(EndingDate.Year, EndingDate.Month);
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Material paper = Surface("Cal_Paper", new Color(0.93f, 0.9f, 0.8f), 0.05f, false);
            Material red = Surface("Cal_Red", new Color(0.72f, 0.12f, 0.12f), 0.05f, false);
            Material frame = Surface("Cal_Frame", new Color(0.16f, 0.1f, 0.07f), 0.1f, false);
            Material ring = Surface("Cal_Ring", new Color(0.9f, 0.1f, 0.1f), 0f, true);

            var calendar = new GameObject("Wall Calendar").transform;
            calendar.SetParent(parent, false);
            // Turned about Y so its local +X is the viewer's right and +Z points into the wall; the room is on its -Z side.
            calendar.SetLocalPositionAndRotation(EndingActing.DefaultCalendar, Quaternion.Euler(0f, 180f, 0f));

            Box(calendar, cube, frame, "Frame", new Vector3(0f, 0f, 0.014f), new Vector3(EndingCalendarLayout.Width + 0.04f, EndingCalendarLayout.Height + 0.04f, 0.012f));
            Box(calendar, cube, paper, "Card", new Vector3(0f, 0f, 0.006f), new Vector3(EndingCalendarLayout.Width, EndingCalendarLayout.Height, 0.012f));
            Box(calendar, cube, red, "Header", new Vector3(0f, EndingCalendarLayout.TitleY, -0.001f), new Vector3(EndingCalendarLayout.Width, 0.1f, 0.012f));

            Text3D(calendar, "Title", "OCTOBER " + EndingDate.Year, new Vector2(0f, EndingCalendarLayout.TitleY), 0.052f, new Color(0.98f, 0.95f, 0.88f));
            string[] weekdays = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
            for (int column = 0; column < CalendarMonth.Columns; column++)
                Text3D(calendar, weekdays[column], weekdays[column], new Vector2(EndingCalendarLayout.Cell(column, 0).x, EndingCalendarLayout.WeekdayY),
                    0.021f, column >= CalendarMonth.SaturdayColumn ? new Color(0.72f, 0.12f, 0.12f) : new Color(0.2f, 0.18f, 0.2f));
            for (int day = 1; day <= month.DayCount; day++)
                Text3D(calendar, "Day " + day, day.ToString(), EndingCalendarLayout.Cell(month.ColumnOf(day), month.RowOf(day)),
                    day == EndingDate.Day ? 0.044f : 0.036f,
                    month.IsWeekend(day) ? new Color(0.72f, 0.12f, 0.12f) : new Color(0.12f, 0.11f, 0.14f));

            Mesh ringMesh = EndingProfessorBuilder.EnsureMesh("CalendarRing", BuildRing(28, 0.5f, 0.41f));
            var date = EndingCalendarLayout.EndingDateCell();
            var circle = new GameObject("Date Ring", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            circle.SetParent(calendar, false);
            circle.localPosition = new Vector3(date.x, date.y, -0.0055f);
            circle.localScale = new Vector3(0.1f, 0.1f, 1f);
            circle.GetComponent<MeshFilter>().sharedMesh = ringMesh;
            var ringRenderer = circle.GetComponent<MeshRenderer>();
            // The annulus faces +Z; turn it to face the room.
            circle.localRotation = Quaternion.Euler(0f, 180f, 0f);
            ringRenderer.sharedMaterial = ring;
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            marker = circle;
            return calendar;
        }

        // Flat annulus facing +Z.
        private static Mesh BuildRing(int segments, float outer, float inner)
        {
            var triangles = new List<Vector3>();
            for (int i = 0; i < segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                float b = 2f * Mathf.PI * (i + 1) / segments;
                Vector3 o0 = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * outer;
                Vector3 o1 = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * outer;
                Vector3 i0 = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * inner;
                Vector3 i1 = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * inner;
                IntroBokiBuilder.AddTriangle(triangles, o0, o1, i1, Vector3.forward);
                IntroBokiBuilder.AddTriangle(triangles, o0, i1, i0, Vector3.forward);
            }
            return IntroBokiBuilder.FlatMesh(triangles);
        }

        // 3D text with the project's own signs' recipe. size is the height of one line in meters.
        private static void Text3D(Transform parent, string name, string content, Vector2 position, float size, Color color)
        {
            var label = new GameObject(name, typeof(TextMesh));
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(position.x, position.y, -0.0075f);
            var text = label.GetComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = 64;
            text.characterSize = size / (0.1f * text.fontSize);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = text.font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Transform Box(Transform parent, Mesh cube, Material material, string name, Vector3 position, Vector3 scale)
        {
            var box = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<MeshFilter>().sharedMesh = cube;
            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return box.transform;
        }

        // The classroom of the host scene is dim and green; these lights make the ending readable and are on only while it plays.
        private static GameObject BuildLights(Transform stage, out Light red)
        {
            var lights = new GameObject("Stage Lights").transform;
            lights.SetParent(stage, false);
            PointLight(lights, "Key Light", new Vector3(2.2f, 3.2f, 3.2f), new Color(1f, 0.93f, 0.8f), 7f, 14f);
            PointLight(lights, "Fill Light", new Vector3(-1.5f, 2.2f, 4f), new Color(0.65f, 0.78f, 1f), 3.5f, 11f);
            PointLight(lights, "Calendar Light", new Vector3(3.2f, 2.9f, 0.2f), new Color(1f, 0.95f, 0.85f), 3f, 7f);
            red = PointLight(lights, "Red Light", new Vector3(0f, 2.6f, 0.9f), new Color(1f, 0.1f, 0.05f), 20f, 8f);
            return lights.gameObject;
        }

        private static Light PointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        // A neutral classroom: floor, walls, the board on the back wall and the lectern in front of the professor.
        private static void BuildRoom()
        {
            var room = new GameObject("Classroom Set").transform;
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Material floor = Surface("Room_Floor", new Color(0.2f, 0.22f, 0.21f), 0.05f, false);
            Material wall = Surface("Room_Wall", new Color(0.3f, 0.34f, 0.32f), 0.05f, false);
            Material board = Surface("Room_Board", new Color(0.04f, 0.08f, 0.06f), 0.05f, false);
            Box(room, cube, floor, "Floor", new Vector3(0f, -0.1f, 3f), new Vector3(16f, 0.2f, 16f));
            Box(room, cube, wall, "Back Wall", new Vector3(0f, 2.2f, -1.78f), new Vector3(16f, 4.6f, 0.24f));
            Box(room, cube, wall, "Left Wall", new Vector3(-8f, 2.2f, 3f), new Vector3(0.24f, 4.6f, 16f));
            Box(room, cube, wall, "Right Wall", new Vector3(8f, 2.2f, 3f), new Vector3(0.24f, 4.6f, 16f));
            Box(room, cube, wall, "Front Wall", new Vector3(0f, 2.2f, 11f), new Vector3(16f, 4.6f, 0.24f));
            Box(room, cube, board, "Board", new Vector3(0f, 2.3f, -1.72f), new Vector3(10f, 2f, 0.12f));

            // The lectern in front of the professor: base, body and a slanted reading surface.
            Material wood = Surface("Room_Wood", new Color(0.3f, 0.19f, 0.1f), 0.1f, false);
            Box(room, cube, wood, "Lectern Base", new Vector3(0f, 0.045f, 0.45f), new Vector3(0.72f, 0.09f, 0.58f));
            Box(room, cube, wood, "Lectern Body", new Vector3(0f, 0.55f, 0.45f), new Vector3(0.6f, 0.92f, 0.44f));
            Box(room, cube, wood, "Lectern Top", new Vector3(0f, 1.05f, 0.44f), new Vector3(0.7f, 0.05f, 0.6f)).localRotation = Quaternion.Euler(-9f, 0f, 0f);
        }

        // Dim green like the V1 classroom, so the stage lights are judged against the same dark.
        private static void ConfigureRoom(Light sun)
        {
            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 0.18f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50f, 330f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.42f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.03f, 0.07f, 0.05f);
            RenderSettings.fogDensity = 0.015f;
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

        private static EndingOverlay BuildOverlay(Transform parent)
        {
            var canvasObject = new GameObject("Ending Overlay", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            var letterbox = Rect(root, "Letterbox", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Fill(Rect(letterbox, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 110f)), Color.black);
            Fill(Rect(letterbox, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 110f)), Color.black);

            // A soft band behind the subtitle keeps it readable over bright closeups.
            var backing = Fill(Rect(root, "Subtitle Backing", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(0f, 96f)), new Color(0f, 0f, 0f, 0.5f));
            var subtitle = Label(Rect(root, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(1400f, 120f)),
                "Text", string.Empty, 38, Paper, TextAnchor.LowerCenter);
            subtitle.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            var hint = Label(Rect(root, "Skip Hint", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(400f, 36f)),
                "Text", "ENTER: SKIP", 22, new Color(Paper.r, Paper.g, Paper.b, 0.7f), TextAnchor.LowerRight);

            // The MEOW! burst: a yellow eight-point star behind outlined comic lettering.
            var burst = Rect(root, "Meow Burst", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 190f), new Vector2(620f, 420f));
            var starA = Fill(Rect(burst, "Star A", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 330f)), new Color(1f, 0.85f, 0.2f));
            starA.gameObject.AddComponent<Outline>().effectDistance = new Vector2(6f, -6f);
            var starB = Fill(Rect(burst, "Star B", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 330f)), new Color(1f, 0.85f, 0.2f));
            starB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var meow = Label(Rect(burst, "Meow Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                "Text", "MEOW!", 150, Color.white, TextAnchor.MiddleCenter);
            meow.fontStyle = FontStyle.Bold;
            var outline = meow.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.08f, 0.35f);
            outline.effectDistance = new Vector2(7f, -7f);
            burst.gameObject.SetActive(false);

            var flash = Fill(Rect(root, "Flash", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), new Color(1f, 1f, 1f, 0f));
            var fade = Fill(Rect(root, "Fade", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Color.black);

            var card = Rect(root, "End Card", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Label(card, "Text", "YOU SURVIVED. YOU WERE NEVER LATE.", 52, Paper, TextAnchor.MiddleCenter);

            var overlay = canvasObject.AddComponent<EndingOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("fade").objectReferenceValue = fade;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("letterbox").objectReferenceValue = letterbox.gameObject;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("subtitleBacking").objectReferenceValue = backing.gameObject;
            so.FindProperty("skipHint").objectReferenceValue = hint.transform.parent.gameObject;
            so.FindProperty("meowBurst").objectReferenceValue = burst;
            so.FindProperty("endCard").objectReferenceValue = group;
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

        private static Material Surface(string name, Color color, float smoothness, bool unlit)
            => IntroBokiBuilder.EnsureMaterial(Folder, name, color, smoothness, unlit);

        private static void EnsureAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Cutscenes")) AssetDatabase.CreateFolder("Assets", "Cutscenes");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Cutscenes", "Ending");
            EndingProfessorBuilder.EnsureFolders();

            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath) == null)
            {
                var input = ScriptableObject.CreateInstance<InputActionAsset>();
                var map = input.AddActionMap("Ending");
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

            foreach (string clip in Clips) ConfigureClip(SfxFolder + clip + ".wav");
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureClip(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Missing " + path + "; run music/generate_ending_sfx.py.");
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

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing " + typeof(T).Name + " at " + path + ".");
            return asset;
        }

        private static void RequireNotAdopted()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != ScenePath && AssetDatabase.GetDependencies(path, false).Contains(CutscenePrefab))
                    throw new InvalidOperationException(path + " already uses " + CutscenePrefab
                        + "; a rebuild would break its overrides. Edit the prefab directly instead.");
            }
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
