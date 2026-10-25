using System;
using System.Linq;
using Boki.Combat;
using Boki.Battery;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Editor
{
    public static class BokiModelTestSetup
    {
        public const string ScenePath = "Assets/Scenes/Player_Model_Test.unity";

        [MenuItem("Boki/Player Model/Consolidate Effects in Test Scene")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save dirty scenes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var movement = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ScooterController>(true)).Single();
                GameObject player = movement.gameObject;
                var visuals = player.transform.Find("Visuals");
                if (visuals == null) throw new InvalidOperationException("The rigged Boki visuals are missing.");
                var effects = GetOrAdd<BokiPlayerEffects>(player);
                var trails = new TrailRenderer[2];
                for (int i = 0; i < 2; i++)
                {
                    var anchor = Child(visuals, "Boost Trail " + (i + 1));
                    anchor.localPosition = new Vector3(i == 0 ? -0.25f : 0.25f, 0.3f, -0.7f);
                    var trail = GetOrAdd<TrailRenderer>(anchor.gameObject);
                    trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Effects/Boki/BoostTrail.mat");
                    trail.time = 0.25f;
                    trail.minVertexDistance = 0.15f;
                    trail.startWidth = 0.12f;
                    trail.endWidth = 0f;
                    trail.startColor = i == 0 ? Color.cyan : new Color(1f, 0.1f, 0.65f);
                    trail.endColor = new Color(trail.startColor.r, trail.startColor.g, trail.startColor.b, 0f);
                    trail.emitting = false;
                    trail.shadowCastingMode = ShadowCastingMode.Off;
                    trail.receiveShadows = false;
                    trail.numCapVertices = trail.numCornerVertices = 0;
                    trails[i] = trail;
                }
                var pickup = ConfigureBurst(Child(visuals, "Pickup Glow"), true);
                pickup.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                Set(effects, "pickupGlow", pickup);
                var so = new SerializedObject(effects);
                AssignArray(so.FindProperty("boostTrails"), trails);
                AssignArray(so.FindProperty("damageRenderers"), visuals.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray());
                so.ApplyModifiedPropertiesWithoutUndo();

                var origin = Child(visuals, "LaserOrigin");
                origin.localPosition = new Vector3(0f, 0.8f, 0.9f);
                var beamObject = Child(visuals, "Laser Beam");
                var beam = GetOrAdd<LineRenderer>(beamObject.gameObject);
                beam.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/M2/Laser.mat");
                beam.startWidth = beam.endWidth = 0.06f;
                beam.startColor = Color.cyan;
                beam.endColor = Color.magenta;
                beam.shadowCastingMode = ShadowCastingMode.Off;
                beam.receiveShadows = false;
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.enabled = false;
                var impact = ConfigureBurst(Child(player.transform, "Laser Impact"), false);
                var laser = GetOrAdd<AutoLaser>(player);
                Set(laser, "impactBurst", impact);
                var health = player.GetComponent<PlayerHealth>();
                Set(laser, "playerHealth", health); Set(laser, "beamOrigin", origin); Set(laser, "beam", beam);

                var bridge = GetOrAdd<BokiModelTestEffects>(player);
                Set(bridge, "movement", movement); Set(bridge, "health", health); Set(bridge, "effects", effects);
                Set(bridge, "battery", player.GetComponent<LightBattery>());
                Set(bridge, "presentation", player.GetComponent<ScooterPresentation>());
                var bridgeSettings = new SerializedObject(bridge);
                AssignArray(bridgeSettings.FindProperty("charges"), scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<BatteryCharge>(true)).ToArray());
                bridgeSettings.ApplyModifiedPropertiesWithoutUndo();
                Set(bridge, "targetMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Battery/ChargeAvailable.mat"));
                var hud = scene.GetRootGameObjects().Single(r => r.name == "MovementHUD");
                var instructions = Child(hud.transform, "Model Test Controls");
                var text = GetOrAdd<Text>(instructions.gameObject);
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 16;
                text.color = Color.white;
                text.raycastTarget = false;
                text.text = "BOKI MODEL TEST | WASD: ride | Space: jump | Shift: boost\nH: damage | T: laser target | B: drain 25 battery | G: warning preview (3s)\nX: XP glow preview (no XP awarded) | Ride over charges: refill + green glow | Esc: release mouse.";
                var rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(24f, -110f);
                rect.sizeDelta = new Vector2(1150f, 85f);
                foreach (var component in new Component[] { effects, laser, bridge })
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("BOKI_MODEL_EFFECTS_READY: rigged model, movement, trails, accepted-damage flash, laser, headlight, battery and HUD share Player_Model_Test.");
            }
            finally
            {
                if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static ParticleSystem ConfigureBurst(Transform anchor, bool pickup)
        {
            const string materialPath = "Assets/Effects/Boki/FeedbackParticles.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/EnemyFeedbackParticles.shader"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var particles = GetOrAdd<ParticleSystem>(anchor.gameObject);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = pickup ? 0.35f : 0.14f;
            main.startSpeed = pickup ? 0.7f : 1.5f;
            main.startSize = pickup ? 0.14f : 0.08f;
            main.startColor = new Color(0.4f, 1f, 1f);
            main.maxParticles = pickup ? 32 : 16;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = pickup ? 0.45f : 0.06f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
