using System;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    public static class V1PauseMenuSetup
    {
        private static double nextCheck;
        private static int smokeMap;
        private static int checkingRestart;
        private static readonly string[] Maps = { "V1_Pohorje", "V1_Maribor", "V1_FERI" };

        public static void SmokeChecks()
        {
            SessionState.SetBool("Boki.PauseSmoke", true);
            EditorSceneManager.OpenScene("Assets/Scenes/V1_Pohorje.unity");
            ResumeSmokeChecks();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void ResumeSmokeChecks()
        {
            if (!SessionState.GetBool("Boki.PauseSmoke", false)) return;
            nextCheck = EditorApplication.timeSinceStartup + 5;
            EditorApplication.update -= CheckPause;
            EditorApplication.update += CheckPause;
        }

        private static void CheckPause()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextCheck) return;
            try
            {
                if (checkingRestart != 0)
                {
                    if (SceneManager.GetActiveScene().name != "V1_Pohorje")
                        throw new InvalidOperationException("Restart did not return to Pohorje.");
                    if (checkingRestart == 1)
                    {
                        var loadedMenu = UnityEngine.Object.FindAnyObjectByType<MenuScreen>();
                        Boki.Core.RunSession.Instance.SetPaused(Boki.Core.PauseReason.ScriptedSequence, false);
                        loadedMenu.PauseRequested.Invoke();
                        loadedMenu.Activate(3);
                        if (!loadedMenu.IsMainMenuVisible || Time.timeScale != 0f)
                            throw new InvalidOperationException("Quit to menu failed.");
                        loadedMenu.Activate(0);
                        checkingRestart = 2;
                        nextCheck = EditorApplication.timeSinceStartup + 5;
                        return;
                    }
                    Debug.Log("V1_PAUSE_SMOKE_PASSED: " + Maps[smokeMap]);
                    smokeMap++;
                    checkingRestart = 0;
                    if (smokeMap == Maps.Length)
                    {
                        SessionState.SetBool("Boki.PauseSmoke", false);
                        EditorApplication.Exit(0);
                        return;
                    }
                    EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/" + Maps[smokeMap] + ".unity",
                        new LoadSceneParameters(LoadSceneMode.Single));
                    nextCheck = EditorApplication.timeSinceStartup + 5;
                    return;
                }
                var flow = UnityEngine.Object.FindAnyObjectByType<V1GameFlow>();
                var menu = UnityEngine.Object.FindAnyObjectByType<MenuScreen>();
                if (flow == null || menu == null) throw new InvalidOperationException("Missing flow or menu.");
                var session = Boki.Core.RunSession.Instance;
                session.SetPaused(Boki.Core.PauseReason.ScriptedSequence, false);
                menu.PauseRequested.Invoke();
                if (Time.timeScale != 0f) throw new InvalidOperationException("Pause did not suspend gameplay.");
                menu.Activate(1);
                menu.Activate(0);
                menu.Activate(1);
                menu.Activate(0);
                if (Time.timeScale == 0f) throw new InvalidOperationException("Resume did not restore gameplay.");
                menu.PauseRequested.Invoke();
                menu.Activate(2);
                checkingRestart = 1;
                nextCheck = EditorApplication.timeSinceStartup + 5;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool("Boki.PauseSmoke", false);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Boki/Version 1/Upgrade Pause Menus")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in new[] { "V1_Pohorje", "V1_Maribor", "V1_FERI" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
                    V1GameFlow flow = null;
                    foreach (var root in scene.GetRootGameObjects())
                        if (root.GetComponentInChildren<V1GameFlow>(true) is V1GameFlow found) flow = found;
                    if (flow == null) throw new InvalidOperationException("Missing V1GameFlow in " + name);
                    var flowData = new SerializedObject(flow);
                    if (flowData.FindProperty("menu").objectReferenceValue != null) continue;
                    var preview = EditorSceneManager.OpenScene("Assets/Scenes/Menu_Preview.unity", OpenSceneMode.Additive);
                    MenuScreen source = null;
                    foreach (var root in preview.GetRootGameObjects())
                        if (root.GetComponent<MenuScreen>() is MenuScreen found) source = found;
                    if (source == null) throw new InvalidOperationException("Missing preview menu.");
                    var clone = UnityEngine.Object.Instantiate(source.gameObject);
                    SceneManager.MoveGameObjectToScene(clone, scene);
                    clone.name = "V1 Pause Menu";
                    foreach (var listener in clone.GetComponentsInChildren<AudioListener>(true))
                        UnityEngine.Object.DestroyImmediate(listener);
                    UnityEngine.Object.DestroyImmediate(clone.GetComponent<MenuPreview>());
                    UnityEngine.Object.DestroyImmediate(clone.transform.Find("Preview Notice").gameObject);
                    var menu = clone.GetComponent<MenuScreen>();
                    var data = new SerializedObject(menu);
                    data.FindProperty("startHidden").boolValue = true;
                    var panel = (GameObject)data.FindProperty("panel").objectReferenceValue;
                    // Keep the input component alive while hiding every visible menu layer.
                    foreach (string child in new[] { "Mockup Background", "Readability", "Screen Effects" })
                        clone.transform.Find(child).SetParent(panel.transform, false);
                    panel.transform.Find("Mockup Background").SetAsFirstSibling();
                    panel.transform.Find("Readability").SetSiblingIndex(1);
                    panel.SetActive(false);
                    data.ApplyModifiedPropertiesWithoutUndo();
                    clone.GetComponent<Canvas>().sortingOrder = 50;
                    flowData.FindProperty("menu").objectReferenceValue = menu;
                    flowData.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.CloseScene(preview, true);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("V1_PAUSE_MENU_WIRED: " + name);
                }
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
