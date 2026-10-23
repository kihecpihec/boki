using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Boki.Editor
{
    /// <summary>
    /// Builds Maribor_Map_Test from the Maribor Blender export (output/world-modeling-v1/maps/maribor) around the
    /// existing Boki test player. Collision comes from collision-guides.json (Blender Z-up meters, boxes rotated about Z).
    /// Gate meshes and dynamic gate boxes stay open: this scene has no RouteGate logic.
    /// </summary>
    public static class MariborMapTestBuilder
    {
        private const string Source = "Assets/Scenes/Player_Model_Test.unity";
        private const string Target = "Assets/Scenes/Maribor_Map_Test.unity";
        private const string Art = "Assets/Art/Maribor/";
        private const string Materials = Art + "Materials/";
        private static readonly string[] Maps = { "MB_Shared_Arrival_FERI", "MB_Route_A", "MB_Route_B", "MB_Route_C", "MB_Scenery" };
        private static readonly Color FogColor = new Color(0.20f, 0.07f, 0.06f);

        [System.Serializable] private class Guide { public string name; public float[] center; public float[] size; public float rotation_z; public bool dynamic; }
        [System.Serializable] private class GuideList { public Guide[] items; }
        [System.Serializable] private class Marker { public string name; public float[] position; public string kind; }
        [System.Serializable] private class MarkerList { public Marker[] items; }

        [MenuItem("Boki/Maribor/Build Map Test Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("MARIBOR_FAIL exit Play Mode first"); return; }
            ConfigureImports();
            var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            GameObject player = null;
            Light sun = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.GetComponentInChildren<Boki.Player.ScooterController>(true) != null) player = r;
                else if (r.GetComponent<Light>() != null) sun = r.GetComponent<Light>();
                else if (r.GetComponent<Camera>() == null && r.GetComponentInChildren<Canvas>(true) == null
                         && r.GetComponentInChildren<Camera>(true) == null) Object.DestroyImmediate(r);
            }
            if (player == null) { Debug.LogError("MARIBOR_FAIL no player"); return; }

            // The importer maps Blender (x, y, z) to (-x, z, -y); a 180 degree yaw restores Blender Y as +Z.
            var map = new GameObject("Maribor_Map");
            map.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var file in Maps)
            {
                var go = Instantiate(file + ".fbx", map.transform);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (file == "MB_Scenery") r.shadowCastingMode = ShadowCastingMode.Off;
                    // This test scene has no RouteGate logic, so show every gate and rear seal open.
                    if (System.Text.RegularExpressions.Regex.IsMatch(r.name, "^(EntryGate|RearSeal|Seal)")) r.gameObject.SetActive(false);
                    // Curved streets, lanes and the ramp are not covered by the guide boxes, so walk on their render surface.
                    else if (System.Text.RegularExpressions.Regex.IsMatch(r.name, "^(Floor_A_Street|Floor_A_Sidewalk|Floor_B_Lanes|CarRamp|Jetty|FERI_Step)(\\.\\d+)?$"))
                        r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                }
            }
            foreach (var t in map.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;

            int boxes = BuildColliders(map.transform);
            int lights = AddLamps(map.transform);

            Vector3 pos = new Vector3(0f, 0.1f, 0f);
            Quaternion facing = Quaternion.identity;
            var markers = ReadMarkers();
            foreach (var m in markers) if (m.kind == "PlayerStart") pos = Unity(m.position) + Vector3.up * 0.1f;
            var cc = player.GetComponentInChildren<CharacterController>(true);
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(pos, Quaternion.identity);
            if (cc != null) cc.enabled = true;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.nearClipPlane = 0.2f; cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = FogColor;
                cam.transform.SetPositionAndRotation(pos + new Vector3(0f, 3f, -6f), Quaternion.Euler(15f, 0f, 0f));
            }
            ConfigureLighting(sun);
            Physics.SyncTransforms();
            EditorSceneManager.SaveScene(scene, Target);
            bool grounded = Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 5f);
            Debug.Log($"MARIBOR_GROUND {grounded} {(grounded ? hit.collider.name : "")}");
            Debug.Log($"MARIBOR_DONE colliders={boxes} lamps={lights} start={pos}");
        }

        [MenuItem("Boki/Maribor/Play Map Test Scene")]
        public static void Play()
        {
            EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        public static void OpenOnly() => EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);

        // Blender (x, y, z up) -> Unity (x, z up, y forward).
        private static Vector3 Unity(float[] b) => new Vector3(b[0], b[2], b[1]);

        private static GameObject Instantiate(string file, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Export/" + file);
            if (asset == null) throw new FileNotFoundException("Missing " + Art + "Export/" + file);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            return go;
        }

        // ------------------------------------------------------------------ imports and materials

        private static void ConfigureImports()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Materials);
            foreach (var png in Directory.GetFiles(Art + "Textures", "*.png"))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(png.Replace('\\', '/'));
                ti.filterMode = FilterMode.Point; ti.mipmapEnabled = true; ti.anisoLevel = 0;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            var fbxs = Directory.GetFiles(Art + "Export", "*.fbx");
            var materials = new Dictionary<string, Material>();
            foreach (var path in fbxs)
            {
                string p = path.Replace('\\', '/');
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.SaveAndReimport();
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (obj is Material src && !materials.ContainsKey(src.name))
                        materials[src.name] = BuildMaterial(src.name, src);
            }
            foreach (var path in fbxs)
            {
                string p = path.Replace('\\', '/');
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                foreach (var m in materials)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.Key), m.Value);
                mi.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("MARIBOR_MATERIALS " + string.Join(", ", materials.Keys));
        }

        private static Material BuildMaterial(string name, Material source)
        {
            string key = System.Text.RegularExpressions.Regex.Replace(name, @"\.\d+$", "");
            string path = Materials + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            Texture2D tex = null;
            foreach (var candidate in new[] { key, key.Replace("MB_", ""), key.Replace("PSX_", "") })
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/" + candidate + ".png");
                if (tex != null) break;
            }
            if (tex == null && source != null && source.HasProperty("_MainTex")) tex = source.GetTexture("_MainTex") as Texture2D;
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tex != null || source == null ? Color.white : source.color);
            mat.SetFloat("_Smoothness", 0f); mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.enableInstancing = true;
            if (key.Contains("Window") || key.Contains("Glow") || key.Contains("Light") || key.Contains("Fluorescent")
                || key.Contains("Charge") || key == "MB_Lamp")
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                mat.SetColor("_EmissionColor", (tex != null ? new Color(1f, 0.6f, 0.3f) : mat.GetColor("_BaseColor")) * 1.6f);
                if (tex != null) mat.SetTexture("_EmissionMap", tex);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureLighting(Light sun)
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.22f, 0.20f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.14f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.05f, 0.05f);
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(RenderSettings.ambientEquatorColor);
            probe.AddDirectionalLight(Vector3.up, RenderSettings.ambientSkyColor - RenderSettings.ambientEquatorColor, 0.6f);
            RenderSettings.ambientProbe = probe;
            if (sun == null) return;
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.45f, 0.3f);
            sun.intensity = 0.9f;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(24f, -40f, 0f);
        }

        // ------------------------------------------------------------------ authored collision and lamps

        private static Marker[] ReadMarkers()
        {
            string json = File.ReadAllText(Art + "markers.json");
            return JsonUtility.FromJson<MarkerList>("{\"items\":" + json + "}").items;
        }

        // Collision boxes are fitted to the MB_Collision_Guides meshes so they match the imported map exactly.
        private static int BuildColliders(Transform parent)
        {
            string json = File.ReadAllText(Art + "collision-guides.json");
            var skip = new HashSet<string>();
            foreach (var g in JsonUtility.FromJson<GuideList>("{\"items\":" + json + "}").items)
                if (g.dynamic) skip.Add(g.name); // entry gates and rear seals stay open in this scene
            var guides = Instantiate("MB_Collision_Guides.fbx", parent);
            guides.name = "Maribor_Collision";
            var fits = new List<(GameObject go, Vector3[] v, int[] tris)>();
            foreach (var mf in guides.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                if (mf.sharedMesh != null && !skip.Contains(go.name))
                {
                    var v = mf.sharedMesh.vertices;
                    var m = mf.transform.localToWorldMatrix;
                    for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
                    fits.Add((go, v, mf.sharedMesh.triangles));
                }
                else if (skip.Contains(go.name)) go.SetActive(false);
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null) Object.DestroyImmediate(renderer);
                Object.DestroyImmediate(mf);
            }
            foreach (var f in fits) FitBox(f.go, f.v, f.tris);
            return fits.Count;
        }

        // Guide boxes can be rotated about the vertical axis, so derive the box axes from the face normals.
        private static void FitBox(GameObject go, Vector3[] v, int[] tris)
        {
            var axes = new List<Vector3>(3);
            for (int t = 0; t + 2 < tris.Length && axes.Count < 4; t += 3)
            {
                Vector3 nrm = Vector3.Cross(v[tris[t + 1]] - v[tris[t]], v[tris[t + 2]] - v[tris[t]]);
                if (nrm.sqrMagnitude < 1e-10f) continue;
                nrm.Normalize();
                bool known = false;
                foreach (var a in axes) if (Mathf.Abs(Vector3.Dot(a, nrm)) > 0.98f) { known = true; break; }
                if (!known) axes.Add(nrm);
            }
            Quaternion rotation = Quaternion.identity;
            if (axes.Count >= 3)
            {
                axes.Sort((a, b) => Mathf.Abs(b.y).CompareTo(Mathf.Abs(a.y)));
                Vector3 up = axes[0].y < 0f ? -axes[0] : axes[0];
                Vector3 forward = Vector3.ProjectOnPlane(axes[1], up);
                if (forward.sqrMagnitude > 1e-6f) rotation = Quaternion.LookRotation(forward.normalized, up);
            }
            var inverse = Quaternion.Inverse(rotation);
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var p in v) { var l = inverse * p; min = Vector3.Min(min, l); max = Vector3.Max(max, l); }
            go.transform.SetPositionAndRotation(rotation * ((min + max) * 0.5f), rotation);
            go.transform.localScale = Vector3.one;
            go.AddComponent<BoxCollider>().size = max - min;
        }

        // The export carries no lights; place a small warm point light at every living streetlamp head.
        private static int AddLamps(Transform map)
        {
            int n = 0;
            foreach (var t in map.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("ENV_C_Streetlamp") || t.name.Contains("Dead") || t.childCount > 0 && t.GetComponentInChildren<Light>() != null) continue;
                var go = new GameObject("LampLight");
                go.transform.SetParent(t, false);
                go.transform.localPosition = new Vector3(0f, 4f, 0f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.shadows = LightShadows.None;
                l.color = new Color(1f, 0.62f, 0.3f); l.range = 10f; l.intensity = 2f;
                n++;
            }
            return n;
        }
    }
}
