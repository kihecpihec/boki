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

        [MenuItem("Boki/Light Battery/Apply Darkness to V1 and Model Test")]
        public static void ApplyDarkness()
        {
            RequireClean();
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] { ModelScene, "Assets/Scenes/V1_Pohorje.unity",
                    "Assets/Scenes/V1_Maribor.unity", "Assets/Scenes/V1_FERI.unity" })
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    ConfigureLighting(scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Debug.Log("V1_DARKNESS_APPLIED");
        }

        [MenuItem("Boki/Light Battery/Apply Surrounding Scooter Light to V1 and Model Test")]
        public static void ApplyV1Headlights()
        {
            RequireClean();
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] { ModelScene, V1SceneSetup.Scenes[0], V1SceneSetup.Scenes[1], V1SceneSetup.Scenes[2] })
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                        throw new InvalidOperationException("Missing V1 scene: " + path);
                foreach (string path in new[] { ModelScene, V1SceneSetup.Scenes[0], V1SceneSetup.Scenes[1], V1SceneSetup.Scenes[2] })
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    ConfigureWideWhiteLight(scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("V1_WIDE_WHITE_LIGHT_SAVED " + path);
                }
            }
            finally
            {
                if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        internal static void ConfigureWideWhiteLight(Scene scene)
        {
            LightBattery battery = Find<LightBattery>(scene);
            ScooterDarkness darkness = Find<ScooterDarkness>(scene);
            if (battery == null || darkness == null)
                throw new InvalidOperationException("Missing scooter light or darkness overlay in " + scene.path);
            var settings = new SerializedObject(battery);
            var lamp = settings.FindProperty("surroundingLight").objectReferenceValue as Light;
            var mount = settings.FindProperty("headlightMount").objectReferenceValue as Transform;
            if (mount == null)
                foreach (Transform child in battery.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Socket_Headlight") { mount = child; break; }
            if (lamp == null || mount == null)
                throw new InvalidOperationException("Assign the scooter lamp and headlight socket in " + scene.path);
            settings.FindProperty("headlightMount").objectReferenceValue = mount;
            settings.FindProperty("intensity").floatValue = 5f;
            settings.FindProperty("headlightRange").floatValue = 28f;
            settings.FindProperty("headlightHeightOffset").floatValue = 0.1f;
            settings.FindProperty("headlightBrightnessMultiplier").floatValue = 12f;
            settings.FindProperty("fillIntensity").floatValue = 0.55f;
            settings.FindProperty("fillRange").floatValue = 2.5f;
            settings.FindProperty("nearbyBrightnessMultiplier").floatValue = 1.3f;
            settings.FindProperty("nearbyRangeMultiplier").floatValue = 1.4f;
            settings.FindProperty("riderFillPosition").vector3Value = new Vector3(0f, 1.6f, -0.9f);
            settings.FindProperty("emptyFillIntensity").floatValue = 0.12f;
            settings.FindProperty("emptyFillRange").floatValue = 2.2f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            // Pin presentation values even when they currently equal inherited script defaults.
            if (PrefabUtility.IsPartOfPrefabInstance(battery))
            {
                Object source = PrefabUtility.GetCorrespondingObjectFromSource(battery);
                var overrides = new System.Collections.Generic.List<PropertyModification>(
                    PrefabUtility.GetPropertyModifications(battery));
                overrides.RemoveAll(item => item.target == source
                    && (item.propertyPath == "headlightAngle" || item.propertyPath == "headlightDownwardAngle"));
                SerializedProperty property = settings.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.Float) continue;
                    string path = property.propertyPath;
                    overrides.RemoveAll(item => item.target == source && item.propertyPath == path);
                    overrides.Add(new PropertyModification { target = source, propertyPath = path,
                        value = property.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                }
                PrefabUtility.SetPropertyModifications(battery, overrides.ToArray());
            }
            lamp.type = LightType.Point;
            lamp.color = Color.white;
            lamp.useColorTemperature = false;
            lamp.range = 28f;
            lamp.intensity = 60f;
            lamp.shadows = LightShadows.None;
            lamp.transform.position = mount.position + battery.transform.up * 0.1f;
            lamp.transform.rotation = battery.transform.rotation;
            var overlay = new SerializedObject(darkness);
            overlay.FindProperty("lightRadius").floatValue = 7f;
            overlay.FindProperty("darknessOpacity").floatValue = 0.3f;
            overlay.FindProperty("forwardOffset").floatValue = 0f;
            overlay.FindProperty("radiusScale").floatValue = 0.75f;
            overlay.FindProperty("extraDarkness").floatValue = 0.03f;
            overlay.FindProperty("chargedRadiusMultiplier").floatValue = 3f;
            overlay.FindProperty("chargedOpacityMultiplier").floatValue = 0.65f;
            overlay.ApplyModifiedPropertiesWithoutUndo();
            foreach (Object changed in new Object[] { battery, lamp, lamp.transform, darkness })
                if (PrefabUtility.IsPartOfPrefabInstance(changed))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
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
            light.range = 28f;
            light.intensity = 60f;
            light.color = Color.white;
            light.useColorTemperature = false;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            LightBattery battery = player.GetComponent<LightBattery>();
            if (battery == null) battery = player.AddComponent<LightBattery>();
            Set(battery, "player", health);
            Set(battery, "surroundingLight", light);
            return battery;
        }

        internal static void ConfigureLighting(Scene scene)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.006f, 0.006f, 0.006f);
            RenderSettings.ambientSkyColor = RenderSettings.ambientLight;
            RenderSettings.ambientEquatorColor = RenderSettings.ambientLight;
            RenderSettings.ambientGroundColor = RenderSettings.ambientLight;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogDensity = 0.045f;
            var probe = new UnityEngine.Rendering.SphericalHarmonicsL2();
            probe.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = probe;
            foreach (ThirdPersonCamera follow in FindAll<ThirdPersonCamera>(scene))
            {
                Camera camera = follow.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }
            foreach (Light light in FindAll<Light>(scene))
            {
                if (light.type == LightType.Directional) light.intensity = 0f;
                // Keep landmarks local; they must not illuminate an entire encounter without battery.
                else if (light.GetComponentInParent<LightBattery>() == null)
                {
                    light.range = Mathf.Min(light.range, 3f);
                    light.intensity = Mathf.Min(light.intensity, 0.35f);
                    light.shadows = LightShadows.None;
                }
            }
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
