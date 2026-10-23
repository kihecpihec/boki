using System;
using Boki.Combat;
using Boki.Enemies;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Play Mode check for Enemy_Test: each enemy system runs in isolation so one cannot kill Boki mid-check.
    // Run from the Editor menu, or in batch mode with -executeMethod Boki.Editor.EnemySmokeChecks.Run (no -quit).
    [InitializeOnLoad]
    public static class EnemySmokeChecks
    {
        private const string Key = "Boki.Enemies.SmokeRunning";
        private const string ScenePath = "Assets/Scenes/Enemy_Test.unity";
        private static bool failed;
        private static int stage;
        private static double stageStart;
        private static PlayerHealth player;
        private static MeleeEnemy[] melee;
        private static RangedEnemy[] ranged;
        private static GuiltChaser guilt;
        private static ProfessorBoss boss;
        private static int meleeHits, shots, sheets, telegraphs, slams, guiltStrikes;
        private static bool sawWarning, sawChase, sawReposition;
        private static int projectilesSeen;
        private static int hpBefore;

        static EnemySmokeChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += StateChanged;
        }

        [MenuItem("Boki/Enemy Models/Run Smoke Checks")]
        public static void Run() => EditorApplication.delayCall += () => EditorApplication.delayCall += Begin;

        private static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            EditorSceneManager.OpenScene(ScenePath);
            // In-memory only (never saved): survive the whole check so isolation phases are not cut short by death.
            var health = Object.FindFirstObjectByType<PlayerHealth>();
            var serialized = new SerializedObject(health);
            serialized.FindProperty("maximumHealth").intValue = 100000;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) Start();
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            EditorSceneManager.OpenScene(ScenePath); // discard the in-memory health override
            Debug.Log(failed ? "ENEMY_SMOKE_FAILED" : "ENEMY_SMOKE_PASSED");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }

        private static void Start()
        {
            failed = false;
            stage = 0;
            meleeHits = shots = sheets = telegraphs = slams = guiltStrikes = projectilesSeen = 0;
            sawWarning = sawChase = sawReposition = false;
            player = Object.FindFirstObjectByType<PlayerHealth>();
            melee = Object.FindObjectsByType<MeleeEnemy>(FindObjectsSortMode.None);
            ranged = Object.FindObjectsByType<RangedEnemy>(FindObjectsSortMode.None);
            // The auto-laser would kill the melee enemies before they reach Boki; this check covers enemy attacks.
            foreach (AutoLaser laser in Object.FindObjectsByType<AutoLaser>(FindObjectsSortMode.None)) laser.enabled = false;
            guilt = Object.FindFirstObjectByType<GuiltChaser>();
            boss = Object.FindFirstObjectByType<ProfessorBoss>();
            Check(melee.Length == 3 && ranged.Length == 2 && guilt != null && boss != null, "Expected 3 melee, 2 ranged, Guilt and the boss.");
            if (failed) return;
            foreach (MeleeEnemy enemy in melee) enemy.Attacked += () => meleeHits++;
            foreach (RangedEnemy enemy in ranged) enemy.Fired += () => shots++;
            guilt.PhaseChanged += phase =>
            {
                sawWarning |= phase == GuiltPhase.Warning;
                sawChase |= phase == GuiltPhase.Chase;
                sawReposition |= phase == GuiltPhase.Reposition;
            };
            guilt.Struck += () => guiltStrikes++;
            boss.BarrageSheetFired += () => sheets++;
            boss.SlamTelegraphStarted += () => telegraphs++;
            boss.SlamLanded += () => slams++;
            SetActive(true, true, false, false);
            stageStart = EditorApplication.timeSinceStartup;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || failed || stage >= 99) return;
            double elapsed = EditorApplication.timeSinceStartup - stageStart;
            switch (stage)
            {
                case 0: // melee and Watchers
                    if (meleeHits > 0 && shots > 0) Advance();
                    else if (elapsed > 15) Fail("Melee hit " + meleeHits + ", Watcher shots " + shots + " within 15 s.");
                    break;
                case 1: // boss: barrage then slam
                    if (sheets >= 3 && telegraphs >= 1 && slams >= 1) Advance();
                    else if (elapsed > 25) Fail("Boss sheets " + sheets + ", telegraphs " + telegraphs + ", slams " + slams + " within 25 s.");
                    break;
                case 2: // Guilt at accelerated time (grace 20 s + warning 1.5 s + chase)
                    Time.timeScale = 10f;
                    if (sawWarning && sawChase && guiltStrikes > 0 && sawReposition) Advance();
                    else if (elapsed > 20)
                        Fail("Guilt warning=" + sawWarning + " chase=" + sawChase + " strikes=" + guiltStrikes + " reposition=" + sawReposition);
                    break;
                case 3:
                    Time.timeScale = 1f;
                    Debug.Log("ENEMY_SMOKE: melee hits " + meleeHits + ", Watcher shots " + shots + ", sheets " + sheets
                        + ", telegraphs " + telegraphs + ", slams " + slams + ", Guilt strikes " + guiltStrikes);
                    stage = 99;
                    EditorApplication.ExitPlaymode();
                    break;
            }
        }

        private static void Advance()
        {
            stage++;
            stageStart = EditorApplication.timeSinceStartup;
            if (stage == 1) SetActive(false, false, false, true);
            else if (stage == 2) SetActive(false, false, true, false);
        }

        private static void SetActive(bool meleeOn, bool rangedOn, bool guiltOn, bool bossOn)
        {
            foreach (MeleeEnemy enemy in melee) if (enemy != null) enemy.gameObject.SetActive(meleeOn);
            foreach (RangedEnemy enemy in ranged) if (enemy != null) enemy.gameObject.SetActive(rangedOn);
            guilt.gameObject.SetActive(guiltOn);
            boss.gameObject.SetActive(bossOn);
        }

        private static void Check(bool condition, string message) { if (!condition) Fail(message); }

        private static void Fail(string message)
        {
            failed = true;
            Debug.LogError("ENEMY_SMOKE: " + message);
            if (player != null && melee != null)
                foreach (MeleeEnemy enemy in melee)
                    if (enemy != null)
                        Debug.Log("ENEMY_SMOKE: melee " + enemy.name + " at " + enemy.transform.position + ", player at "
                            + player.transform.position + ", moving " + enemy.IsMoving);
            Time.timeScale = 1f;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }
    }
}
