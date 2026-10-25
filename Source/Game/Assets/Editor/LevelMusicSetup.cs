using System;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    public static class LevelMusicSetup
    {
        [MenuItem("Boki/Audio/Apply Level Music")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            AssetDatabase.Refresh();
            string[] tracks = { "pohorje_gameplay", "maribor_gameplay", "feri_gameplay" };
            foreach (string track in tracks)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/Audio/Music/" + track + ".wav");
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.65f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                string[] scenes = { "V1_Pohorje", "V1_Maribor", "V1_FERI", "Player_Model_Test" };
                for (int i = 0; i < scenes.Length; i++)
                {
                    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + scenes[i] + ".unity");
                    var ambience = RecordedAudioSetup.All<MapAmbience>(scene);
                    if (ambience.Length != 1) throw new InvalidOperationException("Expected one map ambience in " + scenes[i]);
                    Transform child = ambience[0].transform.Find("Gameplay Music");
                    if (child == null)
                    {
                        child = new GameObject("Gameplay Music").transform;
                        child.SetParent(ambience[0].transform, false);
                    }
                    var source = child.GetComponent<AudioSource>();
                    if (source == null) source = child.gameObject.AddComponent<AudioSource>();
                    source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/" + tracks[i % 3] + ".wav");
                    source.playOnAwake = false;
                    source.loop = true;
                    source.spatialBlend = 0f;
                    source.volume = 0f;
                    source.priority = 180;
                    var fields = new SerializedObject(ambience[0]);
                    fields.FindProperty("musicSource").objectReferenceValue = source;
                    fields.FindProperty("musicVolume").floatValue = i == 2 ? 0.1f : 0.24f;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    Validate(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("LEVEL_MUSIC_WIRED: " + scenes[i]);
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
            var all = RecordedAudioSetup.All<MapAmbience>(scene);
            if (all.Length != 1) throw new InvalidOperationException("Missing map ambience in " + scene.name);
            var fields = new SerializedObject(all[0]);
            var source = fields.FindProperty("musicSource").objectReferenceValue as AudioSource;
            if (source == null || source.clip == null || !source.loop || source.playOnAwake || source.spatialBlend != 0f)
                throw new InvalidOperationException("Invalid gameplay music in " + scene.name);
        }
    }
}
