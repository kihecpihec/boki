using System;
using System.Collections.Generic;
using System.IO;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    /// <summary>Shared importer for the authored v3 environment contracts and their owned review scenes.</summary>
    internal sealed class MapProductionTools
    {
        private readonly string mapName;
        private readonly string folder;
        private readonly Color fogColor;
        internal string Art => "Assets/Art/" + mapName + "_v3/";
        internal string PreviewScene => "Assets/Scenes/" + mapName + "_Production_Preview.unity";
        internal string PrefabPath => Art + mapName + "_Production.prefab";
        internal string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/world-modeling-v3/maps/" + folder));
        internal bool IsCity => mapName == "Maribor";
        private const string PlayerScene = "Assets/Scenes/Player_Model_Test.unity";

        internal MapProductionTools(string name, string directory, Color fog)
        {
            mapName = name;
            folder = directory;
            fogColor = fog;
        }

        [Serializable] internal sealed class Point
        {
            public float x, y, z;
            public Vector3 Value => new Vector3(x, y, z);
        }

        [Serializable] internal sealed class ExportData { public string file, owner; }
        [Serializable] internal sealed class MaterialData { public string name, texture; public float[] emission; public bool clamp; }
        [Serializable] internal sealed class Shape
        {
            public string name, owner, kind, role;
            public float[] center, size, rotation;
            public float radius, height;
            public bool trigger;
        }
        [Serializable] internal sealed class Marker
        {
            public string name, owner, kind;
            public float[] position, size, forward;
        }
        [Serializable] internal sealed class Lane { public string name, owner; public float width; public float[] offsets; public Point[] points; }
        [Serializable] internal sealed class Pad { public string name; public float cx, cz, hx, hz, radius, height; }
        [Serializable] internal sealed class ReviewCamera { public string name; public float[] eye, target; }
        [Serializable] internal sealed class Expected { public int render_triangles, render_objects; }
        [Serializable] internal sealed class Manifest
        {
            public int version;
            public string name;
            public ExportData[] exports;
            public MaterialData[] materials;
            public string[] terrain;
            public Shape[] colliders;
            public Marker[] markers;
            public Lane[] lanes;
            public Pad[] pads;
            public Point[] floorSamples;
            public ReviewCamera[] reviewCameras;
            public Expected expected;
        }

        public void ImportAndBuild()
        {
            RequireSafeEditor();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                AssetDatabase.Refresh();
                var data = LoadManifest();
                ConfigureImports(data);
                BuildPrefab(data);
                BuildPreview(data);
                Debug.Log(mapName.ToUpperInvariant() + "_PRODUCTION_IMPORTED " + PrefabPath + " and " + PreviewScene);
            }
            finally { RestoreSetup(setup); }
        }

        public void OpenPreview()
        {
            RequireSafeEditor();
            EditorSceneManager.OpenScene(PreviewScene, OpenSceneMode.Single);
        }

        public void ImportAndValidate()
        {
            ImportAndBuild();
            MapProductionChecks.Run(this);
        }

        internal static void RequireSafeEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before changing an environment preview.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open scenes before running the environment tools.");
        }

        internal static void RestoreSetup(SceneSetup[] setup)
        {
            foreach (var scene in setup)
                if (scene.isLoaded)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                    return;
                }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        internal Manifest LoadManifest()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Art + mapName + "_Manifest.json");
            if (asset == null) throw new FileNotFoundException("Run the v3 Blender builder first: missing " + mapName + "_Manifest.json.");
            var data = JsonUtility.FromJson<Manifest>(asset.text);
            if (data == null || data.version != 3 || data.colliders == null || data.terrain == null)
                throw new InvalidDataException("Unsupported or incomplete environment art contract.");
            return data;
        }

        internal static Vector3 Vector(float[] value)
        {
            if (value == null || value.Length != 3) throw new InvalidDataException("Expected a three-component vector.");
            return new Vector3(value[0], value[1], value[2]);
        }

        internal static Quaternion Rotation(float[] value)
        {
            if (value == null || value.Length != 4) throw new InvalidDataException("Expected a quaternion.");
            return new Quaternion(value[0], value[1], value[2], value[3]).normalized;
        }

        private void ConfigureImports(Manifest data)
        {
            Directory.CreateDirectory(Art + "Materials");
            AssetDatabase.Refresh();
            var materials = new Dictionary<string, Material>();
            foreach (var item in data.materials)
            {
                string path = Art + "Materials/" + item.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetFloat("_Smoothness", 0f);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.SetFloat("_EnvironmentReflections", 0f);
                mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                mat.SetFloat("_Cull", (float)CullMode.Back);
                mat.enableInstancing = true;
                if (!string.IsNullOrEmpty(item.texture))
                {
                    string texturePath = Art + item.texture;
                    var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (importer == null) throw new FileNotFoundException(texturePath);
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = true;
                    importer.anisoLevel = 0;
                    importer.wrapMode = item.clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                    mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                    mat.SetColor("_BaseColor", Color.white);
                    mat.DisableKeyword("_EMISSION");
                    if (item.emission != null && item.emission.Length == 3)
                    {
                        mat.SetTexture("_EmissionMap", mat.GetTexture("_BaseMap"));
                        mat.SetColor("_EmissionColor", new Color(item.emission[0], item.emission[1], item.emission[2]));
                        mat.EnableKeyword("_EMISSION");
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                }
                else if (item.emission != null && item.emission.Length == 3)
                {
                    var color = new Color(item.emission[0], item.emission[1], item.emission[2]);
                    mat.SetTexture("_BaseMap", null);
                    mat.SetColor("_BaseColor", color);
                    mat.SetColor("_EmissionColor", color * 2.2f);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                EditorUtility.SetDirty(mat);
                materials.Add(item.name, mat);
            }
            foreach (var item in data.exports)
            {
                string path = Art + item.file;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new FileNotFoundException(path);
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.addCollider = false;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.isReadable = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var entry in materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.Key), entry.Value);
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void MarkStatic(GameObject obj)
        {
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        private void BuildPrefab(Manifest data)
        {
            var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(temporary);
            try
            {
                var root = new GameObject(data.name);
                var physics = Child(root.transform, "Collision");
                var terrainPhysics = Child(physics, "Terrain");
                var obstaclePhysics = Child(physics, "AuthoredShapes");
                var markerRoot = Child(root.transform, "Markers");
                var seals = Child(root.transform, "Seals");
                var owners = new Dictionary<string, Transform>();
                foreach (string owner in new[] { "Shared", "A", "B", "C", "Backdrop" })
                    owners[owner] = Child(Child(root.transform, owner == "Shared" || owner == "Backdrop" ? owner : "Route_" + owner), "InteriorRender");
                Child(owners["C"].parent, "Removable_Roofs");
                var terrainNames = new HashSet<string>(data.terrain);
                int terrainCount = 0;
                foreach (var item in data.exports)
                {
                    if (item.owner == "CollisionGuides" || item.owner == "Markers") continue;
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + item.file);
                    if (model == null) throw new FileNotFoundException(item.file);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, temporary);
                    instance.transform.SetParent(item.owner == "Roofs" ? owners["C"].parent.Find("Removable_Roofs") : owners[item.owner], false);
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                    foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null) throw new InvalidDataException("Empty mesh " + mf.name);
                        if (mf.name.StartsWith("PL_Seal_", StringComparison.Ordinal))
                        {
                            mf.transform.SetParent(seals, true);
                            GameObjectUtility.SetStaticEditorFlags(mf.gameObject, 0);
                        }
                        else if (item.owner == "Roofs")
                        {
                            mf.transform.SetParent(owners["C"].parent.Find("Removable_Roofs"), true);
                            MarkStatic(mf.gameObject);
                        }
                        else MarkStatic(mf.gameObject);
                        if (!terrainNames.Contains(mf.name)) continue;
                        var collision = Child(terrainPhysics, "COL_Mesh_" + mf.name);
                        collision.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                        collision.localScale = mf.transform.lossyScale;
                        collision.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                        MarkStatic(collision.gameObject);
                        terrainCount++;
                    }
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                        if (renderer.name.Contains("Forest") || item.owner == "Backdrop") renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                if (terrainCount != data.terrain.Length)
                    throw new InvalidDataException($"Expected {data.terrain.Length} terrain chunks; imported {terrainCount}.");
                foreach (var shape in data.colliders)
                {
                    var obj = Child(obstaclePhysics, shape.name);
                    obj.SetPositionAndRotation(Vector(shape.center), Rotation(shape.rotation));
                    Collider collider;
                    if (shape.kind == "Box")
                    {
                        var box = obj.gameObject.AddComponent<BoxCollider>();
                        box.size = Vector(shape.size);
                        collider = box;
                    }
                    else if (shape.kind == "Capsule")
                    {
                        var capsule = obj.gameObject.AddComponent<CapsuleCollider>();
                        capsule.direction = 1;
                        capsule.height = shape.height;
                        capsule.radius = shape.radius;
                        collider = capsule;
                    }
                    else throw new InvalidDataException("Unknown collision shape " + shape.kind);
                    collider.isTrigger = shape.trigger;
                    if (!shape.role.StartsWith("seal_", StringComparison.Ordinal)) MarkStatic(obj.gameObject);
                }
                foreach (var marker in data.markers)
                {
                    var obj = Child(markerRoot, marker.name);
                    obj.position = Vector(marker.position);
                    obj.rotation = Quaternion.LookRotation(Vector(marker.forward), Vector3.up);
                    if (marker.kind != "route_selection" && marker.kind != "combat_occupancy" && marker.kind != "level_exit") continue;
                    var trigger = obj.gameObject.AddComponent<BoxCollider>();
                    trigger.size = Vector(marker.size);
                    trigger.isTrigger = true;
                    if (marker.kind == "route_selection") trigger.enabled = false;
                }
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new IOException("Could not save the environment production prefab.");
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        private void BuildPreview(Manifest data)
        {
            var scene = EditorSceneManager.OpenScene(PlayerScene, OpenSceneMode.Single);
            GameObject player = null;
            Light sun = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<ScooterController>(true) != null) player = root;
                else if (root.TryGetComponent<Light>(out var found)) sun = found;
                else if (root.GetComponentInChildren<Camera>(true) == null &&
                         root.GetComponentInChildren<Canvas>(true) == null &&
                         root.GetComponent<UnityEngine.EventSystems.EventSystem>() == null)
                    Object.DestroyImmediate(root);
            }
            if (player == null) throw new InvalidOperationException("The player model scene has no scooter controller.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var map = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            OpenSealsForPreview(map, data);
            foreach (var marker in data.markers)
            {
                if (marker.name != "MK_Start_Player") continue;
                var cc = player.GetComponentInChildren<CharacterController>();
                cc.enabled = false;
                player.transform.SetPositionAndRotation(Vector(marker.position), Quaternion.identity);
                cc.enabled = true;
            }
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = IsCity ? 175f : 195f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = IsCity ? new Color(0.58f, 0.49f, 0.46f) : new Color(0.52f, 0.59f, 0.63f);
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = probe;
            if (sun == null) sun = new GameObject("Morning Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.color = IsCity ? new Color(1f, 0.64f, 0.43f) : new Color(0.79f, 0.87f, 1f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(32f, -38f, 0f);
            foreach (var marker in data.markers)
            {
                if (marker.kind != "light_violet" && marker.name != "MK_Light_Alcove" && !(IsCity && marker.kind == "light_warm")) continue;
                var obj = map.transform.Find("Markers/" + marker.name);
                var light = obj.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = marker.kind == "light_violet" ? new Color(0.6f, 0.2f, 1f) : new Color(1f, 0.43f, 0.13f);
                light.range = marker.size[0];
                light.intensity = IsCity ? 3.0f : 5.0f;
                light.shadows = LightShadows.None;
            }
            var camera = Camera.main;
            if (camera != null)
            {
                camera.backgroundColor = fogColor;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.farClipPlane = 520f;
                camera.nearClipPlane = 0.15f;
                camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0, 2.6f, -5f), Quaternion.Euler(12, 0, 0));
            }
            Physics.SyncTransforms();
            if (!EditorSceneManager.SaveScene(scene, PreviewScene)) throw new IOException("Could not save the environment preview.");
        }

        internal static void OpenSealsForPreview(GameObject map, Manifest data)
        {
            foreach (var shape in data.colliders)
            {
                if (!shape.role.StartsWith("seal_", StringComparison.Ordinal)) continue;
                var collider = map.transform.Find("Collision/AuthoredShapes/" + shape.name).GetComponent<Collider>();
                collider.enabled = false;
            }
            var seals = map.transform.Find("Seals");
            foreach (Transform seal in seals) seal.gameObject.SetActive(false);
            foreach (var trigger in map.transform.Find("Markers").GetComponentsInChildren<BoxCollider>())
                if (trigger.name.StartsWith("MK_Trigger_", StringComparison.Ordinal)) trigger.enabled = true;
        }

        public void ExportPackage()
        {
            RequireSafeEditor();
            string output = Path.Combine(Output, mapName + "_Production.unitypackage");
            // Every environment dependency is already in this directory. Do not bundle URP's package-cache sources.
            AssetDatabase.ExportPackage(Art.TrimEnd('/'), output, ExportPackageOptions.Recurse);
            Debug.Log(mapName.ToUpperInvariant() + "_PRODUCTION_PACKAGE " + output);
        }

        public void CaptureAndExport()
        {
            Capture();
            ExportPackage();
        }

        public void BuildDelivery()
        {
            ImportAndValidate();
            CaptureAndExport();
        }

        public void Capture()
        {
            RequireSafeEditor();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(PreviewScene, OpenSceneMode.Single);
                string directory = Path.Combine(Output, "Previews");
                Directory.CreateDirectory(directory);
                var go = new GameObject(mapName + " Review Camera");
                var camera = go.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = fogColor;
                camera.nearClipPlane = 0.15f;
                camera.farClipPlane = 650f;
                camera.fieldOfView = 66f;
                var shots = LoadManifest().reviewCameras;
                var render = new RenderTexture(1280, 720, 24);
                camera.targetTexture = render;
                try
                {
                    camera.transform.SetPositionAndRotation(Vector(shots[0].eye), Quaternion.LookRotation(Vector(shots[0].target)-Vector(shots[0].eye)));
                    camera.Render();
                    foreach (var shot in shots)
                    {
                        RenderSettings.fogEndDistance = shot.name.EndsWith("overview", StringComparison.Ordinal) ? 650f : IsCity ? 175f : 195f;
                        camera.transform.SetPositionAndRotation(Vector(shot.eye), Quaternion.LookRotation(Vector(shot.target)-Vector(shot.eye)));
                        camera.Render();
                        var previous = RenderTexture.active;
                        RenderTexture.active = render;
                        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                        try
                        {
                            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                            File.WriteAllBytes(Path.Combine(directory, "unity-"+shot.name+".png"), image.EncodeToPNG());
                        }
                        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
                    }
                }
                finally { camera.targetTexture = null; render.Release(); Object.DestroyImmediate(render); Object.DestroyImmediate(go); }
                Debug.Log(mapName.ToUpperInvariant() + "_PRODUCTION_CAPTURE " + directory);
            }
            finally { RestoreSetup(setup); }
        }
    }
}
