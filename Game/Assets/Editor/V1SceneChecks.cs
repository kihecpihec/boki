using System;
using Boki.Battery;
using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.Upgrades;
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
    [InitializeOnLoad]
    public static class V1SceneChecks
    {
        private const string Key = "Boki.V1.Checks";
        private static readonly string[] UnlockKeys = { "boki.unlock.forestC", "boki.unlock.cityC", "boki.unlock.feriC" };
        [Serializable] private sealed class Saved { public bool[] has; public int[] values; public string[] paths; public bool[] loaded, active; public int background, editorInput; }
        private static int stage;
        private static double nextTick, started;
        private static bool failed;
        private static string failure;
        private static float pausedBattery;
        private static int carriedHealth;
        private static bool encounterVerified;
        private static int professorSamples;
        private static Vector3 professorStart;
        private static Vector3 professorPaused;

        static V1SceneChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
            Application.logMessageReceived += Log;
        }

        [MenuItem("Boki/Version 1/Run Integration Checks")]
        public static void Run()
        {
            V1SceneSetup.RequireCleanEditor();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var saved = new Saved { has = new bool[3], values = new int[3], paths = new string[setup.Length], loaded = new bool[setup.Length], active = new bool[setup.Length] };
            saved.background = (int)InputSystem.settings.backgroundBehavior;
            saved.editorInput = (int)InputSystem.settings.editorInputBehaviorInPlayMode;
            for (int i = 0; i < setup.Length; i++) { saved.paths[i] = setup[i].path; saved.loaded[i] = setup[i].isLoaded; saved.active[i] = setup[i].isActive; }
            failed = false; failure = "";
            for (int i = 0; i < UnlockKeys.Length; i++)
            {
                saved.has[i] = PlayerPrefs.HasKey(UnlockKeys[i]); saved.values[i] = PlayerPrefs.GetInt(UnlockKeys[i]);
                PlayerPrefs.SetInt(UnlockKeys[i], 0);
            }
            PlayerPrefs.Save();
            SessionState.SetString(Key + ".Saved", JsonUtility.ToJson(saved));
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(V1SceneSetup.Scenes[0]);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Boki/Version 1/Run Professor Checks")]
        public static void RunProfessor()
        {
            Run();
            SessionState.SetBool(Key + ".Professor", true);
            EditorSceneManager.OpenScene(V1SceneSetup.Scenes[2]);
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = SessionState.GetBool(Key + ".Professor", false) ? 80 : 0; professorSamples = 0; encounterVerified = false; started = EditorApplication.timeSinceStartup; nextTick = started + 0.5;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            var saved = JsonUtility.FromJson<Saved>(SessionState.GetString(Key + ".Saved", ""));
            for (int i = 0; i < UnlockKeys.Length; i++)
                if (saved.has[i]) PlayerPrefs.SetInt(UnlockKeys[i], saved.values[i]); else PlayerPrefs.DeleteKey(UnlockKeys[i]);
            PlayerPrefs.Save();
            SessionState.SetBool(Key, false);
            bool professorOnly = SessionState.GetBool(Key + ".Professor", false);
            SessionState.EraseBool(Key + ".Professor");
            InputSystem.settings.backgroundBehavior = (InputSettings.BackgroundBehavior)saved.background;
            InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)saved.editorInput;
            bool restore = saved.paths.Length > 0;
            foreach (var path in saved.paths) restore &= !string.IsNullOrEmpty(path);
            if (restore)
            {
                var setup = new SceneSetup[saved.paths.Length];
                for (int i = 0; i < setup.Length; i++) setup[i] = new SceneSetup { path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.active[i] };
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (professorOnly) Debug.Log(failed ? "PROFESSOR_ARENA_CHECKS_FAILED: " + failure : "PROFESSOR_ARENA_CHECKS_PASSED");
            else Debug.Log(failed ? "V1_INTEGRATION_FAILED: " + failure : "V1_INTEGRATION_PASSED: physical route triggers, portal guards/travel, XP contact, upgrade pause, state carry, boss victory, next-run unlocks, City C escape, POWER OFF, death and retry.");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }
        private static void Log(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || failed) return;
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (trace.Contains("UnityEditor.Search.SearchDatabase")) return;
            failed = true; failure = message;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (failed) { EditorApplication.ExitPlaymode(); return; }
            if (EditorApplication.timeSinceStartup - started > 100) { Fail("Timeout at stage " + stage); return; }
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.2;
            try
            {
                var scene = SceneManager.GetActiveScene();
                var session = RunSession.Instance;
                var player = V1SceneSetup.Find<PlayerHealth>(scene);
                var manager = V1SceneSetup.Find<RouteManager>(scene);
                var flow = V1SceneSetup.Find<V1GameFlow>(scene);
                if (session == null || player == null || manager == null || flow == null || session.IsLoading) return;
                player.GetComponent<ScooterController>().enabled = false;
                switch (stage)
                {
                    case 80:
                        var focusedBoss = V1SceneSetup.Find<ProfessorBoss>(scene);
                        Teleport(player, new Vector3(0f, 0.05f, 78f));
                        focusedBoss.gameObject.SetActive(true);
                        focusedBoss.BeginFight();
                        stage = 8;
                        break;
                    case 0:
                        Check(scene.path == V1SceneSetup.Scenes[0], "Starts in V1 Pohorje");
                        Validate(scene);
                        Check(!V1SceneSetup.Find<LevelPortal>(scene).TryTravel(), "Portal rejects travel before a route is chosen");
                        Check(session.State.Gate(RouteLevel.Forest, RouteChoice.C) == GateState.PersistentLock, "First-run Forest C locked");
                        Select(scene, RouteChoice.A); stage++; break;
                    case 1:
                        Check(session.State.Choice(RouteLevel.Forest) == RouteChoice.A, "Physical Forest selection trigger");
                        CheckBranches(scene, RouteChoice.A);
                        if (!encounterVerified)
                        {
                            var space = V1SceneSetup.All<BatteryCombatSpace>(scene).Find(s => s.gameObject.activeInHierarchy);
                            Vector3 center = space.GetComponent<BoxCollider>().bounds.center;
                            Check(Physics.Raycast(center + Vector3.up * 8, Vector3.down, out var ground, 30, 1, QueryTriggerInteraction.Ignore), "Combat space has ground");
                            player.GetComponent<AutoLaser>().enabled = false;
                            Teleport(player, ground.point + Vector3.up * 0.15f);
                            var camera = V1SceneSetup.Find<ThirdPersonCamera>(scene); camera.enabled = false;
                            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0, 3, -5), Quaternion.Euler(15, 0, 0));
                            nextTick = EditorApplication.timeSinceStartup + 1; stage = 100; break;
                        }
                        player.GetComponent<AutoLaser>().enabled = true;
                        player.TakeDamage(7);
                        session.State.Progression.AddXp(2);
                        int killsBefore = session.State.KillCount;
                        var rewardTemplate = V1SceneSetup.All<MeleeEnemy>(scene)[0].gameObject;
                        var target = Object.Instantiate(rewardTemplate, player.transform.position + Vector3.right * 2, Quaternion.identity);
                        target.SetActive(true); target.GetComponent<EnemyHealth>().TakeDamage(10000);
                        Check(session.State.KillCount == killsBefore + 1, "Kill counted once");
                        var pickup = V1SceneSetup.Find<XpPickup>(scene);
                        Check(pickup != null, "Kill creates contact-collected XP");
                        Teleport(player, pickup.transform.position - Vector3.up * 0.5f);
                        stage++; break;
                    case 2:
                        Check(session.State.XP == 3, "Contact collects XP");
                        Check(flow.IsChoosing && Time.timeScale == 0 && !session.IsGameplayActive, "Upgrade pauses gameplay");
                        Check(V1SceneSetup.Find<UpgradeScreen>(scene).IsVisible, "Actual upgrade screen is visible and initialized");
                        pausedBattery = session.State.Battery; stage++; break;
                    case 3:
                        Check(Mathf.Approximately(session.State.Battery, pausedBattery), "Battery frozen during upgrade");
                        flow.Choose(0);
                        Check(session.State.Progression.Selections == 1 && !flow.IsChoosing, "Exactly one upgrade accepted");
                        session.State.Battery = 80; carriedHealth = player.Current;
                        Check(V1SceneSetup.Find<LevelPortal>(scene).TryTravel(), "Forest portal accepted");
                        stage++; break;
                    case 4:
                        Check(scene.path == V1SceneSetup.Scenes[1], "Forest portal loads V1 Maribor"); Validate(scene);
                        Check(player.Current == carriedHealth && session.State.Battery > 78 && session.State.Battery <= 80, "HP/battery carry across scenes");
                        Check(session.State.Progression.Selections == 1, "Upgrade carries across scenes");
                        Check(session.State.Gate(RouteLevel.City, RouteChoice.C) == GateState.RouteLock, "Route lock precedes persistent lock");
                        Select(scene, RouteChoice.A); stage++; break;
                    case 5:
                        Check(session.State.Choice(RouteLevel.City) == RouteChoice.A, "Physical City selection trigger");
                        Check(V1SceneSetup.Find<LevelPortal>(scene).TryTravel(), "City portal accepted"); stage++; break;
                    case 6:
                        Check(scene.path == V1SceneSetup.Scenes[2], "City portal loads V1 FERI"); Validate(scene);
                        Select(scene, RouteChoice.A); stage++; break;
                    case 7:
                        Check(session.State.Choice(RouteLevel.Feri) == RouteChoice.A, "Physical FERI selection trigger");
                        Teleport(player, V1SceneSetup.Find<V1BossArena>(scene).transform.position - Vector3.up * 0.5f); stage++; break;
                    case 8:
                        var boss = V1SceneSetup.Find<ProfessorBoss>(scene);
                        if (!SessionState.GetBool(Key + ".Professor", false))
                        {
                            Check(session.HasEnteredBossArena && boss.IsFighting, "Physical arena entry begins professor fight");
                            Check(session.State.Guilt.BossEntered, "Boss entry ends Guilt pressure");
                        }
                        if (professorSamples++ == 0)
                        {
                            professorStart = boss.transform.position;
                            var feedback = boss.GetComponent<EnemyFeedback>();
                            Check(feedback != null && feedback.enabled, "Professor has active hit/death feedback");
                            boss.GetComponent<EnemyHealth>().TakeDamage(1);
                            bool flashed = false;
                            var block = new MaterialPropertyBlock();
                            foreach (var renderer in boss.GetComponentsInChildren<Renderer>())
                            {
                                renderer.GetPropertyBlock(block, 0);
                                flashed |= block.HasColor(Shader.PropertyToID("_BaseColor"))
                                    || block.HasColor(Shader.PropertyToID("_Color"));
                            }
                            Check(flashed, "Professor accepted hit flashes model materials");
                            break;
                        }
                        if (professorSamples == 2)
                        {
                            professorPaused = boss.transform.position;
                            session.State.Guilt.GameplaySuspended = true;
                            break;
                        }
                        if (professorSamples == 3)
                        {
                            Check(boss.transform.position == professorPaused, "Professor patrol respects shared pause");
                            session.State.Guilt.GameplaySuspended = false;
                        }
                        var floor = (BoxCollider)new SerializedObject(boss).FindProperty("movementFloor").objectReferenceValue;
                        Bounds safe = floor.bounds;
                        Vector3 at = boss.transform.position;
                        Check(at.x > safe.min.x + 0.5f && at.x < safe.max.x - 0.5f
                            && at.z > safe.min.z + 0.5f && at.z < safe.max.z - 0.5f,
                            "Professor remains inside arena borders");
                        if (professorSamples < 60) break;
                        Check(Vector3.Distance(professorStart, at) > 1f, "Professor patrol moves during fight");
                        boss.GetComponent<EnemyHealth>().TakeDamage(10000);
                        Check(Object.FindObjectsByType<EnemyDeathBurst>().Length > 0,
                            "Professor death emits detached particle burst");
                        Check(flow.IsTerminal && Time.timeScale == 0, "Boss defeat shows terminal victory screen");
                        if (SessionState.GetBool(Key + ".Professor", false))
                        {
                            EditorApplication.ExitPlaymode();
                            break;
                        }
                        flow.Retry(); stage++; break;
                    case 9:
                        Check(scene.path == V1SceneSetup.Scenes[0], "Victory retry returns to Pohorje");
                        Check(player.Current == 100 && session.State.XP == 0 && session.State.Progression.Selections == 0, "New run clears temporary progression");
                        Check(session.State.AvailableUnlocks.ForestC && session.State.AvailableUnlocks.CityC && session.State.AvailableUnlocks.FeriC, "All earned unlocks become available next run");
                        Select(scene, RouteChoice.C); stage++; break;
                    case 10:
                        Check(session.State.Choice(RouteLevel.Forest) == RouteChoice.C, "Unlocked Forest C is selectable");
                        V1SceneSetup.Find<LevelPortal>(scene).TryTravel(); stage++; break;
                    case 11:
                        Check(scene.path == V1SceneSetup.Scenes[1], "Second run reaches City");
                        Select(scene, RouteChoice.C); stage++; break;
                    case 12:
                        Check(session.State.Choice(RouteLevel.City) == RouteChoice.C, "City C selected");
                        Check(!V1SceneSetup.Find<LevelPortal>(scene).TryTravel(), "City C portal requires side escape");
                        Teleport(player, Event(scene, V1RouteEventKind.CityReveal).transform.position - Vector3.up * 0.5f); stage++; break;
                    case 13:
                        Check(session.State.CityRetreatStarted && session.State.Guilt.ScriptedSequenceActive, "City C reveal suspends ambient Guilt");
                        Teleport(player, Event(scene, V1RouteEventKind.CitySideDoor).transform.position - Vector3.up * 0.5f); stage++; break;
                    case 14:
                        Check(session.State.CitySideEscapeCompleted && !session.State.Guilt.ScriptedSequenceActive, "Physical side door completes escape");
                        Check(V1SceneSetup.Find<LevelPortal>(scene).TryTravel(), "Escape opens progression"); stage++; break;
                    case 15:
                        Check(scene.path == V1SceneSetup.Scenes[2], "Secret run reaches FERI");
                        Select(scene, RouteChoice.C); stage++; break;
                    case 16:
                        Check(session.State.Choice(RouteLevel.Feri) == RouteChoice.C, "FERI C selected");
                        Teleport(player, Event(scene, V1RouteEventKind.PowerOff).transform.position - Vector3.up * 0.5f); stage++; break;
                    case 17:
                        Check(Event(scene, V1RouteEventKind.PowerOff).PlayerInside, "POWER OFF trigger detects the player");
                        if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
                        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        InputSystem.EnableDevice(Keyboard.current);
                        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(UnityEngine.InputSystem.Key.E));
                        InputSystem.Update(); stage++; break;
                    case 18:
                        Check(session.State.PowerOff, "E activates POWER OFF in its physical volume (inside=" + Event(scene, V1RouteEventKind.PowerOff).PlayerInside + ", active=" + session.IsGameplayActive + ", key=" + Keyboard.current.eKey.isPressed + ")");
                        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                        Teleport(player, V1SceneSetup.Find<V1BossArena>(scene).transform.position - Vector3.up * 0.5f); stage++; break;
                    case 19:
                        var modifiedBoss = V1SceneSetup.Find<ProfessorBoss>(scene).GetComponent<EnemyHealth>();
                        Check(modifiedBoss.Current == Mathf.RoundToInt(modifiedBoss.Maximum * 0.85f), "POWER OFF sets boss starting HP to 85%");
                        player.TakeDamage(10000);
                        Check(flow.IsTerminal && Time.timeScale == 0, "Death opens terminal screen and suspends combat");
                        flow.Retry(); stage++; break;
                    case 20:
                        Check(scene.path == V1SceneSetup.Scenes[0] && player.Current == 100 && session.State.XP == 0, "Death retry resets run at Pohorje");
                        Check(Time.timeScale == 1 && !flow.IsTerminal, "Retry resumes gameplay");
                        session.State.Progression.AddXp(33);
                        foreach (var kind in new[] { UpgradeId.DoubleTrouble, UpgradeId.PanicMode, UpgradeId.TurboSkiro, UpgradeId.TurboSkiro, UpgradeId.RaveOverdrive })
                            Check(FixtureChoose(session.State.Progression, kind), "Prepare capped late offer");
                        player.GetComponent<LightBattery>().enabled = false; // Hold charge full for this eligibility fixture.
                        session.State.Battery = session.State.LightBattery.Capacity;
                        stage++; break;
                    case 21:
                        var upgrade = V1SceneSetup.Find<UpgradeScreen>(scene);
                        Check(flow.IsChoosing && upgrade.IsVisible, "Late eligible offer is visible");
                        var buttons = new SerializedObject(upgrade).FindProperty("buttons");
                        Check(((UnityEngine.UI.Button)buttons.GetArrayElementAtIndex(0).objectReferenceValue).gameObject.activeSelf
                            && ((UnityEngine.UI.Button)buttons.GetArrayElementAtIndex(1).objectReferenceValue).gameObject.activeSelf
                            && !((UnityEngine.UI.Button)buttons.GetArrayElementAtIndex(2).objectReferenceValue).gameObject.activeSelf, "Only two eligible cards displayed");
                        upgrade.RequestChoice(2);
                        Check(session.State.Progression.Selections == 5, "Hidden card cannot be selected");
                        upgrade.RequestChoice(0);
                        Check(session.State.Progression.IsMaxLevel && !flow.IsChoosing && Time.timeScale == 1, "Final choice resumes at maximum level");
                        EditorApplication.ExitPlaymode(); break;
                    case 100:
                        var occupied = V1SceneSetup.All<BatteryCombatSpace>(scene).Find(s => s.gameObject.activeInHierarchy);
                        Check(occupied.State.Occupied && session.State.Guilt.AccumulatedCombatSeconds > 0f, "Physical combat occupancy activates battery and Guilt clocks");
                        Check(EnemyHealth.Active.Count > 0 && EnemyHealth.Active.Count <= 4, "Selected route replenishes safely within its target count");
                        encounterVerified = true; stage = 1; break;
                }
            }
            catch (Exception e) { Fail("stage " + stage + ": " + e.Message + "\n" + e.StackTrace); }
        }

        private sealed class PickRandom : System.Random
        {
            private readonly int first;
            private bool picked;
            public PickRandom(UpgradeId id, UpgradeState state)
            {
                for (UpgradeId candidate = 0; candidate < id; candidate++) if (state.Eligible(candidate)) first++;
            }
            public override int Next(int maxValue) { if (picked) return 0; picked = true; return first % maxValue; }
        }
        private static bool FixtureChoose(UpgradeState state, UpgradeId kind)
        {
            if (!state.CreateOffer(new PickRandom(kind, state))) return false;
            for (int i = 0; i < state.OfferCount; i++) if (state.Offer(i) == kind) return state.TrySelect(i, state.OfferVersion);
            return false;
        }

        private static V1RouteEvent Event(Scene scene, V1RouteEventKind kind)
        {
            foreach (var item in V1SceneSetup.All<V1RouteEvent>(scene))
                if (new SerializedObject(item).FindProperty("kind").enumValueIndex == (int)kind) return item;
            throw new Exception("Missing route event " + kind);
        }
        private static void Select(Scene scene, RouteChoice choice)
        {
            foreach (var gate in V1SceneSetup.All<RouteGate>(scene))
                if (new SerializedObject(gate).FindProperty("choice").enumValueIndex == (int)choice)
                { Teleport(V1SceneSetup.Find<PlayerHealth>(scene), gate.GetComponent<BoxCollider>().bounds.center - Vector3.up * 0.8f); return; }
            throw new Exception("Missing gate " + choice);
        }
        private static void Teleport(PlayerHealth player, Vector3 point)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false; player.transform.position = point; controller.enabled = true;
            Physics.SyncTransforms(); controller.Move(Vector3.forward * 0.01f);
        }
        internal static void ValidateEnemyEffects(Scene scene)
        {
            int templateRoots = 0;
            foreach (var transform in V1SceneSetup.All<Transform>(scene))
                if (transform.name == "Enemy Attack Templates")
                {
                    templateRoots++;
                    Check(!transform.gameObject.activeSelf, "Attack templates remain inactive");
                }
            Check(templateRoots == 1, "Exactly one enemy attack template root");
            foreach (var health in V1SceneSetup.All<EnemyHealth>(scene))
                Check(new SerializedObject(health).FindProperty("showCombatFeedback").boolValue
                    || (health.TryGetComponent<EnemyFeedback>(out var feedback) && feedback.enabled),
                    "Hit and death feedback enabled: " + health.name);
            foreach (var ranged in V1SceneSetup.All<RangedEnemy>(scene))
            {
                var projectile = (EnemyProjectile)new SerializedObject(ranged).FindProperty("projectilePrefab").objectReferenceValue;
                ValidateProjectileEffect(projectile, scene);
                Check(projectile.name == (ranged.name.StartsWith("ExamPaper", StringComparison.Ordinal)
                    ? "Proj_ExamPaper" : "Proj_WatcherOrb"), "Correct projectile visual: " + ranged.name);
            }
            foreach (var boss in V1SceneSetup.All<ProfessorBoss>(scene))
            {
                ValidateProjectileEffect((EnemyProjectile)new SerializedObject(boss).FindProperty("sheetPrefab").objectReferenceValue, scene);
                var view = boss.GetComponent<ProfessorAttackView>();
                Check(view != null && view.enabled, "Professor attack view enabled");
                var serialized = new SerializedObject(view);
                foreach (string field in new[] { "visual", "warning", "countdown", "shockwave", "castPulse" })
                    Check(serialized.FindProperty(field).objectReferenceValue != null, "Professor effect assignment: " + field);
            }
            Debug.Log("V1_ENEMY_EFFECTS_VALIDATED " + scene.path);
        }

        private static void ValidateProjectileEffect(EnemyProjectile projectile, Scene scene)
        {
            Check(projectile != null && projectile.gameObject.scene == scene, "Scene-local projectile template");
            Check(!projectile.gameObject.activeInHierarchy, "Projectile template cannot fire or update");
            var serialized = new SerializedObject(projectile);
            Check(serialized.FindProperty("trailMaterial").objectReferenceValue != null, "Projectile trail/impact material assigned");
            Check(serialized.FindProperty("trailWidth").floatValue > 0f, "Visible projectile trail width");
        }

        private static void Validate(Scene scene)
        {
            ValidateEnemyEffects(scene);
            var battery = new SerializedObject(V1SceneSetup.Find<LightBattery>(scene));
            var lamp = battery.FindProperty("surroundingLight").objectReferenceValue as Light;
            Check(battery.FindProperty("headlightRange").floatValue == 28f
                && battery.FindProperty("headlightBrightnessMultiplier").floatValue == 12f,
                "Wide, surrounding V1 scooter light is explicitly configured");
            Check(lamp != null && lamp.type == LightType.Point && lamp.color == Color.white && !lamp.useColorTemperature
                && lamp.shadows == LightShadows.None, "V1 scooter beam stays white without additional shadows");
            Check(battery.FindProperty("riderFillPosition").vector3Value == new Vector3(0f, 1.6f, -0.9f)
                && battery.FindProperty("nearbyBrightnessMultiplier").floatValue == 1.3f,
                "Restrained V1 rider fill avoids a backpack hotspot");
            var darkness = new SerializedObject(V1SceneSetup.Find<ScooterDarkness>(scene));
            Check(darkness.FindProperty("chargedRadiusMultiplier").floatValue == 3f
                && darkness.FindProperty("darknessOpacity").floatValue == 0.3f,
                "V1 darkness opening accommodates the wide beam");
            Check(V1SceneSetup.All<RouteGate>(scene).Count == 3, "Exactly three route gates");
            Check(V1SceneSetup.All<PlayerHud>(scene).Count == 1, "One real HUD");
            Check(V1SceneSetup.All<PlayerGameplayFeedback>(scene).Count == 1, "One production feedback bridge");
            Check(V1SceneSetup.All<BokiModelTestEffects>(scene).Count == 0 && V1SceneSetup.All<EnemyCombatPreview>(scene).Count == 0, "No test controls/spawners");
            foreach (var gate in V1SceneSetup.All<RouteGate>(scene))
            {
                var so = new SerializedObject(gate);
                foreach (string field in new[] { "manager", "blocker", "selectionTrigger", "sign", "exitBlocker", "exitVisual", "encounterRoot", "interiorRoot" })
                    Check(so.FindProperty(field).objectReferenceValue != null, "Gate assignment " + field);
            }
        }
        private static void CheckBranches(Scene scene, RouteChoice selected)
        {
            foreach (var gate in V1SceneSetup.All<RouteGate>(scene))
            {
                var so = new SerializedObject(gate);
                bool chosen = so.FindProperty("choice").enumValueIndex == (int)selected;
                var encounter = (GameObject)so.FindProperty("encounterRoot").objectReferenceValue;
                var front = (Collider)so.FindProperty("blocker").objectReferenceValue;
                var rear = (Collider)so.FindProperty("exitBlocker").objectReferenceValue;
                Check(encounter.activeSelf == chosen && front.enabled != chosen && rear.enabled != chosen, "Only the selected branch opens and activates");
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Fail(string message) { failed = true; failure = message; EditorApplication.ExitPlaymode(); }
    }
}
