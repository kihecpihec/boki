using System;
using System.Collections.Generic;
using System.Linq;
using Boki.Intro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Builds Boki_Intro.prefab: the intro-only Boki. It is the player model's neutral bind-pose rig with its own atlas
    // (painted eyes, brows and mouth blanked) and separate 3D face parts that IntroBoki animates. The player model,
    // its prefabs and its atlas are not touched.
    public static class IntroBokiBuilder
    {
        public const string Folder = "Assets/Art/Intro";
        public const string PrefabPath = Folder + "/Boki_Intro.prefab";
        private const string ModelPath = Folder + "/CHR_Boki_BindPose.fbx";
        private const string AtlasTexturePath = Folder + "/T_Boki_Intro_BaseColor.png";
        private const string PlayerAtlasPath = "Assets/Art/Player/Boki_Atlas.mat";
        private const string MaterialFolder = Folder + "/Materials";
        private const string MeshFolder = Folder + "/Meshes";

        // Face space origin, in character space: on the head axis at eye level (see IntroBoki).
        private static readonly Vector3 FaceOrigin = new Vector3(0f, 1.56f, 0f);

        private static readonly Color Skin = new Color32(0xB4, 0xA6, 0x96, 0xFF);

        public static GameObject Build()
        {
            EnsureFolders();
            ConfigureTextures();
            Material atlas = EnsureAtlasMaterial();
            Mesh sphere = EnsureMesh("LowPolySphere", BuildSphere(10, 6));
            Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            Material white = EnsureMaterial(MaterialFolder, "Face_Eye", new Color(0.95f, 0.94f, 0.88f), 0.55f, false);
            Material pupilMaterial = EnsureMaterial(MaterialFolder, "Face_Pupil", new Color(0.07f, 0.09f, 0.11f), 0.7f, false);
            Material skin = EnsureMaterial(MaterialFolder, "Face_Skin", Skin, 0.1f, false);
            Material brows = EnsureMaterial(MaterialFolder, "Face_Brow", new Color32(0x34, 0x28, 0x20, 0xFF), 0.1f, false);
            Material mouthMaterial = EnsureMaterial(MaterialFolder, "Face_Mouth", new Color(0.09f, 0.04f, 0.045f), 0.2f, false);
            Material sweat = EnsureMaterial(MaterialFolder, "Face_Sweat", new Color(0.65f, 0.85f, 1f), 0f, true);
            Material alert = EnsureMaterial(MaterialFolder, "Face_Alert", new Color32(0xEF, 0x62, 0x4A, 0xFF), 0f, true);

            var root = new GameObject("Boki Intro", typeof(IntroBoki));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ModelPath), root.transform);
            model.transform.localPosition = Vector3.zero;
            var animator = model.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = atlas;
                renderer.sharedMaterials = materials;
            }

            Transform head = Bone(model, "head");
            var face = new GameObject("Face").transform;
            face.SetPositionAndRotation(root.transform.TransformPoint(FaceOrigin), root.transform.rotation);
            face.SetParent(head, true);

            Transform eyeL = Part("Eye L", face, sphere, white);
            Transform eyeR = Part("Eye R", face, sphere, white);
            Transform pupilL = Part("Pupil L", face, sphere, pupilMaterial);
            Transform pupilR = Part("Pupil R", face, sphere, pupilMaterial);
            Transform stalkL = Stalk("Stalk L", face, cylinder, skin);
            Transform stalkR = Stalk("Stalk R", face, cylinder, skin);
            Transform lashL = Part("Lash L", face, cube, brows);
            Transform lashR = Part("Lash R", face, cube, brows);
            Transform browL = Part("Brow L", face, cube, brows);
            Transform browR = Part("Brow R", face, cube, brows);
            Transform mouth = Part("Mouth", face, cube, mouthMaterial);

            Transform cues = new GameObject("Cues").transform;
            cues.SetParent(root.transform, false);
            Transform alertMark = new GameObject("Alert Mark").transform;
            alertMark.SetParent(cues, false);
            Part("Alert Bar", alertMark, cube, alert).SetLocalPositionAndRotation(new Vector3(0f, 0.04f, 0f), Quaternion.identity);
            alertMark.Find("Alert Bar").localScale = new Vector3(0.045f, 0.12f, 0.02f);
            Part("Alert Dot", alertMark, cube, alert).SetLocalPositionAndRotation(new Vector3(0f, -0.06f, 0f), Quaternion.identity);
            alertMark.Find("Alert Dot").localScale = new Vector3(0.045f, 0.045f, 0.02f);
            var drops = new Transform[4];
            for (int i = 0; i < drops.Length; i++) drops[i] = Part("Sweat Drop " + i, cues, sphere, sweat);

            // Authored pose: lying face up, ready for the cutscene to take over.
            root.transform.SetLocalPositionAndRotation(IntroActing.LyingSpot, Quaternion.Euler(-90f, 0f, 0f));

            var so = new SerializedObject(root.GetComponent<IntroBoki>());
            Assign(so, "spine", Bone(model, "spine"));
            Assign(so, "chest", Bone(model, "chest"));
            Assign(so, "neck", Bone(model, "neck"));
            Assign(so, "head", head);
            Assign(so, "upperArmL", Bone(model, "upper_arm.L"));
            Assign(so, "lowerArmL", Bone(model, "lower_arm.L"));
            Assign(so, "handL", Bone(model, "hand.L"));
            Assign(so, "upperArmR", Bone(model, "upper_arm.R"));
            Assign(so, "lowerArmR", Bone(model, "lower_arm.R"));
            Assign(so, "thighL", Bone(model, "thigh.L"));
            Assign(so, "shinL", Bone(model, "shin.L"));
            Assign(so, "thighR", Bone(model, "thigh.R"));
            Assign(so, "shinR", Bone(model, "shin.R"));
            Assign(so, "face", face);
            Assign(so, "eyeL", eyeL);
            Assign(so, "eyeR", eyeR);
            Assign(so, "pupilL", pupilL);
            Assign(so, "pupilR", pupilR);
            Assign(so, "stalkL", stalkL);
            Assign(so, "stalkR", stalkR);
            Assign(so, "lashL", lashL);
            Assign(so, "lashR", lashR);
            Assign(so, "browL", browL);
            Assign(so, "browR", browR);
            Assign(so, "mouth", mouth);
            Assign(so, "cues", cues);
            Assign(so, "alertMark", alertMark);
            SerializedProperty sweatProperty = so.FindProperty("sweatDrops");
            sweatProperty.arraySize = drops.Length;
            for (int i = 0; i < drops.Length; i++) sweatProperty.GetArrayElementAtIndex(i).objectReferenceValue = drops[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if (saved == null) throw new InvalidOperationException("Could not save " + PrefabPath + ".");
            return saved;
        }

        private static Transform Part(string name, Transform parent, Mesh mesh, Material material)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Face parts are small and move every frame; they neither cast nor need to receive shadows.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
        }

        // An anchor IntroBoki scales (Z = half the length) over a cylinder turned onto Z.
        private static Transform Stalk(string name, Transform parent, Mesh cylinder, Material material)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            Transform body = Part("Stalk Body", anchor, cylinder, material);
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return anchor;
        }

        private static void Assign(SerializedObject so, string field, Transform value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException("IntroBoki has no field " + field + ".");
            property.objectReferenceValue = value;
        }

        private static Transform Bone(GameObject model, string name)
        {
            Transform bone = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (bone == null) throw new InvalidOperationException("CHR_Boki_BindPose has no bone named " + name + ".");
            return bone;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Missing " + Folder + "; copy CHR_Boki_BindPose.fbx and run output/intro-boki-v1/build_intro_atlas.py.");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(Folder, "Materials");
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(Folder, "Meshes");
        }

        // Same texture treatment as the player atlas: crisp pixels, no filtering blur, no compression.
        private static void ConfigureTextures()
        {
            AssetDatabase.ImportAsset(AtlasTexturePath);
            var importer = AssetImporter.GetAtPath(AtlasTexturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing " + AtlasTexturePath + ".");
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        // Copies the player atlas material so shader and surface settings match, and swaps in the intro texture.
        private static Material EnsureAtlasMaterial()
        {
            string path = MaterialFolder + "/Boki_Intro_Atlas.mat";
            var texture = Load<Texture2D>(AtlasTexturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Load<Material>(PlayerAtlasPath)) { name = "Boki_Intro_Atlas" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = texture;
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static Material EnsureMaterial(string folder, string name, Color color, float smoothness, bool unlit)
        {
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP shader is unavailable.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetColor("_BaseColor", color);
            if (!unlit) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Mesh assets are rewritten in place so their GUIDs, and every prefab using them, survive a rebuild.
        public static Mesh EnsureMesh(string name, Mesh built)
        {
            string path = MeshFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            built.name = name;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            EditorUtility.CopySerialized(built, existing);
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // Unit-diameter flat-shaded UV sphere: chunky facets that match the PS1 look of the model.
        public static Mesh BuildSphere(int slices, int stacks)
        {
            Vector3 Point(int stack, int slice)
            {
                float v = Mathf.PI * stack / stacks;
                float u = 2f * Mathf.PI * slice / slices;
                return 0.5f * new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
            }

            var triangles = new List<Vector3>();
            for (int i = 0; i < stacks; i++)
            {
                for (int j = 0; j < slices; j++)
                {
                    Vector3 a = Point(i, j), b = Point(i, j + 1), c = Point(i + 1, j + 1), d = Point(i + 1, j);
                    if (i > 0) AddTriangle(triangles, a, b, c, a + b + c);
                    if (i < stacks - 1) AddTriangle(triangles, a, c, d, a + c + d);
                }
            }
            return FlatMesh(triangles);
        }

        // Outward is only used to pick the winding, so callers need not get the vertex order right.
        public static void AddTriangle(List<Vector3> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) (b, c) = (c, b);
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        public static Mesh FlatMesh(List<Vector3> triangles)
        {
            var normals = new Vector3[triangles.Count];
            var indices = new int[triangles.Count];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 normal = Vector3.Cross(triangles[i + 1] - triangles[i], triangles[i + 2] - triangles[i]).normalized;
                normals[i] = normals[i + 1] = normals[i + 2] = normal;
                indices[i] = i;
                indices[i + 1] = i + 1;
                indices[i + 2] = i + 2;
            }
            var mesh = new Mesh { vertices = triangles.ToArray(), normals = normals, triangles = indices };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing " + typeof(T).Name + " at " + path + ".");
            return asset;
        }
    }
}
