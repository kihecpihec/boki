using System;
using System.Collections.Generic;
using Boki.Core;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    public static class MainMenuSetup
    {
        [MenuItem("Boki/Menus/Set Up Production Main Menu")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RunSession.MainMenuScene) == null)
                {
                    var scene = EditorSceneManager.OpenScene("Assets/Scenes/Menu_Preview.unity");
                    MenuScreen menu = null;
                    foreach (var root in scene.GetRootGameObjects())
                        if (root.GetComponentInChildren<MenuScreen>(true) is MenuScreen found) menu = found;
                    if (menu == null) throw new InvalidOperationException("Missing preview MenuScreen.");
                    UnityEngine.Object.DestroyImmediate(menu.GetComponent<MenuPreview>());
                    var notice = menu.transform.Find("Preview Notice");
                    if (notice != null) UnityEngine.Object.DestroyImmediate(notice.gameObject);
                    var flow = menu.gameObject.AddComponent<MainMenuFlow>();
                    var data = new SerializedObject(flow);
                    data.FindProperty("menu").objectReferenceValue = menu;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.SaveScene(scene, RunSession.MainMenuScene);
                }
                var scenes = new List<EditorBuildSettingsScene>
                {
                    new EditorBuildSettingsScene(RunSession.MainMenuScene, true)
                };
                foreach (var entry in EditorBuildSettings.scenes)
                    if (entry.path != RunSession.MainMenuScene) scenes.Add(entry);
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("MAIN_MENU_SETUP_COMPLETE");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
