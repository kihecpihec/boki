using System;
using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Changes only the in-memory model-test fixture; never saves a scene or prefab.
    [InitializeOnLoad]
    public static class ProfessorChallengeChecks
    {
        private const string Key = "Boki.ProfessorChallenge.Checks";
        [Serializable] private sealed class Setup
        {
            public string[] paths;
            public bool[] loaded;
            public bool[] active;
        }
        private static int frames;
        private static bool failed;

        static ProfessorChallengeChecks()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update += Tick;
        }

        [MenuItem("Boki/Enemy Models/Run Professor Challenge Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
            var saved = new Setup { paths = new string[scenes.Length], loaded = new bool[scenes.Length], active = new bool[scenes.Length] };
            for (int i = 0; i < scenes.Length; i++)
            {
                saved.paths[i] = scenes[i].path;
                saved.loaded[i] = scenes[i].isLoaded;
                saved.active[i] = scenes[i].isActive;
            }
            SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(saved));
            EditorSceneManager.OpenScene("Assets/Scenes/Player_Model_Test.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) { frames = 0; failed = false; }
            if (change != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            var saved = JsonUtility.FromJson<Setup>(SessionState.GetString(Key + ".Scenes", ""));
            if (saved != null && saved.paths.Length > 0 && !string.IsNullOrEmpty(saved.paths[0]))
            {
                var scenes = new SceneSetup[saved.paths.Length];
                for (int i = 0; i < scenes.Length; i++)
                    scenes[i] = new SceneSetup { path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.active[i] };
                EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.EraseString(Key + ".Scenes");
            Debug.Log(failed ? "PROFESSOR_CHALLENGE_FAILED" : "PROFESSOR_CHALLENGE_PASSED");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || ++frames != 10) return;
            try { CheckRuntime(); }
            catch (Exception exception) { failed = true; Debug.LogException(exception); }
            finally { EditorApplication.ExitPlaymode(); }
        }

        private static void CheckRuntime()
        {
            ProfessorBoss boss = null;
            foreach (var station in Object.FindObjectsByType<EnemyTestStation>(FindObjectsSortMode.None))
            {
                var settings = new SerializedObject(station);
                var template = settings.FindProperty("enemyTemplate").objectReferenceValue as GameObject;
                if (template == null || template.GetComponent<ProfessorBoss>() == null) continue;
                station.Visit();
                boss = station.Enemy.GetComponent<ProfessorBoss>();
                break;
            }
            var player = Object.FindFirstObjectByType<PlayerHealth>();
            Require(boss != null && boss.enabled && player != null, "Model test must have a live professor and player.");
            var health = boss.GetComponent<EnemyHealth>();
            var hud = boss.GetComponentInChildren<ProfessorHealthBar>();
            Require(hud != null && boss.GetComponentsInChildren<ProfessorHealthBar>().Length == 1,
                "Expected exactly one automatically attached professor HUD.");
            var canvas = hud.GetComponent<Canvas>();
            hud.SendMessage("LateUpdate");
            Require(canvas.enabled, "HUD must be visible in an active fight.");
            CaptureHud(boss, canvas);
            foreach (Graphic graphic in hud.GetComponentsInChildren<Graphic>())
                Require(!graphic.raycastTarget, "Boss HUD must not intercept input.");
            foreach (var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                if (enemy != health) enemy.gameObject.SetActive(false);
            foreach (var laser in Object.FindObjectsByType<AutoLaser>()) laser.enabled = false;
            foreach (var chaser in Object.FindObjectsByType<GuiltChaser>()) chaser.gameObject.SetActive(false);

            // Isolate contact above world geometry, using the actual model-test colliders and controller.
            boss.transform.SetPositionAndRotation(new Vector3(0f, 100f, 0f), Quaternion.identity);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(0f, 100f, 0.9f);
            controller.enabled = true;
            Physics.SyncTransforms();
            int hp = player.Current;
            RunSession.Ensure().SetPaused(PauseReason.Menu, true);
            boss.SendMessage("Update");
            Require(player.Current == hp, "Pause must suppress professor contact damage.");
            RunSession.Instance.SetPaused(PauseReason.Menu, false);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 100.9f, 0.65f);
            wall.transform.localScale = new Vector3(2f, 3f, 0.1f);
            Physics.SyncTransforms();
            boss.SendMessage("ApplyContactAttack");
            Require(player.Current == hp, "World geometry must block contact damage.");
            Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
            boss.SendMessage("ApplyContactAttack");
            Require(player.Current == hp - 20, "Touching the professor must deal exactly 20 damage.");
            Require(player.GetComponent<ScooterController>().HorizontalSpeed > 9.9f,
                "Contact must apply the knockback impulse.");
            boss.SendMessage("ApplyContactAttack");
            Require(player.Current == hp - 20, "Repeated contact in one frame must respect cooldown.");

            health.TakeDamage(health.Maximum / 2);
            hud.SendMessage("LateUpdate");
            var fill = hud.transform.Find("Deadline Panel/Health Frame/Health Track/Health");
            Require(Mathf.Abs(fill.localScale.x - (float)health.Current / health.Maximum) < 0.001f,
                "Health bar must match actual professor health.");
            Require(hud.transform.Find("Deadline Panel/Phase").GetComponent<Text>().text == "08:00 / DEADLINE",
                "Half health must display the deadline phase.");
            boss.enabled = false;
            hud.SendMessage("LateUpdate");
            Require(!canvas.enabled, "Disabling the boss must hide its HUD.");
            boss.enabled = true;
            player.TakeDamage(player.Maximum);
            hud.SendMessage("LateUpdate");
            Require(!canvas.enabled, "Player death must hide the boss HUD.");
            health.TakeDamage(health.Maximum);
            hud.SendMessage("LateUpdate");
            Require(!canvas.enabled, "Boss death must hide the boss HUD.");
            Debug.Log("PROFESSOR_CHALLENGE: model-test wiring, contact, knockback, cooldown, wall, pause, HP display, phase and terminal visibility checked.");
        }

        private static void CaptureHud(ProfessorBoss boss, Canvas canvas)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Camera camera = Camera.main;
            var texture = new RenderTexture(1280, 720, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.transform.position = boss.transform.position + new Vector3(0f, 3f, -8f);
                camera.transform.LookAt(boss.transform.position + Vector3.up);
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes("/tmp/boki-professor-healthbar.png", pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                camera.targetTexture = previousTarget;
                camera.transform.SetPositionAndRotation(position, rotation);
                RenderTexture.active = previous;
                texture.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(pixels);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
