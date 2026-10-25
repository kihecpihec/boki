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
    public static class RecordedAudioChecks
    {
        private const string Key = "Boki.RecordedAudioChecks";
        [Serializable] private sealed class SavedScenes
        {
            public string[] paths;
            public bool[] loaded;
            public bool[] active;
        }
        private static int stage, pausedSample;
        private static double started, nextTick;
        private static string failure;
        private static RecordedGameAudio output;
        private static PlayerHealth player;
        private static V1GameFlow flow;
        private static V1BossArena arena;
        private static ProfessorBoss professor;
        private static AudioSource[] world;
        private static AudioSource dialogue, death;
        private static EnemyHealth spawned;

        static RecordedAudioChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
            Application.logMessageReceived += Logged;
        }

        [MenuItem("Boki/Audio/Run Recorded Audio Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            var saved = new SavedScenes { paths = new string[previous.Length], loaded = new bool[previous.Length], active = new bool[previous.Length] };
            for (int i = 0; i < previous.Length; i++)
            {
                saved.paths[i] = previous[i].path;
                saved.loaded[i] = previous[i].isLoaded;
                saved.active[i] = previous[i].isActive;
            }
            SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(saved));
            foreach (string scene in new[] { "Player_Model_Test", "V1_Pohorje", "V1_Maribor", "V1_FERI" })
                RecordedAudioSetup.Validate(EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity"));
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0;
                failure = null;
                started = EditorApplication.timeSinceStartup;
                nextTick = started + 1;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key + ".Scenes", ""));
            if (saved != null && saved.paths.Length > 0 && !string.IsNullOrEmpty(saved.paths[0]))
            {
                var previous = new SceneSetup[saved.paths.Length];
                for (int i = 0; i < previous.Length; i++) previous[i] = new SceneSetup
                    { path = saved.paths[i], isLoaded = saved.loaded[i], isActive = saved.active[i] };
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.EraseString(Key + ".Scenes");
            Debug.Log(failure == null
                ? "RECORDED_AUDIO_CHECKS_PASSED: four scene bindings, spawned enemy hit/death, surviving death tail, six-voice limit, menu pause/resume, actual FERI boss entry, question/reply, victory/reaction, disable cleanup."
                : "RECORDED_AUDIO_CHECKS_FAILED: " + failure);
            if (Application.isBatchMode) EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void Logged(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                && !trace.Contains("UnityEditor.Search.SearchDatabase")) failure = message;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (failure != null) { EditorApplication.ExitPlaymode(); return; }
            if (EditorApplication.timeSinceStartup - started > 45) { Fail("Timeout at stage " + stage + " alive=" + RunSession.Instance.State.Health.IsAlive
                    + " menu=" + RunSession.Instance.State.Pause.Contains(PauseReason.Menu)
                    + " upgrade=" + RunSession.Instance.State.Pause.Contains(PauseReason.Upgrade)
                    + " scripted=" + RunSession.Instance.State.Pause.Contains(PauseReason.ScriptedSequence)
                    + " active=" + RunSession.Instance.IsGameplayActive
                    + " focused=" + Application.isFocused); return; }
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.06;
            try
            {
                var session = RunSession.Instance;
                if (output != null) output.SendMessage("OnApplicationFocus", true);
                if (arena != null) arena.SendMessage("OnApplicationFocus", true);
                if (player != null) player.GetComponent<PlayerAudio>().SendMessage("OnApplicationFocus", true);
                switch (stage)
                {
                    case 0:
                        output = Object.FindAnyObjectByType<RecordedGameAudio>();
                        player = Object.FindAnyObjectByType<PlayerHealth>();
                        flow = Object.FindAnyObjectByType<V1GameFlow>();
                        arena = Object.FindAnyObjectByType<V1BossArena>();
                        Check(output != null && player != null && arena != null && flow != null, "FERI runtime references");
                        output.SendMessage("OnApplicationFocus", true);
                        player.GetComponent<ScooterController>().enabled = false;
                        player.GetComponent<AutoLaser>().enabled = false;
                        var fields = new SerializedObject(output);
                        dialogue = (AudioSource)fields.FindProperty("dialogueSource").objectReferenceValue;
                        world = new AudioSource[6];
                        for (int i = 0; i < world.Length; i++) world[i] = (AudioSource)fields.FindProperty("enemySources").GetArrayElementAtIndex(i).objectReferenceValue;
                        Check(session.State.TrySelect(RouteLevel.Forest, RouteChoice.A) && session.State.TryAdvance(RouteLevel.Forest)
                            && session.State.TrySelect(RouteLevel.City, RouteChoice.A) && session.State.TryAdvance(RouteLevel.City)
                            && session.State.TrySelect(RouteLevel.Feri, RouteChoice.A), "Valid route state for actual arena entry");
                        EnemyHealth template = null;
                        foreach (EnemyHealth candidate in RecordedAudioSetup.All<EnemyHealth>(SceneManager.GetActiveScene()))
                            if (candidate.GetComponent<ProfessorBoss>() == null && candidate.GetComponent<EnemyAudio>() != null)
                            { template = candidate; break; }
                        Check(template != null, "Wired regular enemy template");
                        GameObject instance = Object.Instantiate(template.gameObject, player.transform.position + Vector3.forward * 4, Quaternion.identity);
                        instance.SetActive(true);
                        spawned = instance.GetComponent<EnemyHealth>();
                        foreach (AudioSource source in world) source.Stop();
                        spawned.TakeDamage(1);
                        AudioSource hit = Playing("damage_", world);
                        Check(hit != null, "Spawned enemy damage voice");
                        spawned.TakeDamage(spawned.Current);
                        death = Playing("death_", world);
                        Check(death != null, "Lethal hit plays a death variation");
                        Check(hit.volume < death.volume && death.volume < 0.5f, "Enemy mix is quieter, with deaths above hits");
                        stage++;
                        break;
                    case 1:
                        Check(spawned == null && death.isPlaying, "Death audio survives destroyed enemy");
                        session.SetPaused(PauseReason.Menu, true);
                        stage++;
                        break;
                    case 2:
                        Check(!death.isPlaying, "Menu pauses death tail");
                        pausedSample = death.timeSamples;
                        stage++;
                        break;
                    case 3:
                        Check(death.timeSamples == pausedSample, "Death tail remains frozen");
                        session.SetPaused(PauseReason.Menu, false);
                        stage++;
                        break;
                    case 4:
                        Check(death.isPlaying, "Death tail resumes");
                        for (int i = 0; i < 20; i++) output.PlayEnemyDeath(player.transform.position);
                        int sounding = 0;
                        foreach (AudioSource source in world) if (source.isPlaying) sounding++;
                        Check(sounding <= 2, "Crowded death bursts retain at most two audible enemy voices");
                        Check(output.GetComponentsInChildren<AudioSource>().Length == 7, "Voice limit creates no new sources");
                        var controller = player.GetComponent<CharacterController>();
                        controller.enabled = false;
                        player.transform.position = arena.transform.position;
                        controller.enabled = true;
                        arena.SendMessage("OnTriggerEnter", controller);
                        professor = (ProfessorBoss)new SerializedObject(arena).FindProperty("professor").objectReferenceValue;
                        Check(session.HasEnteredBossArena && professor.IsFighting, "Actual arena begins fight once");
                        Check(session.State.Pause.Contains(PauseReason.ScriptedSequence) && dialogue.clip.name == "professor_question", "Opening question owns scripted pause");
                        stage++;
                        break;
                    case 5:
                        if (dialogue.clip.name != "skoraj_pravocasno") return;
                        var music = (AudioSource)new SerializedObject(arena).FindProperty("bossMusic").objectReferenceValue;
                        Check(music != null && music.clip.name == "boss_fight" && !music.isPlaying,
                            "Boss music is assigned and silent during opening");
                        var seal = (GameObject)new SerializedObject(arena).FindProperty("entranceSeal").objectReferenceValue;
                        Vector3 intoArena = professor.transform.position - seal.transform.position;
                        intoArena.y = 0f;
                        Check(Vector3.Dot(Camera.main.transform.position - seal.transform.position, intoArena) > 0f,
                            "Dialogue camera is inside the sealed arena");
                        Check(session.State.Pause.Contains(PauseReason.ScriptedSequence), "Reply retains combat pause");
                        session.SetPaused(PauseReason.Menu, true);
                        stage++;
                        break;
                    case 6:
                        Check(!dialogue.isPlaying, "Menu pauses scripted speech");
                        pausedSample = dialogue.timeSamples;
                        stage++;
                        break;
                    case 7:
                        Check(dialogue.timeSamples == pausedSample, "Scripted speech clock stays frozen");
                        session.SetPaused(PauseReason.Menu, false);
                        stage++;
                        break;
                    case 8:
                        if (session.State.Pause.Contains(PauseReason.ScriptedSequence) || !session.IsGameplayActive) return;
                        Check(session.IsGameplayActive, "Opening returns control after both lines");
                        var fightMusic = (AudioSource)new SerializedObject(arena).FindProperty("bossMusic").objectReferenceValue;
                        if (!fightMusic.isPlaying) return;
                        Check(fightMusic.loop && fightMusic.volume > 0f && fightMusic.volume <= 0.22f,
                            "Boss track loops with a quiet fade after dialogue");
                        EnemyHealth boss = professor.GetComponent<EnemyHealth>();
                        boss.TakeDamage(boss.Current);
                        Check(flow.IsTerminal && dialogue.clip.name == "professor_saturday" && dialogue.isPlaying, "Victory plays Saturday line behind terminal screen");
                        stage++;
                        break;
                    case 9:
                        if (!dialogue.clip.name.StartsWith("boki_reaction_", StringComparison.Ordinal)) return;
                        Check(dialogue.isPlaying, "Victory reaction follows professor");
                        foreach (AudioSource source in world) Check(!source.isPlaying, "Terminal state silences enemy voices");
                        output.enabled = false;
                        output.PlayBossOpening();
                        Check(!dialogue.isPlaying && session.State.Pause.Contains(PauseReason.Menu)
                            && !session.State.Pause.Contains(PauseReason.ScriptedSequence), "Disabled audio cannot acquire a pause or release the terminal pause");
                        EditorApplication.ExitPlaymode();
                        break;
                }
            }
            catch (Exception exception) { Fail("stage " + stage + ": " + exception.Message); }
        }

        private static AudioSource Playing(string prefix, AudioSource[] sources)
        {
            foreach (AudioSource source in sources)
                if (source.isPlaying && source.clip != null && source.clip.name.StartsWith(prefix, StringComparison.Ordinal)) return source;
            return null;
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Fail(string message) { failure = message; EditorApplication.ExitPlaymode(); }
    }
}
