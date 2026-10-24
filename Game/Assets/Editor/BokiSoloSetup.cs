using System;
using System.Linq;
using Boki.Battery;
using Boki.Combat;
using Boki.Player;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    public static class BokiSoloSetup
    {
        public const string ScenePath = "Assets/Scenes/Boki_Solo_Test.unity";

        [MenuItem("Boki/Player Model/Create Solo Scene and Fix Scooter Emitters")]
        public static void Apply()
        {
            RequireClean();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BokiModelTestSetup.ScenePath) == null)
                throw new InvalidOperationException("Player_Model_Test is missing; refusing to create a replacement.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene source = EditorSceneManager.OpenScene(BokiModelTestSetup.ScenePath, OpenSceneMode.Single);
                ConfigureEmitters(source);
                ConfigureRidingEffects(source);
                EditorSceneManager.SaveScene(source);
                bool created = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null;
                if (created && !AssetDatabase.CopyAsset(BokiModelTestSetup.ScenePath, ScenePath))
                    throw new InvalidOperationException("Could not copy Player_Model_Test.");
                Scene solo = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (created) StripArena(solo);
                ConfigureEmitters(solo);
                ConfigureRidingEffects(solo);
                Validate(solo);
                EditorSceneManager.SaveScene(solo);
                AssetDatabase.SaveAssets();
                Debug.Log("BOKI_SOLO_READY: " + ScenePath);
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void ConfigureEmitters(Scene scene)
        {
            var movement = Find<ScooterController>(scene).Single();
            Transform visuals = movement.transform.Find("Visuals");
            Transform muzzle = visuals.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Socket_LaserMuzzle");
            Transform lampSocket = visuals.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Socket_Headlight");
            var laser = movement.GetComponent<AutoLaser>();
            var settings = new SerializedObject(laser);
            Transform origin = (Transform)settings.FindProperty("beamOrigin").objectReferenceValue;
            origin.SetParent(muzzle, false);
            // The model's tiny emitter is offset left of the centre of its housing.
            origin.position = muzzle.position + movement.transform.right * -0.017f + movement.transform.forward * 0.004f;
            origin.rotation = movement.transform.rotation;
            var beam = (LineRenderer)settings.FindProperty("beam").objectReferenceValue;
            beam.startWidth = 0.018f;
            beam.endWidth = 0.008f;
            beam.startColor = new Color(0.75f, 1f, 1f);
            beam.endColor = new Color(0.15f, 0.85f, 1f);
            beam.numCapVertices = 3;
            beam.shadowCastingMode = ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.enabled = false;
            var mount = lampSocket.Find("Headlight Mount");
            if (mount == null)
            {
                mount = new GameObject("Headlight Mount").transform;
                mount.SetParent(lampSocket, false);
            }
            mount.position = lampSocket.position + movement.transform.forward * 0.012f;
            mount.rotation = movement.transform.rotation;
            // FBX sockets carry the centimetre conversion; keep light offsets in metres.
            Vector3 socketScale = lampSocket.lossyScale;
            mount.localScale = new Vector3(1f / socketScale.x, 1f / socketScale.y, 1f / socketScale.z);
            var battery = movement.GetComponent<LightBattery>();
            var lightSettings = new SerializedObject(battery);
            lightSettings.FindProperty("headlightMount").objectReferenceValue = mount;
            lightSettings.FindProperty("headlightAngle").floatValue = 140f;
            lightSettings.FindProperty("headlightDownwardAngle").floatValue = 18f;
            lightSettings.FindProperty("headlightRange").floatValue = 24f;
            lightSettings.FindProperty("headlightBrightnessMultiplier").floatValue = 24f;
            lightSettings.FindProperty("nearbyBrightnessMultiplier").floatValue = 1.3f;
            lightSettings.FindProperty("riderFillPosition").vector3Value = new Vector3(0f, 1.6f, -0.9f);
            lightSettings.ApplyModifiedPropertiesWithoutUndo();
            var lamp = (Light)lightSettings.FindProperty("surroundingLight").objectReferenceValue;
            // Keep the prefab hierarchy intact in Edit Mode; LightBattery attaches it on enable.
            lamp.transform.position = mount.position + movement.transform.up * 0.65f;
            lamp.transform.rotation = mount.rotation * Quaternion.Euler(18f, 0f, 0f);
            lamp.type = LightType.Spot;
            lamp.spotAngle = 140f;
            lamp.innerSpotAngle = 63f;
            lamp.range = 24f;
            lamp.color = Color.white;
            lamp.shadows = LightShadows.None;
            foreach (Object changed in new Object[] { origin, beam, mount, battery, lamp, lamp.transform })
                PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ConfigureRidingEffects(Scene scene)
        {
            var movement = Find<ScooterController>(scene).Single();
            var effects = movement.GetComponent<BokiPlayerEffects>();
            var presentation = new SerializedObject(movement.GetComponent<ScooterPresentation>());
            var wheel = (Transform)presentation.FindProperty("rearWheel").objectReferenceValue;
            Transform visuals = movement.transform.Find("Visuals");
            var dirt = ConfigureDirt(visuals, "Rear Wheel Dirt", wheel, false);
            var landing = ConfigureDirt(visuals, "Landing Dust", wheel, true);
            var settings = new SerializedObject(effects);
            settings.FindProperty("movement").objectReferenceValue = movement;
            settings.FindProperty("health").objectReferenceValue = movement.GetComponent<PlayerHealth>();
            settings.FindProperty("ridingDirt").objectReferenceValue = dirt;
            settings.FindProperty("landingDust").objectReferenceValue = landing;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var battery = movement.GetComponent<LightBattery>();
            var lightSettings = new SerializedObject(battery);
            lightSettings.FindProperty("strongGuiltWarning").boolValue = true;
            lightSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(effects);
            PrefabUtility.RecordPrefabInstancePropertyModifications(battery);
        }

        private static ParticleSystem ConfigureDirt(Transform visuals, string name, Transform wheel, bool landing)
        {
            Transform anchor = visuals.Find(name);
            if (anchor == null)
            {
                anchor = new GameObject(name).transform;
                anchor.SetParent(visuals, false);
            }
            anchor.position = wheel.position + Vector3.down * 0.06f;
            anchor.rotation = Quaternion.LookRotation(landing ? Vector3.up : -visuals.forward + Vector3.up * 0.55f);
            Vector3 scale = visuals.lossyScale;
            anchor.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            var particles = anchor.GetComponent<ParticleSystem>();
            if (particles == null) particles = anchor.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = landing ? 24 : 64;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(landing ? 0.6f : 1.2f, landing ? 1.5f : 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, landing ? 0.16f : 0.09f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.28f, 0.16f, 0.07f, 0.85f),
                new Color(0.65f, 0.47f, 0.25f, 0.65f));
            main.gravityModifier = landing ? 0.15f : 0.7f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = landing ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Cone;
            shape.radius = landing ? 0.2f : 0.055f;
            shape.angle = 28f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Effects/Boki/FeedbackParticles.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static void StripArena(Scene scene)
        {
            var movement = Find<ScooterController>(scene).Single();
            movement.transform.SetParent(null, true);
            movement.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -8f), Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(movement.transform);
            var hud = Find<PlayerHud>(scene).Single();
            hud.BindGuilt(null);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == movement.gameObject || root.GetComponent<ThirdPersonCamera>() != null
                    || root.name == "Boki HUD" || root.name == "Scooter Light UI"
                    || root.name == "Directional Light" || root.name == "Floor"
                    || root.name == "North wall" || root.name == "South wall"
                    || root.name == "East wall" || root.name == "West wall"
                    || root.name == "Model test charges") continue;
                Object.DestroyImmediate(root);
            }
            var bridge = movement.GetComponent<BokiModelTestEffects>();
            var settings = new SerializedObject(bridge);
            var charges = Find<BatteryCharge>(scene);
            var array = settings.FindProperty("charges");
            array.arraySize = charges.Length;
            for (int i = 0; i < charges.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = charges[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            var controls = new GameObject("Solo Controls", typeof(RectTransform), typeof(Text));
            controls.transform.SetParent(hud.transform, false);
            var text = controls.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = new Color(0.85f, 0.83f, 0.75f);
            text.raycastTarget = false;
            text.text = "BOKI SOLO TEST\nWASD: ride   Space: jump   Shift: boost   Mouse: camera\nT: laser target   H: damage   B: drain battery   G: warning   X: XP glow\nGreen charges: refill battery   Esc: release mouse";
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(900f, 100f);
        }

        private static void Validate(Scene scene)
        {
            if (Find<ScooterController>(scene).Length != 1 || Find<PlayerHud>(scene).Length != 1
                || Find<ThirdPersonCamera>(scene).Length != 1 || Find<PlayerTestArena>(scene).Length != 0
                || Find<EnemyHealth>(scene).Length != 0)
                throw new InvalidOperationException("Solo scene must contain one player, HUD and camera, without enemy stations.");
            foreach (var component in Find<MonoBehaviour>(scene))
            {
                if (component == null) throw new InvalidOperationException("Missing script in solo scene.");
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default)
                        throw new InvalidOperationException("Broken reference: " + component.name + "." + property.propertyPath);
            }
        }

        private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void RequireClean()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
        }
    }
}
