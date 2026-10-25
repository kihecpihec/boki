using System;
using Boki.Combat;
using Boki.Core;
using Boki.Intro;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Boki.Editor
{
    [InitializeOnLoad]
    public static class PlayerAudioChecks
    {
        private const string Key = "Boki.PlayerAudioChecks";
        private static int stage;
        private static double nextTick;
        private static double started;
        private static string failure;
        private static SceneSetup[] previous;
        private static GameObject target;
        private static InputSettings.BackgroundBehavior previousBackground;
        private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        static PlayerAudioChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
            Application.logMessageReceived += Log;
        }

        [MenuItem("Boki/Audio/Run Player Audio Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            previous = EditorSceneManager.GetSceneManagerSetup();
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            failure = null;
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Player_Model_Test.unity");
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = -4;
                started = EditorApplication.timeSinceStartup;
                nextTick = started + 1;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            if (previous != null && previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log(failure == null ? "PLAYER_AUDIO_CHECKS_PASSED: intro skip, departure waits for movement and plays once, live prefab references, damage, laser shot, XP cue, shared pause/resume, death, disable cleanup."
                : "PLAYER_AUDIO_CHECKS_FAILED: " + failure);
            if (Application.isBatchMode) EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void Log(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (!trace.Contains("UnityEditor.Search.SearchDatabase")) failure = message;
        }

        private static void Press(params Key[] keys)
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            InputSystem.EnableDevice(Keyboard.current);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
            InputSystem.Update();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (failure != null) { EditorApplication.ExitPlaymode(); return; }
            if (EditorApplication.timeSinceStartup - started > 30) { Fail("Timeout at stage " + stage); return; }
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + .06;
            try
            {
                var audio = UnityEngine.Object.FindAnyObjectByType<PlayerAudio>();
                Check(audio != null && audio.isActiveAndEnabled, "Player audio enabled");
                // Simulate Game view focus for this headless playback smoke check.
                audio.SendMessage("OnApplicationFocus", true);
                var health = audio.GetComponent<PlayerHealth>();
                var serialized = new SerializedObject(audio);
                var motor = (AudioSource)serialized.FindProperty("motorSource").objectReferenceValue;
                var actions = (AudioSource)serialized.FindProperty("actionSource").objectReferenceValue;
                var rewards = (AudioSource)serialized.FindProperty("rewardSource").objectReferenceValue;
                var session = RunSession.Instance;
                switch (stage++)
                {
                    case -4:
                        rewards.Stop();
                        var intro = UnityEngine.Object.FindAnyObjectByType<IntroCutscene>();
                        intro.Play();
                        Press(UnityEngine.InputSystem.Key.Enter);
                        // Consume this simulated input frame before the Editor advances Input System again.
                        intro.SendMessage("Update");
                        break;
                    case -3:
                        Press();
                        Check(!session.State.Pause.Contains(PauseReason.ScriptedSequence), "Enter skips intro");
                        Check(!rewards.isPlaying, "Departure waits while standing still");
                        Check(serialized.FindProperty("departureLine").objectReferenceValue != null, "Departure clip imported and bound");
                        break;
                    case -2:
                        // Headless Editor focus blocks riding input; drive the existing movement API instead.
                        audio.GetComponent<ScooterController>().ApplyKnockback(Vector3.forward, 2f);
                        audio.SendMessage("LateUpdate");
                        Check(audio.GetComponent<ScooterController>().HorizontalSpeed > 0.3f, "Rider starts moving");
                        Check(rewards.isPlaying, "First movement plays departure voice");
                        rewards.Stop();
                        break;
                    case -1:
                        audio.GetComponent<ScooterController>().ApplyKnockback(Vector3.forward, 2f);
                        audio.SendMessage("LateUpdate");
                        Check(!rewards.isPlaying, "Continued movement does not repeat departure voice");
                        Press();
                        break;
                    case 0:
                        Check(motor.clip != null && motor.loop, "Imported motor loop bound");
                        audio.GetComponent<ScooterController>().enabled = false;
                        health.TakeDamage(1);
                        Check(actions.isPlaying, "Accepted damage plays an action sound");
                        actions.Stop();
                        health.TakeDamage(1);
                        Check(!actions.isPlaying, "Rapid damage does not stack pain voices");
                        break;
                    case 1:
                        actions.Stop();
                        target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        target.layer = 2;
                        target.transform.position = audio.transform.position + audio.transform.forward * 3 + Vector3.up;
                        target.AddComponent<EnemyHealth>();
                        audio.GetComponent<AutoLaser>().enabled = true;
                        break;
                    case 2:
                        Check(actions.isPlaying, "Actual laser shot plays audio");
                        actions.Stop();
                        audio.SendMessage("PlayShot");
                        Check(!actions.isPlaying, "Repeated laser audio is suppressed during its sound cooldown");
                        UnityEngine.Object.Destroy(target);
                        audio.GetComponent<AutoLaser>().enabled = false;
                        audio.PlayXpCollection();
                        Check(rewards.isPlaying, "Pickup source plays imported XP clip");
                        session.SetPaused(PauseReason.Menu, true);
                        break;
                    case 3:
                        Check(!motor.isPlaying && !actions.isPlaying && !rewards.isPlaying, "Shared menu pause suspends all sources");
                        rewards.Stop();
                        audio.PlayXpCollection();
                        Check(!rewards.isPlaying, "XP sound cooldown spans collection frames and gameplay pause");
                        session.SetPaused(PauseReason.Menu, false);
                        break;
                    case 4:
                        Check(motor.isPlaying, "Motor resumes after shared pause");
                        health.TakeDamage(health.Current);
                        Check(actions.isPlaying && !motor.isPlaying, "Death plays while motor stops");
                        break;
                    case 5:
                        audio.enabled = false;
                        Check(!motor.isPlaying && !actions.isPlaying && !rewards.isPlaying, "Disable stops all sources");
                        EditorApplication.ExitPlaymode();
                        break;
                }
            }
            catch (Exception exception) { Fail("stage " + (stage - 1) + ": " + exception.Message); }
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        private static void Fail(string message) { failure = message; EditorApplication.ExitPlaymode(); }
    }
}
