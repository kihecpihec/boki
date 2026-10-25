using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    public static class EnemyModelSetup
    {
        public const string Art = "Assets/Art/Enemies/";
        private static readonly string[] Characters = { "CHR_Melee", "CHR_Guilt", "CHR_Professor" };
        private static readonly string[] Props = { "CHR_Watcher", "PROJ_WatcherOrb", "PROP_ExamPaper", "PROP_Lectern", "PROP_FloorRing" };
        private static readonly string[] LoopingClips =
            { "Melee_Idle", "Melee_Pursuit", "Guilt_Warning", "Guilt_Chase", "Professor_Idle" };

        [MenuItem("Boki/Enemy Models/Configure Imports")]
        public static void ConfigureImports()
        {
            RequireClean();
            foreach (string name in Characters) ConfigureModel(name, true);
            foreach (string name in Props) ConfigureModel(name, false);
            ConfigureTextures();
            AssetDatabase.SaveAssets();
            Debug.Log("Enemy model imports configured.");
        }

        [MenuItem("Boki/Enemy Models/Inspect Imports")]
        public static void Inspect()
        {
            var report = new StringBuilder();
            foreach (string name in Characters) Describe(report, name);
            foreach (string name in Props) Describe(report, name);
            Debug.Log(report.ToString());
        }

        private static void ConfigureModel(string name, bool character)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Art + "Export/" + name + ".fbx");
            if (importer == null) throw new InvalidOperationException("Missing model: " + name);
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            bool animated = character;
            importer.importAnimation = animated;
            importer.animationType = animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (animated)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.motionNodeName = "root";
                // Takes import as "CHR_Melee|Melee_Pursuit"; the clip keeps the part after the pipe.
                ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
                var clips = new ModelImporterClipAnimation[defaults.Length];
                for (int i = 0; i < defaults.Length; i++)
                {
                    ModelImporterClipAnimation clip = defaults[i];
                    int pipe = clip.takeName.IndexOf('|');
                    clip.name = pipe >= 0 ? clip.takeName.Substring(pipe + 1) : clip.takeName;
                    clip.loopTime = Array.IndexOf(LoopingClips, clip.name) >= 0;
                    clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                    clips[i] = clip;
                }
                importer.clipAnimations = clips;
            }
            importer.SaveAndReimport();
        }

        private static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art + "Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }

        private static void Describe(StringBuilder report, string name)
        {
            string path = Art + "Export/" + name + ".fbx";
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            report.AppendLine("== " + name);
            if (root == null) { report.AppendLine("  MISSING"); return; }
            report.AppendLine("  root scale " + root.transform.localScale + " rot " + root.transform.localEulerAngles);
            DescribeNode(report, root.transform, 1);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    report.AppendLine("  clip " + clip.name + " " + clip.length.ToString("0.00") + "s loop=" + clip.isLooping);
        }

        private static void DescribeNode(StringBuilder report, Transform node, int depth)
        {
            // Skip the 22-bone skeleton: only report mesh, socket and root nodes.
            var renderer = node.GetComponent<Renderer>();
            if (renderer != null || node.name.StartsWith("Socket") || depth <= 2)
            {
                report.Append(' ', depth * 2).Append(node.name);
                if (renderer != null) report.Append(" [").Append(renderer.GetType().Name).Append(" bounds ").Append(renderer.bounds.size).Append(']');
                report.AppendLine();
            }
            foreach (Transform child in node) DescribeNode(report, child, depth + 1);
        }

        private static void RequireClean()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before setting up enemy assets.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard dirty scenes before setting up enemy assets.");
        }
    }
}
