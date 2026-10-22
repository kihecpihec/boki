using System;
using System.IO;
using Boki.Core;
using Boki.Enemies;
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
    public static class M4SceneBuilder
    {
        [MenuItem("Boki/Create M4 Graybox Levels")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(RunSession.PohorjeScene) || File.Exists(RunSession.MariborScene) || File.Exists(RunSession.FeriScene))
                throw new InvalidOperationException("M4 scenes exist; refusing to overwrite.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
                else if (SceneManager.GetSceneAt(i).path == M2SceneBuilder.ScenePath)
                    throw new InvalidOperationException("Close Combat_Test before creating M4 copies.");
            var previous = SceneManager.GetActiveScene();
            Create(RouteLevel.Forest, RunSession.PohorjeScene, previous);
            Create(RouteLevel.City, RunSession.MariborScene, previous);
            Create(RouteLevel.Feri, RunSession.FeriScene, previous);
            Debug.Log("M4_SCENES_CREATED");
        }

        private static void Create(RouteLevel level, string destination, Scene previous)
        {
            var scene = EditorSceneManager.OpenScene(M2SceneBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponent<MeshRenderer>() != null) Object.DestroyImmediate(root);
                var bootstrap = new GameObject("M4 Run Session").AddComponent<RunSession>();
                SetString(bootstrap, "forestDestination", RunSession.PohorjeScene);
                SetString(bootstrap, "cityDestination", RunSession.MariborScene);
                SetString(bootstrap, "feriDestination", RunSession.FeriScene);
                var health = Find<PlayerHealth>(scene);
                SetBool(health, "useRunSession", true);
                var combat = Find<CombatSession>(scene);
                SetBool(combat, "useRunSession", true);
                health.transform.position = new Vector3(0, 0.1f, -12);
                var manager = new GameObject("Route Manager").AddComponent<RouteManager>();
                SetInt(manager, "level", (int)level);
                Ref(manager, "player", health);
                var canvas = Find<Canvas>(scene);
                var status = Label(canvas.transform, "Route Status", level.ToString(), new Vector2(0, 240), new Vector2(1200, 80), 21);
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
                Cube("Route floor", new Vector3(0, -0.5f, 37), new Vector3(36, 1, 114), floor);
                Cube("West boundary", new Vector3(-18, 4, 37), new Vector3(1, 8, 114), material);
                Cube("East boundary", new Vector3(18, 4, 37), new Vector3(1, 8, 114), material);
                Cube("South boundary", new Vector3(0, 4, -20), new Vector3(36, 8, 1), material);
                Cube("North boundary", new Vector3(0, 4, 94), new Vector3(36, 8, 1), material);
                foreach (float x in new[] { -6f, 6f })
                    Cube("Route divider", new Vector3(x, 4, 30), new Vector3(1, 8, 61), material);
                BuildLocation(level, material, floor);
                var encounters = new GameObject[3];
                for (int i = 0; i < encounters.Length; i++)
                {
                    encounters[i] = new GameObject("Route " + (RouteChoice)(i + 1) + " Encounters");
                    encounters[i].SetActive(false);
                }
                var enemies = FindAll<MeleeEnemy>(scene);
                var positions = new[] { new Vector3(-14, 0.1f, 20), new Vector3(-10, 0.1f, 34),
                    new Vector3(-14, 0.1f, 48), new Vector3(2, 0.1f, 40) };
                for (int i = 0; i < enemies.Length; i++)
                {
                    enemies[i].transform.position = positions[i];
                    enemies[i].transform.SetParent(encounters[i < 3 ? 0 : 1].transform, true);
                }
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
                    ConfigureBranch(gate, (RouteChoice)(i + 1), encounters[i], material);
                }
                var endpoint = new GameObject(level == RouteLevel.Feri ? "Classroom Arrival" : "Level Exit");
                endpoint.transform.position = new Vector3(0, 1.5f, 82);
                var exitTrigger = endpoint.AddComponent<BoxCollider>();
                exitTrigger.isTrigger = true; exitTrigger.size = new Vector3(34, 3, 2);
                var exitBody = endpoint.AddComponent<Rigidbody>(); exitBody.isKinematic = true; exitBody.useGravity = false;
                if (level != RouteLevel.Feri)
                {
                    Sign("Exit sign", new Vector3(0, 3, 80), level == RouteLevel.Forest ? "MARIBOR →" : "FERI →");
                    Ref(endpoint.AddComponent<LevelExit>(), "manager", manager);
                }
                else
                {
                    var arrival = endpoint.AddComponent<ClassroomArrival>();
                    Ref(arrival, "manager", manager);
                    Ref(arrival, "message", Label(canvas.transform, "Classroom Status", "", new Vector2(0, 160), new Vector2(1100, 60), 26));
                    Sign("Classroom board", new Vector3(0, 3, 92), "PRISOTNOST: 0/1");
                    Cube("Lectern", new Vector3(0, 0.7f, 89), new Vector3(3, 1.4f, 1), material);
                }
                EditorSceneManager.SaveScene(scene, destination, true);
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        private static void ConfigureBranch(RouteGate gate, RouteChoice choice, GameObject encounter, Material material)
        {
            float x = ((int)choice - 2) * 12;
            var rear = Cube("Route " + choice + " Exit Barrier", new Vector3(x, 4, 60), new Vector3(11, 8, 0.6f), material);
            Ref(gate, "exitBlocker", rear.GetComponent<Collider>());
            Ref(gate, "exitVisual", rear.GetComponent<Renderer>());
            Ref(gate, "encounterRoot", encounter);
        }

        // Updates only the owned M4 scene instances, retaining scene and asset GUIDs.
        [MenuItem("Boki/Apply M4 Branch Isolation")]
        public static void ApplyBranchIsolation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool canRestore = setup.Length > 0;
            foreach (var item in setup) if (string.IsNullOrEmpty(item.path)) canRestore = false;
            try
            {
                foreach (var path in new[] { RunSession.PohorjeScene, RunSession.MariborScene, RunSession.FeriScene })
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    var gates = FindAll<RouteGate>(scene);
                    var enemies = FindAll<MeleeEnemy>(scene);
                    var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/M2/Wall.mat");
                    foreach (var gate in gates)
                    {
                        var serialized = new SerializedObject(gate);
                        if (serialized.FindProperty("exitBlocker").objectReferenceValue != null) continue;
                        var choice = (RouteChoice)serialized.FindProperty("choice").intValue;
                        var encounter = new GameObject("Route " + choice + " Encounters");
                        encounter.SetActive(false);
                        foreach (var enemy in enemies)
                        {
                            var branch = enemy.transform.position.x < -6 ? RouteChoice.A : RouteChoice.B;
                            if (branch == choice) enemy.transform.SetParent(encounter.transform, true);
                        }
                        ConfigureBranch(gate, choice, encounter, material);
                    }
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (canRestore) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Debug.Log("M4_BRANCH_ISOLATION_APPLIED");
        }

        private static void BuildLocation(RouteLevel level, Material wall, Material floor)
        {
            string[] names = level == RouteLevel.Forest ? new[] { "Strmina", "Gondola", "Rave tunel" }
                : level == RouteLevel.City ? new[] { "Tabor", "Lent", "Podzemni prehod" }
                : new[] { "Glavni vhod", "Dvorišče", "Servisni vhod" };
            Sign("Location", new Vector3(0, 5, -5), level == RouteLevel.Forest ? "POHORJE — CHOOSE A PATH"
                : level == RouteLevel.City ? "MARIBOR — YOUR PATH HAS CONSEQUENCES" : "FERI — REACH THE CLASSROOM");
            for (int i = 0; i < 3; i++)
                Sign("Branch name", new Vector3((i - 1) * 12, 6, 0), names[i]);
            if (level == RouteLevel.Forest)
            {
                Sign("Movement prompt", new Vector3(0, 2.5f, -17), "WASD MOVE  /  SPACE JUMP  /  SHIFT BOOST");
                for (int i = 0; i < 7; i++)
                {
                    var tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder); tree.name = "Slalom tree";
                    tree.transform.position = new Vector3(i % 2 == 0 ? -15 : -9, 2.5f, 10 + i * 6);
                    tree.transform.localScale = new Vector3(1.5f, 2.5f, 1.5f);
                    tree.GetComponent<Renderer>().sharedMaterial = wall;
                }
                OptionalRamp(0, 23, wall);
                OptionalRamp(-2, 47, wall);
                for (int i = 0; i < 3; i++)
                {
                    Cube("Gondola pole", new Vector3(4.5f, 4, 15 + i * 18), new Vector3(0.4f, 8, 0.4f), wall);
                    Cube("Cabin silhouette", new Vector3(2, 6, 15 + i * 18), new Vector3(2, 2, 2), wall);
                }
            }
            else if (level == RouteLevel.City)
            {
                for (int i = 0; i < 5; i++)
                {
                    var car = Cube("Street wreck", new Vector3(i % 2 == 0 ? -15 : -9, 0.7f, 12 + i * 9), new Vector3(2.5f, 1.4f, 4), wall);
                    car.transform.rotation = Quaternion.Euler(0, i % 2 == 0 ? 20 : -20, 0);
                }
                Cube("River silhouette", new Vector3(4, 0.1f, 32), new Vector3(2, 0.2f, 52), wall);
                Cube("Riverbank railing", new Vector3(2.5f, 1, 32), new Vector3(0.3f, 2, 52), wall);
                Cube("Bridge silhouette", new Vector3(0, 5, 30), new Vector3(11, 1, 3), wall);
                Cube("Bridge support", new Vector3(-4.5f, 2.5f, 30), new Vector3(1, 5, 1), wall);
                Cube("Riverside obstacle", new Vector3(-2.5f, 0.6f, 46), new Vector3(2, 1.2f, 2), wall);
            }
            else
            {
                Cube("Corridor roof", new Vector3(-12, 6, 32), new Vector3(11, 0.4f, 56), floor);
                for (int i = 0; i < 4; i++)
                {
                    Cube("Wrong classroom door", new Vector3(-17.3f, 1.4f, 12 + i * 12), new Vector3(0.15f, 2.8f, 1.6f), wall);
                    Sign("Door number", new Vector3(-16.8f, 3.3f, 12 + i * 12), "ROOM " + (404 + i));
                }
                Cube("Courtyard planter", new Vector3(0, 0.6f, 20), new Vector3(3, 1.2f, 6), wall);
                OptionalRamp(0, 40, wall);
                Cube("Side corridor roof", new Vector3(0, 6, 53), new Vector3(11, 0.4f, 14), floor);
                // Leave a wide classroom doorway after the route merge.
                Cube("Classroom west wall", new Vector3(-12, 4, 75), new Vector3(12, 8, 0.5f), wall);
                Cube("Classroom east wall", new Vector3(12, 4, 75), new Vector3(12, 8, 0.5f), wall);
                for (int i = 0; i < 3; i++)
                {
                    Cube("Classroom desk", new Vector3(-10, 0.5f, 80 + i * 4), new Vector3(4, 1, 1.5f), wall);
                    Cube("Classroom desk", new Vector3(10, 0.5f, 80 + i * 4), new Vector3(4, 1, 1.5f), wall);
                }
            }
            // Reserve C without a playable unfinished interior.
            Cube("Reserved C route", new Vector3(12, 4, 4), new Vector3(11, 8, 1), wall);
        }
        private static void OptionalRamp(float x, float z, Material material)
        {
            var up = Cube("Ramp up", new Vector3(x, 0.35f, z), new Vector3(3, 0.3f, 6), material);
            up.transform.rotation = Quaternion.Euler(-10, 0, 0);
            var down = Cube("Ramp down", new Vector3(x, 0.35f, z + 6), new Vector3(3, 0.3f, 6), material);
            down.transform.rotation = Quaternion.Euler(10, 0, 0);
        }
        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            var result = new System.Collections.Generic.List<T>();
            foreach (var root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
            return result.ToArray();
        }
        private static void SetString(Object target, string property, string value)
        { var so = new SerializedObject(target); so.FindProperty(property).stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
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
