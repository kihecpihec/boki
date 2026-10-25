using System;
using Boki.Combat;
using Boki.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Uses a temporary empty scene; never saves or upgrades a map or prefab.
    [InitializeOnLoad]
    public static class EnemyFeedbackChecks
    {
        private const string Key = "Boki.EnemyFeedback.Checks";
        [Serializable] private sealed class Setup
        {
            public string[] paths;
            public bool[] loaded;
            public bool[] active;
        }
        private static int stage;
        private static double started;
        private static bool failed;
        private static Material material;
        private static EnemyHealth enemy;
        private static Renderer mesh;
        private static Color flash;
        private static ParticleSystem burst;
        private static float pausedParticleTime;
        private static int damageEvents;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color RestColor = new Color(0.2f, 0.3f, 0.4f, 1f);

        static EnemyFeedbackChecks()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            EditorApplication.update += Tick;
        }

        // Batch: -executeMethod Boki.Editor.EnemyFeedbackChecks.Run (without -quit).
        [MenuItem("Boki/Enemy Models/Run Feedback Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running feedback checks.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard dirty scenes before running feedback checks.");
            SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
            var saved = new Setup { paths = new string[scenes.Length], loaded = new bool[scenes.Length],
                active = new bool[scenes.Length] };
            for (int i = 0; i < scenes.Length; i++)
            {
                saved.paths[i] = scenes[i].path;
                saved.loaded[i] = scenes[i].isLoaded;
                saved.active[i] = scenes[i].isActive;
            }
            SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(saved));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void StateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                stage = damageEvents = 0;
                failed = false;
                started = EditorApplication.timeSinceStartup;
            }
            if (change != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            Setup setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Key + ".Scenes", ""));
            if (setup != null && setup.paths.Length > 0 && !string.IsNullOrEmpty(setup.paths[0]))
            {
                var scenes = new SceneSetup[setup.paths.Length];
                for (int i = 0; i < scenes.Length; i++)
                    scenes[i] = new SceneSetup { path = setup.paths[i], isLoaded = setup.loaded[i], isActive = setup.active[i] };
                EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.EraseString(Key + ".Scenes");
            Debug.Log(failed ? "ENEMY_FEEDBACK_FAILED" : "ENEMY_FEEDBACK_PASSED");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }

        private static EnemyHealth CreateEnemy()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.GetComponent<Renderer>().sharedMaterial = material;
            var original = new MaterialPropertyBlock();
            original.SetColor(BaseColor, RestColor);
            original.SetFloat("_FeedbackCheck", 0.7f);
            root.GetComponent<Renderer>().SetPropertyBlock(original, 0);
            return root.AddComponent<EnemyHealth>();
        }

        private static Color ReadColor()
        {
            var block = new MaterialPropertyBlock();
            mesh.GetPropertyBlock(block, 0);
            Check(Mathf.Approximately(block.GetFloat("_FeedbackCheck"), 0.7f), "Existing property block lost.");
            return block.GetColor(BaseColor);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || failed || stage == 99) return;
            try
            {
                double elapsed = EditorApplication.timeSinceStartup - started;
                if (elapsed > 15) throw new InvalidOperationException("Feedback check timed out at stage " + stage);
                switch (stage)
                {
                    case 0:
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        material.SetColor(BaseColor, Color.blue);
                        enemy = CreateEnemy();
                        mesh = enemy.GetComponent<Renderer>();
                        Check(enemy.GetComponent<EnemyFeedback>() != null, "Feedback was not attached automatically.");
                        enemy.Damaged += () => damageEvents++;
                        enemy.TakeDamage(0);
                        enemy.TakeDamage(-1);
                        Check(damageEvents == 0 && ReadColor() == RestColor, "Rejected damage produced feedback.");
                        enemy.TakeDamage(1);
                        Check(damageEvents == 1 && ReadColor() != RestColor, "Valid hit did not flash.");
                        flash = ReadColor();
                        RunSession.Ensure().State.Guilt.GameplaySuspended = true;
                        Advance();
                        break;
                    case 1:
                        if (elapsed < 0.25) return;
                        Check(ReadColor() == flash, "Hit flash advanced during shared pause.");
                        RunSession.Instance.State.Guilt.GameplaySuspended = false;
                        Advance();
                        break;
                    case 2:
                        if (ReadColor() != RestColor) return;
                        Check(material.GetColor(BaseColor) == Color.blue, "Shared material was modified.");
                        enemy.TakeDamage(1);
                        enemy.gameObject.SetActive(false);
                        Check(ReadColor() == RestColor, "Disabling enemy left a flash override.");
                        enemy.gameObject.SetActive(true);
                        for (int i = 0; i < 10; i++)
                        {
                            EnemyHealth victim = CreateEnemy();
                            victim.TakeDamage(int.MaxValue);
                            victim.TakeDamage(int.MaxValue);
                        }
                        var bursts = Object.FindObjectsByType<EnemyDeathBurst>();
                        Check(bursts.Length == 6, "Death bursts did not respect the simultaneous cap.");
                        burst = bursts[0].GetComponent<ParticleSystem>();
                        Check(burst.particleCount == 10, "Death burst did not emit particles.");
                        Check(burst.isPaused, "Death particles must use explicit simulation.");
                        pausedParticleTime = burst.time;
                        Check(burst.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name
                            == "Boki/Enemy Feedback Particles", "Particle shader resource missing.");
                        RunSession.Instance.State.Guilt.GameplaySuspended = true;
                        Advance();
                        break;
                    case 3:
                        if (elapsed < 0.5) return;
                        Check(burst != null && burst.particleCount == 10 && burst.time == pausedParticleTime,
                            "Detached death burst advanced during shared pause.");
                        RunSession.Instance.State.Guilt.GameplaySuspended = false;
                        Advance();
                        break;
                    case 4:
                        if (Object.FindObjectsByType<EnemyDeathBurst>().Length != 0) return;
                        Object.Destroy(material);
                        stage = 99;
                        EditorApplication.ExitPlaymode();
                        break;
                }
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
                EditorApplication.ExitPlaymode();
            }
        }

        private static void Advance() { stage++; started = EditorApplication.timeSinceStartup; }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
