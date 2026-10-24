using System;
using System.Linq;
using Boki.Core;
using Boki.Player;
using Boki.UI;
using Boki.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Editor
{
    public sealed class UpgradeTestLab : EditorWindow
    {
        public const string ScenePath = "Assets/Scenes/Upgrade_Test.unity";
        private Vector2 scroll;

        [MenuItem("Boki/Upgrades/Open Test Controls")]
        public static void OpenControls() => GetWindow<UpgradeTestLab>("Upgrade Test");

        [MenuItem("Boki/Upgrades/Create Separate Test Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("Upgrade_Test already exists; preserving its edits.");
                return;
            }
            const string source = "Assets/Scenes/Player_Model_Test.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(source) == null)
                throw new InvalidOperationException("Player_Model_Test is missing; no replacement will be created.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(source, OpenSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene, ScenePath, true))
                    throw new InvalidOperationException("Could not copy Player_Model_Test.");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                UpgradeGameplaySetup.Wire(scene);
                foreach (var text in Find<Text>(scene))
                    if (text.name == "Model Test Controls" || text.name == "Sandbox Controls")
                        text.text = "UPGRADE TEST | WASD: ride | Space: jump | Shift: boost | Esc: arena panel\n"
                            + "Boki > Upgrades > Open Test Controls: XP, recovery, pause and reset\n"
                            + "Choose cards with 1/2/3 or arrows + Enter. Use enemy stations for laser and Panic Mode.";
                EditorSceneManager.SaveScene(scene);
                Debug.Log("UPGRADE_TEST_READY: real progression, movement course and eight enemy stations.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Open Upgrade_Test and enter Play Mode. Controls use real run progression; reset between builds. The movement course and enemy stations remain available.", MessageType.Info);
            var scene = SceneManager.GetActiveScene();
            if (!EditorApplication.isPlaying || scene.path != ScenePath || RunSession.Instance == null) return;
            var arena = Find<PlayerTestArena>(scene).SingleOrDefault();
            var player = Find<PlayerHealth>(scene).SingleOrDefault();
            var screen = Find<UpgradeScreen>(scene).SingleOrDefault();
            if (arena == null || player == null || screen == null) return;
            var session = RunSession.Instance;
            var state = session.State;
            var progression = state.Progression;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("HP", player.Current + " / " + player.Maximum);
            EditorGUILayout.LabelField("Battery", state.LightBattery.Current.ToString("F1") + " / " + state.LightBattery.Capacity.ToString("F0"));
            EditorGUILayout.LabelField("XP / selections", progression.XP + " / " + progression.Selections + " selections");
            EditorGUILayout.LabelField("Next threshold", progression.IsMaxLevel ? "MAX LEVEL" : progression.NextThreshold.ToString());
            EditorGUILayout.LabelField("Laser damage multiplier", progression.DamageMultiplier.ToString("F2"));
            EditorGUILayout.LabelField("Riding bonus", progression.MovementBonus.ToString("P0"));
            using (new EditorGUI.DisabledScope(!player.IsAlive))
            {
                if (GUILayout.Button("Add 1 XP")) progression.AddXp();
                if (GUILayout.Button("Reach next threshold") && !progression.IsMaxLevel)
                    progression.AddXp(Math.Max(0, progression.NextThreshold - progression.XP));
                if (GUILayout.Button("Queue all remaining selections")) progression.AddXp(33);
                if (GUILayout.Button("Take 30 damage / enable healing reward")) player.TakeDamage(30);
                if (GUILayout.Button("Set battery to 50%")) state.LightBattery.Current = state.LightBattery.Capacity * .5f;
                if (GUILayout.Button("Empty battery")) state.LightBattery.Current = 0;
                if (GUILayout.Button("Toggle independent menu pause"))
                    session.SetPaused(PauseReason.Menu, !state.Pause.Contains(PauseReason.Menu));
                if (GUILayout.Button("Kill player / test pending selection cleanup")) player.TakeDamage(int.MaxValue);
                if (progression.HasOffer)
                    for (int i = 0; i < progression.OfferCount; i++)
                        if (GUILayout.Button("Choose " + (i + 1) + ": " + progression.Offer(i))) screen.RequestChoice(i);
            }
            EditorGUILayout.Space();
            foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
                EditorGUILayout.LabelField(id.ToString(), progression.Count(id) + " selected; " + (progression.Eligible(id) ? "eligible" : "unavailable"));
            if (GUILayout.Button("Restart sandbox / clear XP and upgrades")) arena.RestartSandbox();
            EditorGUILayout.EndScrollView();
        }
    }
}
