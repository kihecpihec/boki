using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    public static class BokiEffectsPreviewSetup
    {
        private const string PreviewPath = "Assets/Scenes/BokiEffectsPreview.unity";
        private const string MaterialFolder = "Assets/Effects/Boki";

        [MenuItem("Boki/Effects/Create Isolated Preview")]
        public static void CreatePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Never overwrite an existing scene, even our own preview.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath) != null)
            {
                Debug.Log("Boki preview already exists. Open " + PreviewPath + " to preview effects.");
                return;
            }

            // Batch startup has an empty untitled scene. Open the template read-only
            // before additive creation; never discard a user's unsaved scene.
            if (Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(index).path))
                {
                    Debug.LogWarning("Save or close untitled scenes before creating the Boki effects preview. Existing work has been left untouched.");
                    return;
                }
            }

            EnsureFolder("Assets", "Effects");
            EnsureFolder("Assets/Effects", "Boki");
            Material body = GetMaterial("Body", "Universal Render Pipeline/Unlit", new Color(0.15f, 0.65f, 0.7f));
            Material floor = GetMaterial("Floor", "Universal Render Pipeline/Lit", new Color(0.22f, 0.25f, 0.3f));
            Material trailMaterial = GetMaterial("BoostTrail", "Boki/Boost Trail", Color.white);

            Scene previousScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                GameObject player = new GameObject("Boki Effects Preview (placeholder)");
                GameObject deck = CreateShape("Scooter", PrimitiveType.Cube, player.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.2f, 1.5f), body);
                GameObject rider = CreateShape("Boki", PrimitiveType.Capsule, player.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.7f, 0.5f), body);

                TrailRenderer[] trails = new TrailRenderer[2];
                for (int index = 0; index < trails.Length; index++)
                {
                    GameObject anchor = new GameObject("Boost Trail " + (index + 1));
                    anchor.transform.SetParent(player.transform, false);
                    anchor.transform.localPosition = new Vector3(index == 0 ? -0.25f : 0.25f, 0.3f, -0.7f);
                    TrailRenderer trail = anchor.AddComponent<TrailRenderer>();
                    trail.sharedMaterial = trailMaterial;
                    trail.time = 0.25f;
                    trail.minVertexDistance = 0.15f;
                    trail.startWidth = 0.12f;
                    trail.endWidth = 0f;
                    trail.startColor = index == 0 ? Color.cyan : new Color(1f, 0.1f, 0.65f);
                    trail.endColor = new Color(trail.startColor.r, trail.startColor.g, trail.startColor.b, 0f);
                    trail.emitting = false;
                    trail.shadowCastingMode = ShadowCastingMode.Off;
                    trail.receiveShadows = false;
                    trail.numCapVertices = 0;
                    trail.numCornerVertices = 0;
                    trails[index] = trail;
                }

                BokiPlayerEffects effects = player.AddComponent<BokiPlayerEffects>();
                var serialized = new SerializedObject(effects);
                AssignArray(serialized.FindProperty("boostTrails"), trails);
                AssignArray(serialized.FindProperty("damageRenderers"), new[] { deck.GetComponent<Renderer>(), rider.GetComponent<Renderer>() });
                serialized.ApplyModifiedPropertiesWithoutUndo();
                player.AddComponent<BokiEffectsPreview>();

                CreateShape("Preview Floor", PrimitiveType.Cube, null, new Vector3(0f, -0.2f, 0f), new Vector3(16f, 0.2f, 16f), floor);
                GameObject cameraObject = new GameObject("Preview Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                camera.transform.position = new Vector3(10f, 11f, -12f);
                camera.transform.LookAt(new Vector3(0f, 0.5f, 0f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.025f, 0.04f);
                camera.farClipPlane = 60f;

                EditorSceneManager.SaveScene(scene, PreviewPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created " + PreviewPath + ". Open it alone and press Play. Automatic boost every 4 s, damage every 3 s; hold Left Shift for trails, H for damage. No gameplay scenes or build settings were changed.");
            }
            finally
            {
                if (previousScene.IsValid())
                    SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static Material GetMaterial(string name, string shaderName, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new System.InvalidOperationException("Missing shader: " + shaderName);
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreateShape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            return shape;
        }

        private static void AssignArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
    }
}
