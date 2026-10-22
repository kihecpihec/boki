using System;
using System.IO;
using Boki.Core;
using Boki.Player;
using Boki.Routes;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    public static class M3SceneBuilder
    {
        [MenuItem("Boki/Create M3 Route Arenas")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(RunSession.ForestScene) || File.Exists(RunSession.CityScene))
                throw new InvalidOperationException("M3 scenes exist; refusing to overwrite.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
                else if (SceneManager.GetSceneAt(i).path == M2SceneBuilder.ScenePath)
                    throw new InvalidOperationException("Close Combat_Test before creating M3 copies.");
            var previous = SceneManager.GetActiveScene();
            Create(RouteLevel.Forest, RunSession.ForestScene, previous);
            Create(RouteLevel.City, RunSession.CityScene, previous);
            Debug.Log("M3_SCENES_CREATED");
        }

        private static void Create(RouteLevel level, string destination, Scene previous)
        {
            var scene = EditorSceneManager.OpenScene(M2SceneBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponent<MeshRenderer>() != null) Object.DestroyImmediate(root);
                var health = Find<PlayerHealth>(scene);
                SetBool(health, "useRunSession", true);
                var combat = Find<CombatSession>(scene);
                SetBool(combat, "useRunSession", true);
                health.transform.position = new Vector3(0, 0.1f, -9);
                var manager = new GameObject("Route Manager").AddComponent<RouteManager>();
                SetInt(manager, "level", (int)level);
                Ref(manager, "player", health);
                var canvas = Find<Canvas>(scene);
                var status = Label(canvas.transform, "Route Status", level.ToString(), new Vector2(0, 240), new Vector2(1100, 70), 24);
                Ref(manager, "status", status);
                var buttonObject = new GameObject("Start New Run", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(canvas.transform, false);
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(440, 300); rect.sizeDelta = new Vector2(260, 50);
                var button = buttonObject.GetComponent<Button>();
                button.targetGraphic = buttonObject.GetComponent<Image>();
                buttonObject.GetComponent<Image>().color = new Color(0.1f, 0.25f, 0.3f);
                Label(buttonObject.transform, "Label", "Start New Run", Vector2.zero, new Vector2(260, 50), 22);
                UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(manager.StartNewRun));

                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/M2/Wall.mat");
                var floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/M2/Floor.mat");
                Cube("Route floor", new Vector3(0, -0.5f, 9), new Vector3(36, 1, 48), floor);
                Cube("West boundary", new Vector3(-18, 4, 9), new Vector3(1, 8, 48), material);
                Cube("East boundary", new Vector3(18, 4, 9), new Vector3(1, 8, 48), material);
                Cube("South boundary", new Vector3(0, 4, -15), new Vector3(36, 8, 1), material);
                Cube("North boundary", new Vector3(0, 4, 33), new Vector3(36, 8, 1), material);
                foreach (float x in new[] { -6f, 6f })
                    Cube("Lane divider", new Vector3(x, 4, 9), new Vector3(1, 8, 19), material);
                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * 12;
                    var root = new GameObject("Gate " + (RouteChoice)(i + 1));
                    root.transform.position = new Vector3(x, 0, 0);
                    var gate = root.AddComponent<RouteGate>();
                    var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                    var trigger = root.AddComponent<BoxCollider>(); trigger.isTrigger = true;
                    trigger.center = new Vector3(0, 1.5f, 2); trigger.size = new Vector3(10.5f, 3, 1);
                    var blocker = Cube("Gate barrier", new Vector3(x, 4, 0), new Vector3(11, 8, 0.6f), material);
                    blocker.transform.SetParent(root.transform, true);
                    var sign = Sign("Route sign", new Vector3(x, 4.5f, -0.5f), ""); sign.transform.SetParent(root.transform, true);
                    Ref(gate, "manager", manager); SetInt(gate, "choice", i + 1);
                    Ref(gate, "blocker", blocker.GetComponent<Collider>());
                    Ref(gate, "selectionTrigger", trigger); Ref(gate, "barrierVisual", blocker.GetComponent<Renderer>());
                    Ref(gate, "sign", sign);
                }
                if (level == RouteLevel.Forest)
                {
                    Sign("Exit sign", new Vector3(0, 3, 28), "TO CITY - CHOOSE A ROUTE FIRST");
                    var exitObject = new GameObject("Forest Exit"); exitObject.transform.position = new Vector3(0, 1.5f, 29);
                    var trigger = exitObject.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(34, 3, 2);
                    var body = exitObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                    Ref(exitObject.AddComponent<LevelExit>(), "manager", manager);
                }
                else Sign("Test endpoint", new Vector3(0, 3, 28), "M3 TEST END - FERI IN M4");
                EditorSceneManager.SaveScene(scene, destination, true);
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            { var component = root.GetComponentInChildren<T>(true); if (component != null) return component; }
            throw new InvalidOperationException("Missing " + typeof(T).Name);
        }
        private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name;
            item.transform.position = position; item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material; return item;
        }
        private static TextMesh Sign(string name, Vector3 position, string content)
        {
            var sign = new GameObject(name).AddComponent<TextMesh>(); sign.transform.position = position;
            sign.anchor = TextAnchor.MiddleCenter; sign.alignment = TextAlignment.Center;
            sign.characterSize = 0.045f; sign.fontSize = 48; sign.text = content; return sign;
        }
        private static Text Label(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text)); item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = item.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white; text.raycastTarget = false; return text;
        }
        private static void Ref(Object target, string property, Object value)
        { var so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetInt(Object target, string property, int value)
        { var so = new SerializedObject(target); so.FindProperty(property).intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetBool(Object target, string property, bool value)
        { var so = new SerializedObject(target); so.FindProperty(property).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
