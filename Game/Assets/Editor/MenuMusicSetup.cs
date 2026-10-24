using System;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    public static class MenuMusicSetup
    {
        private const string ClipPath = "Assets/Audio/Music/main_menu.ogg";
        private const string ScenePath = "Assets/Scenes/Menu_Preview.unity";
        private const string ObjectName = "Menu Music";

        [MenuItem("Boki/Menus/Add Main Menu Music To Preview")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");

            ConfigureImport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null) throw new InvalidOperationException("Missing " + ClipPath);

            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var screen = UnityEngine.Object.FindFirstObjectByType<MenuScreen>();
                if (screen == null) throw new InvalidOperationException("Menu_Preview has no MenuScreen.");
                if (UnityEngine.Object.FindFirstObjectByType<MenuMusic>() != null)
                {
                    Debug.Log("Main menu music is already wired; nothing changed.");
                    return;
                }
                var host = new GameObject(ObjectName, typeof(AudioSource), typeof(MenuMusic));
                host.transform.SetParent(screen.transform, false);
                var source = host.GetComponent<AudioSource>();
                source.clip = clip;
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                var music = new SerializedObject(host.GetComponent<MenuMusic>());
                music.FindProperty("screen").objectReferenceValue = screen;
                music.FindProperty("source").objectReferenceValue = source;
                music.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("MENU_MUSIC_WIRED: added '" + ObjectName + "' to " + ScenePath);
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        // Streaming keeps the 64 s loop out of memory; Vorbis loops without a gap.
        private static void ConfigureImport()
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(ClipPath);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }
    }
}
