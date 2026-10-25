using System;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    public static class MenuSfxSetup
    {
        private const string SfxFolder = "Assets/Audio/Sfx/";
        private const string ScenePath = "Assets/Scenes/Menu_Preview.unity";
        private const string ObjectName = "Menu Sfx";

        [MenuItem("Boki/Menus/Add Menu Sound Effects To Preview")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");

            var select = ImportClip("menu_select");
            var click = ImportClip("menu_click");
            var back = ImportClip("menu_back");
            var pause = ImportClip("menu_pause");

            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var screen = UnityEngine.Object.FindFirstObjectByType<MenuScreen>();
                if (screen == null) throw new InvalidOperationException("Menu_Preview has no MenuScreen.");

                var sfx = UnityEngine.Object.FindFirstObjectByType<MenuSfx>();
                GameObject host = sfx != null ? sfx.gameObject : new GameObject(ObjectName, typeof(AudioSource), typeof(MenuSfx));
                if (sfx == null)
                {
                    host.transform.SetParent(screen.transform, false);
                    sfx = host.GetComponent<MenuSfx>();
                }
                var source = host.GetComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;

                var serialized = new SerializedObject(sfx);
                serialized.FindProperty("screen").objectReferenceValue = screen;
                serialized.FindProperty("source").objectReferenceValue = source;
                serialized.FindProperty("select").objectReferenceValue = select;
                serialized.FindProperty("click").objectReferenceValue = click;
                serialized.FindProperty("back").objectReferenceValue = back;
                serialized.FindProperty("pause").objectReferenceValue = pause;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("MENU_SFX_WIRED: '" + ObjectName + "' in " + ScenePath);
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        // Short clips: decode once on load so playback has no streaming latency.
        private static AudioClip ImportClip(string name)
        {
            string path = SfxFolder + name + ".wav";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Missing " + path + "; import it in Unity first.");
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
