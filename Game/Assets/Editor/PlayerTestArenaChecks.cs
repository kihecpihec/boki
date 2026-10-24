using System;
using System.Linq;
using Boki.Battery;
using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    [InitializeOnLoad]
    public static class PlayerTestArenaChecks
    {
        private const string Key = "Boki.PlayerTestArena.Checks";
        private static int stage, stationIndex, xpBefore, packsBefore, sheets, slams, healthBefore;
        private static double started;
        private static bool failed;
        private static PlayerTestArena arena;
        private static PlayerHealth player;
        private static EnemyTestStation[] stations;
        private static BatteryCharge charge;

        static PlayerTestArenaChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        [MenuItem("Boki/Player Model/Run Arena Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save dirty scenes first.");
            SessionState.SetString(Key + ".Setup", JsonUtility.ToJson(new SavedSetup
            {
                paths = setup.Select(s => s.path).ToArray(),
                loaded = setup.Select(s => s.isLoaded).ToArray(),
                active = setup.Select(s => s.isActive).ToArray()
            }));
            EditorSceneManager.OpenScene(BokiModelTestSetup.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        [Serializable] private sealed class SavedSetup
        {
            public string[] paths;
            public bool[] loaded, active;
        }

        private static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                stage = stationIndex = sheets = slams = 0;
                failed = false;
                started = EditorApplication.timeSinceStartup;
            }
            if (mode != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            var saved = JsonUtility.FromJson<SavedSetup>(SessionState.GetString(Key + ".Setup", ""));
            if (saved != null && saved.paths.Length > 0 && !string.IsNullOrEmpty(saved.paths[0]))
                EditorSceneManager.RestoreSceneManagerSetup(Enumerable.Range(0, saved.paths.Length).Select(i => new SceneSetup
                {
                    path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.active[i]
                }).ToArray());
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            Debug.Log(failed ? "PLAYER_TEST_ARENA_FAILED" : "PLAYER_TEST_ARENA_PASSED");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }

        private static void Next(int value) { stage = value; started = EditorApplication.timeSinceStartup; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        private static void Move(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || stage == 99) return;
            try
            {
                double elapsed = EditorApplication.timeSinceStartup - started;
                if (elapsed > 35) throw new InvalidOperationException("Arena checks timed out at stage " + stage);
                switch (stage)
                {
                    case 0:
                        arena = Object.FindAnyObjectByType<PlayerTestArena>();
                        if (arena == null || elapsed < 0.3) return;
                        player = Object.FindAnyObjectByType<PlayerHealth>();
                        stations = Object.FindObjectsByType<EnemyTestStation>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
                        Require(arena.enabled && stations.Length == 8, "Eight wired stations must be present.");
                        Require(player.transform.Find("Visuals").GetComponentInChildren<Animator>() != null, "The original rigged Boki must remain.");
                        Require(Object.FindObjectsByType<BatteryCharge>(FindObjectsSortMode.None).Length == 27, "Expected 24 station charges and three original charges.");
                        Require(EnemyHealth.Active.Count == 0, "Hub must be safe before station selection.");
                        Capture("/tmp/boki-arena-hub.png");
                        var buttons = arena.GetComponentsInChildren<Button>();
                        Require(buttons.Length == 13 && buttons.All(b => b.onClick.GetPersistentEventCount() == 1
                            && b.onClick.GetPersistentTarget(0) != null), "All test panel buttons must have live callbacks.");
                        arena.ToggleLaser();
                        arena.Visit(stations[stationIndex]);
                        Next(1);
                        break;
                    case 1:
                        if (elapsed < 0.2) return;
                        Require(arena.ActiveStation == stations[stationIndex] && arena.ActiveStation.Enemy != null,
                            "Selected station must spawn an enemy.");
                        Require(arena.ActiveStation.Enemy.GetComponents<MonoBehaviour>().All(b => b.enabled), "Enemy adapters must accept their references.");
                        Require(EnemyHealth.Active.Count == (stationIndex == 6 ? 0 : 1), "Only the selected enemy may register as a laser target.");
                        if (++stationIndex < stations.Length) { arena.Visit(stations[stationIndex]); Next(1); break; }
                        arena.Visit(stations[0]);
                        xpBefore = RunSession.Instance.State.XP;
                        packsBefore = Object.FindObjectsByType<ArenaXpPickup>(FindObjectsSortMode.None).Length;
                        arena.ActiveStation.Enemy.GetComponent<EnemyHealth>().TakeDamage(10000);
                        Next(2);
                        break;
                    case 2:
                        if (elapsed < 0.15) return;
                        Require(Object.FindObjectsByType<ArenaXpPickup>(FindObjectsSortMode.None).Length == packsBefore + 1,
                            "Regular kill must leave exactly one XP pack.");
                        Require(RunSession.Instance.State.KillCount == 1 && RunSession.Instance.State.XP == xpBefore,
                            "Kill counts once, XP waits for contact.");
                        var pack = Object.FindObjectsByType<ArenaXpPickup>(FindObjectsSortMode.None)
                            .First(p => stations[0].Contains(p.transform.position));
                        Move(pack.transform.position - Vector3.up * 0.6f);
                        RunSession.Instance.State.Guilt.GameplaySuspended = true;
                        Next(3);
                        break;
                    case 3:
                        if (elapsed < 0.2) return;
                        Require(RunSession.Instance.State.XP == xpBefore, "Paused contact must not collect XP.");
                        RunSession.Instance.State.Guilt.GameplaySuspended = false;
                        Next(4);
                        break;
                    case 4:
                        if (RunSession.Instance.State.XP == xpBefore) return;
                        Require(RunSession.Instance.State.XP == xpBefore + 1, "Contact grants one XP.");
                        arena.Visit(stations[0]);
                        charge = stations[0].GetComponentsInChildren<BatteryCharge>().First();
                        RunSession.Instance.State.LightBattery.Current = 50f;
                        Move(charge.transform.position - Vector3.up * 0.6f);
                        Next(5);
                        break;
                    case 5:
                        if (charge.Available) return;
                        Require(RunSession.Instance.State.Battery > 73f && RunSession.Instance.State.Battery <= 75f,
                            "Battery contact grants +25 with ordinary drain.");
                        arena.ResetEncounter();
                        Next(6);
                        break;
                    case 6:
                        if (elapsed < 0.2) return;
                        Require(arena.ActiveStation.Enemy.GetComponent<EnemyHealth>().IsAlive && !charge.Available,
                            "Reset respawns enemy without refilling spent charges.");
                        healthBefore = player.Current;
                        Move(arena.ActiveStation.Enemy.transform.position + Vector3.forward * 0.7f);
                        Next(12);
                        break;
                    case 12:
                        if (player.Current == healthBefore) return;
                        Require(player.Current < healthBefore, "Melee contact must damage the rigged player.");
                        arena.Visit(stations[3]);
                        Next(7);
                        break;
                    case 7:
                        if (Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length == 0) return;
                        arena.ReturnToHub();
                        Next(8);
                        break;
                    case 8:
                        if (elapsed < 0.2) return;
                        Require(EnemyHealth.Active.Count == 0 && Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length == 0,
                            "Leaving encounter removes enemy targets and live projectiles.");
                        arena.Visit(stations[7]);
                        var boss = arena.ActiveStation.Enemy.GetComponent<ProfessorBoss>();
                        boss.BarrageSheetFired += Sheet;
                        boss.SlamTelegraphStarted += Slam;
                        Next(9);
                        break;
                    case 9:
                        if (sheets < 3 || slams < 1) return;
                        Require(player.IsAlive, "Boss test must remain playable.");
                        Capture("/tmp/boki-arena-professor.png");
                        arena.Visit(stations[6]);
                        Next(10);
                        break;
                    case 10:
                        if (elapsed < 0.3) return;
                        var guilt = arena.ActiveStation.Enemy.GetComponent<GuiltChaser>();
                        Require(guilt.enabled && guilt.Pressure == RunSession.Instance.State.Guilt && guilt.Pressure.IsActive,
                            "Guilt must use shared occupancy and warning state.");
                        Require(guilt.GetComponent<EnemyHealth>() == null, "Guilt stays invulnerable.");
                        if (guilt.Phase != GuiltPhase.Chase || guilt.Pressure.AwaitingPlacement) return;
                        Require(player.GetComponent<LightBattery>().WarningStrength > 0f,
                            "A safely placed Guilt chase must drive the player's warning effects.");
                        arena.ReturnToHub();
                        player.TakeDamage(10000);
                        Require(!player.IsAlive && !player.GetComponent<ScooterController>().enabled, "Death stops riding.");
                        arena.RestartSandbox();
                        Next(11);
                        break;
                    case 11:
                        if (elapsed < 0.5) return;
                        arena = Object.FindAnyObjectByType<PlayerTestArena>();
                        player = Object.FindAnyObjectByType<PlayerHealth>();
                        Require(arena != null && player.Current == 100 && RunSession.Instance.State.XP == 0
                            && RunSession.Instance.State.KillCount == 0 && RunSession.Instance.State.Battery > 98f,
                            "Restart must restore HP, XP, kill count, battery and hub.");
                        Require(EnemyHealth.Active.Count == 0, "Restart returns to a safe hub.");
                        stage = 99;
                        EditorApplication.ExitPlaymode();
                        break;
                }
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
                stage = 99;
                EditorApplication.ExitPlaymode();
            }
        }

        private static void Sheet() => sheets++;
        private static void Slam() => slams++;

        private static void Capture(string path)
        {
            if (!Environment.GetCommandLineArgs().Contains("-arenaScreenshots")) return;
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var cameras = canvases.Select(c => c.worldCamera).ToArray();
            var distances = canvases.Select(c => c.planeDistance).ToArray();
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            float aspect = camera.aspect;
            try
            {
                camera.aspect = 1280f / 720f;
                foreach (var canvas in canvases)
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = 0.5f;
                    }
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.aspect = aspect;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = cameras[i];
                    canvases[i].planeDistance = distances[i];
                }
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
