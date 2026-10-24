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
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            SimplifyScene();
        }

        [MenuItem("Boki/Upgrades/Simplify Test Scene")]
        public static void SimplifyScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var player = Find<PlayerHealth>(scene).Single();
                foreach (var events in Find<UnityEngine.EventSystems.EventSystem>(scene))
                    events.transform.SetParent(null, true);
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root == player.transform.root.gameObject || root.GetComponentInChildren<Camera>(true) != null
                        || root.GetComponentInChildren<UpgradeScreen>(true) != null
                        || root.GetComponentInChildren<PlayerHud>(true) != null
                        || root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null
                        || root.name == "Directional Light" || root.name == "Floor") continue;
                    UnityEngine.Object.DestroyImmediate(root);
                }
                var floor = scene.GetRootGameObjects().Single(r => r.name == "Floor");
                floor.transform.position = new Vector3(0, -.5f, 0);
                floor.transform.localScale = new Vector3(24, 1, 28);
                player.transform.SetPositionAndRotation(new Vector3(0, .3f, -10), Quaternion.identity);
                var packs = new GameObject("Upgrade XP Drops");
                SceneManager.MoveGameObjectToScene(packs, scene);
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Effects/Boki/ArenaXP.mat");
                for (int i = 0; i < 40; i++)
                {
                    var pack = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    pack.name = "XP Pack " + (i + 1);
                    pack.transform.SetParent(packs.transform);
                    pack.transform.position = new Vector3(-8 + (i % 5) * 4, .45f, -6 + (i / 5) * 2.4f);
                    pack.transform.localScale = Vector3.one * .65f;
                    pack.GetComponent<Renderer>().sharedMaterial = material;
                    pack.GetComponent<SphereCollider>().isTrigger = true;
                    var body = pack.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    pack.AddComponent<XpPickup>();
                }
                foreach (var text in Find<Text>(scene))
                    if (text.name == "Model Test Controls" || text.name == "Sandbox Controls")
                        text.text = "UPGRADE TEST | WASD: ride | Space: jump | Shift: boost\nCollect gold XP. Hover cards or use arrows + Enter / 1, 2, 3.\nBoki > Upgrades > Open Test Controls: recovery, pause and reset";
                WireVisuals(scene);
                EditorSceneManager.SaveScene(scene);
                scene = EditorSceneManager.OpenScene("Assets/Scenes/Player_Model_Test.unity", OpenSceneMode.Single);
                WireVisuals(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("UPGRADE_TEST_SIMPLIFIED: 40 XP packs; upgrade hardware wired in both model scenes.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        internal static void WireVisuals(Scene scene)
        {
            var manager = Find<UpgradeManager>(scene).Single();
            var presentation = Find<ScooterPresentation>(scene).Single();
            var data = new SerializedObject(presentation);
            var front = (Transform)data.FindProperty("frontWheel").objectReferenceValue;
            var rear = (Transform)data.FindProperty("rearWheel").objectReferenceValue;
            var parent = front.parent;
            var old = parent.Find("Upgrade Hardware");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var hardware = new GameObject("Upgrade Hardware").transform;
            hardware.SetParent(parent, false);
            var serialized = new SerializedObject(manager);
            var batteries = serialized.FindProperty("batteryModules");
            var lasers = serialized.FindProperty("laserModules");
            batteries.arraySize = 2;
            lasers.arraySize = 3;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Effects/Boki/ArenaXP.mat");
            Vector3 deck = (front.position + rear.position) * .5f + Vector3.up * .12f;
            var laser = Find<Boki.Combat.AutoLaser>(scene).Single();
            var origin = (Transform)new SerializedObject(laser).FindProperty("beamOrigin").objectReferenceValue;
            for (int i = 0; i < 2; i++)
                batteries.GetArrayElementAtIndex(i).objectReferenceValue = Part("Battery expansion " + (i + 1),
                    deck + manager.transform.right * (i == 0 ? -.16f : .16f), new Vector3(.11f, .13f, .38f));
            for (int i = 0; i < 3; i++)
                lasers.GetArrayElementAtIndex(i).objectReferenceValue = Part("Laser amplifier " + (i + 1),
                    origin.position - manager.transform.forward * (.08f + i * .09f), new Vector3(.17f, .14f, .055f));
            serialized.FindProperty("secondEmitter").objectReferenceValue = Part("Second laser emitter",
                origin.position + manager.transform.right * .19f, new Vector3(.12f, .12f, .25f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);

            GameObject Part(string name, Vector3 position, Vector3 scale)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = name;
                part.transform.SetParent(hardware, false);
                part.transform.SetPositionAndRotation(position, manager.transform.rotation);
                // World dimensions stay readable even with imported model scale.
                var size = part.transform.lossyScale;
                part.transform.localScale = new Vector3(scale.x / size.x, scale.y / size.y, scale.z / size.z);
                part.GetComponent<Renderer>().sharedMaterial = material;
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                part.SetActive(false);
                return part;
            }
        }

        private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Open Upgrade_Test and enter Play Mode. Controls use real run progression; reset between builds. Collect XP in the compact riding area.", MessageType.Info);
            var scene = SceneManager.GetActiveScene();
            if (!EditorApplication.isPlaying || scene.path != ScenePath || RunSession.Instance == null) return;
            var arena = Find<PlayerTestArena>(scene).SingleOrDefault();
            var player = Find<PlayerHealth>(scene).SingleOrDefault();
            var screen = Find<UpgradeScreen>(scene).SingleOrDefault();
            if (player == null || screen == null) return;
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
            if (GUILayout.Button("Restart sandbox / clear XP and upgrades"))
            {
                session.State.NewRun(state.AvailableUnlocks, Time.realtimeSinceStartupAsDouble);
                EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
