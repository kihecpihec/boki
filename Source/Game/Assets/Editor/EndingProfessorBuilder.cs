using System;
using System.Collections.Generic;
using System.Linq;
using Boki.Ending;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Builds Professor_Ending.prefab: the enemy professor's rig and mesh, driven by EndingProfessor, plus a cat's ears,
    // whiskers, nose and tongue as separate parts on the head. The enemy model, its prefab and its materials are not touched.
    public static class EndingProfessorBuilder
    {
        public const string Folder = "Assets/Art/Ending";
        public const string PrefabPath = Folder + "/Professor_Ending.prefab";
        private const string ModelPath = "Assets/Art/Enemies/Export/CHR_Professor.fbx";
        private const string BodyMaterialPath = "Assets/Art/Enemies/M_Professor.mat";
        private const string MaterialFolder = Folder + "/Materials";
        private const string MeshFolder = Folder + "/Meshes";

        // Face space origin, in character space: on the head axis at eye level (see EndingProfessor).
        private static readonly Vector3 FaceOrigin = new Vector3(0f, 1.648f, 0f);

        public static GameObject Build()
        {
            EnsureFolders();
            Mesh ear = EnsureMesh("EarPrism", BuildEarPrism());
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            Material fur = IntroBokiBuilder.EnsureMaterial(MaterialFolder, "Cat_Fur", new Color32(0x2B, 0x28, 0x33, 0xFF), 0.1f, false);
            Material inner = IntroBokiBuilder.EnsureMaterial(MaterialFolder, "Cat_EarInner", new Color32(0xE8, 0x8F, 0xA6, 0xFF), 0.1f, false);
            Material whisker = IntroBokiBuilder.EnsureMaterial(MaterialFolder, "Cat_Whisker", new Color32(0x1A, 0x19, 0x20, 0xFF), 0.1f, false);
            Material nose = IntroBokiBuilder.EnsureMaterial(MaterialFolder, "Cat_Nose", new Color32(0xF0, 0x7C, 0x98, 0xFF), 0.3f, false);
            Material tongue = IntroBokiBuilder.EnsureMaterial(MaterialFolder, "Cat_Tongue", new Color32(0xE0, 0x5E, 0x7C, 0xFF), 0.5f, false);
            Material mouth = IntroBokiBuilder.EnsureMaterial(MaterialFolder, "Cat_Mouth", new Color32(0x24, 0x0A, 0x10, 0xFF), 0.2f, false);

            var root = new GameObject("Professor Ending", typeof(EndingProfessor));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ModelPath), root.transform);
            model.transform.localPosition = Vector3.zero;
            var animator = model.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            ResetToBindPose(model);
            var body = Load<Material>(BodyMaterialPath);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = body;
                renderer.sharedMaterials = materials;
            }

            Transform head = Bone(model, "head");
            var face = new GameObject("Cat Face").transform;
            face.SetPositionAndRotation(root.transform.TransformPoint(FaceOrigin), root.transform.rotation);
            face.SetParent(head, true);

            // The ears grow out of the bald crown, tipped outward and a little forward.
            Transform earL = Ear("Ear L", face, ear, fur, inner, -0.058f, 8f, 18f);
            Transform earR = Ear("Ear R", face, ear, fur, inner, 0.058f, 8f, -18f);

            // Three whiskers a side, fanned from the cheeks. The anchor's Z axis is the whisker; its Z scale is the length.
            var whiskers = new Transform[6];
            float[] pitch = { -9f, 3f, 15f };
            float[] lift = { 0.011f, 0f, -0.011f };
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                for (int i = 0; i < 3; i++)
                {
                    var anchor = new GameObject((side == 0 ? "Whisker L " : "Whisker R ") + i).transform;
                    anchor.SetParent(face, false);
                    anchor.localPosition = new Vector3(s * 0.046f, -0.056f + lift[i], 0.058f);
                    anchor.localRotation = Quaternion.Euler(pitch[i], s * 62f, 0f);
                    anchor.localScale = new Vector3(1f, 1f, 0.15f);
                    Transform stem = Part("Whisker Body", anchor, cube, whisker);
                    stem.localPosition = new Vector3(0f, 0f, 0.5f);
                    stem.localScale = new Vector3(0.0045f, 0.0035f, 1f);
                    whiskers[side * 3 + i] = anchor;
                }
            }

            Transform catNose = Part("Cat Nose", face, cube, nose);
            catNose.localPosition = new Vector3(0f, -0.061f, 0.122f);
            catNose.localScale = new Vector3(0.028f, 0.02f, 0.022f);

            Transform catMouth = Part("Open Mouth", face, cube, mouth);
            catMouth.localPosition = new Vector3(0f, -0.108f, 0.062f);
            catMouth.localScale = new Vector3(0.046f, 0.006f, 0.006f);

            var tongueAnchor = new GameObject("Tongue").transform;
            tongueAnchor.SetParent(face, false);
            tongueAnchor.localPosition = new Vector3(0f, -0.123f, 0.06f);
            tongueAnchor.localScale = new Vector3(0.034f, 0.009f, 0.05f);
            Transform tongueBody = Part("Tongue Body", tongueAnchor, cube, tongue);
            tongueBody.localPosition = new Vector3(0f, 0f, 0.5f);

            var so = new SerializedObject(root.GetComponent<EndingProfessor>());
            Assign(so, "spine", Bone(model, "spine"));
            Assign(so, "chest", Bone(model, "chest"));
            Assign(so, "neck", Bone(model, "neck"));
            Assign(so, "head", head);
            Assign(so, "upperArmL", Bone(model, "upper_arm.L"));
            Assign(so, "lowerArmL", Bone(model, "lower_arm.L"));
            Assign(so, "handL", Bone(model, "hand.L"));
            Assign(so, "upperArmR", Bone(model, "upper_arm.R"));
            Assign(so, "lowerArmR", Bone(model, "lower_arm.R"));
            Assign(so, "face", face);
            Assign(so, "earL", earL);
            Assign(so, "earR", earR);
            Assign(so, "nose", catNose);
            Assign(so, "mouth", catMouth);
            Assign(so, "tongue", tongueAnchor);
            SerializedProperty whiskerProperty = so.FindProperty("whiskers");
            whiskerProperty.arraySize = whiskers.Length;
            for (int i = 0; i < whiskers.Length; i++) whiskerProperty.GetArrayElementAtIndex(i).objectReferenceValue = whiskers[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if (saved == null) throw new InvalidOperationException("Could not save " + PrefabPath + ".");
            return saved;
        }

        // The FBX ships in the lectern idle pose (stooped, hands forward). The cutscene poses are offsets from the modeled
        // standing pose, so every skinned bone is snapped to the pose the mesh was bound in before anything is attached.
        private static void ResetToBindPose(GameObject model)
        {
            var targets = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Matrix4x4[] bind = skin.sharedMesh.bindposes;
                Transform[] bones = skin.bones;
                for (int i = 0; i < bones.Length; i++)
                    if (bones[i] != null && !targets.ContainsKey(bones[i]))
                        targets[bones[i]] = skin.transform.localToWorldMatrix * bind[i].inverse;
            }
            // Parents first, so a child's world pose is not disturbed by a later parent change.
            foreach (var pair in targets.OrderBy(entry => Depth(entry.Key)))
            {
                pair.Key.SetPositionAndRotation(pair.Value.GetPosition(), pair.Value.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(pair.Key);
            }
        }

        private static int Depth(Transform transform)
        {
            int depth = 0;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent) depth++;
            return depth;
        }

        // Anchor at the base of the ear; roll leans the tip outward (+ for the left ear), pitch leans it forward.
        private static Transform Ear(string name, Transform face, Mesh mesh, Material fur, Material inner, float x, float pitch, float roll)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(face, false);
            anchor.localPosition = new Vector3(x, 0.093f, -0.006f);
            anchor.localRotation = Quaternion.Euler(pitch, 0f, roll);
            Transform outer = Part("Ear Outer", anchor, mesh, fur);
            outer.localPosition = new Vector3(0f, -0.012f, 0f);
            outer.localScale = new Vector3(0.074f, 0.115f, 0.03f);
            Transform lining = Part("Ear Inner", anchor, mesh, inner);
            lining.localPosition = new Vector3(0f, -0.006f, 0.016f);
            lining.localScale = new Vector3(0.044f, 0.075f, 0.01f);
            return anchor;
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

        // A tall isosceles triangle with thickness: unit width, unit height from y = 0 to 1, unit depth centered on z = 0.
        private static Mesh BuildEarPrism()
        {
            var a = new Vector3(-0.5f, 0f, 0.5f);
            var b = new Vector3(0.5f, 0f, 0.5f);
            var c = new Vector3(0f, 1f, 0.5f);
            Vector3 back = Vector3.back;
            var triangles = new List<Vector3>();
            IntroBokiBuilder.AddTriangle(triangles, a, b, c, Vector3.forward);
            IntroBokiBuilder.AddTriangle(triangles, a + back, b + back, c + back, Vector3.back);
            AddSide(triangles, a, b, Vector3.down);
            AddSide(triangles, b, c, new Vector3(1f, 0.5f, 0f));
            AddSide(triangles, c, a, new Vector3(-1f, 0.5f, 0f));
            return IntroBokiBuilder.FlatMesh(triangles);
        }

        private static void AddSide(List<Vector3> triangles, Vector3 from, Vector3 to, Vector3 outward)
        {
            Vector3 back = Vector3.back;
            IntroBokiBuilder.AddTriangle(triangles, from, to, to + back, outward);
            IntroBokiBuilder.AddTriangle(triangles, from, to + back, from + back, outward);
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

        private static void Assign(SerializedObject so, string field, Transform value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException("EndingProfessor has no field " + field + ".");
            property.objectReferenceValue = value;
        }

        private static Transform Bone(GameObject model, string name)
        {
            Transform bone = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (bone == null) throw new InvalidOperationException("CHR_Professor has no bone named " + name + ".");
            return bone;
        }

        public static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art", "Ending");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(Folder, "Materials");
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(Folder, "Meshes");
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing " + typeof(T).Name + " at " + path + ".");
            return asset;
        }
    }
}
