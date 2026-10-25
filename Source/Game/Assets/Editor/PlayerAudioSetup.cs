using System;
using Boki.Combat;
using Boki.Battery;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    public static class PlayerAudioSetup
    {
        private const string PrefabPath = "Assets/Prefabs/Player/Boki_Rigged_Test.prefab";
        private static readonly string[] Scenes = { "Player_Model_Test", "V1_Pohorje", "V1_Maribor", "V1_FERI" };
        private static readonly string[] Clips = { "motor", "jump", "landing", "boost", "shot", "batteryPickup",
            "xpPickup", "damage", "death", "batteryLow", "batteryEmpty", "guiltHeartbeat" };

        [MenuItem("Boki/Audio/Apply Player Sounds")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            AssetDatabase.Refresh();
            foreach (string clip in Clips)
            {
                string path = "Assets/Audio/Sfx/Player/" + clip + ".wav";
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new InvalidOperationException("Missing sound: " + path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Configure(prefab.GetComponent<ScooterController>());
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in Scenes)
                {
                    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
                    int players = 0;
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (ScooterController movement in root.GetComponentsInChildren<ScooterController>(true))
                        {
                            if (movement.GetComponent<AutoLaser>() == null)
                                throw new InvalidOperationException(name + " player is missing AutoLaser.");
                            Configure(movement);
                            Validate(movement);
                            players++;
                        }
                    if (players != 1) throw new InvalidOperationException(name + " must have exactly one player.");
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("PLAYER_AUDIO_WIRED: " + name);
                }
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_AUDIO_APPLIED: shared rigged prefab and all four scenes.");
        }

        private static void Configure(ScooterController movement)
        {
            if (movement == null) throw new InvalidOperationException("Missing scooter controller.");
            GameObject player = movement.gameObject;
            var audio = player.GetComponent<PlayerAudio>();
            if (audio == null) audio = player.AddComponent<PlayerAudio>();
            var serialized = new SerializedObject(audio);
            Set(serialized, "movement", movement);
            Set(serialized, "health", player.GetComponent<PlayerHealth>());
            Set(serialized, "battery", player.GetComponent<LightBattery>());
            Set(serialized, "laser", player.GetComponent<AutoLaser>(), false);
            Set(serialized, "motorSource", Source(player.transform, "Scooter Motor Audio", 160));
            Set(serialized, "actionSource", Source(player.transform, "Player Action Audio", 100));
            Set(serialized, "rewardSource", Source(player.transform, "Player Pickup Audio", 80));
            foreach (string name in Clips)
                Set(serialized, name, AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/Player/" + name + ".wav"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RecordedAudioSetup.ConfigurePlayer(movement);
            EditorUtility.SetDirty(audio);
            if (PrefabUtility.IsPartOfPrefabInstance(audio)) PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
        }

        private static AudioSource Source(Transform parent, string name, int priority)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            return source;
        }

        private static void Validate(ScooterController movement)
        {
            var audio = movement.GetComponent<PlayerAudio>();
            var serialized = new SerializedObject(audio);
            foreach (string name in Clips)
            {
                var clip = serialized.FindProperty(name).objectReferenceValue as AudioClip;
                if (clip == null || clip.samples <= 0 || clip.channels != 1)
                    throw new InvalidOperationException("Invalid player sound: " + name);
            }
            foreach (string name in new[] { "movement", "health", "battery", "laser", "motorSource", "actionSource", "rewardSource" })
                if (serialized.FindProperty(name).objectReferenceValue == null)
                    throw new InvalidOperationException("Missing player sound binding: " + name);
            if (movement.GetComponents<PlayerAudio>().Length != 1 || movement.GetComponentsInChildren<AudioSource>(true).Length != 3)
                throw new InvalidOperationException("Player must have one audio component and three sources.");
        }

        private static void Set(SerializedObject target, string name, UnityEngine.Object value, bool required = true)
        {
            if (required && value == null) throw new InvalidOperationException("Missing PlayerAudio reference: " + name);
            target.FindProperty(name).objectReferenceValue = value;
        }
    }
}
