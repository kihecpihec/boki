using System;
using Boki.Combat;
using Boki.Core;
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
    public static class MapAmbienceChecks
    {
        private const string Key = "Boki.MapAmbienceChecks";
        [Serializable] private sealed class SavedScenes { public string[] paths; public bool[] loaded; public bool[] active; }
        private static int stage, bedSample, detailSample, musicSample;
        private static double started, nextTick;
        private static string failure;
        private static MapAmbience ambience;
        private static AudioSource bed, detail, speech, music;
        private static PlayerHealth player;
        private static AudioClip previousDetail;
        private static float beforeDuck, musicBeforeDuck;

        static MapAmbienceChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
            Application.logMessageReceived += Logged;
        }

        [MenuItem("Boki/Audio/Run Map Ambience Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            var saved = new SavedScenes { paths = new string[previous.Length], loaded = new bool[previous.Length], active = new bool[previous.Length] };
            for (int i = 0; i < previous.Length; i++)
            { saved.paths[i] = previous[i].path; saved.loaded[i] = previous[i].isLoaded; saved.active[i] = previous[i].isActive; }
            SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(saved));
            foreach (string scene in new[] { "V1_Pohorje", "V1_Maribor", "V1_FERI", "Player_Model_Test" })
            {
                Scene opened = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
                MapAmbienceSetup.Validate(opened);
                LevelMusicSetup.Validate(opened);
            }
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            { stage = 0; failure = null; started = EditorApplication.timeSinceStartup; nextTick = started + 1; }
            if (state != PlayModeStateChange.EnteredEditMode) return;
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
            Debug.Log(failure == null
                ? "MAP_AMBIENCE_CHECKS_PASSED: four map bindings, loop/detail playback, pause/focus resume, speech ducking, variations, disable/re-enable, death, scene cleanup, city loop and FERI boss silence."
                : "MAP_AMBIENCE_CHECKS_FAILED: " + failure);
            if (Application.isBatchMode) EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void Logged(string message, string trace, LogType type)
        {
            if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying
                && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                && !trace.Contains("UnityEditor.Search.SearchDatabase")) failure = message;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (failure != null) { EditorApplication.ExitPlaymode(); return; }
            if (EditorApplication.timeSinceStartup - started > 45) { Fail("Timeout at stage " + stage); return; }
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.15;
            try
            {
                RunSession session = RunSession.Instance;
                switch (stage)
                {
                    case 0:
                        Bind();
                        ambience.enabled = false;
                        var tuning = new SerializedObject(ambience);
                        // Accelerate only this live test instance; authored gaps are validated separately.
                        tuning.FindProperty("minimumInterval").floatValue = 1f;
                        tuning.FindProperty("maximumInterval").floatValue = 1f;
                        tuning.ApplyModifiedPropertiesWithoutUndo();
                        ambience.enabled = true;
                        ambience.SendMessage("OnApplicationFocus", true);
                        nextTick += 1.2;
                        break;
                    case 1:
                        Check(bed.isPlaying && bed.loop && bed.volume > 0f && bed.volume <= 0.16f, "Quiet faded loop plays");
                        Check(music.isPlaying && music.loop && music.volume > 0f, "Gameplay music plays");
                        Check(detail.isPlaying && !detail.loop, "Sparse detail plays");
                        previousDetail = detail.clip;
                        session.SetPaused(PauseReason.Menu, true);
                        break;
                    case 2:
                        Check(!bed.isPlaying && !detail.isPlaying && !music.isPlaying, "Menu pauses both layers");
                        bedSample = bed.timeSamples; detailSample = detail.timeSamples; musicSample = music.timeSamples;
                        break;
                    case 3:
                        Check(bed.timeSamples == bedSample && detail.timeSamples == detailSample && music.timeSamples == musicSample, "Paused samples stay frozen");
                        session.SetPaused(PauseReason.Menu, false);
                        break;
                    case 4:
                        Check(bed.isPlaying && detail.isPlaying && music.isPlaying, "Both layers resume");
                        ambience.SendMessage("OnApplicationFocus", false);
                        break;
                    case 5:
                        Check(!bed.isPlaying && !detail.isPlaying && !music.isPlaying, "Focus loss pauses ambience");
                        ambience.SendMessage("OnApplicationFocus", true);
                        break;
                    case 6:
                        Check(bed.isPlaying && detail.isPlaying && music.isPlaying, "Focus return resumes ambience");
                        beforeDuck = bed.volume; musicBeforeDuck = music.volume;
                        speech.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/Recorded/Dialogue/professor_question.wav");
                        speech.volume = 0f;
                        speech.Play();
                        nextTick += 0.6;
                        break;
                    case 7:
                        Check(bed.volume < beforeDuck && music.volume < musicBeforeDuck && detail.volume < 0.01f, "Dialogue ducks bed and details");
                        speech.Stop();
                        nextTick += 0.6;
                        break;
                    case 8:
                        Check(bed.volume > 0.056f, "Ambience recovers after speech");
                        if (!detail.isPlaying || detail.clip == previousDetail) return;
                        Check(detail.clip != previousDetail, "Next sparse detail avoids immediate repeat");
                        ambience.enabled = false;
                        Check(!bed.isPlaying && !detail.isPlaying && !music.isPlaying, "Disable stops both sources");
                        ambience.enabled = true;
                        ambience.SendMessage("OnApplicationFocus", true);
                        break;
                    case 9:
                        Check(bed.isPlaying && ambience.GetComponentsInChildren<AudioSource>().Length == 3, "Re-enable restarts without new sources");
                        player.TakeDamage(player.Current);
                        break;
                    case 10:
                        Check(!bed.isPlaying && !detail.isPlaying && !music.isPlaying, "Death stops ambience");
                        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/V1_Maribor.unity", new LoadSceneParameters(LoadSceneMode.Single));
                        nextTick += 1;
                        break;
                    case 11:
                        Check(ambience == null && bed == null && detail == null, "Scene travel destroys old ambience sources");
                        session.State.NewRun(default, Time.realtimeSinceStartupAsDouble);
                        Bind();
                        break;
                    case 12:
                        Check(bed.isPlaying && bed.clip.name == "city_air" && music.isPlaying && music.clip.name == "maribor_gameplay", "City has its own live bed");
                        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/V1_FERI.unity", new LoadSceneParameters(LoadSceneMode.Single));
                        nextTick += 1;
                        break;
                    case 13:
                        Bind();
                        break;
                    case 14:
                        Check(bed.isPlaying && bed.clip.name == "feri_ventilation" && bed.volume <= 0.075f && music.isPlaying && music.clip.name == "feri_gameplay", "FERI approach stays quieter");
                        Check(session.State.TrySelect(RouteLevel.Forest, RouteChoice.A) && session.State.TryAdvance(RouteLevel.Forest)
                            && session.State.TrySelect(RouteLevel.City, RouteChoice.A) && session.State.TryAdvance(RouteLevel.City)
                            && session.State.TrySelect(RouteLevel.Feri, RouteChoice.A) && session.EnterBossArena(), "Enter boss run state");
                        break;
                    case 15:
                        Check(!bed.isPlaying && !detail.isPlaying && !music.isPlaying, "Boss and ending retain silence from map ambience");
                        EditorApplication.ExitPlaymode();
                        break;
                }
                stage++;
            }
            catch (Exception exception) { Fail("stage " + stage + ": " + exception.Message); }
        }

        private static void Bind()
        {
            ambience = Object.FindAnyObjectByType<MapAmbience>();
            Check(ambience != null && ambience.isActiveAndEnabled, "Enabled map ambience exists");
            var fields = new SerializedObject(ambience);
            bed = (AudioSource)fields.FindProperty("bedSource").objectReferenceValue;
            music = (AudioSource)fields.FindProperty("musicSource").objectReferenceValue;
            detail = (AudioSource)fields.FindProperty("detailSource").objectReferenceValue;
            var dialogue = (RecordedGameAudio)fields.FindProperty("dialogue").objectReferenceValue;
            speech = (AudioSource)new SerializedObject(dialogue).FindProperty("dialogueSource").objectReferenceValue;
            player = (PlayerHealth)fields.FindProperty("player").objectReferenceValue;
            player.GetComponent<ScooterController>().enabled = false;
            player.GetComponent<AutoLaser>().enabled = false;
            ambience.SendMessage("OnApplicationFocus", true);
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Fail(string message) { failure = message; EditorApplication.ExitPlaymode(); }
    }
}
