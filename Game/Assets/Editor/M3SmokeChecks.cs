using System;
using System.Reflection;
using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.Routes;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    [InitializeOnLoad]
    public static class M3SmokeChecks
    {
        private const string Key = "Boki.M3.SmokeRunning";
        private static int stage, cycles;
        private static bool failed;
        private static double started, waitUntil;
        private static RunSession original;
        private static PlayerHealth player;
        private static RouteManager manager;
        private static RouteChoice forestChoice;
        private static Mouse uiMouse;
        private static Vector2 uiPosition;
        private static int uiFrame;
        private static InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private static InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;
        static M3SmokeChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += ModeChanged;
            Application.logMessageReceived += (message, trace, type) =>
            { if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) failed = true; };
        }
        [MenuItem("Boki/Run M3 Smoke Checks")]
        public static void Run() => EditorApplication.delayCall += () => EditorApplication.delayCall += Begin;
        private static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Save scenes first.");
            // Exercise the invalid direct-entry path before the normal run.
            EditorSceneManager.OpenScene(RunSession.CityScene);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        private static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            { stage = cycles = 0; failed = false; started = EditorApplication.timeSinceStartup; }
            if (mode == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                Debug.Log(failed ? "M3_SMOKE_FAILED" : "M3_SMOKE_PASSED: direct City entry, real UI mouse click/cursor handling, physical gates/triggers, both Forest choices, HP carry, three City deaths/retries, singleton lifetime.");
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                Require(EditorApplication.timeSinceStartup - started < 90, "M3 smoke timeout at stage " + stage);
                if (stage == 0)
                {
                    manager = Object.FindFirstObjectByType<RouteManager>();
                    Require(!manager.IsReady && !manager.Select(RouteChoice.A), "Direct City entry must not fabricate a Forest choice.");
                    original = RunSession.Instance;
                    new GameObject("Duplicate Session Probe").AddComponent<RunSession>();
                    BeginStartNewRunClick(); stage = -3;
                }
                else if (stage == -3 && Time.frameCount > uiFrame)
                {
                    InputSystem.QueueStateEvent(uiMouse, new MouseState { position = uiPosition }.WithButton(MouseButton.Left));
                    InputSystem.Update();
                    var camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
                    typeof(ThirdPersonCamera).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(camera, null);
                    Require(Cursor.lockState == CursorLockMode.None, "Pressing UI must not capture the cursor.");
                    EventSystem.current.currentInputModule.Process();
                    uiFrame = Time.frameCount; stage = -2;
                }
                else if (stage == -2 && Time.frameCount > uiFrame)
                {
                    InputSystem.QueueStateEvent(uiMouse, new MouseState { position = uiPosition });
                    InputSystem.Update();
                    EventSystem.current.currentInputModule.Process();
                    waitUntil = Time.realtimeSinceStartupAsDouble + 0.5;
                    stage = -1;
                }
                else if (stage == -1 && Time.realtimeSinceStartupAsDouble >= waitUntil)
                {
                    Require(SceneManager.GetActiveScene().path == RunSession.ForestScene,
                        "Actual UI press/release must load Forest through the saved button binding.");
                    EndUIClick(); stage = 1;
                }
                else if (stage == 1 && SceneManager.GetActiveScene().path == RunSession.ForestScene)
                {
                    Prepare();
                    Require(player.Current == 100 && original.State.Choice(RouteLevel.Forest) == RouteChoice.None, "Fresh run must reset HP and choices.");
                    Require(manager.Gate(RouteChoice.C) == GateState.PersistentLock, "Forest C must remain locked.");
                    // Actual CharacterController movement against the locked C barrier.
                    Teleport(new Vector3(12, 0.1f, -2));
                    player.GetComponent<CharacterController>().Move(Vector3.forward * 4);
                    Require(player.transform.position.z < 0, "Locked gate must physically block the controller.");
                    Require(!manager.Advance(), "Unchosen exit must reject transition.");
                    forestChoice = cycles % 2 == 0 ? RouteChoice.A : RouteChoice.B;
                    float x = forestChoice == RouteChoice.A ? -12 : 0;
                    Teleport(new Vector3(x, 0.1f, 1));
                    player.GetComponent<CharacterController>().Move(Vector3.forward * 1.2f);
                    waitUntil = Time.realtimeSinceStartupAsDouble + 0.2; stage = 2;
                }
                else if (stage == 2 && Time.realtimeSinceStartupAsDouble >= waitUntil)
                {
                    Require(original.State.Choice(RouteLevel.Forest) == forestChoice, "Physical trigger must select the Forest route.");
                    Require(manager.Gate(forestChoice) == GateState.Selected, "Selected gate must remain open.");
                    Require(!manager.Select(forestChoice == RouteChoice.A ? RouteChoice.B : RouteChoice.A), "Alternatives cannot be selected.");
                    player.TakeDamage(27);
                    Require(player.Current == 73 && original.State.Health.Current == 73, "HP must have one session authority.");
                    Teleport(new Vector3(0, 0.1f, 28.5f));
                    player.GetComponent<CharacterController>().Move(Vector3.forward);
                    stage = 3;
                }
                else if (stage == 3 && SceneManager.GetActiveScene().path == RunSession.CityScene)
                {
                    Prepare();
                    Require(player.Current == 73, "City must retain damaged HP.");
                    Require(manager.Gate(RouteChoice.A) == GateState.Available, "City A must be open.");
                    Require(manager.Gate(RouteChoice.B) == (forestChoice == RouteChoice.A ? GateState.Available : GateState.RouteLock), "Forest choice must filter City B.");
                    Require(manager.Gate(RouteChoice.C) == (forestChoice == RouteChoice.A ? GateState.RouteLock : GateState.PersistentLock), "City C lock priority must match the Forest choice.");
                    Require(manager.Select(RouteChoice.A), "City choice must succeed.");
                    var combat = Object.FindFirstObjectByType<CombatSession>();
                    player.TakeDamage(int.MaxValue);
                    Require(combat.IsDead && Object.FindFirstObjectByType<CombatView>().DeathVisible, "Death must show UI.");
                    Require(!manager.Select(RouteChoice.B) && !manager.Advance(), "Dead players cannot choose or exit.");
                    Require(!player.GetComponent<ScooterController>().enabled && !Object.FindFirstObjectByType<ThirdPersonCamera>().enabled, "Death must stop controls.");
                    foreach (var enemy in Object.FindObjectsByType<MeleeEnemy>(FindObjectsSortMode.None)) Require(!enemy.enabled, "Death must stop enemies.");
                    var retryButton = Object.FindFirstObjectByType<CombatView>().RetryButton;
                    retryButton.onClick.Invoke();
                    var freshHealth = original.State.Health;
                    combat.Retry(); // A stale repeated request must not reset/load twice.
                    Require(ReferenceEquals(freshHealth, original.State.Health), "Repeated Retry must not create another run.");
                    cycles++; stage = 4;
                }
                else if (stage == 4 && SceneManager.GetActiveScene().path == RunSession.ForestScene)
                {
                    Prepare();
                    Require(player.Current == 100 && original.State.Choice(RouteLevel.City) == RouteChoice.None, "Retry must clear City and HP.");
                    Require(!Object.FindFirstObjectByType<CombatView>().DeathVisible, "Retry must hide death UI.");
                    if (cycles >= 3) EditorApplication.ExitPlaymode(); else stage = 1;
                }
            }
            catch (Exception error) { failed = true; EndUIClick(); Debug.LogException(error); EditorApplication.ExitPlaymode(); }
        }
        private static void BeginStartNewRunClick()
        {
            Button button = null;
            foreach (var candidate in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (candidate.name == "Start New Run") button = candidate;
            Require(button != null, "Start New Run button must exist.");
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            uiPosition = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(ThirdPersonCamera).GetField("hasFocus", flags).SetValue(camera, true);
            Cursor.lockState = CursorLockMode.None;
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            uiMouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(uiMouse, new MouseState { position = uiPosition });
            InputSystem.Update();
            EventSystem.current.currentInputModule.Process();
            Require((bool)typeof(ThirdPersonCamera).GetMethod("IsPointerOverUI", flags).Invoke(camera, null),
                "Camera must detect the button through a current-position UI raycast.");
            uiFrame = Time.frameCount;
        }
        private static void EndUIClick()
        {
            if (uiMouse == null) return;
            InputSystem.RemoveDevice(uiMouse);
            uiMouse = null;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
        }

        private static void Prepare()
        {
            player = Object.FindFirstObjectByType<PlayerHealth>(); manager = Object.FindFirstObjectByType<RouteManager>();
            Require(RunSession.Instance == original && Object.FindObjectsByType<RunSession>(FindObjectsSortMode.None).Length == 1, "Exactly one original session must survive.");
            Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "One camera required.");
            Require(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "One listener required.");
            player.GetComponent<ScooterController>().enabled = false;
            player.GetComponent<AutoLaser>().enabled = false;
            foreach (var enemy in Object.FindObjectsByType<MeleeEnemy>(FindObjectsSortMode.None)) enemy.enabled = false;
        }
        private static void Teleport(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.position = position; controller.enabled = true; Physics.SyncTransforms();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
