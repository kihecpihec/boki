using System;
using Boki.Battery;
using Boki.Combat;
using Boki.Core;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Runs against the authored scene so missing references and real pickup contacts are exercised.
    [InitializeOnLoad]
    public static class BokiModelFeedbackChecks
    {
        private const string Key = "Boki.ModelFeedback.Checks";
        private static int stage;
        private static double started;
        private static bool failed;
        private static LightBattery battery;
        private static Light lamp;
        private static BokiPlayerEffects effects;
        private static ParticleSystem glow, impact;
        private static BatteryCharge charge;
        private static float fullIntensity, pausedTime, restVisualHeight;
        private static EnemyHealth laserTarget;
        private static Vector3 colliderScale;

        static BokiModelFeedbackChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        [MenuItem("Boki/Player Model/Run Feedback Checks")]
        public static void Run() => RunScene(BokiModelTestSetup.ScenePath);

        [MenuItem("Boki/Player Model/Run Solo Feedback Checks")]
        public static void RunSolo() => RunScene(BokiSoloSetup.ScenePath);

        private static void RunScene(string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            var saved = new SavedSetup { paths = new string[scenes.Length], loaded = new bool[scenes.Length], active = new bool[scenes.Length] };
            for (int i = 0; i < scenes.Length; i++)
            {
                saved.paths[i] = scenes[i].path;
                saved.loaded[i] = scenes[i].isLoaded;
                saved.active[i] = scenes[i].isActive;
            }
            SessionState.SetString(Key + ".Setup", JsonUtility.ToJson(saved));
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<ScooterController>(true);
                if (controller != null)
                    SessionState.SetFloat(Key + ".VisualHeight", controller.transform.Find("Visuals").localScale.y);
            }
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        [Serializable] private sealed class SavedSetup
        {
            public string[] paths;
            public bool[] loaded;
            public bool[] active;
        }

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
            var saved = JsonUtility.FromJson<SavedSetup>(SessionState.GetString(Key + ".Setup", ""));
            if (saved != null && saved.paths.Length > 0 && !string.IsNullOrEmpty(saved.paths[0]))
            {
                var scenes = new SceneSetup[saved.paths.Length];
                for (int i = 0; i < scenes.Length; i++)
                    scenes[i] = new SceneSetup { path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.active[i] };
                EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.EraseString(Key + ".Setup");
            Debug.Log(failed ? "BOKI_MODEL_FEEDBACK_FAILED" : "BOKI_MODEL_FEEDBACK_PASSED");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || stage == 99) return;
            try
            {
                double elapsed = EditorApplication.timeSinceStartup - started;
                if (elapsed > 20) throw new InvalidOperationException("Model feedback checks timed out at stage " + stage);
                switch (stage)
                {
                    case 0:
                        battery = Object.FindAnyObjectByType<LightBattery>();
                        if (battery == null || elapsed < 0.2) return;
                        effects = battery.GetComponent<BokiPlayerEffects>();
                        glow = battery.transform.Find("Visuals/Pickup Glow").GetComponent<ParticleSystem>();
                        impact = battery.transform.Find("Laser Impact").GetComponent<ParticleSystem>();
                        var settings = new SerializedObject(battery);
                        lamp = (Light)settings.FindProperty("surroundingLight").objectReferenceValue;
                        Require(battery.enabled && effects.enabled && lamp.enabled, "Scene feedback must be active.");
                        var mount = (Transform)settings.FindProperty("headlightMount").objectReferenceValue;
                        Require(mount != null && mount.parent.name == "Socket_Headlight"
                            && lamp.transform.parent == mount && lamp.transform.localPosition == Vector3.zero,
                            "Headlight must stay attached to the authored scooter socket in Play Mode.");
                        var fill = mount.Find("Scooter Nearby Fill");
                        Require(fill != null && Vector3.Distance(fill.position, mount.position) < 0.5f,
                            "Nearby fill must remain on the scooter despite FBX socket scale.");
                        var laserSettings = new SerializedObject(battery.GetComponent<AutoLaser>());
                        var origin = (Transform)laserSettings.FindProperty("beamOrigin").objectReferenceValue;
                        Require(origin.parent.name == "Socket_LaserMuzzle", "Laser must originate on the scooter muzzle.");
                        colliderScale = battery.transform.localScale;
                        battery.State.Current = 100;
                        Next();
                        break;
                    case 1:
                        if (elapsed < 0.05) return;
                        fullIntensity = lamp.intensity;
                        battery.State.Current = 20;
                        Next();
                        break;
                    case 2:
                        if (elapsed < 0.05) return;
                        Require(lamp.enabled && lamp.intensity < fullIntensity * 0.5f, "Low battery must visibly weaken the lamp.");
                        battery.State.Current = 0;
                        Next();
                        break;
                    case 3:
                        if (elapsed < 0.05) return;
                        Require(!lamp.enabled, "Empty battery must extinguish the lamp.");
                        battery.State.Current = 50;
                        charge = Object.FindAnyObjectByType<BatteryCharge>();
                        var controller = battery.GetComponent<CharacterController>();
                        controller.enabled = false;
                        battery.transform.position = charge.transform.position - Vector3.up * 0.6f;
                        controller.enabled = true;
                        Physics.SyncTransforms();
                        Next();
                        break;
                    case 4:
                        if (charge.Available) return;
                        Require(battery.State.Current > 70 && glow.particleCount > 0, "Contact recharge must produce a pickup glow.");
                        RunSession.Instance.State.Guilt.GameplaySuspended = true;
                        pausedTime = glow.time;
                        Next();
                        break;
                    case 5:
                        if (elapsed < 0.2) return;
                        Require(glow.time == pausedTime, "Shared pause must freeze pickup particles.");
                        RunSession.Instance.State.Guilt.GameplaySuspended = false;
                        charge.gameObject.SetActive(false);
                        glow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        RunSession.Instance.State.XP++;
                        Next();
                        break;
                    case 6:
                        if (elapsed < 0.05) return;
                        Require(glow.particleCount > 0, "An XP gain must produce a glow.");
                        var guilt = RunSession.Instance.State.Guilt;
                        guilt.SetCombatSpaceOccupied(123, true);
                        guilt.Tick(20);
                        Next();
                        break;
                    case 7:
                        if (elapsed < 0.05) return;
                        Require(battery.WarningStrength == 1f
                            && battery.GetComponent<ScooterPresentation>().GuiltWarningStrength == 1f,
                            "Shared Guilt warning must drive the headlight and camera.");
                        RunSession.Instance.State.Guilt.Reset();
                        battery.GetComponent<BokiModelTestEffects>().SpawnLaserTarget();
                        laserTarget = Object.FindAnyObjectByType<EnemyHealth>();
                        Require(laserTarget != null, "Test target must spawn.");
                        laserTarget.TakeDamage(laserTarget.Current - 1);
                        Next();
                        break;
                    case 8:
                        if (laserTarget != null || impact.particleCount == 0) return;
                        var beamSettings = new SerializedObject(battery.GetComponent<AutoLaser>());
                        var beam = (LineRenderer)beamSettings.FindProperty("beam").objectReferenceValue;
                        var muzzle = (Transform)beamSettings.FindProperty("beamOrigin").objectReferenceValue;
                        Require(beam.enabled, "A laser hit must show the short beam and impact together.");
                        Require(Vector3.Distance(beam.GetPosition(0), muzzle.position) < 0.001f,
                            "The visible laser pulse must start at the scooter emitter.");
                        Require(battery.transform.localScale == colliderScale, "Feedback must not scale the collision root.");
                        var visual = battery.transform.Find("Visuals");
                        restVisualHeight = SessionState.GetFloat(Key + ".VisualHeight", 1f);
                        var body = battery.GetComponent<CharacterController>();
                        body.enabled = false;
                        battery.transform.position += Vector3.up * 2f;
                        body.enabled = true;
                        Physics.SyncTransforms();
                        Next();
                        break;
                    case 9:
                        if (battery.transform.Find("Visuals").localScale.y >= restVisualHeight * 0.98f) return;
                        Require(battery.transform.localScale == colliderScale, "Landing squash must preserve the collision root scale.");
                        Next();
                        break;
                    case 10:
                        if (elapsed < 0.3) return;
                        // Batch frame rate varies; let active simulation finish the recovery.
                        if (!Mathf.Approximately(battery.transform.Find("Visuals").localScale.y, restVisualHeight)) return;
                        effects.enabled = false;
                        Require(glow.particleCount == 0, "Disabling effects must clear pickup particles.");
                        stage = 99;
                        EditorApplication.ExitPlaymode();
                        break;
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

        private static void Next() { stage++; started = EditorApplication.timeSinceStartup; }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
