using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Boki.Editor
{
    /// <summary>
    /// Builds FERI_Map_Test from Assets/Art/FERI around the existing Boki test player. Collision boxes come from the
    /// axis-aligned COL_ guide meshes; Route B's floor also gets a MeshCollider because the guides leave its ramps
    /// uncovered (README step 2). Seals and their guides stay open: this scene has no RouteGate logic. Never edits
    /// Level3_FERI or any other existing scene.
    /// </summary>
    public static class FeriMapTestBuilder
    {
        public const string Target = "Assets/Scenes/FERI_Map_Test.unity";
        private const string Source = "Assets/Scenes/Player_Model_Test.unity";
        private const string Art = "Assets/Art/FERI/";
        private static readonly string[] Chunks =
        {
            "FERI_Shared_Arrival", "FERI_Route_A", "FERI_Route_B", "FERI_Route_C",
            "FERI_Shared_Landing", "FERI_Arena_Classroom", "FERI_Seals",
        };
        private static readonly Color FogColor = new Color(0.03f, 0.07f, 0.05f);

        [System.Serializable] private class Marker { public string name; public string type; public float[] position; }
        [System.Serializable] private class MarkerList { public Marker[] markers; }

        [MenuItem("Boki/FERI/Build Map Test Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("FERI_FAIL exit Play Mode first"); return; }
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("FERI_FAIL save or discard the open scene first"); return; }

            var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            GameObject player = null;
            Light sun = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<Boki.Player.ScooterController>(true) != null) player = root;
                else if (root.GetComponent<Light>() != null) sun = root.GetComponent<Light>();
                else if (root.GetComponent<Camera>() == null && root.GetComponentInChildren<Canvas>(true) == null
                         && root.GetComponentInChildren<Camera>(true) == null) Object.DestroyImmediate(root);
            }
            if (player == null) { Debug.LogError("FERI_FAIL no player"); return; }

            // The importer maps Blender (x, y, z) to (-x, z, -y); a 180 degree yaw restores Blender Y as +Z.
            var map = new GameObject("FERI_Map");
            map.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (string chunk in Chunks)
            {
                var instance = Instantiate(chunk, map.transform);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    // No RouteGate logic here, so every gate and rear seal is shown open.
                    if (Regex.IsMatch(renderer.name, "^PL_(Seal|ArenaEntrySeal)_")) renderer.gameObject.SetActive(false);
                    // Route B's ramps have no guide box; only its floor mesh collides, never a whole decorative chunk.
                    else if (renderer.name == "PL_Route_B_Floor")
                        renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                }
            }
            // Roofs are presentation geometry; keep them off so the camera sees in, enable to test camera clearance.
            Instantiate("FERI_Removable_Roofs", map.transform).SetActive(false);
            int boxes = BuildColliders(map.transform);
            foreach (var t in map.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;

            Vector3 start = StartPosition() + Vector3.up * 0.1f;
            var controller = player.GetComponentInChildren<CharacterController>(true);
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(start, Quaternion.identity);
            if (controller != null) controller.enabled = true;
            var camera = Camera.main;
            if (camera != null)
            {
                camera.nearClipPlane = 0.2f; camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = FogColor;
                camera.transform.SetPositionAndRotation(start + new Vector3(0f, 3f, -6f), Quaternion.Euler(15f, 0f, 0f));
            }
            ConfigureLighting(sun);
            Physics.SyncTransforms();
            EditorSceneManager.SaveScene(scene, Target);
            bool grounded = Physics.Raycast(start + Vector3.up * 2f, Vector3.down, out var hit, 5f);
            Debug.Log($"FERI_GROUND {grounded} {(grounded ? hit.collider.name : "")}");
            Debug.Log($"FERI_DONE colliders={boxes} start={start}");
        }

        [MenuItem("Boki/FERI/Play Map Test Scene")]
        public static void Play()
        {
            EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        // Blender (x, y, z up) -> Unity (x, z up, y forward) once the map root is yawed 180 degrees.
        public static Vector3 Unity(float[] blender) => new Vector3(blender[0], blender[2], blender[1]);

        private static GameObject Instantiate(string file, Transform parent)
        {
            string path = Art + "Export/" + file + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new FileNotFoundException("Missing " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static Vector3 StartPosition()
        {
            var list = JsonUtility.FromJson<MarkerList>(File.ReadAllText(Art + "markers.json")).markers;
            foreach (var marker in list)
                if (marker.name == "Start_Player") return Unity(marker.position);
            throw new System.InvalidOperationException("markers.json has no Start_Player");
        }

        // Guides are axis-aligned boxes, so the mesh bounds are the collider.
        private static int BuildColliders(Transform parent)
        {
            var guides = Instantiate("FERI_CollisionGuides", parent);
            guides.name = "FERI_Collision";
            int count = 0;
            foreach (var filter in guides.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = filter.gameObject;
                var bounds = filter.sharedMesh.bounds;
                Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(filter);
                if (go.name.Contains("Seal")) { go.SetActive(false); continue; } // gates and seals stay open in this scene
                var box = go.AddComponent<BoxCollider>();
                box.center = bounds.center; box.size = bounds.size;
                count++;
            }
            return count;
        }

        // FERI keeps its own green/red palette, not the city's orange-red; one dim main light and no local lights.
        private static void ConfigureLighting(Light sun)
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.015f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.42f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.26f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.08f, 0.06f);
            if (sun == null) return;
            sun.type = LightType.Directional;
            sun.color = new Color(0.65f, 0.85f, 0.75f);
            sun.intensity = 0.8f;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
