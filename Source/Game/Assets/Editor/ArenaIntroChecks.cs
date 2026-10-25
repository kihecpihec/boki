using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Boki.ArenaIntro;
using Boki.Battery;
using Boki.Combat;
using Boki.Core;
using Boki.Intro;
using Boki.Player;
using Boki.Routes;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Headless Play Mode checks of the first-arena scare: the real trigger, the pause it holds, the restored scene, skipping,
    // the arena flooding afterwards and review frames of every beat. "Preview" runs ArenaIntro_Preview; "Pohorje" opens
    // V1_Pohorje, adds the cutscene in memory when it is missing and never saves the scene. Run without -nographics: frames need the GPU.
    [InitializeOnLoad]
    public static class ArenaIntroChecks
    {
        private const string Key = "Boki.ArenaIntroChecks";
        private const string FramesVariable = "BOKI_ARENA_INTRO_FRAMES";
        private const float MinimumLaneShare = 0.6f;

        [Serializable] private sealed class Saved { public string[] paths; public bool[] loaded, active; public int background, editorInput; }

        private static readonly (int shot, float progress)[] Frames =
        {
            (0, 0f), (0, 0.6f), (0, 1f),
            (1, 0.12f), (1, 0.45f), (1, 0.9f),
            (2, 0.15f), (2, 0.4f), (2, 0.7f), (2, 1f),
            (3, 0.1f), (3, 0.55f), (3, 1f),
            (4, 0.08f), (4, 0.25f), (4, 0.4f), (4, 0.55f), (4, 0.7f), (4, 0.85f), (4, 1f),
            (5, 0.1f), (5, 0.35f), (5, 0.5f), (5, 1f),
        };

        private static int stage;
        private static bool pohorje;
        private static double started, nextTick, stageStarted;
        private static string failure;
        private static ArenaIntroCutscene cutscene;
        private static PlayerHealth player;
        private static Camera view;
        private static Vector3 restPosition;
        private static Quaternion restRotation, restRiderRotation;
        private static float restFov;
        private static int baselineEnemies;
        private static Vector3 entrance;
        private static bool enterDown;
        private static string frameFolder;

        static ArenaIntroChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
            Application.logMessageReceived += Logged;
        }

        [MenuItem("Boki/Arena Intro/Run Preview Checks")]
        public static void RunPreview() => Run(false);

        [MenuItem("Boki/Arena Intro/Run Pohorje Checks")]
        public static void RunPohorje() => Run(true);

        private static void Run(bool inPohorje)
        {
            IntroPreviewSetup.RequireIdle();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var saved = new Saved { paths = new string[setup.Length], loaded = new bool[setup.Length], active = new bool[setup.Length] };
            saved.background = (int)InputSystem.settings.backgroundBehavior;
            saved.editorInput = (int)InputSystem.settings.editorInputBehaviorInPlayMode;
            for (int i = 0; i < setup.Length; i++) { saved.paths[i] = setup[i].path; saved.loaded[i] = setup[i].isLoaded; saved.active[i] = setup[i].isActive; }
            SessionState.SetString(Key + ".Saved", JsonUtility.ToJson(saved));
            SessionState.SetBool(Key + ".Pohorje", inPohorje);
            if (inPohorje)
            {
                EditorSceneManager.OpenScene(V1SceneSetup.Scenes[0]);
                if (Object.FindAnyObjectByType<ArenaIntroCutscene>() == null) ArenaIntroPreviewSetup.Adopt(false);
            }
            else EditorSceneManager.OpenScene(ArenaIntroPreviewSetup.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0;
                failure = null;
                cutscene = null;
                pohorje = SessionState.GetBool(Key + ".Pohorje", false);
                started = stageStarted = EditorApplication.timeSinceStartup;
                nextTick = started + 0.5;
                string name = pohorje ? "arena-intro-pohorje" : "arena-intro-preview";
                frameFolder = Environment.GetEnvironmentVariable(FramesVariable);
                if (string.IsNullOrEmpty(frameFolder)) frameFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/" + name + "/frames"));
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            var saved = JsonUtility.FromJson<Saved>(SessionState.GetString(Key + ".Saved", ""));
            InputSystem.settings.backgroundBehavior = (InputSettings.BackgroundBehavior)saved.background;
            InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)saved.editorInput;
            bool restore = saved.paths.Length > 0;
            foreach (string path in saved.paths) restore &= !string.IsNullOrEmpty(path);
            if (restore)
            {
                var setup = new SceneSetup[saved.paths.Length];
                for (int i = 0; i < setup.Length; i++) setup[i] = new SceneSetup { path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.active[i] };
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string where = pohorje ? "POHORJE" : "PREVIEW";
            Debug.Log(failure == null
                ? "ARENA_INTRO_" + where + "_CHECKS_PASSED"
                : "ARENA_INTRO_" + where + "_CHECKS_FAILED: " + failure);
            if (Application.isBatchMode) EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void Logged(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || failure != null) return;
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !trace.Contains("UnityEditor.Search.SearchDatabase"))
                failure = message;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (failure != null) { EditorApplication.ExitPlaymode(); return; }
            if (EditorApplication.timeSinceStartup - started > 240) { Fail("Timeout at stage " + stage); return; }
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.1;
            try { Step(); }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        private static void Fail(string message)
        {
            failure = failure ?? message;
            EditorApplication.ExitPlaymode();
        }

        private static void Check(bool condition, string what)
        {
            if (!condition) throw new Exception("FAILED: " + what);
            Debug.Log("check ok: " + what);
        }

        // A wait that is still running: only fail when it has run out of time.
        private static bool StillWaiting(double limit, string what)
        {
            if (Waited > limit) throw new Exception("FAILED: " + what);
            return true;
        }

        private static void Goto(int next)
        {
            stage = next;
            stageStarted = EditorApplication.timeSinceStartup;
        }

        private static double Waited => EditorApplication.timeSinceStartup - stageStarted;

        private static void Step()
        {
            RunSession session = RunSession.Instance;
            Scene scene = SceneManager.GetActiveScene();
            if (session == null || session.IsLoading) return;
            if (cutscene == null)
            {
                cutscene = V1SceneSetup.Find<ArenaIntroCutscene>(scene);
                player = V1SceneSetup.Find<PlayerHealth>(scene);
                view = Camera.main;
                if (cutscene == null || player == null || view == null) return;
            }
            // V1_Pohorje opens with the Bellevue intro, which holds gameplay until it ends; skip it like a player.
            if (pohorje && stage == 0 && (session.State.Pause.Contains(PauseReason.ScriptedSequence) || Waited < 1.5))
            {
                if (session.State.Pause.Contains(PauseReason.ScriptedSequence)) PressEnter();
                return;
            }
            switch (stage)
            {
                case 0:
                    Check(!cutscene.IsPlaying && session.IsGameplayActive, "Starts idle with gameplay running");
                    Check(!cutscene.transform.Find("Arena Intro Stage/Actors").gameObject.activeSelf, "Actors are hidden until the scene plays");
                    ReleaseEnter();
                    var laser = player.GetComponent<AutoLaser>();
                    if (laser != null) laser.enabled = false;
                    if (pohorje) { SelectRoute(scene, RouteChoice.A); Goto(1); }
                    else { Teleport(new Vector3(0f, 0.05f, -24f)); Goto(2); }
                    break;

                case 1:
                    if (Waited < 0.8) return;
                    Check(session.State.Choice(RouteLevel.Forest) == RouteChoice.A, "Route A chosen at its physical gate");
                    Goto(2);
                    break;

                case 2:
                    if (Waited < 1.0) return;
                    baselineEnemies = Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude).Length;
                    Check(!cutscene.IsPlaying, "Nothing plays before Boki enters the arena");
                    // Wait just outside first, so the follow camera has settled when he rides in, as it would on a real approach.
                    Teleport(EntrancePoints(scene, out entrance));
                    Goto(3);
                    break;

                case 3:
                    if (Waited < 1.5) return;
                    Check(!cutscene.IsPlaying, "Waiting at the arena's edge does not start the scene");
                    Teleport(entrance);
                    Goto(10);
                    break;

                case 10:
                    if (!cutscene.IsPlaying && StillWaiting(3, "Entering the arena starts the scene")) return;
                    ReadRest();
                    Check(session.State.Pause.Contains(PauseReason.ScriptedSequence) && Time.timeScale == 0f && !session.IsGameplayActive,
                        "The scene holds the scripted-sequence pause");
                    Check(!player.GetComponent<ScooterController>().enabled, "Scooter controls are off while it plays");
                    Check(Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude).Length == baselineEnemies,
                        "No enemy is placed before the creeper has been shown");
                    CaptureFrames();
                    Goto(11);
                    break;

                case 11:
                    if (cutscene.IsPlaying && StillWaiting(30, "The scene finishes by itself")) return;
                    CheckResumed(session, "after playing to the end");
                    Goto(pohorje ? 12 : 14);
                    break;

                case 12:
                    int enemies = Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude).Length;
                    if (enemies <= baselineEnemies && StillWaiting(5, "The arena floods once the scene is over")) return;
                    Debug.Log("check ok: The arena floods once the scene is over (" + (enemies - baselineEnemies) + " enemies after " + Waited.ToString("0.0") + " s)");
                    Goto(13);
                    break;

                case 13:
                    ProbeLanes(scene);
                    Goto(14);
                    break;

                case 14:
                    cutscene.Play();
                    ReadRest();
                    Check(cutscene.IsPlaying && Time.timeScale == 0f, "A second play holds the pause again");
                    cutscene.Skip();
                    CheckResumed(session, "after skipping through the API");
                    Goto(15);
                    break;

                case 15:
                    cutscene.Play();
                    ReadRest();
                    Check(cutscene.IsPlaying, "Playing again for the keyboard skip");
                    PressEnter();
                    Goto(16);
                    break;

                case 16:
                    if (cutscene.IsPlaying && StillWaiting(2, "Enter skips the scene")) return;
                    ReleaseEnter();
                    CheckResumed(session, "after skipping with Enter");
                    Goto(17);
                    break;

                case 17:
                    if (Waited < 0.5) return;
                    Check(!cutscene.IsPlaying, "Enter after the end does nothing");
                    EditorApplication.ExitPlaymode();
                    break;
            }
        }

        // What the scene captured when it started: the gameplay camera and the way Boki faced, which it must give back.
        private static void ReadRest()
        {
            restPosition = Private<Vector3>("restCameraPosition");
            restRotation = Private<Quaternion>("restCameraRotation");
            restRiderRotation = Private<Quaternion>("restRiderRotation");
            restFov = Private<float>("baseFieldOfView");
        }

        private static T Private<T>(string field)
            => (T)typeof(ArenaIntroCutscene).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cutscene);

        // Everything the scene touched is back as it was, with the controls on and gameplay running.
        private static void CheckResumed(RunSession session, string when)
        {
            Check(!session.State.Pause.Contains(PauseReason.ScriptedSequence) && Mathf.Approximately(Time.timeScale, 1f) && session.IsGameplayActive,
                "Gameplay resumes " + when);
            Check(player.GetComponent<ScooterController>().enabled && Object.FindAnyObjectByType<ThirdPersonCamera>().enabled,
                "Controls are back on " + when);
            Check(player.transform.Find("Visuals").gameObject.activeSelf, "The rider is visible again " + when);
            Check(!cutscene.transform.Find("Arena Intro Stage/Actors").gameObject.activeSelf, "The actors are hidden again " + when);
            Check(Quaternion.Angle(player.transform.rotation, restRiderRotation) < 1f, "Boki faces the way he did " + when);
            // The follow camera takes the pose back over on the next frame; compare after it has.
            float moved = Vector3.Distance(view.transform.position, restPosition);
            float turned = Quaternion.Angle(view.transform.rotation, restRotation);
            float zoomed = Mathf.Abs(view.fieldOfView - restFov);
            Check(moved < 0.1f && turned < 2f && zoomed < 0.5f,
                $"The camera is back on its gameplay pose {when} (moved {moved:0.00} m, turned {turned:0.0} deg, fov {zoomed:0.0})");
        }

        // Renders every beat to PNGs with the real scene lights and checks the composition: faces and the creeper are in frame.
        private static void CaptureFrames()
        {
            Directory.CreateDirectory(frameFolder);
            var canvas = cutscene.GetComponentInChildren<ArenaIntroOverlay>(true).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = view;
            canvas.planeDistance = view.nearClipPlane + 0.01f;
            Transform face = cutscene.GetComponentInChildren<IntroBoki>(true).Face;
            Transform creeper = cutscene.transform.Find("Arena Intro Stage/Actors/Creeper");
            Transform foot = creeper.GetComponentsInChildren<Transform>(true).First(t => t.name == "foot.L");
            float footLow = float.MaxValue, footHigh = float.MinValue;
            var texture = new RenderTexture(1280, 720, 24);
            var misses = new List<string>();
            view.targetTexture = texture;
            cutscene.ShowFrame(Frames[0].shot, Frames[0].progress);
            view.Render(); // warm-up: the first render after load lacks textures and lights
            foreach (var (shot, progress) in Frames)
            {
                cutscene.ShowFrame(shot, progress);
                Canvas.ForceUpdateCanvases();
                view.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                File.WriteAllBytes(Path.Combine(frameFolder, $"shot{shot}-{Mathf.RoundToInt(progress * 100):000}.png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);

                string name = $"shot {shot} at {progress:0.00}";
                if ((shot == ArenaIntroActing.Startle || shot == ArenaIntroActing.Line) && !InFrame(face.position, 0.25f, 0.75f, 0.3f, 0.8f))
                    misses.Add(name + ": the face is out of frame");
                if (shot == ArenaIntroActing.Creep)
                {
                    float stride = creeper.InverseTransformPoint(foot.position).z;
                    footLow = Mathf.Min(footLow, stride);
                    footHigh = Mathf.Max(footHigh, stride);
                }
                if ((shot == ArenaIntroActing.Creep && progress >= 0.4f || shot == ArenaIntroActing.Lunge)
                    && !InFrame(creeper.position + Vector3.up * 1.1f, 0.05f, 0.95f, 0.05f, 0.95f))
                    misses.Add(name + ": the creeper is out of frame");
            }
            RenderTexture.active = null;
            view.targetTexture = null;
            texture.Release();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Debug.Log("frames written to " + frameFolder);
            Check(footHigh - footLow > 0.2f, $"The creeper's feet move through its stride ({footHigh - footLow:0.00} m of travel across the creep frames)");
            Check(misses.Count == 0, "The faces and the creeper are in frame in every shot" + (misses.Count > 0 ? ": " + string.Join("; ", misses) : ""));
        }

        private static bool InFrame(Vector3 world, float minX, float maxX, float minY, float maxY)
        {
            Vector3 point = view.WorldToViewportPoint(world);
            return point.z > 0f && point.x > minX && point.x < maxX && point.y > minY && point.y < maxY;
        }

        // The enemy needs a clear lane wherever Boki might come in: try a grid of positions across the occupied arena.
        private static void ProbeLanes(Scene scene)
        {
            BatteryCombatSpace space = V1SceneSetup.All<BatteryCombatSpace>(scene).Find(s => s.gameObject.activeInHierarchy);
            Bounds bounds = space.GetComponent<BoxCollider>().bounds;
            const int Steps = 7;
            int ground = 0, lanes = 0;
            var blocked = new List<string>();
            for (int i = 0; i < Steps; i++)
                for (int j = 0; j < Steps; j++)
                {
                    float x = Mathf.Lerp(bounds.min.x, bounds.max.x, (i + 0.5f) / Steps);
                    float z = Mathf.Lerp(bounds.min.z, bounds.max.z, (j + 0.5f) / Steps);
                    if (!Physics.Raycast(new Vector3(x, bounds.max.y + 5f, z), Vector3.down, out RaycastHit hit, 80f, 1, QueryTriggerInteraction.Ignore)) continue;
                    ground++;
                    Teleport(hit.point + Vector3.up * 0.1f);
                    cutscene.Play();
                    if (cutscene.IsPlaying) lanes++;
                    else blocked.Add($"({x:0},{z:0})");
                    cutscene.Skip();
                }
            Debug.Log($"lane probe: {lanes} of {ground} arena positions found a clear lane; none at {string.Join(" ", blocked)}");
            Check(ground > 0 && lanes >= MinimumLaneShare * ground, $"At least {MinimumLaneShare:P0} of arena positions find a clear lane ({lanes} of {ground})");
        }

        // Where a rider coming from the route's gate first meets the occupied arena volume: a point just outside it (returned)
        // and one just inside it.
        private static Vector3 EntrancePoints(Scene scene, out Vector3 inside)
        {
            if (!pohorje)
            {
                inside = new Vector3(0f, 0.05f, -6f);
                return new Vector3(0f, 0.05f, -12f);
            }
            BatteryCombatSpace space = V1SceneSetup.All<BatteryCombatSpace>(scene).Find(s => s.gameObject.activeInHierarchy);
            Bounds bounds = space.GetComponent<BoxCollider>().bounds;
            Vector3 edge = bounds.ClosestPoint(player.transform.position);
            Vector3 toCenter = Vector3.ProjectOnPlane(bounds.center - edge, Vector3.up).normalized;
            inside = OnGround(edge + toCenter * 1.5f);
            return OnGround(edge - toCenter * 3f);
        }

        private static Vector3 OnGround(Vector3 point)
        {
            Check(Physics.Raycast(point + Vector3.up * 30f, Vector3.down, out RaycastHit ground, 80f, 1, QueryTriggerInteraction.Ignore),
                "The arena approach has ground");
            return ground.point + Vector3.up * 0.1f;
        }

        private static void SelectRoute(Scene scene, RouteChoice choice)
        {
            foreach (RouteGate gate in V1SceneSetup.All<RouteGate>(scene))
                if (new SerializedObject(gate).FindProperty("choice").enumValueIndex == (int)choice)
                {
                    Teleport(gate.GetComponent<BoxCollider>().bounds.center - Vector3.up * 0.8f);
                    return;
                }
            throw new Exception("Missing gate " + choice);
        }

        private static void Teleport(Vector3 point)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = point;
            controller.enabled = true;
            Physics.SyncTransforms();
            controller.Move(Vector3.forward * 0.01f);
        }

        // Queue the event and let the player loop read it; reading it by hand swallows the press.
        private static void PressEnter()
        {
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(Keyboard.current);
            enterDown = !enterDown;
            InputSystem.QueueStateEvent(Keyboard.current, enterDown ? new KeyboardState(UnityEngine.InputSystem.Key.Enter) : new KeyboardState());
        }

        private static void ReleaseEnter()
        {
            if (enterDown) PressEnter();
        }
    }
}
