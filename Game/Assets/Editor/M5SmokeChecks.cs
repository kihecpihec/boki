using System;
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
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    [InitializeOnLoad]
    public static class M5SmokeChecks
    {
        private const string Key = "Boki.M5.SmokeRunning";
        private static readonly RouteChoice[][] Histories = {
            new[] { RouteChoice.A, RouteChoice.A, RouteChoice.A },
            new[] { RouteChoice.A, RouteChoice.A, RouteChoice.B },
            new[] { RouteChoice.A, RouteChoice.B, RouteChoice.B },
            new[] { RouteChoice.B, RouteChoice.A, RouteChoice.A },
            new[] { RouteChoice.B, RouteChoice.A, RouteChoice.B },
            new[] { RouteChoice.C, RouteChoice.B, RouteChoice.C },
            new[] { RouteChoice.B, RouteChoice.C, RouteChoice.A },
            new[] { RouteChoice.C, RouteChoice.C, RouteChoice.C }
        };
        private static int stage, iteration, walkTarget, afterWalk;
        private static Vector3[] walkPoints;
        private static string walkScene;
        private static double lastProgress;
        private static bool failed;
        private static double started, waitUntil;
        private static RunSession original;
        private static PlayerHealth player;
        private static RouteManager manager;
        private static RouteChoice Choice(int level) => Histories[Math.Max(0, iteration - 3)][level];

        static M5SmokeChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += ModeChanged;
            Application.logMessageReceived += (message, trace, type) =>
            { if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) failed = true; };
        }
        [MenuItem("Boki/Run M5 Smoke Checks")]
        public static void Run() => EditorApplication.delayCall += () => EditorApplication.delayCall += Begin;
        private static void Begin()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Save scenes first.");
            Require(Application.productName == "Boki-M5-Validation", "Run only in the isolated validation project.");
            ClearProgress();
            var entryScene = EditorSceneManager.OpenScene(RunSession.FeriScene);
            // An already-open M4 scene predates the M5 checkbox. Reproduce its
            // default-false value if that old serialized option still exists.
            foreach (var root in entryScene.GetRootGameObjects())
            {
                var bootstrap = root.GetComponent<RunSession>();
                if (bootstrap == null) continue;
                var serialized = new SerializedObject(bootstrap);
                var legacyFlag = serialized.FindProperty("persistUnlocks");
                if (legacyFlag != null)
                {
                    legacyFlag.boolValue = false;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        private static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            { stage = 0; iteration = 3; failed = false; started = EditorApplication.timeSinceStartup; }
            if (mode == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                Debug.Log(failed ? "M5_SMOKE_FAILED" : "M5_SMOKE_PASSED: direct entry, five fresh A/B histories and three unlocked C histories, real gates/exits/arena, saved flags and next-run snapshots, rear seals and chosen-only encounters, CharacterController traversal, HP carry, singleton lifetime.");
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                Require(EditorApplication.timeSinceStartup - started < 240, "M4 smoke timeout at stage " + stage);
                if (Time.realtimeSinceStartupAsDouble < waitUntil) return;
                if (stage == 0)
                {
                    manager = Object.FindFirstObjectByType<RouteManager>();
                    Require(!manager.IsReady && !manager.Select(RouteChoice.A), "Direct FERI must reject selection.");
                    original = RunSession.Instance;
                    Require(original.EntryScene == RunSession.PohorjeScene, "Direct FERI must configure the M4 entry.");
                    EditorSceneManager.LoadSceneInPlayMode(RunSession.MariborScene, new LoadSceneParameters(LoadSceneMode.Single));
                    stage = 1;
                }
                else if (stage == 1 && SceneManager.GetActiveScene().path == RunSession.MariborScene)
                {
                    Prepare();
                    Require(!manager.IsReady && !manager.Select(RouteChoice.A), "Direct City must reject selection.");
                    manager.StartNewRun(); stage = 2;
                }
                else if (stage == 2 && SceneManager.GetActiveScene().path == RunSession.PohorjeScene)
                {
                    Prepare();
                    Require(player.Current == 100 && original.State.Choice(RouteLevel.Feri) == RouteChoice.None, "Retry resets HP and choices.");
                    Require(!Object.FindFirstObjectByType<CombatView>().DeathVisible, "Fresh run hides death panel.");
                    Require(manager.Gate(RouteChoice.C) == (iteration < 8 ? GateState.PersistentLock : GateState.Available), "Forest C uses new-run snapshot.");
                    if (iteration < 8)
                    {
                        Teleport(new Vector3(12, 0.1f, -2));
                        player.GetComponent<CharacterController>().Move(Vector3.forward * 4);
                        Require(player.transform.position.z < 0, "C physically blocks movement.");
                    }
                    Require(!manager.Advance(), "Unchosen Forest cannot exit.");
                    if (iteration == 0) { DieAndRetry(); return; }
                    CrossGate(Choice(0)); stage = 3;
                }
                else if (stage == 3)
                {
                    Require(original.State.Choice(RouteLevel.Forest) == Choice(0), "Physical Forest selection.");
                    VerifyBranches(Choice(0));
                    Require(!manager.Select(Choice(0) == RouteChoice.A ? RouteChoice.B : RouteChoice.A), "Alternatives close.");
                    player.TakeDamage(27);
                    BeginWalk(Choice(0), 4);
                }
                else if (stage == 4 && SceneManager.GetActiveScene().path == RunSession.MariborScene)
                {
                    Prepare(); Require(player.Current == 73, "Forest damage carries to City.");
                    Require(manager.Gate(RouteChoice.B) == (Choice(0) != RouteChoice.B ? GateState.Available : GateState.RouteLock), "City B follows Forest choice.");
                    Require(manager.Gate(RouteChoice.C) == (Choice(0) == RouteChoice.A ? GateState.RouteLock : iteration < 8 ? GateState.PersistentLock : GateState.Available), "C lock priority.");
                    var flags = new PersistentUnlocks(new PlayerPrefsUnlockStore()).Read();
                    Require(flags.ForestC, "Forest exit saves immediately.");
                    Require(original.State.AvailableUnlocks.ForestC == (iteration >= 8), "Forest snapshot is unchanged after saving.");
                    Require(!manager.Advance(), "Unchosen City cannot exit.");
                    if (iteration == 1) { DieAndRetry(); return; }
                    CrossGate(Choice(1)); stage = 5;
                }
                else if (stage == 5)
                {
                    Require(original.State.Choice(RouteLevel.City) == Choice(1), "Physical City selection.");
                    VerifyBranches(Choice(1));
                    BeginWalk(Choice(1), 6);
                }
                else if (stage == 6 && SceneManager.GetActiveScene().path == RunSession.FeriScene)
                {
                    Prepare(); Require(player.Current == 73, "HP carries into FERI.");
                    Require(manager.Gate(RouteChoice.A) == (Choice(1) != RouteChoice.B ? GateState.Available : GateState.RouteLock), "City choice filters FERI A.");
                    Require(new PersistentUnlocks(new PlayerPrefsUnlockStore()).Read().CityC, "City exit saves immediately.");
                    Require(!new PersistentUnlocks(new PlayerPrefsUnlockStore()).Read().FeriC || iteration >= 8, "FERI unlock requires arena entry.");
                    Require(!manager.Advance(), "FERI has no fourth scene.");
                    if (iteration == 2) { DieAndRetry(); return; }
                    CrossEndpoint(); stage = 7;
                }
                else if (stage == 7)
                {
                    Require(!Object.FindFirstObjectByType<ClassroomArrival>().HasArrived, "Unchosen player cannot arrive.");
                    CrossGate(Choice(2)); stage = 8;
                }
                else if (stage == 8)
                {
                    Require(original.State.Choice(RouteLevel.Feri) == Choice(2), "Physical FERI selection.");
                    VerifyBranches(Choice(2));
                    BeginWalk(Choice(2), 9);
                }
                else if (stage == 20)
                {
                    if (SceneManager.GetActiveScene().path != walkScene) { stage = afterWalk; return; }
                    var delta = walkPoints[walkTarget] - player.transform.position;
                    delta.y = 0;
                    if (delta.magnitude < 0.2f)
                    {
                        if (++walkTarget == walkPoints.Length)
                        { stage = afterWalk; waitUntil = Time.realtimeSinceStartupAsDouble + 0.2; return; }
                        lastProgress = EditorApplication.timeSinceStartup;
                        return;
                    }
                    var before = player.transform.position;
                    player.GetComponent<CharacterController>().Move(Vector3.ClampMagnitude(delta, 0.3f) + Vector3.down * 0.15f);
                    var progress = player.transform.position - before; progress.y = 0;
                    if (progress.magnitude > 0.02f) lastProgress = EditorApplication.timeSinceStartup;
                    Require(EditorApplication.timeSinceStartup - lastProgress < 3, "Graybox path blocked in " + walkScene + " at " + player.transform.position);
                }
                else if (stage == 9)
                {
                    Require(Object.FindFirstObjectByType<ClassroomArrival>().HasArrived, "Classroom trigger marks arrival.");
                    Require(original.State.Choice(RouteLevel.Forest) == Choice(0) && original.State.Choice(RouteLevel.City) == Choice(1), "Earlier choices survive.");
                    var flags = new PersistentUnlocks(new PlayerPrefsUnlockStore()).Read();
                    Require(flags.ForestC && flags.CityC && flags.FeriC, "All three milestones persist.");
                    Require(original.State.AvailableUnlocks.FeriC == (iteration >= 8), "Arena does not mutate current snapshot.");
                    Require(!original.EnterBossArena(), "Arena entry is accepted once per run.");
                    iteration++;
                    if (iteration >= 11) EditorApplication.ExitPlaymode();
                    else
                    {
                        if (iteration < 8) ClearProgress();
                        manager.StartNewRun(); stage = 2;
                    }
                }
            }
            catch (Exception error) { failed = true; Debug.LogException(error); EditorApplication.ExitPlaymode(); }
        }
        private static void DieAndRetry()
        {
            var combat = Object.FindFirstObjectByType<CombatSession>();
            player.TakeDamage(int.MaxValue);
            Require(combat.IsDead && Object.FindFirstObjectByType<CombatView>().DeathVisible, "Death shows ABSENT.");
            Require(!manager.Advance() && !manager.Select(RouteChoice.A), "Dead player cannot progress.");
            Require(!player.GetComponent<ScooterController>().enabled && !Object.FindFirstObjectByType<ThirdPersonCamera>().enabled, "Death disables controls.");
            foreach (var enemy in Object.FindObjectsByType<MeleeEnemy>(FindObjectsSortMode.None)) Require(!enemy.enabled, "Death stops enemies.");
            Object.FindFirstObjectByType<CombatView>().RetryButton.onClick.Invoke();
            var health = original.State.Health;
            combat.Retry();
            Require(ReferenceEquals(health, original.State.Health), "Repeated Retry cannot reset twice.");
            iteration++; stage = 2;
        }
        private static void Prepare()
        {
            player = Object.FindFirstObjectByType<PlayerHealth>(); manager = Object.FindFirstObjectByType<RouteManager>();
            Require(original == RunSession.Instance && Object.FindObjectsByType<RunSession>(FindObjectsSortMode.None).Length == 1, "One original session survives.");
            Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1 && Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "One camera/listener.");
            VerifyBranches(RouteChoice.None);
            player.GetComponent<ScooterController>().enabled = false;
            player.GetComponent<AutoLaser>().enabled = false;
            foreach (var enemy in Object.FindObjectsByType<MeleeEnemy>(FindObjectsSortMode.None)) enemy.enabled = false;
        }
        private static void VerifyBranches(RouteChoice selected)
        {
            var savedPosition = player.transform.position;
            int expectedEnemies = 0;
            foreach (var gate in Object.FindObjectsByType<RouteGate>(FindObjectsSortMode.None))
            {
                var fields = new SerializedObject(gate);
                var choice = (RouteChoice)fields.FindProperty("choice").intValue;
                bool chosen = choice == selected;
                var rear = (Collider)fields.FindProperty("exitBlocker").objectReferenceValue;
                var rearVisual = (Renderer)fields.FindProperty("exitVisual").objectReferenceValue;
                var encounter = (GameObject)fields.FindProperty("encounterRoot").objectReferenceValue;
                Require(rear != null && rearVisual != null && encounter != null, "Every branch needs a rear seal and encounter root.");
                Require(rear.enabled == !chosen && rearVisual.enabled == !chosen, "Unselected exits must block movement and visibility.");
                Require(encounter.activeSelf == chosen, "Only the chosen encounter may activate.");
                var enemies = encounter.GetComponentsInChildren<MeleeEnemy>(true);
                foreach (var enemy in enemies)
                {
                    Require(enemy.gameObject.activeInHierarchy == chosen, "Unchosen enemies must remain inactive.");
                    if (chosen) { expectedEnemies++; enemy.enabled = false; }
                }
                bool locked = manager.Gate(choice) == GateState.RouteLock || manager.Gate(choice) == GateState.PersistentLock;
                Require(((Collider)fields.FindProperty("blocker").objectReferenceValue).enabled == locked, "Entrance agrees with route rules.");
                if (selected != RouteChoice.None && !chosen)
                {
                    Teleport(new Vector3(((int)choice - 2) * 12, 0.1f, 62));
                    player.GetComponent<CharacterController>().Move(Vector3.back * 4);
                    Require(player.transform.position.z > 60, "Unchosen branch must block reentry from the merge.");
                    Require(Physics.Linecast(new Vector3(((int)choice - 2) * 12, 2, 62),
                        new Vector3(((int)choice - 2) * 12, 2, 58), 1, QueryTriggerInteraction.Ignore), "Rear seal must occlude branch sight/laser lines.");
                }
            }
            Require(EnemyHealth.Active.Count == expectedEnemies, "Only selected enemies enter the targeting registry.");
            Teleport(savedPosition);
        }
        private static void CrossGate(RouteChoice choice)
        {
            Teleport(new Vector3(((int)choice - 2) * 12, 0.1f, 1));
            player.GetComponent<CharacterController>().Move(Vector3.forward * 1.2f);
            waitUntil = Time.realtimeSinceStartupAsDouble + 0.15;
        }
        private static void BeginWalk(RouteChoice choice, int nextStage)
        {
            float x = ((int)choice - 2) * 12;
            walkPoints = manager.Level == RouteLevel.Feri && choice == RouteChoice.B
                ? new[] { new Vector3(-3, 0, 12), new Vector3(-3, 0, 27), new Vector3(0, 0, 34),
                    new Vector3(0, 0, 64), new Vector3(0, 0, 70), new Vector3(0, 0, 82) }
                : new[] { new Vector3(x, 0, 64), new Vector3(0, 0, 70), new Vector3(0, 0, 82) };
            walkTarget = 0; afterWalk = nextStage; walkScene = SceneManager.GetActiveScene().path;
            lastProgress = EditorApplication.timeSinceStartup; stage = 20;
        }
        private static void CrossEndpoint()
        {
            Teleport(new Vector3(0, 0.1f, 80));
            player.GetComponent<CharacterController>().Move(Vector3.forward * 2);
            waitUntil = Time.realtimeSinceStartupAsDouble + 0.2;
        }
        private static void Teleport(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.position = position; controller.enabled = true; Physics.SyncTransforms();
        }
        private static void ClearProgress()
        {
            foreach (var key in new[] { "boki.unlock.forestC", "boki.unlock.cityC", "boki.unlock.feriC" }) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
