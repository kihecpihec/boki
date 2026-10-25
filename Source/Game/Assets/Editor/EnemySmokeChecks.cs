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
        [Serializable]
        private sealed class SavedSceneSetup
        {
            public string[] paths;
            public bool[] loaded;
            public bool[] active;
        }

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
        private static bool sawDeadlineBoundary, sawDeadlineImpact, sawProjectileTrail;

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
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            var saved = new SavedSceneSetup { paths = new string[setup.Length],
                loaded = new bool[setup.Length], active = new bool[setup.Length] };
            for (int i = 0; i < setup.Length; i++)
            {
                saved.paths[i] = setup[i].path;
                saved.loaded[i] = setup[i].isLoaded;
                saved.active[i] = setup[i].isActive;
            }
            SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(saved));
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
            EditorSceneManager.OpenScene(ScenePath); // discard in-memory fixture changes
            var saved = JsonUtility.FromJson<SavedSceneSetup>(SessionState.GetString(Key + ".Scenes", ""));
            if (saved != null && saved.paths.Length > 0 && !string.IsNullOrEmpty(saved.paths[0]))
            {
                var setup = new SceneSetup[saved.paths.Length];
                for (int i = 0; i < setup.Length; i++)
                    setup[i] = new SceneSetup { path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.active[i] };
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.EraseString(Key + ".Scenes");
            Debug.Log(failed ? "ENEMY_SMOKE_FAILED" : "ENEMY_SMOKE_PASSED");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }

        private static void Start()
        {
            failed = false;
            stage = 0;
            meleeHits = shots = sheets = telegraphs = slams = guiltStrikes = projectilesSeen = 0;
            sawWarning = sawChase = sawReposition = false;
            sawDeadlineBoundary = sawDeadlineImpact = sawProjectileTrail = false;
            player = Object.FindFirstObjectByType<PlayerHealth>();
            melee = Object.FindObjectsByType<MeleeEnemy>(FindObjectsSortMode.None);
            ranged = Object.FindObjectsByType<RangedEnemy>(FindObjectsSortMode.None);
            // The auto-laser would kill the melee enemies before they reach Boki; this check covers enemy attacks.
            foreach (AutoLaser laser in Object.FindObjectsByType<AutoLaser>(FindObjectsSortMode.None)) laser.enabled = false;
            guilt = Object.FindFirstObjectByType<GuiltChaser>();
            boss = Object.FindFirstObjectByType<ProfessorBoss>();
            Check(melee.Length == 3 && ranged.Length == 2 && guilt != null && boss != null, "Expected 3 melee, 2 ranged, Guilt and the boss.");
            if (failed) return;
            var attackView = boss.GetComponent<ProfessorAttackView>();
            Check(attackView != null && attackView.enabled, "Professor attack presentation must be wired.");
            Transform visuals = player.transform.Find("Visuals");
            Check(visuals != null && visuals.GetComponentInChildren<SkinnedMeshRenderer>() != null,
                "Enemy_Test must use the rigged Boki model.");
            if (failed) return;
            foreach (string name in new[] { "Boki placeholder", "Scooter deck", "Forward marker" })
                Check(!player.transform.Find(name).gameObject.activeSelf, "Player placeholder still visible: " + name);
            var animator = visuals.GetComponentInChildren<Animator>();
            Check(animator != null && animator.runtimeAnimatorController != null && !animator.applyRootMotion,
                "Boki must retain the riding animation without root motion.");
            var laserSettings = new SerializedObject(player.GetComponent<AutoLaser>());
            Check(laserSettings.FindProperty("beamOrigin").objectReferenceValue is Transform muzzle
                && muzzle.name == "Socket_LaserMuzzle" && muzzle.IsChildOf(visuals),
                "Laser must originate at the new scooter muzzle.");
            foreach (EnemyHealth enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                Check(enemy.GetComponent<EnemyFeedback>() != null && enemy.GetComponent<EnemyFeedback>().enabled,
                    "Missing enabled hit/death feedback on " + enemy.name);
            Check(guilt.GetComponent<EnemyHealth>() == null, "Guilt must remain invulnerable.");
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
            foreach (EnemyProjectile projectile in Object.FindObjectsByType<EnemyProjectile>())
            {
                var trail = projectile.GetComponent<TrailRenderer>();
                sawProjectileTrail |= trail != null && trail.sharedMaterial != null;
            }
            if (stage == 1)
            {
                var boundary = boss.transform.Find("Deadline Boundary").GetComponent<LineRenderer>();
                var impact = boss.transform.Find("Deadline Impact").GetComponent<LineRenderer>();
                if (boundary.enabled)
                {
                    sawDeadlineBoundary = true;
                    Vector3 point = boundary.GetPosition(0);
                    Vector3 center = boss.SlamCenter;
                    point.y = center.y;
                    Check(Mathf.Abs(Vector3.Distance(point, center) - boss.SlamRadius) < 0.01f,
                        "Deadline boundary must match the damage radius.");
                }
                sawDeadlineImpact |= impact.enabled;
            }
            switch (stage)
            {
                case 0: // melee and Watchers
                    if (meleeHits > 0 && shots > 0) Advance();
                    else if (elapsed > 15) Fail("Melee hit " + meleeHits + ", Watcher shots " + shots + " within 15 s.");
                    break;
                case 1: // boss: barrage then slam
                    if (sheets >= 3 && telegraphs >= 1 && slams >= 1 && sawDeadlineImpact)
                    {
                        Check(sawDeadlineBoundary && sawProjectileTrail, "Missing deadline boundary or projectile trails.");
                        if (!failed) Advance();
                    }
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
                    CheckSweptProjectile();
                    if (failed) return;
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
            else if (stage == 2)
            {
                SetActive(false, false, true, false);
                foreach (LineRenderer line in boss.GetComponentsInChildren<LineRenderer>(true))
                    Check(!line.enabled, "Professor effects must clear when disabled.");
                CheckGuiltPlacement();
            }
        }

        // In-memory fixture only: exercise real physics/camera rejection without changing the scene asset.
        private static void CheckGuiltPlacement()
        {
            var playerController = player.GetComponent<CharacterController>();
            playerController.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, 0f), Quaternion.identity);
            playerController.enabled = true;
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 4f, -6f),
                Quaternion.LookRotation(new Vector3(0f, -0.3f, 1f)));
            var serialized = new SerializedObject(guilt);
            var markers = serialized.FindProperty("spawnPoints");
            var visual = (GameObject)serialized.FindProperty("visualRoot").objectReferenceValue;
            var controller = guilt.GetComponent<CharacterController>();
            Vector3[] invalid = { new Vector3(0f, 0f, 13f), new Vector3(0f, 0f, -3f),
                new Vector3(0f, -10f, -13f) };
            foreach (Vector3 position in invalid)
            {
                for (int i = 0; i < markers.arraySize; i++)
                    ((Transform)markers.GetArrayElementAtIndex(i).objectReferenceValue).position = position;
                Physics.SyncTransforms();
                guilt.Pressure.Reset();
                guilt.SetPressureActive(true);
                guilt.Pressure.Tick(20f);
                Check(guilt.Pressure.AwaitingPlacement && !visual.activeSelf && !controller.enabled,
                    "Guilt must remain hidden with no safe marker at " + position);
                if (failed) return;
            }
            for (int i = 0; i < markers.arraySize; i++)
                ((Transform)markers.GetArrayElementAtIndex(i).objectReferenceValue).position = new Vector3(0f, 0f, -13f);
            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = new Vector3(0f, 1f, -13f);
            blocker.transform.localScale = new Vector3(3f, 3f, 3f);
            Physics.SyncTransforms();
            guilt.Pressure.Reset();
            guilt.SetPressureActive(true);
            guilt.Pressure.Tick(20f);
            Check(guilt.Pressure.AwaitingPlacement && !visual.activeSelf, "Guilt must reject occupied capsule space.");
            Object.DestroyImmediate(blocker);
            Physics.SyncTransforms();
            guilt.Pressure.Reset();
            guilt.SetPressureActive(true);
            guilt.Pressure.Tick(20f);
            Check(!guilt.Pressure.AwaitingPlacement && visual.activeSelf && controller.enabled,
                "Guilt must appear at a valid offscreen grounded marker behind Boki.");
            guilt.SetPressureActive(false);
            guilt.Pressure.Tick(10f);
            Check(!visual.activeSelf && !controller.enabled && guilt.Pressure.AccumulatedCombatSeconds == 20f,
                "Leaving the combat space must hide Guilt and freeze pressure.");
            guilt.SetPressureActive(true);
            guilt.Pressure.EndForBoss();
            Check(!visual.activeSelf && !controller.enabled && !guilt.Pressure.IsActive,
                "Boss entry must end ambient Guilt.");
            guilt.Pressure.Reset();
            guilt.SetPressureActive(true);
            sawWarning = sawChase = sawReposition = false;
            guiltStrikes = 0;
            Debug.Log("GUILT_PLACEMENT: invalid distance/direction/ground, occupied marker, valid marker, exit and boss checks completed.");
        }

        private static void CheckSweptProjectile()
        {
            // Cross the target in one frame, with both endpoints outside its hit radius.
            var targetObject = new GameObject("Projectile sweep target");
            var shotObject = new GameObject("Projectile sweep check");
            try
            {
                targetObject.transform.position = new Vector3(0f, 100f, 0f);
                var targetHealth = targetObject.AddComponent<PlayerHealth>();
                var shot = shotObject.AddComponent<EnemyProjectile>();
                shotObject.transform.position = targetObject.transform.position + new Vector3(0f, 0.9f, -2f);
                var settings = new SerializedObject(shot);
                settings.FindProperty("speed").floatValue = 4f / Time.deltaTime;
                settings.FindProperty("obstructionMask").intValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
                shot.Launch(targetHealth, Vector3.forward, 7);
                shot.SendMessage("Update");
                Check(targetHealth.Current == targetHealth.Maximum - 7,
                    "A projectile crossing Boki between frames must land exactly one hit.");
            }
            finally
            {
                Object.DestroyImmediate(shotObject);
                Object.DestroyImmediate(targetObject);
            }
        }

        private static void SetActive(bool meleeOn, bool rangedOn, bool guiltOn, bool bossOn)
        {
            foreach (MeleeEnemy enemy in melee) if (enemy != null) enemy.gameObject.SetActive(meleeOn);
            foreach (RangedEnemy enemy in ranged) if (enemy != null) enemy.gameObject.SetActive(rangedOn);
            guilt.gameObject.SetActive(guiltOn);
            if (guiltOn) guilt.SetPressureActive(true);
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
