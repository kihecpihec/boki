using System;
using System.Linq;
using Boki.Player;
using Boki.UI;
using Boki.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Editor
{
    public static class UpgradeGameplaySetup
    {
        [MenuItem("Boki/Upgrades/Wire Player Model Test")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            const string path = "Assets/Scenes/Player_Model_Test.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new InvalidOperationException("Player_Model_Test is missing; no replacement will be created.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Wire(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("UPGRADE_GAMEPLAY_WIRED: Player_Model_Test; arena preserved, maps and shared prefabs untouched.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        internal static void Wire(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            PlayerHealth player = roots.SelectMany(r => r.GetComponentsInChildren<PlayerHealth>(true)).Single();
            Set(player, "useRunSession", true);
            UpgradeScreen screen = roots.SelectMany(r => r.GetComponentsInChildren<UpgradeScreen>(true)).SingleOrDefault();
            if (screen == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Upgrades/UpgradeScreen.prefab");
                if (prefab == null) throw new InvalidOperationException("Existing upgrade mockup prefab is missing.");
                screen = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<UpgradeScreen>();
            }
            var screenData = new SerializedObject(screen);
            screenData.FindProperty("quickSelect").arraySize = 3;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Upgrades/UpgradeInput.asset"))
            {
                if (!(asset is InputActionReference reference)) continue;
                if (reference.name.StartsWith("Choice", StringComparison.Ordinal))
                    screenData.FindProperty("quickSelect").GetArrayElementAtIndex(int.Parse(reference.name.Substring(6)) - 1)
                        .objectReferenceValue = reference;
                else screenData.FindProperty(reference.name.ToLowerInvariant()).objectReferenceValue = reference;
            }
            screenData.ApplyModifiedPropertiesWithoutUndo();
            // The arena panel uses order 30; upgrade selection must cover and intercept it.
            screen.GetComponent<Canvas>().sortingOrder = 50;
            PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
            PrefabUtility.RecordPrefabInstancePropertyModifications(screen.GetComponent<Canvas>());
            UpgradeManager manager = player.GetComponent<UpgradeManager>();
            if (manager == null) manager = player.gameObject.AddComponent<UpgradeManager>();
            Set(manager, "screen", screen);
            Set(manager, "player", player);
            var events = roots.SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).SingleOrDefault();
            if (events == null)
            {
                var eventObject = new GameObject("Upgrade Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events = eventObject.GetComponent<EventSystem>();
                eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            events.sendNavigationEvents = false;
            // The arena binds its active clone at runtime; never create another shared-state pursuer.
            Set(player.GetComponent<ScooterController>(), "guilt", null);
            var hud = roots.SelectMany(r => r.GetComponentsInChildren<PlayerHud>(true)).Single();
            Set(hud, "guilt", null);
            foreach (var old in roots.Where(r => r.name == "Upgrade Guilt Playtest" || r.name.StartsWith("Upgrade Guilt Spawn ", StringComparison.Ordinal)))
                UnityEngine.Object.DestroyImmediate(old);
            Text controls = roots.SelectMany(r => r.GetComponentsInChildren<Text>(true)).FirstOrDefault(t => t.name == "Model Test Controls");
            if (controls != null) controls.text = "ENEMY SANDBOX | WASD: ride | Space: jump | Shift: boost | Esc: test panel\nH: damage | B: drain battery | Gold: XP | Green: battery +25\nXP thresholds: 3 / 7 / 12 / 18 / 25 / 33 | Upgrade: 1/2/3 or arrows + Enter";
            foreach (Text text in roots.SelectMany(r => r.GetComponentsInChildren<Text>(true)))
                if (text.text.Contains("XP tests collection and HUD feedback; upgrade selection is not implemented."))
                    text.text = text.text.Replace("XP tests collection and HUD feedback; upgrade selection is not implemented.", "XP awards real upgrades; choose a card when the run pauses.");
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<ScooterController>());
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(UnityEngine.Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
