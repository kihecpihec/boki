using System;
using System.IO;
using Boki.Battery;
using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.Routes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    [InitializeOnLoad]
    public static class ScooterLightChecks
    {
        private const string Key = "Boki.LightBattery.Checking";
        [Serializable]
        private sealed class SavedSetup
        {
            public string[] paths;
            public bool[] loaded;
            public bool[] active;
        }
        private static int stage;
        private static double stageStarted;
        private static double started;
        private static bool failed;
        private static LightBattery battery;
        private static BatteryCharge[] charges;
        private static BatteryCharge first;
        private static CharacterController player;
        private static float pausedCharge, pausedTimer, carriedCharge;
        private static Light lamp;
        private static Light fill;
        private static Camera previewCamera;
        private static RenderTexture previewTexture;
        private static float chargedIntensity, chargedRange;

        static ScooterLightChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        [MenuItem("Boki/Light Battery/Capture Player Preview")]
        public static void CapturePreview() => Begin(true);

        [MenuItem("Boki/Light Battery/Run Gameplay Checks")]
        public static void Run() => Begin(false);

        private static void Begin(bool capture)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            var saved = new SavedSetup { paths = new string[setup.Length], loaded = new bool[setup.Length], active = new bool[setup.Length] };
            for (int i = 0; i < setup.Length; i++)
            {
                saved.paths[i] = setup[i].path;
                saved.loaded[i] = setup[i].isLoaded;
                saved.active[i] = setup[i].isActive;
            }
            SessionState.SetString(Key + ".Previous", JsonUtility.ToJson(saved));
            SessionState.SetBool(Key + ".Capture", capture);
            EditorSceneManager.OpenScene(capture
                ? "Assets/Scenes/Player_Model_Test.unity" : RunSession.PohorjeScene, OpenSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                started = stageStarted = EditorApplication.timeSinceStartup;
                stage = 0;
                failed = false;
            }
            if (mode != PlayModeStateChange.EnteredEditMode) return;
            bool captured = SessionState.GetBool(Key + ".Capture", false);
            SessionState.SetBool(Key, false);
            SessionState.EraseBool(Key + ".Capture");
            var saved = JsonUtility.FromJson<SavedSetup>(SessionState.GetString(Key + ".Previous", ""));
            if (saved != null && saved.paths.Length > 0 && !string.IsNullOrEmpty(saved.paths[0]))
            {
                var setup = new SceneSetup[saved.paths.Length];
                for (int i = 0; i < setup.Length; i++)
                    setup[i] = new SceneSetup { path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.active[i] };
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.EraseString(Key + ".Previous");
            Debug.Log(failed ? "SCOOTER_LIGHT_CHECKS_FAILED"
                : captured ? "SCOOTER_LIGHT_PREVIEW_CAPTURED" : "SCOOTER_LIGHT_CHECKS_PASSED");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup - started > 60f) { Finish("Timed out."); return; }
            if (EditorApplication.timeSinceStartup - stageStarted < 0.5f) return;
            try
            {
                if (SessionState.GetBool(Key + ".Capture", false)) { CaptureTick(); return; }
                switch (stage)
                {
                    case 0:
                        battery = Object.FindAnyObjectByType<LightBattery>();
                        Require(battery != null && battery.isActiveAndEnabled, "Player light must be active.");
                        player = battery.GetComponent<CharacterController>();
                        battery.GetComponent<ScooterController>().enabled = false;
                        Object.FindAnyObjectByType<AutoLaser>().enabled = false;
                        Object.FindAnyObjectByType<ThirdPersonCamera>().enabled = false;
                        Require(Object.FindAnyObjectByType<RouteManager>().Select(RouteChoice.A), "Select Forest A.");
                        foreach (MeleeEnemy enemy in Object.FindObjectsByType<MeleeEnemy>()) enemy.enabled = false;
                        charges = Object.FindObjectsByType<BatteryCharge>();
                        Require(charges.Length == 3, "Only the selected branch's three charges may be active.");
                        first = charges[0];
                        lamp = battery.GetComponentInChildren<Light>();
                        foreach (Light light in battery.GetComponentsInChildren<Light>())
                            if (light.name == "Scooter Nearby Fill") fill = light;
                        Require(lamp.type == LightType.Point && fill != null && fill.enabled,
                            "Charged scooter has a headlight and nearby fill.");
                        Require(battery.State.Current < 100f && lamp.enabled, "Active gameplay drains battery and keeps charged light on.");
                        RunSession.Instance.State.Guilt.GameplaySuspended = true;
                        battery.State.Current = 100f;
                        Require(fill.type == LightType.Point && fill.color == Color.white && lamp.color == Color.white
                            && lamp.range > 20f && lamp.renderingLayerMask == 1u
                            && fill.renderingLayerMask == 2u,
                            "The scooter casts white light all around; a separate layer keeps rider lighting subtle.");
                        Teleport(first.transform.position - Vector3.up * 0.6f);
                        Next();
                        break;
                    case 1:
                        Require(first.Available && battery.State.Current == 100f, "Paused/full contact leaves the charge available.");
                        battery.State.Current = 50f;
                        RunSession.Instance.State.Guilt.GameplaySuspended = false;
                        Next();
                        break;
                    case 2:
                        Require(!first.Available && battery.State.Current > 72f && battery.State.Current <= 75f,
                            "Physics contact must collect +25 exactly once.");
                        RunSession.Instance.State.Guilt.GameplaySuspended = true;
                        pausedCharge = battery.State.Current;
                        BatteryCombatSpace space = first.GetComponentInParent<BatteryCombatSpace>();
                        int slot = new SerializedObject(first).FindProperty("slot").intValue;
                        pausedTimer = space.State.Remaining(slot);
                        Next();
                        break;
                    case 3:
                        Require(battery.State.Current == pausedCharge, "Suspension freezes drain.");
                        var firstSpace = first.GetComponentInParent<BatteryCombatSpace>();
                        int firstSlot = new SerializedObject(first).FindProperty("slot").intValue;
                        Require(firstSpace.State.Remaining(firstSlot) == pausedTimer, "Suspension freezes charge recharge.");
                        RunSession.Instance.State.Guilt.GameplaySuspended = false;
                        Teleport(new Vector3(0, 0.1f, -12));
                        Next();
                        break;
                    case 4:
                        var leftSpace = first.GetComponentInParent<BatteryCombatSpace>();
                        Require(!leftSpace.State.Occupied, "Leaving the volume clears occupancy.");
                        pausedTimer = leftSpace.State.Remaining(new SerializedObject(first).FindProperty("slot").intValue);
                        battery.State.Current = 0f;
                        Next();
                        break;
                    case 5:
                        Require(!lamp.enabled && fill.enabled && fill.intensity > 0f && fill.intensity <= 0.15f
                            && fill.range <= 2.5f && fill.color == Color.white && battery.EnemySpeedMultiplier == 1.3f,
                            "Empty battery leaves only a faint white rider fill; enemy movement multiplier remains 1.3.");
                        var absentSpace = first.GetComponentInParent<BatteryCombatSpace>();
                        Require(absentSpace.State.Remaining(new SerializedObject(first).FindProperty("slot").intValue) == pausedTimer,
                            "Recharge cannot advance while outside the space.");
                        BatteryCharge recovery = charges[1];
                        Teleport(recovery.transform.position - Vector3.up * 0.6f);
                        Next();
                        break;
                    case 6:
                        Require(lamp.enabled && fill.enabled && battery.State.Current > 22f && battery.EnemySpeedMultiplier == 1f,
                            "A reachable pickup restores the light and ordinary enemy speed from empty.");
                        carriedCharge = battery.State.Current;
                        Require(RunSession.Instance.Advance(RouteLevel.Forest), "Advance to City.");
                        Next();
                        break;
                    case 7:
                        battery = Object.FindAnyObjectByType<LightBattery>();
                        Require(SceneManager.GetActiveScene().path == RunSession.MariborScene, "City scene loaded.");
                        Require(battery.State.Current <= carriedCharge && battery.State.Current > carriedCharge - 5f,
                            "Partial charge carries through scene loading and the new player binds.");
                        RunSession.Instance.State.Health.TakeDamage(100);
                        carriedCharge = battery.State.Current;
                        Next();
                        break;
                    case 8:
                        Require(battery.State.Current == carriedCharge && !lamp, "Death freezes battery and the old scene light was destroyed.");
                        Require(RunSession.Instance.Retry(), "Retry starts a fresh run.");
                        Next();
                        break;
                    case 9:
                        Require(SceneManager.GetActiveScene().path == RunSession.PohorjeScene, "Retry returns to Pohorje.");
                        Require(RunSession.Instance.State.Health.Current == 100 && RunSession.Instance.State.Battery > 95f,
                            "Retry resets HP and battery before active drain resumes.");
                        Require(RunSession.Instance.State.Choice(RouteLevel.Forest) == RouteChoice.None,
                            "Retry clears current-run choices.");
                        Finish(null);
                        break;
                }
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }
        private static void CaptureTick()
        {
            switch (stage)
            {
                case 0:
                    battery = Object.FindAnyObjectByType<LightBattery>();
                    battery.GetComponent<ScooterController>().enabled = false;
                    ThirdPersonCamera follow = Object.FindAnyObjectByType<ThirdPersonCamera>();
                    follow.enabled = false;
                    follow.transform.position = battery.transform.position + new Vector3(3f, 3f, -5f);
                    follow.transform.LookAt(battery.transform.position + Vector3.up);
                    previewCamera = follow.GetComponent<Camera>();
                    previewTexture = new RenderTexture(1280, 720, 24);
                    previewCamera.targetTexture = previewTexture;
                    foreach (Canvas canvas in Object.FindObjectsByType<Canvas>())
                    {
                        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = previewCamera;
                        canvas.planeDistance = 1f;
                    }
                    RunSession.Instance.State.Guilt.GameplaySuspended = true;
                    battery.State.Current = 100f;
                    Next();
                    break;
                case 1:
                    if (EditorApplication.timeSinceStartup - stageStarted < 1.0) return;
                    foreach (Light light in battery.GetComponentsInChildren<Light>())
                        Debug.Log($"SCOOTER_PREVIEW {light.name} position={light.transform.position} forward={light.transform.forward} intensity={light.intensity} range={light.range} player={battery.transform.position}");
                    lamp = new SerializedObject(battery).FindProperty("surroundingLight").objectReferenceValue as Light;
                    Require(lamp != null && lamp.type == LightType.Point && lamp.range > 27f,
                        "The model scene casts light all around from the scooter.");
                    foreach (SkinnedMeshRenderer renderer in battery.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        Require(renderer.renderingLayerMask == 2u && (renderer.renderingLayerMask & lamp.renderingLayerMask) == 0u,
                            "The bright surrounding lamp cannot illuminate Boki's renderers.");
                    chargedIntensity = lamp.intensity;
                    chargedRange = lamp.range;
                    previewCamera.Render();
                    CaptureFrame("boki-light-charged.png");
                    Next();
                    break;
                case 2:
                    battery.State.Current = 50f;
                    Next();
                    break;
                case 3:
                    if (EditorApplication.timeSinceStartup - stageStarted < 0.8) return;
                    Require(lamp.intensity < chargedIntensity * 0.75f && lamp.range < chargedRange * 0.75f,
                        "Half battery dims and contracts the surrounding light.");
                    CaptureFrame("boki-light-half.png");
                    Next();
                    break;
                case 4:
                    battery.State.Current = 0f;
                    Next();
                    break;
                case 5:
                    Require(!lamp.enabled && battery.Visibility == 0f, "Empty battery extinguishes surrounding light.");
                    CaptureFrame("boki-light-empty.png");
                    Next();
                    break;
                case 6:
                    var rider = battery.GetComponentInChildren<SkinnedMeshRenderer>();
                    Require(rider != null, "The preview has Boki's rigged model.");
                    battery.enabled = false;
                    Require(rider.renderingLayerMask == 1u, "Disabling the light restores Boki's authored rendering layer.");
                    battery.enabled = true;
                    Require(rider.renderingLayerMask == 2u, "Re-enabling the light isolates Boki from the bright spill again.");
                    Finish(null);
                    break;
            }
        }

        private static void CaptureFrame(string name)
        {
            Canvas.ForceUpdateCanvases();
            previewCamera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = previewTexture;
            var texture = new Texture2D(previewTexture.width, previewTexture.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), name), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            RenderTexture.active = previous;
        }

        private static void Teleport(Vector3 position)
        {
            player.enabled = false;
            player.transform.position = position;
            player.enabled = true;
            Physics.SyncTransforms();
        }
        private static void Next() { stage++; stageStarted = EditorApplication.timeSinceStartup; }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(string error)
        {
            if (error != null) { failed = true; Debug.LogError(error); }
            if (previewTexture != null)
            {
                if (previewCamera != null) previewCamera.targetTexture = null;
                previewTexture.Release();
                Object.DestroyImmediate(previewTexture);
            }
            EditorApplication.ExitPlaymode();
        }
    }
}
