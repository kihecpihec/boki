using System;
using System.Linq;
using Boki.Battery;
using Boki.Combat;
using Boki.Enemies;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Editor-only diagnostics; tuning applies to the active instance, never shared prefabs.
    public sealed class EnemyCombatLab : EditorWindow
    {
        public const string ScenePath = "Assets/Scenes/Enemy_Combat_Test.unity";
        private Vector2 scroll;
        private int hitDamage = 10;
        private static readonly string[] CourseRoots =
        {
            "Jump obstacle", "Camera corner", "East wall", "Overhead obstacle", "South wall",
            "North wall", "Camera wall", "Ramp", "Overhead support", "West wall", "Model test charges", "Floor"
        };

        [MenuItem("Boki/Enemies/Open Combat Lab Controls")]
        public static void OpenControls() => GetWindow<EnemyCombatLab>("Enemy Combat Lab");

        [MenuItem("Boki/Enemies/Create Separate Combat Test Scene")]
        public static void CreateScene() => BuildScene(false);

        public static void RefreshForIntegration() => BuildScene(true);

        private static void BuildScene(bool refresh)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save dirty scenes first.");
            if (!refresh && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("Enemy_Combat_Test already exists; preserving its edits.");
                return;
            }
            const string source = "Assets/Scenes/Player_Model_Test.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(source) == null)
                throw new InvalidOperationException("Player_Model_Test is missing.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var sourceScene = EditorSceneManager.OpenScene(source, OpenSceneMode.Single);
                if (!EditorSceneManager.SaveScene(sourceScene, ScenePath, true))
                    throw new InvalidOperationException("Could not copy Player_Model_Test.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var arena = Find<PlayerTestArena>(scene).Single();
                var player = Find<PlayerHealth>(scene).Single();
                foreach (var root in scene.GetRootGameObjects())
                    if (CourseRoots.Contains(root.name)) Object.DestroyImmediate(root);
                var settings = new SerializedObject(arena);
                var hub = (Transform)settings.FindProperty("hub").objectReferenceValue;
                settings.FindProperty("movementEntry").objectReferenceValue = hub;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var effectsSettings = new SerializedObject(player.GetComponent<BokiModelTestEffects>());
                var charges = Find<BatteryCharge>(scene);
                var chargeArray = effectsSettings.FindProperty("charges");
                chargeArray.arraySize = charges.Length;
                for (int i = 0; i < charges.Length; i++)
                    chargeArray.GetArrayElementAtIndex(i).objectReferenceValue = charges[i];
                effectsSettings.ApplyModifiedPropertiesWithoutUndo();
                var marker = arena.transform.Find("Movement Arena Spawn");
                if (marker != null) Object.DestroyImmediate(marker.gameObject);
                var movementButton = arena.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(b => b.name == "Original movement area");
                if (movementButton != null) Object.DestroyImmediate(movementButton.gameObject);
                foreach (Text text in Find<Text>(scene))
                    if (text.name == "Sandbox Controls" || text.name == "Model Test Controls")
                        text.text = "ENEMY COMBAT TEST | WASD: ride | Space: jump | Shift: boost | Esc: panel\n"
                            + "Select an enemy; disable laser to observe attacks. Reset enemy to replay.\n"
                            + "Boki > Enemies > Open Combat Lab Controls: damage, kill, live tuning.\n"
                            + "Guilt is invulnerable. Tuning active instances resets with the encounter.";
                player.transform.SetPositionAndRotation(hub.position, hub.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                var laser = player.GetComponent<AutoLaser>();
                laser.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(laser);
                ((Text)settings.FindProperty("laserLabel").objectReferenceValue).text = "Laser: OFF (observe attacks)";
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("ENEMY_COMBAT_LAB_READY: eight stations; source scene and prefabs unchanged.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [MenuItem("Boki/Enemies/Simplify Combat Test Stages")]
        public static void SimplifyStages()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save dirty scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var arena = Find<PlayerTestArena>(scene).Single();
                var stages = Find<EnemyTestStation>(scene);
                if (stages.Length != 8) throw new InvalidOperationException("Expected eight enemy stages.");
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == "MovementHUD" || CourseRoots.Contains(root.name))
                        Object.DestroyImmediate(root);
                var packs = arena.transform.Find("XP Packs");
                if (packs != null) Object.DestroyImmediate(packs.gameObject);
                foreach (var charge in Find<BatteryCharge>(scene)) Object.DestroyImmediate(charge.gameObject);
                foreach (var space in Find<BatteryCombatSpace>(scene)) Object.DestroyImmediate(space.gameObject);
                foreach (var effects in Find<BokiModelTestEffects>(scene)) Object.DestroyImmediate(effects);
                foreach (var text in Find<Text>(scene))
                {
                    if (text.name == "Model Test Controls") Object.DestroyImmediate(text.gameObject);
                    else if (text.name == "Sandbox Controls")
                        text.text = "ENEMY STAGES | WASD: ride | Mouse: look | Space: jump | Shift: boost\n"
                            + "Choose an enemy. Esc: use buttons. Reset enemy: try again.\n"
                            + "Laser OFF: observe attacks. Laser ON: fight.\n"
                            + "Restart restores health. Guilt is invulnerable.";
                }
                var player = Find<PlayerHealth>(scene).Single();
                var settings = new SerializedObject(arena);
                var hub = (Transform)settings.FindProperty("hub").objectReferenceValue;
                settings.FindProperty("movementEntry").objectReferenceValue = hub;
                settings.ApplyModifiedPropertiesWithoutUndo();
                player.transform.SetPositionAndRotation(hub.position, hub.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                // Keep the shared player and HUD wiring required by the encounter controls.
                foreach (var stage in stages)
                {
                    var stageSettings = new SerializedObject(stage);
                    if (stageSettings.FindProperty("entry").objectReferenceValue == null
                        || stageSettings.FindProperty("enemyTemplate").objectReferenceValue == null
                        || stageSettings.FindProperty("volume").objectReferenceValue == null)
                        throw new InvalidOperationException("Incomplete enemy stage: " + stage.name);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save enemy stages.");
                Debug.Log("ENEMY_STAGES_SIMPLIFIED: eight wired stages; model controls, sample XP and battery stations removed.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Open Enemy_Combat_Test and enter Play Mode. Changes below affect only the active enemy and reset when it respawns.", MessageType.Info);
            var scene = SceneManager.GetActiveScene();
            if (!EditorApplication.isPlaying || scene.path != ScenePath) return;
            var arena = Find<PlayerTestArena>(scene).SingleOrDefault();
            if (arena == null) return;
            var player = Find<PlayerHealth>(scene).SingleOrDefault();
            if (player != null) EditorGUILayout.LabelField("Player health", player.Current + " / " + player.Maximum);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var station in Find<EnemyTestStation>(scene).OrderBy(s => s.name))
                if (GUILayout.Button(station.name)) arena.Visit(station);
            if (GUILayout.Button("Reset encounter")) arena.ResetEncounter();
            if (GUILayout.Button("Restart sandbox / restore player")) arena.RestartSandbox();
            if (GUILayout.Button("Toggle automatic laser")) arena.ToggleLaser();
            var enemy = arena.ActiveStation != null ? arena.ActiveStation.Enemy : null;
            if (enemy != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(enemy.name, EditorStyles.boldLabel);
                var health = enemy.GetComponent<EnemyHealth>();
                if (health != null)
                {
                    EditorGUILayout.LabelField("Health", health.Current + " / " + health.Maximum);
                    hitDamage = Mathf.Max(1, EditorGUILayout.IntField("Test hit damage", hitDamage));
                    if (GUILayout.Button("Apply hit")) health.TakeDamage(hitDamage);
                    if (GUILayout.Button("Kill / observe death")) health.TakeDamage(int.MaxValue);
                }
                else EditorGUILayout.LabelField("Invulnerable — no death test (Guilt)");
                if (GUILayout.Button("Select active enemy in Inspector")) Selection.activeGameObject = enemy;
                foreach (var adapter in enemy.GetComponents<MonoBehaviour>())
                {
                    if (adapter == null || adapter is EnemyHealth) continue;
                    var serialized = new SerializedObject(adapter);
                    var property = serialized.GetIterator();
                    bool header = false;
                    while (property.NextVisible(true))
                    {
                        if (property.depth != 0 || (property.propertyType != SerializedPropertyType.Float
                            && property.propertyType != SerializedPropertyType.Integer)) continue;
                        if (!header) { EditorGUILayout.LabelField(adapter.GetType().Name, EditorStyles.boldLabel); header = true; }
                        EditorGUILayout.PropertyField(property);
                    }
                    serialized.ApplyModifiedProperties();
                }
                EditorGUILayout.HelpBox("Speed and damage fields are live where the adapter reads them directly. Scheduler/cooldown settings captured on enable need an Inspector disable/enable to reinitialize. Use the Inspector for animation controllers and projectile templates.", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
