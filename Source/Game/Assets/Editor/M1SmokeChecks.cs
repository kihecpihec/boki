using System;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Boki.Editor
{
    // Integration smoke checks; movement feel and keyboard/mouse behavior need Play Mode review.
    [InitializeOnLoad]
    public static class M1SmokeChecks
    {
        private const string RunningKey = "Boki.M1.SmokeRunning";
        private static int frames;
        private static bool failed;

        static M1SmokeChecks()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            Application.logMessageReceived += LogReceived;
            EditorApplication.update += Tick;
        }

        [MenuItem("Boki/Run M1 Smoke Checks")]
        public static void Run()
        {
            // Let Editor startup callbacks finish before measuring errors from the scene.
            EditorApplication.delayCall += () => EditorApplication.delayCall += RunChecks;
        }

        private static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running smoke checks.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your open scenes before running smoke checks.");

            EditorSceneManager.OpenScene("Assets/Scenes/Gameplay_Test.unity");
            var controller = UnityEngine.Object.FindFirstObjectByType<ScooterController>();
            var camera = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>();
            Require(controller != null && camera != null, "Player and follow camera must exist.");
            CheckReferences(controller, "movementCamera", "moveAction", "jumpAction", "boostAction");
            CheckReferences(camera, "followPivot", "lookAction");
            Require(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1,
                "Exactly one scene camera is required.");
            Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1,
                "Exactly one Audio Listener is required.");
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Require(input.FindAction("Player/Boost") != null && input.FindAction("Player/Sprint") == null,
                "Boost must replace the template Sprint action.");

            SessionState.SetBool(RunningKey, true);
            failed = false;
            frames = 0;
            EditorApplication.EnterPlaymode();
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) frames = 0;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(RunningKey, false);
                Debug.Log(failed ? "M1_SMOKE_FAILED" : "M1_SMOKE_PASSED: references, grounding, wall collision, camera obstruction, and 120 Play Mode frames.");
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        private static void LogReceived(string message, string trace, LogType type)
        {
            if (SessionState.GetBool(RunningKey, false) && (type == LogType.Error || type == LogType.Exception))
                failed = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying
                || EditorApplication.isCompiling) return;
            try
            {
                frames++;
                var player = UnityEngine.Object.FindFirstObjectByType<ScooterController>();
                Require(player != null && player.enabled, "Movement component must remain enabled.");
                var controller = player.GetComponent<CharacterController>();
                if (frames == 30)
                {
                    Require(player.transform.position.y >= -0.1f, "Player must not fall through the floor.");
                    controller.Move(Vector3.down * 2f);
                    Require(controller.isGrounded, "Controller must settle on the floor.");
                    controller.Move(Vector3.right * 50f);
                    Require(player.transform.position.x < 15.5f, "Arena wall must stop the controller.");
                    controller.enabled = false;
                    player.transform.position = new Vector3(0f, 0.05f, -14.5f);
                    controller.enabled = true;
                    Physics.SyncTransforms();
                }
                if (frames == 60)
                {
                    var camera = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>();
                    Require(camera != null && camera.enabled, "Follow camera must remain enabled.");
                    float distance = Vector3.Distance(camera.transform.position, player.transform.Find("CameraPivot").position);
                    Require(distance < 2f, "Camera must shorten its distance near the south wall.");
                }
                if (frames >= 120) EditorApplication.ExitPlaymode();
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
                EditorApplication.ExitPlaymode();
            }
        }

        private static void CheckReferences(UnityEngine.Object target, params string[] names)
        {
            var serialized = new SerializedObject(target);
            foreach (string name in names)
                Require(serialized.FindProperty(name).objectReferenceValue != null, "Missing Inspector reference: " + name);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
