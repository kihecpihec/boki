using System;
using System.Linq;
using Boki.Combat;
using Boki.Core;
using Boki.Player;
using Boki.UI;
using Boki.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    [InitializeOnLoad]
    public static class UpgradeGameplayChecks
    {
        private const string Key = "Boki.UpgradeGameplayChecks";
        private static int stage;
        private static double started;
        private static bool failed;
        private static RunSession session;
        private static PlayerHealth player;
        private static UpgradeScreen screen;
        private static AutoLaser laser;
        private static EnemyHealth first, second;
        private static int revision;
        private static int packsBefore;
        private static float batteryBefore;
        private static Vector3 playerBefore;

        static UpgradeGameplayChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        [MenuItem("Boki/Upgrades/Run Gameplay Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            var saved = new SavedScenes { paths = scenes.Select(s => s.path).ToArray(),
                loaded = scenes.Select(s => s.isLoaded).ToArray(), active = scenes.Select(s => s.isActive).ToArray() };
            SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(saved));
            EditorSceneManager.OpenScene("Assets/Scenes/Player_Model_Test.unity", OpenSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        [Serializable] private sealed class SavedScenes { public string[] paths; public bool[] loaded, active; }
        private static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0;
                failed = false;
                started = EditorApplication.timeSinceStartup;
            }
            if (mode != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key + ".Scenes", ""));
            if (saved != null && saved.paths.Length > 0 && !string.IsNullOrEmpty(saved.paths[0]))
            {
                var setup = new SceneSetup[saved.paths.Length];
                for (int i = 0; i < setup.Length; i++) setup[i] = new SceneSetup
                    { path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.active[i] };
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.EraseString(Key + ".Scenes");
            Debug.Log(failed ? "UPGRADE_GAMEPLAY_FAILED" : "UPGRADE_GAMEPLAY_PASSED");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || stage == 99) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 40) throw new InvalidOperationException("Timed out at stage " + stage);
                if (stage == 0)
                {
                    session = RunSession.Instance;
                    player = Object.FindFirstObjectByType<PlayerHealth>();
                    screen = Object.FindFirstObjectByType<UpgradeScreen>();
                    laser = Object.FindFirstObjectByType<AutoLaser>();
                    if (session == null || player == null || screen == null || laser == null) return;
                    Require(Object.FindFirstObjectByType<UpgradeManager>().isActiveAndEnabled, "UpgradeManager must be wired.");
                    Require(Object.FindObjectsByType<UpgradeManager>().Length == 1
                        && Object.FindObjectsByType<PlayerHud>().Length == 1
                        && Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length == 1,
                        "Combined scene must have one upgrade manager, HUD and EventSystem.");
                    var arena = Object.FindFirstObjectByType<PlayerTestArena>();
                    Require(arena != null && arena.GetComponentsInChildren<Boki.Enemies.EnemyTestStation>().Length == 8,
                        "Upgrade setup must preserve all eight arena stations.");
                    Require(screen.GetComponent<Canvas>().sortingOrder > arena.GetComponentInChildren<Canvas>().sortingOrder,
                        "Upgrade selection must cover the arena panel.");
                    laser.enabled = false;
                    player.TakeDamage(10);
                    session.State.LightBattery.Current = 50;
                    session.State.Progression.AddXp(33);
                    Require(screen.IsVisible && Time.timeScale == 0f, "Threshold must show existing mockup and pause.");
                    arena.Visit(arena.GetComponentsInChildren<Boki.Enemies.EnemyTestStation>()[0]);
                    Require(arena.ActiveStation == null, "Station controls must reject actions during upgrade selection.");
                    batteryBefore = session.State.Battery;
                    playerBefore = player.transform.position;
                    revision = session.State.Progression.OfferVersion;
                    stage++;
                    return;
                }
                if (stage == 1)
                {
                    Require(session.State.Battery == batteryBefore && player.transform.position == playerBefore, "Battery and movement must freeze.");
                    session.SetPaused(PauseReason.Menu, true);
                    screen.RequestChoice(0);
                    Require(session.State.Progression.Selections == 1 && screen.IsVisible && Time.timeScale == 0f, "Queued choice must stay paused.");
                    Require(!session.State.Progression.TrySelect(0, revision), "Stale offer must be rejected.");
                    stage++;
                    return;
                }
                if (stage == 2)
                {
                    if (!session.State.Progression.IsMaxLevel)
                    {
                        screen.RequestChoice(0);
                        return;
                    }
                    Require(!screen.IsVisible && Time.timeScale == 0f, "Completing choices must retain menu pause.");
                    session.SetPaused(PauseReason.Menu, false);
                    Require(Time.timeScale > 0f && !session.State.Progression.AddXp(), "Max level stops collection and resumes after final pause reason.");
                    // Reset via the run authority, then bind the demonstration player to its fresh health.
                    session.State.NewRun(default, 0);
                    player.enabled = false;
                    player.enabled = true;
                    session.State.Progression.AddXp(3);
                    // Existing random offers stay stable. Find a fresh run with Double Trouble offered.
                    for (int n = 0; !Enumerable.Range(0, session.State.Progression.OfferCount).Any(i => session.State.Progression.Offer(i) == UpgradeId.DoubleTrouble); n++)
                    {
                        Require(n < 100, "Could not offer Double Trouble.");
                        session.State.NewRun(default, n);
                        player.enabled = false;
                        player.enabled = true;
                        session.State.Progression.AddXp(3);
                    }
                    int index = Enumerable.Range(0, session.State.Progression.OfferCount).First(i => session.State.Progression.Offer(i) == UpgradeId.DoubleTrouble);
                    // Direct rule selection avoids the screen's deliberate same-frame input guard.
                    Require(session.State.Progression.TrySelect(index, session.State.Progression.OfferVersion), "Double Trouble must apply.");
                    first = Target(player.transform.position + player.transform.forward * 4f);
                    second = Target(player.transform.position + player.transform.forward * 5f);
                    laser.enabled = true;
                    stage++;
                    return;
                }
                if (stage == 3)
                {
                    if (first.Current == 60) return;
                    Require(first.Current == 40 && second.Current == 40, "Double Trouble must damage two distinct visible targets once.");
                    laser.enabled = false;
                    Object.Destroy(second.gameObject);
                    Object.Destroy(first.gameObject);
                    packsBefore = Object.FindObjectsByType<XpPickup>().Length;
                    XpPickup.Spawn(player.transform.position - Vector3.up * .1f, player.gameObject.scene);
                    stage++;
                    return;
                }
                if (stage == 4)
                {
                    if (session.State.XP == 3) return;
                    Require(session.State.XP == 4, "Real contact must award one XP pack.");
                    Require(Object.FindObjectsByType<XpPickup>().Length == packsBefore, "Collected pack must disappear.");
                    player.TakeDamage(100);
                    Require(!session.IsGameplayActive && !screen.IsVisible, "Death ends gameplay without stranding upgrade UI.");
                    stage = 99;
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception exception)
            {
                failed = true;
                stage = 99;
                Debug.LogException(exception);
                EditorApplication.ExitPlaymode();
            }
        }

        private static EnemyHealth Target(Vector3 position)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Upgrade Check Target";
            target.layer = 2;
            target.transform.position = position;
            var enemy = target.AddComponent<EnemyHealth>();
            return enemy;
        }
    }
}
