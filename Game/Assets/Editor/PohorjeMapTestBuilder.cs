using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Boki.Editor
{
    /// <summary>
    /// Builds Pohorje_Map_Test from the Pohorje v2 export (output/world-modeling-v2/maps/pohorje/build_pohorje.py)
    /// around the existing Boki test player. Collision follows the export's naming convention only:
    /// TERRAIN_* mesh colliders, UBX_ boxes, UCP_ capsules, USP_ spheres, UCX_ convex meshes.
    /// </summary>
    public static class PohorjeMapTestBuilder
    {
        private const string Source = "Assets/Scenes/Player_Model_Test.unity";
        private const string Target = "Assets/Scenes/Pohorje_Map_Test.unity";
        private const string Art = "Assets/Art/Pohorje_v2/";
        private const string Materials = Art + "Materials/";

        // Material names written by build_pohorje.py (PALETTE / EMISSIVE).
        private static readonly string[] Textured = { "Path", "Dirt", "Snow", "Concrete", "Ground", "Cliff", "Bark",
            "Needles", "Wood", "Metal", "RedPaint", "Dark" };
        private static readonly Dictionary<string, Color> Emissive = new Dictionary<string, Color>
        {
            ["NeonPurple"] = new Color(0.62f, 0.20f, 1.0f), ["LampWarm"] = new Color(1.0f, 0.62f, 0.28f),
            ["ChargeGreen"] = new Color(0.30f, 1.0f, 0.55f), ["SignA"] = new Color(0.35f, 0.85f, 0.45f),
            ["SignB"] = new Color(0.35f, 0.60f, 1.0f), ["SignC"] = new Color(0.70f, 0.40f, 1.0f),
            ["WarnRed"] = new Color(1.0f, 0.15f, 0.1f),
        };
        private static readonly Color FogColor = new Color(0.055f, 0.07f, 0.11f);

        [MenuItem("Boki/Pohorje/Build Map Test Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("POHORJE_FAIL exit Play Mode first"); return; }
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
            if (player == null) { Debug.LogError("POHORJE_FAIL no player"); return; }

            var map = new GameObject("Pohorje_Map");
            var terrain = Instantiate("Pohorje_Terrain.fbx", map.transform);
            foreach (var mf in terrain.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            var props = Instantiate("Pohorje_Props.fbx", map.transform);
            foreach (var r in props.GetComponentsInChildren<Renderer>())
            {
                // Backdrop and forest never block anything; keep their shadows off for the 5600G.
                if (r.name.StartsWith("BACKDROP_") || r.name.StartsWith("FOREST_")) r.shadowCastingMode = ShadowCastingMode.Off;
                // This test scene has no RouteGate logic, so show every gate open.
                if (r.name.StartsWith("GATEVIS_")) r.gameObject.SetActive(false);
            }
            foreach (var t in map.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;

            int[] counts = BuildColliders(map.transform);
            var markers = Instantiate("Pohorje_Markers.fbx", map.transform);
            int lights = AddLights(markers.transform);

            Transform start = markers.transform.Find("MK_PlayerStart");
            var cc = player.GetComponentInChildren<CharacterController>(true);
            Vector3 pos = start != null ? start.position + Vector3.up * 0.1f : new Vector3(0f, 40.2f, -3f);
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(pos, Quaternion.identity);
            if (cc != null) cc.enabled = true;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.nearClipPlane = 0.2f; cam.farClipPlane = 420f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = FogColor;
                cam.transform.SetPositionAndRotation(pos + new Vector3(0f, 3f, -6f), Quaternion.Euler(15f, 0f, 0f));
            }
            ConfigureLighting(sun);
            Physics.SyncTransforms();
            EditorSceneManager.SaveScene(scene, Target);
            Debug.Log($"POHORJE_DONE boxes={counts[0]} capsules={counts[1]} spheres={counts[2]} convex={counts[3]} gatesOpen={counts[4]} lights={lights}");
        }

        [MenuItem("Boki/Pohorje/Play Map Test Scene")]
        public static void Play()
        {
            EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static GameObject Instantiate(string file, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art + file);
            if (asset == null) throw new FileNotFoundException("Run build_pohorje.py first; missing " + Art + file);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            return go;
        }

        // ------------------------------------------------------------------ import settings and materials

        private static void ConfigureImports()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Materials);
            var materials = new Dictionary<string, Material>();
            foreach (var n in Textured) materials[n] = LitMaterial(n, Art + "Textures/Pohorje_" + n + ".png");
            foreach (var e in Emissive) materials[e.Key] = EmissiveMaterial(e.Key, e.Value);
            materials["Skyline"] = SkylineMaterial();

            foreach (var png in Directory.GetFiles(Art + "Textures", "*.png"))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(png.Replace('\\', '/'));
                bool sky = png.EndsWith("Skyline.png");
                ti.filterMode = FilterMode.Point; ti.mipmapEnabled = !sky; ti.anisoLevel = 0;
                ti.wrapMode = sky ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                ti.alphaIsTransparency = sky; ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            foreach (var fbx in new[] { "Pohorje_Terrain.fbx", "Pohorje_Props.fbx", "Pohorje_Collision.fbx", "Pohorje_Markers.fbx" })
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(Art + fbx);
                mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
                mi.importNormals = ModelImporterNormals.Import; mi.isReadable = fbx != "Pohorje_Markers.fbx";
                mi.meshCompression = ModelImporterMeshCompression.Off; mi.generateSecondaryUV = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var m in materials)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Pohorje_" + m.Key), m.Value);
                mi.SaveAndReimport();
            }
        }

        private static Material LoadOrCreate(string name, string shader)
        {
            string path = Materials + "Pohorje_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = Shader.Find(shader);
            return mat;
        }

        private static Material LitMaterial(string name, string texture)
        {
            AssetDatabase.ImportAsset(texture);
            var mat = LoadOrCreate(name, "Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0f); mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EmissiveMaterial(string name, Color color)
        {
            var mat = LoadOrCreate(name, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color * 0.6f);
            mat.SetFloat("_Smoothness", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.SetColor("_EmissionColor", color * 2.2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SkylineMaterial()
        {
            string tex = Art + "Textures/Pohorje_Skyline.png";
            AssetDatabase.ImportAsset(tex);
            var mat = LoadOrCreate("Skyline", "Universal Render Pipeline/Unlit");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.5f); mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureLighting(Light sun)
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 15f;
            RenderSettings.fogEndDistance = 220f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.40f, 0.46f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.27f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.08f, 0.10f);
            // Batch builds never bake the environment, so write the ambient probe directly (sky above, ground below).
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(RenderSettings.ambientEquatorColor);
            probe.AddDirectionalLight(Vector3.up, RenderSettings.ambientSkyColor - RenderSettings.ambientEquatorColor, 0.6f);
            RenderSettings.ambientProbe = probe;
            if (sun == null) return;
            sun.type = LightType.Directional;
            sun.color = new Color(0.62f, 0.70f, 0.95f);
            sun.intensity = 1.0f;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        }

        // ------------------------------------------------------------------ lights from markers

        private static int AddLights(Transform markers)
        {
            int n = 0;
            foreach (Transform t in markers.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("MK_Light_")) continue;
                var light = t.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                if (t.name.StartsWith("MK_Light_Lamp_")) { light.color = new Color(1f, 0.62f, 0.3f); light.range = 11f; light.intensity = 2.2f; }
                else if (t.name.StartsWith("MK_Light_Neon_")) { light.color = new Color(0.65f, 0.25f, 1f); light.range = 9f; light.intensity = 2.6f; }
                else { light.color = new Color(1f, 0.15f, 0.1f); light.range = 7f; light.intensity = 2.5f; }
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ authored collision

        private static int[] BuildColliders(Transform parent)
        {
            var guides = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Pohorje_Collision.fbx");
            var root = new GameObject("Pohorje_Collision");
            root.transform.SetParent(parent, false);
            var counts = new int[5];
            foreach (var mf in guides.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                string name = mf.name;
                var go = new GameObject(name) { isStatic = true };
                go.transform.SetParent(root.transform, false);
                var world = WorldVertices(mf);
                Collider c;
                if (name.StartsWith("UCP_")) { c = FitCapsule(go, world); counts[1]++; }
                else if (name.StartsWith("USP_")) { c = FitSphere(go, world); counts[2]++; }
                else if (name.StartsWith("UCX_"))
                {
                    var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.convex = true; c = mc; counts[3]++;
                }
                else { c = FitBox(go, world, mf.sharedMesh.triangles); counts[0]++; }
                if (name.StartsWith("UBX_GATE_")) { c.enabled = false; go.isStatic = false; counts[4]++; }
            }
            return counts;
        }

        private static Vector3[] WorldVertices(MeshFilter mf)
        {
            var v = mf.sharedMesh.vertices;
            var m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
            return v;
        }

        // Boxes can be rotated, so derive their axes from the face normals.
        private static Collider FitBox(GameObject go, Vector3[] v, int[] tris)
        {
            var axes = new List<Vector3>(3);
            for (int t = 0; t + 2 < tris.Length && axes.Count < 4; t += 3)
            {
                Vector3 n = Vector3.Cross(v[tris[t + 1]] - v[tris[t]], v[tris[t + 2]] - v[tris[t]]);
                if (n.sqrMagnitude < 1e-10f) continue;
                n.Normalize();
                bool known = false;
                foreach (var a in axes) if (Mathf.Abs(Vector3.Dot(a, n)) > 0.98f) { known = true; break; }
                if (!known) axes.Add(n);
            }
            Quaternion rotation = Quaternion.identity;
            if (axes.Count == 3)
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
            var box = go.AddComponent<BoxCollider>();
            box.size = max - min;
            return box;
        }

        // Capsule proxies are cylinders: the principal axis of their vertices is the capsule axis.
        private static Collider FitCapsule(GameObject go, Vector3[] v)
        {
            Vector3 mean = Vector3.zero;
            foreach (var p in v) mean += p;
            mean /= v.Length;
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p in v)
            {
                var d = p - mean;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            Vector3 axis = new Vector3(0.3f, 0.9f, 0.3f).normalized;
            for (int i = 0; i < 32; i++)
                axis = new Vector3(xx * axis.x + xy * axis.y + xz * axis.z, xy * axis.x + yy * axis.y + yz * axis.z,
                    xz * axis.x + yz * axis.y + zz * axis.z).normalized;
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity, radius = 0f;
            foreach (var p in v)
            {
                float s = Vector3.Dot(p - mean, axis);
                lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s);
                radius = Mathf.Max(radius, Vector3.ProjectOnPlane(p - mean, axis).magnitude);
            }
            go.transform.SetPositionAndRotation(mean + axis * ((lo + hi) * 0.5f), Quaternion.LookRotation(axis));
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = radius;
            capsule.height = Mathf.Max(hi - lo, radius * 2f);
            return capsule;
        }

        private static Collider FitSphere(GameObject go, Vector3[] v)
        {
            var b = new Bounds(v[0], Vector3.zero);
            foreach (var p in v) b.Encapsulate(p);
            go.transform.position = b.center;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = (b.extents.x + b.extents.y + b.extents.z) / 3f;
            return sphere;
        }
    }
}
