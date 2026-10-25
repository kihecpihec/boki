using System;
using System.Collections.Generic;
using Boki.Combat;
using Boki.Enemies;
using Boki.Intro;
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
    public static class RecordedAudioSetup
    {
        private const string AudioPath = "Assets/Audio/Sfx/Recorded/";
        private static readonly string[] Scenes = { "Player_Model_Test", "V1_Pohorje", "V1_Maribor", "V1_FERI" };

        [MenuItem("Boki/Audio/Apply Departure Voice And Repetition")]
        public static void ApplyDepartureVoice()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save dirty scenes first.");
            AssetDatabase.Refresh();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "Dialogue/profesor_will_be_mad.mp3");
            if (clip == null) throw new InvalidOperationException("Import profesor_will_be_mad.mp3 first.");
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
            importer.forceToMono = true;
            importer.SaveAndReimport();
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in Scenes)
                {
                    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                    var players = All<PlayerAudio>(scene);
                    if (players.Length != 1) throw new InvalidOperationException(name + " must have one PlayerAudio.");
                    var binding = new SerializedObject(players[0]);
                    Set(binding, "departureLine", clip);
                    binding.FindProperty("laserSoundInterval").floatValue = 0.5f;
                    binding.FindProperty("guiltVoiceInterval").floatValue = 3.5f;
                    Save(binding);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log("DEPARTURE_AUDIO_WIRED: Player_Model_Test and V1 scenes.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [MenuItem("Boki/Audio/Wire V1 FERI Boss Music")]
        public static void WireBossMusic()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save dirty scenes first.");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/boss_fight.mp3");
            if (clip == null) throw new InvalidOperationException("Import boss_fight.mp3 first.");
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/V1_FERI.unity");
                var arenas = All<V1BossArena>(scene);
                if (arenas.Length != 1) throw new InvalidOperationException("Expected one V1 FERI boss arena.");
                Transform child = arenas[0].transform.Find("Boss Fight Music");
                if (child == null)
                {
                    child = new GameObject("Boss Fight Music").transform;
                    child.SetParent(arenas[0].transform, false);
                }
                var source = child.GetComponent<AudioSource>();
                if (source == null) source = child.gameObject.AddComponent<AudioSource>();
                source.clip = clip;
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                var binding = new SerializedObject(arenas[0]);
                Set(binding, "bossMusic", source);
                Save(binding);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("V1_FERI_BOSS_MUSIC_WIRED");
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [MenuItem("Boki/Audio/Apply Recorded Sounds")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            foreach (string name in Scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + name + ".unity") == null)
                    throw new InvalidOperationException("Missing existing scene: " + name);
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioPath.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            // Preserve the existing player wiring, including the latest light and camera tuning.
            PlayerAudioSetup.Apply();
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in Scenes)
                {
                    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                    ScooterController[] players = All<ScooterController>(scene);
                    if (players.Length != 1) throw new InvalidOperationException(name + " must have one player.");
                    ConfigurePlayer(players[0]);
                    GameObject root = null;
                    foreach (GameObject candidate in scene.GetRootGameObjects())
                        if (candidate.name == "Recorded Game Audio") root = candidate;
                    if (root == null)
                    {
                        root = new GameObject("Recorded Game Audio");
                        SceneManager.MoveGameObjectToScene(root, scene);
                    }
                    RecordedGameAudio output = root.GetComponent<RecordedGameAudio>();
                    if (output == null) output = root.AddComponent<RecordedGameAudio>();
                    var serialized = new SerializedObject(output);
                    Set(serialized, "player", players[0].GetComponent<PlayerHealth>());
                    V1GameFlow[] flows = All<V1GameFlow>(scene);
                    RouteManager[] routes = All<RouteManager>(scene);
                    Set(serialized, "flow", flows.Length > 0 ? flows[0] : null);
                    Set(serialized, "routes", routes.Length > 0 ? routes[0] : null);
                    if (flows.Length > 0)
                        Set(serialized, "subtitle", new SerializedObject(flows[0]).FindProperty("objective").objectReferenceValue);
                    else Set(serialized, "subtitle", null);
                    var voices = new Object[6];
                    for (int i = 0; i < voices.Length; i++) voices[i] = Source(root.transform, "Enemy Voice " + (i + 1), true);
                    SetArray(serialized, "enemySources", voices);
                    Set(serialized, "dialogueSource", Source(root.transform, "Recorded Dialogue", false));
                    SetArray(serialized, "enemyDamage", Clips("Enemies/damage", 10));
                    SetArray(serialized, "enemyDeath", Clips("Enemies/death", 4));
                    Set(serialized, "enemyGreeting", Clip("Enemies/greeting"));
                    SetArray(serialized, "teleport", Clips("World/teleport", 2));
                    Set(serialized, "professorQuestion", Clip("Dialogue/professor_question"));
                    Set(serialized, "bokiReply", Clip("Dialogue/skoraj_pravocasno"));
                    Set(serialized, "professorSaturday", Clip("Dialogue/professor_saturday"));
                    SetArray(serialized, "bokiReaction", Clips("Dialogue/boki_reaction", 2));
                    Save(serialized);
                    foreach (EnemyHealth enemy in All<EnemyHealth>(scene))
                    {
                        EnemyAudio voice = enemy.GetComponent<EnemyAudio>();
                        if (voice == null) voice = enemy.gameObject.AddComponent<EnemyAudio>();
                        var binding = new SerializedObject(voice);
                        Set(binding, "output", output);
                        binding.FindProperty("professor").boolValue = enemy.GetComponent<ProfessorBoss>() != null;
                        Save(binding);
                    }
                    foreach (V1BossArena arena in All<V1BossArena>(scene))
                    {
                        var binding = new SerializedObject(arena);
                        Set(binding, "recordedAudio", output);
                        Save(binding);
                    }
                    foreach (IntroCutscene intro in All<IntroCutscene>(scene))
                    {
                        var binding = new SerializedObject(intro);
                        Set(binding, "bokiLine", Clip("Dialogue/fak_ne_spet"));
                        Save(binding);
                    }
                    Validate(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("RECORDED_AUDIO_WIRED: " + name);
                }
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("RECORDED_AUDIO_APPLIED: current V1 scenes and Player_Model_Test.");
        }

        public static void ConfigurePlayer(ScooterController movement)
        {
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "Player/motor.wav") == null) return;
            var audio = movement.GetComponent<PlayerAudio>();
            if (audio == null) throw new InvalidOperationException("Apply player audio before recorded clips.");
            var binding = new SerializedObject(audio);
            Set(binding, "motor", Clip("Player/motor"));
            SetArray(binding, "damageVariations", Clips("Player/damage", 3));
            SetArray(binding, "shotVariations", Clips("Laser/shot", 9));
            SetArray(binding, "guiltVariations", Clips("Guilt/voice", 2));
            Set(binding, "guiltReaction", Clip("Dialogue/kaj_je_to_blo"));
            Set(binding, "snack", Clip("Player/snack"));
            Set(binding, "departureLine", AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "Dialogue/profesor_will_be_mad.mp3"));
            binding.FindProperty("laserSoundInterval").floatValue = 0.5f;
            binding.FindProperty("guiltVoiceInterval").floatValue = 3.5f;
            Save(binding);
        }

        internal static void Validate(Scene scene)
        {
            RecordedGameAudio[] outputs = All<RecordedGameAudio>(scene);
            if (outputs.Length != 1) throw new InvalidOperationException("Expected one recorded audio output.");
            var binding = new SerializedObject(outputs[0]);
            if (binding.FindProperty("enemySources").arraySize != 6
                || outputs[0].GetComponentsInChildren<AudioSource>(true).Length != 7)
                throw new InvalidOperationException("Expected six enemy sources and one dialogue source.");
            foreach (EnemyHealth health in All<EnemyHealth>(scene))
            {
                var audio = health.GetComponent<EnemyAudio>();
                if (audio == null || new SerializedObject(audio).FindProperty("output").objectReferenceValue != outputs[0])
                    throw new InvalidOperationException("Missing enemy recorded audio binding: " + health.name);
            }
            foreach (V1BossArena arena in All<V1BossArena>(scene))
                if (new SerializedObject(arena).FindProperty("recordedAudio").objectReferenceValue != outputs[0])
                    throw new InvalidOperationException("Missing professor dialogue binding.");
        }

        internal static T[] All<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            return found.ToArray();
        }
        private static AudioClip Clip(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + name + ".wav");
            if (clip == null) throw new InvalidOperationException("Missing recorded clip: " + name);
            return clip;
        }
        private static Object[] Clips(string prefix, int count)
        {
            var clips = new Object[count];
            for (int i = 0; i < count; i++) clips[i] = Clip(prefix + "_" + (i + 1).ToString("00"));
            return clips;
        }
        private static AudioSource Source(Transform parent, string name, bool spatial)
        {
            Transform child = parent.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = source.loop = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 24f;
            source.priority = spatial ? 140 : 50;
            source.volume = spatial ? 0.5f : 0.75f;
            EditorUtility.SetDirty(source);
            return source;
        }
        private static void Set(SerializedObject target, string name, Object value)
            => target.FindProperty(name).objectReferenceValue = value;
        private static void SetArray(SerializedObject target, string name, Object[] values)
        {
            SerializedProperty array = target.FindProperty(name);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        private static void Save(SerializedObject target)
        {
            target.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target.targetObject);
            if (PrefabUtility.IsPartOfPrefabInstance(target.targetObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target.targetObject);
        }
    }
}
