using System;
using Boki.Battery;
using Boki.Core;
using Boki.Player;
using Boki.Routes;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Updates owned scene instances in place, preserving routes, geometry and existing GUIDs.
    public static class ScooterLightSetup
    {
        private const string PlayerPrefab = "Assets/Prefabs/Player/Boki_Rigged_Test.prefab";
        private const string ModelScene = "Assets/Scenes/Player_Model_Test.unity";
        private const string MaterialFolder = "Assets/Art/Battery";

        [MenuItem("Boki/Light Battery/Apply to Player and Gameplay Scenes")]
        public static void Apply()
        {
            RequireClean();
            Material charged = MaterialAsset("ChargeAvailable", new Color(0.2f, 1f, 0.75f), true);
            Material depleted = MaterialAsset("ChargeDepleted", new Color(0.24f, 0.28f, 0.32f), false);
            var prefab = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                ConfigurePlayer(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] { ModelScene, RunSession.PohorjeScene, RunSession.MariborScene, RunSession.FeriScene })
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    ScooterController movement = Find<ScooterController>(scene);
                    if (movement == null) throw new InvalidOperationException("Missing player in " + path);
                    LightBattery battery = ConfigurePlayer(movement.gameObject);
                    ConfigureUI(scene, battery);
                    ConfigureLighting(scene);
                    if (path == ModelScene)
                        AddSpace(scene, null, "Model test charges", battery, new Vector3(0, 2, 0), new Vector3(34, 6, 34),
                            new[] { new Vector3(-6, 0.7f, -5), new Vector3(6, 0.7f, -3), new Vector3(-6, 0.7f, 7) }, charged, depleted);
                    else
                    {
                        RouteManager manager = Find<RouteManager>(scene);
                        foreach (RouteGate gate in FindAll<RouteGate>(scene))
                        {
                            var serialized = new SerializedObject(gate);
                            int choice = serialized.FindProperty("choice").intValue;
                            var encounter = (GameObject)serialized.FindProperty("encounterRoot").objectReferenceValue;
                            if (encounter == null) throw new InvalidOperationException("Missing selected-only encounter root.");
                            float x = (choice - 2) * 12f;
                            Vector3[] points = choice == 2
                                ? new[] { new Vector3(-3, 0.7f, 14), new Vector3(0, 0.7f, 34), new Vector3(-3, 0.7f, 54) }
                                : new[] { new Vector3(x, 0.7f, 14), new Vector3(x + 1, 0.7f, 32), new Vector3(x - 1, 0.7f, 52) };
                            // Gameplay §5: Lent has more recovery opportunities than the other combat spaces.
                            if (path == RunSession.MariborScene && choice == 2)
                                points = new[] { new Vector3(-3, 0.7f, 14), new Vector3(0, 0.7f, 24),
                                    new Vector3(-2, 0.7f, 38), new Vector3(0, 0.7f, 54) };
                            AddSpace(scene, encounter.transform, "Battery space " + choice, battery,
                                new Vector3(x, 2, 32), new Vector3(10.5f, 6, 58), points, charged, depleted);
                        }
                        if (manager == null) throw new InvalidOperationException("Missing RouteManager.");
                        if (path == RunSession.FeriScene)
                            AddSpace(scene, null, "Classroom battery space", battery, new Vector3(0, 2, 87), new Vector3(34, 6, 12),
                                new[] { new Vector3(-8, 0.7f, 86), new Vector3(8, 0.7f, 86) }, charged, depleted);
                    }
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SCOOTER_LIGHT_SETUP_APPLIED");
        }

        private static LightBattery ConfigurePlayer(GameObject player)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null) health = player.AddComponent<PlayerHealth>();
            Set(health, "useRunSession", true);
            Transform lamp = player.transform.Find("Scooter Surrounding Light");
            if (lamp == null)
            {
                lamp = new GameObject("Scooter Surrounding Light").transform;
                lamp.SetParent(player.transform, false);
                lamp.localPosition = new Vector3(0, 1.5f, 0.3f);
            }
            Light light = lamp.GetComponent<Light>();
            if (light == null) light = lamp.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 5f;
            light.color = new Color(0.55f, 0.9f, 1f);
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            LightBattery battery = player.GetComponent<LightBattery>();
            if (battery == null) battery = player.AddComponent<LightBattery>();
            Set(battery, "player", health);
            Set(battery, "surroundingLight", light);
            return battery;
        }

        private static void ConfigureLighting(Scene scene)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.07f, 0.09f, 0.13f);
            RenderSettings.ambientEquatorColor = new Color(0.045f, 0.055f, 0.085f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.03f, 0.045f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.015f, 0.02f, 0.04f);
            RenderSettings.fogDensity = 0.035f;
            var probe = new UnityEngine.Rendering.SphericalHarmonicsL2();
            probe.AddAmbientLight(RenderSettings.ambientEquatorColor);
            RenderSettings.ambientProbe = probe;
            foreach (ThirdPersonCamera follow in FindAll<ThirdPersonCamera>(scene))
            {
                Camera camera = follow.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = RenderSettings.fogColor;
            }
            foreach (Light light in FindAll<Light>(scene))
                if (light.type == LightType.Directional) light.intensity = Mathf.Min(light.intensity, 0.18f);
        }

        private static void ConfigureUI(Scene scene, LightBattery battery)
        {
            Camera camera = Find<ThirdPersonCamera>(scene).GetComponent<Camera>();
            GameObject root = Root(scene, "Scooter Light UI");
            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Draw darkness behind gameplay HUDs, while text on this canvas remains above its overlay.
            canvas.sortingOrder = -10;
            GameObject veil = Child(root.transform, "Darkness", typeof(RectTransform), typeof(RawImage));
            Stretch(veil.GetComponent<RectTransform>());
            RawImage image = veil.GetComponent<RawImage>();
            image.raycastTarget = false;
            ScooterDarkness darkness = root.GetComponent<ScooterDarkness>();
            if (darkness == null) darkness = root.AddComponent<ScooterDarkness>();
            Set(darkness, "battery", battery);
            Set(darkness, "worldCamera", camera);
            Set(darkness, "overlay", image);
            Set(darkness, "darknessShader", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/UI/ScooterDarkness.shader"));
            GameObject label = Child(root.transform, "Battery Status", typeof(RectTransform), typeof(Text));
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(24, -70);
            rect.sizeDelta = new Vector2(460, 36);
            Text text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.raycastTarget = false;
            text.text = "LIGHT 100 / 100";
            BatteryView view = root.GetComponent<BatteryView>();
            if (view == null) view = root.AddComponent<BatteryView>();
            Set(view, "battery", battery);
            Set(view, "statusText", text);
        }

        private static void AddSpace(Scene scene, Transform parent, string name, LightBattery battery,
            Vector3 center, Vector3 size, Vector3[] points, Material charged, Material depleted)
        {
            GameObject root = parent == null ? Root(scene, name) : Child(parent, name);
            root.transform.position = center;
            BoxCollider volume = root.GetComponent<BoxCollider>();
            if (volume == null) volume = root.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = size;
            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body == null) body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            BatteryCombatSpace space = root.GetComponent<BatteryCombatSpace>();
            if (space == null) space = root.AddComponent<BatteryCombatSpace>();
            PlayerHealth player = battery.GetComponent<PlayerHealth>();
            Set(space, "battery", battery); Set(space, "player", player);
            Set(space, "spaceId", name); Set(space, "chargeCount", points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                GameObject pickup = Child(root.transform, "Battery Charge " + i);
                pickup.transform.position = points[i];
                SphereCollider trigger = pickup.GetComponent<SphereCollider>();
                if (trigger == null) trigger = pickup.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.9f;
                // Separate trigger bodies prevent the arena volume and pickup triggers becoming one compound body.
                Rigidbody pickupBody = pickup.GetComponent<Rigidbody>();
                if (pickupBody == null) pickupBody = pickup.AddComponent<Rigidbody>();
                pickupBody.isKinematic = true; pickupBody.useGravity = false;
                GameObject available = Visual(pickup.transform, "Available", charged, new Vector3(0.55f, 1f, 0.55f));
                GameObject empty = Visual(pickup.transform, "Recharging", depleted, new Vector3(0.65f, 0.15f, 0.65f));
                empty.SetActive(false);
                BatteryCharge charge = pickup.GetComponent<BatteryCharge>();
                if (charge == null) charge = pickup.AddComponent<BatteryCharge>();
                Set(charge, "space", space); Set(charge, "player", player); Set(charge, "slot", i);
                Set(charge, "chargedVisual", available); Set(charge, "depletedVisual", empty);
            }
        }

        private static GameObject Visual(Transform parent, string name, Material material, Vector3 scale)
        {
            Transform found = parent.Find(name);
            GameObject go = found != null ? found.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Material MaterialAsset(string name, Color color, bool glow)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Art", "Battery");
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 2f); }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static GameObject Root(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
        private static GameObject Child(Transform parent, string name, params Type[] components)
        {
            Transform child = parent.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name, components);
            go.transform.SetParent(parent, false);
            return go;
        }
        private static T Find<T>(Scene scene) where T : Component
        {
            T[] all = FindAll<T>(scene);
            return all.Length > 0 ? all[0] : null;
        }
        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            var found = new System.Collections.Generic.List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            return found.ToArray();
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Set(Object target, string field, Object value)
        { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(Object target, string field, bool value)
        { var so = new SerializedObject(target); so.FindProperty(field).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(Object target, string field, int value)
        { var so = new SerializedObject(target); so.FindProperty(field).intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(Object target, string field, string value)
        { var so = new SerializedObject(target); so.FindProperty(field).stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void RequireClean()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard dirty scenes before applying scooter light setup.");
        }
    }
}
