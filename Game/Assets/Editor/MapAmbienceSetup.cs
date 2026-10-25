using System;
using Boki.Player;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    public static class MapAmbienceSetup
    {
        private const string AudioPath = "Assets/Audio/Ambience/";
        private static readonly string[] Scenes = { "Player_Model_Test", "V1_Pohorje", "V1_Maribor", "V1_FERI" };

        [MenuItem("Boki/Audio/Apply Map Ambience")]
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
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in Scenes)
                {
                    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                    PlayerHealth[] players = RecordedAudioSetup.All<PlayerHealth>(scene);
                    RecordedGameAudio[] dialogues = RecordedAudioSetup.All<RecordedGameAudio>(scene);
                    V1GameFlow[] flows = RecordedAudioSetup.All<V1GameFlow>(scene);
                    if (players.Length != 1 || dialogues.Length != 1)
                        throw new InvalidOperationException(name + " needs one player and one recorded dialogue output.");
                    MapAmbience[] existing = RecordedAudioSetup.All<MapAmbience>(scene);
                    if (existing.Length > 1) throw new InvalidOperationException(name + " has duplicate map ambience.");
                    MapAmbience ambience;
                    if (existing.Length == 1) ambience = existing[0];
                    else
                    {
                        var root = new GameObject("Map Ambience");
                        SceneManager.MoveGameObjectToScene(root, scene);
                        ambience = root.AddComponent<MapAmbience>();
                    }
                    bool city = name == "V1_Maribor", feri = name == "V1_FERI";
                    AudioSource bed = Source(ambience.transform, "Environment Bed");
                    bed.clip = Clip(feri ? "feri_ventilation" : city ? "city_air" : "forest_wind");
                    bed.loop = true;
                    AudioSource detail = Source(ambience.transform, "Distant Details");
                    detail.loop = false;
                    var binding = new SerializedObject(ambience);
                    Set(binding, "player", players[0]);
                    Set(binding, "flow", flows.Length == 1 ? flows[0] : null);
                    Set(binding, "dialogue", dialogues[0]);
                    Set(binding, "bedSource", bed);
                    Set(binding, "detailSource", detail);
                    var clips = binding.FindProperty("details");
                    clips.arraySize = 2;
                    clips.GetArrayElementAtIndex(0).objectReferenceValue = Clip(feri ? "pipe_creak" : city ? "distant_metal" : "distant_bird");
                    clips.GetArrayElementAtIndex(1).objectReferenceValue = Clip(feri ? "electrical_tick" : city ? "street_gust" : "foliage_rustle");
                    binding.FindProperty("bedVolume").floatValue = feri ? 0.075f : city ? 0.14f : 0.16f;
                    binding.FindProperty("detailVolume").floatValue = feri ? 0.075f : city ? 0.11f : 0.12f;
                    binding.FindProperty("minimumInterval").floatValue = feri ? 22f : city ? 16f : 12f;
                    binding.FindProperty("maximumInterval").floatValue = feri ? 38f : city ? 30f : 24f;
                    binding.FindProperty("silenceInBossArena").boolValue = feri;
                    binding.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(ambience);
                    Validate(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("MAP_AMBIENCE_WIRED: " + name);
                }
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        public static void Validate(Scene scene)
        {
            MapAmbience[] all = RecordedAudioSetup.All<MapAmbience>(scene);
            if (all.Length != 1)
                throw new InvalidOperationException(scene.name + " needs one ambience with bed and detail sources.");
            var fields = new SerializedObject(all[0]);
            int expectedSources = fields.FindProperty("musicSource").objectReferenceValue == null ? 2 : 3;
            if (all[0].GetComponentsInChildren<AudioSource>().Length != expectedSources)
                throw new InvalidOperationException(scene.name + " has unexpected ambience sources.");
            foreach (string field in new[] { "player", "dialogue", "bedSource", "detailSource" })
                if (fields.FindProperty(field).objectReferenceValue == null)
                    throw new InvalidOperationException(scene.name + " missing ambience " + field);
            foreach (AudioSource source in all[0].GetComponentsInChildren<AudioSource>())
                if (source.playOnAwake || source.spatialBlend != 0f)
                    throw new InvalidOperationException(scene.name + " ambience must be controlled non-spatial audio.");
            var bed = (AudioSource)fields.FindProperty("bedSource").objectReferenceValue;
            string expected = scene.name == "V1_FERI" ? "feri_ventilation" : scene.name == "V1_Maribor" ? "city_air" : "forest_wind";
            if (bed.clip == null || bed.clip.name != expected || !bed.loop)
                throw new InvalidOperationException(scene.name + " has the wrong ambient loop.");
            if (fields.FindProperty("minimumInterval").floatValue < 12f
                || fields.FindProperty("maximumInterval").floatValue < fields.FindProperty("minimumInterval").floatValue
                || fields.FindProperty("silenceInBossArena").boolValue != (scene.name == "V1_FERI"))
                throw new InvalidOperationException(scene.name + " has invalid ambient spacing or boss handling.");
            var details = fields.FindProperty("details");
            if (details.arraySize != 2 || details.GetArrayElementAtIndex(0).objectReferenceValue == null
                || details.GetArrayElementAtIndex(1).objectReferenceValue == null)
                throw new InvalidOperationException(scene.name + " is missing ambient variations.");
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + name + ".wav");
            if (clip == null) throw new InvalidOperationException("Missing ambient clip " + name);
            return clip;
        }
        private static void Set(SerializedObject target, string field, Object value)
            => target.FindProperty(field).objectReferenceValue = value;
        private static AudioSource Source(Transform root, string name)
        {
            Transform child = root.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(root, false); }
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 200;
            source.volume = 0f;
            return source;
        }
    }
}
